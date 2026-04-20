#!/usr/bin/env bash
# rebucket-pdfs-s3.sh
#
# Migra i PDF dal filesystem locale (volume Docker `meepleai_pdf_uploads`)
# allo storage S3/R2 configurato via `STORAGE_PROVIDER=s3`.
#
# Wrapper CLI per l'endpoint POST /api/v1/admin/storage/migrate (MigrateStorageCommand).
# Idempotente: skip dei file già presenti su S3 (check ExistsAsync in handler).
#
# Target d'uso: retro-migrazione dei 113 PDF seedati in staging (batch 2026-04-15)
# dal filesystem locale a Cloudflare R2, senza downtime né modifiche al layout chiavi.
#
# Layout chiavi S3 (NON cambia): pdf_uploads/{gameId}/{fileId}_{filename}
#
# Prerequisiti:
#   - curl, jq
#   - Credenziali admin (email+password) OPPURE cookie di sessione pre-autenticato
#   - STORAGE_PROVIDER=s3 attivo sul target (altrimenti l'endpoint risponde con errore)
#
# Uso:
#   scripts/rebucket-pdfs-s3.sh --env local                      # dry-run locale
#   scripts/rebucket-pdfs-s3.sh --env staging --execute          # esecuzione staging
#   ADMIN_EMAIL=... ADMIN_PASSWORD=... scripts/rebucket-pdfs-s3.sh --env staging
#
# Exit codes:
#   0 = success (dry-run o execute completato senza errori)
#   1 = errore input/args
#   2 = login fallito
#   3 = errore API (endpoint migration)
#   4 = abort utente alla conferma
#   5 = tool mancante (curl/jq)

set -euo pipefail

# ---------- Colori ----------
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

print_info()    { echo -e "${BLUE}[INFO]${NC} $*"; }
print_ok()      { echo -e "${GREEN}[OK]${NC} $*"; }
print_warn()    { echo -e "${YELLOW}[WARN]${NC} $*"; }
print_error()   { echo -e "${RED}[ERROR]${NC} $*" >&2; }

# ---------- Default ----------
ENVIRONMENT="local"
EXECUTE=0
EMAIL="${ADMIN_EMAIL:-}"
PASSWORD="${ADMIN_PASSWORD:-}"
SESSION_COOKIE="${ADMIN_SESSION_COOKIE:-}"

# ---------- Parse args ----------
usage() {
    cat <<EOF
Usage: $0 [options]

Options:
  --env <local|staging>    Target environment (default: local)
  --execute                Eseguire la migrazione (default: solo dry-run)
  --email <admin@...>      Email admin (oppure env ADMIN_EMAIL)
  --password <secret>      Password admin (oppure env ADMIN_PASSWORD)
  --session-cookie <val>   Cookie sessione pre-autenticato (oppure env ADMIN_SESSION_COOKIE)
  -h, --help               Mostra questo help

Esempi:
  $0 --env local                                       # Dry-run locale
  ADMIN_EMAIL=a@b.c ADMIN_PASSWORD=xxx $0 --env staging --execute
  ADMIN_SESSION_COOKIE="meepleai.sid=abc" $0 --env staging
EOF
}

while [[ $# -gt 0 ]]; do
    case "$1" in
        --env)               ENVIRONMENT="$2"; shift 2 ;;
        --execute)           EXECUTE=1; shift ;;
        --email)             EMAIL="$2"; shift 2 ;;
        --password)          PASSWORD="$2"; shift 2 ;;
        --session-cookie)    SESSION_COOKIE="$2"; shift 2 ;;
        -h|--help)           usage; exit 0 ;;
        *)                   print_error "Argomento sconosciuto: $1"; usage; exit 1 ;;
    esac
done

# ---------- Tool check ----------
for tool in curl jq; do
    if ! command -v "$tool" >/dev/null 2>&1; then
        print_error "Tool richiesto mancante: $tool"
        exit 5
    fi
done

# ---------- Resolve URL ----------
case "$ENVIRONMENT" in
    local)   API_URL="http://localhost:8080/api/v1" ;;
    staging) API_URL="https://api.meepleai.app/api/v1" ;;
    *)       print_error "Env non valido: $ENVIRONMENT (usa local|staging)"; exit 1 ;;
esac

print_info "Target: $ENVIRONMENT ($API_URL)"
print_info "Modalità: $([ $EXECUTE -eq 1 ] && echo 'EXECUTE' || echo 'DRY-RUN')"

# ---------- Auth ----------
COOKIE_JAR="$(mktemp)"
trap 'rm -f "$COOKIE_JAR"' EXIT

if [[ -n "$SESSION_COOKIE" ]]; then
    print_info "Uso session cookie pre-autenticato"
    # Scrive il cookie nel formato Netscape per curl
    echo -e "# Netscape HTTP Cookie File\n" > "$COOKIE_JAR"
    # Il cookie passato è trattato come raw e inviato via -b
    AUTH_CURL_ARGS=(-b "$SESSION_COOKIE")
