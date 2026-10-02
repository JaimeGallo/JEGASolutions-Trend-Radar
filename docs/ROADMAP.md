# ROADMAP: JEGASolutions Trend Radar

> Versión 0.1 (Fase 1). Las estimaciones son orientativas y suponen un desarrollador con apoyo de Claude Code.

## Principio de secuencia

Primero que la **metodología funcione a mano**, luego automatizar. El activo que no se puede recuperar es el tiempo: una señal no registrada hoy no podrá demostrar antelación mañana. Por eso la captura va primero, incluso antes del MVP completo.

## Fase 0: Captura inmediata (en curso desde 2026-10-02)

Mientras se construye el MVP (instrucciones en `signals/README.md`):
- [x] Carpeta `signals/` con plantilla Markdown por señal y script `scripts/nueva-senal.sh`.
- [x] Validador `scripts/validar_senales.py` y workflow `Señales`: falla si se modifica o borra una señal existente; los cambios de opinión van como revisiones `TR-XX-NNN.vN.md`.
- [x] Sello OpenTimestamps (`.ots`, evidencia E4) generado por GitHub Actions en cada push; anclaje en Bitcoin completado por el workflow semanal `Completar sellos` (requiere que esté en la rama por defecto).
- [x] Casos semilla de la sec. 41 registrados con el texto literal de la especificación.
- [ ] Jaime registra al menos 10 señales reales prospectivas.
- [x] Importación a la app (M1): `scripts/importar_senales.py` conserva el código, adjunta el `.md` (mismo SHA-256 que el `.ots`) con la fecha del commit (E2) y el `.ots` (E0 hasta verificarlo); `recorded_at` = fecha de importación.
- Tras importar, las señales nuevas se registran solo en la app (crear archivos nuevos en `signals/` haría chocar los códigos). El anclaje externo de la app, equivalente a los `.ots`, llega en M6; conviene adelantarlo.

Criterio de salida: al menos 10 señales reales registradas antes de que exista la app.

## Fase 1: Discovery (este entregable)

- [x] DISCOVERY.md, PRODUCT_SPEC.md, ARCHITECTURE.md, DATA_MODEL.md, RESEARCH_METHODOLOGY.md, CONVERGENCE_MODEL.md, METRICS.md, ROADMAP.md
- [ ] Revisión y respuestas de Jaime a las decisiones pendientes (DISCOVERY §7)
- [ ] Ajustes a los documentos

Criterio de salida: documentos aprobados.

## Fase 2: Esqueleto de arquitectura (implementada el 2026-10-02)

- Solución .NET 10 con capas Domain / Application / Infrastructure / API y proyectos de pruebas.
- Frontend Vite + React + TypeScript + Tailwind con layout persistente (nav lateral, maestro-detalle, paleta de comandos vacía).
- Docker Compose: PostgreSQL 16, API, frontend.
- Autenticación propia (JWT + BCrypt) y roles OWNER / REVIEWER / VIEWER.
- Despliegue local con Docker Compose.
- Migración inicial: `users`, `domains`, `audit_log`, `integrity_chain` + triggers append-only.
- CI en GitHub Actions: build, pruebas, lint.
- `.gitignore` ajustado a .NET + Node.
- UI en español desde el inicio (textos centralizados para no mezclar idiomas).

Criterio de salida: `docker compose up` levanta todo; prueba de integración demuestra que `UPDATE` y `DELETE` sobre `audit_log` fallan a nivel de BD.

Estado:
- [x] Pruebas de integración: el rol de aplicación y el propietario no pueden hacer `UPDATE`, `DELETE` ni `TRUNCATE` sobre `audit_log`; la verificación detecta alteraciones hechas desactivando triggers; 40 inserciones concurrentes mantienen la cadena válida.
- [x] Flujo completo verificado en navegador (login, sesión tras recarga, atajos, paleta, integridad, móvil sin scroll horizontal).
- [ ] `docker compose up` verificado en una máquina con Docker (el entorno donde se construyó no tenía el daemon de Docker; el workflow de CI construye las imágenes).
- Fuera de esta fase: anclaje externo diario (M6) y cliente TypeScript generado desde OpenAPI (M1).

## Fase 3: MVP (5 a 7 semanas)

