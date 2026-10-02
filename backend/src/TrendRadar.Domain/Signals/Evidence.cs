namespace TrendRadar.Domain.Signals;

/// <summary>
/// Evidencia asociada a una señal. Append-only: una corrección es una evidencia nueva.
/// </summary>
public sealed class Evidence
{
    private Evidence()
    {
    }

    public Guid Id { get; private set; }

    public Guid SignalId { get; private set; }

    public EvidenceKind Kind { get; private set; }

    public EvidenceRole Role { get; private set; }

    public string Description { get; private set; } = string.Empty;

    public string? Url { get; private set; }

    public string? GitRepo { get; private set; }

    public string? GitCommit { get; private set; }

    public string? FileName { get; private set; }

    public string? FileSha256 { get; private set; }

    public string? FileMime { get; private set; }

    public long? FileSize { get; private set; }

    /// <summary>Fecha que la evidencia demuestra (declarada por quien la registra).</summary>
    public DateTimeOffset? ArtifactTimestamp { get; private set; }

    public TimestampAuthority TimestampAuthority { get; private set; }

    public EvidenceLevel Level { get; private set; }

    public DateTimeOffset RecordedAt { get; private set; }

    public Guid RecordedBy { get; private set; }

    public static Evidence Create(Guid signalId, EvidenceDraft d, StoredFile? file, Guid by, DateTimeOffset now)
    {
        // Sin fecha que demostrar, la evidencia no aporta a la cronología.
        var authority = d.ArtifactTimestamp is null ? TimestampAuthority.None : d.TimestampAuthority;
        return new Evidence
        {
            Id = Guid.CreateVersion7(now),
            SignalId = signalId,
            Kind = d.Kind,
            Role = d.Role,
            Description = d.Description.Trim(),
            Url = Clean(d.Url),
            GitRepo = Clean(d.GitRepo),
            GitCommit = Clean(d.GitCommit),
            FileName = file?.FileName,
            FileSha256 = file?.Sha256,
            FileMime = file?.Mime,
            FileSize = file?.Size,
            ArtifactTimestamp = d.ArtifactTimestamp,
            TimestampAuthority = authority,
            Level = EvidenceRules.LevelFor(authority),
            RecordedBy = by,
        };
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}

public sealed record EvidenceDraft(
    EvidenceKind Kind,
    EvidenceRole Role,
    string Description,
    string? Url,
    string? GitRepo,
    string? GitCommit,
    DateTimeOffset? ArtifactTimestamp,
    TimestampAuthority TimestampAuthority);

public sealed record StoredFile(string FileName, string Sha256, string Mime, long Size);
