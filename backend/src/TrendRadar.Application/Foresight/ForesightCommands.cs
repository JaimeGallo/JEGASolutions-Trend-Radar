using TrendRadar.Domain.Foresight;

namespace TrendRadar.Application.Foresight;

public sealed record CreateHypothesis(string Statement, string? Rationale);

public sealed record ChangeHypothesisStatus(HypothesisStatus Status, string Reason);

public sealed record PredictionInput(
    string Statement,
    string ResolutionCriteria,
    DateOnly HorizonDate,
    int Confidence,
    int BaseRate,
    int Specificity,
    string? EvidenceSnapshot = null,
    string? HypothesisCode = null,
    string? Reason = null);

public sealed record ResolvePrediction(PredictionOutcome Outcome, decimal? PartialCredit, string Rationale, string? EvidenceUrl);

public sealed record WithdrawPrediction(string Reason);
