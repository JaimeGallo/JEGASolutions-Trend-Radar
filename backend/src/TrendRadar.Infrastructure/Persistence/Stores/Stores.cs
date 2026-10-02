using Microsoft.EntityFrameworkCore;
using TrendRadar.Application.Abstractions;
using TrendRadar.Domain.Auditing;
using TrendRadar.Domain.Auth;
using TrendRadar.Domain.Taxonomy;
using TrendRadar.Domain.Users;

namespace TrendRadar.Infrastructure.Persistence.Stores;

internal sealed class UserStore(TrendRadarDbContext db) : IUserStore
{
    public Task<User?> FindByEmailAsync(string normalizedEmail, CancellationToken ct) =>
        db.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail, ct);

    public Task<User?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);

    public Task<bool> AnyAsync(CancellationToken ct) => db.Users.AnyAsync(ct);

    public async Task<IReadOnlyList<User>> ListAsync(CancellationToken ct) =>
        await db.Users.AsNoTracking().OrderBy(u => u.CreatedAt).ToListAsync(ct);

    public void Add(User user) => db.Users.Add(user);
}

internal sealed class RefreshTokenStore(TrendRadarDbContext db) : IRefreshTokenStore
{
    public Task<RefreshToken?> FindByHashAsync(string tokenHash, CancellationToken ct) =>
        db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task RevokeAllForUserAsync(Guid userId, DateTimeOffset now, CancellationToken ct)
    {
        var active = await db.RefreshTokens.Where(t => t.UserId == userId && t.RevokedAt == null).ToListAsync(ct);
        foreach (var token in active)
        {
            token.Revoke(now);
        }
    }

    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);
}

internal sealed class AuditStore(TrendRadarDbContext db) : IAuditStore, IAuditReader
{
    public void Add(AuditEntry entry) => db.AuditLog.Add(entry);

    public async Task<IReadOnlyList<AuditEntry>> ListAsync(AuditQuery query, CancellationToken ct)
    {
        var q = db.AuditLog.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            q = q.Where(a => a.EntityType == query.EntityType);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityId))
        {
            q = q.Where(a => a.EntityId == query.EntityId);
        }

        return await q.OrderByDescending(a => a.Id).Take(Math.Clamp(query.Take, 1, 500)).ToListAsync(ct);
    }
}

internal sealed class TaxonomyReader(TrendRadarDbContext db) : ITaxonomyReader
{
    public async Task<IReadOnlyList<SignalDomain>> ListDomainsAsync(CancellationToken ct) =>
        await db.Domains.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Code).ToListAsync(ct);
}

internal sealed class IntegrityVerifier(TrendRadarDbContext db, TimeProvider time) : IIntegrityVerifier
{
    public async Task<IntegrityReport> VerifyAsync(CancellationToken ct)
    {
        var audit = await db.Database
            .SqlQuery<VerifyRow>($"SELECT checked_entries, first_invalid_id FROM audit_log_verify()")
            .SingleAsync(ct);
        var versions = await db.Database
            .SqlQuery<VerifyRow>($"SELECT checked_entries, first_invalid_id FROM signal_versions_verify()")
            .SingleAsync(ct);
        return new IntegrityReport(
            [
                new ChainReport("audit_log", audit.CheckedEntries, audit.FirstInvalidId),
                new ChainReport("signal_versions", versions.CheckedEntries, versions.FirstInvalidId),
            ],
            time.GetUtcNow());
    }

    private sealed record VerifyRow(long CheckedEntries, long? FirstInvalidId);
}
