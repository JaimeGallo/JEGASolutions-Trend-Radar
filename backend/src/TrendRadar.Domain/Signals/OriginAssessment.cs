namespace TrendRadar.Domain.Signals;

/// <summary>
/// Origen usable de una señal para la cronología (RESEARCH_METHODOLOGY §2): la fecha más temprana
/// que se puede sostener frente a un tercero, y en qué se basa.
/// </summary>
public sealed record OriginAssessment(
    OriginBasis Basis,
    DateTimeOffset? SupportedOriginAt,
    EvidenceLevel? SupportedLevel,
    Guid? SupportingEvidenceId)
{
    public static OriginAssessment For(Signal signal, IEnumerable<Evidence> evidence)
    {
        var best = evidence
            .Where(e => e.Role == EvidenceRole.Origin && e.ArtifactTimestamp is not null && e.Level >= EvidenceLevel.E2)
            .OrderBy(e => e.ArtifactTimestamp)
            .ThenByDescending(e => e.Level)
            .FirstOrDefault();

        var preRegistered = !signal.IsRetrospective && signal.ImportedFrom is null;

        // Si hay evidencia anterior al registro, esa fecha es la más temprana sostenible.
        if (best is not null && (!preRegistered || best.ArtifactTimestamp < signal.RecordedAt))
        {
            return new OriginAssessment(OriginBasis.VerifiedOrigin, best.ArtifactTimestamp, best.Level, best.Id);
        }

        // El registro propio solo llegará a E4 cuando exista el anclaje externo de la cadena (M6).
        return preRegistered
            ? new OriginAssessment(OriginBasis.PreRegistered, signal.RecordedAt, null, null)
            : new OriginAssessment(OriginBasis.ClaimedOnly, null, null, null);
    }
}
