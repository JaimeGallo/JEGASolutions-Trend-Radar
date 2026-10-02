# ARCHITECTURE: JEGASolutions Trend Radar

> Versión 0.1 (Fase 1, propuesta). Pendiente de aprobación.

## 1. Decisión resumida

**Monolito modular** en ASP.NET Core (.NET 10 LTS) con Clean Architecture, PostgreSQL 16, SPA en React + Vite + TypeScript, desplegado con Docker Compose. Sin colas, sin microservicios, sin base vectorial separada.

## 2. Justificación frente al ecosistema JEGASolutions

Revisé el repositorio `jegasolutions-platform` (estado al 20-09-2026). Hallazgos:

| Aspecto | Ecosistema actual | Decisión para Trend Radar |
|---|---|---|
| Backend | ASP.NET Core net8.0 (18 proyectos) y net9.0 (10 proyectos), capas API / Domain / Application / Infrastructure / Data | ASP.NET Core **.NET 10 LTS**, misma estructura de capas. .NET 8 y .NET 9 terminan soporte en noviembre de 2026; .NET 10 tiene soporte hasta noviembre de 2028 |
| ORM / BD | EF Core + Npgsql, `EFCore.NamingConventions` (snake_case), PostgreSQL 15 en Docker | EF Core 10 + Npgsql, snake_case, **PostgreSQL 16** (necesario para pgvector moderno y mejor rendimiento) |
| Auth | JWT (`JwtBearer`, `System.IdentityModel.Tokens.Jwt`), BCrypt, guía de SSO | JWT compatible con el SSO de la plataforma; ver decisión pendiente en DISCOVERY.md |
| Frontend | React 18 + Vite 5 + Tailwind 3 + Recharts + lucide-react + axios + react-router | React + Vite + Tailwind + Recharts + lucide-react, **con TypeScript** (el modelo de datos tiene muchos estados y enums; los tipos evitan errores de integridad en la UI) |
| IA | `IAIProvider` + `MultiAIService` con OpenAI, Anthropic, DeepSeek, Groq, Ollama en report-builder | Reutilizar el mismo patrón de interfaz (copiado y adaptado, no compartido como paquete por ahora) y añadir embeddings |
| Pruebas | xUnit + coverlet | xUnit + Testcontainers (PostgreSQL real para probar triggers de inmutabilidad) |
| Despliegue | Docker Compose por app + compose raíz, red `jega-network` | Docker Compose propio, compatible para integrarse luego al compose de la plataforma |

**Por qué no Next.js** (sugerido en la sec. 53 como opción): es una herramienta interna autenticada, sin necesidad de SEO ni renderizado en servidor. Vite SPA mantiene coherencia con las 7 apps existentes, reduce infraestructura (sin servidor Node en producción, solo archivos estáticos detrás de Nginx) y permite reutilizar conocimiento del equipo.

**Por qué repositorio separado y no un módulo de la plataforma:** Trend Radar no es (aún) un producto SaaS multi-tenant para clientes; es una herramienta interna con requisitos de integridad muy distintos. Mantenerlo aparte evita mezclar migraciones y permisos. Si más adelante se ofrece a clientes, la estructura de capas permite moverlo a `apps/trend-radar`.

## 3. Vista de contenedores

```
┌───────────────────────┐      HTTPS      ┌────────────────────────────────┐
│  SPA React + Vite     │ ──────────────► │  TrendRadar.API (ASP.NET Core) │
│  (Nginx, estáticos)   │   JSON / JWT    │  REST + OpenAPI                │
└───────────────────────┘                 │                                │
                                          │  Application (casos de uso)    │
                                          │  Domain (reglas, invariantes)  │
                                          │  Infrastructure:               │
                                          │   ├ EF Core / Npgsql           │
                                          │   ├ Almacenamiento de archivos │
                                          │   ├ IAIProvider (Fase 4)       │
                                          │   ├ ISourceProvider (Fase 5)   │
                                          │   └ IIntegrityAnchor           │
                                          └──────────────┬─────────────────┘
                                                         │
                         ┌───────────────────────────────┼─────────────────────┐
                         ▼                               ▼                     ▼
               ┌──────────────────┐          ┌────────────────────┐   ┌─────────────────┐
               │ PostgreSQL 16    │          │ Archivos evidencia │   │ Anclaje externo │
               │ + pg_trgm        │          │ (volumen local o   │   │ (repo git de    │
               │ + unaccent       │          │  S3 compatible)    │   │  digests / TSA) │
               │ + pgvector (F4)  │          │ hash SHA-256       │   └─────────────────┘
               └──────────────────┘          └────────────────────┘
```

