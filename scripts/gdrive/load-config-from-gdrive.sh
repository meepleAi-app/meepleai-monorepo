#!/bin/bash
# =============================================================================
# MeepleAI - Load Configuration from Google Drive
# =============================================================================
# Script per scaricare la configurazione da una cartella condivisa Google Drive
#
# Prerequisiti:
#   - pip install gdown
#   - Cartella Google Drive con accesso "Chiunque con il link"
#
# Uso:
#   ./scripts/gdrive/load-config-from-gdrive.sh [FOLDER_ID]
#
# =============================================================================

set -e

# Colori per output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Directory di lavoro
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
TEMP_DIR="$PROJECT_ROOT/.config-temp"

# ID della cartella Google Drive (può essere passato come argomento)
FOLDER_ID="${1:-YOUR_FOLDER_ID_HERE}"

echo -e "${BLUE}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${BLUE}║     MeepleAI - Configuration Loader from Google Drive      ║${NC}"
echo -e "${BLUE}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Verifica prerequisiti
check_prerequisites() {
    echo -e "${YELLOW}🔍 Verificando prerequisiti...${NC}"

    if ! command -v python3 &> /dev/null; then
        echo -e "${RED}❌ Python3 non trovato. Installalo prima di procedere.${NC}"
        exit 1
    fi

    if ! python3 -c "import gdown" 2>/dev/null; then
        echo -e "${YELLOW}📦 Installando gdown...${NC}"
        pip install gdown --quiet
    fi

    echo -e "${GREEN}✅ Prerequisiti verificati${NC}"
}

# Verifica FOLDER_ID
validate_folder_id() {
    if [ "$FOLDER_ID" == "YOUR_FOLDER_ID_HERE" ]; then
        echo -e "${RED}❌ FOLDER_ID non configurato!${NC}"
        echo ""
        echo -e "Modifica questo script e imposta FOLDER_ID, oppure passalo come argomento:"
        echo -e "  ${YELLOW}./scripts/gdrive/load-config-from-gdrive.sh 1ABC123xyz${NC}"
        echo ""
        echo -e "Per ottenere il FOLDER_ID:"
        echo -e "  1. Apri la cartella su Google Drive"
        echo -e "  2. L'URL sarà: https://drive.google.com/drive/folders/${YELLOW}FOLDER_ID${NC}"
        echo -e "  3. Copia l'ID dalla URL"
        exit 1
    fi

    echo -e "${GREEN}✅ FOLDER_ID: ${FOLDER_ID:0:8}...${NC}"
}

# Scarica da Google Drive
download_from_gdrive() {
    echo -e "${YELLOW}📥 Scaricando configurazione da Google Drive...${NC}"

    # Pulisci directory temporanea
    rm -rf "$TEMP_DIR"
    mkdir -p "$TEMP_DIR"

    # Scarica la cartella
    cd "$TEMP_DIR"
    gdown --folder "https://drive.google.com/drive/folders/$FOLDER_ID" -O . --quiet
    cd "$PROJECT_ROOT"

    echo -e "${GREEN}✅ Download completato${NC}"
}

# Applica configurazione
apply_configuration() {
    echo -e "${YELLOW}🔧 Applicando configurazione...${NC}"

    # Trova la sottocartella (gdown crea una cartella con il nome originale)
    CONFIG_DIR=$(find "$TEMP_DIR" -maxdepth 1 -type d ! -name ".config-temp" | head -1)
    if [ -z "$CONFIG_DIR" ] || [ "$CONFIG_DIR" == "$TEMP_DIR" ]; then
        CONFIG_DIR="$TEMP_DIR"
    fi

    # .env.local
    if [ -f "$CONFIG_DIR/.env.local" ]; then
        if [ -f "$PROJECT_ROOT/.env.local" ]; then
            cp "$PROJECT_ROOT/.env.local" "$PROJECT_ROOT/.env.local.backup.$(date +%Y%m%d_%H%M%S)"
            echo -e "  ${YELLOW}⚠️  Backup creato: .env.local.backup.*${NC}"
        fi
        cp "$CONFIG_DIR/.env.local" "$PROJECT_ROOT/.env.local"
        echo -e "  ${GREEN}✅ .env.local installato${NC}"
    else
        echo -e "  ${YELLOW}⚠️  .env.local non trovato nel download${NC}"
    fi

    # .env.staging (opzionale)
    if [ -f "$CONFIG_DIR/.env.staging" ]; then
        cp "$CONFIG_DIR/.env.staging" "$PROJECT_ROOT/.env.staging"
        echo -e "  ${GREEN}✅ .env.staging installato${NC}"
    fi

    # Secrets
    if [ -d "$CONFIG_DIR/secrets" ]; then
        mkdir -p "$PROJECT_ROOT/infra/secrets"

        for secret_file in "$CONFIG_DIR/secrets/"*.txt; do
            if [ -f "$secret_file" ]; then
                filename=$(basename "$secret_file")
                cp "$secret_file" "$PROJECT_ROOT/infra/secrets/$filename"
                chmod 600 "$PROJECT_ROOT/infra/secrets/$filename"
                echo -e "  ${GREEN}✅ Secret: $filename${NC}"
            fi
        done
    else
        echo -e "  ${YELLOW}⚠️  Directory secrets non trovata${NC}"
    fi

    # OAuth credentials (opzionale)
    if [ -f "$CONFIG_DIR/oauth-credentials.json" ]; then
        cp "$CONFIG_DIR/oauth-credentials.json" "$PROJECT_ROOT/infra/oauth-credentials.json"
        echo -e "  ${GREEN}✅ oauth-credentials.json installato${NC}"
    fi
}

