namespace TrendRadar.Domain.Foresight;

/// <summary>
/// Explicación propuesta o desarrollo futuro derivado de una señal. El enunciado es inmutable;
/// solo cambia su estado a medida que llega evidencia.
/// </summary>
public sealed class Hypothesis
{
    private Hypothesis()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>HY-NNNN, asignado por la base de datos.</summary>
    public string? Code { get; private set; }

    public Guid SignalId { get; private set; }

    public string Statement { get; private set; } = string.Empty;

    public string? Rationale { get; private set; }

    public HypothesisStatus Status { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public Guid RecordedBy { get; private set; }

    public static Hypothesis Create(Guid signalId, string statement, string? rationale, Guid by, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        SignalId = signalId,
        Statement = statement.Trim(),
        Rationale = string.IsNullOrWhiteSpace(rationale) ? null : rationale.Trim(),
        Status = HypothesisStatus.Open,
        RecordedBy = by,
    };

    public void ChangeStatus(HypothesisStatus status) => Status = status;
}
