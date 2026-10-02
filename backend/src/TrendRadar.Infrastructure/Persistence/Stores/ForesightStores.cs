using Microsoft.EntityFrameworkCore;
using TrendRadar.Application.Foresight;
using TrendRadar.Domain.Common;
using TrendRadar.Domain.Foresight;

namespace TrendRadar.Infrastructure.Persistence.Stores;

internal sealed class ForesightStore(TrendRadarDbContext db) : IForesightStore
{
    public Task<Prediction?> FindPredictionAsync(string code, CancellationToken ct) =>
        db.Predictions.SingleOrDefaultAsync(p => p.Code == code, ct);

    public Task<Hypothesis?> FindHypothesisAsync(string code, CancellationToken ct) =>
        db.Hypotheses.SingleOrDefaultAsync(h => h.Code == code, ct);

    public Task<PredictionResolution?> LatestResolutionAsync(Guid predictionId, CancellationToken ct) =>
        db.PredictionResolutions.AsNoTracking()
            .Where(r => r.PredictionId == predictionId)
            .OrderByDescending(r => r.ResolvedAt).ThenByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);

    public void Add(Hypothesis hypothesis) => db.Hypotheses.Add(hypothesis);

    public void Add(Prediction prediction) => db.Predictions.Add(prediction);

    public void Add(PredictionResolution resolution) => db.PredictionResolutions.Add(resolution);
}

internal sealed class ForesightQueries(TrendRadarDbContext db, TimeProvider time) : IForesightQueries
{
    private DateOnly Today => DateOnly.FromDateTime(time.GetUtcNow().ToOffset(DateInterval.ColombiaOffset).DateTime);

    public async Task<PredictionPage> ListAsync(PredictionQuery query, CancellationToken ct)
    {
        var today = Today;
        var q = db.Predictions.AsNoTracking();
        if (query.Status is { } status)
        {
            q = q.Where(p => p.Status == status);
        }

        q = query.Due switch
        {
            PredictionDue.Overdue => q.Where(p => p.Status == PredictionStatus.Open && p.HorizonDate < today),
            PredictionDue.Soon => q.Where(p => p.Status == PredictionStatus.Open && p.HorizonDate >= today && p.HorizonDate <= today.AddDays(30)),
            _ => q,
        };

        if (!string.IsNullOrWhiteSpace(query.SignalCode))
        {
            q = q.Where(p => db.Signals.Any(s => s.Id == p.SignalId && s.SignalCode == query.SignalCode));
        }

        var total = await q.CountAsync(ct);
        var ordered = query.Due is null && query.Status is null or PredictionStatus.Open
            ? q.OrderBy(p => p.Status).ThenBy(p => p.HorizonDate)
            : q.OrderBy(p => p.HorizonDate);
        var items = await Summaries(ordered.Take(Math.Clamp(query.Take, 1, 500)), today).ToListAsync(ct);
        return new PredictionPage(items, total);
    }

    public async Task<PredictionDetailView?> GetAsync(string code, CancellationToken ct)
    {
        var p = await db.Predictions.AsNoTracking().SingleOrDefaultAsync(x => x.Code == code, ct);
        if (p is null)
        {
            return null;
        }

        var signal = await db.Signals.AsNoTracking().SingleAsync(s => s.Id == p.SignalId, ct);
        var signalTitle = await db.SignalVersions.AsNoTracking()
            .Where(v => v.SignalId == signal.Id && v.Version == signal.CurrentVersion).Select(v => v.Title).FirstOrDefaultAsync(ct)
            ?? signal.OriginalTitle;
        var hypothesis = p.HypothesisId is null ? null : await db.Hypotheses.AsNoTracking().SingleAsync(h => h.Id == p.HypothesisId, ct);
        var supersedes = p.SupersedesId is null ? null
            : await db.Predictions.AsNoTracking().Where(x => x.Id == p.SupersedesId).Select(x => x.Code).SingleAsync(ct);
        var supersededBy = await db.Predictions.AsNoTracking().Where(x => x.SupersedesId == p.Id).Select(x => x.Code!).ToListAsync(ct);
        var resolutions = await db.PredictionResolutions.AsNoTracking()
            .Where(r => r.PredictionId == p.Id).OrderBy(r => r.ResolvedAt).ThenBy(r => r.Id).ToListAsync(ct);
        var snapshots = await db.PredictionSnapshots.AsNoTracking().Where(s => s.PredictionId == p.Id).OrderBy(s => s.Id).ToListAsync(ct);

        var userIds = resolutions.Select(r => r.ResolvedBy).Append(p.RecordedBy).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);
        var current = resolutions.LastOrDefault();

        return new PredictionDetailView(
            p.Code!, signal.SignalCode!, signalTitle, hypothesis?.Code, hypothesis?.Statement,
            p.Statement, p.ResolutionCriteria, p.HorizonDate, p.Confidence, p.BaseRate, p.Specificity, p.EvidenceSnapshot,
            p.RecordedAt, names.GetValueOrDefault(p.RecordedBy, "?"), p.LockedAt, p.IsLocked(time.GetUtcNow()),
            p.Version, supersedes, supersededBy, p.Status, p.IsOverdue(Today), p.WithdrawnAt, p.WithdrawalReason,
            resolutions.Select(r => new ResolutionView(
                r.Id, r.Outcome, r.PartialCredit, r.Rationale, r.EvidenceUrl, r.ResolvedAt,
                names.GetValueOrDefault(r.ResolvedBy, "?"), r == current)).ToList(),
            snapshots.Select(s => new SnapshotView(s.CreatedAt, s.Snapshot, Convert.ToHexStringLower(s.ChainHash))).ToList());
    }

    public async Task<SignalForesightView> ForSignalAsync(string signalCode, CancellationToken ct)
    {
        var signalId = await db.Signals.AsNoTracking().Where(s => s.SignalCode == signalCode).Select(s => s.Id).SingleOrDefaultAsync(ct);
        var hypotheses = await db.Hypotheses.AsNoTracking().Where(h => h.SignalId == signalId).OrderBy(h => h.RecordedAt)
            .Select(h => new HypothesisView(
                h.Code!, h.Statement, h.Rationale, h.Status, h.RecordedAt,
                db.Predictions.Where(p => p.HypothesisId == h.Id).OrderBy(p => p.RecordedAt).Select(p => p.Code!).ToList()))
            .ToListAsync(ct);
        var predictions = await Summaries(
            db.Predictions.AsNoTracking().Where(p => p.SignalId == signalId).OrderBy(p => p.RecordedAt), Today).ToListAsync(ct);
        return new SignalForesightView(hypotheses, predictions);
    }

    private IQueryable<PredictionSummaryView> Summaries(IQueryable<Prediction> q, DateOnly today) => q.Select(p => new PredictionSummaryView(
        p.Code!,
        db.Signals.Where(s => s.Id == p.SignalId).Select(s => s.SignalCode!).First(),
        p.Statement,
        p.HorizonDate,
        p.Confidence,
        p.BaseRate,
        p.Specificity,
        p.Status,
        p.Status == PredictionStatus.Open && p.HorizonDate < today,
        p.Version,
        db.PredictionResolutions.Where(r => r.PredictionId == p.Id)
            .OrderByDescending(r => r.ResolvedAt).ThenByDescending(r => r.Id)
            .Select(r => (PredictionOutcome?)r.Outcome).FirstOrDefault(),
        p.RecordedAt));
}
