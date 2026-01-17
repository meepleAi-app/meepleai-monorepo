#!/usr/bin/env sh
# Issue #2570: Process environment variables loaded from .secret files via env_file
# This script maps/builds derived environment variables from base secrets
# Usage in compose: entrypoint: ["/scripts/load-secrets-env.sh", "original-cmd", "args"]

set -e

echo "[secrets] processing environment variables from .secret files..."

# Issue #2570: All secrets now loaded from .secret files via env_file
# Variables already available: POSTGRES_*, REDIS_*, JWT_*, OPENROUTER_*, ADMIN_*, OAuth

# Map POSTGRES_* to n8n's DB_POSTGRESDB_* format (for n8n service compatibility)
if [ -n "$POSTGRES_USER" ]; then
    export DB_POSTGRESDB_USER="$POSTGRES_USER"
    echo "[secrets] mapped POSTGRES_USER to DB_POSTGRESDB_USER"
fi
if [ -n "$POSTGRES_PASSWORD" ]; then
    export DB_POSTGRESDB_PASSWORD="$POSTGRES_PASSWORD"
    echo "[secrets] mapped POSTGRES_PASSWORD to DB_POSTGRESDB_PASSWORD"
fi
if [ -n "$POSTGRES_DB" ]; then
    export DB_POSTGRESDB_DATABASE="$POSTGRES_DB"
    echo "[secrets] mapped POSTGRES_DB to DB_POSTGRESDB_DATABASE"
fi

# Build Redis connection string from REDIS_PASSWORD env var (loaded from redis.secret)
if [ -n "$REDIS_PASSWORD" ]; then
    export REDIS_URL="redis:6379,password=${REDIS_PASSWORD},abortConnect=false"
    echo "[secrets] built REDIS_URL with password from redis.secret"
fi

# Issue #2570: Secret consolidation complete
# All secrets now loaded from .secret files via env_file
# No Docker secrets (/run/secrets) needed anymore

echo "[secrets] secrets loaded. executing: $@"
echo

exec "$@"
