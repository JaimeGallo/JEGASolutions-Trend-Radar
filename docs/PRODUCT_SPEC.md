# PRODUCT_SPEC: JEGASolutions Trend Radar

> Versión del documento: 0.1 (Fase 1, borrador). Basado en la especificación v2.0.

## 1. Propósito

Trend Radar es un sistema de **registro, contraste y medición** de señales anticipatorias. No es un agregador de noticias de tendencias.

Responde, con evidencia y sin suponer el resultado:

1. ¿Qué creíamos antes de conocer el resultado?
2. ¿Qué pasó realmente?
3. ¿Cuánto tiempo antes lo registramos, y qué tan específico era?
4. ¿En qué dominios y condiciones el reconocimiento de patrones de Jaime/JEGASolutions parece funcionar, y en cuáles no?

### Principios de producto (no negociables)

| # | Principio | Consecuencia en el producto |
|---|---|---|
| P1 | La historia no se reescribe | Registros originales inmutables; cambios como versiones nuevas |
| P2 | Evidencia antes del evento | Cronología basada en fechas verificables, no en memoria |
| P3 | Las intuiciones son hipótesis | Toda afirmación se puede evaluar y fallar |
| P4 | Los fallos cuentan | No hay borrado; las fallas entran en todas las métricas |
| P5 | Convergencia no es causalidad ni copia | Vocabulario controlado en conclusiones |
| P6 | Observación ≠ interpretación ≠ resultado medido | Tres capas visualmente separadas en toda la UI y los informes |
| P7 | La IA propone, el humano decide | Todo lo generado por IA queda marcado y requiere aceptación |
| P8 | Captura antes que análisis | Registrar una señal debe tomar menos de 30 segundos |

## 2. Usuarios y roles

| Rol | Quién | Permisos |
|---|---|---|
| OWNER | Jaime Gallo | Todo, salvo modificar registros inmutables |
| REVIEWER | Segundo evaluador opcional | Puntuar convergencias en modo ciego, comentar, disputar |
| VIEWER | Socios, asesores | Solo lectura |

Ningún rol, incluido OWNER, puede editar campos inmutables ni borrar registros.

## 3. Glosario operativo

| Término | Definición en el sistema |
|---|---|
| Señal (Signal) | Observación registrada por Jaime/JEGASolutions. Etapas: INTUITION, SIGNAL, OPPORTUNITY |
| Hipótesis | Explicación propuesta o desarrollo futuro, derivada de una o más señales. No necesariamente medible |
| Predicción | Afirmación medible, con horizonte temporal, criterio de resolución y confianza, registrada antes del resultado |
| Evento externo | Desarrollo público observable (lanzamiento, publicación, adopción) con fuente y fecha |
| Convergencia | Relación evaluada entre una señal interna y un evento externo, con relación temporal y puntuación |
| Tendencia (Trend) | Agrupación de señales y eventos con ciclo de vida |
| Exposición (Exposure) | Momento en que Jaime vio por primera vez un evento externo. Necesaria para evaluar independencia |
| Evidencia | Artefacto que respalda una afirmación: commit, captura, documento, URL, correo, mensaje |
| Resultado (Outcome) | Resolución de una predicción o experimento |
| Aprendizaje (Learning) | Conclusión registrada a partir de un resultado |

## 4. Requisitos funcionales

Prioridad: **M** = MVP, **I** = Fase Inteligencia, **A** = Fase Automatización.

### 4.1 Señales (Internal Radar)
| ID | Requisito | Prio |
|---|---|---|
| RF-S01 | Captura rápida: título y texto original obligatorios; todo lo demás opcional y completable después | M |
| RF-S02 | `recorded_at` asignado por el servidor, inmutable | M |
| RF-S03 | `original_text` inmutable; ediciones generan `signal_versions` | M |
| RF-S04 | Declarar `claimed_origin_at` con precisión e indicar si es retroactiva | M |
| RF-S05 | Adjuntar evidencia de origen (archivo, URL, commit, captura) con su propio sello de tiempo y nivel E0 a E4 | M |
| RF-S06 | Código legible `TR-{DOMINIO}-{NNN}` asignado automáticamente, nunca reutilizado | M |
| RF-S07 | Clasificar por dominio, subdominio, alcance geográfico, etiquetas, tipo de fuente | M |
| RF-S08 | Confianza al crear (0 a 100) inmutable | M |
| RF-S09 | Fusionar y dividir sin perder originales | I |
| RF-S10 | Importar señales desde commits git o notas, conservando la fecha original como evidencia, no como `recorded_at` | I |

