using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TrendRadar.Application.Auditing;
using TrendRadar.Application.Auth;
using TrendRadar.Domain.Auditing;
using TrendRadar.Domain.Users;

namespace TrendRadar.Domain.Tests;

public sealed class AuthServiceTests
{
    private const string Password = "contraseña-segura-123";

    private readonly FakeStore _store = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 2, 12, 0, 0, TimeSpan.Zero));
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        _auth = new AuthService(
            _store, _store, _store, new FakeHasher(), new FakeIssuer(_time), new AuditRecorder(_store), _time,
            Options.Create(new AuthOptions { JwtSecret = new string('x', 32), RefreshTokenDays = 14 }));
    }

    private async Task<User> OwnerAsync()
    {
        Assert.True(await _auth.BootstrapOwnerAsync("Jaime@Example.com ", "Jaime", Password, default));
        return _store.Users.Single();
    }

    [Fact]
    public async Task Bootstrap_crea_un_unico_propietario_y_lo_audita()
    {
        var owner = await OwnerAsync();

        Assert.Equal("jaime@example.com", owner.Email);
        Assert.Equal(UserRole.Owner, owner.Role);
        Assert.False(await _auth.BootstrapOwnerAsync("otro@example.com", "Otro", Password, default));
        Assert.Single(_store.Users);
        var entry = Assert.Single(_store.Audit);
        Assert.Equal(AuditActions.Create, entry.Action);
        Assert.DoesNotContain("hash", entry.NewValue, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Login_correcto_emite_tokens_y_audita()
    {
        var owner = await OwnerAsync();

        var result = await _auth.LoginAsync("JAIME@example.com", Password, default);

        Assert.True(result.Succeeded);
        Assert.Equal($"access:{owner.Id}", result.Tokens!.AccessToken.Value);
        var stored = Assert.Single(_store.Tokens);
        Assert.Equal(AuthService.HashToken(result.Tokens.RefreshToken), stored.TokenHash);
        Assert.NotEqual(result.Tokens.RefreshToken, stored.TokenHash);
        Assert.Equal(_time.GetUtcNow().AddDays(14), stored.ExpiresAt);
        Assert.Equal(AuditActions.Login, _store.Audit[^1].Action);
    }

    [Theory]
    [InlineData("jaime@example.com", "incorrecta-123456")]
    [InlineData("nadie@example.com", Password)]
    [InlineData("", Password)]
    public async Task Login_fallido_no_emite_tokens(string email, string password)
    {
        await OwnerAsync();

        var result = await _auth.LoginAsync(email, password, default);

        Assert.False(result.Succeeded);
        Assert.Equal(AuthService.InvalidCredentials, result.Error);
        Assert.Empty(_store.Tokens);
    }

    [Fact]
    public async Task Login_fallido_queda_auditado()
    {
        await OwnerAsync();

        await _auth.LoginAsync("jaime@example.com", "incorrecta-123456", default);

        Assert.Equal(AuditActions.LoginFailed, _store.Audit[^1].Action);
    }

    [Fact]
    public async Task Usuario_inactivo_no_puede_iniciar_sesion()
    {
        var owner = await OwnerAsync();
        owner.Deactivate();

        Assert.False((await _auth.LoginAsync("jaime@example.com", Password, default)).Succeeded);
    }

    [Fact]
    public async Task Refresh_rota_el_token()
    {
        await OwnerAsync();
        var login = await _auth.LoginAsync("jaime@example.com", Password, default);

        var refreshed = await _auth.RefreshAsync(login.Tokens!.RefreshToken, default);

        Assert.True(refreshed.Succeeded);
        Assert.NotEqual(login.Tokens.RefreshToken, refreshed.Tokens!.RefreshToken);
        Assert.Equal(2, _store.Tokens.Count);
        Assert.True(_store.Tokens[0].IsRevoked);
        Assert.False(_store.Tokens[1].IsRevoked);
    }

    [Fact]
    public async Task Reusar_un_token_rotado_revoca_todas_las_sesiones()
    {
        await OwnerAsync();
        var login = await _auth.LoginAsync("jaime@example.com", Password, default);
        await _auth.RefreshAsync(login.Tokens!.RefreshToken, default);

        var reuse = await _auth.RefreshAsync(login.Tokens.RefreshToken, default);

        Assert.False(reuse.Succeeded);
        Assert.All(_store.Tokens, t => Assert.True(t.IsRevoked));
        Assert.Equal(AuditActions.RefreshTokenReuse, _store.Audit[^1].Action);
    }

    [Fact]
    public async Task Refresh_expirado_falla()
    {
        await OwnerAsync();
        var login = await _auth.LoginAsync("jaime@example.com", Password, default);
        _time.Advance(TimeSpan.FromDays(14));

        Assert.False((await _auth.RefreshAsync(login.Tokens!.RefreshToken, default)).Succeeded);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("token-inventado")]
    public async Task Refresh_desconocido_falla(string? token)
    {
        var result = await _auth.RefreshAsync(token, default);

        Assert.False(result.Succeeded);
        Assert.Equal(AuthService.InvalidSession, result.Error);
    }

    [Fact]
    public async Task Logout_revoca_el_token()
    {
        await OwnerAsync();
        var login = await _auth.LoginAsync("jaime@example.com", Password, default);

        await _auth.LogoutAsync(login.Tokens!.RefreshToken, default);

        Assert.True(_store.Tokens.Single().IsRevoked);
        Assert.False((await _auth.RefreshAsync(login.Tokens.RefreshToken, default)).Succeeded);
    }

    [Fact]
    public async Task Crear_usuario_valida_datos_y_rechaza_un_segundo_propietario()
    {
        var owner = await OwnerAsync();

        Assert.NotNull((await _auth.CreateUserAsync(owner.Id, "rev@example.com", "Revisor", Password, UserRole.Reviewer, default)).User);
        Assert.NotNull((await _auth.CreateUserAsync(owner.Id, "otro@example.com", "Otro", Password, UserRole.Owner, default)).Error);
        Assert.NotNull((await _auth.CreateUserAsync(owner.Id, "REV@example.com", "Duplicado", Password, UserRole.Viewer, default)).Error);
        Assert.NotNull((await _auth.CreateUserAsync(owner.Id, "corta@example.com", "Corta", "123", UserRole.Viewer, default)).Error);
        Assert.NotNull((await _auth.CreateUserAsync(owner.Id, "sin-arroba", "X", Password, UserRole.Viewer, default)).Error);
        Assert.Equal(2, _store.Users.Count);
        Assert.Equal(owner.Id, _store.Audit[^1].UserId);
    }
}