Tareas programadas (alertas de vencimiento, anclaje diario, informes) se ejecutan como `BackgroundService` dentro de la API. No se introduce Hangfire ni colas hasta que haga falta.

## 4. Estructura del repositorio propuesta

```
JEGASolutions-Trend-Radar/
├── docs/                         # Estos documentos
├── backend/
│   ├── TrendRadar.sln
│   ├── src/
│   │   ├── TrendRadar.Domain/          # Entidades, value objects, reglas (cronología, rúbricas, fórmulas)
│   │   ├── TrendRadar.Application/     # Casos de uso, DTOs, validación, puertos (interfaces)
│   │   ├── TrendRadar.Infrastructure/  # EF Core, migraciones SQL, archivos, IA, anclaje
│   │   └── TrendRadar.API/             # Controllers, auth, OpenAPI, BackgroundServices
│   └── tests/
│       ├── TrendRadar.Domain.Tests/        # Reglas puras: cronología, TTC, JAI, convergencia
│       └── TrendRadar.Integration.Tests/   # Testcontainers: inmutabilidad en BD, auditoría
├── frontend/
│   └── src/
│       ├── app/            # Rutas, layout persistente, paleta de comandos
│       ├── features/       # signals, predictions, events, convergences, dashboard, challenge
│       ├── components/     # UI compartida (paneles, timeline, badges epistémicos)
│       └── lib/            # cliente API tipado (generado desde OpenAPI), atajos
├── seed/                   # Casos semilla en JSON versionado
├── docker-compose.yml
└── README.md
```

## 5. Módulos de dominio

| Módulo | Responsabilidad | Fase |
|---|---|---|
| Signals | Captura, versiones, origen, evidencia de origen | MVP |
| Foresight | Hipótesis, predicciones, versiones, resolución | MVP |
| ExternalRadar | Eventos, fuentes, actores, exposiciones | MVP |
| Convergence | Relación temporal, rúbrica, clasificación, verificación | MVP |
| Opportunities | Oportunidades, experimentos, resultados, aprendizajes | MVP básico |
| Metrics | Scores, TTC, JAI, calibración, snapshots | MVP (cálculo) |
| Integrity | Auditoría, cadena de hashes, anclaje externo, exportación | MVP |
| Projects | Proyectos de cliente, requisitos, decisiones UX | Fase 4 |
| Intelligence | Embeddings, candidatas, clasificación, contraargumentos | Fase 4 |
| Ingestion | Monitoreo de fuentes, alertas, informes programados | Fase 5 |

Reglas de dependencia: Domain no depende de nada; Application depende de Domain; Infrastructure implementa puertos de Application; API compone todo. Las fórmulas (cronología, TTC, convergencia, JAI) viven en Domain como funciones puras con pruebas unitarias exhaustivas.

## 6. Inmutabilidad: cómo se garantiza técnicamente

La inmutabilidad se aplica en **cuatro capas**, porque una garantía solo en código no resiste una corrección "rápida" en la base de datos.

### Capa 1: Dominio
Las entidades exponen los campos originales como solo lectura. No existen métodos `Update` para `original_text`, `recorded_at`, `confidence_at_creation`, ni para los campos de una predicción bloqueada. Un cambio de idea es `signal.Revise(newText, reason)` que produce una `SignalVersion` nueva.

### Capa 2: Base de datos
- Tablas append-only (`signal_versions`, `prediction_versions`, `audit_log`, `integrity_chain`): el rol de la aplicación tiene `INSERT` y `SELECT`, **sin** `UPDATE` ni `DELETE`.
- Tablas con columnas mixtas (`signals`, `predictions`): trigger `BEFORE UPDATE` que rechaza cambios en columnas inmutables y trigger `BEFORE DELETE` que rechaza todo borrado.
- `recorded_at` con `DEFAULT now()` y trigger que ignora cualquier valor enviado por el cliente.
- Las migraciones que tocan estos triggers requieren revisión explícita (convención documentada).

### Capa 3: Cadena de hashes
Cada registro de `signal_versions`, `prediction_versions` y `audit_log` guarda:
```
content_hash = SHA-256(json canónico del contenido)
chain_hash   = SHA-256(previous_chain_hash || content_hash || recorded_at)
```
Alterar un registro histórico rompe la cadena desde ese punto. Un endpoint `/integrity/verify` recalcula la cadena; la UI muestra el estado.