### 4.2 Hipótesis y predicciones
| ID | Requisito | Prio |
|---|---|---|
| RF-P01 | Crear hipótesis vinculadas a una o más señales | M |
| RF-P02 | Crear predicción con: enunciado, criterio de resolución verificable, fecha límite (horizonte), confianza, tasa base estimada, especificidad autodeclarada | M |
| RF-P03 | Predicción inmutable tras un período de gracia de 15 minutos (para typos). Después, solo versión nueva V2, V3 que no reemplaza a V1 en las métricas | M |
| RF-P04 | Resolver predicción: CONFIRMED, PARTIAL, FAILED, UNRESOLVABLE, con evidencia y fecha de resolución | M |
| RF-P05 | Alertar predicciones próximas a su fecha límite | M |
| RF-P06 | Predicción vencida sin resolver queda en OVERDUE y se muestra como pendiente de evaluar | M |
| RF-P07 | Retirar una predicción solo con motivo; queda visible y contabilizada como WITHDRAWN | M |

### 4.3 Eventos externos (External Radar)
| ID | Requisito | Prio |
|---|---|---|
| RF-E01 | Registro manual de evento: título, descripción, actor, tipo, fecha pública (intervalo + precisión), fuente | M |
| RF-E02 | Fuente con URL, tipo (PRIMARY, SECONDARY, COMMUNITY, USER_GENERATED), fecha de publicación, fecha de consulta, extracto | M |
| RF-E03 | Archivar copia del extracto y, opcionalmente, URL de Wayback Machine | M |
| RF-E04 | Registrar exposición: cuándo vio Jaime el evento por primera vez | M |
| RF-E05 | Búsqueda de "arte previo": registrar eventos más tempranos del mismo concepto | M |
| RF-E06 | Asistencia de investigación por IA con fuentes verificadas | I |
| RF-E07 | Monitoreo programado de fuentes | A |

### 4.4 Convergencias
| ID | Requisito | Prio |
|---|---|---|
| RF-C01 | Crear convergencia señal-evento | M |
| RF-C02 | Cálculo automático de relación temporal BEFORE / AFTER / SIMULTANEOUS / UNKNOWN según reglas de [RESEARCH_METHODOLOGY.md](RESEARCH_METHODOLOGY.md) | M |
| RF-C03 | Puntuación por rúbrica de 9 dimensiones, especificidad y clasificación ([CONVERGENCE_MODEL.md](CONVERGENCE_MODEL.md)) | M |
| RF-C04 | Lista de verificación anti-sesgo obligatoria antes de marcar HUMAN VERIFIED | M |
| RF-C05 | Explicaciones alternativas obligatorias (mínimo una) | M |
| RF-C06 | Time-to-Convergence y Anticipation Window como rango | M |
| RF-C07 | Puntuación ciega opcional | M |
| RF-C08 | Convergencias candidatas generadas por similitud semántica | I |

### 4.5 Modos de análisis
| ID | Modo | Prio |
|---|---|---|
| RF-M01 | **Challenge Jaime**: formulario guiado de 9 bloques (Señal, Evidencia, Contraevidencia, Explicaciones alternativas, Tasa base, Predicción comprobable, Horizonte, Experimento, Decisión). En MVP sin IA; produce señal + hipótesis + predicción + experimento borrador | M |
| RF-M02 | Challenge Jaime con IA generando contraargumentos marcados AI GENERATED | I |
| RF-M03 | **Historical Verification**: dado un Signal ID, informe de cronología, evidencia, evento externo más temprano, convergencia, TTC, confianza, alternativas, preguntas abiertas | M (manual) / I (asistido) |
| RF-M04 | **"¿Realmente lo vi primero?"**: vista de conclusión con vocabulario limitado a Supported, Plausible, Uncertain, Not supported | M |