# Cleanup
cleanup() {
    echo -e "${YELLOW}🧹 Pulizia file temporanei...${NC}"
    rm -rf "$TEMP_DIR"
    echo -e "${GREEN}✅ Pulizia completata${NC}"
}

# Validazione finale
validate_installation() {
    echo -e "${YELLOW}🔍 Validando installazione...${NC}"

    local errors=0

    # Verifica .env.local
    if [ -f "$PROJECT_ROOT/.env.local" ]; then
        echo -e "  ${GREEN}✓ .env.local presente${NC}"

        # Verifica variabili critiche
        if grep -q "OPENROUTER_API_KEY=sk-" "$PROJECT_ROOT/.env.local" 2>/dev/null; then
            echo -e "  ${GREEN}✓ OPENROUTER_API_KEY configurato${NC}"
        else
            echo -e "  ${RED}✗ OPENROUTER_API_KEY mancante o vuoto${NC}"
            errors=$((errors + 1))
        fi

        if grep -q "POSTGRES_PASSWORD=" "$PROJECT_ROOT/.env.local" 2>/dev/null; then
            echo -e "  ${GREEN}✓ POSTGRES_PASSWORD presente${NC}"
        else
            echo -e "  ${RED}✗ POSTGRES_PASSWORD mancante${NC}"
            errors=$((errors + 1))
        fi
    else
        echo -e "  ${RED}✗ .env.local non trovato${NC}"
        errors=$((errors + 1))
    fi

    # Verifica secrets directory
    if [ -d "$PROJECT_ROOT/infra/secrets" ]; then
        local secret_count=$(ls -1 "$PROJECT_ROOT/infra/secrets/"*.txt 2>/dev/null | wc -l)
        if [ "$secret_count" -gt 0 ]; then
            echo -e "  ${GREEN}✓ $secret_count secrets installati${NC}"
        else
            echo -e "  ${YELLOW}⚠ Nessun secret trovato${NC}"
        fi
    fi

    if [ $errors -gt 0 ]; then
        echo -e "${RED}⚠️  Trovati $errors problemi di configurazione${NC}"
        return 1
    fi

    echo -e "${GREEN}✅ Validazione completata con successo${NC}"
    return 0
}

# Mostra riepilogo
show_summary() {
    echo ""
    echo -e "${BLUE}╔════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║                    RIEPILOGO                               ║${NC}"
    echo -e "${BLUE}╚════════════════════════════════════════════════════════════╝${NC}"
    echo ""
    echo -e "File installati:"
    [ -f "$PROJECT_ROOT/.env.local" ] && echo -e "  ${GREEN}•${NC} .env.local"
    [ -f "$PROJECT_ROOT/.env.staging" ] && echo -e "  ${GREEN}•${NC} .env.staging"
    [ -d "$PROJECT_ROOT/infra/secrets" ] && echo -e "  ${GREEN}•${NC} infra/secrets/ ($(ls -1 "$PROJECT_ROOT/infra/secrets/"*.txt 2>/dev/null | wc -l) files)"
    echo ""
    echo -e "${YELLOW}Prossimi passi:${NC}"
    echo -e "  1. Verifica .env.local: ${BLUE}cat .env.local | head -20${NC}"
    echo -e "  2. Avvia i servizi: ${BLUE}cd infra && docker compose up -d${NC}"
    echo -e "  3. Verifica stato: ${BLUE}docker compose ps${NC}"
    echo ""
}

# Main
main() {
    check_prerequisites
    validate_folder_id
    download_from_gdrive
    apply_configuration
    cleanup

    if validate_installation; then
        show_summary
        echo -e "${GREEN}🎉 Configurazione caricata con successo!${NC}"
        exit 0
    else
        echo -e "${RED}❌ Configurazione incompleta. Verifica manualmente.${NC}"
        exit 1
    fi
}

# Esegui
main
