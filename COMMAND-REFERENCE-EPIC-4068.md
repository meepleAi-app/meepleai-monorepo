# Epic #4068: Complete Command Reference

**Every command you need for development, testing, and deployment**

---

## Development Workflow

### Initial Setup

```bash
# Clone repository
git clone https://github.com/DegrassiAaron/meepleai-monorepo.git
cd meepleai-monorepo

# Checkout feature branch
git checkout -b feature/issue-4177-permission-model
git config branch.feature/issue-4177-permission-model.parent main-dev

# Start infrastructure
cd infra
docker compose up -d postgres redis qdrant

# Verify services running
docker compose ps
# Expected: All services "Up (healthy)"
```

---

### Backend Commands

#### Build & Run

```bash
cd apps/api/src/Api

# Restore dependencies
dotnet restore

# Build
dotnet build

# Build (Release mode)
dotnet build --configuration Release

# Clean build
dotnet clean && dotnet build

# Run
dotnet run

# Run with environment
dotnet run --environment Development

# Run with specific port
dotnet run --urls "http://localhost:8081"

# Watch mode (auto-reload on file changes)
dotnet watch run
```

#### Database

```bash
# Create migration
dotnet ef migrations add AddUserAccountStatus

# List migrations
dotnet ef migrations list

# Apply migrations (local)
dotnet ef database update

# Apply migrations (specific connection)
dotnet ef database update --connection "Host=localhost;Database=meepleai;Username=postgres;Password=postgres"

# Rollback to specific migration
dotnet ef database update PreviousMigrationName

# Generate SQL script (for review before applying)
dotnet ef migrations script AddUserAccountStatus --output migration.sql

# Remove last migration (if not applied)
dotnet ef migrations remove
```

#### Testing

```bash
# All tests
dotnet test

# With coverage
dotnet test /p:CollectCoverage=true /p:CoverageReporters=cobertura

# Filter by category
dotnet test --filter "Category=Unit"
dotnet test --filter "Category=Integration"

# Filter by bounded context
dotnet test --filter "BoundedContext=Administration"

# Filter by epic
dotnet test --filter "Epic=4068"

# Filter by class name
dotnet test --filter "FullyQualifiedName~PermissionTests"

# Verbose output
dotnet test --logger "console;verbosity=detailed"

# Coverage threshold (fails if below 90%)
dotnet test /p:CollectCoverage=true /p:CoverageMinimum=90 /p:ThresholdType=line
```

---

### Frontend Commands

#### Development

```bash
cd apps/web

# Install dependencies
pnpm install

# Install (frozen lockfile - CI)
pnpm install --frozen-lockfile

# Run dev server
pnpm dev

# Run on different port
pnpm dev -p 3001

# Build for production
pnpm build

# Start production server
pnpm start

# Clean build artifacts
rm -rf .next
pnpm build

# Type check
pnpm typecheck

# Lint
pnpm lint

# Lint + auto-fix
pnpm lint --fix

# Format code
pnpm format

# Format check (CI)
pnpm format:check
```

#### Testing

```bash
# All tests
pnpm test

# Watch mode
pnpm test --watch

# Coverage
pnpm test --coverage

# Coverage with threshold (fails if <85%)
pnpm test --coverage --coverageThreshold='{"global":{"branches":85,"functions":85,"lines":85}}'

# Specific test file
pnpm test -- TagStrip.test.tsx

# Multiple files
pnpm test -- Tag Permission Agent

# UI mode (interactive)
pnpm test --ui

# Update snapshots
pnpm test -- --updateSnapshot

# E2E tests (Playwright)
pnpm test:e2e

# E2E specific file
pnpm test:e2e -- permission-flows.spec.ts

# E2E headed mode (see browser)
pnpm test:e2e --headed

# E2E debug mode
pnpm test:e2e --debug

# Accessibility tests
pnpm test:a11y

# Performance tests
pnpm test:perf
```

#### Storybook

```bash
# Run Storybook
pnpm storybook

# Build static Storybook
pnpm build-storybook

# Build + deploy to Chromatic
pnpm exec chromatic --project-token=YOUR_TOKEN

# Test Storybook
pnpm test-storybook
```

