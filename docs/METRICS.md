# METRICS: JEGASolutions Trend Radar

> Versión 0.1 (Fase 1). Todas las fórmulas son versionadas; cada valor guardado en `score_snapshots` indica `formula_version`.

## 0. Reglas generales de presentación

1. Toda métrica muestra **N** (cuántos casos la sustentan) y, cuando aplica, intervalo de confianza.
2. Si N está por debajo del mínimo, se muestra "N insuficiente (n = x de y requeridos)" en lugar del número.
3. Cada métrica tiene un tooltip con fórmula, versión y exclusiones.
4. Ninguna métrica se presenta como "capacidad de predecir el futuro". El JAI se rotula: **"Desempeño histórico de hipótesis anticipatorias registradas"**.
5. Las métricas se calculan por dominio además de globales (sec. 33). Se espera desempeño desigual entre dominios; no se asume capacidad universal.
6. Las métricas separan **retrospectivas** (casos con origen reconstruido) de **prospectivas** (pre-registradas). La vista por defecto muestra solo prospectivas; las retrospectivas aparecen aparte, con advertencia.

## 1. JEGASignal Score (prioridad interna, 0 a 100)

Mecanismo de priorización, **no** medida de verdad. Se guarda como snapshot fechado (cambia con el tiempo, el histórico se conserva).

| Factor | Pregunta | Peso | Escala |
|---|---|---|---|
| Fuerza de evidencia | ¿Cuánta evidencia y de qué nivel? | 1.5 | 0 a 10 |
| Recurrencia | ¿Cuántas veces se observó independientemente? | 1.0 | 0 a 10 |
| Diversidad de fuentes | ¿De cuántos tipos de fuente distintos? | 1.0 | 0 a 10 |
| Crecimiento | ¿Aumentan las observaciones en el tiempo? | 1.0 | 0 a 10 |
| Credibilidad | ¿Calidad de las fuentes (primarias > comunidad)? | 1.0 | 0 a 10 |
| Relevancia | ¿Qué tan relevante para JEGASolutions? | 1.5 | 0 a 10 |
| Impacto potencial | ¿Magnitud si se materializa? | 1.0 | 0 a 10 |
| Especificidad | Rúbrica 1 a 5 escalada | 1.0 | 0 a 10 |
| Relevancia temporal | ¿Es el momento oportuno? | 0.5 | 0 a 10 |
| Potencial de ejecución | ¿Puede JEGASolutions actuar sobre esto? | 1.0 | 0 a 10 |

```
JEGASignal = 100 × Σ (w_k × x_k) / Σ (w_k × 10)
```

En el MVP los factores se asignan manualmente; en la Fase 4 algunos se derivan (recurrencia, diversidad, crecimiento) a partir de datos.

## 2. Time-to-Convergence y Anticipation Window

Solo para convergencias con `temporal_relation = BEFORE`.

```
TTC_min = X_min - I_max        TTC_max = X_max - I_min          (días)
meses = días / 30.44           años = días / 365.25
```

- `I`: intervalo de origen interno usable (pre-registro o verificado ≥ E2).
- `X`: observabilidad del evento evaluado.
- **Anticipation Window**: igual, pero `X` es el evento **más temprano conocido** del mismo concepto (incluye arte previo). Siempre ≤ TTC. Es la medida honesta de antelación: cuánto tiempo existió la señal antes de que algo comparable fuera observable en cualquier parte.

Agregados (dashboard): mediana, media, rango intercuartílico de `anticipation_window_min_days` sobre convergencias VERIFIED, no MARKET_WIDE, similitud ≥ MODERATE. Mínimo N = 5.

## 3. JEGAS Anticipation Index (JAI)

### 3.1 Por qué no éxitos / total
Acertar "la IA seguirá creciendo" no vale lo mismo que acertar una predicción específica, improbable y lejana. El JAI premia **superar la tasa base**, con peso por especificidad y antelación, y castiga el exceso de confianza.

### 3.2 Datos de cada predicción (todos fijados al crear, bloqueados)
- `p`: confianza, probabilidad de que ocurra (0.01 a 0.99)
- `b`: tasa base estimada, probabilidad de que ocurra "por defecto" sin el criterio de Jaime (0.01 a 0.99)
- `spec`: especificidad 1 a 5
- `lead`: meses entre `recorded_at` y `horizon_date`

Al resolver: `o` = 1 (CONFIRMED), `c` ∈ (0,1) (PARTIAL, con crédito justificado), 0 (FAILED).

### 3.3 Fórmula `jai-v1` (Brier Skill Score ponderado)

```
Brier_i      = (p_i - o_i)²                  error de Jaime
BrierBase_i  = (b_i - o_i)²                  error de la tasa base
w_i          = s(spec_i) × l(lead_i)

s(spec): 1 → 0.2, 2 → 0.5, 3 → 1.0, 4 → 1.5, 5 → 2.0
l(lead): 0.5 + 0.5 × min(1, lead_meses / 24)

BSS = 1 - Σ w_i·Brier_i / Σ w_i·BrierBase_i
JAI = 50 + 50 × clamp(BSS, -1, 1)            ∈ [0, 100]
```

Lectura:
- **JAI = 50**: tan bueno como la tasa base (no hay evidencia de anticipación).
- **JAI > 50**: mejor que la tasa base; **< 50**: peor.
- Intervalo de confianza del 90 % por bootstrap (2.000 remuestreos). Si el intervalo incluye 50, la UI dice "no distinguible de la tasa base".
- **Mínimo N = 10** predicciones resueltas para mostrarlo; por debajo, solo se muestra el conteo.

