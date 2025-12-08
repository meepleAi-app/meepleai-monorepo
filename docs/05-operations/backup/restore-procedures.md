# Restore Procedures

**Status**: Production Ready
**Owner**: DevOps Team
**Last Updated**: 2025-01-08
**Issue**: #704

---

## Overview

Step-by-step procedures for restoring MeepleAI services from backups in disaster recovery scenarios.

**⚠️ WARNING**: Restore operations will **OVERWRITE** existing data. Always confirm you have a recent backup before proceeding.

---

## Quick Reference

| Service | Restore Script | Typical Duration |
|---------|---------------|------------------|
| PostgreSQL | `restore-postgres.sh` | 30-120 seconds |
| Qdrant | `restore-qdrant.sh` | 15-60 seconds |
| Redis | `restore-redis.sh` | 10-30 seconds |
| Docker Volumes | `restore-volumes.sh` | 15-45 seconds per volume |

---

## Pre-Restore Checklist

Before performing any restore operation:

- [ ] **Identify the correct backup file** (verify timestamp and service)
- [ ] **Verify backup file integrity** (run `verify-backups.sh`)
- [ ] **Confirm backup file is not corrupted** (check file size, test decompression)
- [ ] **Notify team members** (if in production environment)
- [ ] **Create current backup** (safety net before restore)
- [ ] **Stop dependent services** (if restoring critical dependencies)
- [ ] **Verify sufficient disk space** (at least 2x backup file size)

---

## Recovery Scenarios

### Scenario 1: Single Database Corruption

**Symptoms**:
- PostgreSQL errors in logs
- Data inconsistencies
- Failed transactions

**Priority**: P0 (Critical)
**Estimated RTO**: < 15 minutes

**Procedure**:
1. Stop API service to prevent further writes
2. Identify latest valid backup
3. Restore PostgreSQL database
4. Verify data integrity
5. Restart API service
6. Monitor application logs

### Scenario 2: Complete Data Loss

**Symptoms**:
- All services down
- Volume corruption
- Disk failure

**Priority**: P0 (Critical)
**Estimated RTO**: < 30 minutes

**Procedure**:
1. Provision new infrastructure (if hardware failure)
2. Restore PostgreSQL (most critical)
3. Restore Qdrant (RAG functionality)
4. Restore Redis (sessions)
5. Restore Docker volumes (monitoring data)
6. Verify all services operational
7. Run smoke tests

### Scenario 3: Rollback After Bad Deployment

**Symptoms**:
- Application errors after deployment
- Data migration issues
- Performance degradation

**Priority**: P1 (High)
**Estimated RTO**: < 10 minutes

**Procedure**:
1. Use pre-deployment backup (if available)
2. Restore affected services
3. Rollback code deployment
4. Verify services operational
5. Post-mortem analysis

### Scenario 4: Single Service Failure

**Symptoms**:
- One service (Qdrant, Redis, etc.) malfunctioning
- Other services operational

**Priority**: P1-P2 (High to Medium)
**Estimated RTO**: < 15 minutes

**Procedure**:
1. Identify failed service
2. Stop failed service
3. Restore service from backup
4. Restart service
5. Verify integration with other services
6. Monitor for issues

---

## Detailed Restore Procedures

### PostgreSQL Restore

#### Prerequisites

- PostgreSQL container running
- Backup file available and verified
- Sufficient disk space

#### Command

```bash
./tools/backup/restore-postgres.sh backups/postgres/postgres_YYYYMMDD_HHMMSS.sql.gz
```

#### Interactive Confirmation

```
⚠️  WARNING: DATABASE RESTORE OPERATION
This will OVERWRITE the current database with:
  Backup file: backups/postgres/postgres_20250108_020000.sql.gz
  Database: meepleai

Current database will be PERMANENTLY LOST!

Type 'YES' to confirm restore: YES
```

#### Non-Interactive Mode

```bash
# For automation (requires explicit flag)
FORCE_RESTORE=1 ./tools/backup/restore-postgres.sh backup.sql.gz
```

#### Process

