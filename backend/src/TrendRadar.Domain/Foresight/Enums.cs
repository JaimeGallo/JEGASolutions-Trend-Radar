namespace TrendRadar.Domain.Foresight;

public enum HypothesisStatus
{
    Open,
    Supported,
    Weakened,
    Refuted,
    Superseded,
}

/// <summary>"Vencida" no se guarda: es una predicción abierta cuya fecha límite ya pasó.</summary>
public enum PredictionStatus
{
    Open,
    Resolved,
    Withdrawn,
}

public enum PredictionOutcome
{
    Confirmed,
    Partial,
    Failed,
    Unresolvable,
}
