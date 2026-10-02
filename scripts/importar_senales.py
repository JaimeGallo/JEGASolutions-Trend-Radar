#!/usr/bin/env python3
"""Importa las señales de la Fase 0 (signals/TR-*.md) a Trend Radar.

Uso:
    TR_EMAIL=... TR_PASSWORD=... python3 scripts/importar_senales.py [--api http://localhost:8080] [--dry-run]

Para cada señal:
- la registra con su código original (TR-UX-001, ...), sin renumerar;
- adjunta el archivo .md como evidencia de ORIGEN, con la fecha del commit que lo agregó
  (fecha de autor de git: nivel E2). Su SHA-256 es el mismo que sella el .ots;
- adjunta el sello .ots, si existe, como evidencia sin fecha (E0) hasta verificarlo;
- importa las revisiones TR-XX-NNN.vN.md como versiones nuevas.

Es idempotente: las señales que ya existen se omiten. Debe ejecutarse antes de registrar
señales nuevas en la aplicación, para que los códigos no choquen.
"""
import argparse
import json
import mimetypes
import os
import re
import subprocess
import sys
import urllib.error
import urllib.request
import uuid
from pathlib import Path

RAIZ = Path(__file__).resolve().parent.parent
DIR = RAIZ / "signals"
REPO = "JaimeGallo/JEGASolutions-Trend-Radar"
NOMBRE = re.compile(r"^(TR-([A-Z]+)-\d{3,})(?:\.v(\d+))?\.md$")
COMENTARIO = re.compile(r"<!--.*?-->", re.DOTALL)


class Api:
    def __init__(self, base, dry_run):
        self.base = base.rstrip("/")
        self.token = None
        self.dry_run = dry_run

    def request(self, method, path, body=None, form=None, ok=(200, 201)):
        headers = {}
        data = None
        if self.token:
            headers["Authorization"] = f"Bearer {self.token}"
        if body is not None:
            data = json.dumps(body).encode()
            headers["Content-Type"] = "application/json"
        if form is not None:
            data, content_type = multipart(form)
            headers["Content-Type"] = content_type
        req = urllib.request.Request(self.base + path, data=data, method=method, headers=headers)
        try:
            with urllib.request.urlopen(req) as res:
                return res.status, json.loads(res.read() or b"null")
        except urllib.error.HTTPError as e:
            detail = e.read().decode(errors="replace")
            if e.code in ok:
                return e.code, None
            try:
                detail = json.loads(detail).get("title", detail)
            except ValueError:
                pass
            return e.code, detail

    def login(self, email, password):
        status, body = self.request("POST", "/api/auth/login", {"email": email, "password": password})
        if status != 200:
            sys.exit(f"No se pudo iniciar sesión: {body}")
        self.token = body["accessToken"]


def multipart(fields):
    boundary = uuid.uuid4().hex
    parts = []
    for name, value in fields.items():
        if isinstance(value, Path):
            mime = "text/markdown" if value.suffix == ".md" else mimetypes.guess_type(value.name)[0] or "application/octet-stream"
            parts.append(
                f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"; filename="{value.name}"\r\n'
                f"Content-Type: {mime}\r\n\r\n".encode() + value.read_bytes() + b"\r\n")
        elif value is not None:
            parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{name}"\r\n\r\n{value}\r\n'.encode())
    parts.append(f"--{boundary}--\r\n".encode())
    return b"".join(parts), f"multipart/form-data; boundary={boundary}"


def leer(ruta):
    texto = ruta.read_text(encoding="utf-8")
    lineas = texto.splitlines()
    cabecera, cuerpo = {}, ""
    if lineas and lineas[0].strip() == "---":
        for i, linea in enumerate(lineas[1:], start=1):
            if linea.strip() == "---":
                cuerpo = "\n".join(lineas[i + 1:])
                break
            if ":" in linea:
                clave, valor = linea.split(":", 1)
                cabecera[clave.strip()] = valor.strip()
    secciones, actual = {}, None
    for linea in COMENTARIO.sub("", cuerpo).splitlines():
        if linea.startswith("## "):
            actual = linea[3:].strip().lower()
            secciones[actual] = []
        elif actual:
            secciones[actual].append(linea)
    secciones = {k: "\n".join(v).strip() for k, v in secciones.items()}
    return cabecera, secciones, COMENTARIO.sub("", cuerpo).strip()


def tipo_fuente(valor):
    # OBSERVATION -> Observation, PROJECT_DECISION -> ProjectDecision
    return "".join(p.capitalize() for p in (valor or "OBSERVATION").split("_"))


def commit_que_agrego(ruta):
    salida = subprocess.run(
        ["git", "log", "--diff-filter=A", "--follow", "--format=%H %aI", "--", str(ruta.relative_to(RAIZ))],
        cwd=RAIZ, capture_output=True, text=True, check=True).stdout.strip().splitlines()
    if not salida:
        return None, None
    sha, fecha = salida[-1].split(" ", 1)
    return sha, fecha