1. **Pre-restore backup**: Automatic safety backup created
2. **Terminate connections**: All active connections closed
3. **Drop database**: Existing database dropped
4. **Recreate database**: Fresh database created
5. **Restore data**: Backup file imported
6. **Verification**: Table count check

#### Verification

```bash
# Check database is accessible
docker compose -f infra/docker-compose.yml exec postgres psql -U meeple -d meepleai -c "SELECT COUNT(*) FROM users;"

# Check tables exist
docker compose -f infra/docker-compose.yml exec postgres psql -U meeple -d meepleai -c "\dt"

# Check recent data
docker compose -f infra/docker-compose.yml exec postgres psql -U meeple -d meepleai -c "SELECT MAX(created_at) FROM users;"
```

#### Rollback

If restore fails, use pre-restore safety backup:

```bash
./tools/backup/restore-postgres.sh backups/postgres/pre_restore_YYYYMMDD_HHMMSS.sql.gz
```

---

### Qdrant Restore

#### Prerequisites

- Qdrant container running and healthy
- Backup snapshot file available
- Collection name known (or extracted from filename)

#### Command

```bash
# Automatic collection name extraction
./tools/backup/restore-qdrant.sh backups/qdrant/rules_YYYYMMDD_HHMMSS.snapshot.gz

# Explicit collection name
./tools/backup/restore-qdrant.sh backups/qdrant/rules_YYYYMMDD_HHMMSS.snapshot.gz my_collection
```

#### Process

1. **Decompress snapshot**: Extract from gz/xz compression
2. **Delete existing collection**: Remove current collection data
3. **Upload snapshot**: Send snapshot to Qdrant API
4. **Recovery trigger**: Qdrant recreates collection from snapshot
5. **Verification**: Point count check

#### Verification

```bash
# Check collection exists and has data
curl -s http://localhost:6333/collections/rules | grep points_count

# Check collection info
curl http://localhost:6333/collections/rules
```

#### Multiple Collections

Restore all collections:

```bash
# List available backups
ls backups/qdrant/*.snapshot.gz

# Restore each collection
for backup in backups/qdrant/*_YYYYMMDD_HHMMSS.snapshot.gz; do
    ./tools/backup/restore-qdrant.sh "$backup"
done
```

---

### Redis Restore

#### Prerequisites

- Redis container can be stopped
- Backup RDB file available
- No critical sessions in progress

#### Command

```bash
./tools/backup/restore-redis.sh backups/redis/redis_YYYYMMDD_HHMMSS.rdb.gz
```

#### Process

1. **Pre-restore backup**: Current RDB saved
2. **Stop Redis**: Container stopped gracefully
3. **Replace RDB file**: Backup file copied to volume
4. **Start Redis**: Container restarted
5. **Health check**: Wait for Redis to be ready
6. **Verification**: Key count check

#### Verification

```bash
# Check Redis is accessible
docker compose -f infra/docker-compose.yml exec redis redis-cli PING

# Check key count
docker compose -f infra/docker-compose.yml exec redis redis-cli DBSIZE

# Check Redis info
docker compose -f infra/docker-compose.yml exec redis redis-cli INFO
```

#### Impact

- **Downtime**: Redis unavailable during restore (~10-30 seconds)
- **Sessions**: All active sessions lost
- **Cache**: All cache entries recreated on demand

---

### Docker Volumes Restore

#### Prerequisites

- Backup tar file available
- Target volume name known
- Volume can be recreated (service stopped if needed)

#### Command

```bash
./tools/backup/restore-volumes.sh backups/volumes/infra_grafanadata_YYYYMMDD_HHMMSS.tar.gz infra_grafanadata
```

#### Process

1. **Pre-restore backup**: Current volume backed up
2. **Volume check**: Verify or create target volume
3. **Clear volume**: Remove existing contents
4. **Extract backup**: Tar archive extracted to volume
5. **Verification**: File count and size check

#### Common Volumes

```bash
# Grafana dashboards and datasources
./tools/backup/restore-volumes.sh backups/volumes/infra_grafanadata_*.tar.gz infra_grafanadata

# Prometheus metrics history
./tools/backup/restore-volumes.sh backups/volumes/infra_prometheusdata_*.tar.gz infra_prometheusdata

# n8n workflows and credentials
./tools/backup/restore-volumes.sh backups/volumes/infra_n8ndata_*.tar.gz infra_n8ndata
```

