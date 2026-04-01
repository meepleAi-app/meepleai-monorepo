#!/usr/bin/env bash
# Integration Check — verifica prerequisiti prima di make integration
#
# Usage:
#   bash infra/scripts/integration-check.sh

set -e

SSH_KEY="${HOME}/.ssh/meepleai-staging"
STAGING_HOST="deploy@204.168.135.69"
CONTROL_SOCKET="${HOME}/.ssh/meepleai-tunnel.sock"

PASS=0
FAIL=0

check() {
    local label="$1"
    local ok="$2"
    if [ "$ok" = "true" ]; then
        echo "  ✓ $label"
        PASS=$((PASS + 1))
    else
        echo "  ✗ $label"
        FAIL=$((FAIL + 1))
    fi
}

check_port() {
    local label="$1"
    local host="$2"
    local port="$3"
    # Use bash built-in /dev/tcp — works on Git Bash, WSL, and Linux without nc
    if bash -c "echo >/dev/tcp/$host/$port" &>/dev/null 2>&1; then
        check "$label ($host:$port)" "true"
    else
        check "$label ($host:$port) — non raggiungibile" "false"
    fi
}

echo ""
echo "=== MeepleAI Integration Mode — Pre-flight Check ==="
echo ""

# 1. SSH key
echo "[ SSH ]"
if [ -f "$SSH_KEY" ]; then
    check "SSH key presente: $SSH_KEY" "true"
else
    check "SSH key presente: $SSH_KEY" "false"
    echo "    → Esegui: ssh-keygen -t ed25519 -f $SSH_KEY"
fi

# 2. Staging raggiungibile
echo ""
echo "[ Staging ]"
if ssh -i "$SSH_KEY" -o ConnectTimeout=5 -o BatchMode=yes "$STAGING_HOST" "echo ok" &>/dev/null; then
    check "Staging raggiungibile ($STAGING_HOST)" "true"
else
    check "Staging raggiungibile ($STAGING_HOST)" "false"
    echo "    → Verifica che il server staging sia up e la chiave SSH sia autorizzata"
fi

# 3. Tunnel attivo
echo ""
echo "[ Tunnel SSH ]"
if ssh -O check -S "$CONTROL_SOCKET" "$STAGING_HOST" &>/dev/null; then
    check "Tunnel SSH attivo" "true"
else
    check "Tunnel SSH attivo — non attivo" "false"
    echo "    → Esegui: make tunnel"
fi

# 4. Porte tunnelate
echo ""
echo "[ Porte Locali ]"
check_port "PostgreSQL"  "localhost" 25432
check_port "Redis"       "localhost" 26379
check_port "Embedding"   "localhost" 18000
check_port "Reranker"    "localhost" 18003
check_port "Ollama"      "localhost" 21434

# 5. Tool locali
echo ""
echo "[ Tool Locali ]"
if command -v dotnet &>/dev/null; then
    check "dotnet installato ($(dotnet --version 2>/dev/null))" "true"
else
    check "dotnet installato" "false"
    echo "    → Installa .NET 9 SDK: https://dotnet.microsoft.com/download"
fi

if command -v pnpm &>/dev/null; then
    check "pnpm installato ($(pnpm --version 2>/dev/null))" "true"
else
    check "pnpm installato" "false"
    echo "    → Installa: npm install -g pnpm"
fi

# Riepilogo
echo ""
echo "================================"
echo "  Passed: $PASS | Failed: $FAIL"
echo "================================"
echo ""

if [ $FAIL -gt 0 ]; then
    echo "⚠  Risolvi i problemi sopra prima di eseguire 'make integration'"
    exit 1
else
    echo "✓  Tutto OK. Puoi eseguire 'make integration'"
    exit 0
fi
