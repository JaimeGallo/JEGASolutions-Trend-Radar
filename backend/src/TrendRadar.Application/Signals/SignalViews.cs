using TrendRadar.Domain.Common;
using TrendRadar.Domain.Signals;

namespace TrendRadar.Application.Signals;

public sealed record SignalQuery(
    string? Text = null,
    string? DomainCode = null,
    SignalStage? Stage = null,
    SignalStatus? Status = null,
    bool? Retrospective = null,
    int Skip = 0,
    int Take = 50);

public sealed record SignalSummaryView(
    string Code,
    string Title,
    string DomainCode,
    string? SubdomainCode,
    SignalStage Stage,
    SignalStatus Status,
    DateTimeOffset RecordedAt,
    bool IsRetrospective,
    int CurrentVersion,
    int EvidenceCount,
    int? Confidence);

public sealed record SignalPage(IReadOnlyList<SignalSummaryView> Items, int Total);

public sealed record SignalVersionView(
    int Version,
    string Title,
    string Text,
    string? Context,
    int? Confidence,
    string ChangeReason,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    string ChainHash);

public sealed record EvidenceView(
    Guid Id,
    EvidenceKind Kind,
    EvidenceRole Role,
    string Description,
    string? Url,
    string? GitRepo,
    string? GitCommit,
    string? FileName,
    string? FileSha256,
    string? FileMime,
    long? FileSize,
    DateTimeOffset? ArtifactTimestamp,
    TimestampAuthority TimestampAuthority,
    EvidenceLevel Level,
    DateTimeOffset RecordedAt);

public sealed record ClaimedOriginView(DateTimeOffset? Earliest, DateTimeOffset? Latest, DatePrecision? Precision, string? Note);

public sealed record OriginView(
    DateTimeOffset RecordedAt,
    bool IsRetrospective,
    ClaimedOriginView Claimed,
    OriginBasis Basis,
    DateTimeOffset? SupportedOriginAt,
    EvidenceLevel? SupportedLevel,
    Guid? SupportingEvidenceId);

public sealed record SignalDetailView(
    string Code,
    string OriginalTitle,
    string OriginalText,
    string? OriginalContext,
    DateTimeOffset RecordedAt,
    string RecordedBy,
    string RecordedTimeZone,
    int? ConfidenceAtCreation,
    SourceType SourceType,
    string? ImportedFrom,
    string DomainCode,
    string DomainName,
    string? SubdomainCode,
    string? SubdomainName,
    SignalStage Stage,
    SignalStatus Status,
    string? SourceReference,
    string? GeographicScope,
    int? ImpactEstimate,
    int? RelevanceToJegas,
    Confidentiality Confidentiality,
    int CurrentVersion,
    OriginView Origin,
    IReadOnlyList<SignalVersionView> Versions,
    IReadOnlyList<EvidenceView> Evidence);