---

### Docker Commands

```bash
cd infra

# Start all services
docker compose up -d

# Start specific services
docker compose up -d postgres redis

# View logs (all services)
docker compose logs -f

# View logs (specific service)
docker compose logs -f api

# View logs (last 50 lines)
docker compose logs --tail=50 api

# Restart service
docker compose restart api

# Stop all services
docker compose down

# Stop + remove volumes (⚠️ DATA LOSS!)
docker compose down -v

# Rebuild image + restart
docker compose up -d --build api

# Execute command in container
docker compose exec api dotnet --version

# Exec into container (bash)
docker compose exec api bash

# View container stats (CPU, memory)
docker compose stats

# Clean unused images/volumes
docker system prune -a
```

#### Epic #4068 Specific

```bash
# Start Epic #4068 stack (with monitoring)
docker compose -f docker-compose.epic-4068.yml up -d

# View permission system logs
docker compose -f docker-compose.epic-4068.yml logs -f api | grep -i permission

# Check Redis cache
docker compose -f docker-compose.epic-4068.yml exec redis redis-cli
> KEYS permission:*
> GET permission:user-id-here

# Apply migration
docker compose -f docker-compose.epic-4068.yml exec api dotnet ef database update

# Run tests in container
docker compose -f docker-compose.epic-4068.yml exec api dotnet test --filter Epic=4068
```

---

### Database Commands

#### PostgreSQL

```bash
# Connect to database
docker exec -it meepleai-postgres psql -U postgres -d meepleai

# Execute query
docker exec meepleai-postgres psql -U postgres -d meepleai -c "SELECT * FROM \"Users\" LIMIT 5;"

# Describe table
docker exec -it meepleai-postgres psql -U postgres -d meepleai
\d "Users"

# List indexes
\di

# View query plan
EXPLAIN ANALYZE SELECT \"Tier\", \"Role\", \"Status\" FROM \"Users\" WHERE \"Id\" = 'xxx';

# Check table size
SELECT pg_size_pretty(pg_total_relation_size('\"Users\"'));

# Backup database
docker exec meepleai-postgres pg_dump -U postgres meepleai > backup.sql

# Restore database
cat backup.sql | docker exec -i meepleai-postgres psql -U postgres -d meepleai

# Backup (compressed)
docker exec meepleai-postgres pg_dump -U postgres -Fc meepleai > backup.dump

# Restore (compressed)
docker exec -i meepleai-postgres pg_restore -U postgres -d meepleai < backup.dump
```

#### Epic #4068 Database Queries

```sql
-- User tier distribution
SELECT "Tier", COUNT(*) FROM "Users" GROUP BY "Tier";

-- User role distribution
SELECT "Role", COUNT(*) FROM "Users" GROUP BY "Role";

-- Account status distribution
SELECT "Status", COUNT(*) FROM "Users" GROUP BY "Status";

-- Users with specific permissions (Pro tier OR Editor role)
SELECT "Id", "Email", "Tier", "Role"
FROM "Users"
WHERE "Tier" IN ('pro', 'premium', 'enterprise')
   OR "Role" IN ('editor', 'creator', 'admin', 'superadmin');

-- Check indexes exist
SELECT indexname FROM pg_indexes WHERE tablename='Users';

-- Slow queries (requires pg_stat_statements extension)
SELECT query, mean_exec_time, calls
FROM pg_stat_statements
WHERE query LIKE '%Users%Tier%Role%Status%'
ORDER BY mean_exec_time DESC LIMIT 10;
```

---

### Git Commands

#### Feature Branch Workflow

```bash
# Create feature branch
git checkout main-dev
git pull origin main-dev
git checkout -b feature/issue-4177-permission-model

# Track parent branch (for PR target)
git config branch.feature/issue-4177-permission-model.parent main-dev

# Commit changes
git add .
git commit -m "feat(admin): Permission data model (Issue #4177)

- Add UserTier Pro, Enterprise tiers
- Add Role Creator
- Add UserAccountStatus enum
- Implement Permission value object
- Create PermissionRegistry service

Closes #4177"

# Push to remote
git push -u origin feature/issue-4177-permission-model

# Create PR (to parent branch)
gh pr create --base main-dev --title "Permission Data Model & Schema (#4177)"

# View PR status
gh pr status

# Check out PR for review
gh pr checkout 123

# Merge PR (after approval)
gh pr merge --squash --delete-branch

# Clean up local branch (after merge)
git checkout main-dev
git pull
git branch -D feature/issue-4177-permission-model

# Prune remote tracking branches
git remote prune origin
```

