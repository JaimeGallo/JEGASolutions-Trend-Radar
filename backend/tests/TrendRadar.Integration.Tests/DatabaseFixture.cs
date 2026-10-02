using Microsoft.Extensions.Configuration;
using Npgsql;

namespace TrendRadar.Integration.Tests;

/// <summary>
/// Crea una base de datos desechable, aplica las migraciones con el rol propietario
/// y expone una conexión que actúa con los permisos del rol de aplicación (SET ROLE),
/// igual que en producción, sin depender de su contraseña.
/// Requiere PostgreSQL 16: por defecto el de docker compose (puerto 5433);
/// se puede cambiar con TRENDRADAR_TEST_ADMIN_CONNECTION.
/// </summary>
public sealed class DatabaseFixture : IAsyncLifetime
{
    public const string OwnerEmail = "jaime@example.com";
    public const string OwnerPassword = "contraseña-de-prueba-123";

    private const string DefaultAdmin =
        "Host=localhost;Port=5433;Database=postgres;Username=trendradar_owner;Password=trendradar_owner_dev";

    private readonly string _adminConnection =
        Environment.GetEnvironmentVariable("TRENDRADAR_TEST_ADMIN_CONNECTION") ?? DefaultAdmin;

    public string DatabaseName { get; } = $"trendradar_test_{Guid.NewGuid():N}";

    public string OwnerConnection => With(_adminConnection, DatabaseName, null);

    public string AppConnection => With(_adminConnection, DatabaseName, "-c role=trendradar_app");

    public async Task InitializeAsync()
    {
        await using (var admin = new NpgsqlConnection(_adminConnection))
        {
            await admin.OpenAsync();
            await Exec(admin, """
                DO $$ BEGIN
                    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'trendradar_app') THEN
                        CREATE ROLE trendradar_app NOLOGIN;
                    END IF;
                END $$;
                """);
            await Exec(admin, $"CREATE DATABASE {DatabaseName}");
        }

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:TrendRadarMigrations"] = OwnerConnection,
            })
            .Build();
        await Infrastructure.DependencyInjection.MigrateAsync(config);

        // Propietario de prueba creado antes de cualquier prueba, para no depender del orden.
        await using var owner = await OpenOwnerAsync();
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO users (id, email, display_name, password_hash, role, is_active)
            VALUES (@id, @email, 'Jaime Gallo', @hash, 'Owner', true)
            """,
            owner);
        cmd.Parameters.AddWithValue("id", Guid.CreateVersion7());
        cmd.Parameters.AddWithValue("email", OwnerEmail);
        cmd.Parameters.AddWithValue("hash", BCrypt.Net.BCrypt.HashPassword(OwnerPassword, 4));
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var admin = new NpgsqlConnection(_adminConnection);
        await admin.OpenAsync();
        await Exec(admin, $"DROP DATABASE IF EXISTS {DatabaseName} WITH (FORCE)");
    }

    public static async Task Exec(NpgsqlConnection connection, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, connection);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<T> Scalar<T>(NpgsqlConnection connection, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, connection);
        return (T)(await cmd.ExecuteScalarAsync())!;
    }

    public Task<NpgsqlConnection> OpenAppAsync() => OpenAsync(AppConnection);

    public Task<NpgsqlConnection> OpenOwnerAsync() => OpenAsync(OwnerConnection);

    private static async Task<NpgsqlConnection> OpenAsync(string connectionString)
    {
        var c = new NpgsqlConnection(connectionString);
        await c.OpenAsync();
        return c;
    }

    private static string With(string baseConnection, string database, string? options) =>
        new NpgsqlConnectionStringBuilder(baseConnection) { Database = database, Options = options }.ConnectionString;
}

[CollectionDefinition(Name)]
public sealed class DatabaseTestGroup : ICollectionFixture<DatabaseFixture>
{
    public const string Name = "database";
}
