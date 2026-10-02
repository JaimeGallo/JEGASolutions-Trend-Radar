#!/usr/bin/env python3
"""Valida el registro de señales de la Fase 0.

Uso: python3 scripts/validar_senales.py [BASE_REF]

- Siempre: revisa formato y campos obligatorios de cada signals/TR-*.md.
- Con BASE_REF: además falla si entre BASE_REF y HEAD se modificó, renombró
  o eliminó una señal existente (las señales son inmutables).
"""
import re
import subprocess
import sys
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
DIR = RAIZ / "signals"
DOMINIOS = {"UX", "TECH", "AI", "BIZ", "CUST", "CULT", "MUSIC", "MKT"}
TIPOS_FUENTE = {
    "OBSERVATION", "PROJECT_DECISION", "CLIENT_INTERACTION", "PROTOTYPE", "COMMIT",
    "DOCUMENT", "CONVERSATION", "MARKET_OBSERVATION", "REJECTED_IDEA", "EXPERIMENT",
}
NOMBRE = re.compile(r"^(TR-([A-Z]+)-\d{3})(?:\.v(\d+))?\.md$")
OBLIGATORIOS = ["codigo", "titulo", "dominio", "tipo_fuente", "retrospectiva",
                "fecha_redaccion", "registrado_por"]


def leer_cabecera(texto):
    lineas = texto.splitlines()
    if not lineas or lineas[0].strip() != "---":
        return None
    campos = {}
    for linea in lineas[1:]:
        if linea.strip() == "---":
            return campos
        if ":" in linea:
            clave, valor = linea.split(":", 1)
            campos[clave.strip()] = valor.strip()
    return None


def validar_archivo(ruta):
    errores = []
    m = NOMBRE.match(ruta.name)
    if not m:
        return [f"{ruta.name}: nombre inválido (esperado TR-DOMINIO-NNN.md o TR-DOMINIO-NNN.vN.md)"]
    codigo, dominio_nombre, version = m.group(1), m.group(2), m.group(3)
    cab = leer_cabecera(ruta.read_text(encoding="utf-8"))
    if cab is None:
        return [f"{ruta.name}: falta la cabecera entre líneas '---'"]
    for campo in OBLIGATORIOS:
        if not cab.get(campo):
            errores.append(f"{ruta.name}: campo obligatorio vacío: {campo}")
    if cab.get("codigo") and cab["codigo"] != codigo:
        errores.append(f"{ruta.name}: 'codigo' ({cab['codigo']}) no coincide con el nombre del archivo")
    if dominio_nombre not in DOMINIOS:
        errores.append(f"{ruta.name}: dominio desconocido {dominio_nombre}")
    if cab.get("dominio") and cab["dominio"] != dominio_nombre:
        errores.append(f"{ruta.name}: 'dominio' no coincide con el código")
    if cab.get("tipo_fuente") and cab["tipo_fuente"] not in TIPOS_FUENTE:
        errores.append(f"{ruta.name}: tipo_fuente desconocido {cab['tipo_fuente']}")
    if cab.get("retrospectiva") not in (None, "", "si", "no"):
        errores.append(f"{ruta.name}: 'retrospectiva' debe ser si o no")
    if cab.get("fecha_redaccion") and not re.fullmatch(r"\d{4}-\d{2}-\d{2}", cab["fecha_redaccion"]):
        errores.append(f"{ruta.name}: fecha_redaccion debe ser AAAA-MM-DD")
    conf = cab.get("confianza", "")
    if conf and not (conf.isdigit() and 0 <= int(conf) <= 100):
        errores.append(f"{ruta.name}: confianza debe ser un entero de 0 a 100")
    if version:
        if not (DIR / f"{codigo}.md").exists():
            errores.append(f"{ruta.name}: revisión de una señal que no existe ({codigo}.md)")
        if cab.get("version") != version:
            errores.append(f"{ruta.name}: 'version' debe ser {version}")
        if not cab.get("motivo_revision"):
            errores.append(f"{ruta.name}: una revisión requiere 'motivo_revision'")
    return errores


def es_senal(ruta):
    p = Path(ruta)
    return p.parent.name == "signals" and NOMBRE.match(p.name) is not None


def validar_inmutabilidad(base):
    salida = subprocess.run(
        ["git", "diff", "--name-status", "-M", f"{base}...HEAD", "--", "signals/"],
        cwd=RAIZ, capture_output=True, text=True, check=True,
    ).stdout
    errores = []
    for linea in salida.splitlines():
        partes = linea.split("\t")
        estado, rutas = partes[0], partes[1:]
        if estado == "A":
            continue
        afectadas = [r for r in rutas if es_senal(r)]
        if afectadas:
            accion = {"M": "modificó", "D": "eliminó"}.get(estado[0], "renombró o alteró")
            errores.append(f"Se {accion} una señal existente: {', '.join(afectadas)}. "
                           "Las señales son inmutables; crea una revisión con "
                           "./scripts/nueva-senal.sh --revision CODIGO")
    return errores


def main():
    errores = []
    for ruta in sorted(DIR.glob("TR-*.md")):
        errores += validar_archivo(ruta)
    if len(sys.argv) > 1:
        errores += validar_inmutabilidad(sys.argv[1])
    if errores:
        print("Registro de señales inválido:")
        for e in errores:
            print(f"  - {e}")
        return 1
    print(f"OK: {len(list(DIR.glob('TR-*.md')))} señales válidas.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
