# DISCOVERY: Análisis de la especificación v2.0

> Fase 1, documento de entrada. Resume requisitos, contradicciones, ambigüedades y riesgos detectados en la especificación "JEGASolutions Trend Radar v2.0" y enlaza las decisiones propuestas en el resto de documentos.
>
> Estado: **BORRADOR PARA REVISIÓN**. No se implementa nada hasta que Jaime confirme.

## 1. Pregunta central

> "¿Puede JEGASolutions convertir la intuición en evidencia medible de detección temprana de señales?"

Todo el diseño se subordina a esta pregunta. La consecuencia práctica más importante: **el valor del sistema crece con el tiempo y depende de registrar señales con fecha verificable lo antes posible**. Una señal registrada hoy solo podrá evaluarse dentro de meses o años. Por eso el MVP prioriza la captura inmutable sobre la visualización.

## 2. Documentos producidos

| Documento | Contenido |
|---|---|
| [PRODUCT_SPEC.md](PRODUCT_SPEC.md) | Requisitos funcionales y no funcionales, flujos, modos, alcance del MVP |
| [ARCHITECTURE.md](ARCHITECTURE.md) | Stack, justificación frente al ecosistema JEGASolutions, módulos, inmutabilidad técnica |
| [DATA_MODEL.md](DATA_MODEL.md) | Esquema relacional, reglas de inmutabilidad, versionado, auditoría |
| [RESEARCH_METHODOLOGY.md](RESEARCH_METHODOLOGY.md) | Cronología, niveles de evidencia, verificación histórica, motor anti-sesgo, caso TypeSafe |
| [CONVERGENCE_MODEL.md](CONVERGENCE_MODEL.md) | Cómo se mide y clasifica la convergencia |
| [METRICS.md](METRICS.md) | JEGASignal Score, JAI, Time-to-Convergence, calibración, falsos positivos |
| [ROADMAP.md](ROADMAP.md) | Fases, hitos del MVP, criterios de salida |

## 3. Requisitos identificados (resumen)

**Funcionales núcleo (MVP):** captura de señales con fecha inmutable; versionado; hipótesis; predicciones con criterios de resolución pre-registrados; eventos externos manuales con fuente; evidencia; convergencias con puntuación por rúbrica; Time-to-Convergence; dashboard básico; modo Challenge Jaime; auditoría; búsqueda y filtros.

**Funcionales posteriores:** similitud semántica, convergencias candidatas generadas por IA, clustering, calibración, informes semanal, mensual y trimestral, monitoreo de fuentes, grafo de conocimiento.

**No funcionales críticos:** integridad histórica verificable (no solo "prometida"); trazabilidad de fuentes; separación observación / interpretación / resultado medido; marcado AI GENERATED vs HUMAN VERIFIED; interfaz densa sin scroll innecesario; despliegue Docker; costo de infraestructura bajo.

**Entidades:** ver [DATA_MODEL.md](DATA_MODEL.md). Se agregan a la lista de la sección 42: `trends`, `signal_origin_claims`, `exposures`, `entity_links`, `ai_suggestions`, `score_snapshots`, `verification_runs`, `integrity_anchors`.

## 4. Contradicciones y ambigüedades

Cada punto incluye la resolución propuesta. Los marcados con **[DECIDIR]** necesitan confirmación de Jaime.

### A1. `created_at` vs. señales históricas (la más importante)
La especificación exige preservar la "fecha exacta de creación", pero el caso TypeSafe y casi todos los casos semilla son **retroactivos**: se registran hoy sobre algo que ocurrió antes. Si `created_at` es la fecha de registro, ningún caso semilla podría ser BEFORE. Si `created_at` es la fecha que Jaime recuerda, el sistema acepta fechas no verificables y pierde integridad.

**Resolución:** separar tres conceptos.
- `recorded_at`: fecha en que el sistema registró la señal. La fija el servidor. Inmutable.
- `claimed_origin_at`: fecha en que Jaime afirma que la idea existía. Declarada, con precisión (día, mes, año, aproximada).
- `verified_origin_at`: fecha más temprana respaldada por evidencia con sello de tiempo de terceros. Puede estar vacía.

