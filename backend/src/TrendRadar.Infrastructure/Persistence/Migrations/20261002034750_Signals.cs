using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;

#nullable disable

namespace TrendRadar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Signals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Búsqueda sin tildes: unaccent no es IMMUTABLE, así que se envuelve para usarlo en la columna generada.
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS unaccent;
                CREATE FUNCTION f_unaccent(text) RETURNS text
                LANGUAGE sql IMMUTABLE PARALLEL SAFE STRICT AS $$
                    SELECT public.unaccent('public.unaccent'::regdictionary, $1)
                $$;
                """);

            migrationBuilder.CreateTable(
                name: "signals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    signal_code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    original_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    original_text = table.Column<string>(type: "text", nullable: false),
                    original_context = table.Column<string>(type: "text", nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_tz = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    confidence_at_creation = table.Column<int>(type: "integer", nullable: true),
                    source_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    domain_id = table.Column<Guid>(type: "uuid", nullable: false),
                    claimed_origin_earliest = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    claimed_origin_latest = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    claimed_origin_precision = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    claimed_origin_note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    declared_retrospective = table.Column<bool>(type: "boolean", nullable: false),
                    is_retrospective = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    imported_from = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    subdomain_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    geographic_scope = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    impact_estimate = table.Column<int>(type: "integer", nullable: true),
                    relevance_to_jegas = table.Column<int>(type: "integer", nullable: true),
                    confidentiality = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    current_version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    search_vector = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true, computedColumnSql: "to_tsvector('simple', f_unaccent(coalesce(signal_code, '') || ' ' || original_title || ' ' || original_text || ' ' || coalesce(original_context, '')))", stored: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_signals", x => x.id);
                    table.ForeignKey(
                        name: "fk_signals_domains_domain_id",
                        column: x => x.domain_id,
                        principalTable: "domains",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_signals_domains_subdomain_id",
                        column: x => x.subdomain_id,
                        principalTable: "domains",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_signals_users_recorded_by",
                        column: x => x.recorded_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "evidence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    signal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    git_repo = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    git_commit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    file_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    file_sha256 = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    file_mime = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    file_size = table.Column<long>(type: "bigint", nullable: true),
                    artifact_timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    timestamp_authority = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    level = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_evidence", x => x.id);
                    table.ForeignKey(
                        name: "fk_evidence_signals_signal_id",
                        column: x => x.signal_id,
                        principalTable: "signals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_evidence_users_recorded_by",
                        column: x => x.recorded_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "signal_versions",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    signal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    context = table.Column<string>(type: "text", nullable: true),
                    confidence = table.Column<int>(type: "integer", nullable: true),
                    change_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    content_hash = table.Column<byte[]>(type: "bytea", nullable: false, defaultValueSql: "'\\x'::bytea"),
                    chain_hash = table.Column<byte[]>(type: "bytea", nullable: false, defaultValueSql: "'\\x'::bytea")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_signal_versions", x => x.id);
                    table.ForeignKey(
                        name: "fk_signal_versions_signals_signal_id",
                        column: x => x.signal_id,
                        principalTable: "signals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_signal_versions_users_created_by",
                        column: x => x.created_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_evidence_recorded_by",
                table: "evidence",
                column: "recorded_by");

            migrationBuilder.CreateIndex(
                name: "ix_evidence_signal_id",
                table: "evidence",
                column: "signal_id");

            migrationBuilder.CreateIndex(
                name: "ix_signal_versions_created_by",
                table: "signal_versions",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_signal_versions_signal_id_version",
                table: "signal_versions",
                columns: new[] { "signal_id", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_signals_domain_id_recorded_at",
                table: "signals",
                columns: new[] { "domain_id", "recorded_at" });

            migrationBuilder.CreateIndex(
                name: "ix_signals_recorded_by",
                table: "signals",
                column: "recorded_by");

            migrationBuilder.CreateIndex(
                name: "ix_signals_search_vector",
                table: "signals",
                column: "search_vector")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "ix_signals_signal_code",
                table: "signals",
                column: "signal_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_signals_subdomain_id",
                table: "signals",
                column: "subdomain_id");

            // Protecciones del registro de señales (ARCHITECTURE §6). Revisión explícita para cualquier cambio.
            migrationBuilder.Sql("""
                CREATE TABLE signal_code_counters (
                    domain_code varchar(40) PRIMARY KEY,
                    last_number integer NOT NULL
                );

                -- Código TR-DOMINIO-NNN, fecha de registro del servidor y marca retrospectiva.
                CREATE FUNCTION signals_before_insert() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    dcode text;
                    n integer;
                BEGIN
                    SELECT code INTO dcode FROM domains WHERE id = NEW.domain_id AND parent_id IS NULL;
                    IF dcode IS NULL THEN
                        RAISE EXCEPTION 'signals: el dominio debe ser de primer nivel' USING ERRCODE = 'check_violation';
                    END IF;

                    IF NEW.signal_code IS NULL THEN
                        INSERT INTO signal_code_counters AS c VALUES (dcode, 1)
                            ON CONFLICT (domain_code) DO UPDATE SET last_number = c.last_number + 1
                            RETURNING last_number INTO n;
                        NEW.signal_code := 'TR-' || dcode || '-'
                            || CASE WHEN n < 1000 THEN lpad(n::text, 3, '0') ELSE n::text END;
                    ELSE
                        -- Importación de la Fase 0: se conserva el código y el contador avanza.
                        IF NEW.signal_code !~ ('^TR-' || dcode || '-[0-9]{3,}$') THEN
                            RAISE EXCEPTION 'signals: código % no corresponde al dominio %', NEW.signal_code, dcode
                                USING ERRCODE = 'check_violation';
                        END IF;
                        n := substring(NEW.signal_code FROM '([0-9]+)$')::integer;
                        INSERT INTO signal_code_counters AS c VALUES (dcode, n)
                            ON CONFLICT (domain_code) DO UPDATE SET last_number = greatest(c.last_number, n);
                    END IF;

                    NEW.recorded_at := now();
                    NEW.current_version := 0;
                    NEW.is_retrospective := NEW.declared_retrospective
                        OR (NEW.claimed_origin_latest IS NOT NULL AND NEW.claimed_origin_latest < now() - interval '7 days');
                    RETURN NEW;
                END $$;

                CREATE TRIGGER signals_before_insert BEFORE INSERT ON signals
                    FOR EACH ROW EXECUTE FUNCTION signals_before_insert();

                -- El registro original no cambia nunca; solo la clasificación y la versión actual.
                CREATE FUNCTION signals_guard_update() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF (NEW.id, NEW.signal_code, NEW.original_title, NEW.original_text, NEW.original_context,
                        NEW.recorded_at, NEW.recorded_by, NEW.recorded_tz, NEW.confidence_at_creation, NEW.source_type,
                        NEW.domain_id, NEW.claimed_origin_earliest, NEW.claimed_origin_latest, NEW.claimed_origin_precision,
                        NEW.claimed_origin_note, NEW.declared_retrospective, NEW.is_retrospective, NEW.imported_from)
                       IS DISTINCT FROM
                       (OLD.id, OLD.signal_code, OLD.original_title, OLD.original_text, OLD.original_context,
                        OLD.recorded_at, OLD.recorded_by, OLD.recorded_tz, OLD.confidence_at_creation, OLD.source_type,
                        OLD.domain_id, OLD.claimed_origin_earliest, OLD.claimed_origin_latest, OLD.claimed_origin_precision,
                        OLD.claimed_origin_note, OLD.declared_retrospective, OLD.is_retrospective, OLD.imported_from) THEN
                        RAISE EXCEPTION 'signals: el registro original de % es inmutable', OLD.signal_code
                            USING ERRCODE = 'insufficient_privilege';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE TRIGGER signals_guard_update BEFORE UPDATE ON signals
                    FOR EACH ROW EXECUTE FUNCTION signals_guard_update();
                CREATE TRIGGER signals_no_delete BEFORE DELETE ON signals
                    FOR EACH ROW EXECUTE FUNCTION forbid_modification();
                CREATE TRIGGER signals_no_truncate BEFORE TRUNCATE ON signals
                    FOR EACH STATEMENT EXECUTE FUNCTION forbid_modification();

                -- Versiones: append-only, encadenadas con SHA-256, ids consecutivos.
                CREATE FUNCTION signal_versions_content_hash(
                    p_signal_id uuid, p_version integer, p_title text, p_text text, p_context text,
                    p_confidence integer, p_reason text, p_created_at timestamptz, p_created_by uuid) RETURNS bytea
                LANGUAGE sql STABLE AS $$
                    SELECT digest(jsonb_build_object(
                        'signal_id', p_signal_id,
                        'version', p_version,
                        'title', p_title,
                        'text', p_text,
                        'context', p_context,
                        'confidence', p_confidence,
                        'change_reason', p_reason,
                        'created_at_us', floor(extract(epoch FROM p_created_at) * 1000000)::bigint,
                        'created_by', p_created_by)::text, 'sha256')
                $$;

                CREATE FUNCTION signal_versions_before_insert() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    prev_id bigint;
                    prev bytea;
                    original record;
                BEGIN
                    PERFORM pg_advisory_xact_lock(74610002);
                    SELECT id, chain_hash INTO prev_id, prev FROM signal_versions ORDER BY id DESC LIMIT 1;
                    NEW.id := coalesce(prev_id, 0) + 1;
                    SELECT coalesce(max(version), 0) + 1 INTO NEW.version FROM signal_versions WHERE signal_id = NEW.signal_id;

                    -- La versión 1 debe reproducir exactamente el registro original.
                    IF NEW.version = 1 THEN
                        SELECT original_title, original_text, original_context, confidence_at_creation
                            INTO original FROM signals WHERE id = NEW.signal_id;
                        IF (NEW.title, NEW.text, NEW.context, NEW.confidence)
                           IS DISTINCT FROM (original.original_title, original.original_text,
                                             original.original_context, original.confidence_at_creation) THEN
                            RAISE EXCEPTION 'signal_versions: la versión 1 debe ser el registro original'
                                USING ERRCODE = 'check_violation';
                        END IF;
                    END IF;

                    NEW.created_at := clock_timestamp();
                    NEW.content_hash := signal_versions_content_hash(
                        NEW.signal_id, NEW.version, NEW.title, NEW.text, NEW.context,
                        NEW.confidence, NEW.change_reason, NEW.created_at, NEW.created_by);
                    NEW.chain_hash := digest(coalesce(prev, ''::bytea) || NEW.content_hash, 'sha256');
                    RETURN NEW;
                END $$;

                CREATE FUNCTION signal_versions_after_insert() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    UPDATE signals SET current_version = NEW.version WHERE id = NEW.signal_id;
                    RETURN NULL;
                END $$;

                CREATE TRIGGER signal_versions_before_insert BEFORE INSERT ON signal_versions
                    FOR EACH ROW EXECUTE FUNCTION signal_versions_before_insert();
                CREATE TRIGGER signal_versions_after_insert AFTER INSERT ON signal_versions
                    FOR EACH ROW EXECUTE FUNCTION signal_versions_after_insert();
                CREATE TRIGGER signal_versions_no_modification BEFORE UPDATE OR DELETE ON signal_versions
                    FOR EACH ROW EXECUTE FUNCTION forbid_modification();
                CREATE TRIGGER signal_versions_no_truncate BEFORE TRUNCATE ON signal_versions
                    FOR EACH STATEMENT EXECUTE FUNCTION forbid_modification();

                CREATE FUNCTION signal_versions_verify()
                RETURNS TABLE (checked_entries bigint, first_invalid_id bigint)
                LANGUAGE plpgsql STABLE AS $$
                DECLARE
                    r signal_versions;
                    prev bytea := ''::bytea;
                    n bigint := 0;
                BEGIN
                    FOR r IN SELECT * FROM signal_versions ORDER BY id LOOP
                        n := n + 1;
                        IF r.id <> n
                           OR r.content_hash IS DISTINCT FROM signal_versions_content_hash(
                               r.signal_id, r.version, r.title, r.text, r.context,
                               r.confidence, r.change_reason, r.created_at, r.created_by)
                           OR r.chain_hash IS DISTINCT FROM digest(prev || r.content_hash, 'sha256') THEN
                            checked_entries := n;
                            first_invalid_id := r.id;
                            RETURN NEXT;
                            RETURN;
                        END IF;
                        prev := r.chain_hash;
                    END LOOP;
                    checked_entries := n;
                    first_invalid_id := NULL;
                    RETURN NEXT;
                END $$;

                -- Evidencia: append-only con fecha de registro del servidor.
                CREATE FUNCTION evidence_before_insert() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    NEW.recorded_at := now();
                    RETURN NEW;
                END $$;

                CREATE TRIGGER evidence_before_insert BEFORE INSERT ON evidence
                    FOR EACH ROW EXECUTE FUNCTION evidence_before_insert();
                CREATE TRIGGER evidence_no_modification BEFORE UPDATE OR DELETE ON evidence
                    FOR EACH ROW EXECUTE FUNCTION forbid_modification();
                CREATE TRIGGER evidence_no_truncate BEFORE TRUNCATE ON evidence
                    FOR EACH STATEMENT EXECUTE FUNCTION forbid_modification();

                -- Permisos del rol de aplicación: UPDATE solo en columnas de clasificación.
                REVOKE ALL ON signals, signal_versions, evidence, signal_code_counters FROM PUBLIC;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'trendradar_app') THEN
                        GRANT SELECT, INSERT ON signals TO trendradar_app;
                        GRANT UPDATE (stage, status, subdomain_id, source_reference, geographic_scope,
                                      impact_estimate, relevance_to_jegas, confidentiality, current_version)
                            ON signals TO trendradar_app;
                        GRANT SELECT, INSERT ON signal_versions TO trendradar_app;
                        EXECUTE format('GRANT USAGE ON SEQUENCE %s TO trendradar_app',
                            pg_get_serial_sequence('signal_versions', 'id'));
                        GRANT SELECT, INSERT ON evidence TO trendradar_app;
                        GRANT SELECT, INSERT, UPDATE ON signal_code_counters TO trendradar_app;
                    ELSE
                        RAISE NOTICE 'Rol trendradar_app no existe: permisos no asignados.';
                    END IF;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS evidence_no_truncate ON evidence;
                DROP TRIGGER IF EXISTS evidence_no_modification ON evidence;
                DROP TRIGGER IF EXISTS evidence_before_insert ON evidence;
                DROP TRIGGER IF EXISTS signal_versions_no_truncate ON signal_versions;
                DROP TRIGGER IF EXISTS signal_versions_no_modification ON signal_versions;
                DROP TRIGGER IF EXISTS signal_versions_after_insert ON signal_versions;
                DROP TRIGGER IF EXISTS signal_versions_before_insert ON signal_versions;
                DROP TRIGGER IF EXISTS signals_no_truncate ON signals;
                DROP TRIGGER IF EXISTS signals_no_delete ON signals;
                DROP TRIGGER IF EXISTS signals_guard_update ON signals;
                DROP TRIGGER IF EXISTS signals_before_insert ON signals;
                DROP FUNCTION IF EXISTS evidence_before_insert();
                DROP FUNCTION IF EXISTS signal_versions_verify();
                DROP FUNCTION IF EXISTS signal_versions_after_insert();
                DROP FUNCTION IF EXISTS signal_versions_before_insert();
                DROP FUNCTION IF EXISTS signal_versions_content_hash(uuid, integer, text, text, text, integer, text, timestamptz, uuid);
                DROP FUNCTION IF EXISTS signals_guard_update();
                DROP FUNCTION IF EXISTS signals_before_insert();
                DROP TABLE IF EXISTS signal_code_counters;
                """);

            migrationBuilder.DropTable(
                name: "evidence");

            migrationBuilder.DropTable(
                name: "signal_versions");

            migrationBuilder.DropTable(
                name: "signals");

            migrationBuilder.Sql("DROP FUNCTION IF EXISTS f_unaccent(text);");
        }
    }
}
