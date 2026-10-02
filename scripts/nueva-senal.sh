#!/usr/bin/env bash
# Crea una señal nueva (o una revisión) a partir de signals/_plantilla.md.
#   ./scripts/nueva-senal.sh UX "Título corto"
#   ./scripts/nueva-senal.sh --revision TR-UX-002
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
dir="$root/signals"
dominios="UX TECH AI BIZ CUST CULT MUSIC MKT"
hoy="$(date +%Y-%m-%d)"

escapar() { printf '%s' "$1" | sed 's/[&/\]/\\&/g'; }

uso() {
  echo "Uso: $0 DOMINIO \"Título\"" >&2
  echo "     $0 --revision TR-XX-NNN" >&2
  echo "Dominios: $dominios" >&2
  exit 1
}

[ $# -ge 1 ] || uso

if [ "$1" = "--revision" ]; then
  [ $# -eq 2 ] || uso
  original="$2"
  [ -f "$dir/$original.md" ] || { echo "No existe signals/$original.md" >&2; exit 1; }
  n=2
  while [ -f "$dir/$original.v$n.md" ]; do n=$((n + 1)); done
  destino="$dir/$original.v$n.md"
  titulo="$(escapar "$(sed -n 's/^titulo: //p' "$dir/$original.md" | head -1)")"
  dominio="$(sed -n 's/^dominio: //p' "$dir/$original.md" | head -1)"
  sed -e "s/^codigo: .*/codigo: $original/" \
      -e "s/^titulo: .*/titulo: $titulo/" \
      -e "s/^dominio: .*/dominio: $dominio/" \
      -e "s/^fecha_redaccion: .*/fecha_redaccion: $hoy/" \
      "$dir/_plantilla.md" \
    | awk -v n="$n" 'NR==2{print "version: " n; print "motivo_revision: "} {print}' > "$destino"
  echo "Creada revisión: signals/$(basename "$destino")"
  echo "Completa 'motivo_revision' y el texto. No edites signals/$original.md."
  exit 0
fi

[ $# -eq 2 ] || uso
dominio="$(echo "$1" | tr '[:lower:]' '[:upper:]')"
titulo="$2"
case " $dominios " in *" $dominio "*) ;; *) echo "Dominio inválido: $dominio" >&2; uso ;; esac

ultimo=0
for f in "$dir"/TR-"$dominio"-[0-9][0-9][0-9].md; do
  [ -e "$f" ] || continue
  num="$(basename "$f" .md)"; num="${num##*-}"
  num=$((10#$num))
  [ "$num" -gt "$ultimo" ] && ultimo="$num"
done
codigo="$(printf 'TR-%s-%03d' "$dominio" $((ultimo + 1)))"
destino="$dir/$codigo.md"

titulo_sed="$(escapar "$titulo")"
sed -e "s/^codigo: .*/codigo: $codigo/" \
    -e "s/^titulo: .*/titulo: $titulo_sed/" \
    -e "s/^dominio: .*/dominio: $dominio/" \
    -e "s/^fecha_redaccion: .*/fecha_redaccion: $hoy/" \
    "$dir/_plantilla.md" > "$destino"

echo "Creada: signals/$codigo.md"
