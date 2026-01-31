# Deployment Scripts

Production-ready deployment automation for staging and production environments.

## 🚀 **Quick Start Workflow**

### **1. Deploy to Staging**
```bash
# Deploy latest code to staging
bash tools/deployment/deploy-staging.sh

# Verify health
bash tools/deployment/health-check.sh staging

# Run smoke tests
bash tools/deployment/smoke-test.sh staging
```

### **2. Deploy to Production**
```bash
# Deploy verified staging build to production
bash tools/deployment/deploy-production.sh

# Monitor production
bash tools/deployment/view-logs.sh production all
```

### **3. Rollback if Needed**
```bash
# Rollback to previous version
bash tools/deployment/rollback.sh production
```

---

## 📋 **Scripts**

### 🚢 **deploy-staging.sh**
**Purpose:** Deploy application to staging environment

**What it does:**
1. **Pre-flight checks:** Git status, current branch, Docker availability
2. **Run tests:** Backend (90%+ coverage) + Frontend tests
3. **Build Docker images:** API and Web with git commit hash tag
4. **Push to registry:** Tagged as both `{hash}` and `staging-latest`
5. **Backup database:** Create backup before deployment
6. **Deploy:** Update docker-compose and restart services
7. **Health checks:** Verify all services are healthy
8. **Smoke tests:** Test critical functionality

**Usage:**
```bash
# Standard deployment
bash tools/deployment/deploy-staging.sh

# Skip tests (faster, use with caution)
REQUIRE_TESTS=false bash tools/deployment/deploy-staging.sh

# Custom image tag
IMAGE_TAG=v1.2.3 bash tools/deployment/deploy-staging.sh
```

**Environment Variables:**
- `DOCKER_REGISTRY` - Docker registry URL (default: `ghcr.io/degrassiaaron`)
- `IMAGE_TAG` - Image tag (default: git commit hash)
- `STAGING_HOST` - Staging hostname (default: `staging.meepleai.dev`)
- `REQUIRE_TESTS` - Run tests before deploy (default: `true`)

**Who uses it:** Developers after completing features
**When:** After PR merge, before production deploy
**Requirements:** Docker, git, bash, pnpm, .NET SDK 9

---

### 🏭 **deploy-production.sh**
**Purpose:** Deploy application to production with safety checks

**What it does:**
1. **Safety confirmation:** Requires typing "DEPLOY TO PRODUCTION"
2. **Pre-flight checks:**
   - Must be on `main` branch
   - Working directory must be clean
   - Docker images must exist in registry
3. **Backup production database:** Critical - deployment fails if backup fails
4. **Tag images:** Pull staging images, tag as `production-latest`
5. **Deploy instructions:** Provides step-by-step deployment guide
6. **Health checks:** Verify production health
7. **Smoke tests:** Test critical production functionality

**Usage:**
```bash
# Production deployment (requires confirmation)
bash tools/deployment/deploy-production.sh

# Deploy specific version
IMAGE_TAG=v1.2.3 bash tools/deployment/deploy-production.sh
```

**Safety Features:**
- ✅ Requires explicit confirmation
- ✅ Main branch enforcement
- ✅ Mandatory production backup
- ✅ Deployment record created
- ✅ Post-deployment verification

**Who uses it:** DevOps team, Tech leads
**When:** After staging verification, during deployment windows
**Requirements:** Production access, Docker registry credentials

---

### 🏥 **health-check.sh**
**Purpose:** Verify all services are healthy

**What it does:**
1. **API health:** Check `/health` endpoint
2. **Database:** Check `/health/ready` (PostgreSQL connection)
3. **Web app:** Verify frontend loads
4. **Dependencies:** Check PostgreSQL, Redis, Qdrant connections
5. **Retry logic:** 30 attempts with 2s delay

**Usage:**
```bash
# Check staging health
bash tools/deployment/health-check.sh staging

# Check production health
bash tools/deployment/health-check.sh production

# Check local development
bash tools/deployment/health-check.sh local
```

**Who uses it:** Automated deployment pipelines, manual verification
**When:** After every deployment, in monitoring scripts
**Exit codes:** 0 = healthy, 1 = unhealthy

---

### 💨 **smoke-test.sh**
**Purpose:** Test critical user journeys work end-to-end

