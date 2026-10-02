# RESEARCH_METHODOLOGY: JEGASolutions Trend Radar

> Versión 0.1 (Fase 1). Define cómo se establece la cronología, cómo se valora la evidencia, cómo se verifica un caso histórico y cómo se previene el sesgo retrospectivo.

## 1. Pregunta de investigación

> ¿Registra Jaime/JEGASolutions ciertos patrones antes de que sean observables externamente, y en qué condiciones?

Se trata como hipótesis a contrastar, no como premisa. Un resultado posible y aceptable del sistema es: **"No hay evidencia suficiente de anticipación en el dominio X."**

## 2. Tres fechas por cada señal

| Fecha | Quién la fija | Uso |
|---|---|---|
| `recorded_at` | Reloj del servidor al guardar | Prueba más fuerte: el sistema la controla y la ancla externamente |
| `claimed_origin` | Jaime, como intervalo con precisión | Hipótesis sobre cuándo existía la idea. Por sí sola **no** sirve para afirmar BEFORE |
| `verified_origin_at` | Derivada de la evidencia más temprana de nivel E2 o superior | Base de la cronología en casos retrospectivos |

Una señal **pre-registrada** (registrada en el sistema antes del evento externo) es la evidencia de mayor calidad. Las señales retrospectivas pueden ser válidas, pero siempre con menor peso y marcadas como tales.

## 3. Niveles de evidencia de fecha

| Nivel | Nombre | Ejemplos | ¿Puede sostener BEFORE? |
|---|---|---|---|
| E0 | Memoria declarada | "Lo pensé a mediados de 2025" | No. Máximo UNKNOWN |
| E1 | Artefacto propio sin sello externo | Archivo local con fecha de modificación, nota sin sincronizar, captura sin metadatos | No. Máximo UNKNOWN (fechas fácilmente alterables) |
| E2 | Artefacto con fecha controlable por el autor, pero consistente | Fecha de autor de un commit git, metadatos EXIF, documento con historial de versiones local | Sí, con advertencia y peso reducido, si es coherente con otra evidencia |
| E3 | Sello de tiempo de un tercero | Fecha de push en GitHub, correo enviado (cabeceras del proveedor), mensaje a cliente (WhatsApp, Slack), captura del sitio en Wayback Machine, historial de Google Docs, despliegue con fecha en Vercel u otro hosting | Sí |
| E4 | Sello criptográfico o pre-registro | Señal registrada en Trend Radar con cadena anclada, sello RFC 3161, OpenTimestamps | Sí, máxima confianza |

Notas:
- La fecha de autor de un commit (`git_author_date`) puede fijarse a mano. La fecha en que GitHub recibió el push (eventos de la API, fecha de PR, fecha de despliegue) no. Siempre que exista, se prefiere la segunda.
- La fecha de push solo es consultable en la API de eventos de GitHub durante 90 días. Para que sea evidencia duradera hay que capturarla en ese plazo (o usar un sello E4). Por eso el registro de la Fase 0 sella cada señal con OpenTimestamps.
- Un repositorio clonado con historial reescrito (rebase, squash) puede perder fechas. Se registra la fuente exacta (repo, commit, URL).
- La evidencia debe mostrar **el contenido específico** de la idea, no solo actividad en esa fecha. Un commit de enero que "toca el layout" no prueba que en enero existiera el patrón de reducción de scroll; hay que mostrar el diff o una captura de esa versión.

## 4. Fecha del evento externo

Para cada evento se busca la **primera fecha públicamente observable**, no la fecha de difusión masiva:
1. Fuente primaria: anuncio oficial, changelog, release de GitHub, documentación con fecha, publicación académica.
2. Si no hay fecha explícita: primera captura en Wayback Machine (API CDX) que muestre el elemento relevante, no solo el dominio.
3. Fuentes secundarias solo como respaldo; comunidad (Reddit, X) nunca como única prueba de adopción.

