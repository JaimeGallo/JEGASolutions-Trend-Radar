namespace TrendRadar.Domain.Signals;

/// <summary>Etapa de una señal (DISCOVERY A3). Hipótesis, predicciones, etc. son entidades propias.</summary>
public enum SignalStage
{
    Intuition,
    Signal,
    Opportunity,
}

/// <summary>Las señales nunca se borran; se archivan.</summary>
public enum SignalStatus
{
    Active,
    Dormant,
    Archived,
}

public enum SourceType
{
    Observation,
    ProjectDecision,
    ClientInteraction,
    Prototype,
    Commit,
    Document,
    Conversation,
    MarketObservation,
    RejectedIdea,
    Experiment,
}

public enum Confidentiality
{
    Public,
    Internal,
    ClientConfidential,
}

public enum EvidenceKind
{
    File,
    Url,
    GitCommit,
    Note,
}

public enum EvidenceRole
{
    /// <summary>Demuestra cuándo existía la idea.</summary>
    Origin,
    Supports,
    Contradicts,
}

/// <summary>Quién fija la fecha que la evidencia demuestra (RESEARCH_METHODOLOGY §3).</summary>
public enum TimestampAuthority
{
    /// <summary>Sin fecha verificable: memoria o afirmación.</summary>
    None,

    /// <summary>Artefacto propio sin sello externo (archivo local, captura sin metadatos).</summary>
    Self,

    /// <summary>Fecha controlable por el autor: fecha de autor de git, metadatos EXIF.</summary>
    GitAuthor,
    FileMetadata,

    /// <summary>Sello de un tercero.</summary>
    GitHub,
    EmailProvider,
    ChatProvider,
    Wayback,
    Hosting,

    /// <summary>Sello criptográfico.</summary>
    OpenTimestamps,
    Rfc3161,
}

public enum EvidenceLevel
{
    E0,
    E1,
    E2,
    E3,
    E4,
}

/// <summary>De dónde sale el origen usable de una señal para la cronología.</summary>
public enum OriginBasis
{
    /// <summary>Registrada en Trend Radar antes de cualquier evento externo: la prueba más fuerte.</summary>
    PreRegistered,

    /// <summary>Respaldada por evidencia de nivel E2 o superior.</summary>
    VerifiedOrigin,

    /// <summary>Solo hay una fecha declarada: no sirve para afirmar BEFORE.</summary>
    ClaimedOnly,
}