La cronología (BEFORE / AFTER / SIMULTANEOUS / UNKNOWN) usa `verified_origin_at`. Si solo existe `claimed_origin_at`, el resultado máximo es UNKNOWN. Las señales con `recorded_at` anterior al evento externo son evidencia de la máxima calidad ("pre-registradas"). Detalle en [RESEARCH_METHODOLOGY.md](RESEARCH_METHODOLOGY.md).

### A2. `created_before_external_event` y `retrospective_status` en la entidad Signal
Una señal puede compararse con muchos eventos externos, así que "creada antes del evento" no es un atributo de la señal sino de cada par señal-evento. **Resolución:** la relación temporal vive en `convergences.temporal_relation`. En la señal queda solo `is_retrospective` (true si `recorded_at` es posterior a `claimed_origin_at` por más de 7 días).

### A3. Tipos de señal que en realidad son entidades
La sección 9 lista PREDICTION, CONVERGENCE, EXPERIMENT, TREND como "tipos de señal", pero la sección 42 los define como tablas propias. **Resolución:** `signals.stage` solo admite INTUITION, SIGNAL, OPPORTUNITY. Hipótesis, predicciones, convergencias, experimentos y tendencias son entidades enlazadas a la señal. La UI puede seguir mostrando la "etapa más avanzada" alcanzada.

### A4. Tres máquinas de estado superpuestas
Tipos de señal (sec. 9), `status` (sec. 8) y ciclo de vida de tendencia (sec. 29) se mezclan. **Resolución:** el ciclo de vida (OBSERVATION a DEAD) pertenece a la entidad `trends` (agrupación de señales y eventos), no a cada señal. `signals.status` se limita a ACTIVE, DORMANT, ARCHIVED (nunca DELETED).

### A5. Inmutabilidad vs. "editar, fusionar, dividir" (sec. 45)
**Resolución:** editar crea una nueva versión; fusionar crea una señal nueva con enlaces `MERGED_FROM` a las originales, que se conservan; dividir crea señales hijas con `SPLIT_FROM`. Nada se borra. Las métricas usan la señal original para la cronología.

### A6. Ventana SIMULTANEOUS no definida
**Resolución:** por defecto ±30 días entre fechas verificadas, configurable por dominio y registrada en cada cálculo (`simultaneity_window_days`). Si los intervalos de incertidumbre de las fechas se solapan, el resultado es SIMULTANEOUS o UNKNOWN, nunca BEFORE.

### A7. Precisión de fechas
Muchos eventos externos solo se conocen al mes o al año ("se lanzó a inicios de 2027"). **Resolución:** toda fecha relevante se guarda como intervalo `[earliest, latest]` más `precision`. Time-to-Convergence se reporta como rango (mínimo, máximo) y el valor conservador es el mínimo.

### A8. Taxonomía de dominios inconsistente
La sec. 10 pone "AI" como subdominio de Technology y "Market" mezcla geografía (Medellín) con segmento (B2B, SMEs). La sec. 33 incluye "product design" y "SaaS" como dominios de primer nivel. **Resolución:** dominio jerárquico (domain > subdomain) editable; geografía va en `geographic_scope`; segmento de mercado va como etiqueta. Los reportes por dominio pueden agrupar a cualquier nivel. **[DECIDIR]** taxonomía inicial definitiva; propuesta en [DATA_MODEL.md](DATA_MODEL.md).

### A9. Fórmula del JAI no especificada
La especificación prohíbe éxitos/total pero no define pesos. **Resolución:** JAI basado en puntuación de Brier relativa a una tasa base declarada **al momento de crear la predicción**, ponderada por especificidad y antelación, con intervalo de confianza y mínimo de 10 predicciones resueltas para mostrarse. Ver [METRICS.md](METRICS.md).

