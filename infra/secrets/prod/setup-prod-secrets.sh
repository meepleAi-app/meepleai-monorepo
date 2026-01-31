#!/bin/bash
# =============================================================================
# MeepleAI Production Secrets Setup
# =============================================================================
#
# This script generates secure secrets for production deployment
#
# Usage:
#   chmod +x setup-prod-secrets.sh
#   ./setup-prod-secrets.sh
#
# After running:
#   1. Review generated secrets
#   2. Update openrouter-api-key.txt with your actual API key
#   3. Update email credentials if using Gmail
#   4. Generate Traefik dashboard password hash
#
# =============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "🔐 MeepleAI Production Secrets Generator"
echo "========================================="
echo ""

# Function to generate secure random string
generate_secret() {
    openssl rand -base64 32 | tr -d '/+=' | head -c 32
}

# Function to generate secure password
generate_password() {
    openssl rand -base64 24 | tr -d '/+='
}

# =============================================================================
# Database Secrets
# =============================================================================
echo "📦 Generating database secrets..."

if [ ! -f postgres-password.txt ]; then
    generate_secret > postgres-password.txt
    echo "   ✅ postgres-password.txt"
else
    echo "   ⏭️  postgres-password.txt (exists)"
fi

# =============================================================================
# Redis Secrets
# =============================================================================
echo "📦 Generating Redis secrets..."

if [ ! -f redis-password.txt ]; then
    generate_secret > redis-password.txt
    echo "   ✅ redis-password.txt"
else
    echo "   ⏭️  redis-password.txt (exists)"
fi

# =============================================================================
# Application Secrets
# =============================================================================
echo "📦 Generating application secrets..."

if [ ! -f initial-admin-password.txt ]; then
    generate_password > initial-admin-password.txt
    echo "   ✅ initial-admin-password.txt"
else
    echo "   ⏭️  initial-admin-password.txt (exists)"
fi

if [ ! -f jwt-secret.txt ]; then
    openssl rand -base64 64 | tr -d '\n' > jwt-secret.txt
    echo "   ✅ jwt-secret.txt"
else
    echo "   ⏭️  jwt-secret.txt (exists)"
fi

# =============================================================================
# Monitoring Secrets
# =============================================================================
echo "📦 Generating monitoring secrets..."

if [ ! -f grafana-admin-password.txt ]; then
    generate_password > grafana-admin-password.txt
    echo "   ✅ grafana-admin-password.txt"
else
    echo "   ⏭️  grafana-admin-password.txt (exists)"
fi

# =============================================================================
# N8N Secrets
# =============================================================================
echo "📦 Generating N8N secrets..."

if [ ! -f n8n-encryption-key.txt ]; then
    generate_secret > n8n-encryption-key.txt
    echo "   ✅ n8n-encryption-key.txt"
else
    echo "   ⏭️  n8n-encryption-key.txt (exists)"
fi

if [ ! -f n8n-basic-auth-password.txt ]; then
    generate_password > n8n-basic-auth-password.txt
    echo "   ✅ n8n-basic-auth-password.txt"
else
    echo "   ⏭️  n8n-basic-auth-password.txt (exists)"
fi

# =============================================================================
# API Keys (placeholders)
# =============================================================================
echo "📦 Creating API key placeholders..."

if [ ! -f openrouter-api-key.txt ]; then
    echo "sk-or-v1-REPLACE_WITH_YOUR_OPENROUTER_API_KEY" > openrouter-api-key.txt
    echo "   ⚠️  openrouter-api-key.txt (NEEDS YOUR API KEY)"
else
    echo "   ⏭️  openrouter-api-key.txt (exists)"
fi

# =============================================================================
# Email Secrets (placeholders)
# =============================================================================
echo "📦 Creating email placeholders..."

if [ ! -f gmail-app-password.txt ]; then
    echo "REPLACE_WITH_GMAIL_APP_PASSWORD" > gmail-app-password.txt
    echo "   ⚠️  gmail-app-password.txt (NEEDS CONFIGURATION)"
else
    echo "   ⏭️  gmail-app-password.txt (exists)"
fi

# =============================================================================
# Traefik Dashboard Password
# =============================================================================
echo "📦 Generating Traefik dashboard password..."

if [ ! -f traefik-dashboard-password.txt ]; then
    TRAEFIK_PASS=$(generate_password)
    echo "$TRAEFIK_PASS" > traefik-dashboard-password.txt

    # Generate bcrypt hash for Traefik
    if command -v htpasswd &> /dev/null; then
        HASH=$(htpasswd -nbB admin "$TRAEFIK_PASS" | sed -e 's/\$/\$\$/g')
        echo "$HASH" > traefik-dashboard-users.txt
        echo "   ✅ traefik-dashboard-password.txt"
        echo "   ✅ traefik-dashboard-users.txt (bcrypt hash)"
    else
        echo "   ✅ traefik-dashboard-password.txt"
        echo "   ⚠️  htpasswd not found - run this to generate hash:"
        echo "      htpasswd -nbB admin \"\$(cat traefik-dashboard-password.txt)\""
    fi
else
    echo "   ⏭️  traefik-dashboard-password.txt (exists)"
fi

# =============================================================================
# Set permissions
# =============================================================================
echo ""
echo "🔒 Setting file permissions..."
chmod 600 *.txt 2>/dev/null || true
echo "   ✅ Permissions set to 600"

# =============================================================================
# Summary
# =============================================================================
echo ""
echo "========================================="
echo "✅ Production secrets generated!"
echo "========================================="
echo ""
echo "⚠️  ACTION REQUIRED:"
echo "   1. Edit openrouter-api-key.txt with your OpenRouter API key"
echo "   2. Edit gmail-app-password.txt if using email notifications"
echo "   3. Update traefik dashboard hash in middlewares.prod.yml"
echo ""
echo "📋 Generated credentials:"
echo "   - Admin password: $(cat initial-admin-password.txt)"
echo "   - Grafana admin: $(cat grafana-admin-password.txt)"
echo "   - Traefik dashboard: $(cat traefik-dashboard-password.txt)"
echo ""
echo "🔐 Keep these credentials secure!"
echo ""