---

### GitHub CLI Commands

#### Issues

```bash
# View issue
gh issue view 4177

# List issues for epic
gh issue list --search "epic:4068"

# Create issue
gh issue create --title "New Issue" --body "Description" --label "p1-high,enhancement"

# Update issue (add label)
gh issue edit 4177 --add-label "status:in-progress"

# Close issue
gh issue close 4177 --comment "Completed in PR #123"

# Reopen issue
gh issue reopen 4177
```

#### Pull Requests

```bash
# Create PR
gh pr create --base main-dev --title "Title" --body "Description"

# Create draft PR
gh pr create --base main-dev --draft

# List PRs
gh pr list

# View PR
gh pr view 123

# Review PR
gh pr review 123 --approve
gh pr review 123 --request-changes --body "Needs fixes"

# Merge PR
gh pr merge 123 --squash --delete-branch

# Check PR status
gh pr status

# View PR diff
gh pr diff 123
```

---

### Testing Commands

#### Backend Testing

```bash
cd apps/api/src/Api

# Quick test (Epic #4068 only)
dotnet test --filter Epic=4068 --logger "console;verbosity=minimal"

# With coverage
dotnet test --filter Epic=4068 /p:CollectCoverage=true

# Integration tests (requires running database)
docker compose -f ../../../infra/docker-compose.yml up -d postgres
dotnet test --filter "Category=Integration&Epic=4068"

# Specific test class
dotnet test --filter "FullyQualifiedName~PermissionTests"

# Specific test method
dotnet test --filter "FullyQualifiedName~PermissionTests.Check_WithOrLogic_ReturnsCorrectResult"

# Run until first failure
dotnet test --filter Epic=4068 -- RunConfiguration.StopOnFirstTestFailure=true

# Parallel execution (faster)
dotnet test --parallel
```

#### Frontend Testing

```bash
cd apps/web

# Quick test
pnpm test -- --run

# Watch mode
pnpm test

# Specific component
pnpm test -- TagStrip

# Coverage report
pnpm test -- --coverage --coverage.reporter=html
# Open: coverage/index.html in browser

# E2E with trace
pnpm exec playwright test --trace on

# E2E specific test
pnpm exec playwright test tests/e2e/epic-4068/permission-flows.spec.ts

# E2E show report
pnpm exec playwright show-report

# Accessibility audit
pnpm add -D @axe-core/playwright
pnpm exec playwright test --grep @a11y
```

---

### Monitoring & Observability

#### Prometheus Queries

```promql
# Permission check rate
rate(permission_check_total[5m])

# Permission denied rate
rate(permission_denied_total[5m]) / rate(permission_check_total[5m])

# Permission API latency (p95)
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket{endpoint="/api/v1/permissions/me"}[5m]))

# Cache hit rate
rate(permission_cache_hit_total[5m]) / (rate(permission_cache_hit_total[5m]) + rate(permission_cache_miss_total[5m]))

# Tooltip positioning performance (p95)
histogram_quantile(0.95, rate(tooltip_position_duration_ms_bucket[5m]))

# Active WebSocket connections
signalr_active_connections{hub="permission"}
```

#### Grafana Dashboard

```bash
# Access Grafana
open http://localhost:3001
# Login: admin / admin

# Import dashboard
curl -X POST http://localhost:3001/api/dashboards/db \
  -H "Content-Type: application/json" \
  -d @infra/monitoring/grafana/dashboards/epic-4068-complete-dashboard.json

# Export dashboard
curl http://localhost:3001/api/dashboards/uid/epic-4068-complete > dashboard-export.json
```

#### Logging

