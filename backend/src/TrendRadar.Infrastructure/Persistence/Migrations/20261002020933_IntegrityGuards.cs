using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrendRadar.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Protecciones de integridad en la base de datos (ARCHITECTURE §6, capas 2 y 3):
    /// triggers append-only, cadena de hashes del log de auditoría, verificación,
    /// permisos del rol de aplicación y taxonomía inicial.
    /// Cualquier migración que toque estos objetos requiere revisión explícita.
    /// </summary>
    public partial class IntegrityGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE EXTENSION IF NOT EXISTS pgcrypto;

                -- Rechaza UPDATE, DELETE o TRUNCATE en tablas históricas.
                CREATE FUNCTION forbid_modification() RETURNS trigger
                LANGUAGE plpgsql AS $$
                BEGIN
                    RAISE EXCEPTION '%: % prohibido (registro histórico inmutable)', TG_TABLE_NAME, TG_OP
                        USING ERRCODE = 'insufficient_privilege';
                END $$;

                -- Hash del contenido de una entrada. jsonb normaliza el orden de claves y espacios.
                CREATE FUNCTION audit_log_content_hash(
                    p_occurred_at timestamptz, p_user_id uuid, p_action text, p_entity_type text,
                    p_entity_id text, p_old jsonb, p_new jsonb, p_reason text) RETURNS bytea
                LANGUAGE sql STABLE AS $$
                    SELECT digest(jsonb_build_object(
                        'occurred_at_us', floor(extract(epoch FROM p_occurred_at) * 1000000)::bigint,
                        'user_id', p_user_id,
                        'action', p_action,
                        'entity_type', p_entity_type,
                        'entity_id', p_entity_id,
                        'old_value', p_old,
                        'new_value', p_new,
                        'reason', p_reason)::text, 'sha256')
                $$;

                -- Asigna id, fecha y hashes en el servidor, serializando las inserciones
                -- para que el orden de la cadena coincida con el de los ids. Los ids son
                -- consecutivos (sin huecos), así que una entrada eliminada también se nota.
                CREATE FUNCTION audit_log_before_insert() RETURNS trigger
                LANGUAGE plpgsql AS $$
                DECLARE
                    prev_id bigint;
                    prev bytea;
                BEGIN
                    PERFORM pg_advisory_xact_lock(74610001);
                    SELECT id, chain_hash INTO prev_id, prev FROM audit_log ORDER BY id DESC LIMIT 1;
                    NEW.id := coalesce(prev_id, 0) + 1;
                    NEW.occurred_at := clock_timestamp();
                    NEW.content_hash := audit_log_content_hash(
                        NEW.occurred_at, NEW.user_id, NEW.action, NEW.entity_type,
                        NEW.entity_id, NEW.old_value, NEW.new_value, NEW.reason);
                    NEW.chain_hash := digest(coalesce(prev, ''::bytea) || NEW.content_hash, 'sha256');
                    RETURN NEW;
                END $$;

                CREATE TRIGGER audit_log_before_insert BEFORE INSERT ON audit_log
                    FOR EACH ROW EXECUTE FUNCTION audit_log_before_insert();
                CREATE TRIGGER audit_log_no_modification BEFORE UPDATE OR DELETE ON audit_log
                    FOR EACH ROW EXECUTE FUNCTION forbid_modification();
                CREATE TRIGGER audit_log_no_truncate BEFORE TRUNCATE ON audit_log
                    FOR EACH STATEMENT EXECUTE FUNCTION forbid_modification();

                -- Usuarios y dominios se desactivan, nunca se borran.
                CREATE TRIGGER users_no_delete BEFORE DELETE ON users
                    FOR EACH ROW EXECUTE FUNCTION forbid_modification();
                CREATE TRIGGER users_no_truncate BEFORE TRUNCATE ON users
                    FOR EACH STATEMENT EXECUTE FUNCTION forbid_modification();
                CREATE TRIGGER domains_no_delete BEFORE DELETE ON domains
                    FOR EACH ROW EXECUTE FUNCTION forbid_modification();
                CREATE TRIGGER domains_no_truncate BEFORE TRUNCATE ON domains
                    FOR EACH STATEMENT EXECUTE FUNCTION forbid_modification();

                -- Recorre la cadena en orden y devuelve la primera entrada inválida, si la hay.
                CREATE FUNCTION audit_log_verify()
                RETURNS TABLE (checked_entries bigint, first_invalid_id bigint)
                LANGUAGE plpgsql STABLE AS $$
                DECLARE
                    r audit_log;
                    prev bytea := ''::bytea;
                    n bigint := 0;
                BEGIN
                    FOR r IN SELECT * FROM audit_log ORDER BY id LOOP
                        n := n + 1;
                        IF r.id <> n
                           OR r.content_hash IS DISTINCT FROM audit_log_content_hash(
                               r.occurred_at, r.user_id, r.action, r.entity_type,
                               r.entity_id, r.old_value, r.new_value, r.reason)
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

                -- Permisos del rol de aplicación (lo crea el entorno: docker/postgres/init.sh).
                REVOKE ALL ON users, domains, audit_log, refresh_tokens FROM PUBLIC;
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'trendradar_app') THEN
                        GRANT SELECT, INSERT, UPDATE ON users TO trendradar_app;
                        GRANT SELECT ON domains TO trendradar_app;
                        GRANT SELECT, INSERT ON audit_log TO trendradar_app;
                        EXECUTE format('GRANT USAGE ON SEQUENCE %s TO trendradar_app',
                            pg_get_serial_sequence('audit_log', 'id'));
                        GRANT SELECT, INSERT, UPDATE ON refresh_tokens TO trendradar_app;
                    ELSE
                        RAISE NOTICE 'Rol trendradar_app no existe: permisos no asignados.';
                    END IF;
                END $$;

                -- Taxonomía inicial (DATA_MODEL §3.1).
                INSERT INTO domains (id, code, name, parent_id, is_active) VALUES
                    (gen_random_uuid(), 'TECH',  'Tecnología',   NULL, true),
                    (gen_random_uuid(), 'AI',    'IA',           NULL, true),
                    (gen_random_uuid(), 'UX',    'UX / Producto', NULL, true),
                    (gen_random_uuid(), 'BIZ',   'Negocio',      NULL, true),
                    (gen_random_uuid(), 'CUST',  'Cliente',      NULL, true),
                    (gen_random_uuid(), 'CULT',  'Cultura',      NULL, true),
                    (gen_random_uuid(), 'MUSIC', 'Música',       NULL, true),
                    (gen_random_uuid(), 'MKT',   'Mercado',      NULL, true);

                INSERT INTO domains (id, code, name, parent_id, is_active)
                SELECT gen_random_uuid(), p.code || '.' || s.code, s.name, p.id, true
                FROM (VALUES
                    ('TECH', 'CLOUD', 'Cloud'), ('TECH', 'DATA', 'Datos'), ('TECH', 'APIS', 'APIs'),
                    ('TECH', 'DEVTOOLS', 'Herramientas de desarrollo'), ('TECH', 'ROBOTICS', 'Robótica'),
                    ('TECH', 'SECURITY', 'Ciberseguridad'), ('TECH', 'INTERFACES', 'Interfaces'),
                    ('AI', 'AGENTS', 'Agentes'), ('AI', 'LLM', 'LLMs'), ('AI', 'AUTOMATION', 'Automatización'),
                    ('UX', 'NAVIGATION', 'Navegación'), ('UX', 'IA', 'Arquitectura de información'),
                    ('UX', 'INTERACTION', 'Diseño de interacción'), ('UX', 'DASHBOARDS', 'Dashboards'),
                    ('UX', 'DENSITY', 'Densidad visual'), ('UX', 'ONBOARDING', 'Onboarding'),
                    ('UX', 'PERSONALIZATION', 'Personalización'), ('UX', 'WORKFLOW', 'Diseño de flujos'),
                    ('UX', 'ACCESSIBILITY', 'Accesibilidad'),
                    ('BIZ', 'SAAS', 'SaaS'), ('BIZ', 'PRICING', 'Precios'), ('BIZ', 'SALES', 'Ventas'),
                    ('BIZ', 'MARKETING', 'Marketing'), ('BIZ', 'OPERATIONS', 'Operaciones'),
                    ('BIZ', 'PRODUCTIVITY', 'Productividad'), ('BIZ', 'ENTREPRENEURSHIP', 'Emprendimiento'),
                    ('CUST', 'UNMET', 'Necesidades no satisfechas'), ('CUST', 'IMPLICIT', 'Necesidades implícitas'),
                    ('CUST', 'PAIN', 'Puntos de dolor recurrentes'), ('CUST', 'MANUAL', 'Flujos manuales (Excel, WhatsApp)'),
                    ('CUST', 'ADMIN', 'Trabajo administrativo repetitivo'),
                    ('CULT', 'FASHION', 'Moda'), ('CULT', 'ENTERTAINMENT', 'Entretenimiento'),
                    ('CULT', 'AESTHETICS', 'Estética visual'), ('CULT', 'SOCIAL', 'Comportamiento social'),
                    ('MUSIC', 'TRACKS', 'Selección de pistas'), ('MUSIC', 'ARTISTS', 'Artistas'), ('MUSIC', 'GENRES', 'Géneros'),
                    ('MKT', 'COMPETITION', 'Competencia'), ('MKT', 'ADOPTION', 'Adopción'), ('MKT', 'REGULATION', 'Regulación')
                ) AS s(parent, code, name)
                JOIN domains p ON p.code = s.parent;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS domains_no_truncate ON domains;
                DROP TRIGGER IF EXISTS domains_no_delete ON domains;
                DELETE FROM domains WHERE parent_id IS NOT NULL;
                DELETE FROM domains;
                DROP TRIGGER IF EXISTS users_no_truncate ON users;
                DROP TRIGGER IF EXISTS users_no_delete ON users;
                DROP TRIGGER IF EXISTS audit_log_no_truncate ON audit_log;
                DROP TRIGGER IF EXISTS audit_log_no_modification ON audit_log;
                DROP TRIGGER IF EXISTS audit_log_before_insert ON audit_log;
                DROP FUNCTION IF EXISTS audit_log_verify();
                DROP FUNCTION IF EXISTS audit_log_before_insert();
                DROP FUNCTION IF EXISTS audit_log_content_hash(timestamptz, uuid, text, text, text, jsonb, jsonb, text);
                DROP FUNCTION IF EXISTS forbid_modification();
                """);
        }
    }
}