def importar(api, ruta, codigo, dominio):
    cab, sec, cuerpo = leer(ruta)
    status, _ = api.request("GET", f"/api/signals/{codigo}", ok=(404,))
    if status == 200:
        print(f"  {codigo}: ya existe, se omite")
        return False

    texto = sec.get("texto original") or cuerpo
    otras = {k: v for k, v in sec.items() if k not in ("texto original", "contexto") and v}
    if otras:
        # Evidencia, hipótesis y predicción de la Fase 0 se conservan como parte del texto original.
        texto += "\n\n" + "\n\n".join(f"## {k.capitalize()}\n{v}" for k, v in otras.items())
    confianza = cab.get("confianza")
    cuerpo_import = {
        "code": codigo,
        "title": cab.get("titulo") or codigo,
        "text": texto,
        "domainCode": dominio,
        "context": sec.get("contexto") or None,
        "confidence": int(confianza) if confianza and confianza.isdigit() else None,
        "sourceType": tipo_fuente(cab.get("tipo_fuente")),
        "retrospective": cab.get("retrospectiva") == "si",
        "claimedOrigin": cab.get("origen_declarado") or None,
        "importedFrom": f"signals/{ruta.name}",
    }
    sha, fecha = commit_que_agrego(ruta)
    if api.dry_run:
        print(f"  {codigo}: se importaría (commit {sha[:12] if sha else 'sin commit'} {fecha or ''})")
        return True

    status, body = api.request("POST", "/api/signals/import", cuerpo_import)
    if status != 201:
        sys.exit(f"  {codigo}: error al importar: {body}")
    print(f"  {codigo}: importada")

    descripcion = f"Archivo original de la Fase 0 ({ruta.name})"
    if sha:
        descripcion += f", agregado en el commit {sha[:12]} de {REPO}"
    estado, cuerpo_resp = api.request("POST", f"/api/signals/{codigo}/evidence/file", form={
        "file": ruta,
        "role": "Origin",
        "description": descripcion + ". Su SHA-256 es el que certifica el sello .ots.",
        "artifactTimestamp": fecha,
        "timestampAuthority": "GitAuthor" if fecha else "None",
    })
    if estado != 200:
        sys.exit(f"  {codigo}: error al adjuntar el archivo: {cuerpo_resp}")

    ots = ruta.with_name(ruta.name + ".ots")
    if ots.exists():
        estado, cuerpo_resp = api.request("POST", f"/api/signals/{codigo}/evidence/file", form={
            "file": ots,
            "role": "Origin",
            "description": f"Sello OpenTimestamps de {ruta.name}. Verificar con 'ots verify'; "
                           "una vez anclado en Bitcoin, registrar su fecha como evidencia E4.",
            "timestampAuthority": "None",
        })
        if estado != 200:
            sys.exit(f"  {codigo}: error al adjuntar el sello: {cuerpo_resp}")
    return True


def importar_revision(api, ruta, codigo, version):
    cab, sec, cuerpo = leer(ruta)
    status, detalle = api.request("GET", f"/api/signals/{codigo}")
    if status != 200:
        sys.exit(f"  {ruta.name}: la señal {codigo} no existe")
    motivo = f"{cab.get('motivo_revision', '')} (revisión v{version} de la Fase 0: {ruta.name})"
    if any(ruta.name in v["changeReason"] for v in detalle["versions"]):
        print(f"  {ruta.name}: ya importada, se omite")
        return
    if api.dry_run:
        print(f"  {ruta.name}: se importaría como versión nueva")
        return
    confianza = cab.get("confianza")
    status, body = api.request("POST", f"/api/signals/{codigo}/versions", {
        "title": cab.get("titulo") or codigo,
        "text": sec.get("texto original") or cuerpo,
        "context": sec.get("contexto") or None,
        "confidence": int(confianza) if confianza and confianza.isdigit() else None,
        "reason": motivo,
    })
    if status != 200:
        sys.exit(f"  {ruta.name}: error: {body}")
    print(f"  {ruta.name}: importada como versión nueva")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--api", default=os.environ.get("TR_API", "http://localhost:8080"))
    parser.add_argument("--dry-run", action="store_true", help="Muestra qué se importaría sin escribir nada")
    args = parser.parse_args()

    email, password = os.environ.get("TR_EMAIL"), os.environ.get("TR_PASSWORD")
    if not email or not password:
        sys.exit("Define TR_EMAIL y TR_PASSWORD (la cuenta de propietario).")

    api = Api(args.api, args.dry_run)
    api.login(email, password)

    archivos = sorted(p for p in DIR.glob("TR-*.md") if NOMBRE.match(p.name))
    originales = [p for p in archivos if not NOMBRE.match(p.name).group(3)]
    revisiones = sorted((p for p in archivos if NOMBRE.match(p.name).group(3)),
                        key=lambda p: int(NOMBRE.match(p.name).group(3)))
    print(f"Señales: {len(originales)} · revisiones: {len(revisiones)}")
    nuevas = sum(importar(api, p, *NOMBRE.match(p.name).group(1, 2)) for p in originales)
    for p in revisiones:
        m = NOMBRE.match(p.name)
        importar_revision(api, p, m.group(1), m.group(3))
    print(f"Listo: {nuevas} señales {'por importar' if args.dry_run else 'importadas'}.")


if __name__ == "__main__":
    main()
