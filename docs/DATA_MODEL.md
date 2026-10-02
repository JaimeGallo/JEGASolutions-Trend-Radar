# DATA_MODEL: JEGASolutions Trend Radar

> Versión 0.1 (Fase 1, propuesta). PostgreSQL 16, nombres en snake_case, claves `uuid` (v7, ordenables por tiempo), tiempos `timestamptz` en UTC.

## 1. Convenciones

- **Inmutable (🔒):** el valor no puede cambiar tras la inserción. Protegido por trigger.
- **Append-only (📜):** la tabla solo admite `INSERT`. El rol de la app no tiene `UPDATE` ni `DELETE`.
- **Fecha incierta:** se guarda como trío `*_earliest`, `*_latest`, `*_precision` (`DAY`, `MONTH`, `QUARTER`, `YEAR`, `APPROXIMATE`). Para una fecha exacta, earliest = latest.
- **Procedencia de interpretación:** columnas `provenance` (`HUMAN`, `AI_GENERATED`) y `verification` (`UNVERIFIED`, `HUMAN_VERIFIED`, `DISPUTED`).
- Ninguna tabla histórica tiene borrado físico. Estados como `ARCHIVED` o `WITHDRAWN` reemplazan al borrado.

## 2. Diagrama de entidades (núcleo)

```
users ─┬─< signals ──< signal_versions 📜
       │      │ ├──< signal_origin_claims ──< evidence
       │      │ ├──< evidence
       │      │ └──<> hypotheses ──< predictions ──< prediction_versions 📜
       │      │                           └──< prediction_resolutions
       │      │
       │      └──< convergences >── external_events ──< event_sources >── sources
       │              │                    │  └──< exposures >── users
       │              │                    └── actors
       │              └──< convergence_assessments
       │
       ├──< opportunities ──< experiments ──< outcomes ──< learnings
       ├──< trends ──<> (signals, external_events)
       └──< audit_log 📜     integrity_anchors 📜     entity_links (grafo)
```

## 3. Tablas

### 3.1 Taxonomía

```sql
domains (
  id uuid PK, code text UNIQUE,              -- 'UX', 'TECH', 'AI', 'BIZ', 'CUST', 'CULT', 'MKT', 'MUSIC'
  name text, parent_id uuid NULL FK domains, -- jerarquía domain > subdomain
  is_active bool
)
tags (id uuid PK, name text UNIQUE, kind text)  -- kind: SEGMENT, TOPIC, CLIENT, FREE
```

Taxonomía inicial propuesta (pendiente de confirmar):

| Código | Dominio | Subdominios |
|---|---|---|
| TECH | Tecnología | Cloud, Data, APIs, Developer tools, Robotics, Cybersecurity, Interfaces |
| AI | IA (primer nivel por relevancia para JEGASolutions) | Agents, LLMs, Automation |
| UX | UX / Producto | Navigation, Information architecture, Interaction, Dashboards, Visual density, Onboarding, Personalization, Workflow, Accessibility |
| BIZ | Negocio | SaaS, Pricing, Sales, Marketing, Operations, Productivity, Entrepreneurship |
| CUST | Cliente | Unmet needs, Implicit needs, Pain points, Manual workflows (Excel, WhatsApp, administrativo) |
| CULT | Cultura | Fashion, Entertainment, Visual aesthetics, Social behavior |
| MUSIC | Música (separado por tener metodología propia de tasa base) | Album track selection, Artists, Genres |
| MKT | Mercado | Competencia, Adopción, Regulación |

Geografía (Colombia, Medellín, LatAm) va en `geographic_scope`; segmento (B2B, SMEs, enterprise) va como `tags.kind = SEGMENT`.

### 3.2 Señales

