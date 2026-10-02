using TrendRadar.Application.Abstractions;
using TrendRadar.Domain.Auditing;
using TrendRadar.Domain.Auth;
using TrendRadar.Domain.Users;

namespace TrendRadar.Domain.Tests;

internal sealed class FakeStore : IUserStore, IRefreshTokenStore, IAuditStore, IUnitOfWork
{
    public List<User> Users { get; } = [];

    public List<RefreshToken> Tokens { get; } = [];

    public List<AuditEntry> Audit { get; } = [];

    public int Saves { get; private set; }

    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken ct) =>
        Task.FromResult(Users.SingleOrDefault(u => u.Email == normalizedEmail));

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct) =>
        Task.FromResult(Users.SingleOrDefault(u => u.Id == id));

    public Task<bool> AnyAsync(CancellationToken ct) => Task.FromResult(Users.Count > 0);

    public Task<IReadOnlyList<User>> ListAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<User>>(Users);

    public void Add(User user) => Users.Add(user);

    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct) =>
        Task.FromResult(Tokens.SingleOrDefault(t => t.TokenHash == tokenHash));

    public Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        foreach (var t in Tokens.Where(t => t.UserId == userId))
        {
            t.Revoke(now);
        }

        return Task.CompletedTask;
    }

    public void Add(RefreshToken token) => Tokens.Add(token);

    public void Add(AuditEntry entry) => Audit.Add(entry);

    public Task SaveChangesAsync(CancellationToken ct)
    {
        Saves++;
        return Task.CompletedTask;
    }

    public Task InTransactionAsync(Func<Task> work, CancellationToken ct) => work();
}

internal sealed class FakeHasher : IPasswordHasher
{
    public string Hash(string password) => "hash:" + password;

    public bool Verify(string password, string passwordHash) => passwordHash == "hash:" + password;
}

internal sealed class FakeIssuer(TimeProvider time) : IAccessTokenIssuer
{
    public AccessToken Issue(User user) => new($"access:{user.Id}", time.GetUtcNow().AddMinutes(15));
}
