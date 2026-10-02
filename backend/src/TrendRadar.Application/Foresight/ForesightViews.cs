using TrendRadar.Domain.Foresight;

namespace TrendRadar.Application.Foresight;

public enum PredictionDue
{
    /// <summary>Abiertas con fecha límite vencida.</summary>
    Overdue,

    /// <summary>Abiertas que vencen en los próximos 30 días.</summary>
    Soon,
}

public sealed record PredictionQuery(
    PredictionStatus? Status = null,
    PredictionDue? Due = null,
    string? SignalCode = null,
    int Take = 100);

public sealed record PredictionSummaryView(
    string Code,
    string SignalCode,
    string Statement,
    DateOnly HorizonDate,
    int Confidence,
    int BaseRate,
    int Specificity,
    PredictionStatus Status,
    bool IsOverdue,
    int Version,
    PredictionOutcome? Outcome,
    DateTimeOffset RecordedAt);

public sealed record PredictionPage(IReadOnlyList<PredictionSummaryView> Items, int Total);

public sealed record ResolutionView(
    Guid Id,
    PredictionOutcome Outcome,
    decimal? PartialCredit,
    string Rationale,
    string? EvidenceUrl,
    DateTimeOffset ResolvedAt,
    string ResolvedBy,
    bool IsCurrent);

public sealed record SnapshotView(DateTimeOffset CreatedAt, string Snapshot, string ChainHash);

public sealed record PredictionDetailView(
    string Code,
    string SignalCode,
    string SignalTitle,
    string? HypothesisCode,
    string? HypothesisStatement,
    string Statement,
    string ResolutionCriteria,
    DateOnly HorizonDate,
    int Confidence,
    int BaseRate,
    int Specificity,
    string? EvidenceSnapshot,
    DateTimeOffset RecordedAt,
    string RecordedBy,
    DateTimeOffset LockedAt,
    bool IsLocked,
    int Version,
    string? SupersedesCode,
    IReadOnlyList<string> SupersededBy,
    PredictionStatus Status,
    bool IsOverdue,
    DateTimeOffset? WithdrawnAt,
    string? WithdrawalReason,
    IReadOnlyList<ResolutionView> Resolutions,
    IReadOnlyList<SnapshotView> Snapshots);

public sealed record HypothesisView(
    string Code,
    string Statement,
    string? Rationale,
    HypothesisStatus Status,
    DateTimeOffset RecordedAt,
    IReadOnlyList<string> PredictionCodes);

public sealed record SignalForesightView(IReadOnlyList<HypothesisView> Hypotheses, IReadOnlyList<PredictionSummaryView> Predictions);
