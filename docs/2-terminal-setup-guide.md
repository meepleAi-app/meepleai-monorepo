# 🚀 2-Terminal Parallel Development - Setup Guide

**Data**: 2026-02-11
**Roadmap**: 4 settimane, 47 issue, 2 terminal paralleli

---

## ✅ Environment Status

### Docker Services (All Healthy)
```
✅ PostgreSQL     :5432   (Database)
✅ Qdrant         :6333   (Vector DB)
✅ Redis          :6379   (Cache)
✅ API            :8080   (Backend)
✅ Web            :3000   (Frontend)
✅ Embedding      :8000   (AI Service)
✅ Reranker       :8003   (AI Service)
✅ SmolDocling    :8002   (PDF Service)
✅ Unstructured   :8001   (PDF Service)
✅ Grafana        :3001   (Monitoring)
✅ Prometheus     :9090   (Metrics)
```

### Dependencies
```
✅ Frontend: pnpm dependencies up-to-date
✅ Backend: dotnet packages restored
✅ Database: Migrations applied
✅ Secrets: Configured
```

---

## 🖥️ Terminal A Setup (Frontend-Heavy)

### Directory Structure
```
Terminal A workspace:
├── apps/web/src/
│   ├── components/ui/data-display/meeple-card/  (Week 1)
│   ├── app/(protected)/library/wishlist/        (Week 1)
│   ├── components/layout/navbar/                (Week 2)
│   ├── components/notifications/                (Week 2)
│   ├── app/(protected)/agents/                  (Week 3)
│   └── app/(protected)/settings/security/       (Week 4)
└── apps/web/e2e/
    └── epic-*.spec.ts (test files già creati)
```

### Terminal A Start Commands

**Posizione**: Terminale PowerShell/Bash #1

```bash
# 1. Naviga al workspace frontend
cd D:/Repositories/meepleai-monorepo-dev/apps/web

# 2. Avvia dev server con watch mode
pnpm dev

# Output atteso:
# ▲ Next.js 16.1.1
# - Local: http://localhost:3000
# - Ready in XXXms
```

### Terminal A - Week 1 Tasks

**Focus**: Epic #1 (MeepleCard UI) + Wishlist UI

```bash
# Quick Reference Commands

# Run unit tests (watch mode)
pnpm test -- --watch

# Run specific test file
pnpm test -- MeepleCard

# Run E2E tests
pnpm test:e2e -- epic-1-meeple-card.spec.ts

# Type checking
pnpm typecheck

# Linting
pnpm lint
```

**Issue Order** (recommended):
1. #4073 - Accessibility (1d) - `meeple-card/accessibility.tsx`
2. #4076 - Mobile Tags (1d) - `meeple-card/tag-list.tsx`
3. #4075 - Vertical Layout (1d) - `meeple-card/tag-container.tsx`
4. #4072 - Smart Tooltips (0.5d) - `meeple-card/tooltip-provider.tsx`
5. #4080 - Tests (1d) - `__tests__/meeple-card.test.tsx`
6. #4081 - Performance (0.5d) - `meeple-card/index.tsx` (memo)
7. #4114 - Wishlist (3d) - `app/(protected)/library/wishlist/page.tsx`

**Total**: 8 giorni effort

---

## 💻 Terminal B Setup (Backend-Heavy)

### Directory Structure
```
Terminal B workspace:
├── apps/api/src/Api/
│   ├── BoundedContexts/UserLibrary/         (Week 1)
│   ├── BoundedContexts/UserNotifications/   (Week 1)
│   ├── BoundedContexts/DocumentProcessing/  (Week 2)
│   ├── BoundedContexts/KnowledgeBase/       (Week 3)
│   └── BoundedContexts/Administration/      (Week 4)
└── tests/Api.Tests/
    └── (test files per bounded context)
```

### Terminal B Start Commands

**Posizione**: Terminale PowerShell/Bash #2

