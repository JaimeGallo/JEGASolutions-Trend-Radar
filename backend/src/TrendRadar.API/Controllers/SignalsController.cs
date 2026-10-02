using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrendRadar.Application.Common;
using TrendRadar.Application.Signals;
using TrendRadar.Domain.Signals;

namespace TrendRadar.API.Controllers;

[ApiController]
[Route("api/signals")]
[Authorize]
public sealed class SignalsController(SignalService service, ISignalQueries queries) : ControllerBase
{
    [HttpGet]
    public Task<SignalPage> List(
        [FromQuery] string? q,
        [FromQuery] string? domain,
        [FromQuery] SignalStage? stage,
        [FromQuery] SignalStatus? status,
        [FromQuery] bool? retrospective,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default) =>
        queries.ListAsync(new SignalQuery(q, domain, stage, status, retrospective, skip, take), ct);

    [HttpGet("{code}")]
    public async Task<ActionResult<SignalDetailView>> Get(string code, CancellationToken ct) =>
        await queries.GetAsync(code, ct) is { } detail ? detail : NotFoundProblem(code);

    [HttpPost]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<SignalDetailView>> Capture(CaptureSignal request, CancellationToken ct)
    {
        var result = await service.CaptureAsync(request, User.GetUserId(), ct);
        return result.Succeeded ? await Created(result.Value!, ct) : Fail(result);
    }

    /// <summary>Importa una señal de la Fase 0 (signals/*.md) conservando su código.</summary>
    [HttpPost("import")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<SignalDetailView>> Import(ImportSignal request, CancellationToken ct)
    {
        var result = await service.ImportAsync(request, User.GetUserId(), ct);
        return result.Succeeded ? await Created(result.Value!, ct) : Fail(result);
    }

    [HttpPost("{code}/versions")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<SignalDetailView>> Revise(string code, ReviseSignal request, CancellationToken ct)
    {
        var result = await service.ReviseAsync(code, request, User.GetUserId(), ct);
        return result.Succeeded ? await Detail(code, ct) : Fail(result);
    }

    [HttpPut("{code}/classification")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<SignalDetailView>> Reclassify(string code, ReclassifySignal request, CancellationToken ct)
    {
        var result = await service.ReclassifyAsync(code, request, User.GetUserId(), ct);
        return result.Succeeded ? await Detail(code, ct) : Fail(result);
    }

    [HttpPost("{code}/evidence")]
    [Authorize(Policy = Policies.Owner)]
    public async Task<ActionResult<SignalDetailView>> AddEvidence(string code, AddEvidence request, CancellationToken ct)
    {
        var result = await service.AddEvidenceAsync(code, request, null, User.GetUserId(), ct);
        return result.Succeeded ? await Detail(code, ct) : Fail(result);
    }

    [HttpPost("{code}/evidence/file")]
    [Authorize(Policy = Policies.Owner)]
    [RequestSizeLimit(SignalService.MaxFileBytes + (1024 * 1024))]
    [RequestFormLimits(MultipartBodyLengthLimit = SignalService.MaxFileBytes + (1024 * 1024))]
    public async Task<ActionResult<SignalDetailView>> AddFileEvidence(string code, [FromForm] FileEvidenceForm form, CancellationToken ct)
    {
        if (form.File is null)
        {
            return Problem(title: "Adjunta el archivo.", statusCode: StatusCodes.Status400BadRequest);
        }

        await using var stream = form.File.OpenReadStream();
        var result = await service.AddEvidenceAsync(
            code,
            new AddEvidence(EvidenceKind.File, form.Role, form.Description ?? string.Empty, null, null, null,
                form.ArtifactTimestamp, form.TimestampAuthority),
            new UploadedFile(form.File.FileName, form.File.ContentType, form.File.Length, stream),
            User.GetUserId(),
            ct);
        return result.Succeeded ? await Detail(code, ct) : Fail(result);
    }

    private async Task<ActionResult<SignalDetailView>> Created(string code, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, (await queries.GetAsync(code, ct))!);

    private async Task<ActionResult<SignalDetailView>> Detail(string code, CancellationToken ct) => (await queries.GetAsync(code, ct))!;

    private ObjectResult NotFoundProblem(string code) =>
        Problem(title: $"No existe la señal {code}.", statusCode: StatusCodes.Status404NotFound);

    private ObjectResult Fail<T>(Result<T> result) => Problem(
        title: result.Error,
        statusCode: result.Kind switch
        {
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        });
}

public sealed class FileEvidenceForm
{
    public IFormFile? File { get; set; }

    public EvidenceRole Role { get; set; } = EvidenceRole.Supports;

    public string? Description { get; set; }

    public DateTimeOffset? ArtifactTimestamp { get; set; }

    public TimestampAuthority TimestampAuthority { get; set; } = TimestampAuthority.None;
}

[ApiController]
[Route("api/evidence")]
[Authorize]
public sealed class EvidenceController(ISignalQueries queries, IEvidenceFileStore files) : ControllerBase
{
    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var evidence = await queries.GetEvidenceAsync(id, ct);
        if (evidence?.FileSha256 is not { } sha || files.OpenRead(sha) is not { } stream)
        {
            return Problem(title: "Archivo no encontrado.", statusCode: StatusCodes.Status404NotFound);
        }

        Response.Headers.XContentTypeOptions = "nosniff";
        var mime = evidence.FileMime ?? "application/octet-stream";
        var inline = mime.StartsWith("image/", StringComparison.Ordinal) && mime != "image/svg+xml";
        return inline
            ? File(stream, mime)
            : File(stream, mime, evidence.FileName ?? sha, enableRangeProcessing: false);
    }
}