### A10. "False Positive Rate" mal nombrado
En pronóstico, "predije X y no pasó" es la tasa de falsos descubrimientos (1 - precisión), no la tasa de falsos positivos clásica (que requiere conocer los negativos). **Resolución:** el dashboard muestra "Tasa de predicciones fallidas" (lo que la sección 21 quiere decir) y documenta la terminología. Los falsos negativos se registran como "tendencias no detectadas" en un registro explícito.

### A11. Autoevaluación
Jaime registra las señales y también puntuaría las convergencias que lo favorecen. Es el mayor riesgo de sesgo de confirmación del sistema. **Resolución:** (1) rúbricas con criterios escritos; (2) modo de **puntuación ciega**: el evaluador ve la descripción interna y la externa sin fechas ni autoría; (3) opción de segundo evaluador; (4) la IA propone una puntuación independiente marcada AI GENERATED. **[DECIDIR]** ¿habrá un segundo evaluador humano (socio, colega)?

### A12. La propia app como experimento de su hipótesis UX
La sec. 31 y 57 piden que la interfaz encarne la hipótesis "información accesible sin scroll innecesario". Si después se usa la satisfacción con la app como evidencia a favor de esa hipótesis, sería circular. **Resolución:** la app aplica el principio por diseño, pero no se usa como evidencia para TR-UX-001.

### A13. Requirement Anticipation Rate sin definición operativa
"Satisface sustancialmente" y "rediseño mayor" no están definidos. **Resolución:** rúbrica de 4 niveles basada en el porcentaje de la primera entrega que sobrevive a la aprobación y en el tipo de cambios solicitados. Ver [METRICS.md](METRICS.md). Requiere registrar el requisito **antes** de la primera entrega para que cuente.

### A14. Caso música: datos de línea base
Para comparar contra tasa base hay que saber cuántas pistas tenía el álbum, cuáles eligió Jaime y la popularidad posterior de todas. La API de Spotify restringió endpoints de popularidad y recomendaciones para apps nuevas. **Resolución:** en el MVP el caso música se registra manualmente (pistas elegidas + fecha + álbum completo); la popularidad se ingresa a mano desde fuentes públicas citadas. Automatizar queda fuera de alcance hasta verificar acceso a datos.

### A15. Entidades sin definir
`products`, `competitors`, `metrics`, `reviews` aparecen en la sec. 42 sin atributos. **Resolución:** `products` y `competitors` se modelan como `actors` (organizaciones) y `external_events` de tipo PRODUCT_LAUNCH; `metrics` como `score_snapshots`; `reviews` como `reports`. Se evita duplicar conceptos.

### A16. Usuario único vs. multiusuario (resuelto: autenticación propia)
Todo está centrado en Jaime, pero hay `created_by`, auditoría y posible segundo evaluador. **Resolución:** modelo multiusuario simple (roles OWNER, REVIEWER, VIEWER) sin multi-tenant. **[DECIDIR]** ¿autenticación propia o SSO de la plataforma JEGASolutions?

### A17. Idioma (resuelto: UI en español)
La especificación está en inglés; Jaime trabaja en español; los clientes son de Colombia. **Resolución propuesta:** UI en español, códigos y enums en inglés (`TR-UX-001`, `BEFORE`), búsqueda de texto completo con configuración `simple` + `unaccent` para soportar ambos idiomas. **[DECIDIR]**

### A18. Confidencialidad de clientes
Los casos frontend/cliente contienen requisitos y reacciones de clientes, que pueden ser confidenciales, y la IA los enviaría a proveedores externos. **Resolución:** campo `confidentiality` (PUBLIC, INTERNAL, CLIENT_CONFIDENTIAL); lo CLIENT_CONFIDENTIAL no se envía a proveedores de IA externos salvo configuración explícita (o solo a Ollama local). Clientes pueden seudonimizarse.

### A19. Informes automáticos en el MVP
Las secciones 36 a 38 describen informes periódicos, pero la sec. 54 los deja fuera del MVP. **Resolución:** el MVP permite generar el informe bajo demanda a partir de consultas; la programación automática llega en la Fase 5.