else
    if [[ -z "$EMAIL" || -z "$PASSWORD" ]]; then
        print_error "Serve --email + --password (o ADMIN_EMAIL/ADMIN_PASSWORD), oppure --session-cookie"
        exit 1
    fi

    print_info "Login admin come $EMAIL..."
    LOGIN_PAYLOAD=$(jq -n --arg e "$EMAIL" --arg p "$PASSWORD" '{email:$e, password:$p}')

    LOGIN_RESPONSE=$(curl -sS -c "$COOKIE_JAR" \
        -H "Content-Type: application/json" \
        -w "\n%{http_code}" \
        -X POST "$API_URL/auth/login" \
        -d "$LOGIN_PAYLOAD" || true)

    HTTP_CODE=$(echo "$LOGIN_RESPONSE" | tail -n1)
    BODY=$(echo "$LOGIN_RESPONSE" | sed '$d')

    if [[ "$HTTP_CODE" != "200" ]]; then
        print_error "Login fallito (HTTP $HTTP_CODE): $BODY"
        exit 2
    fi

    if echo "$BODY" | jq -e '.requiresTwoFactor // false' >/dev/null 2>&1; then
        if [[ $(echo "$BODY" | jq -r '.requiresTwoFactor') == "true" ]]; then
            print_error "Account richiede 2FA — usa --session-cookie di un utente admin dedicato allo scripting"
            exit 2
        fi
    fi

    print_ok "Login riuscito"
    AUTH_CURL_ARGS=(-b "$COOKIE_JAR")
fi

# ---------- Migration call ----------
call_migrate() {
    local dry_run="$1"
    local url="$API_URL/admin/storage/migrate?dryRun=$dry_run"

    print_info "POST $url"

    local response
    response=$(curl -sS "${AUTH_CURL_ARGS[@]}" \
        -H "Content-Type: application/json" \
        -w "\n%{http_code}" \
        -X POST "$url" || true)

    local http_code
    http_code=$(echo "$response" | tail -n1)
    local body
    body=$(echo "$response" | sed '$d')

    if [[ "$http_code" != "200" ]]; then
        print_error "Migration call fallita (HTTP $http_code): $body"
        exit 3
    fi

    echo "$body"
}

print_report() {
    local body="$1"
    local label="$2"

    local total migrated skipped failed dry size errors_count
    total=$(echo "$body" | jq -r '.totalFiles')
    migrated=$(echo "$body" | jq -r '.migrated')
    skipped=$(echo "$body" | jq -r '.skipped')
    failed=$(echo "$body" | jq -r '.failed')
    dry=$(echo "$body" | jq -r '.isDryRun')
    size=$(echo "$body" | jq -r '.totalSizeBytes')
    errors_count=$(echo "$body" | jq -r '.errors | length')

    echo ""
    echo "=========================================="
    echo "  REPORT: $label"
    echo "=========================================="
    echo "  Dry-run:        $dry"
    echo "  Total files:    $total"
    echo "  Migrated:       $migrated"
    echo "  Skipped:        $skipped  (già su S3 o struttura path non valida)"
    echo "  Failed:         $failed"
    echo "  Total size:     $(numfmt --to=iec-i --suffix=B "$size" 2>/dev/null || echo "$size bytes")"
    echo "  Errors logged:  $errors_count"
    echo "=========================================="

    if [[ "$errors_count" -gt 0 ]]; then
        print_warn "Primi 5 errori:"
        echo "$body" | jq -r '.errors[:5][]' | sed 's/^/    - /'
    fi
}

# ---------- Step 1: dry-run sempre ----------
print_info "Step 1/2: dry-run per preview"
DRYRUN_BODY=$(call_migrate "true")
print_report "$DRYRUN_BODY" "DRY-RUN"

TOTAL_FILES=$(echo "$DRYRUN_BODY" | jq -r '.totalFiles')
FAILED_COUNT=$(echo "$DRYRUN_BODY" | jq -r '.failed')

if [[ "$TOTAL_FILES" -eq 0 ]]; then
    print_warn "Nessun file trovato nel filesystem locale. Niente da migrare."
    exit 0
fi

# ---------- Step 2: execute (se richiesto) ----------
if [[ $EXECUTE -eq 0 ]]; then
    print_info "Modalità dry-run: nessuna modifica apportata. Usa --execute per eseguire la migrazione."
    exit 0
fi

# Avvertimento se dry-run ha fallimenti
if [[ "$FAILED_COUNT" -gt 0 ]]; then
    print_warn "Dry-run ha rilevato $FAILED_COUNT fallimenti. Rivedi gli errori prima di procedere."
fi

echo ""
read -r -p "Confermi esecuzione su $ENVIRONMENT ($TOTAL_FILES file)? [y/N] " confirm
if [[ ! "$confirm" =~ ^[Yy]$ ]]; then
    print_warn "Annullato dall'utente."
    exit 4
fi

print_info "Step 2/2: esecuzione migrazione"
EXEC_BODY=$(call_migrate "false")
print_report "$EXEC_BODY" "EXECUTE"

FINAL_FAILED=$(echo "$EXEC_BODY" | jq -r '.failed')
if [[ "$FINAL_FAILED" -gt 0 ]]; then
    print_error "Migrazione completata con $FINAL_FAILED fallimenti"
    exit 3
fi

print_ok "Migrazione completata con successo."
