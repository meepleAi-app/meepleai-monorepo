#!/bin/sh
# Attende gli emulatori e semina l'App Configuration emulator. Esce 0 solo quando tutto e' pronto:
# `api` dipende da questo container con condition: service_completed_successfully.
set -eu

wait_http() { # $1 url, $2 descrizione, $3 accetta-qualsiasi-status (1) o solo 2xx (0)
  i=0
  while [ "$i" -lt 120 ]; do
    if [ "$3" = "1" ]; then
      curl -s -o /dev/null "$1" && { echo "ready: $2"; return 0; }
    else
      curl -fsS -o /dev/null "$1" && { echo "ready: $2"; return 0; }
    fi
    i=$((i + 1)); sleep 2
  done
  echo "timeout waiting for $2 ($1)" >&2; return 1
}

wait_http "http://azurite:10000/" "azurite blob" 1          # risponde 400 senza auth: basta la connessione
wait_http "http://appconfig-emulator:8483/health" "app configuration emulator" 0
wait_http "http://servicebus-emulator:5300/health" "service bus emulator" 0

# Seed: un PUT anonimo per chiave (api-version 2023-11-01); il file e' una mappa piatta chiave->valore.
sed -n 's/^ *"\([^"]*\)": *"\([^"]*\)".*/\1\t\2/p' /seed.json | while IFS="$(printf '\t')" read -r key value; do
  encoded=$(printf '%s' "$key" | sed 's/:/%3A/g')
  curl -fsS -o /dev/null -X PUT "http://appconfig-emulator:8483/kv/$encoded?api-version=2023-11-01" \
    -H "Content-Type: application/vnd.microsoft.appconfig.kv+json" \
    -d "{\"value\":\"$value\"}"
  echo "seeded $key"
done