Además se registra **arte previo**: implementaciones anteriores del mismo concepto por otros actores. El evento contra el cual se mide la anticipación es el **más temprano conocido**, no el que motivó el caso. Esto evita inflar la antelación eligiendo un ejemplo tardío.

## 5. Reglas de cronología

Sean:
- `I = [I_min, I_max]` el intervalo de origen interno usable (pre-registro, o `verified_origin` de nivel ≥ E2);
- `X = [X_min, X_max]` el intervalo de observabilidad externa del evento **más temprano conocido**;
- `w` la ventana de simultaneidad (por defecto 30 días).

```
si no hay origen interno de nivel ≥ E2 y la señal no está pre-registrada  → UNKNOWN
si no hay fecha externa verificada                                        → UNKNOWN
si I_max + w < X_min                                                      → BEFORE
si X_max + w < I_min                                                      → AFTER
en otro caso (los intervalos ± w se tocan)                                → SIMULTANEOUS
```

Regla adicional de **exposición**: si existe una exposición registrada de Jaime a ese evento (o a su arte previo) anterior a `I_min`, la relación pasa a AFTER con motivo `PRIOR_EXPOSURE`, aunque las fechas digan BEFORE. Si no se sabe cuándo ocurrió la exposición, se marca `exposure_unknown` y el resultado se rebaja a lo sumo a "Plausible" en el modo "¿Lo vi primero?".

Solo BEFORE contribuye a métricas de anticipación.

Time-to-Convergence (TTC) = `X - I`, reportado como rango `[X_min - I_max, X_max - I_min]` en días, meses (días / 30.44) y años. El valor conservador para métricas es el mínimo.

## 6. Prevención del sesgo retrospectivo (sec. 60.10)

| Mecanismo | Sesgo que ataca |
|---|---|
| `recorded_at` del servidor + cadena de hashes + anclaje externo | Retrospectivo, reescritura de historia |
| Predicciones con criterio de resolución, horizonte, confianza y tasa base escritos **antes** y bloqueados | Retrospectivo, racionalización post-hoc, olvido de la tasa base |
| Resolución aplicando el criterio original, mostrado junto al formulario de resolución | Racionalización post-hoc |
| Separación de fechas `recorded` / `claimed` / `verified` y niveles E0 a E4 | Retrospectivo, memoria reconstruida |
| Registro de exposiciones | Influencia no reconocida, ilusión de independencia |
| Búsqueda obligatoria de arte previo | Inflar la antelación, sesgo de disponibilidad |
| Puntuación ciega (sin fechas ni autoría) y segundo evaluador | Confirmación, autoevaluación |
| Penalización de especificidad baja ("la IA será importante") | Similitud genérica |
| Denominador total: todas las señales y predicciones cuentan, incluidas las fallidas y retiradas | Selección, supervivencia |
| Registro de tendencias no detectadas (falsos negativos) | Supervivencia |
| Tasa de convergencia por cohorte (de todas las señales de un mes, cuántas convergieron) | Selección, frecuencia (Baader-Meinhof) |
| Lista de verificación anti-sesgo obligatoria antes de HUMAN VERIFIED | Todos |
| Vocabulario controlado en conclusiones | Sobreinterpretación |
| Captura en menos de 30 s | Selección (registrar solo lo "importante") |

### Lista de verificación anti-sesgo (obligatoria por convergencia)

1. ¿Qué evidencia de la fecha interna existe y de qué nivel es?
2. ¿Qué evidencia contradice la relación?
3. ¿Qué explicación alternativa existe? (mínimo una, escrita)
4. ¿La observación fue realmente temprana respecto al **arte previo** más antiguo encontrado?
5. ¿Qué tan específica era la señal original? (1 a 5)
6. ¿Qué tan difícil era anticipar esto? ¿Cuántos actores lo hacían ya?
7. ¿Con qué frecuencia ocurre normalmente este tipo de desarrollo (tasa base)?
8. ¿Jaime había estado expuesto a algo similar antes?
9. ¿Esta coincidencia la encontré buscándola a propósito tras ver el evento externo? (marca de búsqueda motivada)

