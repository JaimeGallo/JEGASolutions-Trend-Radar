using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using TrendRadar.Application.Signals;
using TrendRadar.Domain.Signals;
using TrendRadar.Infrastructure.Persistence.Configurations;

namespace TrendRadar.Infrastructure.Persistence.Stores;

internal sealed partial class SignalQueries(TrendRadarDbContext db) : ISignalQueries
{
    public async Task<SignalPage> ListAsync(SignalQuery query, CancellationToken ct)
    {
        var q = db.Signals.AsNoTracking();

        if (ToPrefixQuery(query.Text) is { } tsQuery)
        {
            q = q.Where(s => EF.Property<NpgsqlTsVector>(s, SignalConfiguration.SearchVector)
                .Matches(EF.Functions.ToTsQuery("simple", TrendRadarDbContext.Unaccent(tsQuery))));
        }

        if (!string.IsNullOrWhiteSpace(query.DomainCode))
        {
            q = q.Where(s => db.Domains.Any(d => d.Id == s.DomainId && d.Code == query.DomainCode));
        }

        if (query.Stage is { } stage)
        {
            q = q.Where(s => s.Stage == stage);
        }

        if (query.Status is { } status)
        {
            q = q.Where(s => s.Status == status);
        }

        if (query.Retrospective is { } retro)
        {
            q = q.Where(s => s.IsRetrospective == retro);
        }

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(s => s.RecordedAt)
            .Skip(Math.Max(0, query.Skip))
            .Take(Math.Clamp(query.Take, 1, 200))
            .Select(s => new SignalSummaryView(
                s.SignalCode!,
                db.SignalVersions.Where(v => v.SignalId == s.Id && v.Version == s.CurrentVersion).Select(v => v.Title).FirstOrDefault()
                    ?? s.OriginalTitle,
                db.Domains.Where(d => d.Id == s.DomainId).Select(d => d.Code).First(),
                db.Domains.Where(d => d.Id == s.SubdomainId).Select(d => d.Code).FirstOrDefault(),
                s.Stage,
                s.Status,
                s.RecordedAt,
                s.IsRetrospective,
                s.CurrentVersion,
                db.Evidence.Count(e => e.SignalId == s.Id),
                db.SignalVersions.Where(v => v.SignalId == s.Id && v.Version == s.CurrentVersion).Select(v => v.Confidence).FirstOrDefault()))
            .ToListAsync(ct);

        return new SignalPage(items, total);
    }

    public async Task<SignalDetailView?> GetAsync(string code, CancellationToken ct)
    {
        var s = await db.Signals.AsNoTracking().SingleOrDefaultAsync(x => x.SignalCode == code, ct);
        if (s is null)
        {
            return null;
        }

        var domain = await db.Domains.AsNoTracking().SingleAsync(d => d.Id == s.DomainId, ct);
        var sub = s.SubdomainId is null ? null : await db.Domains.AsNoTracking().SingleAsync(d => d.Id == s.SubdomainId, ct);
        var versions = await db.SignalVersions.AsNoTracking().Where(v => v.SignalId == s.Id).OrderBy(v => v.Version).ToListAsync(ct);
        var evidence = await db.Evidence.AsNoTracking().Where(e => e.SignalId == s.Id).OrderBy(e => e.RecordedAt).ToListAsync(ct);

        var userIds = versions.Select(v => v.CreatedBy).Append(s.RecordedBy).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.DisplayName, ct);

        var origin = OriginAssessment.For(s, evidence);
        return new SignalDetailView(
            s.SignalCode!,
            s.OriginalTitle,
            s.OriginalText,
            s.OriginalContext,
            s.RecordedAt,
            names.GetValueOrDefault(s.RecordedBy, "?"),
            s.RecordedTimeZone,
            s.ConfidenceAtCreation,
            s.SourceType,
            s.ImportedFrom,
            domain.Code,
            domain.Name,
            sub?.Code,
            sub?.Name,
            s.Stage,
            s.Status,
            s.SourceReference,
            s.GeographicScope,
            s.ImpactEstimate,
            s.RelevanceToJegas,
            s.Confidentiality,
            s.CurrentVersion,
            new OriginView(
                s.RecordedAt,
                s.IsRetrospective,
                new ClaimedOriginView(s.ClaimedOriginEarliest, s.ClaimedOriginLatest, s.ClaimedOriginPrecision, s.ClaimedOriginNote),
                origin.Basis,
                origin.SupportedOriginAt,
                origin.SupportedLevel,
                origin.SupportingEvidenceId),
            versions.Select(v => new SignalVersionView(
                v.Version, v.Title, v.Text, v.Context, v.Confidence, v.ChangeReason, v.CreatedAt,
                names.GetValueOrDefault(v.CreatedBy, "?"), Convert.ToHexStringLower(v.ChainHash))).ToList(),
            evidence.Select(ToView).ToList());
    }

    public async Task<EvidenceView?> GetEvidenceAsync(Guid id, CancellationToken ct)
    {
        var e = await db.Evidence.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);
        return e is null ? null : ToView(e);
    }

    private static EvidenceView ToView(Evidence e) => new(
        e.Id, e.Kind, e.Role, e.Description, e.Url, e.GitRepo, e.GitCommit, e.FileName, e.FileSha256, e.FileMime,
        e.FileSize, e.ArtifactTimestamp, e.TimestampAuthority, e.Level, e.RecordedAt);

    /// <summary>"densidad scro" → "densidad:* &amp; scro:*" (búsqueda por prefijo, sin operadores del usuario).</summary>
    private static string? ToPrefixQuery(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var words = WordPattern().Matches(text).Select(m => m.Value.ToLowerInvariant()).Take(8).ToList();
        return words.Count == 0 ? null : string.Join(" & ", words.Select(w => w + ":*"));
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordPattern();
}
