#!/usr/bin/env sh
# Load Docker Secrets into environment variables
# SEC-708: Docker Secrets support for services that don't natively support _FILE pattern
#
# Usage (in docker-compose.yml):
#   command: ["/scripts/load-secrets-env.sh", "original-command", "args"]
#
# This script reads all /run/secrets/* files and exports them as environment variables
# before executing the original service command.

set -e

SECRETS_DIR="/run/secrets"

# Function to load a secret file into an environment variable
load_secret() {
    secret_name=$1
    env_var_name=$2
    secret_file="$SECRETS_DIR/$secret_name"

    if [ -f "$secret_file" ]; then
        # Read secret value (trim whitespace)
        secret_value=$(cat "$secret_file" | tr -d '\n\r' | xargs)

        if [ -n "$secret_value" ]; then
            # Export as environment variable
            export "${env_var_name}=${secret_value}"
            echo "✅ Loaded secret: $env_var_name (from $secret_name)"
        else
            echo "⚠️  Warning: Secret file is empty: $secret_file"
        fi
    else
        echo "ℹ️  Secret not found (optional): $secret_file"
    fi
}

echo "🔐 Loading Docker Secrets into environment..."

# Load all known secrets
load_secret "postgres-password" "POSTGRES_PASSWORD"
load_secret "postgres-password" "DB_POSTGRESDB_PASSWORD"  # n8n uses different var name
load_secret "openrouter-api-key" "OPENROUTER_API_KEY"
load_secret "n8n-encryption-key" "N8N_ENCRYPTION_KEY"
load_secret "n8n-basic-auth-password" "N8N_BASIC_AUTH_PASSWORD"
load_secret "gmail-app-password" "GMAIL_APP_PASSWORD"
load_secret "grafana-admin-password" "GF_SECURITY_ADMIN_PASSWORD"
load_secret "initial-admin-password" "INITIAL_ADMIN_PASSWORD"

echo "✅ Secrets loaded. Executing: $@"
echo ""

# Execute the original command
exec "$@"
