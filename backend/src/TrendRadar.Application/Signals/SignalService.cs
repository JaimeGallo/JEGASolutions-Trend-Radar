using System.Text.RegularExpressions;
using TrendRadar.Application.Abstractions;
using TrendRadar.Application.Auditing;
using TrendRadar.Application.Common;
using TrendRadar.Domain.Auditing;
using TrendRadar.Domain.Common;
using TrendRadar.Domain.Signals;
using TrendRadar.Domain.Taxonomy;

namespace TrendRadar.Application.Signals;

public sealed partial class SignalService(
    ISignalStore signals,
    IDomainLookup domains,
    IEvidenceFileStore files,
    IUnitOfWork unitOfWork,
    AuditRecorder audit,
    TimeProvider time)
{
    public const int MaxTitle = 200;
    public const int MaxText = 20_000;
    public const long MaxFileBytes = 20 * 1024 * 1024;

    public static readonly IReadOnlyDictionary<string, string> AllowedFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".gif"] = "image/gif",
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain",
        [".md"] = "text/markdown",
        [".csv"] = "text/csv",
        [".json"] = "application/json",
        [".ots"] = "application/vnd.opentimestamps",
    };

    public async Task<Result<string>> CaptureAsync(CaptureSignal c, Guid userId, CancellationToken ct)
    {
        if (ValidateContent(c.Title, c.Text, c.Context, c.Confidence) is { } invalid)
        {
            return Result.Fail<string>(invalid);
        }

        DateInterval? claimed = null;
        if (!string.IsNullOrWhiteSpace(c.ClaimedOrigin))
        {
            claimed = DateInterval.TryParse(c.ClaimedOrigin);
            if (claimed is null)
            {
                return Result.Fail<string>(
                    "No reconozco la fecha de origen. Usa 2025, 2025-Q1, 2025-03 o 2025-03-14; agrega \"aprox\" si no es exacta.");
            }

            if (claimed.Value.Earliest > time.GetUtcNow())
            {
                return Result.Fail<string>("El origen declarado no puede estar en el futuro.");
            }
        }

        var (domain, subdomain, error) = await ResolveDomainAsync(c.DomainCode, c.SubdomainCode, ct);
        if (error is not null)
        {
            return Result.Fail<string>(error);
        }

        var signal = Signal.Capture(
            new SignalDraft(c.Title, c.Text, c.Context, domain!.Id, subdomain?.Id, c.Stage, c.Confidence, c.SourceType,
                c.SourceReference, claimed, claimed is null ? null : c.ClaimedOrigin, c.TimeZone),
            userId,
            time.GetUtcNow());
        return await SaveNewAsync(signal, userId, null, ct);
    }

    public async Task<Result<string>> ImportAsync(ImportSignal i, Guid userId, CancellationToken ct)
    {
        if (ValidateContent(i.Title, i.Text, i.Context, i.Confidence) is { } invalid)
        {
            return Result.Fail<string>(invalid);
        }

        var match = CodePattern().Match(i.Code ?? string.Empty);
        if (!match.Success || !string.Equals(match.Groups[1].Value, i.DomainCode, StringComparison.Ordinal))
        {
            return Result.Fail<string>("El código debe tener la forma TR-DOMINIO-NNN y coincidir con el dominio.");
        }

        if (await signals.CodeExistsAsync(i.Code!, ct))
        {
            return Result.Fail<string>($"La señal {i.Code} ya existe.", ErrorKind.Conflict);
        }

        var (domain, _, error) = await ResolveDomainAsync(i.DomainCode, null, ct);
        if (error is not null)
        {
            return Result.Fail<string>(error);
        }

        // Si el origen declarado no es una fecha ("pendiente"), se conserva solo como nota.
        var claimed = DateInterval.TryParse(i.ClaimedOrigin);
        var signal = Signal.Capture(
            new SignalDraft(i.Title, i.Text, i.Context, domain!.Id, null, SignalStage.Intuition, i.Confidence, i.SourceType,
                null, claimed, i.ClaimedOrigin, null, i.Retrospective, i.Code, i.ImportedFrom),
            userId,
            time.GetUtcNow());
        return await SaveNewAsync(signal, userId, i.ImportedFrom, ct);
    }

    public async Task<Result<int>> ReviseAsync(string code, ReviseSignal r, Guid userId, CancellationToken ct)
    {
        var signal = await signals.FindByCodeAsync(code, ct);
        if (signal is null)
        {
            return Result.Fail<int>($"No existe la señal {code}.", ErrorKind.NotFound);
        }

        if (ValidateContent(r.Title, r.Text, r.Context, r.Confidence) is { } invalid)
        {
            return Result.Fail<int>(invalid);
        }

        if (ValidateReason(r.Reason) is { } badReason)
        {
            return Result.Fail<int>(badReason);
        }

        var current = await signals.CurrentVersionAsync(signal.Id, ct);
        if (current is not null && current.Title == r.Title.Trim() && current.Text == r.Text.Trim()
            && current.Context == (string.IsNullOrWhiteSpace(r.Context) ? null : r.Context.Trim())
            && current.Confidence == r.Confidence)
        {
            return Result.Fail<int>("La revisión no cambia nada respecto a la versión actual.");
        }

        signals.Add(SignalVersion.Revision(signal.Id, r.Title, r.Text, r.Context, r.Confidence, r.Reason, userId));
        audit.Record(userId, AuditActions.Version, "signal", code, newValue: new { r.Title, r.Confidence }, reason: r.Reason.Trim());
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Ok<int>((current?.Version ?? 0) + 1);
    }

    public async Task<Result<bool>> ReclassifyAsync(string code, ReclassifySignal r, Guid userId, CancellationToken ct)
    {
        var signal = await signals.FindByCodeAsync(code, ct);
        if (signal is null)
        {
            return Result.Fail<bool>($"No existe la señal {code}.", ErrorKind.NotFound);
        }

        if (ValidateReason(r.Reason) is { } badReason)
        {
            return Result.Fail<bool>(badReason);
        }

        if (r.ImpactEstimate is < 1 or > 5 || r.RelevanceToJegas is < 1 or > 5)
        {
            return Result.Fail<bool>("Impacto y relevancia van de 1 a 5.");
        }

        Guid? subdomainId = null;
        if (!string.IsNullOrWhiteSpace(r.SubdomainCode))
        {
            var sub = await domains.FindByCodeAsync(r.SubdomainCode, ct);
            if (sub is null || sub.ParentId != signal.DomainId)
            {
                return Result.Fail<bool>("El subdominio no pertenece al dominio de la señal.");
            }

            subdomainId = sub.Id;
        }

        var before = signal.Classification;
        var after = new Domain.Signals.SignalClassification(
            r.Stage, r.Status, subdomainId, Clean(r.SourceReference), Clean(r.GeographicScope),
            r.ImpactEstimate, r.RelevanceToJegas, r.Confidentiality);
        if (before == after)
        {
            return Result.Fail<bool>("No hay cambios en la clasificación.");
        }

        signal.Reclassify(after);
        audit.Record(userId, AuditActions.Reclassify, "signal", code, before, after, r.Reason.Trim());
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Ok<bool>(true);
    }

    public async Task<Result<Guid>> AddEvidenceAsync(string code, AddEvidence e, UploadedFile? file, Guid userId, CancellationToken ct)
    {
        var signal = await signals.FindByCodeAsync(code, ct);
        if (signal is null)
        {
            return Result.Fail<Guid>($"No existe la señal {code}.", ErrorKind.NotFound);
        }

        if (ValidateEvidence(e, file) is { } invalid)
        {
            return Result.Fail<Guid>(invalid);
        }

        StoredFile? stored = null;
        if (file is not null)
        {
            var extension = Path.GetExtension(file.FileName);
            var (sha, size) = await files.SaveAsync(file.Content, ct);
            stored = new StoredFile(SafeFileName(file.FileName), sha, AllowedFiles[extension], size);
        }

        var evidence = Evidence.Create(
            signal.Id,
            new EvidenceDraft(e.Kind, e.Role, e.Description, e.Url, e.GitRepo, e.GitCommit, e.ArtifactTimestamp, e.TimestampAuthority),
            stored,
            userId,
            time.GetUtcNow());
        signals.Add(evidence);
        audit.Record(userId, AuditActions.AddEvidence, "signal", code, newValue: new
        {
            evidence.Id,
            Kind = evidence.Kind.ToString(),
            Role = evidence.Role.ToString(),
            Level = evidence.Level.ToString(),
            evidence.FileSha256,
            evidence.Url,
            evidence.GitCommit,
        });
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Ok<Guid>(evidence.Id);
    }

    private async Task<Result<string>> SaveNewAsync(Signal signal, Guid userId, string? importedFrom, CancellationToken ct)
    {
        signals.Add(signal);
        signals.Add(SignalVersion.Original(signal));
        await unitOfWork.SaveChangesAsync(ct);

        // El código lo asigna la base de datos al insertar; se audita ya con su valor final.
        audit.Record(
            userId,
            importedFrom is null ? AuditActions.Create : AuditActions.Import,
            "signal",
            signal.SignalCode,
            newValue: new { signal.SignalCode, signal.OriginalTitle, signal.RecordedAt, ImportedFrom = importedFrom });
        await unitOfWork.SaveChangesAsync(ct);
        return Result.Ok<string>(signal.SignalCode!);
    }

    private async Task<(SignalDomain? Domain, SignalDomain? Subdomain, string? Error)> ResolveDomainAsync(
        string domainCode, string? subdomainCode, CancellationToken ct)
    {
        var domain = string.IsNullOrWhiteSpace(domainCode) ? null : await domains.FindByCodeAsync(domainCode, ct);
        if (domain is null || domain.ParentId is not null || !domain.IsActive)
        {
            return (null, null, "Elige un dominio válido de primer nivel.");
        }

        if (string.IsNullOrWhiteSpace(subdomainCode))
        {
            return (domain, null, null);
        }

        var sub = await domains.FindByCodeAsync(subdomainCode, ct);
        return sub is null || sub.ParentId != domain.Id
            ? (null, null, "El subdominio no pertenece al dominio elegido.")
            : (domain, sub, null);
    }

    private static string? ValidateContent(string title, string text, string? context, int? confidence)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > MaxTitle)
        {
            return $"El título es obligatorio y tiene máximo {MaxTitle} caracteres.";
        }

        if (string.IsNullOrWhiteSpace(text) || text.Length > MaxText)
        {
            return $"El texto original es obligatorio y tiene máximo {MaxText} caracteres.";
        }

        if (context is { Length: > MaxText })
        {
            return $"El contexto tiene máximo {MaxText} caracteres.";
        }

        return confidence is < 0 or > 100 ? "La confianza va de 0 a 100." : null;
    }

    private static string? ValidateReason(string reason) =>
        string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 5
            ? "Explica el motivo del cambio (mínimo 5 caracteres)."
            : null;

    private string? ValidateEvidence(AddEvidence e, UploadedFile? file)
    {
        if (string.IsNullOrWhiteSpace(e.Description))
        {
            return "Describe qué demuestra esta evidencia.";
        }

        if (e.ArtifactTimestamp > time.GetUtcNow().AddMinutes(5))
        {
            return "La fecha de la evidencia no puede estar en el futuro.";
        }

        if (e.ArtifactTimestamp is not null && e.TimestampAuthority == TimestampAuthority.None)
        {
            return "Indica quién fija la fecha de la evidencia.";
        }

        return e.Kind switch
        {
            EvidenceKind.Url when !Uri.TryCreate(e.Url, UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttps && u.Scheme != Uri.UriSchemeHttp)
                => "La URL debe empezar por http:// o https://.",
            EvidenceKind.GitCommit when string.IsNullOrWhiteSpace(e.GitRepo) || !CommitPattern().IsMatch(e.GitCommit ?? string.Empty)
                => "Indica el repositorio y el hash del commit (7 a 40 caracteres hexadecimales).",
            EvidenceKind.File when file is null => "Adjunta el archivo.",
            EvidenceKind.File when file!.Length is 0 or > MaxFileBytes => "El archivo debe pesar entre 1 byte y 20 MB.",
            EvidenceKind.File when !AllowedFiles.ContainsKey(Path.GetExtension(file!.FileName))
                => "Tipo de archivo no permitido. Usa imágenes, PDF, texto, Markdown, CSV, JSON u .ots.",
            not EvidenceKind.File when file is not null => "Solo la evidencia de tipo archivo lleva adjunto.",
            _ => null,
        };
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string SafeFileName(string name)
    {
        var clean = new string(Path.GetFileName(name).Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '_').ToArray());
        return clean.Length > 120 ? clean[^120..] : clean;
    }

    [GeneratedRegex(@"^TR-([A-Z]+)-\d{3,}$")]
    private static partial Regex CodePattern();

    [GeneratedRegex(@"^[0-9a-fA-F]{7,40}$")]
    private static partial Regex CommitPattern();
}
