using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TrendRadar.API;
using TrendRadar.Application.Signals;
using TrendRadar.Domain.Signals;
using TrendRadar.Domain.Users;

namespace TrendRadar.Integration.Tests;

[Collection(DatabaseTestGroup.Name)]
public sealed class SignalApiTests : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly string _files = Path.Combine(Path.GetTempPath(), "tr-evidence-" + Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _factory;

    public SignalApiTests(DatabaseFixture db) => _factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(b => b
            .UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:TrendRadar", db.AppConnection)
            .UseSetting("Database:MigrateOnStartup", "false")
            .UseSetting("Auth:JwtSecret", "secreto-de-pruebas-con-mas-de-32-bytes")
            .UseSetting("Evidence:StoragePath", _files));

    public void Dispose()
    {
        _factory.Dispose();
        if (Directory.Exists(_files))
        {
            Directory.Delete(_files, recursive: true);
        }
    }

    private async Task<HttpClient> LoginAsync(string email = DatabaseFixture.OwnerEmail, string password = DatabaseFixture.OwnerPassword)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<SignalDetailView> Read(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SignalDetailView>(Json))!;
    }

    private static async Task<string> ErrorTitle(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("title").GetString()!;
    }

    private static Task<HttpResponseMessage> Capture(HttpClient c, string title, string domain = "UX", string? claimedOrigin = null) =>
        c.PostAsJsonAsync("/api/signals", new CaptureSignal(title, $"Texto original de {title}", domain, ClaimedOrigin: claimedOrigin, Confidence: 55), Json);

    [Fact]
    public async Task Captura_rapida_asigna_codigo_fecha_de_servidor_y_version_1()
    {
        var c = await LoginAsync();
        var before = DateTimeOffset.UtcNow.AddSeconds(-5);

        var s = await Read(await Capture(c, "Paneles con scroll interno"), HttpStatusCode.Created);

        Assert.Matches(@"^TR-UX-\d{3}$", s.Code);
        Assert.InRange(s.RecordedAt, before, DateTimeOffset.UtcNow.AddSeconds(5));
        Assert.Equal(1, s.CurrentVersion);
        var v1 = Assert.Single(s.Versions);
        Assert.Equal(SignalVersion.OriginalReason, v1.ChangeReason);
        Assert.Equal(64, v1.ChainHash.Length);
        Assert.Equal(OriginBasis.PreRegistered, s.Origin.Basis);
        Assert.False(s.Origin.IsRetrospective);
    }

    [Fact]
    public async Task Origen_declarado_antiguo_marca_la_senal_como_retrospectiva()
    {
        var c = await LoginAsync();

        var s = await Read(await Capture(c, "Idea antigua", "AI", "2025-Q1"), HttpStatusCode.Created);

        Assert.True(s.Origin.IsRetrospective);
        Assert.Equal(OriginBasis.ClaimedOnly, s.Origin.Basis);
        Assert.Equal(Domain.Common.DatePrecision.Quarter, s.Origin.Claimed.Precision);
    }

    [Theory]
    [InlineData("", "texto", "UX", null, "título")]
    [InlineData("Título", "texto", "NOPE", null, "dominio")]
    [InlineData("Título", "texto", "UX.DENSITY", null, "dominio")]
    [InlineData("Título", "texto", "UX", "hace tiempo", "fecha de origen")]
    [InlineData("Título", "texto", "UX", "2999", "futuro")]
    public async Task Captura_invalida_responde_400_en_espanol(string title, string text, string domain, string? origin, string fragment)
    {
        var c = await LoginAsync();

        var response = await c.PostAsJsonAsync("/api/signals", new CaptureSignal(title, text, domain, ClaimedOrigin: origin), Json);

        Assert.Contains(fragment, await ErrorTitle(response, HttpStatusCode.BadRequest), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Revisar_crea_una_version_nueva_y_conserva_el_original()
    {
        var c = await LoginAsync();
        var s = await Read(await Capture(c, "Hipótesis inicial"), HttpStatusCode.Created);

        var noReason = await c.PostAsJsonAsync($"/api/signals/{s.Code}/versions", new ReviseSignal("x", "y", null, null, ""), Json);
        var same = await c.PostAsJsonAsync(
            $"/api/signals/{s.Code}/versions", new ReviseSignal(s.OriginalTitle, s.OriginalText, null, 55, "sin cambios reales"), Json);
        var revised = await Read(await c.PostAsJsonAsync(
            $"/api/signals/{s.Code}/versions", new ReviseSignal("Hipótesis precisada", "Texto nuevo", null, 70, "Nueva evidencia"), Json));

        Assert.Contains("motivo", await ErrorTitle(noReason, HttpStatusCode.BadRequest), StringComparison.Ordinal);
        Assert.Contains("no cambia nada", await ErrorTitle(same, HttpStatusCode.BadRequest), StringComparison.Ordinal);
        Assert.Equal(2, revised.CurrentVersion);
        Assert.Equal(["Hipótesis inicial", "Hipótesis precisada"], revised.Versions.Select(v => v.Title));
        Assert.Equal("Hipótesis inicial", revised.OriginalTitle);
        Assert.Equal(55, revised.ConfidenceAtCreation);
    }

    [Fact]
    public async Task Reclasificar_exige_motivo_y_subdominio_del_mismo_dominio()
    {
        var c = await LoginAsync();
        var s = await Read(await Capture(c, "Para clasificar"), HttpStatusCode.Created);
        ReclassifySignal Req(string? sub, string reason) =>
            new(SignalStage.Signal, SignalStatus.Active, sub, null, "Medellín", 3, 5, Confidentiality.Internal, reason);

        var wrongSub = await c.PutAsJsonAsync($"/api/signals/{s.Code}/classification", Req("AI.AGENTS", "motivo válido"), Json);
        var ok = await Read(await c.PutAsJsonAsync($"/api/signals/{s.Code}/classification", Req("UX.DENSITY", "Hay evidencia inicial"), Json));

        Assert.Contains("subdominio", await ErrorTitle(wrongSub, HttpStatusCode.BadRequest), StringComparison.Ordinal);
        Assert.Equal(SignalStage.Signal, ok.Stage);
        Assert.Equal("UX.DENSITY", ok.SubdomainCode);
        Assert.Equal("Medellín", ok.GeographicScope);
    }

    [Fact]
    public async Task Evidencia_de_commit_con_fecha_de_tercero_respalda_el_origen()
    {
        var c = await LoginAsync();
        var s = await Read(await Capture(c, "Con evidencia", "AI", "2025"), HttpStatusCode.Created);
        var at = new DateTimeOffset(2025, 6, 1, 15, 0, 0, TimeSpan.Zero);

        var badCommit = await c.PostAsJsonAsync($"/api/signals/{s.Code}/evidence",
            new AddEvidence(EvidenceKind.GitCommit, EvidenceRole.Origin, "commit", GitRepo: "repo", GitCommit: "no-es-hash"), Json);
        var detail = await Read(await c.PostAsJsonAsync($"/api/signals/{s.Code}/evidence",
            new AddEvidence(EvidenceKind.GitCommit, EvidenceRole.Origin, "Primer layout sin scroll",
                GitRepo: "JaimeGallo/jegasolutions-platform", GitCommit: "f03d19ccbd93", ArtifactTimestamp: at,
                TimestampAuthority: TimestampAuthority.GitHub), Json));

        Assert.Contains("commit", await ErrorTitle(badCommit, HttpStatusCode.BadRequest), StringComparison.Ordinal);
        var e = Assert.Single(detail.Evidence);
        Assert.Equal(EvidenceLevel.E3, e.Level);
        Assert.Equal(OriginBasis.VerifiedOrigin, detail.Origin.Basis);
        Assert.Equal(at, detail.Origin.SupportedOriginAt);
    }

    [Fact]
    public async Task Archivo_de_evidencia_se_guarda_con_su_hash_y_se_descarga_identico()
    {
        var c = await LoginAsync();
        var s = await Read(await Capture(c, "Con archivo"), HttpStatusCode.Created);
        var bytes = Encoding.UTF8.GetBytes("captura de pantalla simulada " + Guid.NewGuid());

        var forbidden = await Upload(c, s.Code, "script.exe", bytes);
        var detail = await Read(await Upload(c, s.Code, "captura.png", bytes));
        var e = Assert.Single(detail.Evidence);
        var download = await c.GetAsync(new Uri($"/api/evidence/{e.Id}/file", UriKind.Relative));

        Assert.Contains("no permitido", await ErrorTitle(forbidden, HttpStatusCode.BadRequest), StringComparison.Ordinal);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(bytes)), e.FileSha256);
        Assert.Equal("image/png", e.FileMime);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
    }

    private static Task<HttpResponseMessage> Upload(HttpClient c, string code, string name, byte[] bytes)
    {
        var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(bytes), "file", name },
            { new StringContent("Supports"), "role" },
            { new StringContent("Captura del prototipo"), "description" },
        };
        return c.PostAsync(new Uri($"/api/signals/{code}/evidence/file", UriKind.Relative), form);
    }

    [Fact]
    public async Task Busqueda_ignora_tildes_y_admite_prefijos()
    {
        var c = await LoginAsync();
        var unique = "Zarigüeya" + Guid.NewGuid().ToString("N")[..6];
        var s = await Read(await Capture(c, $"Navegación {unique}"), HttpStatusCode.Created);

        var page = await c.GetFromJsonAsync<SignalPage>($"/api/signals?q=navegacion%20{unique[..7].Replace("ü", "u", StringComparison.Ordinal)}", Json);

        Assert.Contains(page!.Items, i => i.Code == s.Code);
        Assert.All(page.Items, i => Assert.Contains("Navegación", i.Title, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Importar_conserva_el_codigo_rechaza_duplicados_y_la_numeracion_continua()
    {
        var c = await LoginAsync();
        var import = new ImportSignal("TR-MUSIC-007", "Pistas favoritas", "Texto de la fase 0", "MUSIC", null, null,
            SourceType.Observation, true, "pendiente", "signals/TR-MUSIC-007.md");

        var imported = await Read(await c.PostAsJsonAsync("/api/signals/import", import, Json), HttpStatusCode.Created);
        var duplicate = await c.PostAsJsonAsync("/api/signals/import", import, Json);
        var next = await Read(await Capture(c, "Nueva pista", "MUSIC"), HttpStatusCode.Created);

        Assert.Equal("TR-MUSIC-007", imported.Code);
        Assert.True(imported.Origin.IsRetrospective);
        Assert.Equal("pendiente", imported.Origin.Claimed.Note);
        Assert.Equal("signals/TR-MUSIC-007.md", imported.ImportedFrom);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("TR-MUSIC-008", next.Code);
    }

    [Fact]
    public async Task Un_lector_puede_consultar_pero_no_registrar()
    {
        var owner = await LoginAsync();
        var s = await Read(await Capture(owner, "Visible para lectores"), HttpStatusCode.Created);
        var email = $"lector-{Guid.NewGuid():N}@example.com";
        (await owner.PostAsJsonAsync("/api/users", new CreateUserRequest(email, "Lector", DatabaseFixture.OwnerPassword, UserRole.Viewer))).EnsureSuccessStatusCode();
        var viewer = await LoginAsync(email, DatabaseFixture.OwnerPassword);

        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(new Uri($"/api/signals/{s.Code}", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Capture(viewer, "No permitido")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync(new Uri("/api/signals/TR-UX-999999", UriKind.Relative))).StatusCode);
    }

    [Fact]
    public async Task Cada_accion_queda_en_la_auditoria_y_las_cadenas_siguen_integras()
    {
        var c = await LoginAsync();
        var s = await Read(await Capture(c, "Auditada"), HttpStatusCode.Created);
        await c.PostAsJsonAsync($"/api/signals/{s.Code}/versions", new ReviseSignal("Auditada v2", "otro texto", null, null, "Cambio de opinión"), Json);

        var audit = await c.GetFromJsonAsync<List<AuditEntryDto>>($"/api/audit?entityType=signal&entityId={s.Code}");
        var integrity = await c.GetFromJsonAsync<IntegrityDto>("/api/integrity/verify");

        Assert.Equal(["VERSION", "CREATE"], audit!.Select(a => a.Action));
        Assert.Equal("Cambio de opinión", audit![0].Reason);
        Assert.True(integrity!.IsValid);
        Assert.Contains(integrity.Chains, ch => ch.Chain == "signal_versions" && ch.CheckedEntries > 0);
    }
}
