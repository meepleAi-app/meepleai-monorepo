# Backup Strategy

**Status**: Production Ready
**Owner**: DevOps Team
**Last Updated**: 2025-01-08
**Issue**: #704

---

## Overview

Comprehensive backup strategy for MeepleAI infrastructure covering all critical data stores and services.

## Recovery Objectives

| Metric | Target | Alpha Phase | Production |
|--------|--------|-------------|------------|
| **RTO** (Recovery Time Objective) | Time to restore service | < 30 minutes | < 15 minutes |
| **RPO** (Recovery Point Objective) | Maximum data loss | < 24 hours | < 4 hours |
| **Backup Frequency** | How often backups run | Daily | Daily + Continuous |
| **Retention Period** | How long backups kept | 30 days | 90 days |

---

## Backup Scope

### Critical Data (Must Backup)

1. **PostgreSQL Database** (Primary Data Store)
   - User accounts and authentication data
   - Game catalog and rules specifications
   - Chat threads and conversations
   - API keys and OAuth accounts
   - System configuration and alerts
   - n8n workflow metadata

2. **Qdrant Vector Database** (RAG System)
   - Document embeddings (rules, FAQs)
   - Vector collections for semantic search
   - Collection metadata and indexes

3. **Redis Cache** (Session State)
   - User sessions
   - Rate limiting state
   - Cache entries (non-critical but useful)

4. **Docker Volumes** (Application Data)
   - Grafana dashboards and datasources
   - Prometheus metrics history
   - n8n workflow definitions and credentials

### Non-Critical Data (Optional Backup)

- Temporary files and build artifacts
- Log files (retained 30 days, not backed up)
- Container images (rebuil dable from source)

---

## Backup Architecture

### Storage Location

**Alpha Phase**:
- Local filesystem: `./backups/`
- Structure:
  ```
  backups/
  ├── postgres/
  ├── qdrant/
  ├── redis/
  └── volumes/
  ```

**Production Phase** (Planned):
- Primary: Local filesystem
- Secondary: S3/GCS (remote replication)
- Tertiary: Geographic redundancy

### Compression Strategy

| Age | Compression | Ratio | Restore Time | Reasoning |
|-----|-------------|-------|--------------|-----------|
| 0-7 days | gzip | ~65% | Fast (seconds) | Recent backups need fast restore |
| 7-30 days | xz | ~80% | Slower (minutes) | Archive backups prioritize space |
| > 30 days | Deleted | N/A | N/A | Retention policy |

**Automatic Recompression**: Backups older than 7 days are automatically recompressed with xz during the next backup cycle.

---

## Backup Schedule

### Daily Backups

**Time**: 02:00 UTC (off-peak hours)
**Duration**: ~60-120 seconds (sequential mode)
**Frequency**: Once per 24 hours

**Cron Schedule**:
```bash
# /etc/cron.d/meepleai-backup
0 2 * * * /path/to/meepleai-monorepo/tools/backup/backup-all.sh
```

**Systemd Timer** (Alternative):
```ini
# /etc/systemd/system/meepleai-backup.timer
[Unit]
Description=MeepleAI Daily Backup

[Timer]
OnCalendar=daily
OnCalendar=02:00
Persistent=true

[Install]
WantedBy=timers.target
```

### Weekly Verification

**Time**: Sunday 03:00 UTC
**Purpose**: Verify backup integrity and generate reports

**Cron Schedule**:
```bash
# /etc/cron.d/meepleai-verify
0 3 * * 0 /path/to/meepleai-monorepo/tools/backup/verify-backups.sh
```

---

## Retention Policy

### Backup Lifecycle

```
Day 0-7:   Fresh backups (gzip compression)
           Full retention, fast restore capability

Day 7-30:  Archive backups (xz compression)
           Space-optimized, slower restore

Day 30+:   Deleted automatically
           Exceeds retention policy
```

### Policy Justification

- **30 days**: Balances storage costs with recovery needs
- **Alpha phase**: Sufficient for development and testing
- **Production**: Will extend to 90 days with tiered storage

### Special Retention Rules

- **Pre-restore safety backups**: Kept for 7 days
- **Monthly snapshots**: First backup of month kept for 12 months (future)
- **Annual snapshots**: One backup per year kept indefinitely (future)

---

## Backup Execution

### Sequential Mode (Default)

**Advantages**:
- Lower resource usage
- More reliable
- Easier to debug
- Predictable execution time

**Execution Order**:
1. PostgreSQL (most critical)
2. Qdrant (RAG system)
3. Redis (sessions)
4. Docker Volumes (last)

**Use Case**: Standard daily backups

### Parallel Mode (Optional)

**Advantages**:
- Faster execution (~50% time reduction)
- Better resource utilization

**Considerations**:
- Higher CPU/memory usage
- Increased disk I/O
- Potential for resource contention

**Use Case**: Time-constrained backups or on-demand operations

**Command**:
```bash
./tools/backup/backup-all.sh --parallel
```

---

## Monitoring & Alerting

### Success Criteria

- All backup scripts exit with code 0
- Backup files created and non-empty
- Compression integrity verified
- Minimum size thresholds met

### Alert Conditions

| Condition | Severity | Action |
|-----------|----------|--------|
| Backup failure | High | Email + Slack alert immediately |
| No backup in 48h | Critical | Page on-call + Email + Slack |
| Disk space < 10% | High | Email alert |
| Verification failure | Medium | Email alert next day |
| File corruption detected | High | Email + Slack alert |

### Notification Channels

- **Email**: `admin@meepleai.dev`
- **Slack**: `#alerts` channel
- **PagerDuty**: Critical alerts only (production)

---

## Disk Space Management

### Storage Estimation