## 7. Modo Historical Verification / "¿Realmente lo vi primero?"

Entrada: `signal_code`. Salida estructurada:

| Bloque | Contenido |
|---|---|
| Cronología | Fechas interna y externa con niveles, intervalos, relación temporal, exposición |
| Especificidad | Puntuación 1 a 5 y texto original citado literalmente |
| Evidencia | Lista con nivel E0 a E4 y fuente |
| Evento externo | El más temprano conocido, arte previo, fuente primaria |
| Similitud | Puntuación de convergencia, dimensiones, clasificación, evaluadores |
| Tasa base | Frecuencia del tipo de desarrollo y número de actores independientes |
| Explicaciones alternativas | Todas las registradas |
| Preguntas abiertas | Evidencia faltante, fechas sin verificar |
| Conclusión | **Supported**, **Plausible**, **Uncertain** o **Not supported** |

Reglas de conclusión:

| Conclusión | Condiciones mínimas |
|---|---|
| Supported | BEFORE con base PRE_REGISTERED o evidencia ≥ E3; sin exposición previa conocida; especificidad ≥ 3; convergencia ≥ MODERATE verificada por humano (preferible ciega); no MARKET_WIDE con arte previo anterior a la señal |
| Plausible | BEFORE con evidencia E2, o exposición desconocida, o especificidad 2, o solo autoevaluación no ciega |
| Uncertain | UNKNOWN o SIMULTANEOUS, o evidencia contradictoria |
| Not supported | AFTER, exposición previa, COINCIDENCE, o arte previo anterior que hace la idea común en ese momento |

Formato del texto de conclusión (plantilla fija):
> "Una señal de JEGASolutions registrada el {fecha, nivel de evidencia} muestra convergencia {clasificación} con {evento}, observable públicamente desde {fecha}, {TTC} después. Conclusión: {Supported/...}. Explicaciones alternativas: {...}."

## 8. Caso de estudio TR-UX-001: TypeSafe (sec. 16 y 60.9)

### Qué se registra hoy

- **Señal TR-UX-001**, `is_retrospective = true`, `recorded_at` = fecha de carga.
- `original_text`: la descripción de Jaime **en sus propias palabras**, tal como la aporte, sin reformular. Propuesta de título: "Interfaz de alta densidad de información / reducción de scroll externo".
- `claimed_origin`: el intervalo que Jaime indique, con precisión y nivel E0 hasta que haya evidencia.
- **Evento externo EX-0001** "Sitio / interfaz de TypeSafe", estado `UNIDENTIFIED` (no sabemos aún cuál es el producto ni su URL), sin fecha.
- **Exposición**: cuándo vio Jaime TypeSafe por primera vez, declarado (E0) hasta que haya evidencia (historial del navegador, mensaje, captura).
- **Convergencia CV-0001** en estado `CANDIDATE`, relación `UNKNOWN`, sin puntuación.
- **Hipótesis HY-0001**: "Jaime adoptó de forma independiente un patrón de alta densidad / bajo scroll externo antes de observar una implementación comparable." (No: "Jaime inventó el patrón", ni "TypeSafe lo copió".)

### Protocolo de investigación

