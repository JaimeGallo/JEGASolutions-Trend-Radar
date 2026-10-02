using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrendRadar.Application.Common;
using TrendRadar.Application.Foresight;
using TrendRadar.Domain.Foresight;

namespace TrendRadar.API.Controllers;

[ApiController]
[Authorize]
public sealed class ForesightController(ForesightService service, IForesightQueries queries) : ControllerBase
{
    [HttpGet("api/predictions")]
    public Task<PredictionPage> List(
        [FromQuery] PredictionStatus? status,
        [FromQuery] PredictionDue? due,
        [FromQuery] string? signal,
        [FromQuery] int take = 100,
        CancellationToken ct = default) =>
        queries.ListAsync(new PredictionQuery(status, due, signal, take), ct);

    [HttpGet("api/predictions/{code}")]
    public async Task<ActionResult<PredictionDetailView>> Get(string code, CancellationToken ct) =>
        await queries.GetAsync(code, ct) is { } p ? p : NotFoundProblem(code);

    [HttpGet("api/signals/{code}/foresight")]
    public Task<SignalForesightView> ForSignal(string code, CancellationToken ct) => queries.ForSignalAsync(code, ct);

    [HttpPost("api/signals/{code}/hypotheses")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<SignalForesightView>> CreateHypothesis(string code, CreateHypothesis request, CancellationToken ct)
    {
        var r = await service.CreateHypothesisAsync(code, request, User.GetUserId(), ct);
        return r.Succeeded ? StatusCode(StatusCodes.Status201Created, await queries.ForSignalAsync(code, ct)) : Fail(r);
    }

    [HttpPut("api/hypotheses/{code}/status")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<IActionResult> ChangeHypothesisStatus(string code, ChangeHypothesisStatus request, CancellationToken ct)
    {
        var r = await service.ChangeHypothesisStatusAsync(code, request, User.GetUserId(), ct);
        return r.Succeeded ? NoContent() : Fail(r);
    }

    [HttpPost("api/signals/{code}/predictions")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<PredictionDetailView>> CreatePrediction(string code, PredictionInput request, CancellationToken ct)
    {
        var r = await service.CreatePredictionAsync(code, request, User.GetUserId(), ct);
        return r.Succeeded ? StatusCode(StatusCodes.Status201Created, await queries.GetAsync(r.Value!, ct)) : Fail(r);
    }

    /// <summary>Corrección dentro del período de gracia de 15 minutos.</summary>
    [HttpPut("api/predictions/{code}")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<PredictionDetailView>> Correct(string code, PredictionInput request, CancellationToken ct)
    {
        var r = await service.CorrectAsync(code, request, User.GetUserId(), ct);
        return r.Succeeded ? (await queries.GetAsync(code, ct))! : Fail(r);
    }

    [HttpPost("api/predictions/{code}/versions")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<PredictionDetailView>> Supersede(string code, PredictionInput request, CancellationToken ct)
    {
        var r = await service.SupersedeAsync(code, request, User.GetUserId(), ct);
        return r.Succeeded ? StatusCode(StatusCodes.Status201Created, await queries.GetAsync(r.Value!, ct)) : Fail(r);
    }

    [HttpPost("api/predictions/{code}/resolution")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<PredictionDetailView>> Resolve(string code, ResolvePrediction request, CancellationToken ct)
    {
        var r = await service.ResolveAsync(code, request, User.GetUserId(), ct);
        return r.Succeeded ? (await queries.GetAsync(code, ct))! : Fail(r);
    }

    [HttpPost("api/predictions/{code}/withdraw")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<PredictionDetailView>> Withdraw(string code, WithdrawPrediction request, CancellationToken ct)
    {
        var r = await service.WithdrawAsync(code, request, User.GetUserId(), ct);
        return r.Succeeded ? (await queries.GetAsync(code, ct))! : Fail(r);
    }

    private ObjectResult NotFoundProblem(string code) =>
        Problem(title: $"No existe la predicción {code}.", statusCode: StatusCodes.Status404NotFound);

    private ObjectResult Fail<T>(Result<T> result) => Problem(
        title: result.Error,
        statusCode: result.Kind switch
        {
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        });
}
