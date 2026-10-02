# Registro de señales (Fase 0)

Registro mínimo de señales mientras se construye la aplicación. Cada señal es un archivo Markdown. Cuando exista el MVP, se importarán todas con su evidencia de fecha.

## Por qué así

El valor del Trend Radar depende de demostrar **cuándo** existía una idea. Cada señal recibe tres sellos de tiempo independientes:

| Sello | Quién lo pone | Nivel (ver `docs/RESEARCH_METHODOLOGY.md`) |
|---|---|---|
| `fecha_redaccion` en el archivo | Tú | E1: declarado |
| Fecha del commit en git | Tu equipo (se puede alterar) | E2 |
| Recepción del push en GitHub | GitHub | E3 |
| Archivo `.ots` de [OpenTimestamps](https://opentimestamps.org) | Cadena de bloques de Bitcoin, vía GitHub Actions | E4: prueba criptográfica de que el archivo existía en esa fecha, sin revelar su contenido |

El archivo `.ots` se genera automáticamente al hacer push y se completa (anclaje en Bitcoin) con un proceso semanal. Cualquier cambio posterior al archivo de la señal invalida su sello. Por eso **las señales no se editan**.

## Cómo registrar una señal

```bash
git pull                                   # trae los sellos .ots que agregó GitHub Actions
./scripts/nueva-senal.sh UX "Título corto"
# edita el archivo creado en signals/ (solo ese, ahora mismo)
git add signals/ && git commit -m "Señal TR-UX-002" && git push
```

Dominios: `UX`, `TECH`, `AI`, `BIZ`, `CUST`, `CULT`, `MUSIC`, `MKT` (ver `docs/DATA_MODEL.md`).

Recomendaciones:
- **Registra todo**, también lo que parece trivial o lo que podría salir mal. Las métricas necesitan el denominador completo; registrar solo lo que "suena bien" invalida el análisis.
- Escribe con tus palabras, sin pulir. La redacción original es la evidencia.
- Llena la sección "Predicción" solo si puedes decir cómo se comprobaría y para cuándo.
- Si la idea es anterior a hoy, marca `retrospectiva: si` y escribe en `origen_declarado` desde cuándo crees que la tenías (por ejemplo `2025-03`, `2025-Q1` o `2025 aprox`).

## Reglas de inmutabilidad

1. Una vez subida, una señal **no se modifica ni se borra**. El workflow `Señales` falla si un push modifica o elimina un archivo `TR-*.md` existente.
2. Si cambias de opinión, crea una **revisión**: `./scripts/nueva-senal.sh --revision TR-UX-002`. Se crea `TR-UX-002.v2.md` que referencia la original; ambas se conservan.
3. Los errores de digitación también se corrigen con una revisión. Es preferible un error visible a una historia editada.

## Archivos

| Archivo | Contenido |
|---|---|
| `_plantilla.md` | Plantilla base |
| `TR-XX-NNN.md` | Señal original |
| `TR-XX-NNN.vN.md` | Revisión N de una señal |
| `*.md.ots` | Sello de OpenTimestamps del archivo correspondiente |

Para verificar un sello manualmente: `pip install opentimestamps-client` y luego `ots verify signals/TR-UX-001.md.ots`.

## Casos semilla

`TR-UX-001`, `TR-AI-001`, `TR-MUSIC-001`, `TR-BIZ-001` y `TR-CUST-001` son los casos de la sección 41 de la especificación v2.0. Su texto se tomó literalmente de esa especificación. Son **retrospectivos** y el origen declarado está pendiente: el sello de hoy solo demuestra cuándo se registró la afirmación, no cuándo existió la idea.
