using TrendRadar.Domain.Foresight;

namespace TrendRadar.Application.Foresight;

public interface IForesightStore
{
    Task<Prediction?> FindPredictionAsync(string code, CancellationToken ct);

    Task<Hypothesis?> FindHypothesisAsync(string code, CancellationToken ct);

    Task<PredictionResolution?> LatestResolutionAsync(Guid predictionId, CancellationToken ct);

    void Add(Hypothesis hypothesis);

    void Add(Prediction prediction);

    void Add(PredictionResolution resolution);
}

public interface IForesightQueries
{
    Task<PredictionPage> ListAsync(PredictionQuery query, CancellationToken ct);

    Task<PredictionDetailView?> GetAsync(string code, CancellationToken ct);

    Task<SignalForesightView> ForSignalAsync(string signalCode, CancellationToken ct);
}
