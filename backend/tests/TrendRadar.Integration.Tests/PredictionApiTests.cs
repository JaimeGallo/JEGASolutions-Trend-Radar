using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TrendRadar.API;
using TrendRadar.Application.Foresight;
using TrendRadar.Application.Signals;
using TrendRadar.Domain.Foresight;

namespace TrendRadar.Integration.Tests;

[Collection(DatabaseTestGroup.Name)]
public sealed class PredictionApiTests(DatabaseFixture db) : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private static readonly DateOnly Horizon = DateOnly.FromDateTime(DateTime.UtcNow).AddMonths(6);

    private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(b => b
            .UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:TrendRadar", db.AppConnection)
            .UseSetting("Database:MigrateOnStartup", "false")
            .UseSetting("Auth:JwtSecret", "secreto-de-pruebas-con-mas-de-32-bytes"));

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> LoginAsync()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(DatabaseFixture.OwnerEmail, DatabaseFixture.OwnerPassword));
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }

    private static async Task<string> SignalAsync(HttpClient c)
    {
        var r = await c.PostAsJsonAsync("/api/signals", new CaptureSignal("Señal para predecir", "Texto original", "BIZ"), Json);
        return (await r.Content.ReadFromJsonAsync<SignalDetailView>(Json))!.Code;
    }

    private static PredictionInput Input(string statement = "Tres pymes adoptarán el flujo antes de junio", int confidence = 60, string? reason = null, string? hypothesis = null) =>
        new(statement, "Contrato firmado o factura emitida por cada pyme", Horizon, confidence, 20, 3, "Dos clientes lo pidieron", hypothesis, reason);

    private static async Task<PredictionDetailView> Read(HttpResponseMessage r, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.Equal(expected, r.StatusCode);
        return (await r.Content.ReadFromJsonAsync<PredictionDetailView>(Json))!;
    }

    private static async Task<string> Error(HttpResponseMessage r, HttpStatusCode expected)
    {
        Assert.Equal(expected, r.StatusCode);
        using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("title").GetString()!;
    }

    [Fact]
    public async Task Crear_prediccion_con_hipotesis_queda_abierta_y_en_gracia()
    {
        var c = await LoginAsync();
        var signal = await SignalAsync(c);
        var hypothesis = (await (await c.PostAsJsonAsync($"/api/signals/{signal}/hypotheses",
            new CreateHypothesis("Las pymes pagan por eliminar Excel compartido", null), Json))
            .Content.ReadFromJsonAsync<SignalForesightView>(Json))!.Hypotheses.Single().Code;

        var p = await Read(await c.PostAsJsonAsync($"/api/signals/{signal}/predictions", Input(hypothesis: hypothesis), Json), HttpStatusCode.Created);
        var foresight = await c.GetFromJsonAsync<SignalForesightView>($"/api/signals/{signal}/foresight", Json);

        Assert.Matches(@"^PR-\d{4,}$", p.Code);
        Assert.Equal(PredictionStatus.Open, p.Status);
        Assert.False(p.IsLocked);
        Assert.Equal(TimeSpan.FromMinutes(15), p.LockedAt - p.RecordedAt);
        Assert.Equal(hypothesis, p.HypothesisCode);
        Assert.Single(p.Snapshots);
        Assert.Equal([p.Code], foresight!.Hypotheses.Single().PredictionCodes);
    }

    [Theory]
    [InlineData("corto", 60, "enunciado")]
    [InlineData("Enunciado suficientemente largo", 0, "1 a 99")]
    [InlineData("Enunciado suficientemente largo", 100, "1 a 99")]
    public async Task Prediccion_invalida_responde_400_en_espanol(string statement, int confidence, string fragment)
    {
        var c = await LoginAsync();
        var signal = await SignalAsync(c);

        var r = await c.PostAsJsonAsync($"/api/signals/{signal}/predictions", Input(statement, confidence), Json);

        Assert.Contains(fragment, await Error(r, HttpStatusCode.BadRequest), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fecha_limite_pasada_o_hoy_se_rechaza()
    {
        var c = await LoginAsync();
        var signal = await SignalAsync(c);

        var r = await c.PostAsJsonAsync($"/api/signals/{signal}/predictions",
            Input() with { HorizonDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-2) }, Json);

        Assert.Contains("posterior a hoy", await Error(r, HttpStatusCode.BadRequest), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Corregir_en_gracia_exige_motivo_y_cambio_real()
    {
        var c = await LoginAsync();
        var p = await Read(await c.PostAsJsonAsync($"/api/signals/{await SignalAsync(c)}/predictions", Input(), Json), HttpStatusCode.Created);

        var noReason = await c.PutAsJsonAsync($"/api/predictions/{p.Code}", Input(confidence: 70), Json);
        var same = await c.PutAsJsonAsync($"/api/predictions/{p.Code}", Input(reason: "sin cambios"), Json);
        var fixedP = await Read(await c.PutAsJsonAsync($"/api/predictions/{p.Code}", Input(confidence: 70, reason: "Me equivoqué al digitar"), Json));

        Assert.Contains("motivo", await Error(noReason, HttpStatusCode.BadRequest), StringComparison.Ordinal);
        Assert.Contains("no cambia nada", await Error(same, HttpStatusCode.BadRequest), StringComparison.Ordinal);
        Assert.Equal(70, fixedP.Confidence);
        Assert.Equal(2, fixedP.Snapshots.Count);
    }

    [Fact]
    public async Task Tras_la_gracia_corregir_responde_409_y_sugiere_version_nueva()
    {
        var c = await LoginAsync();
        var p = await Read(await c.PostAsJsonAsync($"/api/signals/{await SignalAsync(c)}/predictions", Input(), Json), HttpStatusCode.Created);
        await using (var owner = await db.OpenOwnerAsync())
        {
            await DatabaseFixture.Exec(owner, $"""
                SET session_replication_role = replica;
                UPDATE predictions SET locked_at = now() - interval '1 minute' WHERE code = '{p.Code}';
                SET session_replication_role = origin;
                """);
        }

        var r = await c.PutAsJsonAsync($"/api/predictions/{p.Code}", Input(confidence: 80, reason: "Cambié de opinión"), Json);
        var after = await c.GetFromJsonAsync<PredictionDetailView>($"/api/predictions/{p.Code}", Json);

        Assert.Contains("versión nueva", await Error(r, HttpStatusCode.Conflict), StringComparison.Ordinal);
        Assert.True(after!.IsLocked);
        Assert.Equal(60, after.Confidence);
    }

    [Fact]
    public async Task Version_nueva_no_reemplaza_a_la_original()
    {
        var c = await LoginAsync();
        var v1 = await Read(await c.PostAsJsonAsync($"/api/signals/{await SignalAsync(c)}/predictions", Input(), Json), HttpStatusCode.Created);

        var v2 = await Read(await c.PostAsJsonAsync($"/api/predictions/{v1.Code}/versions",
            Input("Cinco pymes adoptarán el flujo antes de junio", 45, "Vi evidencia contraria"), Json), HttpStatusCode.Created);
        var original = await c.GetFromJsonAsync<PredictionDetailView>($"/api/predictions/{v1.Code}", Json);
        var twice = await c.PostAsJsonAsync($"/api/predictions/{v1.Code}/versions", Input(reason: "otra rama"), Json);

        Assert.Equal(2, v2.Version);
        Assert.Equal(v1.Code, v2.SupersedesCode);
        Assert.Equal(PredictionStatus.Open, original!.Status);
        Assert.Equal([v2.Code], original.SupersededBy);
        Assert.Equal(60, original.Confidence);
        Assert.False(twice.IsSuccessStatusCode);
    }

    [Fact]
    public async Task Resolver_y_disputar_conserva_ambas_resoluciones()
    {
        var c = await LoginAsync();
        var p = await Read(await c.PostAsJsonAsync($"/api/signals/{await SignalAsync(c)}/predictions", Input(), Json), HttpStatusCode.Created);

        var badPartial = await c.PostAsJsonAsync($"/api/predictions/{p.Code}/resolution",
            new ResolvePrediction(PredictionOutcome.Partial, null, "Solo dos pymes firmaron", null), Json);
        await Read(await c.PostAsJsonAsync($"/api/predictions/{p.Code}/resolution",
            new ResolvePrediction(PredictionOutcome.Partial, 0.6m, "Solo dos pymes firmaron contrato", null), Json));
        var disputed = await Read(await c.PostAsJsonAsync($"/api/predictions/{p.Code}/resolution",
            new ResolvePrediction(PredictionOutcome.Confirmed, null, "La tercera firmó con fecha anterior al límite", "https://example.com/contrato"), Json));
        var withdraw = await c.PostAsJsonAsync($"/api/predictions/{p.Code}/withdraw", new WithdrawPrediction("Ya no aplica de ninguna forma"), Json);

        Assert.Contains("crédito", await Error(badPartial, HttpStatusCode.BadRequest), StringComparison.Ordinal);
        Assert.Equal(PredictionStatus.Resolved, disputed.Status);
        Assert.Equal([PredictionOutcome.Partial, PredictionOutcome.Confirmed], disputed.Resolutions.Select(r => r.Outcome));
        Assert.Equal([false, true], disputed.Resolutions.Select(r => r.IsCurrent));
        Assert.Equal(0.6m, disputed.Resolutions[0].PartialCredit);
        Assert.Equal(HttpStatusCode.BadRequest, withdraw.StatusCode);
    }

    [Fact]
    public async Task Retirar_exige_motivo_y_queda_visible()
    {
        var c = await LoginAsync();
        var p = await Read(await c.PostAsJsonAsync($"/api/signals/{await SignalAsync(c)}/predictions", Input(), Json), HttpStatusCode.Created);

        var short_ = await c.PostAsJsonAsync($"/api/predictions/{p.Code}/withdraw", new WithdrawPrediction("no"), Json);
        var withdrawn = await Read(await c.PostAsJsonAsync($"/api/predictions/{p.Code}/withdraw", new WithdrawPrediction("Estaba mal formulada"), Json));
        var resolveAfter = await c.PostAsJsonAsync($"/api/predictions/{p.Code}/resolution",
            new ResolvePrediction(PredictionOutcome.Failed, null, "Intento de resolver una retirada", null), Json);
        var list = await c.GetFromJsonAsync<PredictionPage>("/api/predictions?status=Withdrawn", Json);

        Assert.Contains("por qué la retiras", await Error(short_, HttpStatusCode.BadRequest), StringComparison.Ordinal);
        Assert.Equal(PredictionStatus.Withdrawn, withdrawn.Status);
        Assert.Equal("Estaba mal formulada", withdrawn.WithdrawalReason);
        Assert.NotNull(withdrawn.WithdrawnAt);
        Assert.Equal(HttpStatusCode.BadRequest, resolveAfter.StatusCode);
        Assert.Contains(list!.Items, i => i.Code == p.Code);
    }

    [Fact]
    public async Task Las_predicciones_vencidas_aparecen_en_el_filtro_de_vencidas()
    {
        var c = await LoginAsync();
        var p = await Read(await c.PostAsJsonAsync($"/api/signals/{await SignalAsync(c)}/predictions", Input(), Json), HttpStatusCode.Created);
        await using (var owner = await db.OpenOwnerAsync())
        {
            await DatabaseFixture.Exec(owner, $"""
                SET session_replication_role = replica;
                UPDATE predictions SET horizon_date = current_date - 3 WHERE code = '{p.Code}';
                SET session_replication_role = origin;
                """);
        }

        var overdue = await c.GetFromJsonAsync<PredictionPage>("/api/predictions?due=Overdue", Json);
        var detail = await c.GetFromJsonAsync<PredictionDetailView>($"/api/predictions/{p.Code}", Json);

        Assert.Contains(overdue!.Items, i => i.Code == p.Code && i.IsOverdue);
        Assert.True(detail!.IsOverdue);
    }

    [Fact]
    public async Task Cada_accion_queda_auditada_y_las_tres_cadenas_siguen_integras()
    {
        var c = await LoginAsync();
        var p = await Read(await c.PostAsJsonAsync($"/api/signals/{await SignalAsync(c)}/predictions", Input(), Json), HttpStatusCode.Created);
        await c.PutAsJsonAsync($"/api/predictions/{p.Code}", Input(confidence: 65, reason: "Ajuste de confianza"), Json);
        await c.PostAsJsonAsync($"/api/predictions/{p.Code}/resolution",
            new ResolvePrediction(PredictionOutcome.Failed, null, "Ninguna pyme firmó antes del límite", null), Json);

        var audit = await c.GetFromJsonAsync<List<AuditEntryDto>>($"/api/audit?entityType=prediction&entityId={p.Code}");
        var integrity = await c.GetFromJsonAsync<IntegrityDto>("/api/integrity/verify");

        Assert.Equal(["RESOLVE", "CORRECT", "CREATE"], audit!.Select(a => a.Action));
        Assert.True(integrity!.IsValid);
        Assert.Equal(["audit_log", "signal_versions", "prediction_snapshots"], integrity.Chains.Select(ch => ch.Chain));
    }
}
