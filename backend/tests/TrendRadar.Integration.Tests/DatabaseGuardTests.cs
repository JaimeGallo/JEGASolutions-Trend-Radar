using Npgsql;

namespace TrendRadar.Integration.Tests;

/// <summary>Criterio de salida de la Fase 2: la base de datos impide alterar el historial.</summary>
[Collection(DatabaseTestGroup.Name)]
public sealed class DatabaseGuardTests(DatabaseFixture db)
{
    private static async Task<long> InsertAsync(NpgsqlConnection c, string value)
    {
        await using var cmd = new NpgsqlCommand(
            """
            INSERT INTO audit_log (action, entity_type, entity_id, new_value)
            VALUES ('CREATE', 'prueba', @v, jsonb_build_object('valor', @v))
            RETURNING id
            """,
            c);
        cmd.Parameters.AddWithValue("v", value);
        return (long)(await cmd.ExecuteScalarAsync())!;
    }

    [Theory]
    [InlineData("UPDATE audit_log SET reason = 'alterado'")]
    [InlineData("DELETE FROM audit_log")]
    [InlineData("TRUNCATE audit_log")]
    public async Task El_rol_de_aplicacion_no_puede_alterar_la_auditoria(string sql)
    {
        await using var app = await db.OpenAppAsync();
        await InsertAsync(app, "app");

        var ex = await Assert.ThrowsAsync<PostgresException>(() => DatabaseFixture.Exec(app, sql));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
    }

    [Theory]
    [InlineData("UPDATE audit_log SET reason = 'alterado'")]
    [InlineData("DELETE FROM audit_log")]
    [InlineData("TRUNCATE audit_log")]
    [InlineData("DELETE FROM domains")]
    public async Task Ni_siquiera_el_propietario_puede_alterar_historial(string sql)
    {
        await using var owner = await db.OpenOwnerAsync();
        await InsertAsync(owner, "owner");

        var ex = await Assert.ThrowsAsync<PostgresException>(() => DatabaseFixture.Exec(owner, sql));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, ex.SqlState);
        Assert.Contains("inmutable", ex.MessageText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task El_servidor_asigna_fecha_y_hashes_ignorando_los_del_cliente()
    {
        await using var app = await db.OpenAppAsync();

        var id = await DatabaseFixture.Scalar<long>(app, """
            INSERT INTO audit_log (id, occurred_at, action, entity_type, content_hash, chain_hash)
            VALUES (987654, '2000-01-01', 'CREATE', 'prueba', '\x00', '\x00')
            RETURNING id
            """);
        var year = await DatabaseFixture.Scalar<double>(app, $"SELECT extract(year FROM occurred_at)::float8 FROM audit_log WHERE id = {id}");
        var hashLength = await DatabaseFixture.Scalar<int>(app, $"SELECT length(chain_hash) FROM audit_log WHERE id = {id}");

        Assert.NotEqual(987654, id);
        Assert.True(year >= 2026);
        Assert.Equal(32, hashLength);
    }

    [Fact]
    public async Task La_verificacion_detecta_una_alteracion_hecha_saltandose_los_triggers()
    {
        await using var app = await db.OpenAppAsync();
        await InsertAsync(app, "a");
        var target = await InsertAsync(app, "b");
        await InsertAsync(app, "c");
        Assert.Null(await FirstInvalidAsync(app));

        // Un superusuario puede desactivar triggers: la cadena de hashes lo delata.
        await using (var owner = await db.OpenOwnerAsync())
        {
            await DatabaseFixture.Exec(owner, $$"""
                SET session_replication_role = replica;
                UPDATE audit_log SET new_value = '{"valor": "reescrito"}' WHERE id = {{target}};
                SET session_replication_role = origin;
                """);
        }

        Assert.Equal(target, await FirstInvalidAsync(app));

        // Restaurar el valor original deja la cadena válida otra vez (para no afectar otras pruebas).
        await using (var owner = await db.OpenOwnerAsync())
        {
            await DatabaseFixture.Exec(owner, $$"""
                SET session_replication_role = replica;
                UPDATE audit_log SET new_value = '{"valor": "b"}' WHERE id = {{target}};
                SET session_replication_role = origin;
                """);
        }

        Assert.Null(await FirstInvalidAsync(app));
    }

    [Fact]
    public async Task Inserciones_concurrentes_mantienen_la_cadena_valida()
    {
        await Task.WhenAll(Enumerable.Range(0, 40).Select(async i =>
        {
            await using var c = await db.OpenAppAsync();
            await InsertAsync(c, $"concurrente-{i}");
        }));

        await using var app = await db.OpenAppAsync();
        Assert.Null(await FirstInvalidAsync(app));
    }

    private static async Task<long?> FirstInvalidAsync(NpgsqlConnection c)
    {
        var value = await DatabaseFixture.Scalar<object>(c, "SELECT first_invalid_id FROM audit_log_verify()");
        return value is DBNull ? null : (long)value;
    }
}