```bash
# View API logs (last 100 lines)
docker compose logs --tail=100 api

# Follow logs (real-time)
docker compose logs -f api

# Filter logs (permissions only)
docker compose logs api | grep -i permission

# View logs with timestamps
docker compose logs -t api

# Logs for specific time range
docker compose logs --since 2026-02-12T10:00:00 --until 2026-02-12T11:00:00 api
```

---

### Deployment Commands

#### Staging Deployment

```bash
# Build images
docker build -t meepleai-api:staging apps/api
docker build -t meepleai-web:staging apps/web

# Tag for registry
docker tag meepleai-api:staging ghcr.io/meepleai/api:staging-epic4068
docker tag meepleai-web:staging ghcr.io/meepleai/web:staging-epic4068

# Push to registry
echo "$GITHUB_TOKEN" | docker login ghcr.io -u $GITHUB_USER --password-stdin
docker push ghcr.io/meepleai/api:staging-epic4068
docker push ghcr.io/meepleai/web:staging-epic4068

# Deploy via SSH
ssh staging-server << 'EOF'
  cd /opt/meepleai
  docker compose pull
  docker compose up -d --no-deps api web
EOF

# Apply migration
ssh staging-server "docker exec meepleai-api dotnet ef database update"

# Smoke test
curl https://staging.meepleai.com/health/epic-4068
# Expected: 200 OK
```

#### Production Deployment

```bash
# Pre-deployment backup
ssh production-server "docker exec meepleai-postgres pg_dump -U postgres meepleai | gzip > /backups/pre-epic4068.sql.gz"

# Build & push production images
docker build -t ghcr.io/meepleai/api:v1.5-epic4068 apps/api
docker push ghcr.io/meepleai/api:v1.5-epic4068

# Deploy (blue-green)
ssh production-server << 'EOF'
  cd /opt/meepleai
  # Deploy to green environment (new version)
  docker compose -f docker-compose.green.yml up -d
  # Wait for health check
  sleep 30
  curl http://green:8080/health/epic-4068 || exit 1
EOF

# Apply migration (before switching traffic!)
ssh production-server "docker exec meepleai-api-green dotnet ef database update"

# Switch load balancer (gradual)
# Edit nginx.conf: Change weight from blue:9,green:1 → blue:1,green:9
ssh production-server "docker exec nginx nginx -s reload"

# Monitor for 1 hour
# If stable, switch 100%: blue:0,green:1

# Rollback if issues
ssh production-server "docker exec nginx sed -i 's/green:1/blue:1/' /etc/nginx/nginx.conf && nginx -s reload"
```

---

### CI/CD Commands

```bash
# Trigger CI manually
gh workflow run epic-4068-ci.yml

# View workflow status
gh run list --workflow=epic-4068-ci.yml

# View logs for specific run
gh run view 123456

# Download artifacts
gh run download 123456

# Cancel running workflow
gh run cancel 123456
```

---

### Security Commands

#### Security Audit

```bash
# Run complete security audit
chmod +x scripts/security/epic-4068-security-audit.sh
./scripts/security/epic-4068-security-audit.sh production

# With test tokens
export FREE_USER_TOKEN="eyJhbGc..."
export PRO_USER_TOKEN="eyJhbGc..."
export ADMIN_USER_TOKEN="eyJhbGc..."
./scripts/security/epic-4068-security-audit.sh production

# Expected output:
# ✓ Passed: XX
# ⚠ Warnings: XX
# ✗ Failed: 0
```

#### Dependency Scanning

```bash
# Backend (NuGet)
cd apps/api/src/Api
dotnet list package --vulnerable --include-transitive

# Frontend (npm)
cd apps/web
pnpm audit
pnpm audit --fix

# Snyk scan
pnpm dlx snyk test

# Trivy scan (Docker images)
docker run --rm -v /var/run/docker.sock:/var/run/docker.sock aquasec/trivy image meepleai-api:latest
```

---

### Performance Commands

#### Load Testing

```bash
# Run k6 load test
cd tests/load
docker run -i grafana/k6 run - < epic-4068-permission-load-test.js

# With environment variables
docker run -i -e API_URL=https://api.meepleai.com grafana/k6 run - < epic-4068-permission-load-test.js

# With results output
docker run -i grafana/k6 run --out json=results.json - < epic-4068-permission-load-test.js

# Cloud execution (k6 Cloud)
k6 cloud epic-4068-permission-load-test.js
```