```bash
# 1. Naviga al workspace backend
cd D:/Repositories/meepleai-monorepo-dev/apps/api/src/Api

# 2. Avvia API con watch mode
dotnet watch run

# Output atteso:
# info: Microsoft.Hosting.Lifetime[14]
#       Now listening on: http://localhost:8080
# info: Microsoft.Hosting.Lifetime[0]
#       Application started
```

### Terminal B - Week 1 Tasks

**Focus**: Epic #1 (MeepleCard Backend) + Notifications Backend + Play Records Backend

```bash
# Quick Reference Commands

# Run all tests
dotnet test

# Run specific bounded context tests
dotnet test --filter "BoundedContext=UserLibrary"

# Run with coverage
dotnet test /p:CollectCoverage=true

# Build only (no run)
dotnet build

# Database migrations (if needed)
dotnet ef migrations add MigrationName
dotnet ef database update
```

**Issue Order** (recommended):
1. #4079 - Agent Type (0.5d) - `Application/Queries/GetGameDetailQuery.cs`
2. #4078 - Ownership (0.5d) - `Application/Queries/GetUserLibraryQuery.cs`
3. #4077 - Limits (0.5d) - `Application/Queries/GetUserTierLimitsQuery.cs`
4. #4074 - Permissions (0.5d) - `Infrastructure/Middleware/PermissionMiddleware.cs`
5. #4113 - Notifications (3d) - `BoundedContexts/UserNotifications/`
6. #4115 - Play Records (2d) - `BoundedContexts/GameManagement/`

**Total**: 7 giorni effort

---

## 🔄 Parallel Workflow

### Daily Routine (Entrambi Terminal)

**Morning (9:00)**:
```bash
# Sync con main-dev
git checkout main-dev
git pull origin main-dev

# Verifica status
git status
```

**During Work**:
```bash
# Commit frequenti (ogni feature/fix)
git add .
git commit -m "feat(scope): description (#issue)"

# Push regolare (almeno 2x/giorno)
git push origin main-dev
```

**Evening (18:00)**:
```bash
# Commit WIP se necessario
git add .
git commit -m "wip: [feature] - partial implementation"
git push origin main-dev
```

---

## 🧪 Test Fixtures Setup

### Crea Test Data

```bash
# Terminal A - Frontend test fixtures
cd apps/web
mkdir -p e2e/fixtures
```

**File da creare** (Terminal A):
```bash
# apps/web/e2e/fixtures/sample-game.json
{
  "id": "test-game-1",
  "title": "Test Game for E2E",
  "description": "Mock game for testing",
  "minPlayers": 2,
  "maxPlayers": 4
}

# apps/web/e2e/fixtures/sample-user.json
{
  "email": "test@meepleai.local",
  "password": "TestPassword123!",
  "name": "Test User"
}
```

**File da creare** (Terminal B):
```bash
# apps/api/tests/Api.Tests/Fixtures/
# Testcontainers già configurati
# Seed data per integration tests già presente
```

---

## 📋 Pre-Implementation Checklist

### Terminal A Checklist
- [x] Docker services running
- [x] pnpm dependencies installed
- [x] Dev server startable (`pnpm dev`)
- [x] Tests runnable (`pnpm test`)
- [ ] E2E browser configured (Playwright)
- [x] Test templates created
- [ ] Test fixtures prepared
- [x] Roadmap visualizzata

### Terminal B Checklist
- [x] Docker services running
- [x] dotnet packages restored
- [x] API startable (`dotnet run`)
- [x] Tests runnable (`dotnet test`)
- [x] Database migrations applied
- [x] Secrets configured
- [ ] Test data seeded
- [x] Roadmap visualizzata

---

## 🎯 Week 1 Kickoff Instructions

### Terminal A - First Task (#4073 Accessibility)

**Posizione**: `apps/web/src/components/ui/data-display/meeple-card/`

**Steps**:
1. Read existing MeepleCard component
2. Audit accessibility (keyboard nav, ARIA, contrast)
3. Implement accessibility enhancements
4. Write unit tests
5. Run E2E test: `pnpm test:e2e -- epic-1-meeple-card.spec.ts -g "Accessibility"`
6. Commit: `feat(meeple-card): add WCAG 2.1 AA accessibility (#4073)`

