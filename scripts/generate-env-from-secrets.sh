#!/bin/bash

# Generate .env file from Docker secrets
# Usage: ./scripts/generate-env-from-secrets.sh

set -e

SECRETS_DIR="infra/secrets"
ENV_FILE="apps/api/src/Api/.env"
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"

cd "$PROJECT_ROOT"

echo "🔐 Generating .env file from Docker secrets..."
echo ""

# Check if secrets directory exists
if [ ! -d "$SECRETS_DIR" ]; then
    echo "❌ Error: Secrets directory not found: $SECRETS_DIR"
    echo "   Please ensure Docker secrets are initialized in infra/secrets/"
    exit 1
fi

# Function to read secret file
read_secret() {
    local secret_file="$1"
    if [ -f "$SECRETS_DIR/$secret_file" ]; then
        cat "$SECRETS_DIR/$secret_file" | tr -d '\n\r' | xargs
    else
        echo "MISSING-SECRET-FILE-$secret_file"
    fi
}

# Generate .env file
cat > "$ENV_FILE" << 'EOF_HEADER'
# PostgreSQL Configuration (Local Development)
# AUTO-GENERATED from infra/secrets/ directory
# DO NOT COMMIT THIS FILE - it is gitignored
# Regenerate with: ./scripts/generate-env-from-secrets.sh

EOF_HEADER

# Database credentials
POSTGRES_PASSWORD=$(read_secret "postgres-password.txt")
cat >> "$ENV_FILE" << EOF
# Database Connection
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_DB=meepleai
POSTGRES_USER=postgres
POSTGRES_PASSWORD=$POSTGRES_PASSWORD

EOF

# Redis credentials
REDIS_PASSWORD=$(read_secret "redis-password.txt")
cat >> "$ENV_FILE" << EOF
# Redis Configuration
REDIS_PASSWORD=$REDIS_PASSWORD
REDIS_URL=localhost:6379,password=$REDIS_PASSWORD,abortConnect=false

EOF

# Other services
cat >> "$ENV_FILE" << 'EOF'
# Vector Database
QDRANT_URL=http://localhost:6333

# LLM Service
OLLAMA_URL=http://localhost:11434

EOF

# OpenRouter API key (optional)
if [ -f "$SECRETS_DIR/openrouter-api-key.txt" ]; then
    OPENROUTER_API_KEY=$(read_secret "openrouter-api-key.txt")
    cat >> "$ENV_FILE" << EOF
# AI API Keys
OPENROUTER_API_KEY=$OPENROUTER_API_KEY

EOF
fi

# OAuth credentials (optional)
if [ -f "$SECRETS_DIR/google-oauth-client-id.txt" ]; then
    GOOGLE_CLIENT_ID=$(read_secret "google-oauth-client-id.txt")
    GOOGLE_CLIENT_SECRET=$(read_secret "google-oauth-client-secret.txt")
    cat >> "$ENV_FILE" << EOF
# Google OAuth
GOOGLE_OAUTH_CLIENT_ID=$GOOGLE_CLIENT_ID
GOOGLE_OAUTH_CLIENT_SECRET=$GOOGLE_CLIENT_SECRET

EOF
fi

if [ -f "$SECRETS_DIR/discord-oauth-client-id.txt" ]; then
    DISCORD_CLIENT_ID=$(read_secret "discord-oauth-client-id.txt")
    DISCORD_CLIENT_SECRET=$(read_secret "discord-oauth-client-secret.txt")
    cat >> "$ENV_FILE" << EOF
# Discord OAuth
DISCORD_OAUTH_CLIENT_ID=$DISCORD_CLIENT_ID
DISCORD_OAUTH_CLIENT_SECRET=$DISCORD_CLIENT_SECRET

EOF
fi

if [ -f "$SECRETS_DIR/github-oauth-client-id.txt" ]; then
    GITHUB_CLIENT_ID=$(read_secret "github-oauth-client-id.txt")
    GITHUB_CLIENT_SECRET=$(read_secret "github-oauth-client-secret.txt")
    cat >> "$ENV_FILE" << EOF
# GitHub OAuth
GITHUB_OAUTH_CLIENT_ID=$GITHUB_CLIENT_ID
GITHUB_OAUTH_CLIENT_SECRET=$GITHUB_CLIENT_SECRET

EOF
fi

# Initial admin password
if [ -f "$SECRETS_DIR/initial-admin-password.txt" ]; then
    INITIAL_ADMIN_PASSWORD=$(read_secret "initial-admin-password.txt")
    cat >> "$ENV_FILE" << EOF
# Initial Admin User
INITIAL_ADMIN_EMAIL=admin@meepleai.dev
INITIAL_ADMIN_PASSWORD=$INITIAL_ADMIN_PASSWORD
INITIAL_ADMIN_DISPLAY_NAME=System Admin

EOF
fi

# BGG API token (optional)
if [ -f "$SECRETS_DIR/bgg-api-token.txt" ]; then
    BGG_API_TOKEN=$(read_secret "bgg-api-token.txt")
    cat >> "$ENV_FILE" << EOF
# BoardGameGeek API
BGG_API_TOKEN=$BGG_API_TOKEN
EOF
fi

echo "✅ Generated $ENV_FILE"
echo ""
echo "📝 Note: This file is gitignored and safe for local development"
echo "🔄 Run this script again if you update secrets in infra/secrets/"
echo ""