### 4.6 Oportunidades, experimentos, proyectos
| ID | Requisito | Prio |
|---|---|---|
| RF-O01 | Crear oportunidad desde una señal con los 13 campos de la sec. 27 | M (básico) |
| RF-O02 | Experimentos con hipótesis, criterios de éxito y fallo escritos antes del resultado | M (básico) |
| RF-O03 | Proyectos de cliente: requisito explícito, necesidad inferida, respuesta de diseño, validación, revisiones | I |
| RF-O04 | Requirement Anticipation Rate | I |

### 4.7 Dashboard, búsqueda, reportes
| ID | Requisito | Prio |
|---|---|---|
| RF-D01 | Dashboard principal: KPIs + paneles de señales internas, eventos externos, convergencias, predicciones, oportunidades | M |
| RF-D02 | Línea de tiempo doble interna/externa por convergencia y global | M |
| RF-D03 | Búsqueda de texto completo y filtros combinables | M |
| RF-D04 | Paleta de comandos (Ctrl/Cmd+K) y atajos de teclado | M |
| RF-D05 | Radar de burbujas (confianza interna vs. evidencia externa) | I |
| RF-D06 | Gráfico fecha señal vs. fecha convergencia | I |
| RF-D07 | Grafo de relaciones | I |
| RF-D08 | Trend Brief semanal, Foresight Review mensual, informe trimestral bajo demanda | I |
| RF-D09 | Informes programados | A |

### 4.8 Auditoría y gobierno
| ID | Requisito | Prio |
|---|---|---|
| RF-G01 | Log de auditoría append-only: usuario, fecha, entidad, valor anterior, valor nuevo, motivo | M |
| RF-G02 | Motivo obligatorio para cualquier cambio en entidades de historial | M |
| RF-G03 | Verificación de integridad (cadena de hashes) visible en UI | M |
| RF-G04 | Exportación completa (JSON + CSV) para auditoría externa | M |

## 5. Requisitos no funcionales

| ID | Requisito |
|---|---|
| RNF-01 | Integridad: campos inmutables protegidos en BD (triggers + permisos), no solo en código |
| RNF-02 | Evidencia de manipulación: cadena de hashes SHA-256 sobre versiones y auditoría; anclaje externo periódico |
| RNF-03 | Tiempos en UTC con zona horaria de origen registrada; reloj del servidor como única fuente de `recorded_at` |
| RNF-04 | Captura de señal en menos de 30 s y menos de 3 interacciones desde cualquier pantalla |
| RNF-05 | Respuesta de API p95 menor a 300 ms para lecturas con volumen esperado (menos de 50.000 registros) |
| RNF-06 | Accesibilidad WCAG 2.2 AA; navegación completa por teclado |
| RNF-07 | Pantallas principales en 1440×900 sin scroll de página; scroll solo dentro de paneles |
| RNF-08 | Respaldo diario de BD con retención mínima de 1 año; restauración probada |
| RNF-09 | Secretos fuera del repositorio; datos CLIENT_CONFIDENTIAL nunca enviados a IA externa por defecto |
| RNF-10 | Despliegue con Docker Compose; un solo comando para entorno local |
| RNF-11 | Proveedor de IA intercambiable; la app funciona completa sin IA |
| RNF-12 | Todas las fórmulas de métricas versionadas; cada valor guardado indica la versión de fórmula |

## 6. Alcance del MVP (Fase 3)

**Incluido** (corresponde a la sec. 54):

1. Captura de señales con fecha inmutable y evidencia de origen
2. Línea de tiempo de señales
3. Historial inmutable y versionado
4. Hipótesis
5. Predicciones con criterios pre-registrados, resolución y vencimiento
6. Eventos externos manuales con fuentes y exposiciones
7. Evidencia (archivos, URLs, commits)
8. Convergencias con rúbrica, clasificación y relación temporal
9. Time-to-Convergence y Anticipation Window
10. Dashboard básico con KPIs y paneles
11. Challenge Jaime (formulario guiado, sin IA)
12. Auditoría y verificación de integridad
13. Búsqueda y filtros, paleta de comandos
14. Datos semilla: los 5 casos de la sec. 41, todos en estado de investigación, sin resultados inventados

