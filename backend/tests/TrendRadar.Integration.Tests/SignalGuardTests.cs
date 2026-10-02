using Npgsql;

namespace TrendRadar.Integration.Tests;

/// <summary>Criterio de salida de M1: el registro original de una señal no se puede alterar en la base de datos.</summary>
[Collection(DatabaseTestGroup.Name)]
public sealed class SignalGuardTests(DatabaseFixture db)
{
    private static async Task<Guid> UserAsync(NpgsqlConnection c)
    {
        var id = Guid.NewGuid();
        await DatabaseFixture.Exec(c, $"""
            INSERT INTO users (id, email, display_name, password_hash, role, is_active)
            VALUES ('{id}', '{id}@example.com', 'Prueba', 'x', 'Owner', true)
            """);
        return id;
    }

    private static async Task<(Guid Id, string Code)> SignalAsync(NpgsqlConnection c, Guid user, string domain = "TECH", string? code = null)
    {
        var id = Guid.NewGuid();
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO signals (id, signal_code, original_title, original_text, recorded_by, recorded_tz, source_type,
                                 domain_id, declared_retrospective, stage, status, confidentiality, recorded_at)
            VALUES (@id, @code, 'Título', 'Texto original', @user, 'America/Bogota', 'Observation',
                    (SELECT id FROM domains WHERE code = @domain), false, 'Intuition', 'Active', 'Internal', '2000-01-01')
            RETURNING signal_code
            """,
            c);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("code", (object?)code ?? DBNull.Value);
        cmd.Parameters.AddWithValue("user", user);
        cmd.Parameters.AddWithValue("domain", domain);
        var assigned = (string)(await cmd.ExecuteScalarAsync())!;
        await DatabaseFixture.Exec(c, $"""
            INSERT INTO signal_versions (signal_id, title, text, change_reason, created_by)
            VALUES ('{id}', 'Título', 'Texto original', 'Registro original', '{user}')
            """);
        return (id, assigned);
    }

    [Theory]
    [InlineData("original_text = 'reescrito'")]
    [InlineData("original_title = 'reescrito'")]
    [InlineData("recorded_at = '2001-01-01'")]
    [InlineData("signal_code = 'TR-TECH-999'")]
    [InlineData("is_retrospective = true")]
    public async Task Ni_el_propietario_puede_reescribir_el_registro_original(string assignment)
    {
        await using var owner = await db.OpenOwnerAsync();
        var (id, _) = await SignalAsync(owner, await UserAsync(owner));

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => DatabaseFixture.Exec(owner, $"UPDATE signals SET {assignment} WHERE id = '{id}'"));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
        Assert.Contains("inmutable", ex.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_rol_de_aplicacion_solo_puede_actualizar_la_clasificacion()
    {
        await using var app = await db.OpenAppAsync();
        var (id, _) = await SignalAsync(app, await UserAsync(app));

        await DatabaseFixture.Exec(app, $"UPDATE signals SET stage = 'Signal', status = 'Dormant' WHERE id = '{id}'");
        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => DatabaseFixture.Exec(app, $"UPDATE signals SET original_text = 'x' WHERE id = '{id}'"));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
        Assert.DoesNotContain("inmutable", ex.MessageText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("DELETE FROM signals")]
    [InlineData("TRUNCATE signals CASCADE")]
    [InlineData("UPDATE signal_versions SET text = 'x'")]
    [InlineData("DELETE FROM signal_versions")]
    [InlineData("UPDATE evidence SET description = 'x'")]
    [InlineData("DELETE FROM evidence")]
    public async Task Senales_versiones_y_evidencia_no_se_borran_ni_se_modifican(string sql)
    {
        await using var owner = await db.OpenOwnerAsync();
        var user = await UserAsync(owner);
        var (id, _) = await SignalAsync(owner, user);
        await DatabaseFixture.Exec(owner, $"""
            INSERT INTO evidence (id, signal_id, kind, role, description, timestamp_authority, level, recorded_by)
            VALUES ('{Guid.NewGuid()}', '{id}', 'Note', 'Supports', 'nota', 'None', 'E0', '{user}')
            """);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => DatabaseFixture.Exec(owner, sql));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
    }

    [Fact]
    public async Task El_servidor_fija_la_fecha_de_registro_y_la_version_actual()
    {
        await using var app = await db.OpenAppAsync();
        var (id, _) = await SignalAsync(app, await UserAsync(app));

        var year = await DatabaseFixture.Scalar<double>(app, $"SELECT extract(year FROM recorded_at)::float8 FROM signals WHERE id = '{id}'");
        var version = await DatabaseFixture.Scalar<int>(app, $"SELECT current_version FROM signals WHERE id = '{id}'");

        Assert.True(year >= 2026);
        Assert.Equal(1, version);
    }

    [Fact]
    public async Task La_version_1_debe_reproducir_el_registro_original()
    {
        await using var app = await db.OpenAppAsync();
        var user = await UserAsync(app);
        var id = Guid.NewGuid();
        await DatabaseFixture.Exec(app, $"""
            INSERT INTO signals (id, original_title, original_text, recorded_by, recorded_tz, source_type, domain_id,
                                 declared_retrospective, stage, status, confidentiality)
            VALUES ('{id}', 'Título', 'Texto', '{user}', 'America/Bogota', 'Observation',
                    (SELECT id FROM domains WHERE code = 'TECH'), false, 'Intuition', 'Active', 'Internal')
            """);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => DatabaseFixture.Exec(app, $"""
            INSERT INTO signal_versions (signal_id, title, text, change_reason, created_by)
            VALUES ('{id}', 'Título', 'Texto distinto', 'Registro original', '{user}')
            """));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task Los_codigos_son_consecutivos_por_dominio_incluso_en_paralelo()
    {
        await using var setup = await db.OpenAppAsync();
        var user = await UserAsync(setup);

        var codes = await Task.WhenAll(Enumerable.Range(0, 25).Select(async _ =>
        {
            await using var c = await db.OpenAppAsync();
            return (await SignalAsync(c, user, "CULT")).Code;
        }));

        var numbers = codes.Select(c => int.Parse(c["TR-CULT-".Length..], System.Globalization.CultureInfo.InvariantCulture)).Order().ToList();
        Assert.All(codes, c => Assert.Matches(@"^TR-CULT-\d{3}$", c));
        Assert.Equal(Enumerable.Range(numbers[0], 25), numbers);
    }

    [Fact]
    public async Task Un_codigo_importado_hace_avanzar_el_contador_y_debe_ser_del_dominio()
    {
        await using var app = await db.OpenAppAsync();
        var user = await UserAsync(app);

        await SignalAsync(app, user, "MKT", "TR-MKT-040");
        var next = (await SignalAsync(app, user, "MKT")).Code;
        var ex = await Assert.ThrowsAsync<PostgresException>(() => SignalAsync(app, user, "MKT", "TR-UX-041"));

        Assert.Equal("TR-MKT-041", next);
        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }

    [Fact]
    public async Task La_cadena_de_versiones_detecta_alteraciones()
    {
        await using var owner = await db.OpenOwnerAsync();
        var (id, _) = await SignalAsync(owner, await UserAsync(owner));
        Assert.Null(await FirstInvalidAsync(owner));

        await DatabaseFixture.Exec(owner, $"""
            SET session_replication_role = replica;
            UPDATE signal_versions SET text = 'reescrito' WHERE signal_id = '{id}';
            SET session_replication_role = origin;
            """);
        var broken = await FirstInvalidAsync(owner);

        await DatabaseFixture.Exec(owner, $"""
            SET session_replication_role = replica;
            UPDATE signal_versions SET text = 'Texto original' WHERE signal_id = '{id}';
            SET session_replication_role = origin;
            """);

        Assert.NotNull(broken);
        Assert.Null(await FirstInvalidAsync(owner));
    }

    private static async Task<long?> FirstInvalidAsync(NpgsqlConnection c)
    {
        var value = await DatabaseFixture.Scalar<object>(c, "SELECT first_invalid_id FROM signal_versions_verify()");
        return value is DBNull ? null : (long)value;
    }
}
