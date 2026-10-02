using TrendRadar.Application.Abstractions;
using TrendRadar.Application.Auditing;
using TrendRadar.Application.Common;
using TrendRadar.Application.Signals;
using TrendRadar.Domain.Auditing;
using TrendRadar.Domain.Common;
using TrendRadar.Domain.Foresight;

namespace TrendRadar.Application.Foresight;

public sealed class ForesightService(
    IForesightStore store,
    ISignalStore signals,
    IUnitOfWork unitOfWork,
    AuditRecorder audit,
    TimeProvider time)
{
    public const int MaxText = 2000;

    /// <summary>Hoy en Colombia: las fechas límite son días locales.</summary>
    public DateOnly Today => DateOnly.FromDateTime(time.GetUtcNow().ToOffset(DateInterval.ColombiaOffset).DateTime);

    public async Task<Result<string>> CreateHypothesisAsync(string signalCode, CreateHypothesis c, Guid userId, CancellationToken ct)
    {
        var signal = await signals.FindByCodeAsync(signalCode, ct);
        if (signal is null)
        {
            return Result.Fail<string>($"No existe la señal {signalCode}.", ErrorKind.NotFound);
        }

        if (Text(c.Statement, 10) is null || c.Rationale is { Length: > MaxText })
        {
            return Result.Fail<string>($"Escribe la hipótesis (entre 10 y {MaxText} caracteres).");
        }

        var h = Hypothesis.Create(signal.Id, c.Statement, c.Rationale, userId, time.GetUtcNow());
        await unitOfWork.InTransactionAsync(
            async () =>
            {
                store.Add(h);
                await unitOfWork.SaveChangesAsync(ct);
                audit.Record(userId, AuditActions.Create, "hypothesis", h.Code, newValue: new { h.Code, SignalCode = signalCode, h.Statement });
                await unitOfWork.SaveChangesAsync(ct);
            },
            ct);
        return Result.Ok(h.Code!);
    }

    public async Task<Result<bool>> ChangeHypothesisStatusAsync(string code, ChangeHypothesisStatus c, Guid userId, CancellationToken ct)
    {
        var h = await store.FindHypothesisAsync(code, ct);
        if (h is null)
        {
            return Result.Fail<bool>($"No existe la hipótesis {code}.", ErrorKind.NotFound);
        }

        if (Text(c.Reason, 5) is null)
        {
            return Result.Fail<bool>("Explica el motivo del cambio (mínimo 5 caracteres).");
        }

        if (h.Status == c.Status)
        {
            return Result.Fail<bool>("La hipótesis ya tiene ese estado.");
        }

        var before = h.Status;
        h.ChangeStatus(c.Status);
        audit.Record(userId, AuditActions.StatusChange, "hypothesis", code,
            new { Status = before.ToString() }, new { Status = c.Status.ToString() }, c.Reason.Trim());
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Ok(true);
    }

    public async Task<Result<string>> CreatePredictionAsync(string signalCode, PredictionInput input, Guid userId, CancellationToken ct)
    {
        var signal = await signals.FindByCodeAsync(signalCode, ct);
        if (signal is null)
        {
            return Result.Fail<string>($"No existe la señal {signalCode}.", ErrorKind.NotFound);
        }

        Guid? hypothesisId = null;
        if (!string.IsNullOrWhiteSpace(input.HypothesisCode))
        {
            var h = await store.FindHypothesisAsync(input.HypothesisCode, ct);
            if (h is null || h.SignalId != signal.Id)
            {
                return Result.Fail<string>("La hipótesis no pertenece a esta señal.");
            }

            hypothesisId = h.Id;
        }

        var draft = ToDraft(signal.Id, hypothesisId, input);
        if (Validate(draft) is { } invalid)
        {
            return Result.Fail<string>(invalid);
        }

        var p = Prediction.Create(draft, userId, time.GetUtcNow());
        await SaveNewAsync(p, userId, AuditActions.Create, null, ct);
        return Result.Ok(p.Code!);
    }

    /// <summary>Corrección dentro de los 15 minutos de gracia. Cada corrección queda en el historial.</summary>
    public async Task<Result<bool>> CorrectAsync(string code, PredictionInput input, Guid userId, CancellationToken ct)
    {
        var p = await store.FindPredictionAsync(code, ct);
        if (p is null)
        {
            return Result.Fail<bool>($"No existe la predicción {code}.", ErrorKind.NotFound);
        }

        if (p.IsLocked(time.GetUtcNow()))
        {
            return Result.Fail<bool>(
                "La predicción ya está bloqueada: pasaron los 15 minutos de gracia. Si cambiaste de opinión, crea una versión nueva.",
                ErrorKind.Conflict);
        }

        if (Text(input.Reason, 5) is null)
        {
            return Result.Fail<bool>("Explica el motivo de la corrección (mínimo 5 caracteres).");
        }

        var draft = ToDraft(p.SignalId, p.HypothesisId, input);
        if (Validate(draft) is { } invalid)
        {
            return Result.Fail<bool>(invalid);
        }

        if (draft == p.AsDraft())
        {
            return Result.Fail<bool>("La corrección no cambia nada.");
        }

        var before = p.AsDraft();
        p.Correct(draft);
        audit.Record(userId, AuditActions.Correct, "prediction", code, before, draft, input.Reason!.Trim());
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Ok(true);
    }

    /// <summary>Versión nueva tras cambiar de opinión. La anterior sigue abierta y cuenta en las métricas.</summary>
    public async Task<Result<string>> SupersedeAsync(string code, PredictionInput input, Guid userId, CancellationToken ct)
    {
        var old = await store.FindPredictionAsync(code, ct);
        if (old is null)
        {
            return Result.Fail<string>($"No existe la predicción {code}.", ErrorKind.NotFound);
        }

        if (old.Status != PredictionStatus.Open)
        {
            return Result.Fail<string>("Solo se puede crear una versión nueva de una predicción abierta.");
        }

        if (Text(input.Reason, 5) is null)
        {
            return Result.Fail<string>("Explica qué te hizo cambiar de opinión (mínimo 5 caracteres).");
        }

        var draft = ToDraft(old.SignalId, old.HypothesisId, input);
        if (Validate(draft) is { } invalid)
        {
            return Result.Fail<string>(invalid);
        }

        var p = Prediction.Create(draft, userId, time.GetUtcNow(), old.Id);
        await SaveNewAsync(p, userId, AuditActions.Version, input.Reason!.Trim(), ct, supersedes: code);
        return Result.Ok(p.Code!);
    }

    public async Task<Result<bool>> ResolveAsync(string code, ResolvePrediction r, Guid userId, CancellationToken ct)
    {
        var p = await store.FindPredictionAsync(code, ct);
        if (p is null)
        {
            return Result.Fail<bool>($"No existe la predicción {code}.", ErrorKind.NotFound);
        }

        if (p.Status == PredictionStatus.Withdrawn)
        {
            return Result.Fail<bool>("La predicción fue retirada; no se resuelve.");
        }

        if (Text(r.Rationale, 10) is null)
        {
            return Result.Fail<bool>("Explica cómo se aplicó el criterio de resolución (mínimo 10 caracteres).");
        }

        if (r.Outcome == PredictionOutcome.Partial && r.PartialCredit is not (>= 0.05m and <= 0.95m))
        {
            return Result.Fail<bool>("Un resultado parcial necesita un crédito entre 0,05 y 0,95.");
        }

        if (r.EvidenceUrl is { Length: > 0 } url && (!Uri.TryCreate(url, UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttps && u.Scheme != Uri.UriSchemeHttp)))
        {
            return Result.Fail<bool>("La URL de evidencia debe empezar por http:// o https://.");
        }

        var previous = await store.LatestResolutionAsync(p.Id, ct);
        var resolution = PredictionResolution.Create(p.Id, r.Outcome, r.PartialCredit, r.Rationale, r.EvidenceUrl, userId, time.GetUtcNow(), previous?.Id);
        store.Add(resolution);
        audit.Record(
            userId,
            previous is null ? AuditActions.Resolve : AuditActions.Dispute,
            "prediction",
            code,
            previous is null ? null : new { Outcome = previous.Outcome.ToString(), previous.PartialCredit },
            new { Outcome = r.Outcome.ToString(), resolution.PartialCredit, r.EvidenceUrl },
            r.Rationale.Trim());
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Ok(true);
    }

    public async Task<Result<bool>> WithdrawAsync(string code, WithdrawPrediction w, Guid userId, CancellationToken ct)
    {
        var p = await store.FindPredictionAsync(code, ct);
        if (p is null)
        {
            return Result.Fail<bool>($"No existe la predicción {code}.", ErrorKind.NotFound);
        }

        if (p.Status != PredictionStatus.Open)
        {
            return Result.Fail<bool>("Solo se puede retirar una predicción abierta.");
        }

        if (Text(w.Reason, 10) is null)
        {
            return Result.Fail<bool>("Explica por qué la retiras (mínimo 10 caracteres). Quedará visible y contará en las métricas.");
        }

        p.Withdraw(w.Reason, time.GetUtcNow());
        audit.Record(userId, AuditActions.Withdraw, "prediction", code, reason: w.Reason.Trim());
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Ok(true);
    }

    private async Task SaveNewAsync(Prediction p, Guid userId, string action, string? reason, CancellationToken ct, string? supersedes = null) =>
        await unitOfWork.InTransactionAsync(
            async () =>
            {
                store.Add(p);
                await unitOfWork.SaveChangesAsync(ct);
                audit.Record(userId, action, "prediction", p.Code, newValue: new
                {
                    p.Code,
                    p.Statement,
                    p.ResolutionCriteria,
                    p.HorizonDate,
                    p.Confidence,
                    p.BaseRate,
                    p.Specificity,
                    Supersedes = supersedes,
                }, reason: reason);
                await unitOfWork.SaveChangesAsync(ct);
            },
            ct);

    private static PredictionDraft ToDraft(Guid signalId, Guid? hypothesisId, PredictionInput i) => new(
        signalId, hypothesisId, i.Statement ?? string.Empty, i.ResolutionCriteria ?? string.Empty, i.HorizonDate,
        i.Confidence, i.BaseRate, i.Specificity, string.IsNullOrWhiteSpace(i.EvidenceSnapshot) ? null : i.EvidenceSnapshot.Trim());

    private string? Validate(PredictionDraft d)
    {
        if (Text(d.Statement, 10) is null)
        {
            return $"El enunciado es obligatorio (entre 10 y {MaxText} caracteres).";
        }

        if (Text(d.ResolutionCriteria, 10) is null)
        {
            return "Escribe el criterio de resolución: cómo sabremos si ocurrió (mínimo 10 caracteres).";
        }

        if (d.HorizonDate <= Today || d.HorizonDate > Today.AddYears(10))
        {
            return "La fecha límite debe ser posterior a hoy y no más de 10 años adelante.";
        }

        if (d.Confidence is < 1 or > 99 || d.BaseRate is < 1 or > 99)
        {
            return "Confianza y tasa base van de 1 a 99 (nunca 0 ni 100).";
        }

        if (d.Specificity is < 1 or > 5)
        {
            return "La especificidad va de 1 a 5.";
        }

        return d.EvidenceSnapshot is { Length: > MaxText * 2 } ? "El resumen de evidencia es demasiado largo." : null;
    }

    private static string? Text(string? s, int min) =>
        string.IsNullOrWhiteSpace(s) || s.Trim().Length < min || s.Length > MaxText ? null : s;
}
