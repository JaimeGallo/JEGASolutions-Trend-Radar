using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TrendRadar.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Foresight : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hypotheses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    signal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    statement = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_hypotheses", x => x.id);
                    table.ForeignKey(
                        name: "fk_hypotheses_signals_signal_id",
                        column: x => x.signal_id,
                        principalTable: "signals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_hypotheses_users_recorded_by",
                        column: x => x.recorded_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "predictions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    signal_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hypothesis_id = table.Column<Guid>(type: "uuid", nullable: true),
                    statement = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    resolution_criteria = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    horizon_date = table.Column<DateOnly>(type: "date", nullable: false),
                    confidence = table.Column<int>(type: "integer", nullable: false),
                    base_rate = table.Column<int>(type: "integer", nullable: false),
                    specificity = table.Column<int>(type: "integer", nullable: false),
                    evidence_snapshot = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    recorded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    withdrawal_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_predictions", x => x.id);
                    table.CheckConstraint("ck_predictions_base_rate", "base_rate BETWEEN 1 AND 99");
                    table.CheckConstraint("ck_predictions_confidence", "confidence BETWEEN 1 AND 99");
                    table.CheckConstraint("ck_predictions_specificity", "specificity BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "fk_predictions_hypotheses_hypothesis_id",
                        column: x => x.hypothesis_id,
                        principalTable: "hypotheses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_predictions_predictions_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "predictions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_predictions_signals_signal_id",
                        column: x => x.signal_id,
                        principalTable: "signals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_predictions_users_recorded_by",
                        column: x => x.recorded_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prediction_resolutions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    prediction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    partial_credit = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                    rationale = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    evidence_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    resolved_by = table.Column<Guid>(type: "uuid", nullable: false),
                    supersedes_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prediction_resolutions", x => x.id);
                    table.CheckConstraint("ck_prediction_resolutions_partial", "(outcome = 'Partial' AND partial_credit BETWEEN 0.05 AND 0.95) OR (outcome <> 'Partial' AND partial_credit IS NULL)");
                    table.ForeignKey(
                        name: "fk_prediction_resolutions_prediction_resolutions_supersedes_id",
                        column: x => x.supersedes_id,
                        principalTable: "prediction_resolutions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prediction_resolutions_predictions_prediction_id",
                        column: x => x.prediction_id,
                        principalTable: "predictions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_prediction_resolutions_users_resolved_by",
                        column: x => x.resolved_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "prediction_snapshots",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    prediction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    content_hash = table.Column<byte[]>(type: "bytea", nullable: false, defaultValueSql: "'\\x'::bytea"),
                    chain_hash = table.Column<byte[]>(type: "bytea", nullable: false, defaultValueSql: "'\\x'::bytea")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_prediction_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_prediction_snapshots_predictions_prediction_id",
                        column: x => x.prediction_id,
                        principalTable: "predictions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_hypotheses_code",
                table: "hypotheses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_hypotheses_recorded_by",
                table: "hypotheses",
                column: "recorded_by");

            migrationBuilder.CreateIndex(
                name: "ix_hypotheses_signal_id",
                table: "hypotheses",
                column: "signal_id");

            migrationBuilder.CreateIndex(
                name: "ix_prediction_resolutions_prediction_id",
                table: "prediction_resolutions",
                column: "prediction_id");

            migrationBuilder.CreateIndex(
                name: "ix_prediction_resolutions_resolved_by",
                table: "prediction_resolutions",
                column: "resolved_by");

            migrationBuilder.CreateIndex(
                name: "ix_prediction_resolutions_supersedes_id",
                table: "prediction_resolutions",
                column: "supersedes_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_prediction_snapshots_prediction_id",
                table: "prediction_snapshots",
                column: "prediction_id");

            migrationBuilder.CreateIndex(
                name: "ix_predictions_code",
                table: "predictions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_predictions_hypothesis_id",
                table: "predictions",
                column: "hypothesis_id");

            migrationBuilder.CreateIndex(
                name: "ix_predictions_recorded_by",
                table: "predictions",
                column: "recorded_by");

            migrationBuilder.CreateIndex(
                name: "ix_predictions_signal_id",
                table: "predictions",
                column: "signal_id");

            migrationBuilder.CreateIndex(
                name: "ix_predictions_status_horizon_date",
                table: "predictions",
                columns: new[] { "status", "horizon_date" });

            migrationBuilder.CreateIndex(
                name: "ix_predictions_supersedes_id",
                table: "predictions",
                column: "supersedes_id",
                unique: true);

            // Protecciones de hipótesis y predicciones (DATA_MODEL §4). Revisión explícita para cualquier cambio.
            migrationBuilder.Sql("""
                CREATE SEQUENCE hypothesis_code_seq;
                CREATE SEQUENCE prediction_code_seq;

                CREATE FUNCTION padded_code(prefix text, n bigint) RETURNS text
                LANGUAGE sql IMMUTABLE AS $$
                    SELECT prefix || '-' || CASE WHEN n < 10000 THEN lpad(n::text, 4, '0') ELSE n::text END
                $$;

                -- Hipótesis: enunciado inmutable; solo cambia el estado.
                CREATE FUNCTION hypotheses_before_insert() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    NEW.code := padded_code('HY', nextval('hypothesis_code_seq'));
                    NEW.recorded_at := now();
                    NEW.status := 'Open';
                    RETURN NEW;
                END $$;

                CREATE FUNCTION hypotheses_guard_update() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    IF (NEW.id, NEW.code, NEW.signal_id, NEW.statement, NEW.rationale, NEW.recorded_at, NEW.recorded_by)
                       IS DISTINCT FROM
                       (OLD.id, OLD.code, OLD.signal_id, OLD.statement, OLD.rationale, OLD.recorded_at, OLD.recorded_by) THEN
                        RAISE EXCEPTION 'hypotheses: el enunciado de % es inmutable', OLD.code
                            USING ERRCODE = 'insufficient_privilege';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE TRIGGER hypotheses_before_insert BEFORE INSERT ON hypotheses
                    FOR EACH ROW EXECUTE FUNCTION hypotheses_before_insert();
                CREATE TRIGGER hypotheses_guard_update BEFORE UPDATE ON hypotheses
                    FOR EACH ROW EXECUTE FUNCTION hypotheses_guard_update();
                CREATE TRIGGER hypotheses_no_delete BEFORE DELETE ON hypotheses
                    FOR EACH ROW EXECUTE FUNCTION forbid_modification();
                CREATE TRIGGER hypotheses_no_truncate BEFORE TRUNCATE ON hypotheses
                    FOR EACH STATEMENT EXECUTE FUNCTION forbid_modification();

                -- Predicciones: código, fecha y fin del período de gracia los fija el servidor.
                CREATE FUNCTION predictions_before_insert() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    prev predictions;
                BEGIN
                    NEW.code := padded_code('PR', nextval('prediction_code_seq'));
                    NEW.recorded_at := now();
                    NEW.locked_at := now() + interval '15 minutes';
                    NEW.status := 'Open';
                    NEW.withdrawn_at := NULL;
                    NEW.withdrawal_reason := NULL;

                    IF NEW.supersedes_id IS NULL THEN
                        NEW.version := 1;
                    ELSE
                        SELECT * INTO prev FROM predictions WHERE id = NEW.supersedes_id FOR UPDATE;
                        IF prev.signal_id IS DISTINCT FROM NEW.signal_id THEN
                            RAISE EXCEPTION 'predictions: una versión nueva debe ser de la misma señal'
                                USING ERRCODE = 'check_violation';
                        END IF;
                        IF prev.status <> 'Open' THEN
                            RAISE EXCEPTION 'predictions: solo se versiona una predicción abierta'
                                USING ERRCODE = 'check_violation';
                        END IF;
                        NEW.version := prev.version + 1;
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE FUNCTION predictions_guard_update() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    -- Siempre inmutables.
                    IF (NEW.id, NEW.code, NEW.signal_id, NEW.hypothesis_id, NEW.recorded_at, NEW.recorded_by,
                        NEW.locked_at, NEW.supersedes_id, NEW.version)
                       IS DISTINCT FROM
                       (OLD.id, OLD.code, OLD.signal_id, OLD.hypothesis_id, OLD.recorded_at, OLD.recorded_by,
                        OLD.locked_at, OLD.supersedes_id, OLD.version) THEN
                        RAISE EXCEPTION 'predictions: la identidad y las fechas de % son inmutables', OLD.code
                            USING ERRCODE = 'insufficient_privilege';
                    END IF;

                    -- Contenido: editable solo durante el período de gracia.
                    IF (NEW.statement, NEW.resolution_criteria, NEW.horizon_date, NEW.confidence, NEW.base_rate,
                        NEW.specificity, NEW.evidence_snapshot)
                       IS DISTINCT FROM
                       (OLD.statement, OLD.resolution_criteria, OLD.horizon_date, OLD.confidence, OLD.base_rate,
                        OLD.specificity, OLD.evidence_snapshot) THEN
                        IF now() >= OLD.locked_at THEN
                            RAISE EXCEPTION 'predictions: % está bloqueada desde %', OLD.code, OLD.locked_at
                                USING ERRCODE = 'insufficient_privilege';
                        END IF;
                        IF OLD.status <> 'Open' THEN
                            RAISE EXCEPTION 'predictions: % ya no está abierta', OLD.code
                                USING ERRCODE = 'insufficient_privilege';
                        END IF;
                    END IF;

                    -- Transiciones de estado válidas.
                    IF NEW.status IS DISTINCT FROM OLD.status THEN
                        IF OLD.status <> 'Open' OR NEW.status NOT IN ('Resolved', 'Withdrawn') THEN
                            RAISE EXCEPTION 'predictions: transición % -> % no permitida', OLD.status, NEW.status
                                USING ERRCODE = 'check_violation';
                        END IF;
                        IF NEW.status = 'Resolved'
                           AND NOT EXISTS (SELECT 1 FROM prediction_resolutions WHERE prediction_id = OLD.id) THEN
                            RAISE EXCEPTION 'predictions: % no tiene resolución registrada', OLD.code
                                USING ERRCODE = 'check_violation';
                        END IF;
                        IF NEW.status = 'Withdrawn' THEN
                            IF coalesce(length(trim(NEW.withdrawal_reason)), 0) < 10 THEN
                                RAISE EXCEPTION 'predictions: retirar exige un motivo' USING ERRCODE = 'check_violation';
                            END IF;
                            NEW.withdrawn_at := now();
                        END IF;
                    ELSIF (NEW.withdrawn_at, NEW.withdrawal_reason) IS DISTINCT FROM (OLD.withdrawn_at, OLD.withdrawal_reason) THEN
                        RAISE EXCEPTION 'predictions: el retiro de % es inmutable', OLD.code
                            USING ERRCODE = 'insufficient_privilege';
                    END IF;
                    RETURN NEW;
                END $$;

                CREATE TRIGGER predictions_before_insert BEFORE INSERT ON predictions
                    FOR EACH ROW EXECUTE FUNCTION predictions_before_insert();
                CREATE TRIGGER predictions_guard_update BEFORE UPDATE ON predictions
                    FOR EACH ROW EXECUTE FUNCTION predictions_guard_update();
                CREATE TRIGGER predictions_no_delete BEFORE DELETE ON predictions
                    FOR EACH ROW EXECUTE FUNCTION forbid_modification();
                CREATE TRIGGER predictions_no_truncate BEFORE TRUNCATE ON predictions
                    FOR EACH STATEMENT EXECUTE FUNCTION forbid_modification();

                -- Fotos encadenadas de cada estado de cada predicción, escritas por el propio servidor.
                CREATE FUNCTION prediction_snapshots_content_hash(
                    p_prediction_id uuid, p_snapshot jsonb, p_created_at timestamptz) RETURNS bytea
                LANGUAGE sql STABLE AS $$
                    SELECT digest(jsonb_build_object(
                        'prediction_id', p_prediction_id,
                        'snapshot', p_snapshot,
                        'created_at_us', floor(extract(epoch FROM p_created_at) * 1000000)::bigint)::text, 'sha256')
                $$;

                CREATE FUNCTION prediction_snapshots_before_insert() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    prev_id bigint;
                    prev bytea;
                BEGIN
                    PERFORM pg_advisory_xact_lock(74610003);
                    SELECT id, chain_hash INTO prev_id, prev FROM prediction_snapshots ORDER BY id DESC LIMIT 1;
                    NEW.id := coalesce(prev_id, 0) + 1;
                    NEW.created_at := clock_timestamp();
                    NEW.content_hash := prediction_snapshots_content_hash(NEW.prediction_id, NEW.snapshot, NEW.created_at);
                    NEW.chain_hash := digest(coalesce(prev, ''::bytea) || NEW.content_hash, 'sha256');
                    RETURN NEW;
                END $$;

                CREATE FUNCTION predictions_snapshot() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    INSERT INTO prediction_snapshots (prediction_id, snapshot) VALUES (NEW.id, to_jsonb(NEW));
                    RETURN NULL;
                END $$;

                CREATE TRIGGER prediction_snapshots_before_insert BEFORE INSERT ON prediction_snapshots
                    FOR EACH ROW EXECUTE FUNCTION prediction_snapshots_before_insert();
                CREATE TRIGGER prediction_snapshots_no_modification BEFORE UPDATE OR DELETE ON prediction_snapshots
                    FOR EACH ROW EXECUTE FUNCTION forbid_modification();
                CREATE TRIGGER prediction_snapshots_no_truncate BEFORE TRUNCATE ON prediction_snapshots
                    FOR EACH STATEMENT EXECUTE FUNCTION forbid_modification();
                CREATE TRIGGER predictions_snapshot AFTER INSERT OR UPDATE ON predictions
                    FOR EACH ROW EXECUTE FUNCTION predictions_snapshot();

                CREATE FUNCTION prediction_snapshots_verify()
                RETURNS TABLE (checked_entries bigint, first_invalid_id bigint)
                LANGUAGE plpgsql STABLE AS $$
                DECLARE
                    r prediction_snapshots;
                    prev bytea := ''::bytea;
                    n bigint := 0;
                BEGIN
                    FOR r IN SELECT * FROM prediction_snapshots ORDER BY id LOOP
                        n := n + 1;
                        IF r.id <> n
                           OR r.content_hash IS DISTINCT FROM prediction_snapshots_content_hash(r.prediction_id, r.snapshot, r.created_at)
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

                -- Resoluciones: append-only; disputar es agregar otra que reemplaza a la última.
                CREATE FUNCTION prediction_resolutions_before_insert() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    p predictions;
                    latest uuid;
                BEGIN
                    SELECT * INTO p FROM predictions WHERE id = NEW.prediction_id FOR UPDATE;
                    IF p.status = 'Withdrawn' THEN
                        RAISE EXCEPTION 'prediction_resolutions: % fue retirada', p.code USING ERRCODE = 'check_violation';
                    END IF;
                    SELECT id INTO latest FROM prediction_resolutions
                        WHERE prediction_id = NEW.prediction_id ORDER BY resolved_at DESC, id DESC LIMIT 1;
                    IF latest IS DISTINCT FROM NEW.supersedes_id THEN
                        RAISE EXCEPTION 'prediction_resolutions: una disputa debe reemplazar a la resolución vigente'
                            USING ERRCODE = 'check_violation';
                    END IF;
                    NEW.resolved_at := clock_timestamp();
                    RETURN NEW;
                END $$;

                CREATE FUNCTION prediction_resolutions_after_insert() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    UPDATE predictions SET status = 'Resolved' WHERE id = NEW.prediction_id AND status = 'Open';
                    RETURN NULL;
                END $$;

                CREATE TRIGGER prediction_resolutions_before_insert BEFORE INSERT ON prediction_resolutions
                    FOR EACH ROW EXECUTE FUNCTION prediction_resolutions_before_insert();
                CREATE TRIGGER prediction_resolutions_after_insert AFTER INSERT ON prediction_resolutions
                    FOR EACH ROW EXECUTE FUNCTION prediction_resolutions_after_insert();
                CREATE TRIGGER prediction_resolutions_no_modification BEFORE UPDATE OR DELETE ON prediction_resolutions
                    FOR EACH ROW EXECUTE FUNCTION forbid_modification();
                CREATE TRIGGER prediction_resolutions_no_truncate BEFORE TRUNCATE ON prediction_resolutions
                    FOR EACH STATEMENT EXECUTE FUNCTION forbid_modification();

                -- Permisos del rol de aplicación.
                REVOKE ALL ON hypotheses, predictions, prediction_resolutions, prediction_snapshots FROM PUBLIC;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'trendradar_app') THEN
                        GRANT SELECT, INSERT ON hypotheses TO trendradar_app;
                        GRANT UPDATE (status) ON hypotheses TO trendradar_app;
                        GRANT SELECT, INSERT ON predictions TO trendradar_app;
                        GRANT UPDATE (statement, resolution_criteria, horizon_date, confidence, base_rate, specificity,
                                      evidence_snapshot, status, withdrawn_at, withdrawal_reason)
                            ON predictions TO trendradar_app;
                        GRANT SELECT, INSERT ON prediction_resolutions TO trendradar_app;
                        GRANT SELECT, INSERT ON prediction_snapshots TO trendradar_app;
                        EXECUTE format('GRANT USAGE ON SEQUENCE %s TO trendradar_app',
                            pg_get_serial_sequence('prediction_snapshots', 'id'));
                        GRANT USAGE ON SEQUENCE hypothesis_code_seq, prediction_code_seq TO trendradar_app;
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
                DROP TRIGGER IF EXISTS predictions_snapshot ON predictions;
                DROP TRIGGER IF EXISTS prediction_resolutions_no_truncate ON prediction_resolutions;
                DROP TRIGGER IF EXISTS prediction_resolutions_no_modification ON prediction_resolutions;
                DROP TRIGGER IF EXISTS prediction_resolutions_after_insert ON prediction_resolutions;
                DROP TRIGGER IF EXISTS prediction_resolutions_before_insert ON prediction_resolutions;
                DROP TRIGGER IF EXISTS prediction_snapshots_no_truncate ON prediction_snapshots;
                DROP TRIGGER IF EXISTS prediction_snapshots_no_modification ON prediction_snapshots;
                DROP TRIGGER IF EXISTS prediction_snapshots_before_insert ON prediction_snapshots;
                DROP TRIGGER IF EXISTS predictions_no_truncate ON predictions;
                DROP TRIGGER IF EXISTS predictions_no_delete ON predictions;
                DROP TRIGGER IF EXISTS predictions_guard_update ON predictions;
                DROP TRIGGER IF EXISTS predictions_before_insert ON predictions;
                DROP TRIGGER IF EXISTS hypotheses_no_truncate ON hypotheses;
                DROP TRIGGER IF EXISTS hypotheses_no_delete ON hypotheses;
                DROP TRIGGER IF EXISTS hypotheses_guard_update ON hypotheses;
                DROP TRIGGER IF EXISTS hypotheses_before_insert ON hypotheses;
                DROP FUNCTION IF EXISTS prediction_resolutions_after_insert();
                DROP FUNCTION IF EXISTS prediction_resolutions_before_insert();
                DROP FUNCTION IF EXISTS prediction_snapshots_verify();
                DROP FUNCTION IF EXISTS predictions_snapshot();
                DROP FUNCTION IF EXISTS prediction_snapshots_before_insert();
                DROP FUNCTION IF EXISTS prediction_snapshots_content_hash(uuid, jsonb, timestamptz);
                DROP FUNCTION IF EXISTS predictions_guard_update();
                DROP FUNCTION IF EXISTS predictions_before_insert();
                DROP FUNCTION IF EXISTS hypotheses_guard_update();
                DROP FUNCTION IF EXISTS hypotheses_before_insert();
                DROP FUNCTION IF EXISTS padded_code(text, bigint);
                DROP SEQUENCE IF EXISTS prediction_code_seq;
                DROP SEQUENCE IF EXISTS hypothesis_code_seq;
                """);

            migrationBuilder.DropTable(
                name: "prediction_resolutions");

            migrationBuilder.DropTable(
                name: "prediction_snapshots");

            migrationBuilder.DropTable(
                name: "predictions");

            migrationBuilder.DropTable(
                name: "hypotheses");
        }
    }
}
