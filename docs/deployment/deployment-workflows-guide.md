# Deployment Workflows Guide

> **Scope**: GitHub Actions deployment workflows for staging and production environments
> **Last Updated**: 2026-01-30

## Environment Strategy

| Aspect | Development | Staging | Production |
|--------|-------------|---------|------------|
| **Purpose** | Local dev | Pre-prod testing | Live traffic |
| **Infrastructure** | Docker local | VPS single server | VPS HA |
| **Domain** | `localhost:3000` | `staging.meepleai.com` | `www.meepleai.io` |
| **SSL** | No | Let's Encrypt | Let's Encrypt |
| **Deploy Trigger** | Manual | Push `main-staging` | Tag `v*.*.*` |
| **Approval** | None | None | ✅ Manual |
| **Monitoring** | Minimal | Full stack | Full + alerting |
| **Backups** | No | Daily (7d) | Hourly (30d) |

### Branch Flow
```
main-dev → PR → Merge → main-staging → Auto-deploy staging
         ↓ Test 1-7 days
main → Tag v1.2.3 → Approval → Auto-deploy production
```

---

## Staging Deployment

### Trigger Options
| Method | Command | Use Case |
|--------|---------|----------|
| **Auto** | `git push origin main-staging` | Standard promotion |
| **Manual** | Actions → Run workflow | Emergency skip tests |

### Workflow Stages
```
1. Test (5-10min)     → Full CI suite (skip_tests=false)
2. Build (10-15min)   → Docker images + GHCR push (tag: staging-YYYYMMDD-SHA)
3. Deploy (2-5min)    → SSH + docker compose up -d --no-deps
4. Validate (1-2min)  → Health checks + smoke tests
5. Notify             → Slack + GitHub deployment summary
```

### Health Validation
```bash
# Auto-executed in workflow
curl -sf https://staging.meepleai.com/health
curl -sf https://staging.meepleai.com/api/v1/games?limit=1

# Manual checklist
□ API 200 OK
□ Auth works (login/logout)
□ Critical features functional
□ No console errors
□ Grafana metrics populated
```

---

## Production Deployment

### Pre-Release Checklist
```
□ Staging stable ≥24h
□ No critical bugs
□ DB migrations tested
□ Documentation updated (CHANGELOG.md)
□ Team notified
□ Rollback plan ready
□ Monitoring dashboards configured
```

### Release Process
```bash
# 1. Verify staging
curl -sf https://staging.meepleai.com/health

# 2. Update changelog
# CHANGELOG.md → Document features/fixes/breaking changes

# 3. Create tag
git checkout main && git merge main-staging && git pull
git tag -a v1.2.3 -m "Release v1.2.3: Features summary"
git push origin v1.2.3

# 4. Monitor GitHub Actions → Approve deployment
# 5. Validate production → Monitor Grafana
```

### Production Workflow Stages
```
1. Staging Check (2min)     → Verify staging health
2. Tests (10-15min)         → Full CI + security scan
3. Build (10-15min)         → Images with v1.2.3 + latest tags
4. Approval Gate (manual)   → Manual approval required
5. Deploy (5-10min)         → Blue-green deployment
   ├─ DB backup
   ├─ Scale up (api=2)
   ├─ Run migrations
   ├─ Switch traffic
   └─ Scale down (api=1)
6. Validate (5min)          → Health + smoke + perf tests
7. Rollback (if fails)      → Auto-trigger or manual
8. Notify                   → Slack + GitHub release
```

### Blue-Green Deployment
```bash
# Zero-downtime strategy
docker pull new_image:v1.2.3
docker compose up -d --scale api=2        # Both running
sleep 30 && curl http://new:8080/health   # Verify new
docker compose exec api dotnet ef database update
docker compose up -d --no-deps web        # Switch traffic
docker compose up -d --scale api=1        # Remove old
```

---

## Rollback Procedures

### Decision Tree
```
Deployment failed?
├─ Health check failed during deploy → Auto-rollback (keeps old)
├─ Validation failed after deploy → Manual re-deploy previous version
├─ DB migration failed → Restore DB backup + rollback code
└─ Issues discovered later → Emergency rollback + incident response
```