| Service | Daily Backup Size | 30-Day Total |
|---------|-------------------|--------------|
| PostgreSQL | ~50-200 MB | ~1.5-6 GB |
| Qdrant | ~100-500 MB | ~3-15 GB |
| Redis | ~10-50 MB | ~300 MB-1.5 GB |
| Volumes | ~50-200 MB | ~1.5-6 GB |
| **Total** | ~210-950 MB | ~6.3-28.5 GB |

**Note**: Sizes are estimates for alpha phase. Production sizes will be larger.

### Cleanup Strategy

- **Automatic cleanup**: Run during backup process
- **Manual cleanup** (emergency):
  ```bash
  # Remove backups older than 30 days
  find ./backups -type f -mtime +30 -delete

  # Remove xz backups older than 60 days
  find ./backups -name "*.xz" -mtime +60 -delete
  ```

### Disk Space Monitoring

```bash
# Check available space
df -h ./backups

# Check backup directory size
du -sh ./backups/*
```

**Alert Threshold**: < 10% free space in backup directory

---

## Security Considerations

### Access Control

- Backup scripts run as non-root user (where possible)
- Backup directories: `chmod 700` (owner only)
- Backup files: `chmod 600` (owner read/write only)
- Database credentials: Environment variables, never hardcoded

### Encryption

**Alpha Phase**:
- Encryption at rest: Filesystem-level (LUKS, BitLocker)
- In transit: Not applicable (local backups)

**Production Phase** (Planned):
- S3 encryption: Server-side encryption (SSE-S3 or SSE-KMS)
- GPG encryption: Optional for sensitive backups
- TLS in transit: For remote replication

### Audit Trail

All backup operations logged:
- Timestamp
- Operation (backup/restore/verify)
- Exit code (success/failure)
- Duration
- File size
- Checksums (future enhancement)

**Log Location**: `logs/backup/`

---

## Disaster Recovery Integration

### Recovery Priorities

1. **P0 - Critical**: PostgreSQL (authentication, core data)
2. **P1 - High**: Qdrant (RAG system functionality)
3. **P2 - Medium**: Redis (sessions can be recreated)
4. **P3 - Low**: Volumes (dashboards, metrics history)

### Recovery Scenarios

| Scenario | RTO | RPO | Procedure |
|----------|-----|-----|-----------|
| Single database corruption | < 15 min | < 24 hours | Restore from latest backup |
| Complete data loss | < 30 min | < 24 hours | Restore all services |
| Partial service failure | < 15 min | < 24 hours | Restore affected service only |
| Rollback after bad deployment | < 10 min | 0 | Use pre-deployment backup |

See [Restore Procedures](./restore-procedures.md) for detailed steps.

---

## Testing & Validation

### Quarterly Disaster Recovery Drills

**Schedule**: First Sunday of quarter (Jan, Apr, Jul, Oct)

**Procedure**:
1. Simulate data loss scenario
2. Perform full restore to test environment
3. Verify data integrity
4. Measure RTO/RPO achievement
5. Document lessons learned
6. Update procedures

**Acceptance Criteria**:
- Restore completes within RTO target
- Data loss within RPO target
- All services operational post-restore
- No critical issues discovered

### Monthly Verification

**Schedule**: Sunday after backups (weekly)

**Procedure**:
1. Run `verify-backups.sh` script
2. Check compression integrity
3. Verify file sizes
4. Confirm recent backups exist
5. Review disk space usage

### Automated Testing

**Test Suite**: `tools/backup/test-backup-restore.sh`

**Runs**:
- After any script changes (pre-commit)
- Weekly as part of verification
- Before major deployments

---

## Phase 2 Enhancements (Planned)

### Remote Backup (S3/GCS)

- Automatic upload after local backup
- Geographic redundancy
- Versioning and lifecycle policies
- Cost optimization (Glacier, Coldline)

### Continuous Backup

- PostgreSQL: WAL archiving for point-in-time recovery
- Qdrant: Incremental snapshots
- Redis: AOF (Append-Only File) replication
- RPO reduction: < 5 minutes

### Backup Encryption

- GPG encryption for sensitive data
- KMS integration (AWS KMS, Google KMS)
- Encrypted S3 uploads

### Advanced Monitoring

- Prometheus metrics integration
- Grafana dashboard for backup metrics
- Predictive disk space alerts
- Backup size trend analysis

### Automated Restore Testing

- Scheduled restore to test environment
- Automated data integrity checks
- Performance baseline comparisons

---

## Cost Analysis

### Alpha Phase

| Resource | Cost | Notes |
|----------|------|-------|
| Local disk | Included | No additional cost |
| Compute time | Minimal | 1-2 min/day |
| Network | None | Local only |
| **Total** | ~$0/month | Negligible |

### Production Phase (Estimated)

| Resource | Cost/Month | Notes |
|----------|------------|-------|
| S3 storage (100 GB) | ~$2-3 | Standard tier |
| S3 bandwidth | ~$1 | Uploads only |
| Compute time | Minimal | Same as alpha |
| **Total** | ~$3-5/month | Scalable |

---

## References

- [Restore Procedures](./restore-procedures.md)
- [Disaster Recovery Plan](../deployment/disaster-recovery.md)
- [Backup Scripts README](../../../tools/backup/README.md)
- [PostgreSQL Backup Documentation](https://www.postgresql.org/docs/current/backup.html)
- [Qdrant Snapshots](https://qdrant.tech/documentation/concepts/snapshots/)
- [Redis Persistence](https://redis.io/docs/management/persistence/)

---

## Change Log

| Date | Version | Changes |
|------|---------|---------|
| 2025-01-08 | 1.0 | Initial backup strategy for Issue #704 |

---

**Maintained by**: DevOps Team
**Review Frequency**: Quarterly
