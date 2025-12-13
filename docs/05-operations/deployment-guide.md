# Deployment Guide - MeepleAI

Complete guide for deploying MeepleAI to staging and production environments.

---

## 📋 **Table of Contents**

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Initial Setup](#initial-setup)
4. [Deployment Workflow](#deployment-workflow)
5. [Git Workflow & Branching Strategy](#git-workflow--branching-strategy)
6. [Staging Deployment](#staging-deployment)
7. [Production Deployment](#production-deployment)
8. [Verification](#verification)
9. [Rollback](#rollback)
10. [Monitoring](#monitoring)
11. [Troubleshooting](#troubleshooting)
12. [Best Practices](#best-practices)

---

## 🎯 **Overview**

MeepleAI uses a **two-stage deployment pipeline**:

```
Development → Staging → Production
     │            │           │
     │            │           └─→ meepleai.dev (Users)
     │            └─→ staging.meepleai.dev (Testing)
     └─→ localhost:3000 (Development)
```

**Philosophy:**
- ✅ **Test everything on staging first**
- ✅ **Automate health checks and smoke tests**
- ✅ **Always have a rollback plan**
- ✅ **Monitor for 30 minutes post-deployment**

---

## ✅ **Prerequisites**

### **Required Tools**
```bash
# Check installed
docker --version          # Docker 24+
git --version            # Git 2.30+
pnpm --version           # pnpm 8+
dotnet --version         # .NET SDK 9+
curl --version           # curl (for health checks)
jq --version             # jq (for JSON parsing)
```

### **Access Requirements**
- ✅ Docker registry access (`ghcr.io/degrassiaaron`)
- ✅ Staging server SSH access
- ✅ Production server SSH access (restricted)
- ✅ Database credentials (staging + production)
- ✅ GitHub repository push access

### **Environment Verification**
```bash
# From project root
bash tools/setup/setup-test-environment.sh --skip-tests

# Verify all services start
bash scripts/dev/dev-up.ps1  # Windows
# or
cd infra && docker compose up -d  # Linux/macOS
```

---

## 🔧 **Initial Setup**

### **1. Configure Docker Registry**

```bash
# Login to GitHub Container Registry
echo $GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin

# Verify access
docker pull ghcr.io/degrassiaaron/meepleai-api:staging-latest
```

### **2. Create Deployment Configuration**

Create `.env.deployment` in project root:

```bash
# Docker Registry
DOCKER_REGISTRY=ghcr.io/degrassiaaron

# Staging Environment
STAGING_HOST=staging.meepleai.dev
STAGING_DB_HOST=staging-db.meepleai.dev
STAGING_DB_NAME=meepleai_staging
STAGING_DB_USER=postgres
STAGING_DB_PASSWORD=<your-staging-db-password>

# Production Environment
PRODUCTION_HOST=meepleai.dev
PROD_DB_HOST=db.meepleai.dev
PROD_DB_NAME=meepleai
PROD_DB_USER=postgres
PROD_DB_PASSWORD=<your-prod-db-password>

# Smoke Test Account (Production)
PROD_SMOKETEST_PASSWORD=<smoketest-password>

# Backup Configuration
BACKUP_DIR=/var/backups/meepleai
```

**⚠️ IMPORTANT:** Add to `.gitignore`:
```bash
echo ".env.deployment" >> .gitignore
```

### **3. Create Test Accounts**

**Staging:**
```sql
INSERT INTO users (email, password_hash, display_name, role)
VALUES ('test@meepleai.dev', '<hash>', 'Smoke Test User', 'User');
```
Password: `SmokeTest123!`

**Production:**
```sql
INSERT INTO users (email, password_hash, display_name, role)
VALUES ('smoketest@meepleai.dev', '<hash>', 'Smoke Test', 'User');
```

---

## 🔄 **Deployment Workflow**

### **Standard Release Process**

```
┌─────────────┐
│ Development │ Feature complete, tests passing locally
└──────┬──────┘
       │ git push
       ↓
┌─────────────┐
│   GitHub    │ PR review, CI/CD tests
└──────┬──────┘
       │ merge to develop
       ↓
┌─────────────┐
│   Staging   │ bash tools/deployment/deploy-staging.sh
└──────┬──────┘
       │ Manual testing
       ↓
┌─────────────┐
│     QA      │ Verify features, regression testing
└──────┬──────┘
       │ Approval
       ↓
┌─────────────┐
│ Production  │ bash tools/deployment/deploy-production.sh
└──────┬──────┘
       │ Monitor
       ↓
┌─────────────┐
│   Success   │ 🎉
└─────────────┘
```

---

## 🌳 **Git Workflow & Branching Strategy**

### **Branch Structure**

MeepleAI usa una strategia di branching **Git Flow semplificata**:

```
main            (production-ready, protected)
  │
  └─── develop  (integration, staging-ready)
         │
         ├─── feature/user-auth-2fa
         ├─── feature/pdf-quality-improvements
         ├─── feature/admin-dashboard
         └─── bugfix/rag-citation-fix

hotfix/critical-security-fix (from main, urgent production fixes)
```

### **Branch Mapping to Environments**

| Branch | Environment | Auto-Deploy | Protection |
|--------|-------------|-------------|------------|
| `feature/*` | Local development | ❌ No | None |
| `bugfix/*` | Local development | ❌ No | None |
| `develop` | **Staging** | ✅ Manual | Require PR + CI pass |
| `main` | **Production** | ✅ Manual | Require PR + approval + CI pass |
| `hotfix/*` | Staging → Production | ❌ No | Require approval |

### **Development Workflow**

#### **1. Nuova Feature**

```bash
# 1. Crea feature branch da develop
git checkout develop
git pull origin develop
git checkout -b feature/nome-feature

# 2. Sviluppa e testa localmente
# ... sviluppo ...
pnpm test
dotnet test

# 3. Commit incrementali
git add .
git commit -m "feat: Implement user authentication with 2FA"

# 4. Push feature branch
git push -u origin feature/nome-feature

# 5. Apri Pull Request su GitHub
#    Base: develop ← Compare: feature/nome-feature
```

#### **2. Pull Request Process**

**Su GitHub:**

1. **Crea PR** da `feature/nome-feature` → `develop`
   - Titolo descrittivo: "feat: Add 2FA authentication"
   - Descrizione completa con:
     - Cosa cambia
     - Perché (issue collegata)
     - Come testare
     - Screenshot (se UI)

2. **CI/CD automatico** (GitHub Actions)
   ```
   ✓ Build backend (.NET 9)
   ✓ Run backend tests (90%+ coverage required)
   ✓ Build frontend (Next.js)
   ✓ Run frontend tests (90%+ coverage required)
   ✓ Lint & typecheck
   ✓ Security scan (CodeQL)
   ```

3. **Code Review**
   - Almeno 1 approval richiesta
   - Commenti risolti
   - CI green

4. **Merge to develop**
   ```bash
   # Merge strategy: Squash and merge (recommended)
   # Creates: "feat: Add 2FA authentication (#123)"
   ```

#### **3. Deploy to Staging**

Dopo merge su `develop`:

```bash
# 1. Pull latest develop
git checkout develop
git pull origin develop

# 2. Deploy to staging
bash tools/deployment/deploy-staging.sh

# Script will:
# - Verify clean working directory
# - Run tests
# - Build Docker images tagged with commit hash
# - Push to ghcr.io/degrassiaaron
# - Deploy to staging.meepleai.dev
# - Run health checks
# - Run smoke tests
```

#### **4. Production Release**

Dopo testing completo su staging:

```bash
# 1. Crea PR da develop → main
#    Title: "Release v1.2.0 - 2FA Authentication"
#    Include changelog

# 2. Approvazione richiesta (Tech Lead or CTO)

# 3. Merge to main (merge commit, preserve history)

# 4. Checkout main locally
git checkout main
git pull origin main

# 5. Tag release (opzionale ma consigliato)
git tag -a v1.2.0 -m "Release 1.2.0 - 2FA Authentication"
git push origin v1.2.0

# 6. Deploy to production
bash tools/deployment/deploy-production.sh
# Script will:
# - Require "DEPLOY TO PRODUCTION" confirmation
# - Verify on main branch
# - Backup production database
# - Tag images as production-latest
# - Guide manual deployment on production server
# - Run production health checks & smoke tests
```

### **Hotfix Workflow (Emergency Production Fixes)**

Per bug critici in produzione che richiedono fix immediato:

```bash
# 1. Crea hotfix branch da main
git checkout main
git pull origin main
git checkout -b hotfix/critical-bug-fix

# 2. Fix rapido e test
# ... fix ...
pnpm test
dotnet test

# 3. Commit
git add .
git commit -m "hotfix: Fix critical security vulnerability"

# 4. Push
git push -u origin hotfix/critical-bug-fix

# 5. PR to main (emergency approval)
# Base: main ← Compare: hotfix/critical-bug-fix
# Require tech lead immediate approval

# 6. Merge to main
git checkout main
git pull origin main

# 7. Deploy to staging first (verify fix)
bash tools/deployment/deploy-staging.sh

# 8. Deploy to production
bash tools/deployment/deploy-production.sh

# 9. IMPORTANTE: Backport to develop
git checkout develop
git pull origin develop
git merge main  # or cherry-pick specific commits
git push origin develop
```

### **CI/CD Integration (GitHub Actions)**

File: `.github/workflows/ci.yml`

**Trigger automatici:**
- ✅ Push to any branch
- ✅ Pull request opened/updated
- ✅ Merge to develop
- ✅ Merge to main

**Jobs:**

1. **Backend CI**
   ```yaml
   - Restore dependencies
   - Build (dotnet build)
   - Run tests (dotnet test)
   - Check coverage ≥90%
   - Run code analyzers
   ```

2. **Frontend CI**
   ```yaml
   - Install dependencies (pnpm install)
   - Lint (pnpm lint)
   - Typecheck (pnpm typecheck)
   - Build (pnpm build)
   - Run tests (pnpm test)
   - Check coverage ≥90%
   ```

3. **Security Scan**
   ```yaml
   - CodeQL analysis (C#, TypeScript)
   - Dependency audit
   - Vulnerability scan
   ```

**Status Checks (required for merge):**
- ✅ All tests pass
- ✅ Coverage ≥90%
- ✅ No security vulnerabilities
- ✅ Lint and typecheck pass

### **Branch Protection Rules**

**develop branch:**
```
✓ Require pull request before merging
✓ Require approvals: 1
✓ Require status checks to pass
  - backend-tests
  - frontend-tests
  - security-scan
✓ Require branches to be up to date before merging
✗ Allow force pushes: No
✗ Allow deletions: No
```

**main branch:**
```
✓ Require pull request before merging
✓ Require approvals: 2 (or 1 from tech lead)
✓ Require status checks to pass
  - backend-tests
  - frontend-tests
  - security-scan
✓ Require branches to be up to date before merging
✓ Require signed commits (optional)
✗ Allow force pushes: No
✗ Allow deletions: No
```

### **Commit Message Conventions**

Seguire **Conventional Commits**:

```
<type>(<scope>): <description>

[optional body]

[optional footer]
```

**Types:**
- `feat`: Nuova feature
- `fix`: Bug fix
- `docs`: Documentation
- `style`: Formatting, no code change
- `refactor`: Code refactoring
- `test`: Test aggiunta/modifica
- `chore`: Build, config, dependencies

**Examples:**
```bash
git commit -m "feat(auth): Add 2FA authentication with TOTP"
git commit -m "fix(rag): Correct citation extraction from PDF metadata"
git commit -m "docs(deployment): Add comprehensive deployment guide"
git commit -m "chore(deps): Update .NET to 9.0.1"
```

### **Risoluzione Conflitti**

Se develop è avanti rispetto alla tua feature branch:

```bash
# Opzione 1: Rebase (preferita, storia lineare)
git checkout feature/nome-feature
git fetch origin
git rebase origin/develop
# Risolvi conflitti se necessari
git push --force-with-lease origin feature/nome-feature

# Opzione 2: Merge (preserva storia)
git checkout feature/nome-feature
git merge origin/develop
# Risolvi conflitti
git push origin feature/nome-feature
```

---

## 🚀 **Staging Deployment**

### **Step 1: Pre-Deployment Checklist**

```bash
# 1. Ensure you're on the right branch
git checkout develop
git pull origin develop

# 2. Verify working directory is clean
git status

# 3. Run tests locally (optional but recommended)
bash tools/coverage/run-backend-coverage.sh
bash tools/coverage/run-frontend-coverage.sh

# 4. Load deployment configuration
source .env.deployment
```

### **Step 2: Deploy to Staging**

```bash
# Execute deployment script
bash tools/deployment/deploy-staging.sh
```

**What happens:**
1. ✅ Pre-flight checks (git, Docker)
2. ✅ Run test suite (backend + frontend)
3. ✅ Build Docker images
4. ✅ Tag images with git commit hash
5. ✅ Push to registry
6. ✅ Backup staging database
7. ✅ Deploy to staging server
8. ✅ Health checks (30 retries)
9. ✅ Smoke tests (auth, games, search)

**Expected output:**
```
🚀 MeepleAI Staging Deployment
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
Environment: staging
Image Tag: a1b2c3d
Staging Host: staging.meepleai.dev

📋 Running pre-flight checks...
✓ Git working directory clean
✓ Branch: develop
✓ Docker available

🧪 Running tests...
✓ Backend tests passed (92.3% coverage)
✓ Frontend tests passed (91.5% coverage)

🐳 Building Docker images...
✓ API image built
✓ Web image built

📤 Pushing images to registry...
✓ Images pushed to registry

💾 Creating database backup...
✓ Database backup created

🚢 Deploying to staging...
✓ Deployed

🏥 Running health checks...
✓ API is healthy
✓ Database connection healthy
✓ Web application is healthy

💨 Running smoke tests...
✓ Authentication successful
✓ User profile retrieved
✓ Games list retrieved (23 games)
✓ Search functionality works
✓ Health endpoint responding

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ Staging deployment successful!
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Deployed version: a1b2c3d
Staging URL: https://staging.meepleai.dev

Next steps:
1. Test manually on staging
2. Run: bash tools/deployment/deploy-production.sh
3. If issues, run: bash tools/deployment/rollback.sh staging
```

### **Step 3: Manual Testing on Staging**

**Critical paths to test:**

1. **Authentication Flow**
   ```
   - Register new user
   - Login with credentials
   - Logout
   - Password reset (if implemented)
   ```

2. **PDF Upload**
   ```
   - Upload board game PDF
   - Verify extraction quality
   - Check vector indexing
   ```

3. **Chat/RAG**
   ```
   - Ask question about game rules
   - Verify accurate responses
   - Test follow-up questions
   - Check citation accuracy
   ```

4. **Admin Functions**
   ```
   - View system health dashboard
   - Check user management
   - Review analytics
   ```

5. **Performance**
   ```
   - Page load times acceptable (<2s)
   - API response times (<500ms)
   - Search response times (<1s)
   ```

### **Step 4: Verify Staging Health**

```bash
# Continuous health check
watch -n 10 "bash tools/deployment/health-check.sh staging"

# View logs
bash tools/deployment/view-logs.sh staging all

# Re-run smoke tests
bash tools/deployment/smoke-test.sh staging
```

---

## 🏭 **Production Deployment**

### **⚠️ CRITICAL: Pre-Production Checklist**

- [ ] ✅ Staging deployment successful
- [ ] ✅ Manual testing on staging complete
- [ ] ✅ All critical paths verified
- [ ] ✅ Performance acceptable
- [ ] ✅ No errors in staging logs
- [ ] ✅ QA team approval obtained
- [ ] ✅ Deployment window scheduled (low traffic)
- [ ] ✅ Team notified (Slack/email)
- [ ] ✅ Rollback plan ready
- [ ] ✅ On-call engineer assigned

### **Step 1: Prepare for Production**

```bash
# 1. Switch to main branch
git checkout main
git pull origin main

# 2. Verify it's the same code as staging
STAGING_HASH=$(git rev-parse develop)
MAIN_HASH=$(git rev-parse main)
echo "Staging: $STAGING_HASH"
echo "Main: $MAIN_HASH"
# They should match if you merged develop → main

# 3. Load production configuration
source .env.deployment

# 4. Verify images exist in registry
docker manifest inspect ghcr.io/degrassiaaron/meepleai-api:$STAGING_HASH
docker manifest inspect ghcr.io/degrassiaaron/meepleai-web:$STAGING_HASH
```

### **Step 2: Deploy to Production**

```bash
# Execute production deployment
bash tools/deployment/deploy-production.sh
```

**What happens:**
1. ⚠️ **Safety confirmation required:** Type "DEPLOY TO PRODUCTION"
2. ✅ Verify on `main` branch
3. ✅ Check working directory clean
4. ✅ Verify images exist in registry
5. ✅ **Backup production database** (mandatory)
6. ✅ Tag images as `production-latest`
7. ✅ Push production tags
8. ⏸️ **Manual deployment step** (you deploy to servers)
9. ✅ Health checks
10. ✅ Production smoke tests

**Expected output:**
```
🚀 MeepleAI Production Deployment
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
⚠️  PRODUCTION DEPLOYMENT - PROCEED WITH CAUTION ⚠️
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Environment: production
Image Tag: a1b2c3d
Production Host: meepleai.dev

This will deploy to PRODUCTION.
Have you verified everything works on staging?
Type 'DEPLOY TO PRODUCTION' to continue: DEPLOY TO PRODUCTION

📋 Running pre-flight checks...
✓ Git working directory clean
✓ On main branch
✓ Docker images verified in registry

💾 Creating production database backup...
✓ Production backup created: /var/backups/meepleai/production/backup-20251122-140000.sql.gz

🚢 Deploying to production...
✓ Production images tagged and pushed

📝 Deployment Instructions:

1. SSH to production server:
   ssh production.meepleai.dev

2. Pull latest images:
   docker pull ghcr.io/degrassiaaron/meepleai-api:production-latest
   docker pull ghcr.io/degrassiaaron/meepleai-web:production-latest

3. Run database migrations:
   docker exec meepleai-api dotnet ef database update

4. Rolling update (zero downtime):
   docker-compose up -d --no-deps api
   sleep 30  # Wait for health check
   docker-compose up -d --no-deps web

⚠️  Or use your orchestration tool (k8s apply, etc.)

Press ENTER after deploying to production...
```

### **Step 3: Execute Production Deployment**

**On production server:**

```bash
# SSH to production
ssh production.meepleai.dev

# Navigate to deployment directory
cd /opt/meepleai

# Pull new images
docker compose pull

# Run database migrations (if any)
docker exec meepleai-api dotnet ef database update

# Rolling update for zero downtime
# 1. Update API first
docker compose up -d --no-deps --force-recreate api

# 2. Wait and verify API is healthy
sleep 30
curl https://api.meepleai.dev/health

# 3. Update Web
docker compose up -d --no-deps --force-recreate web

# 4. Verify both services
docker compose ps
```

**Press ENTER on your local machine after deployment completes.**

### **Step 4: Post-Deployment Verification**

Script continues:
```
🏥 Running production health checks...
✓ API is healthy
✓ Database connection healthy
✓ Web application is healthy
✓ PostgreSQL connected
✓ Redis connected
✓ Qdrant connected

💨 Running production smoke tests...
✓ Authentication successful
✓ User profile retrieved
✓ Games list retrieved (142 games)
✓ Search functionality works
✓ Health endpoint responding

━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
✅ Production deployment successful!
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

Deployment ID: prod-20251122-140000-a1b2c3d
Deployed version: a1b2c3d
Production URL: https://meepleai.dev

Monitor for the next 30 minutes:
- Logs: bash tools/deployment/view-logs.sh production
- Metrics: Check Grafana dashboards
- Alerts: Monitor Slack/email

If issues occur:
- Rollback: bash tools/deployment/rollback.sh production
```

---

## ✅ **Verification**

### **Health Checks**

```bash
# Automated health check
bash tools/deployment/health-check.sh production

# Manual endpoint checks
curl https://api.meepleai.dev/health
curl https://api.meepleai.dev/health/ready
curl https://meepleai.dev
```

### **Smoke Tests**

```bash
# Automated smoke tests
bash tools/deployment/smoke-test.sh production

# Manual user flow test
# 1. Open https://meepleai.dev
# 2. Login as regular user
# 3. Upload PDF
# 4. Ask chat question
# 5. Verify response accuracy
```

### **Monitoring Checklist** (First 30 minutes)

- [ ] ✅ No spike in error rate (check logs)
- [ ] ✅ Response times normal (<500ms API, <2s pages)
- [ ] ✅ No database connection errors
- [ ] ✅ Users can login successfully
- [ ] ✅ PDF uploads working
- [ ] ✅ Chat/RAG responses accurate
- [ ] ✅ No alerts triggered (Slack/email)

---

## ⏮️ **Rollback**

### **When to Rollback**

Rollback immediately if:
- ❌ Critical functionality broken (login, chat, upload)
- ❌ Database errors preventing user actions
- ❌ Error rate >5%
- ❌ Production smoke tests fail
- ❌ Severe performance degradation

### **Rollback Procedure**

```bash
# 1. Execute rollback script
bash tools/deployment/rollback.sh production

# The script will:
# - Read previous deployment version
# - Pull previous Docker images
# - Tag as production-latest
# - Provide deployment instructions

# 2. On production server
ssh production.meepleai.dev
cd /opt/meepleai
docker compose pull
docker compose up -d --force-recreate

# 3. Verify rollback successful
bash tools/deployment/health-check.sh production
```

### **Post-Rollback**

1. ✅ Verify production is stable
2. ✅ Notify team of rollback
3. ✅ Investigate root cause
4. ✅ Fix issue in development
5. ✅ Re-deploy to staging
6. ✅ Re-test thoroughly
7. ✅ Schedule new production deployment

---

## 📊 **Monitoring**

### **Real-Time Logs**

```bash
# All services
bash tools/deployment/view-logs.sh production all

# Specific service
bash tools/deployment/view-logs.sh production api 500
bash tools/deployment/view-logs.sh production web
```

### **Dashboards**

**Grafana (if configured):**
- HTTP request rate
- Error rate (5xx responses)
- Response time percentiles (p50, p95, p99)
- Database connection pool usage
- Redis hit rate
- Qdrant query latency

**Seq Logs:**
- Navigate to `https://logs.meepleai.dev` (if configured)
- Filter by level: ERROR, WARNING
- Search for correlation IDs

### **Alerts**

Configure alerts for:
- Error rate >1% for 5 minutes
- Response time p95 >1s for 10 minutes
- Health check failures
- Database connection pool exhaustion
- Disk space <10%

---

## 🔧 **Troubleshooting**

### **Deployment Script Fails**

**Tests fail:**
```bash
# Run tests manually to see details
cd apps/api && dotnet test
cd apps/web && pnpm test

# Check specific failures
bash tools/coverage/run-backend-coverage.sh
```

**Docker build fails:**
```bash
# Check Dockerfile syntax
docker build -f apps/api/Dockerfile apps/api/

# Verify base images available
docker pull mcr.microsoft.com/dotnet/aspnet:9.0
docker pull node:20-alpine
```

**Image push fails:**
```bash
# Re-authenticate
echo $GITHUB_TOKEN | docker login ghcr.io -u USERNAME --password-stdin

# Check registry permissions
docker pull ghcr.io/degrassiaaron/meepleai-api:staging-latest
```

### **Health Checks Fail**

**API not responding:**
```bash
# Check container status
ssh staging "docker ps | grep api"

# View API logs
ssh staging "docker logs meepleai-api --tail 100"

# Check migrations ran
ssh staging "docker exec meepleai-api dotnet ef migrations list"
```

**Database connection fails:**
```bash
# Test database connectivity
ssh staging "docker exec meepleai-postgres psql -U postgres -c 'SELECT 1'"

# Check connection string
ssh staging "docker exec meepleai-api env | grep ConnectionStrings"
```

### **Smoke Tests Fail**

**Authentication fails:**
```bash
# Verify test account exists
ssh staging "docker exec meepleai-postgres psql -U postgres -d meepleai_staging -c \"SELECT email FROM users WHERE email='test@meepleai.dev'\""

# Check auth endpoint directly
curl -X POST https://api.staging.meepleai.dev/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@meepleai.dev","password":"SmokeTest123!"}'
```

---

## 📚 **Best Practices**

### **DO**

✅ **Always deploy to staging first**
✅ **Test thoroughly on staging** (minimum 30 minutes)
✅ **Deploy during low-traffic windows** (e.g., 2-6 AM)
✅ **Have on-call engineer available** during production deploy
✅ **Monitor for 30 minutes post-deployment**
✅ **Keep deployment window short** (<15 minutes)
✅ **Document any manual steps** taken during deployment
✅ **Notify team** before/after deployments
✅ **Verify backups exist** before production deploy
✅ **Test rollback procedure** periodically

### **DON'T**

❌ **Don't deploy on Fridays** (unless emergency)
❌ **Don't deploy during peak hours**
❌ **Don't skip staging** deployment
❌ **Don't deploy without tests passing**
❌ **Don't deploy with uncommitted changes**
❌ **Don't deploy from feature branches** to production
❌ **Don't skip backup** before production deploy
❌ **Don't leave failed deployments** unresolved overnight

### **Emergency Deployments**

If critical bug requires immediate production fix:

1. ✅ Fix on hotfix branch from `main`
2. ✅ Deploy to staging, verify fix works
3. ✅ Get emergency approval from tech lead
4. ✅ Deploy to production
5. ✅ Monitor extra carefully (1 hour minimum)
6. ✅ Backport fix to `develop` branch

---

## 📖 **Additional Resources**

- **Deployment Scripts:** [`tools/deployment/`](../../tools/deployment/)
- **Script Documentation:** [`tools/deployment/README.md`](../../tools/deployment/README.md)
- **Infrastructure Docs:** [`docs/06-infrastructure/`](../06-infrastructure/)
- **Monitoring Setup:** [`docs/05-operations/monitoring.md`](./monitoring.md)
- **Incident Response:** [`docs/05-operations/incident-response.md`](./incident-response.md)

---

## 🆘 **Support**

**For deployment issues:**
- Slack: `#deployments` channel
- On-call: Check PagerDuty rotation
- Email: devops@meepleai.dev

**Escalation:**
1. On-call engineer (immediate)
2. Tech Lead (within 30 min)
3. CTO (critical incidents)

---

**Last Updated: 2025-12-13T10:59:23.970Z
**Maintained by:** DevOps Team
**Version:** 1.0