| Hito | Contenido | Criterio de salida |
|---|---|---|
| M1 Señales (1,5 sem) | Captura rápida, `recorded_at` de servidor, versiones, origen declarado, evidencia (archivos, URL, commits), códigos TR-XX-NNN, lista + detalle | Señal capturable en < 30 s; pruebas de inmutabilidad pasan; editar crea versión |
| M2 Predicciones (1 sem) | Hipótesis, predicciones con criterio/horizonte/confianza/tasa base/especificidad, gracia de 15 min, bloqueo, V2, resolución, vencimientos | Trigger rechaza cambios tras bloqueo; V1 y V2 coexisten |
| M3 Radar externo (1 sem) | Eventos, fuentes con tipo y fechas, actores, exposiciones, arte previo | Evento con fuente primaria y fecha intervalo registrado |
| M4 Convergencias (1,5 sem) | Relación temporal automática, TTC y Anticipation Window, rúbrica, modo ciego, lista anti-sesgo, clasificación, estados | Pruebas unitarias de cronología (todos los casos de §5 de RESEARCH_METHODOLOGY) y de la fórmula `convergence-v1` |
| M5 Dashboard y modos (1 sem) | KPIs, paneles, línea de tiempo doble, búsqueda/filtros, paleta de comandos y atajos, Challenge Jaime (formulario guiado), "¿Lo vi primero?" (informe) | Pantalla principal sin scroll de página a 1440×900 |
| | **M1 implementado (2026-10-02).** Además: importación de la Fase 0 (`scripts/importar_senales.py`) conservando códigos, para que no choquen con la numeración de la app. Pendiente movido: cliente TypeScript generado desde OpenAPI (los tipos se mantienen a mano en `frontend/src/lib/signals.ts`). | Captura verificada en navegador; 51 pruebas unitarias y 48 de integración (inmutabilidad, códigos concurrentes, cadenas, API) |
| M6 Integridad y semilla (0,5 sem) | Cadena de hashes, `/integrity/verify`, anclaje diario en GitHub, exportación, carga de 5 casos semilla sin datos inventados | Verificación de cadena en verde; alterar un registro a mano en BD se detecta |

Fuera del MVP: IA, crawling, informes automáticos, radar de burbujas, grafo, proyectos de cliente.

## Fase 4: Inteligencia (4 a 6 semanas, tras 1 a 2 meses de uso real)

- Adaptación de `IAIProvider` (multi-proveedor, Ollama para datos confidenciales) + `IEmbeddingProvider` + pgvector.
- Clasificación sugerida de señales (dominio, etiquetas, especificidad).
- Convergencias candidatas por embeddings + evaluación por LLM en modo ciego (AI GENERATED).
- Challenge Jaime con contraargumentos y tasa base sugeridos por IA.
- Asistencia de investigación histórica: búsqueda de fechas en Wayback Machine (API CDX), GitHub, fuentes primarias, con URLs verificadas.
- Calibración y JAI visibles al alcanzar N mínimo; radar de burbujas; gráfico fecha señal vs. fecha convergencia; grafo de relaciones.
- Proyectos de cliente, requisitos, decisiones UX y Requirement Anticipation Rate.
- Caso música prospectivo con snapshots de popularidad manuales.
- Informes semanal, mensual y trimestral bajo demanda.
- Revisión de fórmulas v1 con datos reales.

Criterio de salida: la IA nunca escribe en tablas históricas (prueba automatizada); concordancia IA-humano medida.

## Fase 5: Automatización (continuo)

- `ISourceProvider`: RSS, GitHub releases, Hacker News, arXiv, sitios de competidores.
- Monitoreo programado que crea eventos **candidatos** con procedencia completa.
- Alertas: predicciones por vencer, candidatas nuevas, cambios de etapa de tendencias.
- Informes programados (Trend Brief semanal, Foresight Review mensual).
- Detección de aceleración de tendencias.

## Hitos de investigación (paralelos al desarrollo)

| Cuándo | Actividad |
|---|---|
| Fase 0 en adelante | Registrar señales prospectivas de forma continua, incluidas las que parecen triviales |
| Tras M4 | Investigar TR-UX-001 (TypeSafe) con el protocolo de RESEARCH_METHODOLOGY §8 |
| Tras M4 | Investigar TR-AI-001 con historial git de `jegasolutions-platform` |
| Mes 3 | Primer informe trimestral, aunque N sea bajo, para validar el formato |
| Mes 6 | Revisión honesta: ¿está el sistema respondiendo la pregunta central? ¿Qué sobra? |
| Mes 12 | Primer JAI con N razonable (si hay 10+ resueltas) y desempeño por dominio |

## Fuera de alcance (por ahora)

Multi-tenant, comercialización del método "JEGASignal Method" (sec. 50: no se comercializa sin evidencia empírica suficiente), app móvil nativa (la SPA será usable en móvil para captura rápida), integraciones con redes sociales que requieran APIs de pago.
