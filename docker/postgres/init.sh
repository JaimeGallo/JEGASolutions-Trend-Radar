#!/bin/sh
# Crea el rol de aplicación (sin permisos de modificación sobre tablas históricas;
# los permisos los asigna la migración IntegrityGuards). Solo corre al crear el volumen.
set -eu
psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname "$POSTGRES_DB" \
  -v app_password="$TRENDRADAR_APP_PASSWORD" <<'EOSQL'
CREATE ROLE trendradar_app LOGIN PASSWORD :'app_password';
EOSQL
