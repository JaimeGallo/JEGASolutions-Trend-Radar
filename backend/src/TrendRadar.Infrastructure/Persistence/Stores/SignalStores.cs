using Microsoft.EntityFrameworkCore;
using TrendRadar.Application.Signals;
using TrendRadar.Domain.Signals;
using TrendRadar.Domain.Taxonomy;

namespace TrendRadar.Infrastructure.Persistence.Stores;

internal sealed class SignalStore(TrendRadarDbContext db) : ISignalStore
{
    public Task<Signal?> FindByCodeAsync(string code, CancellationToken ct) =>
        db.Signals.SingleOrDefaultAsync(s => s.SignalCode == code, ct);

    public Task<SignalVersion?> CurrentVersionAsync(Guid signalId, CancellationToken ct) =>
        db.SignalVersions.AsNoTracking().Where(v => v.SignalId == signalId).OrderByDescending(v => v.Version).FirstOrDefaultAsync(ct);

    public Task<bool> CodeExistsAsync(string code, CancellationToken ct) => db.Signals.AnyAsync(s => s.SignalCode == code, ct);

    public void Add(Signal signal) => db.Signals.Add(signal);

    public void Add(SignalVersion version) => db.SignalVersions.Add(version);

    public void Add(Evidence evidence) => db.Evidence.Add(evidence);
}

internal sealed class DomainLookup(TrendRadarDbContext db) : IDomainLookup
{
    public Task<SignalDomain?> FindByCodeAsync(string code, CancellationToken ct) =>
        db.Domains.AsNoTracking().SingleOrDefaultAsync(d => d.Code == code, ct);

    public Task<SignalDomain?> FindByIdAsync(Guid id, CancellationToken ct) =>
        db.Domains.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, ct);
}