### Rollback Methods

| Scenario | Method | Command |
|----------|--------|---------|
| **During deploy** | Auto blue-green | Workflow keeps old container |
| **After deploy** | Re-run previous | Actions → Re-run v1.2.2 workflow |
| **Manual SSH** | Docker pull | `docker pull api:v1.2.2 && compose up -d` |
| **DB rollback** | Restore backup | `psql < /backups/pre-deploy.sql` |

### Database Rollback
```bash
docker compose stop api                                    # Stop writes
docker compose exec -T postgres psql -U postgres meepleai \
  < /backups/pre-deploy-20260130.sql                      # Restore
docker pull ghcr.io/owner/repo/api:v1.2.2                 # Previous image
docker compose up -d api                                   # Restart
curl -sf http://localhost:8080/health                      # Verify
```

---

## Hotfix Process

### Urgent Production Bug
```bash
# 1. Create hotfix branch
git checkout -b hotfix/v1.2.4 main

# 2. Apply fix + test locally
dotnet test && pnpm test

# 3. Quick staging validation (skip full tests)
git push origin hotfix/v1.2.4
# Manual workflow → skip_tests=true → Deploy staging

# 4. Validate 15min on staging
curl https://staging.meepleai.com/health

# 5. Tag and deploy
git checkout main && git merge hotfix/v1.2.4
git tag -a v1.2.4 -m "Hotfix v1.2.4: Critical bug"
git push origin v1.2.4

# 6. Expedited approval + monitor closely
```

---

## Monitoring & Validation

### Health Endpoints
```bash
# API
curl https://api.meepleai.io/health
# → {"status":"Healthy","version":"1.2.3","timestamp":"..."}

# Database/Redis/Qdrant
curl https://api.meepleai.io/health/database  # → 12ms
curl https://api.meepleai.io/health/redis     # → 3ms
curl https://api.meepleai.io/health/qdrant    # → 8ms
```

### Smoke Tests (Auto Post-Deploy)
```bash
smoke_tests=(
  "GET /api/v1/health"
  "POST /api/v1/auth/login"
  "GET /api/v1/games?limit=10"
  "POST /api/v1/games/search"
  "GET /api/v1/users/me"
)
```

### Alert Thresholds
| Alert | Condition | Severity |
|-------|-----------|----------|
| **High Error Rate** | `error_rate > 1%` | Critical |
| **Slow Response** | `p95 > 2000ms` | Warning |
| **DB Down** | `pg_up == 0` | Critical |
| **High CPU** | `cpu > 80%` | Warning |

---

## Incident Response

### Severity Classification
| Level | Description | Response Time |
|-------|-------------|---------------|
| **P0** | Complete outage | Immediate (all hands) |
| **P1** | Partial outage | 15min (on-call) |
| **P2** | Degraded perf | 2h (monitor) |

### Response Protocol
```
1. Assess → Check health + Grafana + logs
2. Mitigate → Rollback OR scale OR restart
3. Communicate → Status page + users + stakeholders
4. Resolve → Deploy fix + verify
5. Post-incident → RCA + post-mortem + action items
```

---

## Quick Reference

### Common Commands
```bash
# Staging deploy
git checkout main-staging && git merge main-dev && git push

# Production deploy
git tag -a v1.2.3 -m "..." && git push origin v1.2.3

# Hotfix
git checkout -b hotfix/v1.2.4 main → fix → tag → push

# Health check
curl -sf https://www.meepleai.io/health && echo "✅" || echo "❌"

# Rollback
# Re-run previous workflow OR docker pull api:v1.2.2
```

### Workflow Files
- `.github/workflows/deploy-staging.yml` - Auto on `main-staging` push
- `.github/workflows/deploy-production.yml` - Auto on `v*` tag + approval gate
- `.github/workflows/ci.yml` - Test suite (reused by both)

---

**See Also**: [Docker Versioning](./docker-versioning-guide.md) | [Rollback Guide](./rollback-disaster-recovery.md) | [Monitoring Setup](./monitoring-setup-guide.md)
