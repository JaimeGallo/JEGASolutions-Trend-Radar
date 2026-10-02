namespace TrendRadar.Domain.Signals;

public static class EvidenceRules
{
    /// <summary>Nivel de evidencia de fecha según quién la fija (RESEARCH_METHODOLOGY §3).</summary>
    public static EvidenceLevel LevelFor(TimestampAuthority authority) => authority switch
    {
        TimestampAuthority.None => EvidenceLevel.E0,
        TimestampAuthority.Self => EvidenceLevel.E1,
        TimestampAuthority.GitAuthor or TimestampAuthority.FileMetadata => EvidenceLevel.E2,
        TimestampAuthority.GitHub or TimestampAuthority.EmailProvider or TimestampAuthority.ChatProvider
            or TimestampAuthority.Wayback or TimestampAuthority.Hosting => EvidenceLevel.E3,
        TimestampAuthority.OpenTimestamps or TimestampAuthority.Rfc3161 => EvidenceLevel.E4,
        _ => throw new ArgumentOutOfRangeException(nameof(authority)),
    };

    /// <summary>Margen tras el cual una señal se considera registrada retrospectivamente.</summary>
    public static readonly TimeSpan RetrospectiveThreshold = TimeSpan.FromDays(7);
}