```sql
signals (
  id uuid PK,
  signal_code text UNIQUE 🔒,               -- TR-UX-001; secuencia por dominio, nunca reutilizada
  stage text,                                -- INTUITION | SIGNAL | OPPORTUNITY
  status text,                               -- ACTIVE | DORMANT | ARCHIVED
  original_title text 🔒,
  original_text text 🔒,                     -- redacción original exacta
  original_context text 🔒 NULL,             -- contexto en que surgió
  recorded_at timestamptz 🔒 DEFAULT now(),  -- reloj del servidor, nunca del cliente
  recorded_by uuid 🔒 FK users,
  recorded_tz text 🔒,                       -- zona horaria del autor, p. ej. America/Bogota
  confidence_at_creation smallint 🔒 NULL,   -- 0..100
  source_type text 🔒,                       -- OBSERVATION, PROJECT_DECISION, CLIENT_INTERACTION, PROTOTYPE,
                                             -- COMMIT, DOCUMENT, CONVERSATION, MARKET_OBSERVATION, REJECTED_IDEA, EXPERIMENT
  source_reference text NULL,
  is_retrospective bool 🔒,                  -- true si se registra algo ocurrido antes (ver origen)
  claimed_origin_earliest timestamptz 🔒 NULL,
  claimed_origin_latest   timestamptz 🔒 NULL,
  claimed_origin_precision text 🔒 NULL,
  verified_origin_at timestamptz NULL,       -- derivado de evidencia; solo puede moverse HACIA ATRÁS con nueva evidencia verificada,
                                             -- cada cambio auditado; nunca hacia adelante sin motivo
  verified_origin_level text NULL,           -- E0..E4 (ver RESEARCH_METHODOLOGY)
  domain_id uuid FK domains, subdomain_id uuid NULL FK domains,
  geographic_scope text NULL,
  impact_estimate smallint NULL,             -- 1..5
  relevance_to_jegas smallint NULL,          -- 1..5
  confidentiality text DEFAULT 'INTERNAL',   -- PUBLIC | INTERNAL | CLIENT_CONFIDENTIAL
  current_version int,
  search_vector tsvector GENERATED
)

signal_versions 📜 (
  id uuid PK, signal_id uuid FK, version int,  -- v1 = registro original, creado junto con la señal
  title text, description text,
  evidence_summary text, hypothesis_summary text,
  expected_horizon text NULL, confidence smallint NULL,
  change_reason text NOT NULL,                 -- obligatorio desde v2
  created_at timestamptz DEFAULT now(), created_by uuid,
  content_hash bytea, chain_hash bytea,
  UNIQUE (signal_id, version)
)

signal_origin_claims 📜 (                       -- cada afirmación sobre "cuándo existía esta idea"
  id uuid PK, signal_id uuid FK,
  claimed_earliest timestamptz, claimed_latest timestamptz, precision text,
  evidence_id uuid NULL FK evidence,
  evidence_level text,                          -- E0..E4
  statement text,                               -- p. ej. "Commit abc123 en jegasolutions-platform muestra layout sin scroll"
  created_at timestamptz DEFAULT now(), created_by uuid
)
```

> Los campos de la sec. 8 `hypothesis`, `prediction`, `evidence` no se guardan como texto en la señal sino como entidades enlazadas. `created_before_external_event` y `retrospective_status` se reemplazan por `convergences.temporal_relation` y `signals.is_retrospective` (ver DISCOVERY A2).

### 3.3 Evidencia

```sql
evidence (
  id uuid PK,
  kind text,               -- FILE, SCREENSHOT, URL, GIT_COMMIT, EMAIL, CHAT_MESSAGE, DOCUMENT, NOTE
  description text,
  file_path text NULL, file_sha256 bytea NULL, file_mime text NULL,
  url text NULL, archived_url text NULL,          -- p. ej. Wayback Machine
  git_repo text NULL, git_commit text NULL,
  git_author_date timestamptz NULL,               -- controlable por el autor: baja confianza
  git_push_date timestamptz NULL,                 -- registrada por GitHub: confianza alta
  artifact_timestamp timestamptz NULL,            -- fecha que la evidencia demuestra
  timestamp_authority text NULL,                  -- SELF, GIT_AUTHOR, GITHUB, EMAIL_PROVIDER, WAYBACK, TSA, CLIENT
  evidence_level text,                            -- E0..E4
  recorded_at timestamptz 🔒 DEFAULT now(), recorded_by uuid 🔒,
  confidentiality text
)
evidence_links (evidence_id, entity_type, entity_id, role)  -- role: SUPPORTS, CONTRADICTS, ORIGIN, RESOLUTION
```

### 3.4 Hipótesis y predicciones

