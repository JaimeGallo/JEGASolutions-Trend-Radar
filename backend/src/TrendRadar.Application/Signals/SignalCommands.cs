using TrendRadar.Domain.Signals;

namespace TrendRadar.Application.Signals;

public sealed record CaptureSignal(
    string Title,
    string Text,
    string DomainCode,
    string? Context = null,
    string? SubdomainCode = null,
    SignalStage Stage = SignalStage.Intuition,
    int? Confidence = null,
    SourceType SourceType = SourceType.Observation,
    string? SourceReference = null,
    string? ClaimedOrigin = null,
    string? TimeZone = null);

/// <summary>Importación de una señal de la Fase 0 (signals/*.md) conservando su código.</summary>
public sealed record ImportSignal(
    string Code,
    string Title,
    string Text,
    string DomainCode,
    string? Context,
    int? Confidence,
    SourceType SourceType,
    bool Retrospective,
    string? ClaimedOrigin,
    string ImportedFrom);

public sealed record ReviseSignal(string Title, string Text, string? Context, int? Confidence, string Reason);

public sealed record ReclassifySignal(
    SignalStage Stage,
    SignalStatus Status,
    string? SubdomainCode,
    string? SourceReference,
    string? GeographicScope,
    int? ImpactEstimate,
    int? RelevanceToJegas,
    Confidentiality Confidentiality,
    string Reason);

public sealed record AddEvidence(
    EvidenceKind Kind,
    EvidenceRole Role,
    string Description,
    string? Url = null,
    string? GitRepo = null,
    string? GitCommit = null,
    DateTimeOffset? ArtifactTimestamp = null,
    TimestampAuthority TimestampAuthority = TimestampAuthority.None);

public sealed record UploadedFile(string FileName, string ContentType, long Length, Stream Content);
