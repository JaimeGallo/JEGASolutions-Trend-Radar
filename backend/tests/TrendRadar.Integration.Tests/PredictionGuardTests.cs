using Npgsql;

namespace TrendRadar.Integration.Tests;

/// <summary>Criterio de salida de M2: tras el período de gracia, la base de datos rechaza cambios.</summary>
[Collection(DatabaseTestGroup.Name)]
public sealed class PredictionGuardTests(DatabaseFixture db)
{
    private const string Content = """
        statement = 'Otro enunciado distinto', resolution_criteria = 'Otro criterio distinto'
        """;

    private static async Task<(Guid User, Guid Signal)> SignalAsync(NpgsqlConnection c)
    {
        var user = Guid.NewGuid();
        var signal = Guid.NewGuid();
        await DatabaseFixture.Exec(c, $"""
            INSERT INTO users (id, email, display_name, password_hash, role, is_active)
            VALUES ('{user}', '{user}@example.com', 'Prueba', 'x', 'Owner', true);
            INSERT INTO signals (id, original_title, original_text, recorded_by, recorded_tz, source_type, domain_id,
                                 declared_retrospective, stage, status, confidentiality)
            VALUES ('{signal}', 'Señal', 'Texto', '{user}', 'America/Bogota', 'Observation',
                    (SELECT id FROM domains WHERE code = 'BIZ'), false, 'Intuition', 'Active', 'Internal');
            INSERT INTO signal_versions (signal_id, title, text, change_reason, created_by)
            VALUES ('{signal}', 'Señal', 'Texto', 'Registro original', '{user}');
            """);
        return (user, signal);
    }

    private static async Task<Guid> PredictionAsync(NpgsqlConnection c, Guid user, Guid signal, Guid? supersedes = null)
    {
        var id = Guid.NewGuid();
        await DatabaseFixture.Exec(c, $"""
            INSERT INTO predictions (id, signal_id, statement, resolution_criteria, horizon_date, confidence, base_rate,
                                     specificity, recorded_by, status, supersedes_id, recorded_at, locked_at, version)
            VALUES ('{id}', '{signal}', 'Enunciado de prueba', 'Criterio de prueba', '2030-01-01', 60, 20, 3,
                    '{user}', 'Resolved', {(supersedes is null ? "NULL" : $"'{supersedes}'")}, '2000-01-01', '2100-01-01', 99)
            """);
        return id;
    }

    /// <summary>Simula que pasaron los 15 minutos: solo un superusuario puede saltarse el trigger.</summary>
    private static Task ExpireGraceAsync(NpgsqlConnection owner, Guid id) => DatabaseFixture.Exec(owner, $"""
        SET session_replication_role = replica;
        UPDATE predictions SET locked_at = now() - interval '1 minute' WHERE id = '{id}';
        SET session_replication_role = origin;
        """);

    [Fact]
    public async Task El_servidor_fija_codigo_fechas_gracia_estado_y_version()
    {
        await using var app = await db.OpenAppAsync();
        var (user, signal) = await SignalAsync(app);
        var id = await PredictionAsync(app, user, signal);

        await using var cmd = new NpgsqlCommand(
            $"SELECT code, status, version, extract(epoch FROM locked_at - recorded_at)::int, extract(year FROM recorded_at)::int FROM predictions WHERE id = '{id}'", app);
        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();

        Assert.Matches(@"^PR-\d{4,}$", r.GetString(0));
        Assert.Equal("Open", r.GetString(1));
        Assert.Equal(1, r.GetInt32(2));
        Assert.Equal(15 * 60, r.GetInt32(3));
        Assert.True(r.GetInt32(4) >= 2026);
    }

