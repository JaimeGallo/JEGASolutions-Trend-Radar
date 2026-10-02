# CONVERGENCE_MODEL: JEGASolutions Trend Radar

> Versión 0.1 (Fase 1). Fórmula `convergence-v1`. Toda puntuación guardada referencia la versión de fórmula con que se calculó.

## 1. Qué mide y qué no

La convergencia mide **qué tan parecidos son** una señal interna y un evento externo, ajustado por **qué tan específica** era la señal. No mide:
- causalidad ni copia (nunca se infiere de la convergencia);
- quién fue primero (eso lo decide la relación temporal, por separado);
- mérito ni talento.

Una convergencia tiene, por lo tanto, tres componentes independientes:

```
CONVERGENCIA = SIMILITUD (rúbrica)  ×  ESPECIFICIDAD (señal interna)
               + RELACIÓN TEMPORAL (RESEARCH_METHODOLOGY §5)
               + CONTEXTO DE MERCADO (arte previo, actores independientes)
```

## 2. Rúbrica de similitud: 9 dimensiones

Cada dimensión se puntúa 0 a 4, o N/A si la señal no dice nada al respecto (N/A no es 0: no penaliza ni suma).

| Dim | Pregunta | Peso |
|---|---|---|
| D1 Problema | ¿Abordan el mismo problema? | 1.50 |
| D2 Usuario | ¿El mismo tipo de usuario o cliente? | 1.00 |
| D3 Comportamiento | ¿El mismo cambio de comportamiento o uso? | 1.00 |
| D4 UX | ¿Patrones de interacción equivalentes? | 1.00 |
| D5 Tecnología | ¿Tecnología o enfoque técnico equivalente? | 1.00 |
| D6 Concepto de producto | ¿El mismo concepto de solución? | 1.50 |
| D7 Modelo de negocio | ¿Monetización o modelo equivalente? | 0.75 |
| D8 Terminología | ¿Lenguaje y nombres similares? (peso bajo: es lo más superficial) | 0.50 |
| D9 Implementación | ¿Detalles de implementación concretos coinciden? | 1.25 |

Anclas por nivel (aplican a todas las dimensiones):

| Nivel | Significado |
|---|---|
| 0 | Distinto o contradictorio |
| 1 | Mismo tema general ("ambos son de IA") |
| 2 | Mismo subproblema o categoría, solución distinta |
| 3 | Misma idea central con diferencias materiales |
| 4 | Prácticamente equivalente en esta dimensión |

**Regla anti-palabras clave:** D8 (terminología) nunca puede elevar la clasificación por sí sola. Si D8 ≥ 3 y el resto de dimensiones aplicables promedia < 2, se activa `genericness_flag` y la convergencia no puede superar WEAK.

## 3. Especificidad de la señal interna

Se puntúa sobre el **texto original** (no sobre versiones posteriores), idealmente sin ver el evento externo.

| Nivel | Descripción | Ejemplo | Factor `f` |
|---|---|---|---|
| 1 | Afirmación genérica sobre un campo | "La IA será importante" | 0.40 |
| 2 | Dirección general dentro de un dominio | "Los dashboards serán más densos" | 0.60 |
| 3 | Patrón identificable con usuario o contexto | "Las apps B2B evitarán el scroll de página y usarán paneles con scroll interno" | 0.80 |
| 4 | Solución concreta y comprobable | "Un panel maestro-detalle con paleta de comandos para gestionar reportes de pymes" | 0.95 |
| 5 | Solución concreta con detalles no obvios que serían sorprendentes si coinciden | Flujo, nombres de funciones, modelo de precios específicos | 1.00 |

## 4. Fórmula `convergence-v1`

```
A          = dimensiones con puntuación (no N/A)
similitud  = Σ_{d∈A} (w_d × s_d) / Σ_{d∈A} (w_d × 4)            ∈ [0, 1]
score      = 100 × similitud × f(especificidad)                 ∈ [0, 100]
```

Condiciones de validez:
- Al menos 4 dimensiones aplicables, incluidas D1 y D6. Si no, `classification = UNKNOWN`.
- Si `genericness_flag`, `score = min(score, 39)`.

Por qué multiplicar por la especificidad: una señal genérica coincide con casi cualquier cosa del campo; su "similitud" no aporta información. Así, "la IA será importante" frente a un producto de agentes de IA puede tener D1 = 1, D6 = 1 y especificidad 1 → score ≈ 10 (COINCIDENCE/WEAK), mientras que una descripción detallada de un flujo que luego se implementa casi igual puede superar 80.

## 5. Clasificación

### 5.1 Nivel de similitud (`similarity_level`)

| Nivel | Regla |
|---|---|
| EXACT_HIGH | score ≥ 80 y D6 ≥ 3 y D9 ≥ 3 |
| STRONG_CONCEPTUAL | score ≥ 60 y D1 ≥ 3 y D6 ≥ 3 |
| MODERATE | score ≥ 40 y D1 ≥ 2 |
| WEAK | score ≥ 20 |
| COINCIDENCE | score < 20 |
| UNKNOWN | rúbrica inválida o incompleta |