```sql
hypotheses (
  id uuid PK, code text UNIQUE,               -- HY-0001
  statement text 🔒, rationale text 🔒 NULL,
  recorded_at timestamptz 🔒, recorded_by uuid 🔒,
  status text,                                -- OPEN | SUPPORTED | WEAKENED | REFUTED | SUPERSEDED
  provenance text, verification text
)
hypothesis_signals (hypothesis_id, signal_id)

predictions (
  id uuid PK, code text UNIQUE,               -- PR-0001
  hypothesis_id uuid NULL FK, signal_id uuid FK,
  statement text 🔒,                           -- enunciado exacto
  resolution_criteria text 🔒,                 -- cómo se decidirá, escrito ANTES
  horizon_date date 🔒,                        -- fecha límite de evaluación
  confidence smallint 🔒,                      -- probabilidad 1..99 de que ocurra
  base_rate_estimate smallint 🔒 NULL,         -- probabilidad 1..99 de que "le pase a cualquiera"; base del JAI
  specificity smallint 🔒,                     -- 1..5 según rúbrica (METRICS.md)
  evidence_snapshot text 🔒,                   -- resumen de la evidencia disponible en ese momento
  domain_id uuid FK,
  recorded_at timestamptz 🔒, recorded_by uuid 🔒,
  locked_at timestamptz NULL,                  -- recorded_at + 15 min; tras esto, todo 🔒 es definitivo
  supersedes_id uuid NULL FK predictions,      -- V2 apunta a V1; V1 sigue contando
  version int,                                 -- 1, 2, 3...
  status text                                  -- OPEN | OVERDUE | RESOLVED | WITHDRAWN
)

prediction_versions 📜 (                       -- foto completa de cada versión (incluidas correcciones en gracia)
  id uuid PK, prediction_id uuid, version int, snapshot jsonb,
  change_reason text, created_at timestamptz, created_by uuid,
  content_hash bytea, chain_hash bytea
)

prediction_resolutions 📜 (
  id uuid PK, prediction_id uuid FK,
  outcome text,          -- CONFIRMED | PARTIAL | FAILED | UNRESOLVABLE
  partial_credit numeric(3,2) NULL,             -- 0..1, solo si PARTIAL, justificado
  rationale text, resolved_at timestamptz DEFAULT now(), resolved_by uuid,
  verification text,                            -- una resolución puede ser disputada y reemplazada por otra fila
  supersedes_id uuid NULL
)
```

**Regla de versiones (sec. 24):** cuando Jaime cambia de opinión se crea una predicción nueva con `supersedes_id` y `version + 1`. Ambas se resuelven por separado contra su propia fecha y criterio. V1 nunca desaparece de las métricas; si se retira, cuenta como WITHDRAWN y se reporta aparte (y si se retiró después de que apareciera evidencia contraria, se trata como FAILED; ver METRICS.md).

### 3.5 Radar externo

```sql
actors (id uuid PK, name text, kind text, website text, country text)  -- COMPANY, STARTUP, PROJECT, RESEARCHER, COMMUNITY

sources (
  id uuid PK, url text, title text,
  source_type text,           -- PRIMARY | SECONDARY | COMMUNITY | USER_GENERATED
  publisher text NULL,
  published_earliest timestamptz NULL, published_latest timestamptz NULL, published_precision text NULL,
  retrieved_at timestamptz,   -- fecha de consulta
  excerpt text,               -- fragmento relevante preservado
  archived_url text NULL, content_sha256 bytea NULL,
  recorded_at timestamptz 🔒, recorded_by uuid 🔒,
  provenance text             -- HUMAN | AI_GENERATED (URL verificada)
)

external_events (
  id uuid PK, code text UNIQUE,                -- EX-0001
  title text, description text,
  event_type text,            -- PRODUCT_LAUNCH, FEATURE, UX_PATTERN, RESEARCH, FUNDING, ADOPTION_MILESTONE,
                              -- MARKET_SHIFT, CULTURAL, POPULARITY, OTHER
  actor_id uuid NULL FK,
  status text,                -- UNIDENTIFIED | CANDIDATE | CONFIRMED | DISPUTED
  observable_earliest timestamptz, observable_latest timestamptz, observable_precision text,
                              -- primera fecha en que fue PÚBLICAMENTE observable
  adoption_stage text NULL,   -- ANNOUNCED | AVAILABLE | ADOPTED | MAINSTREAM
  domain_id uuid FK,
  recorded_at timestamptz 🔒, recorded_by uuid 🔒,
  provenance text, verification text
)
event_sources (event_id, source_id, supports text)       -- supports: DATE, EXISTENCE, DETAIL, ADOPTION
event_prior_art (event_id, earlier_event_id, note)       -- eventos anteriores del mismo concepto

exposures (                                              -- cuándo vio Jaime el evento por primera vez
  id uuid PK, event_id uuid FK, user_id uuid FK,
  exposed_earliest timestamptz, exposed_latest timestamptz, precision text,
  context text, evidence_id uuid NULL,
  recorded_at timestamptz 🔒
)
```