### 3.4 Inclusiones y exclusiones
| Estado | Tratamiento |
|---|---|
| CONFIRMED, PARTIAL, FAILED | Incluidas |
| UNRESOLVABLE | Excluidas, pero se reporta su proporción (una tasa alta indica predicciones mal formuladas) |
| WITHDRAWN antes de la mitad del horizonte y sin evidencia contraria registrada | Excluidas, reportadas aparte |
| WITHDRAWN en otro caso | Cuentan como FAILED (o = 0) |
| OVERDUE sin resolver más de 60 días | Alerta; tras 120 días se fuerza revisión |
| Versiones V2, V3 | Cada versión es una predicción independiente con su propia fecha; V1 nunca se excluye |

### 3.5 Riesgo: manipular la tasa base
Si `b` se subestima, el JAI sube artificialmente. Mitigaciones:
- `b` se fija al crear y se bloquea.
- Opción de que un segundo evaluador (o la IA, marcada) estime `b` de forma independiente al crear. Se muestra un **JAI alternativo** con la tasa base del evaluador independiente.
- Se muestra también el Brier score simple (sin tasa base), que no depende de `b`.

## 4. Calibración (Prediction Calibration)

¿Cuando Jaime dice 70 %, ocurre ~70 % de las veces?

- Diagrama de fiabilidad por intervalos de confianza (10-30, 30-50, 50-70, 70-90, 90-99), con N por intervalo.
- **Sesgo de confianza** = media(p) - media(o). Positivo: exceso de confianza.
- Brier score global.
- Mínimo N = 20 resueltas para mostrar el diagrama.

## 5. Predicciones fallidas y falsos negativos

Terminología (ver DISCOVERY A10):

| Métrica del dashboard | Fórmula | Nota |
|---|---|---|
| Tasa de predicciones fallidas | FAILED / (CONFIRMED + PARTIAL + FAILED) | Es lo que la sec. 21 llama "False Positive Rate"; técnicamente es la tasa de falsos descubrimientos |
| Tasa de acierto parcial | PARTIAL / resueltas | |
| Tasa de no resolubles | UNRESOLVABLE / total vencidas | Calidad de formulación |
| Tendencias no detectadas | Conteo en el registro de "missed trends" por período y dominio | Falsos negativos. Solo se cuentan tendencias en dominios monitoreados, registradas con fuente |
| Tasa de omisión | missed / (missed + tendencias con señal interna previa) | Solo si hay al menos 10 tendencias registradas en el dominio |

Ninguna predicción fallida se oculta; el panel de fallidas está en el dashboard principal, al mismo nivel que las convergencias.

## 6. Métricas de convergencia (señales sin predicción formal)

Muchas señales no se formalizan como predicción. Para ellas:

| Métrica | Definición |
|---|---|
| Convergencias anticipatorias | Conteo de convergencias VERIFIED, BEFORE, similitud ≥ MODERATE, no MARKET_WIDE |
| Tasa de convergencia por cohorte | De todas las señales registradas en un mes dado, % que alcanzó una convergencia anticipatoria en los 24 meses siguientes. Denominador: **todas** las señales de la cohorte (controla sesgo de selección) |
| Calidad media de convergencia | Mediana del score de convergencia de las anticipatorias |
| Rechazo de candidatas IA | REJECTED / candidatas de IA (Fase 4) |

## 7. Requirement Anticipation Rate (Fase 4)

Solo cuenta requisitos con `inferred_need` registrado **antes** de la primera entrega.

Rúbrica de magnitud del cambio pedido tras la primera entrega:

| Magnitud | Criterio | Valor |
|---|---|---|
| NONE | Aprobado sin cambios | 1.0 |
| MINOR | Ajustes de texto, color, orden; menos del 10 % del esfuerzo inicial | 1.0 |
| MODERATE | Cambios a parte del flujo; 10 % a 40 % del esfuerzo | 0.5 |
| MAJOR_REDESIGN | Concepto rechazado o más del 40 % rehecho | 0.0 |

```
RAR = media(valor) sobre requisitos elegibles, por proyecto y global; mínimo N = 10
```

Se reporta también la **ambigüedad inicial** (baja, media, alta) para ver si el desempeño se mantiene cuando el requisito es más ambiguo. El RAR mide desempeño observado en proyectos; no prueba superioridad de diseño general (no hay grupo de comparación).

## 8. Desempeño por dominio (sec. 33)

Tabla con, por dominio: señales, predicciones resueltas, JAI (si N ≥ 10), tasa de fallidas, convergencias anticipatorias, mediana de Anticipation Window, tasa de convergencia por cohorte. Celdas con N insuficiente se muestran atenuadas con el N.

## 9. Métricas del informe trimestral (sec. 38)

Total de señales (nuevas y acumuladas), predicciones creadas y resueltas, confirmadas, parciales, fallidas, retiradas, no resolubles; convergencias externas (por clasificación); mediana y media de TTC y Anticipation Window; desempeño por dominio; conversión señal → oportunidad, oportunidad → experimento, experimento → resultado positivo; impacto comercial (solo con evidencia registrada: propuesta enviada, contrato, ingreso, no estimaciones).

El informe separa explícitamente tres secciones: **OBSERVACIÓN** (qué se registró), **INTERPRETACIÓN** (qué creemos que significa, con autor humano o IA) y **RESULTADO MEDIDO** (métricas con N, fórmula y versión).

## 10. Versionado de fórmulas

| Fórmula | Versión inicial | Revisión prevista |
|---|---|---|
| JEGASignal Score | `jegasignal-v1` | Tras 50 señales |
| Convergencia | `convergence-v1` | Tras 20 convergencias evaluadas |
| JAI | `jai-v1` | Tras 30 predicciones resueltas |
| RAR | `rar-v1` | Tras 10 requisitos |

Cambiar una fórmula crea una nueva versión; los snapshots anteriores no se recalculan en sitio. El dashboard indica qué versión usa.