### Capa 4: Anclaje externo
Un `BackgroundService` diario publica el último `chain_hash` (solo el hash, ningún contenido) fuera del sistema mediante `IIntegrityAnchor`:
- **Opción A (recomendada, costo cero):** commit en un repositorio GitHub privado `trend-radar-anchors`. GitHub registra la fecha de push, que el sistema no controla.
- **Opción B:** sello de tiempo RFC 3161 de una TSA pública (por ejemplo FreeTSA), que entrega prueba criptográfica de existencia en una fecha.

Con el anclaje, Jaime puede demostrar a un tercero que una señal existía en una fecha sin revelar su contenido, y que no fue alterada después.

### Período de gracia
Una predicción puede corregirse durante 15 minutos (errores de digitación). Cada corrección queda en auditoría. Pasado el plazo se bloquea a nivel de BD (`locked_at` no nulo activa el trigger).

## 7. API

REST con OpenAPI (Swashbuckle, como en la plataforma). Cliente TypeScript generado desde el esquema. Recursos principales:

```
POST   /api/signals                    GET /api/signals?domain=&stage=&q=&from=&to=
GET    /api/signals/{code}             POST /api/signals/{code}/versions
POST   /api/signals/{code}/origin-evidence
POST   /api/hypotheses                 POST /api/predictions
POST   /api/predictions/{id}/versions  POST /api/predictions/{id}/resolution
POST   /api/external-events            POST /api/external-events/{id}/exposures
POST   /api/sources
POST   /api/convergences               PUT  /api/convergences/{id}/assessment
POST   /api/convergences/{id}/verify
GET    /api/signals/{code}/verification     # Historical Verification / "¿Lo vi primero?"
GET    /api/metrics/dashboard          GET /api/metrics/domains
GET    /api/integrity/verify           GET /api/export
GET    /api/audit?entity=&id=
```

No hay endpoints `DELETE` para entidades históricas.

## 8. Búsqueda

- Fase MVP: PostgreSQL full text con `tsvector` generado (configuración `simple` + `unaccent`, apta para español e inglés mezclados) + `pg_trgm` para búsqueda difusa por título y código.
- Fase 4: `pgvector` en la misma base para embeddings. Sin servicio vectorial externo.

## 9. Abstracción de IA (Fase 4)

Se adapta el patrón `IAIProvider` de report-builder y se agregan capacidades específicas:

```csharp
public interface IAIProvider {
    string ProviderName { get; }
    bool IsAvailable { get; }
    bool IsLocal { get; }                       // Ollama: apto para datos confidenciales
    Task<AIResponse> GenerateAsync(AIRequest request, CancellationToken ct);
}
public interface IEmbeddingProvider {
    string ModelId { get; }
    int Dimensions { get; }
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
}
```

Reglas:
- Toda salida de IA se guarda en `ai_suggestions` con proveedor, modelo, prompt (hash), fecha y estado (PENDING, ACCEPTED, REJECTED, EDITED). Nunca escribe directamente en entidades históricas.
- Las URLs propuestas por IA se verifican con una petición real antes de mostrarse; si no resuelven, se descartan.
- Datos CLIENT_CONFIDENTIAL solo van a proveedores con `IsLocal = true`, salvo configuración explícita.

## 10. Proveedores de datos externos (Fase 5)

`ISourceProvider` con implementaciones por tipo (RSS, GitHub releases, Hacker News, arXiv, Wayback Machine CDX para fechas históricas). Cada ítem ingerido conserva URL, fecha de publicación, fecha de consulta, hash del contenido y proveedor. La ingesta crea **eventos candidatos**, no eventos confirmados.

## 11. Seguridad

- JWT de corta duración + refresh token; contraseñas con BCrypt (o SSO de la plataforma).
- Roles OWNER / REVIEWER / VIEWER con políticas en ASP.NET.
- Archivos de evidencia: verificación de tipo, tamaño máximo, hash SHA-256 guardado al subir; descarga solo autenticada.
- Secretos por variables de entorno (`.env` fuera de git), igual que la plataforma.
- CORS restringido al dominio del frontend.

## 12. Observabilidad y respaldo

- Logs estructurados (Serilog o el logging de la plataforma), sin contenido de señales en logs.
- `pg_dump` diario cifrado; prueba de restauración mensual documentada.
- La exportación JSON completa (`/api/export`) sirve también como respaldo legible.

## 13. Lo que deliberadamente NO se incluye

Kubernetes, microservicios, colas de mensajes, Elasticsearch, base vectorial dedicada, GraphQL, base de datos de grafos (el grafo se modela con `entity_links` en PostgreSQL y se visualiza en el frontend), Redis. Se reevaluará solo si un requisito medido lo exige.