#### Performance Profiling

```bash
# Backend: dotnet-trace
dotnet tool install --global dotnet-trace
dotnet-trace collect --process-id $(pgrep -f Api.dll) --duration 00:00:30

# Frontend: Lighthouse CI
cd apps/web
pnpm add -D @lhci/cli
pnpm exec lhci autorun --collect.url=http://localhost:3000

# Chrome DevTools Protocol (headless profiling)
pnpm exec playwright test --trace on
# View trace: pnpm exec playwright show-trace trace.zip
```

---

### Maintenance Commands

#### Database Maintenance

```bash
# Vacuum analyze (after migration)
docker exec meepleai-postgres psql -U postgres -d meepleai -c "VACUUM ANALYZE \"Users\";"

# Reindex (if query performance degrades)
docker exec meepleai-postgres psql -U postgres -d meepleai -c "REINDEX TABLE CONCURRENTLY \"Users\";"

# Check database size
docker exec meepleai-postgres psql -U postgres -d meepleai -c "SELECT pg_database_size('meepleai');"

# Check table sizes
docker exec meepleai-postgres psql -U postgres -d meepleai -c "SELECT schemaname, tablename, pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) AS size FROM pg_tables WHERE schemaname = 'public' ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC LIMIT 10;"
```

#### Cache Management

```bash
# Clear Redis cache (all keys)
docker exec meepleai-redis redis-cli FLUSHDB

# Clear permission cache only
docker exec meepleai-redis redis-cli --scan --pattern "permission:*" | xargs docker exec meepleai-redis redis-cli DEL

# Check cache size
docker exec meepleai-redis redis-cli INFO memory

# Monitor cache in real-time
docker exec -it meepleai-redis redis-cli MONITOR
```

---

### Troubleshooting Commands

#### Debug Backend Issues

```bash
# Check API health
curl http://localhost:8080/health
curl http://localhost:8080/health/epic-4068

# Test permission endpoint (with auth)
TOKEN=$(curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"password"}' \
  | jq -r '.accessToken')

curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/v1/permissions/me | jq .

# Check database connection
dotnet run -- test-db-connection

# View EF Core generated SQL
export ASPNETCORE_ENVIRONMENT=Development
dotnet run
# Look for SQL queries in console output
```

#### Debug Frontend Issues

```bash
# Check TypeScript errors
pnpm typecheck 2>&1 | tee typecheck.log
grep "error TS" typecheck.log

# Check bundle size
pnpm build
pnpm exec next-bundle-analyzer

# Profile React components
pnpm dev
# Open: http://localhost:3000
# React DevTools → Profiler tab → Record

# Network inspection (API calls)
# Browser DevTools → Network tab → Filter: /permissions
```

#### Debug Test Failures

```bash
# Run single test with verbose output
dotnet test --filter "FullyQualifiedName~PermissionTests.Check_WithOrLogic" --logger "console;verbosity=detailed"

# Run test in debug mode (attach debugger)
# In VS Code: Set breakpoint → F5 (Start Debugging) → Select ".NET Core Attach" → Select Api process

# Frontend test debug
pnpm test -- TagStrip.test.tsx --reporter=verbose

# E2E test with screenshots on failure
pnpm exec playwright test --screenshot=only-on-failure

# E2E test with video recording
pnpm exec playwright test --video=on
```

---

### Utility Commands

#### Code Quality

```bash
# Format all C# files
dotnet format

# Format specific file
dotnet format apps/api/src/Api/Program.cs

# ESLint specific file
pnpm exec eslint apps/web/src/components/ui/tags/TagStrip.tsx --fix

# Prettier all files
pnpm format

# Find todos/fixmes
grep -r "TODO\|FIXME" apps/api/src/Api apps/web/src --exclude-dir=node_modules
```

#### Metrics Collection

```bash
# Count lines of code (Epic #4068 only)
find apps/api/src/Api/BoundedContexts/Administration -name "*.cs" | xargs wc -l

# Count test files
find tests -name "*Permission*.cs" | wc -l

# Count documentation words
wc -w docs/**/*epic-4068*.md
```

