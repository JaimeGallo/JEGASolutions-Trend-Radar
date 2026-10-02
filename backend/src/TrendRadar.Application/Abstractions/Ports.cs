using TrendRadar.Domain.Auditing;
using TrendRadar.Domain.Auth;
using TrendRadar.Domain.Users;

namespace TrendRadar.Application.Abstractions;

public interface IUserStore
{
    Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken ct);

    Task<User?> FindByIdAsync(Guid id, CancellationToken ct);

    Task<bool> AnyAsync(CancellationToken ct);

    Task<IReadOnlyList<User>> ListAsync(CancellationToken ct);

    void Add(User user);
}

public interface IRefreshTokenStore
{
    Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct);

    Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken ct);

    void Add(RefreshToken token);
}

public interface IAuditStore
{
    void Add(AuditEntry entry);
}

/// <summary>Confirma en una sola transacción los cambios y sus entradas de auditoría.</summary>
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct);

    /// <summary>Ejecuta varias escrituras como una sola transacción: todas o ninguna.</summary>
    Task InTransactionAsync(Func<Task> work, CancellationToken ct);
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface IAccessTokenIssuer
{
    AccessToken Issue(User user);
}

public sealed record ChainReport(string Chain, long CheckedEntries, long? FirstInvalidEntryId)
{
    public bool IsValid => FirstInvalidEntryId is null;
}

public sealed record IntegrityReport(IReadOnlyList<ChainReport> Chains, DateTimeOffset VerifiedAt)
{
    public bool IsValid => Chains.All(c => c.IsValid);
}

/// <summary>Recalcula las cadenas de hashes: auditoría y versiones de señales (ARCHITECTURE §6, capa 3).</summary>
public interface IIntegrityVerifier
{
    Task<IntegrityReport> VerifyAsync(CancellationToken ct);
}

public sealed record AuditQuery(string? EntityType, string? EntityId, int Take);

public interface IAuditReader
{
    Task<IReadOnlyList<AuditEntry>> ListAsync(AuditQuery query, CancellationToken ct);
}

public interface ITaxonomyReader
{
    Task<IReadOnlyList<Domain.Taxonomy.SignalDomain>> ListDomainsAsync(CancellationToken ct);
}