### 5.2 Clasificación final (`classification`)

Igual al nivel de similitud, **salvo** que se cumpla la condición de mercado:

**MARKET_WIDE** si existen al menos 3 actores independientes (distintos del evento evaluado y sin relación con JEGASolutions) con implementaciones de nivel ≥ MODERATE respecto a la señal, observables antes o dentro de la ventana de simultaneidad del origen interno, o del evento evaluado. En ese caso se conserva `similarity_level` como dato subordinado ("MARKET_WIDE, similitud STRONG_CONCEPTUAL") y la convergencia no cuenta como anticipación individual en el JAI, aunque la relación temporal sea BEFORE.

Las clasificaciones se guardan con nombres en inglés (enum); la UI muestra: Convergencia alta/exacta, Convergencia conceptual fuerte, Convergencia moderada, Convergencia débil, Coincidencia, Convergencia de mercado, Desconocida.

## 6. Evaluaciones múltiples y autoevaluación

Una convergencia puede tener varias filas en `convergence_assessments`:

| Tipo | Uso |
|---|---|
| Humana ciega | Preferida. El evaluador ve ambos textos sin fechas, autoría ni etiquetas "interno/externo" (se presentan como "Descripción A" y "Descripción B", en orden aleatorio) |
| Humana no ciega | Aceptada, marcada como tal. Rebaja la conclusión máxima a Plausible si es la única |
| IA | Siempre AI GENERATED. Sirve como segunda opinión; nunca basta para VERIFIED |

Agregación:
- Puntuación final = mediana de evaluaciones humanas por dimensión (ciegas primero si existen).
- Si dos evaluaciones humanas difieren en más de 1 punto en D1, D6 o D9, o en más de una clase, la convergencia pasa a `UNDER_REVIEW` y se registra la discrepancia.
- La IA y los humanos se comparan con kappa de Cohen ponderado; si la concordancia es baja, se muestra como alerta de calibración del modelo de IA (no del humano).

## 7. Estados de una convergencia

```
CANDIDATE ──► UNDER_REVIEW ──► VERIFIED (HUMAN_VERIFIED)
    │               │                 │
    └──► REJECTED ◄─┘                 └──► DISPUTED ──► (nueva evaluación)
```

- CANDIDATE: creada a mano o propuesta por IA. No cuenta en ninguna métrica.
- VERIFIED: requiere rúbrica válida, explicaciones alternativas, lista anti-sesgo completa y acción humana explícita.
- REJECTED se conserva y cuenta en la tasa de rechazo de candidatas de IA.

## 8. Similitud semántica asistida (Fase 4)

No se usa similitud por palabras clave para decidir nada. La IA interviene en dos pasos y solo como sugerencia:

1. **Generación de candidatas:** embeddings (pgvector) de `original_text` de señales y de la descripción de eventos. Para cada evento nuevo se recuperan las 10 señales más cercanas por coseno, filtradas por dominio y por fecha (solo señales con origen anterior). Umbral inicial de coseno a calibrar con casos etiquetados; no se fija a ciegas.
2. **Evaluación propuesta por LLM:** el modelo recibe ambos textos (sin fechas, en modo ciego), devuelve puntuaciones por dimensión, especificidad y una justificación con citas literales de ambos textos. Se guarda en `ai_suggestions` y `convergence_assessments` con `assessor_kind = AI`.

Una similitud alta de embeddings nunca convierte una candidata en convergencia verificada (sec. 45 y 55).

## 9. Ejemplo ilustrativo (ficticio, no es un dato del sistema)

> Solo para mostrar la mecánica de la fórmula. No representa ningún caso real ni se cargará como dato.

Señal: "Las herramientas internas de pymes reemplazarán hojas de Excel compartidas por WhatsApp con formularios que generan el reporte automáticamente." Especificidad 4 (f = 0.95).
Evento: producto que convierte mensajes de WhatsApp en registros estructurados y reportes.

| D1 | D2 | D3 | D4 | D5 | D6 | D7 | D8 | D9 |
|---|---|---|---|---|---|---|---|---|
| 4 | 3 | 3 | N/A | 2 | 3 | N/A | 2 | 2 |

Similitud = (6 + 3 + 3 + 2 + 4.5 + 1 + 2.5) / ((1.5+1+1+1+1.5+0.5+1.25) × 4) = 22 / 31 = 0.71 → score = 0.71 × 0.95 × 100 ≈ 67 → STRONG_CONCEPTUAL (D1 ≥ 3, D6 ≥ 3). Si se encuentran 3 actores independientes anteriores con lo mismo → MARKET_WIDE.

## 10. Calibración del modelo

`convergence-v1` es una propuesta inicial. Tras las primeras 20 convergencias evaluadas por humanos se revisa:
- si los pesos reflejan el juicio de los evaluadores (regresión simple de la clasificación humana sobre las dimensiones);
- si los umbrales de clase producen clasificaciones razonables;
- la concordancia entre evaluadores.

Un cambio de pesos o umbrales crea `convergence-v2`. Los valores anteriores se conservan con su versión; se pueden recalcular en paralelo, nunca sobrescribir.
