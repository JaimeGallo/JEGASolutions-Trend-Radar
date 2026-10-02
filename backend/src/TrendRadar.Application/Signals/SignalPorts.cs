using TrendRadar.Domain.Signals;
using TrendRadar.Domain.Taxonomy;

namespace TrendRadar.Application.Signals;

public interface ISignalStore
{
    Task<Signal?> FindByCodeAsync(string code, CancellationToken ct);

    Task<SignalVersion?> CurrentVersionAsync(Guid signalId, CancellationToken ct);

    Task<bool> CodeExistsAsync(string code, CancellationToken ct);

    void Add(Signal signal);

    void Add(SignalVersion version);

    void Add(Evidence evidence);
}

public interface IDomainLookup
{
    Task<SignalDomain?> FindByCodeAsync(string code, CancellationToken ct);

    Task<SignalDomain?> FindByIdAsync(Guid id, CancellationToken ct);
}

/// <summary>Almacén de archivos de evidencia direccionado por contenido (SHA-256).</summary>
public interface IEvidenceFileStore
{
    Task<(string Sha256, long Size)> SaveAsync(Stream content, CancellationToken ct);

    Stream? OpenRead(string sha256);
}

public interface ISignalQueries
{
    Task<SignalPage> ListAsync(SignalQuery query, CancellationToken ct);

    Task<SignalDetailView?> GetAsync(string code, CancellationToken ct);

    Task<EvidenceView?> GetEvidenceAsync(Guid id, CancellationToken ct);
}
