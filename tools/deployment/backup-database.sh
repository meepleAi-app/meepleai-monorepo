#!/bin/bash
# Database Backup Script
# Creates timestamped backups before deployments

set -e

ENVIRONMENT="${1:-staging}"
BACKUP_DIR="${BACKUP_DIR:-/var/backups/meepleai}"
TIMESTAMP=$(date +%Y%m%d-%H%M%S)

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

# Environment configuration
case "$ENVIRONMENT" in
    staging)
        DB_HOST="${STAGING_DB_HOST:-staging-db.meepleai.dev}"
        DB_NAME="${STAGING_DB_NAME:-meepleai_staging}"
        DB_USER="${STAGING_DB_USER:-postgres}"
        ;;
    production)
        DB_HOST="${PROD_DB_HOST:-db.meepleai.dev}"
        DB_NAME="${PROD_DB_NAME:-meepleai}"
        DB_USER="${PROD_DB_USER:-postgres}"
        ;;
    *)
        echo -e "${RED}Unknown environment: ${ENVIRONMENT}${NC}"
        exit 1
        ;;
esac

echo -e "${YELLOW}💾 Database Backup - ${ENVIRONMENT}${NC}"
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Create backup directory
mkdir -p "${BACKUP_DIR}/${ENVIRONMENT}"

BACKUP_FILE="${BACKUP_DIR}/${ENVIRONMENT}/backup-${TIMESTAMP}.sql.gz"

echo "Creating backup..."
echo "Database: ${DB_NAME}"
echo "Host: ${DB_HOST}"
echo "File: ${BACKUP_FILE}"
echo ""

# Create backup (adjust based on your setup)
# Example for PostgreSQL
if command -v pg_dump &> /dev/null; then
    PGPASSWORD="${DB_PASSWORD}" pg_dump \
        -h "${DB_HOST}" \
        -U "${DB_USER}" \
        -d "${DB_NAME}" \
        --format=plain \
        --no-owner \
        --no-privileges \
        | gzip > "${BACKUP_FILE}"

    echo -e "${GREEN}✓${NC} Backup created: ${BACKUP_FILE}"
else
    # Alternative: Use docker exec if database is containerized
    echo -e "${YELLOW}⚠${NC}  pg_dump not found, using docker exec..."

    docker exec meepleai-postgres pg_dump \
        -U "${DB_USER}" \
        -d "${DB_NAME}" \
        --format=plain \
        --no-owner \
        --no-privileges \
        | gzip > "${BACKUP_FILE}"

    echo -e "${GREEN}✓${NC} Backup created via Docker: ${BACKUP_FILE}"
fi

# Verify backup
BACKUP_SIZE=$(du -h "${BACKUP_FILE}" | cut -f1)
echo "Backup size: ${BACKUP_SIZE}"

# Keep only last 30 backups
echo "Cleaning old backups (keeping last 30)..."
ls -t "${BACKUP_DIR}/${ENVIRONMENT}"/backup-*.sql.gz 2>/dev/null | tail -n +31 | xargs -r rm
echo -e "${GREEN}✓${NC} Old backups cleaned"

echo ""
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${GREEN}✅ Backup completed successfully${NC}"
echo -e "${GREEN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""
echo "Restore with:"
echo "gunzip < ${BACKUP_FILE} | psql -h ${DB_HOST} -U ${DB_USER} -d ${DB_NAME}"