### A20. "TypeSafe" no está identificado
No hay URL ni producto concreto. Existen varios productos y bibliotecas con nombres similares. **Resolución:** el caso se registra con el evento externo en estado `UNIDENTIFIED` hasta que Jaime aporte la URL exacta. No se investiga ni se asume nada antes. **[DECIDIR]** URL de TypeSafe y fecha aproximada en que Jaime la vio por primera vez.

## 5. Riesgos

| # | Riesgo | Impacto | Mitigación |
|---|---|---|---|
| R1 | Pocas predicciones resueltas durante meses: métricas sin significado | Alto | Mostrar N y rango; no mostrar JAI con N < 10; priorizar predicciones con horizonte corto (3 a 6 meses) además de las largas |
| R2 | Retroactividad: casos semilla con fechas no verificables | Alto | Niveles de evidencia E0 a E4; UNKNOWN por defecto |
| R3 | Autoevaluación sesgada | Alto | Puntuación ciega, rúbricas, segundo evaluador, IA independiente |
| R4 | Sesgo de selección: solo se registran ideas que "salieron bien" | Alto | Captura rápida (menos de 30 s) para registrar todo; métricas con denominador total; registro de tendencias no detectadas |
| R5 | Inmutabilidad solo a nivel de aplicación (un admin de BD puede alterar) | Medio | Triggers, permisos de BD, cadena de hashes, anclaje externo periódico (commit firmado en GitHub o sello RFC 3161) |
| R6 | Alcance excesivo: 60 secciones de especificación | Alto | MVP estricto (sec. 54), sin crawling ni IA al inicio |
| R7 | IA alucina fuentes o fechas | Alto | La IA nunca escribe hechos; solo propone `ai_suggestions` que un humano acepta; URLs verificadas por fetch real |
| R8 | Fin de soporte de .NET 8 (noviembre 2026) | Medio | Iniciar en .NET 10 LTS |
| R9 | Datos de clientes enviados a LLM externos | Medio | Clasificación de confidencialidad, ver A18 |

## 6. Respuestas directas a la sección 60

| Pregunta | Dónde |
|---|---|
| 1-2. Análisis, contradicciones, ambigüedades | Este documento, secciones 3 y 4 |
| 3. Arquitectura | [ARCHITECTURE.md](ARCHITECTURE.md) |
| 4. Esquema de base de datos | [DATA_MODEL.md](DATA_MODEL.md) |
| 5. Alcance del MVP | [PRODUCT_SPEC.md](PRODUCT_SPEC.md) sección 6 y [ROADMAP.md](ROADMAP.md) |
| 6. Stack según el ecosistema | [ARCHITECTURE.md](ARCHITECTURE.md) sección 2 |
| 7. Predicciones inmutables | [DATA_MODEL.md](DATA_MODEL.md) sección 4 y [ARCHITECTURE.md](ARCHITECTURE.md) sección 6 |
| 8. Medición de convergencia | [CONVERGENCE_MODEL.md](CONVERGENCE_MODEL.md) |
| 9. Caso TypeSafe sin afirmaciones no soportadas | [RESEARCH_METHODOLOGY.md](RESEARCH_METHODOLOGY.md) sección 8 |
| 10. Prevención del sesgo retrospectivo | [RESEARCH_METHODOLOGY.md](RESEARCH_METHODOLOGY.md) sección 6 |

## 7. Decisiones

Tomadas el 2026-10-02:
- **Stack aprobado:** .NET 10 + React/Vite/TypeScript + PostgreSQL.
- **Interfaz en español** (A17 resuelto: UI en español, códigos y enums en inglés).
- **Fase 0 iniciada:** registro de señales en `signals/` con sellos OpenTimestamps.
- **Autenticación propia** por ahora (A16): JWT + BCrypt con roles OWNER, REVIEWER, VIEWER, compatible para migrar luego al SSO de la plataforma.
- **Despliegue local primero** con Docker Compose.

Pendientes:

1. TypeSafe: URL exacta y fecha aproximada de la primera exposición.
2. Fuentes de evidencia interna autorizadas: ¿se puede usar el historial git completo de `jegasolutions-platform` y otros repositorios como evidencia de fechas?
3. ¿Habrá un segundo evaluador humano para convergencias?
