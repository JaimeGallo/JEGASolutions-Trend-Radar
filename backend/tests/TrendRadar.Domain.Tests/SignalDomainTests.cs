using TrendRadar.Domain.Common;
using TrendRadar.Domain.Signals;

namespace TrendRadar.Domain.Tests;

public sealed class DateIntervalTests
{
    private static DateTimeOffset Co(int y, int m, int d, int h = 0, int min = 0) => new(y, m, d, h, min, 0, DateInterval.ColombiaOffset);

    [Fact]
    public void Dia_cubre_el_dia_completo_en_hora_de_Colombia()
    {
        var i = DateInterval.TryParse("2025-03-14")!.Value;

        Assert.Equal(DatePrecision.Day, i.Precision);
        Assert.Equal(Co(2025, 3, 14), i.Earliest);
        Assert.Equal(Co(2025, 3, 15) - TimeSpan.FromTicks(1), i.Latest);
        Assert.Equal(TimeSpan.Zero, i.Earliest.Offset);
    }

    [Theory]
    [InlineData("2025-02", 2025, 2, 1, 2025, 3, 1, DatePrecision.Month)]
    [InlineData("2024-02", 2024, 2, 1, 2024, 3, 1, DatePrecision.Month)]
    [InlineData("2025-Q1", 2025, 1, 1, 2025, 4, 1, DatePrecision.Quarter)]
    [InlineData("2025q4", 2025, 10, 1, 2026, 1, 1, DatePrecision.Quarter)]
    [InlineData("2025", 2025, 1, 1, 2026, 1, 1, DatePrecision.Year)]
    [InlineData(" 2025-12 ", 2025, 12, 1, 2026, 1, 1, DatePrecision.Month)]
    public void Intervalos_por_precision(string text, int y1, int m1, int d1, int y2, int m2, int d2, DatePrecision precision)
    {
        var i = DateInterval.TryParse(text)!.Value;

        Assert.Equal(precision, i.Precision);
        Assert.Equal(Co(y1, m1, d1), i.Earliest);
        Assert.Equal(Co(y2, m2, d2) - TimeSpan.FromTicks(1), i.Latest);
    }

    [Theory]
    [InlineData("2025 aprox")]
    [InlineData("~2025")]
    [InlineData("aprox. 2025")]
    public void Aproximado_amplia_un_anio_a_cada_lado(string text)
    {
        var i = DateInterval.TryParse(text)!.Value;

        Assert.Equal(DatePrecision.Approximate, i.Precision);
        Assert.Equal(Co(2024, 1, 1), i.Earliest);
        Assert.Equal(Co(2027, 1, 1) - TimeSpan.FromTicks(1), i.Latest);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("pendiente")]
    [InlineData("hace mucho")]
    [InlineData("2025-13")]
    [InlineData("2025-02-30")]
    [InlineData("2025-Q5")]
    [InlineData("1850")]
    [InlineData("14/03/2025")]
    public void Texto_no_reconocido_devuelve_null(string? text) => Assert.Null(DateInterval.TryParse(text));
}

public sealed class EvidenceRulesTests
{
    [Theory]
    [InlineData(TimestampAuthority.None, EvidenceLevel.E0)]
    [InlineData(TimestampAuthority.Self, EvidenceLevel.E1)]
    [InlineData(TimestampAuthority.GitAuthor, EvidenceLevel.E2)]
    [InlineData(TimestampAuthority.FileMetadata, EvidenceLevel.E2)]
    [InlineData(TimestampAuthority.GitHub, EvidenceLevel.E3)]
    [InlineData(TimestampAuthority.EmailProvider, EvidenceLevel.E3)]
    [InlineData(TimestampAuthority.ChatProvider, EvidenceLevel.E3)]
    [InlineData(TimestampAuthority.Wayback, EvidenceLevel.E3)]
    [InlineData(TimestampAuthority.Hosting, EvidenceLevel.E3)]
    [InlineData(TimestampAuthority.OpenTimestamps, EvidenceLevel.E4)]
    [InlineData(TimestampAuthority.Rfc3161, EvidenceLevel.E4)]
    public void Nivel_segun_quien_fija_la_fecha(TimestampAuthority authority, EvidenceLevel level) =>
        Assert.Equal(level, EvidenceRules.LevelFor(authority));