**Files to modify**:
- `meeple-card.tsx` (add ARIA labels, keyboard handlers)
- `meeple-card.test.tsx` (add accessibility tests)
- `index.tsx` (export accessibility utilities)

### Terminal B - First Task (#4079 Agent Type Backend)

**Posizione**: `apps/api/src/Api/BoundedContexts/GameManagement/`

**Steps**:
1. Read GameDetailQuery handler
2. Add agent entity type support to DTO
3. Enhance game detail response with agent data
4. Write unit tests
5. Run tests: `dotnet test --filter "Feature=GameDetail"`
6. Commit: `feat(game-management): add agent type support (#4079)`

**Files to modify**:
- `Application/Queries/GetGameDetailQuery.cs`
- `Application/DTOs/GameDetailDto.cs`
- `tests/Api.Tests/GameManagement/GetGameDetailQueryTests.cs`

---

## 🔧 Troubleshooting Setup

### Issue: Docker services not healthy
```bash
# Check logs
docker compose logs [service-name]

# Restart specific service
docker compose restart [service-name]

# Full restart
docker compose down && docker compose up -d
```

### Issue: Frontend dev server non parte
```bash
# Clear cache
rm -rf .next node_modules/.cache

# Reinstall
pnpm install

# Retry
pnpm dev
```

### Issue: Backend API non compila
```bash
# Clean
dotnet clean

# Restore
dotnet restore

# Build
dotnet build
```

### Issue: Tests fail on startup
```bash
# Frontend: Update snapshots
pnpm test -- -u

# Backend: Reset test database
# Testcontainers handles this automatically
```

---

## 📊 Setup Verification

### Run Verification Tests

**Terminal A**:
```bash
cd apps/web

# Verify build works
pnpm build

# Verify tests work
pnpm test -- --run

# Verify E2E setup
pnpm test:e2e -- --list
```

**Terminal B**:
```bash
cd apps/api/src/Api

# Verify build works
dotnet build

# Verify tests work
dotnet test

# Verify API responds
curl http://localhost:8080/health
```

**Expected**:
- ✅ All commands complete without errors
- ✅ Health endpoint returns 200 OK
- ✅ Test suites discovered and runnable

---

## 🚀 Ready to Start!

### Terminal A - Ready Commands
```bash
# Terminal A workspace ready at:
cd D:/Repositories/meepleai-monorepo-dev/apps/web

# Dev server running on:
http://localhost:3000

# First task:
# Implement #4073 - MeepleCard Accessibility
```

### Terminal B - Ready Commands
```bash
# Terminal B workspace ready at:
cd D:/Repositories/meepleai-monorepo-dev/apps/api/src/Api

# API running on:
http://localhost:8080
http://localhost:8080/scalar/v1 (API docs)

# First task:
# Implement #4079 - Agent Type Backend Support
```

---

## 📚 Quick Reference

### Roadmap Documents
- **Roadmap**: `docs/roadmap-parallel-2terminal.md`
- **Visualization**: `docs/implementation-flow-visualization.html` (opened in browser)
- **Gap Analysis**: `docs/ui-api-gap-analysis.md`

### Test Templates
- `apps/web/e2e/epic-1-meeple-card.spec.ts`
- `apps/web/e2e/epic-2-agent-system.spec.ts`
- `apps/web/e2e/epic-3-navbar.spec.ts`
- `apps/web/e2e/epic-4-pdf-status.spec.ts`
- `apps/web/e2e/gap-analysis-critical.spec.ts`

### Helper Scripts
- `scripts/analyze-ui-api-gaps.ps1` - Re-run gap analysis
- `scripts/consolidate-docs-verify.ps1` - Verify documentation

---

**Setup Status**: ✅ COMPLETE
**Ready for**: Implementation kickoff
**Next**: Start Terminal A (#4073) + Terminal B (#4079) in parallel