**Excluido del MVP:** IA, crawling, informes programados, grafo visual, radar de burbujas, proyectos de cliente completos, métricas JAI (se calculan, pero no se muestran hasta tener N suficiente), multi-tenant, app móvil.

## 7. Flujos principales

### F1. Captura rápida
`Ctrl+K → "Nueva señal"` o botón persistente `+` → título + texto original → guardar. El servidor asigna `recorded_at` y código. Opcionalmente: dominio, confianza, origen declarado.

### F2. Registrar caso retroactivo
Nueva señal → marcar "La idea es anterior a hoy" → `claimed_origin_at` + precisión → adjuntar evidencia de origen (commit, captura, archivo) → el sistema calcula el nivel de evidencia y `verified_origin_at` (o lo deja vacío).

### F3. De señal a predicción
Señal → "Formular hipótesis" → "Convertir en predicción": enunciado, criterio de resolución, fecha límite, confianza, tasa base, especificidad → confirmación explícita "Esta predicción quedará bloqueada en 15 minutos".

### F4. Registrar evento externo y evaluar convergencia
Nuevo evento → fuente(s) → exposición de Jaime → "Comparar con señal" → el sistema muestra la relación temporal calculada → buscar arte previo → rúbrica (opcional ciega) → explicaciones alternativas → lista anti-sesgo → clasificación → HUMAN VERIFIED.

### F5. Resolver predicción
Alerta de vencimiento → abrir predicción (enunciado original bloqueado visible) → aplicar criterio de resolución escrito en su momento → resultado + evidencia → aprendizaje.

### F6. ¿Realmente lo vi primero?
Señal → modo verificación → informe con cronología, especificidad, evidencia, evento externo, similitud, tasa base, alternativas, conclusión con vocabulario controlado.

## 8. Diseño de interfaz

### Lenguaje visual
Azul profundo / índigo como base, acentos dorados sobrios para estados verificados y KPIs, tipografía sans de alta legibilidad (Inter o similar) y monoespaciada para códigos y fechas. Modo oscuro por defecto con modo claro. Sin estética de adivinación, sin ciencia ficción exagerada.

### Distribución principal (sin scroll de página)

```
┌──────┬──────────────────────────────────────────────────────────────┐
│      │ KPIs: Señales activas · Predicciones activas · Convergencias │
│ NAV  │       BEFORE · TTC mediana · Tasa fallidas · JAI (N)         │
│      ├───────────────────────┬──────────────────────────────────────┤
│ Radar│ LISTA (panel con      │ DETALLE CONTEXTUAL                   │
│ Señal│ scroll propio)        │ Original · Fecha · Evidencia ·       │
│ Pred.│ filtros arriba        │ Hipótesis · Predicción ·             │
│ Event│                       │ Coincidencias externas ·             │
│ Conv.│                       │ Línea de tiempo doble · Score ·      │
│ Oport│                       │ Resultado                            │
│ Repor│                       │ (secciones expandibles)              │
└──────┴───────────────────────┴──────────────────────────────────────┘
```

Patrón maestro-detalle: abrir una señal no cambia de página; el panel derecho muestra la cadena completa **SEÑAL → EVIDENCIA → LÍNEA DE TIEMPO → CONVERGENCIA → RESULTADO**.

### Separación de capas epistémicas
Cada bloque de contenido lleva una marca visual:
- **OBSERVACIÓN** (registro original, inmutable, icono de candado)
- **INTERPRETACIÓN** (hipótesis, puntuaciones, clasificación; con autor: humano o IA)
- **RESULTADO MEDIDO** (resoluciones, métricas con N y fórmula)

### Atajos previstos
`Ctrl/Cmd+K` paleta · `N` nueva señal · `E` nuevo evento · `/` buscar · `J/K` moverse en listas · `Enter` abrir detalle · `Esc` cerrar panel · `G S` ir a señales · `G P` predicciones · `G C` convergencias.

### Vocabulario prohibido en conclusiones
"genio", "visionario", "profeta", "trendsetter", "predijo" (salvo predicción formal confirmada con cronología BEFORE verificada), "copió", "inventó".