    [Fact]
    public void Evidencia_sin_fecha_queda_en_E0_aunque_declare_autoridad()
    {
        var e = Evidence.Create(Guid.NewGuid(), Draft(null, TimestampAuthority.GitHub), null, Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(TimestampAuthority.None, e.TimestampAuthority);
        Assert.Equal(EvidenceLevel.E0, e.Level);
    }

    internal static EvidenceDraft Draft(DateTimeOffset? at, TimestampAuthority authority, EvidenceRole role = EvidenceRole.Origin) =>
        new(EvidenceKind.Note, role, "nota", null, null, null, at, authority);
}

public sealed class OriginAssessmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 2, 12, 0, 0, TimeSpan.Zero);

    private static Signal Make(bool retrospective = false, string? importedFrom = null)
    {
        var s = Signal.Capture(
            new SignalDraft("t", "x", null, Guid.NewGuid(), null, SignalStage.Intuition, null, SourceType.Observation,
                null, null, null, null, retrospective, importedFrom is null ? null : "TR-UX-001", importedFrom),
            Guid.NewGuid(), Now);

        // RecordedAt e IsRetrospective los fija la base de datos; aquí se simulan.
        typeof(Signal).GetProperty(nameof(Signal.RecordedAt))!.SetValue(s, Now);
        typeof(Signal).GetProperty(nameof(Signal.IsRetrospective))!.SetValue(s, retrospective);
        return s;
    }

    private static Evidence Ev(Signal s, DateTimeOffset? at, TimestampAuthority a, EvidenceRole role = EvidenceRole.Origin) =>
        Evidence.Create(s.Id, EvidenceRulesTests.Draft(at, a, role), null, Guid.NewGuid(), Now);

    [Fact]
    public void Senal_nueva_sin_evidencia_esta_pre_registrada_sin_nivel_hasta_el_anclaje()
    {
        var s = Make();

        var o = OriginAssessment.For(s, []);

        Assert.Equal(OriginBasis.PreRegistered, o.Basis);
        Assert.Equal(Now, o.SupportedOriginAt);
        Assert.Null(o.SupportedLevel);
    }

    [Fact]
    public void Retrospectiva_sin_evidencia_suficiente_queda_solo_declarada()
    {
        var s = Make(retrospective: true);

        var o = OriginAssessment.For(s, [Ev(s, Now.AddYears(-1), TimestampAuthority.Self), Ev(s, null, TimestampAuthority.None)]);

        Assert.Equal(OriginBasis.ClaimedOnly, o.Basis);
        Assert.Null(o.SupportedOriginAt);
    }

    [Fact]
    public void Retrospectiva_usa_la_evidencia_de_origen_mas_temprana_de_nivel_E2_o_superior()
    {
        var s = Make(retrospective: true);
        var early = Ev(s, Now.AddYears(-2), TimestampAuthority.GitAuthor);
        var later = Ev(s, Now.AddYears(-1), TimestampAuthority.GitHub);
        var earlierButSupporting = Ev(s, Now.AddYears(-3), TimestampAuthority.GitHub, EvidenceRole.Supports);

        var o = OriginAssessment.For(s, [later, early, earlierButSupporting]);

        Assert.Equal(OriginBasis.VerifiedOrigin, o.Basis);
        Assert.Equal(early.Id, o.SupportingEvidenceId);
        Assert.Equal(EvidenceLevel.E2, o.SupportedLevel);
    }

    [Fact]
    public void Pre_registrada_con_evidencia_anterior_adelanta_el_origen()
    {
        var s = Make();
        var e = Ev(s, Now.AddMonths(-6), TimestampAuthority.Wayback);

        var o = OriginAssessment.For(s, [e]);

        Assert.Equal(OriginBasis.VerifiedOrigin, o.Basis);
        Assert.Equal(Now.AddMonths(-6), o.SupportedOriginAt);
    }

    [Fact]
    public void Importada_de_la_fase_0_no_cuenta_como_pre_registrada_sin_evidencia()
    {
        var s = Make(importedFrom: "signals/TR-UX-001.md");

        Assert.Equal(OriginBasis.ClaimedOnly, OriginAssessment.For(s, []).Basis);
        Assert.Equal(
            OriginBasis.VerifiedOrigin,
            OriginAssessment.For(s, [Ev(s, Now.AddDays(-30), TimestampAuthority.GitAuthor)]).Basis);
    }
}
