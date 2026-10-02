using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TrendRadar.Application.Abstractions;
using TrendRadar.Application.Auditing;
using TrendRadar.Domain.Auditing;
using TrendRadar.Domain.Auth;
using TrendRadar.Domain.Users;

namespace TrendRadar.Application.Auth;

public sealed record AuthTokens(AccessToken AccessToken, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt, User User);

public sealed record AuthResult(AuthTokens? Tokens, string? Error)
{
    public bool Succeeded => Tokens is not null;

    public static AuthResult Ok(AuthTokens tokens) => new(tokens, null);

    public static AuthResult Fail(string error) => new(null, error);
}

public sealed record CreateUserResult(User? User, string? Error);

public sealed class AuthService(
    IUserStore users,
    IRefreshTokenStore refreshTokens,
    IUnitOfWork unitOfWork,
    IPasswordHasher hasher,
    IAccessTokenIssuer accessTokens,
    AuditRecorder audit,
    TimeProvider time,
    IOptions<AuthOptions> options)
{
    public const string InvalidCredentials = "Correo o contraseña incorrectos.";
    public const string InvalidSession = "La sesión no es válida o expiró. Inicia sesión de nuevo.";

    // Hash de relleno para que un correo inexistente tarde lo mismo que una contraseña errada.
    private static string? s_dummyHash;

    private readonly AuthOptions _options = options.Value;

    public async Task<AuthResult> LoginAsync(string email, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
        {
            return AuthResult.Fail(InvalidCredentials);
        }

        var normalized = User.NormalizeEmail(email);
        var user = await users.FindByEmailAsync(normalized, ct);
        var passwordOk = hasher.Verify(password, user?.PasswordHash ?? DummyHash());

        if (user is null || !user.IsActive || !passwordOk)
        {
            audit.Record(user?.Id, AuditActions.LoginFailed, "user", user?.Id.ToString(), newValue: new { email = normalized });
            await unitOfWork.SaveChangesAsync(ct);
            return AuthResult.Fail(InvalidCredentials);
        }

        var tokens = IssueTokens(user);
        audit.Record(user.Id, AuditActions.Login, "user", user.Id.ToString());
        await unitOfWork.SaveChangesAsync(ct);
        return AuthResult.Ok(tokens);
    }

    public async Task<AuthResult> RefreshAsync(string? rawRefreshToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(rawRefreshToken))
        {
            return AuthResult.Fail(InvalidSession);
        }

        var now = time.GetUtcNow();
        var stored = await refreshTokens.FindByHashAsync(HashToken(rawRefreshToken), ct);
        if (stored is null)
        {
            return AuthResult.Fail(InvalidSession);
        }

        if (stored.IsRevoked)
        {
            // Un token ya rotado se volvió a usar: posible robo. Se cierran todas las sesiones del usuario.
            await refreshTokens.RevokeAllForUserAsync(stored.UserId, now, ct);
            audit.Record(stored.UserId, AuditActions.RefreshTokenReuse, "user", stored.UserId.ToString());
            await unitOfWork.SaveChangesAsync(ct);
            return AuthResult.Fail(InvalidSession);
        }

        var user = await users.FindByIdAsync(stored.UserId, ct);
        if (stored.IsExpired(now) || user is null || !user.IsActive)
        {
            return AuthResult.Fail(InvalidSession);
        }

        stored.Revoke(now);
        var tokens = IssueTokens(user);
        await unitOfWork.SaveChangesAsync(ct);
        return AuthResult.Ok(tokens);
    }

    public async Task LogoutAsync(string? rawRefreshToken, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(rawRefreshToken))
        {
            return;
        }

        var stored = await refreshTokens.FindByHashAsync(HashToken(rawRefreshToken), ct);
        if (stored is null || stored.IsRevoked)
        {
            return;
        }

        stored.Revoke(time.GetUtcNow());
        audit.Record(stored.UserId, AuditActions.Logout, "user", stored.UserId.ToString());
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<CreateUserResult> CreateUserAsync(
        Guid actorId, string email, string displayName, string password, UserRole role, CancellationToken ct)
    {
        if (role == UserRole.Owner)
        {
            return new(null, "Solo puede existir un propietario, creado al iniciar el sistema.");
        }

        return await CreateAsync(actorId, email, displayName, password, role, ct);
    }

    /// <summary>Crea el propietario inicial si la base de datos no tiene usuarios. Devuelve true si lo creó.</summary>
    public async Task<bool> BootstrapOwnerAsync(string email, string displayName, string password, CancellationToken ct)
    {
        if (await users.AnyAsync(ct))
        {
            return false;
        }

        var result = await CreateAsync(null, email, displayName, password, UserRole.Owner, ct);
        return result.Error is null
            ? true
            : throw new InvalidOperationException($"No se pudo crear el propietario inicial: {result.Error}");
    }

    public static string HashToken(string rawToken) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));

    private async Task<CreateUserResult> CreateAsync(
        Guid? actorId, string email, string displayName, string password, UserRole role, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@', StringComparison.Ordinal))
        {
            return new(null, "El correo no es válido.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            return new(null, "El nombre es obligatorio.");
        }

        if (string.IsNullOrEmpty(password) || password.Length < AuthOptions.MinPasswordLength)
        {
            return new(null, $"La contraseña debe tener al menos {AuthOptions.MinPasswordLength} caracteres.");
        }

        if (await users.FindByEmailAsync(User.NormalizeEmail(email), ct) is not null)
        {
            return new(null, "Ya existe un usuario con ese correo.");
        }

        var user = User.Create(email, displayName, hasher.Hash(password), role, time.GetUtcNow());
        users.Add(user);
        audit.Record(
            actorId ?? user.Id,
            AuditActions.Create,
            "user",
            user.Id.ToString(),
            newValue: new { user.Email, user.DisplayName, Role = user.Role.ToString() });
        await unitOfWork.SaveChangesAsync(ct);
        return new(user, null);
    }

    private AuthTokens IssueTokens(User user)
    {
        var now = time.GetUtcNow();
        var raw = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var refresh = RefreshToken.Create(user.Id, HashToken(raw), now, TimeSpan.FromDays(_options.RefreshTokenDays));
        refreshTokens.Add(refresh);
        return new AuthTokens(accessTokens.Issue(user), raw, refresh.ExpiresAt, user);
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private string DummyHash() => s_dummyHash ??= hasher.Hash(Convert.ToHexString(RandomNumberGenerator.GetBytes(16)));
}
