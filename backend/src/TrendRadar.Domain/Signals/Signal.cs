using TrendRadar.Domain.Common;

namespace TrendRadar.Domain.Signals;

/// <summary>
/// Señal del radar interno. Los campos "Original*", la fecha de registro, el dominio y el origen
/// declarado son inmutables (la base de datos rechaza cualquier cambio). Cambiar de opinión
/// crea una <see cref="SignalVersion"/>; reclasificar solo toca los campos mutables.
/// </summary>
public sealed class Signal
{
    private Signal()
    {
    }

    public Guid Id { get; private set; }

    /// <summary>TR-{DOMINIO}-{NNN}. Lo asigna la base de datos (o la importación de la Fase 0).</summary>
    public string? SignalCode { get; private set; }

    public string OriginalTitle { get; private set; } = string.Empty;

    public string OriginalText { get; private set; } = string.Empty;

    public string? OriginalContext { get; private set; }

    /// <summary>Reloj del servidor de base de datos; el valor enviado por la aplicación se ignora.</summary>
    public DateTimeOffset RecordedAt { get; private set; }

    public Guid RecordedBy { get; private set; }

    public string RecordedTimeZone { get; private set; } = "America/Bogota";

    public int? ConfidenceAtCreation { get; private set; }

    public SourceType SourceType { get; private set; }

    public Guid DomainId { get; private set; }

    public DateTimeOffset? ClaimedOriginEarliest { get; private set; }

    public DateTimeOffset? ClaimedOriginLatest { get; private set; }

    public DatePrecision? ClaimedOriginPrecision { get; private set; }

    /// <summary>Texto libre del origen declarado tal como se escribió ("2025-03", "pendiente").</summary>
    public string? ClaimedOriginNote { get; private set; }

    /// <summary>Retrospectiva declarada (solo importaciones de la Fase 0).</summary>
    public bool DeclaredRetrospective { get; private set; }

    /// <summary>Calculado por la base de datos: declarada o con origen declarado 7+ días antes del registro.</summary>
    public bool IsRetrospective { get; private set; }

    /// <summary>Ruta del archivo de la Fase 0 del que se importó, si aplica.</summary>
    public string? ImportedFrom { get; private set; }

    public SignalStage Stage { get; private set; }

    public SignalStatus Status { get; private set; }

    public Guid? SubdomainId { get; private set; }

    public string? SourceReference { get; private set; }

    public string? GeographicScope { get; private set; }

    public int? ImpactEstimate { get; private set; }

    public int? RelevanceToJegas { get; private set; }

    public Confidentiality Confidentiality { get; private set; }

    /// <summary>Lo mantiene la base de datos al insertar versiones.</summary>
    public int CurrentVersion { get; private set; }

    public static Signal Capture(SignalDraft d, Guid recordedBy, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        SignalCode = d.ImportCode,
        OriginalTitle = d.Title.Trim(),
        OriginalText = d.Text.Trim(),
        OriginalContext = string.IsNullOrWhiteSpace(d.Context) ? null : d.Context.Trim(),
        RecordedBy = recordedBy,
        RecordedTimeZone = string.IsNullOrWhiteSpace(d.TimeZone) ? "America/Bogota" : d.TimeZone,
        ConfidenceAtCreation = d.Confidence,
        SourceType = d.SourceType,
        DomainId = d.DomainId,
        SubdomainId = d.SubdomainId,
        ClaimedOriginEarliest = d.ClaimedOrigin?.Earliest,
        ClaimedOriginLatest = d.ClaimedOrigin?.Latest,
        ClaimedOriginPrecision = d.ClaimedOrigin?.Precision,
        ClaimedOriginNote = string.IsNullOrWhiteSpace(d.ClaimedOriginNote) ? null : d.ClaimedOriginNote.Trim(),
        DeclaredRetrospective = d.DeclaredRetrospective,
        ImportedFrom = d.ImportedFrom,
        SourceReference = d.SourceReference,
        Stage = d.Stage,
        Status = SignalStatus.Active,
        Confidentiality = Confidentiality.Internal,
    };

    public void Reclassify(SignalClassification c)
    {
        Stage = c.Stage;
        Status = c.Status;
        SubdomainId = c.SubdomainId;
        SourceReference = c.SourceReference;
        GeographicScope = c.GeographicScope;
        ImpactEstimate = c.ImpactEstimate;
        RelevanceToJegas = c.RelevanceToJegas;
        Confidentiality = c.Confidentiality;
    }

    public SignalClassification Classification => new(
        Stage, Status, SubdomainId, SourceReference, GeographicScope, ImpactEstimate, RelevanceToJegas, Confidentiality);
}

public sealed record SignalDraft(
    string Title,
    string Text,
    string? Context,
    Guid DomainId,
    Guid? SubdomainId,
    SignalStage Stage,
    int? Confidence,
    SourceType SourceType,
    string? SourceReference,
    DateInterval? ClaimedOrigin,
    string? ClaimedOriginNote,
    string? TimeZone,
    bool DeclaredRetrospective = false,
    string? ImportCode = null,
    string? ImportedFrom = null);

public sealed record SignalClassification(
    SignalStage Stage,
    SignalStatus Status,
    Guid? SubdomainId,
    string? SourceReference,
    string? GeographicScope,
    int? ImpactEstimate,
    int? RelevanceToJegas,
    Confidentiality Confidentiality);
