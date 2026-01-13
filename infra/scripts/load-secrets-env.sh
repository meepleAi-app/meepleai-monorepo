#!/usr/bin/env sh
# Load Docker secrets into environment variables for images that do not support *_FILE.
# Usage in compose: entrypoint: ["/scripts/load-secrets-env.sh", "original-cmd", "args"]

set -e

SECRETS_DIR="/run/secrets"

load_secret() {
    secret_name=$1
    env_var_name=$2
    secret_file="$SECRETS_DIR/$secret_name"

    if [ -f "$secret_file" ]; then
        secret_value=$(cat "$secret_file" | tr -d '\n\r' | xargs)
        if [ -n "$secret_value" ]; then
            export "${env_var_name}=${secret_value}"
            echo "[secrets] loaded $env_var_name from $secret_name"
        else
            echo "[secrets][warn] secret file is empty: $secret_file"
        fi
    else
        echo "[secrets][warn] secret not found (optional): $secret_file"
    fi
}

echo "[secrets] loading Docker secrets into environment..."

# Database / cache
load_secret "postgres-password" "POSTGRES_PASSWORD"
load_secret "postgres-password" "DB_POSTGRESDB_PASSWORD"  # n8n variable name
load_secret "n8n-basic-auth-password" "N8N_BASIC_AUTH_PASSWORD"
load_secret "n8n-encryption-key" "N8N_ENCRYPTION_KEY"
load_secret "redis-password" "REDIS_PASSWORD"

# Build Redis connection string with password from secret
if [ -f "$SECRETS_DIR/redis-password" ]; then
    REDIS_PASSWORD_VALUE=$(cat "$SECRETS_DIR/redis-password" | tr -d '\n\r' | xargs)
    export REDIS_URL="redis:6379,password=${REDIS_PASSWORD_VALUE},abortConnect=false"
    echo "[secrets] built REDIS_URL with password from redis-password secret"
fi

# Application / API keys
load_secret "openrouter-api-key" "OPENROUTER_API_KEY"
load_secret "initial-admin-password" "INITIAL_ADMIN_PASSWORD"

# OAuth provider credentials
load_secret "google-oauth-client-id" "GOOGLE_OAUTH_CLIENT_ID"
load_secret "google-oauth-client-secret" "GOOGLE_OAUTH_CLIENT_SECRET"
load_secret "discord-oauth-client-id" "DISCORD_OAUTH_CLIENT_ID"
load_secret "discord-oauth-client-secret" "DISCORD_OAUTH_CLIENT_SECRET"
load_secret "github-oauth-client-id" "GITHUB_OAUTH_CLIENT_ID"
load_secret "github-oauth-client-secret" "GITHUB_OAUTH_CLIENT_SECRET"

# Observability
load_secret "gmail-app-password" "GMAIL_APP_PASSWORD"
load_secret "grafana-admin-password" "GF_SECURITY_ADMIN_PASSWORD"

echo "[secrets] secrets loaded. executing: $@"
echo

exec "$@"