    [Fact]
    public async Task Dentro_de_la_gracia_se_corrige_y_cada_estado_queda_fotografiado()
    {
        await using var app = await db.OpenAppAsync();
        var (user, signal) = await SignalAsync(app);
        var id = await PredictionAsync(app, user, signal);

        await DatabaseFixture.Exec(app, $"UPDATE predictions SET {Content} WHERE id = '{id}'");
        var snapshots = await DatabaseFixture.Scalar<long>(app, $"SELECT count(*) FROM prediction_snapshots WHERE prediction_id = '{id}'");

        Assert.Equal(2, snapshots);
    }

    [Theory]
    [InlineData(Content)]
    [InlineData("horizon_date = '2031-01-01'")]
    [InlineData("confidence = 90")]
    [InlineData("base_rate = 5")]
    public async Task Tras_la_gracia_ni_el_propietario_puede_cambiar_el_contenido(string assignment)
    {
        await using var owner = await db.OpenOwnerAsync();
        var (user, signal) = await SignalAsync(owner);
        var id = await PredictionAsync(owner, user, signal);
        await ExpireGraceAsync(owner, id);

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => DatabaseFixture.Exec(owner, $"UPDATE predictions SET {assignment} WHERE id = '{id}'"));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
        Assert.Contains("bloqueada", ex.MessageText, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("recorded_at = now()")]
    [InlineData("locked_at = now() + interval '1 day'")]
    [InlineData("code = 'PR-9999'")]
    [InlineData("version = 7")]
    public async Task Identidad_y_fechas_son_inmutables_incluso_durante_la_gracia(string assignment)
    {
        await using var owner = await db.OpenOwnerAsync();
        var (user, signal) = await SignalAsync(owner);
        var id = await PredictionAsync(owner, user, signal);

        var ex = await Assert.ThrowsAsync<PostgresException>(
            () => DatabaseFixture.Exec(owner, $"UPDATE predictions SET {assignment} WHERE id = '{id}'"));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
    }

    [Fact]
    public async Task Resolver_requiere_registrar_una_resolucion_y_retirar_requiere_motivo()
    {
        await using var app = await db.OpenAppAsync();
        var (user, signal) = await SignalAsync(app);
        var id = await PredictionAsync(app, user, signal);

        var fakeResolve = await Assert.ThrowsAsync<PostgresException>(
            () => DatabaseFixture.Exec(app, $"UPDATE predictions SET status = 'Resolved' WHERE id = '{id}'"));
        var noReason = await Assert.ThrowsAsync<PostgresException>(
            () => DatabaseFixture.Exec(app, $"UPDATE predictions SET status = 'Withdrawn' WHERE id = '{id}'"));

        Assert.Equal(PostgresErrorCodes.CheckViolation, fakeResolve.SqlState);
        Assert.Equal(PostgresErrorCodes.CheckViolation, noReason.SqlState);
    }