---

## 🚀 One-Command Workflows

### Full Development Setup (from scratch)

```bash
#!/bin/bash
# setup-epic-4068.sh - Complete setup in one command

set -e

echo "🚀 Setting up Epic #4068..."

# Infrastructure
echo "📦 Starting infrastructure..."
cd infra && docker compose up -d postgres redis
cd ..

# Backend
echo "🔧 Setting up backend..."
cd apps/api/src/Api
dotnet restore
dotnet ef database update
cd ../../../..

# Frontend
echo "🎨 Setting up frontend..."
cd apps/web
pnpm install
cd ../..

# Verify
echo "✅ Verifying setup..."
curl -f http://localhost:5432 > /dev/null 2>&1 && echo "✓ PostgreSQL ready" || echo "✗ PostgreSQL not ready"
curl -f http://localhost:6379 > /dev/null 2>&1 && echo "✓ Redis ready" || echo "✗ Redis not ready"

echo "🎉 Setup complete! Run 'dotnet run' (API) and 'pnpm dev' (Web)"
```

Make executable: `chmod +x setup-epic-4068.sh`

Run: `./setup-epic-4068.sh`

---

### Full Test Suite

```bash
#!/bin/bash
# test-epic-4068.sh - Run all Epic #4068 tests

set -e

echo "🧪 Running Epic #4068 test suite..."

# Backend
echo "📊 Backend tests..."
cd apps/api/src/Api
dotnet test --filter Epic=4068 /p:CollectCoverage=true /p:CoverageMinimum=90

# Frontend
echo "🎨 Frontend tests..."
cd ../../../web
pnpm test -- Tag Permission Agent --coverage

# E2E
echo "🌐 E2E tests..."
pnpm test:e2e -- tests/e2e/epic-4068/

# Accessibility
echo "♿ Accessibility tests..."
pnpm test:a11y

echo "✅ All tests passed!"
```

---

### Deploy to Production

```bash
#!/bin/bash
# deploy-epic-4068.sh - Production deployment

set -e

echo "🚀 Deploying Epic #4068 to production..."

# Backup
echo "💾 Creating backup..."
ssh production-server "./backup-database.sh"

# Build & push
echo "🏗️ Building images..."
docker build -t ghcr.io/meepleai/api:v1.5 apps/api
docker build -t ghcr.io/meepleai/web:v1.5 apps/web
docker push ghcr.io/meepleai/api:v1.5
docker push ghcr.io/meepleai/web:v1.5

# Deploy
echo "📦 Deploying..."
ssh production-server << 'EOF'
  cd /opt/meepleai
  docker compose pull
  docker compose up -d --no-deps api web
  docker exec meepleai-api dotnet ef database update
EOF

# Health check
echo "🏥 Health check..."
sleep 30
curl -f https://api.meepleai.com/health/epic-4068 || exit 1

echo "✅ Deployment complete!"
```

---

## 📚 Command Cheat Sheet (Print This!)

```
DEVELOPMENT
  dotnet run                → Run API
  pnpm dev                  → Run Web
  docker compose up -d      → Start infra

TESTING
  dotnet test --filter Epic=4068  → Backend tests
  pnpm test -- Tag                → Frontend tests
  pnpm test:e2e                   → E2E tests

DATABASE
  dotnet ef migrations add Name   → Create migration
  dotnet ef database update       → Apply migration
  docker exec postgres psql ...   → Query database

GIT
  git checkout -b feature/issue-N → New branch
  gh pr create --base main-dev    → Create PR
  gh pr merge --squash            → Merge PR

MONITORING
  curl /health/epic-4068          → Health check
  docker logs api | grep permission → View logs
  open http://localhost:3001       → Grafana

DEPLOYMENT
  docker build -t api:v1.5 apps/api → Build image
  ssh prod "docker compose up -d"   → Deploy
  ./deploy-epic-4068.sh             → Full deploy
```

---

**Bookmark this page!** Quick command reference for daily development.

**Print version**: `pandoc COMMAND-REFERENCE-EPIC-4068.md -o commands.pdf`