1. **Evidencia interna más temprana.** Revisar el historial git completo de `jegasolutions-platform` y otros repositorios (por ejemplo, documentos como `docs/arquitectura/NAVIGATION_IMPROVEMENTS.md`, cambios de layout en frontends), despliegues con fecha, capturas, mensajes a clientes. Para cada candidato: fecha de autor, fecha de push en GitHub, y diff o captura que muestre **el patrón concreto** (paneles con scroll interno, navegación persistente, contenido sin scroll de página).
2. **Definir el patrón con precisión** antes de comparar, para no ajustar la definición al resultado: por ejemplo, "la página principal no requiere scroll vertical del documento en 1440×900; el contenido extra se muestra en paneles con scroll interno, pestañas o secciones expandibles".
3. **Identificar TypeSafe** (URL aportada por Jaime) y establecer la fecha más temprana en que la interfaz relevante fue observable: changelog, anuncio, primera captura relevante en Wayback Machine.
4. **Arte previo.** El patrón de alta densidad sin scroll de página es antiguo y extendido: terminales financieras (Bloomberg), IDEs, clientes de correo de tres paneles, herramientas como Linear o Notion, principios de Tufte sobre densidad de datos. Hay que documentarlo. Esto casi seguro sitúa el patrón general como **MARKET_WIDE / preexistente**.
5. **Reformular la pregunta en función del arte previo.** Lo que sí puede evaluarse: (a) si Jaime adoptó el patrón antes de exponerse a TypeSafe, (b) si la implementación concreta de Jaime comparte rasgos específicos con la de TypeSafe más allá del patrón general, (c) si esa adopción fue temprana dentro de su contexto (por ejemplo, software B2B para pymes en Colombia).
6. **Comparar** con la rúbrica de [CONVERGENCE_MODEL.md](CONVERGENCE_MODEL.md), preferiblemente en modo ciego.
7. **Explicaciones alternativas** a registrar como mínimo: ambos responden a una tendencia preexistente (dashboards densos, herramientas tipo Linear); influencia indirecta de productos que ambos usaron; coincidencia estética sin relación funcional; recuerdo reconstruido de la fecha.
8. **Clasificar** y emitir conclusión con la plantilla de la sección 7.

### Resultado esperado más probable (hipótesis, no conclusión)

Dado el arte previo, la clasificación más probable del patrón general es MARKET_WIDE, y la conclusión sobre "anticipación del mercado" sería Not supported o Uncertain. La pregunta que sí puede llegar a Supported o Plausible es más acotada: **adopción independiente antes de la exposición a TypeSafe**. Esto se deja escrito ahora, antes de investigar, precisamente para no ajustar la expectativa después.

## 9. Casos semilla restantes

| Caso | Enfoque metodológico |
|---|---|
| TR-AI-001 (IA en JEGASolutions) | Evidencia E3 probable (pushes de GitHub del módulo de IA de report-builder). Buscar arte previo: herramientas de reportes con IA existían ampliamente. Probable MARKET_WIDE; interesa medir especificidad de rasgos concretos (p. ej. multi-proveedor, flujos de reportes para pymes) |
| TR-MUSIC-001 | Sin datos históricos verificables: no se evalúa retrospectivamente. Se inicia protocolo **prospectivo**: registrar álbum completo, pistas elegidas y fecha; medir después contra todas las pistas. Tasa base = proporción de pistas que se vuelven populares |
| TR-BIZ-001 | Estudio de contexto. Se usa como **caso control**: el crecimiento general del emprendimiento no es evidencia de predicción personal |
| TR-CUST-001 | Se valida con proyectos. Retrospectivamente solo cuenta si existe evidencia escrita del requisito inicial (correo, acta) anterior a la entrega. Prospectivamente, registrar `inferred_need` antes de cada entrega |

## 10. Integridad de la investigación (sec. 40)

El sistema y quien lo opera (incluida la IA) nunca:
- inventa fuentes, fechas ni evidencia;
- modifica predicciones históricas;
- afirma causalidad, copia o influencia a partir de similitud o correlación;
- afirma adopción de mercado a partir de un solo artículo o de discusión en redes;
- oculta hipótesis fallidas.

Toda URL registrada se consulta realmente; si no resuelve, no se registra como fuente. Si una fecha no puede verificarse, el campo queda vacío y el resultado es UNKNOWN.
