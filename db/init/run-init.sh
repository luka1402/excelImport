#!/bin/bash
# Idempotentna inicijalizacija DemoDb; bezbedno za ponovno pokretanje.
set -euo pipefail

SQLCMD=/opt/mssql-tools18/bin/sqlcmd
run() { "$SQLCMD" -C -f 65001 -S mssql -U sa -P "$MSSQL_SA_PASSWORD" -b "$@"; }

run -i /init/01-create-demodb.sql
run -d DemoDb -i /init/02-schema.sql
run -d DemoDb -i /init/03-seed.sql

if [ -n "${DEMO_APP_PASSWORD:-}" ]; then
  run -d DemoDb -i /init/04-app-login.sql \
    -v AppLogin="$DEMO_APP_LOGIN" AppPassword="$DEMO_APP_PASSWORD"
else
  echo "DEMO_APP_PASSWORD nije postavljen - preskacem kreiranje app logina."
fi
echo "DemoDb inicijalizovan."