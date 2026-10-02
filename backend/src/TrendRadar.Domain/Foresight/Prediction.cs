namespace TrendRadar.Domain.Foresight;

/// <summary>
/// Afirmación medible con fecha límite, registrada antes del resultado (DATA_MODEL §4).
/// Se puede corregir solo durante el período de gracia; después la base de datos rechaza
/// cualquier cambio de contenido. Cambiar de opinión crea una versión nueva que no
/// reemplaza a la anterior: ambas se resuelven por separado.
/// </summary>
public sealed class Prediction
{
    public static readonly TimeSpan GracePeriod = TimeSpan.FromMinutes(15);

    private Prediction()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>PR-NNNN, asignado por la base de datos.</summary>
    public string? Code { get; private set; }

    public Guid SignalId { get; private set; }

    public Guid? HypothesisId { get; private set; }

    public string Statement { get; private set; } = string.Empty;

    /// <summary>Cómo se decidirá si ocurrió, escrito antes de saberlo.</summary>
    public string ResolutionCriteria { get; private set; } = string.Empty;

    public DateOnly HorizonDate { get; private set; }

    /// <summary>Probabilidad (1 a 99) de que ocurra, según quien predice.</summary>
    public int Confidence { get; private set; }

    /// <summary>Probabilidad (1 a 99) de que ocurra "de todos modos"; base del JAI.</summary>
    public int BaseRate { get; private set; }

    /// <summary>1 a 5 según la rúbrica de especificidad (CONVERGENCE_MODEL §3).</summary>
    public int Specificity { get; private set; }

    /// <summary>Resumen de la evidencia disponible al predecir.</summary>
    public string? EvidenceSnapshot { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public Guid RecordedBy { get; private set; }

    /// <summary>Fin del período de gracia; lo fija la base de datos.</summary>
    public DateTimeOffset LockedAt { get; private set; }

    public Guid? SupersedesId { get; private set; }

    /// <summary>1 para la original; la base de datos numera las versiones siguientes.</summary>
    public int Version { get; private set; }

    public PredictionStatus Status { get; private set; }

    public DateTimeOffset? WithdrawnAt { get; private set; }

    public string? WithdrawalReason { get; private set; }

    public static Prediction Create(PredictionDraft d, Guid by, DateTimeOffset now, Guid? supersedesId = null) => new()
    {
        Id = Guid.CreateVersion7(now),
        SignalId = d.SignalId,
        HypothesisId = d.HypothesisId,
        Statement = d.Statement.Trim(),
        ResolutionCriteria = d.ResolutionCriteria.Trim(),
        HorizonDate = d.HorizonDate,
        Confidence = d.Confidence,
        BaseRate = d.BaseRate,
        Specificity = d.Specificity,
        EvidenceSnapshot = string.IsNullOrWhiteSpace(d.EvidenceSnapshot) ? null : d.EvidenceSnapshot.Trim(),
        RecordedBy = by,
        SupersedesId = supersedesId,
        Status = PredictionStatus.Open,
    };

    public bool IsLocked(DateTimeOffset now) => now >= LockedAt;

    public bool IsOverdue(DateOnly today) => Status == PredictionStatus.Open && HorizonDate < today;

    /// <summary>Corrección dentro del período de gracia (errores de digitación, precisiones).</summary>
    public void Correct(PredictionDraft d)
    {
        Statement = d.Statement.Trim();
        ResolutionCriteria = d.ResolutionCriteria.Trim();
        HorizonDate = d.HorizonDate;
        Confidence = d.Confidence;
        BaseRate = d.BaseRate;
        Specificity = d.Specificity;
        EvidenceSnapshot = string.IsNullOrWhiteSpace(d.EvidenceSnapshot) ? null : d.EvidenceSnapshot.Trim();
    }

    public void Withdraw(string reason, DateTimeOffset now)
    {
        Status = PredictionStatus.Withdrawn;
        WithdrawnAt = now;
        WithdrawalReason = reason.Trim();
    }

    public PredictionDraft AsDraft() => new(
        SignalId, HypothesisId, Statement, ResolutionCriteria, HorizonDate, Confidence, BaseRate, Specificity, EvidenceSnapshot);
}

public sealed record PredictionDraft(
    Guid SignalId,
    Guid? HypothesisId,
    string Statement,
    string ResolutionCriteria,
    DateOnly HorizonDate,
    int Confidence,
    int BaseRate,
    int Specificity,
    string? EvidenceSnapshot);

/// <summary>
/// Resolución append-only. Disputar una resolución es registrar otra que la reemplaza;
/// la anterior se conserva.
/// </summary>
public sealed class PredictionResolution
{
    private PredictionResolution()
    {
    }

    public Guid Id { get; private set; }

    public Guid PredictionId { get; private set; }

    public PredictionOutcome Outcome { get; private set; }

    /// <summary>Crédito parcial (0,05 a 0,95), solo para resultados parciales.</summary>
    public decimal? PartialCredit { get; private set; }

    public string Rationale { get; private set; } = string.Empty;

    public string? EvidenceUrl { get; private set; }

    public DateTimeOffset ResolvedAt { get; private set; }

    public Guid ResolvedBy { get; private set; }

    public Guid? SupersedesId { get; private set; }

    public static PredictionResolution Create(
        Guid predictionId, PredictionOutcome outcome, decimal? partialCredit, string rationale, string? evidenceUrl,
        Guid by, DateTimeOffset now, Guid? supersedesId) => new()
    {
        Id = Guid.CreateVersion7(now),
        PredictionId = predictionId,
        Outcome = outcome,
        PartialCredit = outcome == PredictionOutcome.Partial ? partialCredit : null,
        Rationale = rationale.Trim(),
        EvidenceUrl = string.IsNullOrWhiteSpace(evidenceUrl) ? null : evidenceUrl.Trim(),
        ResolvedBy = by,
        SupersedesId = supersedesId,
    };
}

/// <summary>Foto de cada estado de una predicción, escrita por trigger y encadenada con SHA-256.</summary>
public sealed class PredictionSnapshot
{
    private PredictionSnapshot()
    {
    }

    public long Id { get; private set; }

    public Guid PredictionId { get; private set; }

    public string Snapshot { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public byte[] ContentHash { get; private set; } = [];

    public byte[] ChainHash { get; private set; } = [];
}