### 3.6 Convergencias

```sql
convergences (
  id uuid PK, code text UNIQUE,                 -- CV-0001
  signal_id uuid FK, external_event_id uuid FK, prediction_id uuid NULL FK,
  temporal_relation text,       -- BEFORE | AFTER | SIMULTANEOUS | UNKNOWN (calculado, ver RESEARCH_METHODOLOGY)
  temporal_basis text,          -- PRE_REGISTERED | VERIFIED_ORIGIN | CLAIMED_ONLY
  ttc_min_days int NULL, ttc_max_days int NULL,             -- Time-to-Convergence como rango
  anticipation_window_min_days int NULL, anticipation_window_max_days int NULL,
  exposure_before_signal bool NULL,                          -- ¿Jaime vio algo similar antes?
  simultaneity_window_days int DEFAULT 30,
  classification text NULL,     -- EXACT_HIGH | STRONG_CONCEPTUAL | MODERATE | WEAK | COINCIDENCE | MARKET_WIDE | UNKNOWN
  similarity_level text NULL,   -- igual que classification sin MARKET_WIDE (se conserva cuando la clase final es MARKET_WIDE)
  independent_actor_count int NULL,                          -- actores independientes con implementación comparable
  convergence_score numeric(5,2) NULL,                       -- 0..100
  formula_version text,
  alternative_explanations text NOT NULL,
  status text,                  -- CANDIDATE | UNDER_REVIEW | VERIFIED | REJECTED | DISPUTED
  provenance text, verification text,
  created_at timestamptz 🔒, created_by uuid 🔒
)

convergence_assessments 📜 (                    -- cada evaluación por rúbrica; varias por convergencia
  id uuid PK, convergence_id uuid FK,
  assessor_kind text,           -- HUMAN | AI
  assessor_id uuid NULL, ai_model text NULL,
  blind bool,                   -- ¿se evaluó sin ver fechas ni autoría?
  dim_problem smallint, dim_user smallint, dim_behavior smallint, dim_ux smallint,
  dim_technology smallint, dim_product_concept smallint, dim_business_model smallint,
  dim_terminology smallint, dim_implementation smallint,   -- 0..4 o NULL = no aplica
  specificity smallint,         -- 1..5 especificidad de la señal interna
  genericness_flag bool,
  bias_checklist jsonb,         -- respuestas a las 7 preguntas anti-sesgo
  rationale text,
  created_at timestamptz DEFAULT now(),
  content_hash bytea, chain_hash bytea
)
```

### 3.7 Tendencias, oportunidades, experimentos

```sql
trends (
  id uuid PK, code text UNIQUE, name text, description text,
  lifecycle_stage text,   -- OBSERVATION | WEAK_SIGNAL | EMERGING | ACCELERATING | ESTABLISHED | SATURATED | DECLINING | DEAD
  domain_id uuid
)
trend_stage_history 📜 (trend_id, from_stage, to_stage, reason, evidence_id, changed_at, changed_by)
trend_members (trend_id, entity_type, entity_id)   -- señales y eventos

opportunities (
  id uuid PK, code text UNIQUE, signal_id uuid FK,
  problem text, target_customer text, evidence_summary text, market_signal text,
  competitive_landscape text, potential_product text, required_capabilities text,
  cost_estimate text, time_estimate text, risk text, validation_experiment text,
  recommendation text,     -- INVESTIGATE | VALIDATE | PARK | DISCARD  (nunca "BUILD" sin experimento validado)
  status text, created_at timestamptz 🔒, created_by uuid 🔒
)

experiments (
  id uuid PK, code text UNIQUE, hypothesis_id uuid NULL, opportunity_id uuid NULL,
  objective text, method text, cost_estimate numeric NULL, duration_days int NULL,
  success_criteria text 🔒, failure_criteria text 🔒,      -- definidos antes de ejecutar
  status text,             -- PLANNED | RUNNING | COMPLETED | ABANDONED
  started_at timestamptz NULL, ended_at timestamptz NULL,
  created_at timestamptz 🔒
)
outcomes 📜 (id, entity_type, entity_id, result text, summary text, evidence_id, created_at, created_by)
learnings (id, outcome_id NULL, entity_type, entity_id, statement text, created_at 🔒, created_by)
```