**Tests:**
1. **Authentication:** Login with test credentials
2. **User Profile:** Retrieve authenticated user data
3. **List Games:** Verify game catalog loads
4. **Search/RAG:** Test search functionality
5. **Health Endpoint:** Redundant check for API availability

**Usage:**
```bash
# Test staging
bash tools/deployment/smoke-test.sh staging

# Test production (uses separate test account)
bash tools/deployment/smoke-test.sh production
```

**Test Accounts:**
- **Staging:** `test@meepleai.dev` / `SmokeTest123!`
- **Production:** `smoketest@meepleai.dev` / `${PROD_SMOKETEST_PASSWORD}`

**Who uses it:** CI/CD pipeline, post-deployment verification
**When:** After deploy-staging, after deploy-production
**Exit codes:** 0 = all tests passed, 1 = test failed

---

### ⏮️ **rollback.sh**
**Purpose:** Rollback to previous working deployment

**What it does:**
1. **Read deployment history:** From `/tmp/meepleai-last-{env}-deployment.txt`
2. **Pull previous images:** Retrieve last known good version
3. **Retag images:** Tag as `{env}-latest`
4. **Deploy instructions:** Guide for deploying rollback
5. **Health check:** Verify rollback succeeded

**Usage:**
```bash
# Rollback staging
bash tools/deployment/rollback.sh staging

# Rollback production
bash tools/deployment/rollback.sh production
```

**Who uses it:** On-call engineers during incidents
**When:** When deployment causes critical issues
**Requirements:** Deployment history file exists

**Important:** This only rolls back Docker images, not database migrations!

---

### 💾 **backup-database.sh**
**Purpose:** Create timestamped database backups

**What it does:**
1. **Create backup:** PostgreSQL dump compressed with gzip
2. **Timestamp:** Filename format `backup-YYYYMMDD-HHMMSS.sql.gz`
3. **Retention:** Keep last 30 backups, delete older
4. **Verify:** Check backup size and integrity

**Usage:**
```bash
# Backup staging database
bash tools/deployment/backup-database.sh staging

# Backup production database
bash tools/deployment/backup-database.sh production
```

**Backup Location:** `/var/backups/meepleai/{environment}/`

**Restore:**
```bash
# Restore from backup
gunzip < /var/backups/meepleai/production/backup-20251122-120000.sql.gz \
  | psql -h db.meepleai.dev -U postgres -d meepleai
```

**Who uses it:** Automated by deployment scripts, manual backups
**When:** Before every deployment, scheduled daily backups
**Requirements:** PostgreSQL access, write permission to backup directory

---

### 📋 **view-logs.sh**
**Purpose:** Stream logs from deployed services

**Usage:**
```bash
# View all service logs (staging)
bash tools/deployment/view-logs.sh staging all

# View API logs (production, last 200 lines)
bash tools/deployment/view-logs.sh production api 200

# View Web logs
bash tools/deployment/view-logs.sh staging web

# View PostgreSQL logs
bash tools/deployment/view-logs.sh production postgres
```

**Services:** `api`, `web`, `postgres`, `all`

**Who uses it:** Developers debugging issues, DevOps monitoring
**When:** After deployments, during incident response
**Note:** Commands shown are templates - adjust for your infrastructure

---

## 🔄 **Complete Deployment Workflow**

### **Development → Staging → Production**

```bash
# 1. Development complete
git checkout main
git pull origin main

# 2. Deploy to staging
bash tools/deployment/deploy-staging.sh
# ✓ Tests run
# ✓ Images built and pushed
# ✓ Database backed up
# ✓ Deployed to staging
# ✓ Health checks passed
# ✓ Smoke tests passed

# 3. Manual testing on staging
# - Test new features
# - Verify bug fixes
# - Check performance

# 4. Deploy to production
bash tools/deployment/deploy-production.sh
# ✓ Confirmation required
# ✓ Main branch enforced
# ✓ Production backup created
# ✓ Images tagged for production
# ✓ Manual deployment confirmation
# ✓ Health checks passed
# ✓ Smoke tests passed

# 5. Monitor production
bash tools/deployment/view-logs.sh production all

# 6. If issues occur - Rollback
bash tools/deployment/rollback.sh production
```

---

## 🛡️ **Safety Features**

### **Pre-Deployment**
- ✅ Git working directory must be clean
- ✅ Tests must pass (90%+ coverage)
- ✅ Branch restrictions (production = `main` only)
- ✅ Image verification in registry