    [Fact]
    public async Task Una_resolucion_cierra_la_prediccion_y_las_disputas_no_se_bifurcan()
    {
        await using var app = await db.OpenAppAsync();
        var (user, signal) = await SignalAsync(app);
        var id = await PredictionAsync(app, user, signal);
        var first = Guid.NewGuid();
        string Insert(Guid rid, string outcome, Guid? supersedes) => $"""
            INSERT INTO prediction_resolutions (id, prediction_id, outcome, partial_credit, rationale, resolved_by, supersedes_id)
            VALUES ('{rid}', '{id}', '{outcome}', NULL, 'Razonamiento', '{user}', {(supersedes is null ? "NULL" : $"'{supersedes}'")})
            """;

        await DatabaseFixture.Exec(app, Insert(first, "Failed", null));
        var status = await DatabaseFixture.Scalar<string>(app, $"SELECT status FROM predictions WHERE id = '{id}'");
        var fork = await Assert.ThrowsAsync<PostgresException>(() => DatabaseFixture.Exec(app, Insert(Guid.NewGuid(), "Confirmed", null)));
        await DatabaseFixture.Exec(app, Insert(Guid.NewGuid(), "Confirmed", first));
        var reopen = await Assert.ThrowsAsync<PostgresException>(
            () => DatabaseFixture.Exec(app, $"UPDATE predictions SET status = 'Open' WHERE id = '{id}'"));
        var edit = await Assert.ThrowsAsync<PostgresException>(
            () => DatabaseFixture.Exec(app, $"UPDATE prediction_resolutions SET outcome = 'Confirmed' WHERE id = '{first}'"));

        Assert.Equal("Resolved", status);
        Assert.Equal(PostgresErrorCodes.CheckViolation, fork.SqlState);
        Assert.Equal(PostgresErrorCodes.CheckViolation, reopen.SqlState);
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, edit.SqlState);
    }

    [Fact]
    public async Task Las_versiones_forman_una_cadena_lineal_de_la_misma_senal()
    {
        await using var app = await db.OpenAppAsync();
        var (user, signal) = await SignalAsync(app);
        var (_, otherSignal) = await SignalAsync(app);
        var v1 = await PredictionAsync(app, user, signal);

        var v2 = await PredictionAsync(app, user, signal, v1);
        var version = await DatabaseFixture.Scalar<int>(app, $"SELECT version FROM predictions WHERE id = '{v2}'");
        var branch = await Assert.ThrowsAsync<PostgresException>(() => PredictionAsync(app, user, signal, v1));
        var crossSignal = await Assert.ThrowsAsync<PostgresException>(() => PredictionAsync(app, user, otherSignal, v2));

        Assert.Equal(2, version);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, branch.SqlState);
        Assert.Equal(PostgresErrorCodes.CheckViolation, crossSignal.SqlState);
    }

    [Theory]
    [InlineData("DELETE FROM predictions")]
    [InlineData("DELETE FROM prediction_snapshots")]
    [InlineData("UPDATE prediction_snapshots SET snapshot = '{}'")]
    [InlineData("DELETE FROM hypotheses")]
    public async Task Nada_del_historial_de_predicciones_se_borra(string sql)
    {
        await using var owner = await db.OpenOwnerAsync();
        var (user, signal) = await SignalAsync(owner);
        await PredictionAsync(owner, user, signal);
        await DatabaseFixture.Exec(owner, $"""
            INSERT INTO hypotheses (id, signal_id, statement, recorded_by, status)
            VALUES ('{Guid.NewGuid()}', '{signal}', 'Hipótesis de prueba', '{user}', 'Open')
            """);

        var ex = await Assert.ThrowsAsync<PostgresException>(() => DatabaseFixture.Exec(owner, sql));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
    }

    [Fact]
    public async Task La_cadena_de_fotos_detecta_una_alteracion()
    {
        await using var owner = await db.OpenOwnerAsync();
        var (user, signal) = await SignalAsync(owner);
        var id = await PredictionAsync(owner, user, signal);
        Assert.Null(await FirstInvalidAsync(owner));

        await DatabaseFixture.Exec(owner, $"""
            SET session_replication_role = replica;
            UPDATE prediction_snapshots SET snapshot = jsonb_set(snapshot, '{"{confidence}"}', '99') WHERE prediction_id = '{id}';
            SET session_replication_role = origin;
            """);
        var broken = await FirstInvalidAsync(owner);
        await DatabaseFixture.Exec(owner, $"""
            SET session_replication_role = replica;
            UPDATE prediction_snapshots SET snapshot = jsonb_set(snapshot, '{"{confidence}"}', '60') WHERE prediction_id = '{id}';
            SET session_replication_role = origin;
            """);

        Assert.NotNull(broken);
        Assert.Null(await FirstInvalidAsync(owner));
    }

    private static async Task<long?> FirstInvalidAsync(NpgsqlConnection c)
    {
        var value = await DatabaseFixture.Scalar<object>(c, "SELECT first_invalid_id FROM prediction_snapshots_verify()");
        return value is DBNull ? null : (long)value;
    }
}
