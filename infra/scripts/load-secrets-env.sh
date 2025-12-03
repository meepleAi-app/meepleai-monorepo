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

# Application / API keys
load_secret "openrouter-api-key" "OPENROUTER_API_KEY"
load_secret "initial-admin-password" "INITIAL_ADMIN_PASSWORD"

# Observability
load_secret "gmail-app-password" "GMAIL_APP_PASSWORD"
load_secret "grafana-admin-password" "GF_SECURITY_ADMIN_PASSWORD"

echo "[secrets] secrets loaded. executing: $@"
echo

exec "$@"