### 3.8 Proyectos de cliente (Fase 4)

```sql
projects (id, name, client_alias, client_name_encrypted NULL, confidentiality, started_at, ended_at)
client_requirements (
  id, project_id,
  explicit_requirement text 🔒,      -- lo que dijo el cliente
  ambiguity_notes text 🔒,
  inferred_need text 🔒,             -- lo que Jaime interpretó, registrado ANTES de la primera entrega
  recorded_at timestamptz 🔒
)
ux_decisions (id, requirement_id, design_response text 🔒, prototype_evidence_id, delivered_at, recorded_at 🔒)
requirement_validations 📜 (
  id, requirement_id, client_reaction text, requested_changes text,
  change_magnitude text,            -- NONE | MINOR | MODERATE | MAJOR_REDESIGN (rúbrica en METRICS.md)
  approved bool, revision_round int, evidence_id, recorded_at
)
```

### 3.9 Caso música

Se modela con las tablas genéricas para no crear un subsistema aparte:
- `signals` (dominio MUSIC, `source_type = OBSERVATION`): "Del álbum X elijo la pista Y".
- `predictions`: "La pista Y superará la mediana de reproducciones del álbum al [fecha]", con `base_rate_estimate = 1 / número de pistas` (o lo que corresponda).
- `music_selections (signal_id, album, artist, total_tracks, selected_tracks jsonb, all_tracks jsonb, listened_at, selected_at, reason)`.
- `music_popularity_snapshots 📜 (album, track, metric, value, source_id, measured_at)` para comparar todas las pistas, no solo la elegida.

### 3.10 IA, métricas, informes

```sql
ai_suggestions (
  id, target_type, target_id NULL, suggestion_type,   -- CLASSIFICATION, CANDIDATE_CONVERGENCE, COUNTERARGUMENT, SUMMARY, ENTITY
  payload jsonb, provider text, model text, prompt_sha256 bytea,
  status text,         -- PENDING | ACCEPTED | REJECTED | EDITED
  decided_by uuid NULL, decided_at timestamptz NULL, created_at timestamptz
)
embeddings (entity_type, entity_id, version int, model text, vector vector(N), created_at)   -- Fase 4

score_snapshots 📜 (id, metric text, scope jsonb, value numeric, ci_low numeric, ci_high numeric,
                    n int, formula_version text, computed_at timestamptz)
reports (id, kind, period_start, period_end, content jsonb, generated_at, generated_by, provenance)
```

### 3.11 Integridad y grafo

```sql
audit_log 📜 (
  id bigint PK,                              -- asignado por trigger: consecutivo, sin huecos
  occurred_at timestamptz,                   -- asignado por trigger (clock_timestamp)
  user_id uuid, action text,                 -- CREATE | VERSION | STATUS_CHANGE | LINK | RESOLVE | VERIFY | EXPORT | LOGIN
  entity_type text, entity_id uuid,
  old_value jsonb NULL, new_value jsonb NULL,
  reason text NULL,                          -- obligatorio en cambios de entidades históricas
  content_hash bytea, chain_hash bytea
)
integrity_anchors 📜 (id, anchored_at, chain_head_hash bytea, method text, external_reference text)
                    -- method: GITHUB_COMMIT | RFC3161_TSA ; external_reference: SHA del commit o token TSA

entity_links (                               -- aristas del grafo de conocimiento (sec. 35)
  id uuid PK, from_type text, from_id uuid, relation text, to_type text, to_id uuid,
  provenance text, verification text, created_at timestamptz 🔒, created_by uuid 🔒
)
-- relation: OBSERVED, SUPPORTS, GENERATES, COMPARED_WITH, CONVERGES_WITH, CREATES, PRODUCES,
--           MERGED_FROM, SPLIT_FROM, CONTRADICTS, PRIOR_ART_OF
```

Las relaciones principales ya existen como claves foráneas; `entity_links` cubre las relaciones flexibles y la visualización del grafo se construye uniendo ambas.

## 4. Cómo funciona la inmutabilidad de predicciones (sec. 24 y 60.7)

