# 🗺️ Development Roadmap Guide

**Single source of truth for MeepleAI development planning**

## Quick Access

📄 **Main Roadmap:** [DEVELOPMENT-ROADMAP.html](./DEVELOPMENT-ROADMAP.html)

Open in browser to see visual timeline and dual-terminal fullstack execution strategy.

---

## What's New (2026-02-13)

✅ **Unified Fullstack Roadmap**
- Consolidated all scattered roadmap files into single HTML visualization
- Removed obsolete roadmaps with closed issues (16 issues completed!)
- Created dual-terminal **fullstack** strategy (each terminal does frontend + backend)
- Only 5 active issues remaining (76% progress overall)

❌ **Deprecated Files** (Deleted)
- `docs/04-features/admin-dashboard-enterprise/ROADMAP.html`
- `docs/claudedocs/roadmap-shared-game-workflows.md`
- `docs/claudedocs/sequenza-implementazione-finale.md`

---

## Current Status (2026-02-13)

### Epic Progress
- **Epic #4136 (PDF Wizard):** 93% complete (14/15 issues ✅)
  - Backend: 6/7 done
  - Frontend: 8/8 done ✅
  - Testing: 1 remaining (#4160)

- **Epic #4071 (PDF Status):** 33% complete (2/6 issues ✅)
  - Pipeline: #4215 ✅, #4216 ✅
  - Real-Time: #4217, #4218 (P1)
  - Enhancement: #4219, #4220 (P2)

- **Agent System:** 100% complete ✅
  - #3809 ✅, #4229 ✅, #4230 ✅

### Active Issues: 5 total
**Terminal 1:** #4217 (P1, large), #4218 (P1, medium)
**Terminal 2:** #4219 (P2, medium), #4220 (P2, medium), #4160 (test)

---

## Dual Fullstack Terminal Strategy

### Why Fullstack Per Terminal?

**Traditional Approach (❌ OLD):**
```
Terminal 1: All Frontend  →  Blocks on backend APIs
Terminal 2: All Backend   →  Can't test UI until Terminal 1 ready
```

**Fullstack Approach (✅ NEW):**
```
Terminal 1: Feature A (Frontend + Backend + Tests)  →  Complete vertical slice
Terminal 2: Feature B (Frontend + Backend + Tests)  →  Complete vertical slice
```

### Benefits
1. **Context Preservation:** Developer maintains full feature context (UI to DB)
2. **Immediate Integration:** Frontend can call backend immediately (same terminal)
3. **Minimal Cross-Dependencies:** Terminals rarely block each other
4. **Faster Testing:** Complete E2E testing without waiting for other terminal

---

## Terminal 1: Real-Time Pipeline (P1 - Critical)

### Focus
Real-time status tracking per PDF processing con SSE streaming

### Working Directory
```bash
cd D:\Repositories\meepleai-monorepo-dev
```

### Sequence
1. **Day 1-5:** #4217 Multi-Location Status UI (Frontend - Large)
   - Create React components per status display
   - Integrate shadcn/ui components
   - State management con real-time updates
   - Locations: dashboard, detail page, list view

2. **Day 6-7:** #4218 Real-Time SSE Updates (Fullstack - Medium)
   - **Backend:** SSE endpoint + Redis pub/sub + HybridCache
   - **Frontend:** EventSource API + reconnection + UI updates
   - Integration testing

3. **Day 8:** E2E Validation
   - Playwright tests
   - Manual QA
   - Performance check

### Commands
```bash
# Frontend dev server
cd apps/web
pnpm dev              # localhost:3000
pnpm test             # Unit tests
pnpm test:e2e         # E2E tests

# Backend API server
cd apps/api/src/Api
dotnet run            # localhost:8080
dotnet test           # All tests
```

### Estimate
**4-7 giorni lavorativi** (depends on complexity of UI components)

---

## Terminal 2: Metrics & Completion (P2 - Enhancement)

### Focus
Performance metrics, notifications, e completamento test coverage

### Working Directory
```bash
cd D:\Repositories\meepleai-monorepo-dev
```

### Sequence
1. **Day 1-2:** #4219 Duration Metrics & ETA (Fullstack - Medium)
   - **Backend:** StatsD metrics + duration tracking + ETA calculation
   - **Frontend:** Metrics dashboard + progress bars con ETA
   - Integration

2. **Day 3-4:** #4220 Multi-Channel Notifications (Fullstack - Medium)
   - **Backend:** UserNotifications BC + email templates + queue
   - **Frontend:** Toast notifications + notification center UI
   - Email/push integration (opzionale)

3. **Day 5-6:** #4160 Wizard Integration Tests (Backend - Medium)
   - xUnit integration tests
   - Testcontainers setup
   - Coverage target: ≥90%

4. **Day 7:** Final Validation
   - Run all test suites
   - Performance validation
   - Documentation update

### Commands
```bash
# Backend first
cd apps/api/src/Api
dotnet run
dotnet test --filter "Category=Integration"

# Then frontend
cd apps/web
pnpm dev
pnpm test:coverage
```

### Estimate
**4-6 giorni lavorativi**

---

## Parallel Execution Workflow

### Day 1: Setup
```bash
# Terminal 1
git checkout frontend-dev && git pull
git checkout -b feature/issue-4217-status-ui
cd apps/web && pnpm dev

# Terminal 2 (different session)
git checkout main-dev && git pull
git checkout -b feature/issue-4219-metrics
cd apps/api/src/Api && dotnet run
```

### Day 2-7: Development
Both terminals work independently on their sequences. Minimal coordination needed.

### Day 8+: Integration & Validation
```bash
# Terminal 1: Keep frontend running
cd apps/web && pnpm dev

# Terminal 2: Keep backend running + run all tests
cd apps/api/src/Api && dotnet run
# In another tab:
dotnet test && cd ../../../web && pnpm test:e2e
```

---

## Dependencies & Coordination

### Minimal Cross-Dependencies
- Terminal 1 and Terminal 2 features are largely independent
- Both can progress in parallel without blocking

### Coordination Points
| When | What | Why |
|------|------|-----|
| Day 1 | Both start simultaneously | No dependencies |
| Day 4 | Quick sync (optional) | Verify no conflicts |
| Day 8 | Final integration | E2E testing + QA |

---

## Success Criteria

### Terminal 1 Success
- [ ] #4217: Status UI in dashboard, detail, list ✓
- [ ] #4218: SSE streaming working ✓
- [ ] Real-time updates visible in UI ✓
- [ ] E2E test passing ✓

### Terminal 2 Success
- [ ] #4219: Metrics dashboard functional ✓
- [ ] #4220: Notifications working (toast + email) ✓
- [ ] #4160: Integration tests ≥90% coverage ✓
- [ ] All tests passing ✓

### Overall Success
- [ ] Both epics complete
- [ ] All 5 issues closed
- [ ] Test coverage targets met (BE ≥90%, FE ≥85%)
- [ ] Performance benchmarks passed
- [ ] No regressions in existing features

---

## Git Workflow

### Branch Strategy
```bash
# Terminal 1 branches
feature/issue-4217-status-ui      → frontend-dev (Frontend focus)
feature/issue-4218-sse-updates    → main-dev (Fullstack)

# Terminal 2 branches
feature/issue-4219-metrics        → main-dev (Fullstack)
feature/issue-4220-notifications  → main-dev (Fullstack)
feature/issue-4160-integration    → main-dev (Backend)
```

### PR Targets
**CRITICAL:** PRs merge to **parent branch**, not always `main`!
- Frontend work → PR to `frontend-dev`
- Fullstack/Backend → PR to `main-dev`

### Commit Format
```
feat(pdf): add multi-location status UI components

Implements issue #4217
- Dashboard status widget
- Detail page progress bar
- List view status badges

Co-Authored-By: Claude Sonnet 4.5 (1M context) <noreply@anthropic.com>
```

---

## Testing Strategy

### Terminal 1 Testing
```bash
# Unit tests (Vitest)
cd apps/web
pnpm test
pnpm test:coverage  # Target: ≥85%

# E2E tests (Playwright)
pnpm test:e2e

# Type safety
pnpm typecheck
```

### Terminal 2 Testing
```bash
# Backend tests (xUnit)
cd apps/api/src/Api
dotnet test                                # All
dotnet test --filter "Category=Unit"       # Unit
dotnet test --filter "Category=Integration" # Integration
dotnet test /p:CollectCoverage=true        # Coverage ≥90%
```

### Integration Testing
Run both terminals simultaneously:
```bash
# Terminal 1: Frontend dev server
cd apps/web && pnpm dev  # :3000

# Terminal 2: Backend API server
cd apps/api/src/Api && dotnet run  # :8080

# Test full flow
curl http://localhost:8080/health
open http://localhost:3000/admin/shared-games
```

---

## Troubleshooting

### Terminal 1 Issues

**Frontend can't reach backend**
```bash
# Check backend running
curl http://localhost:8080/health

# Check .env.local
cat apps/web/.env.local | grep NEXT_PUBLIC_API_URL
# Should be: http://localhost:8080
```

**TypeScript errors**
```bash
pnpm typecheck        # Check errors
rm -rf .next          # Clear cache
pnpm dev              # Restart
```

**Tests failing**
```bash
# Check test environment
cat apps/web/.env.test

# Run specific test
pnpm test -- status-ui.test.tsx
```

### Terminal 2 Issues

**Database connection failed**
```bash
# Check PostgreSQL
docker compose ps postgres

# Check secrets
cat infra/secrets/database.secret

# Restart if needed
docker compose restart postgres
```

**Tests hanging**
```bash
# Kill testhost processes (Windows)
taskkill //IM testhost.exe //F

# Clean build
dotnet clean
dotnet build
dotnet test
```

**API not responding**
```bash
# Check port 8080
netstat -ano | findstr :8080

# Kill process if needed
taskkill /PID <PID> /F

# Restart
dotnet run
```

---

## FAQ

**Q: Can both terminals work simultaneously?**
A: Yes! That's the whole point. Minimal dependencies allow parallel development.

**Q: What if I only have one developer?**
A: Work on Terminal 1 sequence first (P1 critical), then Terminal 2 (P2 enhancement).

**Q: How do I switch between terminals?**
A: Each terminal is a separate branch. Use `git checkout <branch>` to switch context.

**Q: Do both terminals need to sync frequently?**
A: No. Only coordination points are Day 1 (start) and Day 8 (final integration).

**Q: What if an issue blocks both terminals?**
A: Unlikely with current dependencies. If it happens, prioritize the blocker.

**Q: Can I run both dev servers at once?**
A: Yes! Frontend (:3000) + Backend (:8080) can run simultaneously for live integration.

---

## References

### Epic Specifications
- [Epic #4136: PDF Wizard](https://github.com/DegrassiAaron/meepleai-monorepo/issues/4136)
- [Epic #4071: PDF Status Tracking](https://github.com/DegrassiAaron/meepleai-monorepo/issues/4071)

### Documentation
- [CLAUDE.md](../CLAUDE.md) - Development standards
- [Testing Guide](./05-testing/README.md) - Test patterns
- [Docker Quick Start](./02-development/docker/quick-start.md) - Infrastructure setup

### API Endpoints
- Wizard API: http://localhost:8080/scalar/v1#tag/SharedGameCatalog
- Metrics: http://localhost:8080/metrics (if enabled)
- Health: http://localhost:8080/health

---

**Last Updated:** 2026-02-13
**Owner:** PM Agent
**Status:** 🔄 Active Development • 76% Complete