### **During Deployment**
- ✅ Automatic database backups
- ✅ Deployment record keeping
- ✅ Progressive deployment (API first, then Web)

### **Post-Deployment**
- ✅ Health checks with retries
- ✅ Smoke tests for critical paths
- ✅ Monitoring instructions

---

## ⚙️ **Configuration**

### **Environment Variables**

Create `.env.deployment` file:
```bash
# Docker Registry
DOCKER_REGISTRY=ghcr.io/degrassiaaron

# Staging
STAGING_HOST=staging.meepleai.dev
STAGING_DB_HOST=staging-db.meepleai.dev
STAGING_DB_NAME=meepleai_staging
STAGING_DB_USER=postgres
STAGING_DB_PASSWORD=<secret>

# Production
PRODUCTION_HOST=meepleai.dev
PROD_DB_HOST=db.meepleai.dev
PROD_DB_NAME=meepleai
PROD_DB_USER=postgres
PROD_DB_PASSWORD=<secret>
PROD_SMOKETEST_PASSWORD=<secret>

# Backup
BACKUP_DIR=/var/backups/meepleai
```

Load before deployment:
```bash
source .env.deployment
bash tools/deployment/deploy-staging.sh
```

---

## 🔧 **Customization**

### **Adjust for Your Infrastructure**

Scripts are **templates** - customize based on your deployment target:

**Docker Compose:**
```bash
# In deploy-staging.sh, replace deployment section:
ssh staging.meepleai.dev "cd /opt/meepleai && \
  docker-compose pull && \
  docker-compose up -d"
```

**Kubernetes:**
```bash
# In deploy-staging.sh:
kubectl set image deployment/meepleai-api \
  api=${DOCKER_REGISTRY}/meepleai-api:${IMAGE_TAG} \
  -n staging
```

**Docker Swarm:**
```bash
# In deploy-staging.sh:
docker service update --image \
  ${DOCKER_REGISTRY}/meepleai-api:${IMAGE_TAG} \
  meepleai_api
```

---

## 📊 **Monitoring After Deployment**

### **First 30 Minutes (Critical Window)**
```bash
# 1. Watch logs
bash tools/deployment/view-logs.sh production all

# 2. Monitor error rate (if you have Grafana/Prometheus)
# Check dashboards for HTTP 5xx errors

# 3. Check health repeatedly
watch -n 10 "bash tools/deployment/health-check.sh production"

# 4. Monitor user activity
# Check if users can login, upload PDFs, ask questions
```

### **If Issues Detected**
```bash
# Immediate rollback
bash tools/deployment/rollback.sh production

# Then investigate
bash tools/deployment/view-logs.sh production api 500
```

---

## 🚨 **Troubleshooting**

**Health checks fail:**
```bash
# Check service status
bash tools/deployment/view-logs.sh staging api

# Manually verify endpoints
curl https://api.staging.meepleai.dev/health
```

**Smoke tests fail:**
```bash
# Check test account exists
# Verify API is reachable
# Check database connections
```

**Rollback fails:**
```bash
# Manually pull previous image
docker pull ghcr.io/degrassiaaron/meepleai-api:staging-latest

# Check deployment history
cat /tmp/meepleai-last-staging-deployment.txt
```

**Backup fails:**
```bash
# Check database connectivity
psql -h staging-db.meepleai.dev -U postgres -d meepleai_staging

# Verify backup directory writable
ls -la /var/backups/meepleai/
```

---

## 📝 **Best Practices**

1. **Always deploy to staging first**
2. **Test thoroughly on staging** before production
3. **Deploy during low-traffic windows** (e.g., early morning)
4. **Have rollback plan ready** before deploying
5. **Monitor for 30 minutes** after production deploy
6. **Keep deployment window short** (<15 minutes)
7. **Communicate** deployments to team (Slack, email)
8. **Document** any manual steps taken

---

## 🔗 **Integration with CI/CD**

### **GitHub Actions Example**

```yaml
# .github/workflows/deploy-staging.yml
name: Deploy to Staging

on:
  push:
    branches: [develop]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Deploy to Staging
        env:
          DOCKER_REGISTRY: ${{ secrets.DOCKER_REGISTRY }}
        run: |
          bash tools/deployment/deploy-staging.sh
```

---

**Last Updated:** 2025-11-22
**Maintained by:** DevOps team
**See also:** `tools/coverage/`, `tools/cleanup/`