1. **Creación:** la API inserta `predictions` (version 1) y su foto en `prediction_versions`. `recorded_at` lo pone la BD. `locked_at = recorded_at + 15 min`.
2. **Gracia:** durante 15 minutos se permite corregir enunciado, criterio, horizonte, confianza. Cada corrección inserta una nueva fila en `prediction_versions` y en `audit_log`. El historial muestra las correcciones.
3. **Bloqueo:** pasado `locked_at`, el trigger rechaza cualquier `UPDATE` en columnas 🔒. Solo cambian `status` (OPEN → OVERDUE → RESOLVED/WITHDRAWN) mediante transiciones válidas, cada una auditada.
4. **Cambio de opinión:** se crea una predicción nueva (V2) con `supersedes_id`. V1 queda intacta y sigue en todas las métricas.
5. **Resolución:** se inserta en `prediction_resolutions` (append-only). Si se disputa, se inserta otra resolución que referencia a la anterior. Nunca se sobrescribe.
6. **Verificación:** la cadena de hashes de `prediction_versions` y `audit_log` se ancla diariamente fuera del sistema.

Trigger de ejemplo (simplificado):

```sql
CREATE FUNCTION predictions_guard() RETURNS trigger AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    RAISE EXCEPTION 'predictions: borrado prohibido';
  END IF;
  IF OLD.locked_at IS NOT NULL AND now() > OLD.locked_at AND (
       NEW.statement IS DISTINCT FROM OLD.statement OR
       NEW.resolution_criteria IS DISTINCT FROM OLD.resolution_criteria OR
       NEW.horizon_date IS DISTINCT FROM OLD.horizon_date OR
       NEW.confidence IS DISTINCT FROM OLD.confidence OR
       NEW.base_rate_estimate IS DISTINCT FROM OLD.base_rate_estimate OR
       NEW.specificity IS DISTINCT FROM OLD.specificity OR
       NEW.evidence_snapshot IS DISTINCT FROM OLD.evidence_snapshot) THEN
    RAISE EXCEPTION 'predictions: campos bloqueados desde %', OLD.locked_at;
  END IF;
  IF NEW.recorded_at IS DISTINCT FROM OLD.recorded_at OR NEW.recorded_by IS DISTINCT FROM OLD.recorded_by THEN
    RAISE EXCEPTION 'predictions: recorded_at/recorded_by inmutables';
  END IF;
  RETURN NEW;
END $$ LANGUAGE plpgsql;
```

## 5. Índices principales

- `signals(domain_id, recorded_at)`, `signals USING gin(search_vector)`, `signals USING gin(original_title gin_trgm_ops)`
- `predictions(status, horizon_date)` para alertas de vencimiento
- `convergences(signal_id)`, `convergences(external_event_id)`, `convergences(temporal_relation, status)`
- `external_events(observable_earliest)`, `audit_log(entity_type, entity_id)`

## 6. Datos semilla (sec. 41)

Se cargan desde `seed/*.json` con `recorded_at` real (la fecha de carga) y `is_retrospective = true`. Ningún caso semilla incluye resultados, fechas externas ni puntuaciones inventadas:

| Código | Título | Estado inicial | Pendiente |
|---|---|---|---|
| TR-UX-001 | Interfaz de alta densidad de información / reducción de scroll externo | Señal retrospectiva, origen E0 (declarado), evento externo TypeSafe `UNIDENTIFIED`, convergencia `CANDIDATE` con relación `UNKNOWN` | Evidencia de proyectos, URL y fecha verificada de TypeSafe, fecha de exposición |
| TR-AI-001 | Exploración de IA para trabajo, reportes y automatización en JEGASolutions | Señal retrospectiva; evidencia candidata: historial de `report-builder` (IA multi-proveedor) | Fechas verificadas de commits/pushes; eventos externos comparables; arte previo |
| TR-MUSIC-001 | Selección personal de pistas antes de su popularidad | Señal retrospectiva sin datos | Registros históricos de escucha; desde hoy, capturas prospectivas |
| TR-BIZ-001 | Inicio de JEGASolutions en un contexto de emprendimiento visible | Estudio de contexto de mercado, **no** se evalúa como predicción | Datos de contexto local; se usa como control de tasa base |
| TR-CUST-001 | Aprobación del cliente pese a requisitos ambiguos | Señal retrospectiva; se valida con historial de proyectos | Requisito inicial, entrega, feedback, revisiones por proyecto |
