# JEGASolutions Trend Radar

Plataforma de detección de señales, análisis de convergencia y prospectiva estratégica de JEGASolutions.

Pregunta central: **¿Puede JEGASolutions convertir la intuición en evidencia medible de detección temprana de señales?**

## Estado

- **Fase 0** (en curso): registro inmutable de señales en [`signals/`](signals/README.md), sellado con OpenTimestamps.
- **Fase 1** (Discovery): documentos de diseño en `docs/`.
- **Fase 2** (esqueleto): backend .NET 10, frontend React en español, autenticación propia y protecciones de integridad en PostgreSQL.
- **Fase 3, M1** (señales): captura rápida (tecla `N`), registro original inmutable, versiones encadenadas, origen declarado y respaldado, evidencia con nivel E0 a E4, búsqueda e importación de la Fase 0. Predicciones, eventos y convergencias llegan en M2 a M4.
- **Fase 3, M2** (predicciones): hipótesis por señal; predicciones con criterio de resolución, fecha límite, confianza, tasa base y especificidad; 15 minutos de gracia y luego bloqueo en la base de datos; versiones que no reemplazan a la original; resolución con disputa; retiro con motivo; alertas de vencidas.

## Ejecutar en local

Requisitos: Docker Desktop.

```bash
cp .env.example .env      # edita las claves, JWT_SECRET y el propietario inicial
docker compose up -d --build
```

- Aplicación: http://localhost:5180 (inicia sesión con `OWNER_EMAIL` / `OWNER_PASSWORD`)
- API: http://localhost:8080/api/health
- PostgreSQL: `localhost:5433`

El propietario inicial se crea solo la primera vez, cuando la base de datos no tiene usuarios.

### Importar las señales de la Fase 0

Una sola vez, antes de registrar señales nuevas en la app (así los códigos no chocan):

```bash
TR_EMAIL=tu-correo TR_PASSWORD=tu-clave python3 scripts/importar_senales.py --dry-run   # revisar
TR_EMAIL=tu-correo TR_PASSWORD=tu-clave python3 scripts/importar_senales.py
```

Es idempotente: si se ejecuta otra vez, omite las señales ya importadas.

### Desarrollo sin Docker para la app

```bash
docker compose up -d db                       # solo la base de datos
cd backend && dotnet run --project src/TrendRadar.API --launch-profile http   # http://localhost:8080
cd frontend && npm install && npm run dev     # http://localhost:5173 (reenvía /api a la API)
```

En modo Development la API usa `appsettings.Development.json`: puerto 5433 y las claves `trendradar_owner_dev` / `trendradar_app_dev`. Para que coincidan, usa esas mismas claves en `DB_OWNER_PASSWORD` y `DB_APP_PASSWORD` de tu `.env` local, o sobrescribe `ConnectionStrings__TrendRadar` y `ConnectionStrings__TrendRadarMigrations`. Para el propietario inicial define `Bootstrap__OwnerEmail` y `Bootstrap__OwnerPassword` como variables de entorno.

### Pruebas

```bash
cd backend && dotnet test        # unitarias + integración (requiere la base de datos de docker compose)
cd frontend && npm run lint && npm run build
```

Las pruebas de integración crean y borran una base de datos temporal en el PostgreSQL de docker compose (usuario `trendradar_owner`, clave `trendradar_owner_dev`). Para usar otro servidor o clave: `TRENDRADAR_TEST_ADMIN_CONNECTION`.

## Estructura

| Carpeta | Contenido |
|---|---|
| `backend/` | ASP.NET Core .NET 10: Domain, Application, Infrastructure, API y pruebas |
| `frontend/` | React + Vite + TypeScript + Tailwind, interfaz en español |
| `docker/` | Inicialización de PostgreSQL (rol de aplicación) |
| `signals/` | Registro de señales de la Fase 0 |
| `docs/` | Documentos de diseño |

## Documentos

| Documento | Contenido |
|---|---|
| [docs/DISCOVERY.md](docs/DISCOVERY.md) | Análisis de la especificación, ambigüedades, riesgos, decisiones pendientes |
| [docs/PRODUCT_SPEC.md](docs/PRODUCT_SPEC.md) | Requisitos, flujos, alcance del MVP, diseño de interfaz |
| [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) | Stack, módulos, inmutabilidad técnica |
| [docs/DATA_MODEL.md](docs/DATA_MODEL.md) | Esquema PostgreSQL, versionado, auditoría |
| [docs/RESEARCH_METHODOLOGY.md](docs/RESEARCH_METHODOLOGY.md) | Cronología, niveles de evidencia, anti-sesgo, caso TypeSafe |
| [docs/CONVERGENCE_MODEL.md](docs/CONVERGENCE_MODEL.md) | Rúbrica y clasificación de convergencias |
| [docs/METRICS.md](docs/METRICS.md) | JEGASignal Score, JAI, Time-to-Convergence, calibración |
| [docs/ROADMAP.md](docs/ROADMAP.md) | Fases e hitos |
