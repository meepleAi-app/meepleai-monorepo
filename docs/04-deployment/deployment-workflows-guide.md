# Deployment Workflows Guide

> **Scope**: Complete guide to staging and production deployment workflows with GitHub Actions
> **Last Updated**: 2026-01-30

---

## Table of Contents

1. [Overview](#overview)
2. [Environment Strategy](#environment-strategy)
3. [Staging Deployment](#staging-deployment)
4. [Production Deployment](#production-deployment)
5. [Release Process](#release-process)
6. [Rollback Procedures](#rollback-procedures)
7. [Monitoring & Validation](#monitoring--validation)
8. [Emergency Procedures](#emergency-procedures)

---

## Overview

### Deployment Pipeline Architecture

```
┌────────────────────────────────────────────────────────────────────┐
│                        DEVELOPMENT WORKFLOW                         │
├────────────────────────────────────────────────────────────────────┤
│  Developer                                                          │
│    ├─ Feature Branch: feature/issue-123-add-game-session          │
│    ├─ Local Testing: Unit + Integration tests                      │
│    ├─ Create PR → main-dev                                        │
│    └─ CI Checks: Backend + Frontend + E2E tests                   │
│                                                                     │
│  Code Review                                                        │
│    ├─ Automated: Linting, TypeCheck, Security Scan                │
│    ├─ Manual: Team review and approval                            │
│    └─ Merge → main-dev                                            │
└────────────────────────────────────────────────────────────────────┘
                                  │
                                  ▼
┌────────────────────────────────────────────────────────────────────┐
│                         STAGING WORKFLOW                            │
├────────────────────────────────────────────────────────────────────┤
│  Trigger: Push to main-staging OR manual workflow_dispatch         │
│                                                                     │
│  Pre-Deploy (5-10 min)                                             │
│    ├─ Run Full CI Test Suite (unless skip_tests=true)             │
│    ├─ Backend: Unit + Integration + E2E tests                     │
│    └─ Frontend: Unit + E2E + Visual tests                         │
│                                                                     │
│  Build (10-15 min)                                                 │
│    ├─ Generate Version: staging-YYYYMMDD-SHA                      │
│    ├─ Build API: Docker multi-stage build with caching            │
│    ├─ Build Web: Next.js production build                         │
│    ├─ Tag Images: staging-20260130-a1b2c3d, staging-latest        │
│    └─ Push to GHCR: GitHub Container Registry                     │
│                                                                     │
│  Deploy (2-5 min)                                                  │
│    ├─ SSH to Staging Server                                       │
│    ├─ Pull New Images                                             │
│    ├─ Update docker-compose.staging.yml                           │
│    ├─ Zero-Downtime Deploy: docker compose up -d --no-deps        │
│    ├─ Run Migrations: dotnet ef database update                   │
│    └─ Health Check: curl staging.meepleai.com/health              │
│                                                                     │
│  Validate (1-2 min)                                                │
│    ├─ API Health: /health endpoint + 30 retries                   │
│    ├─ Web Health: HTTP 200 check                                  │
│    └─ Smoke Tests: Critical path validation                       │
│                                                                     │
│  Notify                                                            │
│    ├─ Slack: Deployment status + version                          │
│    └─ GitHub: Deployment summary                                  │
│                                                                     │
│  Verification Period: 1-7 days manual testing                      │
└────────────────────────────────────────────────────────────────────┘
                                  │
                                  ▼
┌────────────────────────────────────────────────────────────────────┐
│                       PRODUCTION WORKFLOW                           │
├────────────────────────────────────────────────────────────────────┤
│  Trigger: Git tag v*.*.* OR manual workflow_dispatch               │
│                                                                     │
│  1. STAGING CHECK (2 min)                                          │
│    ├─ Verify Staging Health: curl staging.meepleai.com/health     │
│    ├─ Compare Versions: Ensure staging tested                     │
│    └─ Skip if: skip_staging_check=true (hotfix only)              │
│                                                                     │
│  2. PRE-PRODUCTION TESTS (10-15 min)                               │
│    ├─ Full CI Suite: Backend + Frontend + E2E                     │
│    ├─ Security Scan: Dependencies + Container scan                │
│    └─ Performance Baseline: Load test if needed                   │
│                                                                     │
│  3. BUILD (10-15 min)                                              │
│    ├─ Determine Version: v1.2.3 from git tag                      │
│    ├─ Build API: Production optimized                             │
│    ├─ Build Web: Production bundle                                │
│    ├─ Tag Images: v1.2.3, latest                                  │
│    └─ Push to GHCR: Immutable production tags                     │
│                                                                     │
│  4. APPROVAL GATE (manual, no timeout)                             │
│    ├─ Environment: production-approval                            │
│    ├─ Required: Manual approval from authorized user              │
│    └─ Review: Version, changes, rollback plan                     │
│                                                                     │
│  5. DEPLOY (5-10 min)                                              │
│    ├─ SSH to Production Server                                    │
│    ├─ Pull New Images                                             │
│    ├─ Database Backup: pg_dump to /backups/                       │
│    ├─ Blue-Green Deploy:                                          │
│    │   ├─ Scale up: --scale api=2 (new + old)                    │
│    │   ├─ Health check: New container validation                 │
│    │   ├─ Run Migrations: dotnet ef database update              │
│    │   ├─ Switch Traffic: Update web container                   │
│    │   └─ Scale down: --scale api=1 (remove old)                 │
│    └─ Cleanup: docker image prune                                 │
│                                                                     │
│  6. VALIDATE (5 min)                                               │
│    ├─ Health Checks: API + Web + 30 retries                       │
│    ├─ Smoke Tests: Critical user journeys                         │
│    ├─ Performance: Response time < 2s threshold                   │
│    └─ Metrics: Prometheus + Grafana dashboards                    │
│                                                                     │
│  7. ROLLBACK (if validation fails)                                 │
│    ├─ Auto-trigger on failure                                     │
│    ├─ Manual: production-rollback environment                     │
│    └─ Restore: docker compose rollback OR re-run previous tag     │
│                                                                     │
│  8. NOTIFY                                                         │
│    ├─ Slack: Production deployment status                         │
│    ├─ GitHub Release: Auto-create with changelog                  │
│    └─ Summary: Deployment report with metrics                     │
└────────────────────────────────────────────────────────────────────┘
```

---

## Environment Strategy

### Environment Comparison

| Aspect | Development | Staging | Production |
|--------|-------------|---------|------------|
| **Purpose** | Local development | Pre-production testing | Live user traffic |
| **Infrastructure** | Docker Compose (local) | VPS/Cloud (single server) | VPS/Cloud (HA setup) |
| **Data** | Seed data, test data | Production-like data | Real user data |
| **Secrets** | `.secret` files (local) | GitHub Secrets + VPS | GitHub Secrets + VPS |
| **Domain** | `localhost:3000` | `staging.meepleai.com` | `www.meepleai.io` |
| **SSL** | No | Let's Encrypt | Let's Encrypt |
| **Monitoring** | Minimal | Full stack | Full stack + alerting |
| **Backups** | No | Daily (7 days retention) | Hourly (30 days retention) |
| **Deploy Trigger** | Manual | Push to `main-staging` | Git tag `v*.*.*` |
| **Approval** | None | None | Manual approval required |

### Branch Strategy

```
main-dev (development)
    │
    ├─ feature/issue-123-*
    ├─ fix/issue-456-*
    └─ refactor/issue-789-*
         │
         └─ PR → main-dev (CI tests, code review)
              │
              └─ Merge
                   │
                   ▼
main-staging (staging)
    │
    └─ Auto-deploy to staging.meepleai.com
         │
         │ Manual testing (1-7 days)
         ▼
main (production)
    │
    ├─ Create git tag: v1.2.3
    └─ Auto-deploy to www.meepleai.io (after approval)
```

---

## Staging Deployment

### Automatic Deployment

**Trigger**: Push to `main-staging` branch

```bash
# Promote main-dev to staging
git checkout main-staging
git merge main-dev
git push origin main-staging

# GitHub Actions automatically:
# 1. Runs tests (unless skip_tests=true)
# 2. Builds images with staging-YYYYMMDD-SHA tag
# 3. Pushes to GitHub Container Registry
# 4. Deploys to staging server via SSH
# 5. Runs health checks and smoke tests
```

### Manual Emergency Deploy

**Use Case**: Skip tests for urgent hotfix

```bash
# Via GitHub Actions UI:
# 1. Go to Actions → Deploy to Staging
# 2. Click "Run workflow"
# 3. Select branch: main-staging
# 4. Set skip_tests: true
# 5. Click "Run workflow"
```

### Workflow File

**`.github/workflows/deploy-staging.yml`**:
```yaml
name: Deploy to Staging

on:
  push:
    branches: [main-staging]
  workflow_dispatch:
    inputs:
      skip_tests:
        description: 'Skip tests (emergency only)'
        default: 'false'
        type: boolean

jobs:
  test:
    if: inputs.skip_tests != 'true'
    uses: ./.github/workflows/ci.yml

  build:
    needs: [test]
    runs-on: ubuntu-latest
    steps:
      - name: Generate Version
        run: |
          VERSION="staging-$(date +'%Y%m%d')-${GITHUB_SHA::7}"

      - name: Build and Push Images
        # ... (see docker-versioning-guide.md)

  deploy:
    needs: [build]
    runs-on: ubuntu-latest
    environment:
      name: staging
      url: https://staging.meepleai.com
    steps:
      - name: Deploy via SSH
        uses: appleboy/ssh-action@v1.0.3
        with:
          host: ${{ secrets.STAGING_HOST }}
          script: |
            cd /opt/meepleai
            docker pull ${{ needs.build.outputs.api_image }}
            docker compose up -d --no-deps api web
```

### Post-Deploy Validation

```bash
# Automated health checks (in workflow)
curl -sf https://staging.meepleai.com/health

# Manual validation checklist
# □ API endpoints respond (200 OK)
# □ Authentication works (login, register, logout)
# □ Critical features functional (games, search, PDF upload)
# □ No console errors in browser
# □ Grafana dashboards showing metrics
# □ No error alerts in monitoring
```

---

## Production Deployment

### Release Workflow

**Trigger**: Git tag `v*.*.*`

```bash
# 1. Verify staging is stable
curl -sf https://staging.meepleai.com/health

# 2. Prepare release
git checkout main
git merge main-staging
git pull origin main

# 3. Create release tag (semantic versioning)
git tag -a v1.2.3 -m "Release v1.2.3: Add game session features

Features:
- Game session creation and management
- Real-time session updates via SSE
- Session history and analytics

Bug Fixes:
- Fix JWT token expiration handling
- Improve PDF upload performance

Breaking Changes:
- None
"

# 4. Push tag (triggers production workflow)
git push origin v1.2.3

# 5. Monitor GitHub Actions
# https://github.com/owner/repo/actions

# 6. Approve deployment
# Actions → Deploy to Production → Review deployments → Approve

# 7. Monitor production
# https://grafana.meepleai.io
```

### Approval Process

**Manual Approval Required**:

1. **GitHub Environment**: `production-approval`
2. **Required Reviewers**: Repository admins only
3. **Review Checklist**:
   - ✅ Staging verified healthy
   - ✅ Version number correct
   - ✅ Changelog reviewed
   - ✅ Database migrations safe (if any)
   - ✅ Rollback plan ready
   - ✅ Team notified
   - ✅ Monitoring dashboards ready

### Blue-Green Deployment

**Zero-Downtime Strategy**:

```bash
# Current state: api-old running

# 1. Pull new image
docker pull ghcr.io/owner/repo/api:v1.2.3

# 2. Scale up (both old and new running)
docker compose up -d --no-deps --scale api=2 api

# 3. Wait for new container health check
sleep 30
curl -sf http://new-container:8080/health

# 4. Run database migrations (if needed)
docker compose exec api dotnet ef database update

# 5. Switch traffic (Traefik routes to new container)
docker compose up -d --no-deps web

# 6. Verify new container receives traffic
curl -sf https://www.meepleai.io/health

# 7. Scale down (remove old container)
docker compose up -d --no-deps --scale api=1 api

# Result: Seamless transition, no downtime
```

### Workflow File

**`.github/workflows/deploy-production.yml`**:
```yaml
name: Deploy to Production

on:
  push:
    tags: ['v*']
  workflow_dispatch:
    inputs:
      version:
        description: 'Version tag (e.g., v1.2.3)'
        type: string

concurrency:
  group: deploy-production
  cancel-in-progress: false

jobs:
  staging-check:
    runs-on: ubuntu-latest
    steps:
      - name: Verify Staging Health
        run: curl -sf https://staging.meepleai.com/health

  test:
    uses: ./.github/workflows/ci.yml

  build:
    needs: [staging-check, test]
    # ... (build images with v1.2.3 tag)

  approve:
    needs: [build]
    environment: production-approval
    # Manual approval required here

  deploy:
    needs: [build, approve]
    environment:
      name: production
      url: https://www.meepleai.io
    steps:
      - name: Blue-Green Deploy
        # ... (see above)

  validate:
    needs: [deploy]
    steps:
      - name: Health + Smoke + Performance Tests
        # ... (validation suite)

  rollback:
    if: failure()
    environment: production-rollback
    # Manual rollback trigger
```

---

## Release Process

### Step-by-Step Guide

#### 1. Pre-Release Planning

```markdown
**Release Checklist** (1-2 days before):

- [ ] All features merged to main-staging
- [ ] Staging deployed and tested (minimum 24 hours)
- [ ] No critical bugs in staging
- [ ] Database migrations tested in staging
- [ ] Performance acceptable in staging
- [ ] Documentation updated (CHANGELOG.md, API docs)
- [ ] Team notified of release schedule
- [ ] Rollback plan documented
- [ ] Monitoring dashboards configured
```

#### 2. Create Release Branch (Optional)

```bash
# For complex releases with testing period
git checkout -b release/v1.2.3 main-staging

# Apply final fixes if needed
git cherry-pick <commit-sha>

# When ready, merge to main
git checkout main
git merge release/v1.2.3
```

#### 3. Update Changelog

**CHANGELOG.md**:
```markdown
# Changelog

## [1.2.3] - 2026-01-30

### Added
- Game session creation and management (#3160)
- Real-time session updates via SSE (#3172)
- Session history and analytics dashboard (#3173)

### Changed
- Improved PDF upload performance (25% faster) (#3179)
- Enhanced error messages for validation failures (#3180)

### Fixed
- JWT token expiration handling (#3181)
- Redis connection timeout in high load (#3182)

### Security
- Updated dependencies with security patches
- Added rate limiting to session endpoints

### Breaking Changes
- None

### Migration Notes
- Database migration required: `20260130_AddGameSessionTables`
- Estimated downtime: 0 seconds (blue-green deployment)
```

#### 4. Version Bump

```bash
# Update version in project files if needed
# (Usually auto-detected from git tag)

# apps/api/src/Api/Api.csproj (optional)
<Version>1.2.3</Version>

# apps/web/package.json (optional)
"version": "1.2.3"
```

#### 5. Create and Push Tag

```bash
git checkout main
git pull origin main

# Create annotated tag (recommended)
git tag -a v1.2.3 -m "Release v1.2.3: Game session features"

# Or lightweight tag
git tag v1.2.3

# Push tag (triggers production workflow)
git push origin v1.2.3

# Verify tag
git show v1.2.3
```

#### 6. Monitor Deployment

```bash
# Watch GitHub Actions
# https://github.com/owner/repo/actions/workflows/deploy-production.yml

# Stages to monitor:
# ✅ Staging Check (2 min)
# ✅ Pre-production Tests (10 min)
# ✅ Build Images (15 min)
# ⏸️  Approval Gate (manual)
# 🚀 Deploy (10 min)
# ✅ Validate (5 min)
```

#### 7. Approve Production Deploy

```bash
# GitHub Actions → Deploy to Production → Review deployments

# Review:
# - Version: v1.2.3
# - Commit: SHA
# - Changes: CHANGELOG.md
# - Migrations: Yes/No
# - Rollback: Ready

# Click: "Approve and deploy"
```

#### 8. Post-Deploy Validation

```bash
# Automated (in workflow)
curl -sf https://www.meepleai.io/health
curl -sf https://api.meepleai.io/health

# Manual checks
# □ Login works
# □ Game search works
# □ Session creation works
# □ No errors in Grafana
# □ Response times acceptable
# □ No user complaints

# Monitoring
open https://grafana.meepleai.io
# Check: Request rates, error rates, response times
```

#### 9. GitHub Release

**Auto-created by workflow**:

```markdown
# Release v1.2.3
**Deployed:** 2026-01-30 14:30 UTC

## Images
- API: `ghcr.io/owner/repo/api:v1.2.3`
- Web: `ghcr.io/owner/repo/web:v1.2.3`

## Changes
See [CHANGELOG.md](./CHANGELOG.md#123---2026-01-30)

## Rollback
If needed:
```bash
git tag -a v1.2.4 <previous-commit>
git push origin v1.2.4
```
```

---

## Rollback Procedures

### Scenario 1: Immediate Rollback (During Deployment)

**Blue-Green Rollback** (deployment in progress):

```bash
# If new container fails health check
# GitHub Actions automatically:
# 1. Scales down new container
# 2. Keeps old container running
# 3. Marks deployment as failed

# Manual intervention if needed:
ssh user@production-server
cd /opt/meepleai

# Remove new container
docker compose up -d --no-deps --scale api=1 api

# Verify old container still running
docker ps | grep api
curl -sf http://localhost:8080/health
```

### Scenario 2: Rollback After Deployment

**Re-deploy Previous Version** (within 1 hour):

```bash
# Option A: Re-run previous successful workflow
# 1. Go to GitHub Actions
# 2. Find last successful production deployment
# 3. Click "Re-run all jobs"

# Option B: Deploy specific previous version
# 1. Go to GitHub Actions → Deploy to Production
# 2. Click "Run workflow"
# 3. Enter version: v1.2.2 (previous working version)
# 4. Approve deployment

# Option C: Manual SSH rollback
ssh user@production-server
cd /opt/meepleai

# Pull previous image
docker pull ghcr.io/owner/repo/api:v1.2.2

# Update docker-compose
export API_IMAGE="ghcr.io/owner/repo/api:v1.2.2"
docker compose up -d --no-deps api

# Rollback database if needed
docker compose exec postgres psql -U postgres meepleai < /backups/pre-deploy-20260130.sql
```

### Scenario 3: Database Rollback

**Critical: Database Migration Failed**:

```bash
ssh user@production-server
cd /opt/meepleai

# 1. Stop API (prevent writes)
docker compose stop api

# 2. Restore database from backup
BACKUP_FILE="/backups/pre-deploy-$(date +%Y%m%d).sql"
docker compose exec -T postgres psql -U postgres meepleai < $BACKUP_FILE

# 3. Deploy previous image (no migrations)
export API_IMAGE="ghcr.io/owner/repo/api:v1.2.2"
docker compose up -d api

# 4. Verify
curl -sf http://localhost:8080/health
```

### Rollback Decision Tree

```
Deployment Failed?
    │
    ├─ Health check failed during deploy
    │   └─ Auto-rollback: Blue-green keeps old container
    │
    ├─ Validation failed after deploy
    │   └─ Manual rollback: Re-deploy previous version
    │
    ├─ Database migration failed
    │   ├─ Backup exists: Restore DB + rollback code
    │   └─ No backup: Forward-fix only (risky!)
    │
    └─ Issues discovered later (hours/days)
        ├─ Non-critical: Schedule hotfix
        └─ Critical: Emergency rollback + incident response
```

---

## Monitoring & Validation

### Health Check Endpoints

```bash
# API Health
curl https://api.meepleai.io/health
# Response: {"status":"Healthy","version":"1.2.3","timestamp":"2026-01-30T14:30:00Z"}

# Web Health
curl -I https://www.meepleai.io
# Response: HTTP/2 200

# Database Health
curl https://api.meepleai.io/health/database
# Response: {"status":"Healthy","responseTime":"12ms"}

# Redis Health
curl https://api.meepleai.io/health/redis
# Response: {"status":"Healthy","responseTime":"3ms"}

# Qdrant Health
curl https://api.meepleai.io/health/qdrant
# Response: {"status":"Healthy","responseTime":"8ms"}
```

### Grafana Dashboards

**Production Monitoring**:

1. **API Metrics Dashboard**:
   - Request rate (req/s)
   - Error rate (%)
   - Response time (p50, p95, p99)
   - Active connections

2. **Infrastructure Dashboard**:
   - CPU usage (%)
   - Memory usage (%)
   - Disk I/O
   - Network traffic

3. **Database Dashboard**:
   - Query performance
   - Connection pool
   - Lock waits
   - Cache hit ratio

**Alert Thresholds**:

```yaml
alerts:
  - name: high_error_rate
    condition: error_rate > 1%
    severity: critical

  - name: slow_response_time
    condition: p95_response_time > 2000ms
    severity: warning

  - name: high_cpu_usage
    condition: cpu_usage > 80%
    severity: warning
```

### Smoke Tests

**Automated Post-Deploy**:

```bash
# Critical user journeys
smoke_tests=(
  "GET /api/v1/health"
  "POST /api/v1/auth/login"
  "GET /api/v1/games?limit=10"
  "POST /api/v1/games/search"
  "GET /api/v1/users/me"
)

for test in "${smoke_tests[@]}"; do
  echo "Testing: $test"
  # Execute and verify response
done
```

---

## Emergency Procedures

### Hotfix Process

**Urgent Production Bug**:

```bash
# 1. Create hotfix branch from main
git checkout -b hotfix/v1.2.4 main

# 2. Apply fix
git cherry-pick <fix-commit-sha>
# OR
# Implement fix directly

# 3. Test locally
dotnet test
pnpm test

# 4. Deploy to staging (skip full test suite)
git push origin hotfix/v1.2.4
# Trigger manual deploy to staging with skip_tests=true

# 5. Quick staging validation (15 min)
curl https://staging.meepleai.com/health

# 6. Merge to main
git checkout main
git merge hotfix/v1.2.4

# 7. Tag and deploy
git tag -a v1.2.4 -m "Hotfix v1.2.4: Critical bug fix"
git push origin v1.2.4

# 8. Skip staging check (emergency)
# Set skip_staging_check: true in workflow

# 9. Expedited approval
# Notify team, get approval ASAP

# 10. Monitor closely
# Watch Grafana for 1-2 hours
```

### Incident Response

**Production Outage**:

```bash
# 1. Assess severity
# - Complete outage: P0 (all hands)
# - Partial outage: P1 (on-call team)
# - Degraded performance: P2 (monitor)

# 2. Immediate actions (P0/P1)
# - Notify team (Slack incident channel)
# - Check health endpoints
# - Review Grafana dashboards
# - Check container logs

# 3. Mitigation
# - Rollback if recent deploy
# - Scale up resources if capacity issue
# - Restart unhealthy containers
# - Enable maintenance mode if needed

# 4. Communication
# - Status page update
# - User notification (if major)
# - Stakeholder briefing

# 5. Resolution
# - Implement fix
# - Deploy hotfix
# - Verify restoration

# 6. Post-incident
# - Root cause analysis
# - Post-mortem document
# - Action items for prevention
```

---

## Quick Reference

### Deployment Commands

```bash
# Staging
git checkout main-staging
git merge main-dev
git push origin main-staging

# Production
git checkout main
git tag -a v1.2.3 -m "Release v1.2.3"
git push origin v1.2.3

# Hotfix
git checkout -b hotfix/v1.2.4 main
# ... fix ...
git tag -a v1.2.4 -m "Hotfix v1.2.4"
git push origin v1.2.4

# Rollback
# Re-run previous successful workflow OR
# Manual: docker pull ghcr.io/owner/repo/api:v1.2.2
```

### Health Checks

```bash
# Quick health check
curl -sf https://www.meepleai.io/health && echo "✅ Healthy" || echo "❌ Down"

# Full validation
for service in api web; do
  curl -sf https://$service.meepleai.io/health
done
```

---

## Additional Resources

- [Docker Versioning Guide](./docker-versioning-guide.md)
- [Rollback & Disaster Recovery](./rollback-disaster-recovery.md)
- [Monitoring Setup Guide](./monitoring-setup-guide.md)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)

---

**Next**: [Docker Volume Management](./docker-volume-management.md)
