# MeepleAI Backup Scripts

Automated backup and restore scripts for all critical MeepleAI services.

## Overview

This directory contains comprehensive backup and restore procedures for:

- **PostgreSQL** - Primary relational database (users, games, rules, sessions)
- **Qdrant** - Vector database (embeddings for RAG system)
- **Redis** - Cache and session storage
- **Docker Volumes** - Grafana, Prometheus, n8n data

## Quick Start

### Running Backups

```bash
# Backup all services (sequential execution)
./tools/backup/backup-all.sh

# Backup all services in parallel (faster)
./tools/backup/backup-all.sh --parallel

# Backup individual services
./tools/backup/backup-postgres.sh
./tools/backup/backup-qdrant.sh
./tools/backup/backup-redis.sh
./tools/backup/backup-volumes.sh
```

### Restoring Backups

**⚠️ WARNING**: Restore operations will OVERWRITE existing data!

```bash
# Restore PostgreSQL
./tools/backup/restore-postgres.sh backups/postgres/postgres_20250108_020000.sql.gz

# Restore Qdrant collection
./tools/backup/restore-qdrant.sh backups/qdrant/rules_20250108_020000.snapshot.gz

# Restore Redis
./tools/backup/restore-redis.sh backups/redis/redis_20250108_020000.rdb.gz

# Restore Docker volume
./tools/backup/restore-volumes.sh backups/volumes/infra_grafanadata_20250108_020000.tar.gz infra_grafanadata
```

### Verifying Backups

```bash
# Verify all backups integrity
./tools/backup/verify-backups.sh
```

### Running Tests

```bash
# Run backup/restore test suite
./tools/backup/test-backup-restore.sh
```

## Configuration

### Environment Variables

Create a `.env` file or set environment variables:

```bash
# Backup configuration
export BACKUP_ROOT_DIR="./backups"           # Root backup directory
export RETENTION_DAYS=30                     # Days to keep backups
export COMPRESSION_THRESHOLD_DAYS=7          # Days before xz compression

# Docker configuration
export DOCKER_COMPOSE_DIR="./infra"          # Docker Compose directory
export POSTGRES_CONTAINER="postgres"
export REDIS_CONTAINER="redis"
export QDRANT_CONTAINER="qdrant"

# Database credentials
export POSTGRES_USER="meeple"
export POSTGRES_DB="meepleai"
export POSTGRES_PASSWORD="your_password"     # Optional

# Qdrant configuration
export QDRANT_URL="http://localhost:6333"

# Notifications (optional)
export NOTIFICATION_EMAIL="admin@example.com"
export SLACK_WEBHOOK_URL="https://hooks.slack.com/..."

# S3 configuration (optional, Phase 2)
export S3_BUCKET="meepleai-backups"
export S3_REGION="us-east-1"
export S3_UPLOAD_ENABLED="false"
```

All configuration is centralized in `backup-config.sh`.

## Backup Strategy

### Retention Policy

- **Recent backups** (< 7 days): Compressed with gzip for fast restore
- **Old backups** (7-30 days): Recompressed with xz for better compression
- **Cleanup**: Backups older than 30 days are automatically deleted

### Compression Comparison

| Format | Compression Ratio | Restore Speed | Use Case |
|--------|-------------------|---------------|----------|
| gzip   | ~60-70%          | Fast          | Recent backups (< 7 days) |
| xz     | ~75-85%          | Slower        | Archive backups (7-30 days) |

### Backup Schedule

Recommended cron schedule:

```bash
# Daily backup at 2 AM
0 2 * * * /path/to/meepleai-monorepo/tools/backup/backup-all.sh

# Weekly verification on Sundays at 3 AM
0 3 * * 0 /path/to/meepleai-monorepo/tools/backup/verify-backups.sh
```

Or use systemd timers (see documentation).

## Scripts Reference

### Backup Scripts

| Script | Purpose | Output |
|--------|---------|--------|
| `backup-all.sh` | Master orchestrator | Coordinates all backups |
| `backup-postgres.sh` | PostgreSQL backup | `.sql.gz` file |
| `backup-qdrant.sh` | Qdrant snapshots | `.snapshot.gz` file |
| `backup-redis.sh` | Redis RDB backup | `.rdb.gz` file |
| `backup-volumes.sh` | Docker volumes | `.tar.gz` files |

### Restore Scripts

