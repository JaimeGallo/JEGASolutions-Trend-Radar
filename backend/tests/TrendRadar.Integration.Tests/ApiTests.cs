using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using TrendRadar.API;

namespace TrendRadar.Integration.Tests;

[Collection(DatabaseTestGroup.Name)]
public sealed class ApiTests(DatabaseFixture db) : IDisposable
{
    private const string OwnerEmail = "jaime@example.com";
    private const string OwnerPassword = "contraseña-de-prueba-123";

    private readonly WebApplicationFactory<Program> _factory = new WebApplicationFactory<Program>()
        .WithWebHostBuilder(b => b
            .UseEnvironment("Testing")
            .UseSetting("ConnectionStrings:TrendRadar", db.AppConnection)
            .UseSetting("Database:MigrateOnStartup", "false")
            .UseSetting("Auth:JwtSecret", "secreto-de-pruebas-con-mas-de-32-bytes")
            .UseSetting("Auth:SecureCookies", "false")
            .UseSetting("Bootstrap:OwnerEmail", OwnerEmail)
            .UseSetting("Bootstrap:OwnerPassword", OwnerPassword));

    public void Dispose() => _factory.Dispose();

    private async Task<(HttpClient Client, AuthResponse Auth)> LoginAsync(string email = OwnerEmail, string password = OwnerPassword)
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth);
    }

    [Fact]
    public async Task Health_responde_ok()
    {
        var response = await _factory.CreateClient().GetAsync(new Uri("/api/health", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Sin_token_los_endpoints_protegidos_responden_401()
    {
        var response = await _factory.CreateClient().GetAsync(new Uri("/api/domains", UriKind.Relative));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_con_contrasena_errada_responde_401_en_espanol()
    {
        var response = await _factory.CreateClient()
            .PostAsJsonAsync("/api/auth/login", new LoginRequest(OwnerEmail, "incorrecta-123456"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains("Correo o contraseña incorrectos", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Flujo_completo_login_me_dominios_refresh_y_reuso()
    {
        var (client, auth) = await LoginAsync();
        Assert.Equal("Owner", auth.User.Role);

        var me = await client.GetFromJsonAsync<UserDto>("/api/auth/me");
        Assert.Equal(OwnerEmail, me!.Email);

        var domains = await client.GetFromJsonAsync<List<DomainDto>>("/api/domains");
        Assert.Contains(domains!, d => d.Code == "UX" && d.ParentId is null);
        Assert.Contains(domains!, d => d.Code == "UX.DENSITY");

        // Rotación: el cliente guarda la cookie; se conserva la primera para intentar reusarla.
        var firstCookie = await RefreshCookieFromLoginAsync();
        var reuseClient = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var first = await PostWithCookie(reuseClient, firstCookie);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var reused = await PostWithCookie(reuseClient, firstCookie);
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
    }

    [Fact]
    public async Task Integridad_y_auditoria_reflejan_los_inicios_de_sesion()
    {
        var (client, _) = await LoginAsync();

        var integrity = await client.GetFromJsonAsync<IntegrityDto>("/api/integrity/verify");
        var audit = await client.GetFromJsonAsync<List<AuditEntryDto>>("/api/audit?entityType=user");

        Assert.True(integrity!.IsValid);
        Assert.True(integrity.CheckedEntries > 0);
        Assert.Contains(audit!, a => a.Action == "LOGIN");
        Assert.All(audit!, a => Assert.Equal(64, a.ChainHash.Length));
    }

    [Fact]
    public async Task Un_lector_no_puede_ver_auditoria_ni_crear_usuarios()
    {
        var (owner, _) = await LoginAsync();
        var email = $"lector-{Guid.NewGuid():N}@example.com";
        var created = await owner.PostAsJsonAsync("/api/users", new CreateUserRequest(email, "Lector", OwnerPassword, Domain.Users.UserRole.Viewer));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var (viewer, auth) = await LoginAsync(email, OwnerPassword);

        Assert.Equal("Viewer", auth.User.Role);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync(new Uri("/api/audit", UriKind.Relative))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync(new Uri("/api/domains", UriKind.Relative))).StatusCode);
    }

    private async Task<string> RefreshCookieFromLoginAsync()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(OwnerEmail, OwnerPassword));
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("tr_refresh=", StringComparison.Ordinal));
        Assert.Contains("httponly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", setCookie, StringComparison.OrdinalIgnoreCase);
        return setCookie.Split(';')[0];
    }

    private static Task<HttpResponseMessage> PostWithCookie(HttpClient client, string cookie)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        request.Headers.Add("Cookie", cookie);
        return client.SendAsync(request);
    }
}
