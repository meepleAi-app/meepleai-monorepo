# Administrator Guide - MeepleAI

**Audience**: System administrators, site reliability engineers, and operations staff.

## 📋 Table of Contents

1. [Administrator Overview](#administrator-overview)
2. [Access & Permissions](#access--permissions)
3. [System Monitoring](#system-monitoring)
4. [User Management](#user-management)
5. [Configuration Management](#configuration-management)
6. [Performance Tuning](#performance-tuning)
7. [Maintenance Tasks](#maintenance-tasks)
8. [Incident Response](#incident-response)
9. [Security Operations](#security-operations)
10. [Troubleshooting](#troubleshooting)
11. [Runbooks](#runbooks)

## 🎯 Administrator Overview

### Administrator Responsibilities

- Monitor system health and performance
- Manage user accounts and permissions
- Configure system settings and feature flags
- Respond to incidents and outages
- Perform routine maintenance
- Ensure security and compliance
- Manage backups and disaster recovery

### Admin Access Levels

| Role | Access | Permissions |
|------|--------|-------------|
| **Super Admin** | Full system access | All operations |
| **Admin** | Most operations | User mgmt, config, monitoring |
| **Editor** | Content management | Games, PDFs, knowledge base |
| **Support** | Read-only + tickets | View logs, assist users |

### Admin UI

**Access**: `https://meepleai.dev/admin` or `http://localhost:3000/admin`

**Login**: Use admin account credentials

**Features**:
- Dashboard with key metrics
- User management
- Configuration editor
- Monitoring dashboards
- Alert management
- System logs

## 🔐 Access & Permissions

### Initial Admin Account

**Created on first startup** using environment variables:

```env
INITIAL_ADMIN_EMAIL=admin@meepleai.dev
INITIAL_ADMIN_PASSWORD=Admin123!
```

**First Login**:
1. Navigate to `/admin`
2. Login with initial credentials
3. **Change password immediately**

### Creating Admin Users

**Via API**:
```bash
curl -X POST http://localhost:8080/api/v1/admin/users \
  -H "Content-Type: application/json" \
  -H "Cookie: your-admin-session" \
  -d '{
    "email": "newadmin@meepleai.dev",
    "password": "SecurePass123!",
    "role": "Admin"
  }'
```

**Via Database** (emergency only):
```sql
-- Connect to database
psql -h localhost -U postgres -d meepleai

-- Create admin user
INSERT INTO users (email, password_hash, role, created_at)
VALUES (
  'admin@meepleai.dev',
  '$2a$11$hashed_password_here',  -- Use bcrypt
  'Admin',
  NOW()
);
```

### Role-Based Access Control (RBAC)

**Roles**:
- `SuperAdmin`: All permissions
- `Admin`: Most administrative functions
- `Editor`: Content management
- `User`: Regular user access

**Permissions**:
- `users.manage`: Create/edit/delete users
- `config.manage`: Modify system configuration
- `games.manage`: Add/edit/delete games
- `pdfs.manage`: Upload/delete PDFs
- `alerts.manage`: Configure alerts
- `logs.view`: View system logs

## 📊 System Monitoring

### Health Dashboard

**Access**: `/admin/health`

**Key Metrics**:
- System uptime
- API response time (P50, P95, P99)
- Error rate
- Active users
- Database performance
- Cache hit rate
- Queue depth
- Resource utilization (CPU, memory, disk)

### Service Health Checks

**API Health**:
```bash
curl http://localhost:8080/health
```

**Expected Response**:
```json
{
  "status": "Healthy",
  "checks": {
    "postgres": "Healthy",
    "redis": "Healthy",
    "qdrant": "Healthy",
    "openrouter": "Healthy"
  },
  "duration": "00:00:00.123",
  "timestamp": "2025-11-15T10:30:00Z"
}
```

**Component Health**:
```bash
# PostgreSQL
docker exec -it postgres pg_isready

# Redis
docker exec -it redis redis-cli ping

# Qdrant
curl http://localhost:6333/healthz

# n8n
curl http://localhost:5678/healthz
```

### Monitoring Tools

**Seq (Logs)**: `http://localhost:8081`
- Structured logging
- Query and filter logs
- Create alerts
- Export logs

**Jaeger (Traces)**: `http://localhost:16686`
- Distributed tracing
- Performance analysis
- Dependency visualization
- Latency tracking

**Prometheus (Metrics)**: `http://localhost:9090`
- Time-series metrics
- Custom queries (PromQL)
- Alert rules
- Service discovery

**Grafana (Dashboards)**: `http://localhost:3001`
- Visual dashboards
- Multiple data sources
- Alert visualization
- Reports

### Key Performance Indicators (KPIs)

| Metric | Target | Warning | Critical |
|--------|--------|---------|----------|
| **Uptime** | >99.5% | <99% | <95% |
| **P95 Response Time** | <2s | >3s | >5s |
| **Error Rate** | <1% | >2% | >5% |
| **CPU Usage** | <70% | >80% | >90% |
| **Memory Usage** | <80% | >90% | >95% |
| **Disk Usage** | <75% | >85% | >95% |
| **Cache Hit Rate** | >80% | <70% | <50% |

## 👥 User Management

### View All Users

**Admin UI**: Navigate to `/admin/users`

**API**:
```bash
curl http://localhost:8080/api/v1/admin/users \
  -H "Cookie: your-admin-session"
```

**Database**:
```sql
SELECT id, email, role, created_at, last_login_at, is_active
FROM users
ORDER BY created_at DESC;
```

### User Details

**View user profile**:
```sql
SELECT
  u.id,
  u.email,
  u.role,
  u.created_at,
  u.last_login_at,
  COUNT(DISTINCT cs.id) as chat_sessions,
  COUNT(DISTINCT ak.id) as api_keys
FROM users u
LEFT JOIN chat_sessions cs ON u.id = cs.user_id
LEFT JOIN api_keys ak ON u.id = ak.user_id
WHERE u.id = 123
GROUP BY u.id;
```

### Create User

**Admin UI**: `/admin/users/create`

**API**:
```bash
curl -X POST http://localhost:8080/api/v1/admin/users \
  -H "Content-Type: application/json" \
  -H "Cookie: your-admin-session" \
  -d '{
    "email": "user@example.com",
    "password": "TempPass123!",
    "role": "User",
    "sendWelcomeEmail": true
  }'
```

### Update User

**Change role**:
```sql
UPDATE users SET role = 'Admin' WHERE id = 123;
```

**Disable user**:
```sql
UPDATE users SET is_active = false WHERE id = 123;
```

**Reset password** (generate reset token):
```bash
curl -X POST http://localhost:8080/api/v1/admin/users/123/reset-password \
  -H "Cookie: your-admin-session"
```

### Delete User

**Soft delete** (recommended):
```sql
UPDATE users SET is_active = false, deleted_at = NOW() WHERE id = 123;
```

**Hard delete** (permanent):
```sql
-- Delete user data (cascade)
DELETE FROM chat_sessions WHERE user_id = 123;
DELETE FROM api_keys WHERE user_id = 123;
DELETE FROM oauth_accounts WHERE user_id = 123;
DELETE FROM users WHERE id = 123;
```

### User Sessions

**View active sessions**:
```sql
SELECT
  s.id,
  u.email,
  s.ip_address,
  s.user_agent,
  s.created_at,
  s.expires_at
FROM sessions s
JOIN users u ON s.user_id = u.id
WHERE s.expires_at > NOW()
ORDER BY s.created_at DESC;
```

**Revoke session**:
```sql
DELETE FROM sessions WHERE id = 'session-id';
```

**Revoke all user sessions** (force logout):
```sql
DELETE FROM sessions WHERE user_id = 123;
```

## ⚙️ Configuration Management

### Configuration Interface

**Admin UI**: `/admin/configuration`

**Features**:
- View all configuration settings
- Edit settings by category
- Version history
- Rollback support
- Bulk import/export

### Configuration Categories

1. **Features**: Feature flags and toggles
2. **RateLimit**: API rate limiting
3. **AI/LLM**: AI model settings
4. **RAG**: Retrieval-augmented generation
5. **PDF**: PDF processing settings
6. **Email**: Email service configuration
7. **Storage**: File storage settings

### Common Configuration Tasks

**Enable/Disable Features**:
```bash
curl -X PUT http://localhost:8080/api/v1/admin/config/features/enable-2fa \
  -H "Content-Type: application/json" \
  -H "Cookie: your-admin-session" \
  -d '{"value": true}'
```

**Update Rate Limits**:
```bash
curl -X PUT http://localhost:8080/api/v1/admin/config/ratelimit/requests-per-minute \
  -H "Content-Type: application/json" \
  -H "Cookie: your-admin-session" \
  -d '{"value": 100}'
```

**Configure AI Models**:
```bash
curl -X PUT http://localhost:8080/api/v1/admin/config/ai/primary-model \
  -H "Content-Type: application/json" \
  -H "Cookie: your-admin-session" \
  -d '{"value": "anthropic/claude-3.5-sonnet"}'
```

### Configuration Backup

**Export all settings**:
```bash
curl http://localhost:8080/api/v1/admin/config/export \
  -H "Cookie: your-admin-session" \
  > config-backup-$(date +%Y%m%d).json
```

**Import settings**:
```bash
curl -X POST http://localhost:8080/api/v1/admin/config/import \
  -H "Content-Type: application/json" \
  -H "Cookie: your-admin-session" \
  -d @config-backup-20251115.json
```

### Rollback Configuration

**Via API**:
```bash
curl -X POST http://localhost:8080/api/v1/admin/config/rollback/version-123 \
  -H "Cookie: your-admin-session"
```

**Via Database**:
```sql
-- View configuration history
SELECT * FROM configuration_history ORDER BY created_at DESC LIMIT 10;

-- Restore previous version
UPDATE configuration
SET value = (
  SELECT value FROM configuration_history
  WHERE key = 'features.enable-2fa'
  AND version = 123
)
WHERE key = 'features.enable-2fa';
```

## ⚡ Performance Tuning

### Database Optimization

**Analyze Query Performance**:
```sql
-- Slow queries
SELECT
  query,
  calls,
  total_time,
  mean_time,
  max_time
FROM pg_stat_statements
ORDER BY mean_time DESC
LIMIT 20;

-- Missing indexes
SELECT
  schemaname,
  tablename,
  attname,
  null_frac,
  avg_width,
  n_distinct
FROM pg_stats
WHERE schemaname = 'public'
ORDER BY null_frac DESC;
```

**Add Indexes**:
```sql
-- Add index for common queries
CREATE INDEX idx_users_email ON users(email);
CREATE INDEX idx_chat_sessions_user_id ON chat_sessions(user_id);
CREATE INDEX idx_games_title ON games(title);

-- Full-text search index
CREATE INDEX idx_rule_specs_content_fts ON rule_specs USING GIN(to_tsvector('english', content));
```

**Vacuum and Analyze**:
```bash
# Connect to database
docker exec -it postgres psql -U postgres -d meepleai

# Vacuum
VACUUM ANALYZE;

# Vacuum specific table
VACUUM ANALYZE users;
```

### Cache Optimization

**Redis Cache Stats**:
```bash
docker exec -it redis redis-cli INFO stats
```

**Monitor Cache Hit Rate**:
```bash
# Should be >80%
docker exec -it redis redis-cli INFO stats | grep keyspace_hits
docker exec -it redis redis-cli INFO stats | grep keyspace_misses
```

**Clear Cache**:
```bash
# Clear all cache (use with caution!)
docker exec -it redis redis-cli FLUSHALL

# Clear specific pattern
docker exec -it redis redis-cli KEYS "user:*" | xargs docker exec -it redis redis-cli DEL
```

**Configure Cache Settings**:
```env
HYBRID_CACHE__DEFAULT_EXPIRATION_MINUTES=5
HYBRID_CACHE__MAX_CACHE_SIZE_MB=1024
```

### Connection Pooling

**Configure Pool Size**:
```env
CONNECTION_POOL__MIN=10
CONNECTION_POOL__MAX=100
CONNECTION_POOL__IDLE_TIMEOUT_SECONDS=300
```

**Monitor Connections**:
```sql
-- Active connections
SELECT count(*) FROM pg_stat_activity WHERE state = 'active';

-- Connection pool stats
SELECT
  datname,
  numbackends,
  xact_commit,
  xact_rollback
FROM pg_stat_database
WHERE datname = 'meepleai';
```

### Resource Limits

**Docker Compose**:
```yaml
services:
  api:
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 4G
        reservations:
          cpus: '1'
          memory: 2G
```

**Kubernetes**:
```yaml
resources:
  requests:
    memory: "2Gi"
    cpu: "1"
  limits:
    memory: "4Gi"
    cpu: "2"
```

## 🔧 Maintenance Tasks

### Daily Tasks

**Health Check**:
```bash
# Run daily at 9 AM
#!/bin/bash
curl -f http://localhost:8080/health || echo "API health check failed" | mail -s "MeepleAI Alert" admin@meepleai.dev
```

**Review Logs**:
- Check Seq for errors: `@Level = 'Error'`
- Review API errors
- Check authentication failures
- Monitor rate limit violations

**Monitor Metrics**:
- Check Grafana dashboards
- Verify all metrics within thresholds
- Review alerts

### Weekly Tasks

**Backup Verification**:
```bash
# Verify latest backup
aws s3 ls s3://meepleai-backups/postgres/ --recursive | tail -1

# Test restore (to separate database)
./scripts/test-restore.sh
```

**Security Updates**:
```bash
# Check for vulnerabilities
cd apps/api
dotnet list package --vulnerable

cd apps/web
pnpm audit --audit-level=high

# Update dependencies if needed
dotnet add package <PackageName>
pnpm update <package-name>
```

**Performance Review**:
- Review slow query report
- Check cache hit rates
- Analyze resource utilization trends
- Identify optimization opportunities

### Monthly Tasks

**Disk Cleanup**:
```bash
# Clean up old logs (>30 days)
find /var/log/meepleai -name "*.log" -mtime +30 -delete

# Clean up old backups (>30 days)
find /backups/postgres -name "*.sql.gz" -mtime +30 -delete

# Docker cleanup
docker system prune -af --volumes

# Clear old cache data
bash tools/cleanup-caches.sh
```

**Certificate Renewal**:
```bash
# Check certificate expiration
echo | openssl s_client -servername meepleai.dev -connect meepleai.dev:443 2>/dev/null | openssl x509 -noout -dates

# Renew if needed (Let's Encrypt auto-renews)
certbot renew --dry-run
```

**Capacity Planning**:
- Review resource usage trends
- Project future needs
- Plan for scaling
- Budget for infrastructure

### Quarterly Tasks

**Disaster Recovery Test**:
- Restore from backup
- Verify all services
- Document recovery time
- Update runbooks

**Security Audit**:
- Review access logs
- Audit user permissions
- Check for unauthorized access
- Update security policies

**Performance Baseline**:
- Run load tests
- Document performance metrics
- Compare with previous quarter
- Identify degradation

## 🚨 Incident Response

### Incident Severity Levels

| Level | Description | Response Time | Escalation |
|-------|-------------|---------------|------------|
| **P0 - Critical** | System down, data loss | 15 minutes | Immediate |
| **P1 - High** | Major feature broken | 1 hour | 2 hours |
| **P2 - Medium** | Minor feature broken | 4 hours | 8 hours |
| **P3 - Low** | Cosmetic issue | 24 hours | 48 hours |

### Incident Response Process

**1. Detect**:
- Monitor alerts
- User reports
- Health checks

**2. Assess**:
- Determine severity
- Identify impact
- Estimate affected users

**3. Respond**:
- Acknowledge incident
- Form response team
- Begin investigation

**4. Mitigate**:
- Apply immediate fixes
- Implement workarounds
- Communicate status

**5. Resolve**:
- Deploy permanent fix
- Verify resolution
- Monitor stability

**6. Post-Mortem**:
- Document incident
- Identify root cause
- Create action items
- Update runbooks

### Common Incidents

See [Runbooks](#runbooks) section below.

## 🔒 Security Operations

### Security Monitoring

**Failed Login Attempts**:
```sql
SELECT
  email,
  ip_address,
  COUNT(*) as attempts,
  MAX(attempted_at) as last_attempt
FROM failed_logins
WHERE attempted_at > NOW() - INTERVAL '1 hour'
GROUP BY email, ip_address
HAVING COUNT(*) > 5
ORDER BY attempts DESC;
```

**Suspicious Activity**:
```sql
-- Multiple users from same IP
SELECT
  ip_address,
  COUNT(DISTINCT user_id) as user_count
FROM sessions
WHERE created_at > NOW() - INTERVAL '1 day'
GROUP BY ip_address
HAVING COUNT(DISTINCT user_id) > 5;

-- Unusual API usage
SELECT
  user_id,
  COUNT(*) as request_count
FROM api_requests
WHERE created_at > NOW() - INTERVAL '1 hour'
GROUP BY user_id
HAVING COUNT(*) > 1000;
```

### Access Logs

**Review access logs**:
```bash
# Seq query
@MessageTemplate LIKE '%login%'

# Nginx access logs
tail -f /var/log/nginx/access.log

# API access logs
docker compose logs -f api | grep "POST /api/v1/auth"
```

### Security Patches

**Apply security updates**:
```bash
# Backend
cd apps/api
dotnet list package --vulnerable
dotnet add package <PackageName> --version <SafeVersion>

# Frontend
cd apps/web
pnpm audit fix

# Docker images
docker pull postgres:17
docker pull redis:7-alpine
docker pull qdrant/qdrant:latest
```

### Penetration Testing

**Schedule**: Annually or after major releases

**Tools**:
- OWASP ZAP
- Burp Suite
- Nmap
- SQLMap

**Process**:
1. Engage security firm
2. Define scope
3. Conduct tests
4. Review findings
5. Remediate issues
6. Re-test

## 🐛 Troubleshooting

### Service Won't Start

**Check logs**:
```bash
docker compose logs api
docker compose logs postgres
```

**Check dependencies**:
```bash
# Verify all services running
docker compose ps

# Check health
curl http://localhost:8080/health
```

**Common causes**:
- Database not ready (wait for migrations)
- Missing environment variables
- Port conflicts
- Insufficient resources

### High CPU Usage

**Identify process**:
```bash
# Docker
docker stats

# System
top
htop
```

**Common causes**:
- Inefficient queries
- Infinite loops
- Too many concurrent requests
- Lack of caching

**Mitigation**:
- Scale horizontally
- Optimize queries
- Add caching
- Rate limiting

### High Memory Usage

**Check memory usage**:
```bash
docker stats
free -h
```

**Common causes**:
- Memory leaks
- Large caches
- Too many connections
- Large datasets in memory

**Mitigation**:
- Restart service
- Reduce cache size
- Optimize queries
- Scale vertically

### Database Performance

**Slow queries**:
```sql
-- Enable query logging
ALTER DATABASE meepleai SET log_min_duration_statement = 1000;

-- View slow queries
SELECT * FROM pg_stat_statements ORDER BY mean_time DESC LIMIT 10;
```

**Mitigation**:
- Add indexes
- Optimize queries
- Increase connection pool
- Vacuum database

### Cache Issues

**Low hit rate**:
```bash
# Check hit rate
docker exec -it redis redis-cli INFO stats | grep keyspace_hits

# Should be >80%
```

**Mitigation**:
- Increase cache TTL
- Warm cache on startup
- Add more cache keys
- Increase cache size

## 📚 Runbooks

### Runbook: Database Connection Failure

**Symptoms**:
- API health check fails
- "Cannot connect to database" errors
- Users cannot login

**Diagnosis**:
```bash
# Check if Postgres is running
docker ps | grep postgres

# Check connection
psql -h localhost -U postgres -d meepleai

# Check logs
docker compose logs postgres
```

**Resolution**:
```bash
# Restart Postgres
docker compose restart postgres

# Verify connection
curl http://localhost:8080/health

# If still failing, check credentials
cat infra/env/.env.dev | grep Postgres
```

**Prevention**:
- Monitor database health
- Set up connection pool
- Configure automatic restart

### Runbook: High Error Rate

**Symptoms**:
- Error rate >5%
- Alerts firing
- User complaints

**Diagnosis**:
```bash
# Check error logs (Seq)
# Query: @Level = 'Error' AND @Timestamp > DateAdd(Now(), -1, 'h')

# Check error rate (Prometheus)
rate(http_requests_total{status=~"5.."}[5m])

# Identify error patterns
docker compose logs api | grep ERROR
```

**Resolution**:
```bash
# If specific endpoint failing
# - Check dependencies
# - Review recent deployments
# - Rollback if needed

# If all endpoints failing
docker compose restart api

# Monitor recovery
watch curl http://localhost:8080/health
```

**Prevention**:
- Comprehensive testing
- Gradual rollouts
- Circuit breakers
- Rate limiting

### Runbook: Out of Disk Space

**Symptoms**:
- "No space left on device" errors
- Database writes failing
- Application crashes

**Diagnosis**:
```bash
# Check disk usage
df -h

# Find large files
du -sh /* | sort -h

# Check Docker volumes
docker system df
```

**Resolution**:
```bash
# Clean up logs
find /var/log -name "*.log" -mtime +7 -delete

# Clean up Docker
docker system prune -af --volumes

# Clean up backups
find /backups -name "*.sql.gz" -mtime +30 -delete

# Extend volume (if cloud)
aws ec2 modify-volume --volume-id vol-xxx --size 200
```

**Prevention**:
- Monitor disk usage
- Set up log rotation
- Automate cleanup
- Plan capacity

### Runbook: Memory Leak

**Symptoms**:
- Increasing memory usage over time
- OOM (Out of Memory) errors
- Service crashes

**Diagnosis**:
```bash
# Monitor memory over time
docker stats --no-stream

# Check for memory leaks (use profiler)
dotnet-trace collect --process-id <pid>
dotnet-dump collect --process-id <pid>
```

**Resolution**:
```bash
# Short term: Restart service
docker compose restart api

# Long term: Fix code
# - Review recent changes
# - Use memory profiler
# - Identify leaking resources
```

**Prevention**:
- Dispose resources properly
- Use `using` statements
- Monitor memory trends
- Load testing

## 📞 Escalation Contacts

| Issue | Contact | Method |
|-------|---------|--------|
| **Critical Outage** | On-call Engineer | PagerDuty |
| **Security Incident** | Security Team | security@meepleai.dev |
| **Data Loss** | Database Admin | dba@meepleai.dev |
| **Infrastructure** | DevOps Lead | devops@meepleai.dev |

## 📚 Additional Resources

- **[Deployment Guide](./04-deployment-guide.md)** - Deployment procedures
- **[Architecture Guide](./06-architecture-guide.md)** - System architecture
- **[Main Documentation](../docs/INDEX.md)** - Complete documentation
- **Seq**: http://localhost:8081
- **Jaeger**: http://localhost:16686
- **Grafana**: http://localhost:3001

---

**Version**: 1.0-rc
**Last Updated**: 2025-11-15
**For**: System Administrators