| Script | Purpose | Input |
|--------|---------|-------|
| `restore-postgres.sh` | Restore PostgreSQL | `.sql.gz` or `.sql.xz` |
| `restore-qdrant.sh` | Restore Qdrant | `.snapshot.gz` or `.snapshot.xz` |
| `restore-redis.sh` | Restore Redis | `.rdb.gz` or `.rdb.xz` |
| `restore-volumes.sh` | Restore Docker volume | `.tar.gz` or `.tar.xz` |

### Utility Scripts

| Script | Purpose |
|--------|---------|
| `backup-config.sh` | Centralized configuration |
| `verify-backups.sh` | Integrity checks and validation |
| `test-backup-restore.sh` | Test suite for backup/restore |

## Backup File Naming

All backup files follow a consistent naming pattern:

```
<service>_<timestamp>.<extension>

Examples:
postgres_20250108_020000.sql.gz
rules_20250108_020000.snapshot.gz
redis_20250108_020000.rdb.gz
infra_grafanadata_20250108_020000.tar.gz
```

Timestamp format: `YYYYMMDD_HHMMSS`

## Safety Features

### Pre-Restore Backups

All restore scripts automatically create a safety backup before overwriting:

```
backups/<service>/pre_restore_<timestamp>.<ext>
```

This allows rollback if restore fails.

### Confirmation Prompts

Restore scripts require explicit confirmation:

```bash
# Interactive mode (prompts for confirmation)
./tools/backup/restore-postgres.sh backup.sql.gz

# Non-interactive mode (requires FORCE_RESTORE=1)
FORCE_RESTORE=1 ./tools/backup/restore-postgres.sh backup.sql.gz
```

### Verification

All restore scripts include verification steps:

- PostgreSQL: Table count check
- Qdrant: Point count check
- Redis: Key count check
- Volumes: File count and size check

## Troubleshooting

### Backup Fails

```bash
# Check Docker containers are running
docker compose -f infra/docker-compose.yml ps

# Check disk space
df -h ./backups

# Check script logs
ls -lh logs/backup/

# Verify configuration
bash -x tools/backup/backup-config.sh
```

### Restore Fails

```bash
# Check backup file integrity
gunzip -t backups/postgres/postgres_*.sql.gz

# Verify Docker container is running
docker compose -f infra/docker-compose.yml ps

# Check pre-restore safety backup
ls -lh backups/*/pre_restore_*
```

### Permission Issues

```bash
# Ensure scripts are executable
chmod +x tools/backup/*.sh

# Check Docker socket permissions
docker info

# Verify backup directory permissions
ls -ld backups/
```

## Notifications

### Email Notifications

Configure via environment variable:

```bash
export NOTIFICATION_EMAIL="admin@example.com"
```

Requires `mail` command (e.g., `mailutils` package).

### Slack Notifications

Configure via webhook:

```bash
export SLACK_WEBHOOK_URL="https://hooks.slack.com/services/YOUR/WEBHOOK/URL"
```

## Remote Backup (Phase 2)

S3 upload support is included but disabled by default:

```bash
# Enable S3 uploads
export S3_UPLOAD_ENABLED="true"
export S3_BUCKET="meepleai-backups"
export S3_REGION="us-east-1"

# Run backup with S3 upload
./tools/backup/backup-all.sh
```

Requires AWS CLI configured with appropriate credentials.

## Performance

### Execution Times (Approximate)

| Operation | Time |
|-----------|------|
| PostgreSQL backup | 5-30s |
| Qdrant backup | 10-60s |
| Redis backup | 1-5s |
| Volumes backup | 5-20s per volume |
| Full backup (sequential) | 30-120s |
| Full backup (parallel) | 15-60s |

Times depend on data size and system performance.

### Resource Usage

- **Sequential mode**: Lower CPU/memory, safer
- **Parallel mode**: Higher CPU/memory, faster

## Documentation

- **Backup Strategy**: `docs/05-operations/backup-strategy.md`
- **Restore Procedures**: `docs/05-operations/restore-procedures.md`
- **Disaster Recovery**: `docs/05-operations/deployment/disaster-recovery.md`

## Support

For issues or questions:

1. Check logs in `logs/backup/`
2. Run verification: `./tools/backup/verify-backups.sh`
3. Review documentation in `docs/05-operations/`
4. Open issue on GitHub

## License

Part of MeepleAI project.