#### Verification

```bash
# Check volume exists
docker volume inspect infra_grafanadata

# Check files in volume
docker run --rm -v infra_grafanadata:/data alpine ls -lh /data

# Check volume size
docker run --rm -v infra_grafanadata:/data alpine du -sh /data
```

---

## Full System Restore

Complete disaster recovery from scratch.

### Step-by-Step Procedure

#### 1. Infrastructure Setup

```bash
# Clone repository
git clone https://github.com/your-org/meepleai-monorepo.git
cd meepleai-monorepo

# Start Docker Compose services
cd infra
docker compose up -d

# Wait for services to be healthy
docker compose ps
```

#### 2. Restore PostgreSQL (Priority 1)

```bash
cd ../
./tools/backup/restore-postgres.sh backups/postgres/postgres_LATEST.sql.gz

# Verify
docker compose -f infra/docker-compose.yml exec postgres psql -U meeple -d meepleai -c "SELECT COUNT(*) FROM users;"
```

#### 3. Restore Qdrant (Priority 2)

```bash
# Restore all collections
for backup in backups/qdrant/*.snapshot.gz; do
    ./tools/backup/restore-qdrant.sh "$backup"
done

# Verify
curl http://localhost:6333/collections
```

#### 4. Restore Redis (Priority 3)

```bash
./tools/backup/restore-redis.sh backups/redis/redis_LATEST.rdb.gz

# Verify
docker compose -f infra/docker-compose.yml exec redis redis-cli PING
```

#### 5. Restore Docker Volumes (Priority 4)

```bash
# Grafana
./tools/backup/restore-volumes.sh backups/volumes/infra_grafanadata_LATEST.tar.gz infra_grafanadata

# Prometheus
./tools/backup/restore-volumes.sh backups/volumes/infra_prometheusdata_LATEST.tar.gz infra_prometheusdata

# n8n
./tools/backup/restore-volumes.sh backups/volumes/infra_n8ndata_LATEST.tar.gz infra_n8ndata
```

#### 6. Restart All Services

```bash
cd infra
docker compose restart

# Wait for services to be healthy
sleep 30
docker compose ps
```

#### 7. Verification & Smoke Tests

```bash
# API health check
curl http://localhost:8080/health

# Frontend health check
curl http://localhost:3000

# Database connectivity
docker compose exec postgres psql -U meeple -d meepleai -c "SELECT 1;"

# Qdrant health
curl http://localhost:6333/healthz

# Redis health
docker compose exec redis redis-cli PING
```

#### 8. Post-Restore Checks

- [ ] All services showing as healthy
- [ ] API responds to requests
- [ ] Frontend loads correctly
- [ ] User authentication works
- [ ] RAG system returns results
- [ ] Metrics visible in Grafana
- [ ] No error spikes in logs

---

## Troubleshooting

### Restore Fails: "File Not Found"

```bash
# Check backup file exists
ls -lh backups/postgres/postgres_*.sql.gz

# Check file permissions
stat backups/postgres/postgres_*.sql.gz

# Verify file path is absolute or relative to script location
pwd
```

### Restore Fails: "Container Not Running"

```bash
# Check container status
docker compose -f infra/docker-compose.yml ps

# Start container if stopped
docker compose -f infra/docker-compose.yml start postgres

# Check container logs
docker compose -f infra/docker-compose.yml logs postgres
```

### Restore Fails: "Corrupted Backup File"

```bash
# Test gzip integrity
gunzip -t backups/postgres/postgres_*.sql.gz

# Test xz integrity
xz -t backups/postgres/postgres_*.sql.xz

# Check file size
du -h backups/postgres/postgres_*.sql.gz

# Try alternate backup file
ls -lt backups/postgres/ | head -5
```

### Restore Succeeds But Data Missing

```bash
# Check backup was from correct timestamp
ls -lh backups/postgres/postgres_*.sql.gz

# Verify backup file size is reasonable
du -h backups/postgres/postgres_*.sql.gz

# Check backup creation log
cat logs/backup/postgres_backup_*.log

# Try earlier backup
./tools/backup/restore-postgres.sh backups/postgres/postgres_OLDER_DATE.sql.gz
```

### Restore Takes Too Long

```bash
# Check disk I/O
iostat -x 5

# Check available RAM
free -h

# Check decompression progress (in another terminal)
ps aux | grep gunzip

# Consider using less compressed backup (if available)
ls backups/postgres/*.sql
```

---

## Safety Mechanisms

### Pre-Restore Backups

All restore scripts automatically create a safety backup:

```
backups/<service>/pre_restore_<timestamp>.<ext>
```

**Retention**: 7 days (automatically cleaned up)

### Confirmation Prompts

Interactive restore requires typing "YES":

```bash
$ ./tools/backup/restore-postgres.sh backup.sql.gz
Type 'YES' to confirm restore: yes  # ❌ Not accepted
Type 'YES' to confirm restore: YES  # ✅ Accepted
```

### Non-Interactive Protection

Automation requires explicit environment variable:

```bash
# This will fail (protection against accidents)
./tools/backup/restore-postgres.sh backup.sql.gz

# This will succeed (explicit intent)
FORCE_RESTORE=1 ./tools/backup/restore-postgres.sh backup.sql.gz
```

---

## Performance Optimization

### Parallel Decompression

For large backups, use parallel decompression:

```bash
# Install pigz (parallel gzip)
apt-get install pigz

# Decompress with pigz
pigz -dc backup.sql.gz | psql ...
```

### Restore Priority Order

Restore critical services first:

1. **PostgreSQL** - Required for authentication and core functionality
2. **Qdrant** - Required for RAG system
3. **Redis** - Sessions can be recreated, lower priority
4. **Volumes** - Monitoring data, lowest priority

### Network Optimization

For remote backups:

```bash
# Download from S3 with parallel streams
aws s3 cp --only-show-errors s3://bucket/backup.sql.gz - | gunzip | psql ...

# Use faster compression for network transfers
zstd -d backup.sql.zst | psql ...
```

---

## Post-Restore Actions

### Immediate Actions (< 5 minutes)

1. **Verify service health**: All containers running
2. **Check API endpoints**: /health returns 200
3. **Test authentication**: Login with test user
4. **Verify RAG system**: Test chat functionality
5. **Check logs**: No error spikes

### Short-Term Actions (< 1 hour)

1. **Monitor error rates**: Check Grafana dashboards
2. **Verify data integrity**: Spot-check database records
3. **Test critical workflows**: User registration, chat, PDF upload
4. **Check background jobs**: n8n workflows running
5. **Update status page**: Inform users of recovery

### Follow-Up Actions (< 24 hours)

1. **Root cause analysis**: Identify what caused data loss
2. **Update procedures**: Document lessons learned
3. **Improve backups**: Adjust frequency or retention if needed
4. **Team debrief**: Share findings and improvements
5. **Incident report**: Document timeline and resolution

---

## Escalation Contacts

### On-Call Rotation

- **Primary**: DevOps on-call (PagerDuty)
- **Secondary**: Backend team lead
- **Escalation**: Engineering Manager
- **Final**: CTO

### Communication Channels

- **Slack**: #incidents (high-priority alerts)
- **Email**: incidents@meepleai.dev
- **Status Page**: status.meepleai.dev (public updates)

---

## References

- [Backup Strategy](./backup-strategy.md)
- [Disaster Recovery Plan](../deployment/disaster-recovery.md)
- [Backup Scripts README](../../../tools/backup/README.md)
- [PostgreSQL Recovery](https://www.postgresql.org/docs/current/backup-dump.html)
- [Qdrant Snapshots](https://qdrant.tech/documentation/concepts/snapshots/)
- [Redis Persistence](https://redis.io/docs/management/persistence/)

---

## Change Log

| Date | Version | Changes |
|------|---------|---------|
| 2025-01-08 | 1.0 | Initial restore procedures for Issue #704 |

---

**Maintained by**: DevOps Team
**Review Frequency**: Quarterly
**Next Drill**: Q1 2025 (March)
