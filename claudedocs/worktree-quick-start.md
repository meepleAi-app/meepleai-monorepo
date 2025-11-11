# Git Worktree Quick Start - Ready to Code! 🚀

**Setup Status**: ✅ COMPLETE
**Date**: 2025-01-15

---

## ✅ Worktree Setup Complete!

### Active Worktrees

```
✅ Worktree 1 (Backend): D:/Repositories/meepleai-monorepo
   Branch: main
   Focus: Backend API Development

✅ Worktree 2 (Frontend): D:/Repositories/meepleai-monorepo-frontend
   Branch: frontend-dev
   Focus: Frontend UI Development
```

Verification: `git worktree list`

---

## 🎬 Start Developing NOW (3 Steps)

### Step 1: Open 2 VS Code Windows (1 minute)

```bash
# Terminal 1 - Backend
code D:/Repositories/meepleai-monorepo

# Terminal 2 - Frontend
code D:/Repositories/meepleai-monorepo-frontend
```

### Step 2: Start Backend API (2 minutes)

```bash
# In Backend VS Code terminal
cd D:/Repositories/meepleai-monorepo/apps/api
dotnet restore  # First time only
dotnet run      # Starts API on :8080

# ✅ Backend API running!
# Test: http://localhost:8080/health
```

### Step 3: Start Frontend Dev Server (2 minutes)

```bash
# In Frontend VS Code terminal
cd D:/Repositories/meepleai-monorepo-frontend/apps/web
pnpm install  # First time only
pnpm dev      # Starts frontend on :3000

# ✅ Frontend running!
# Test: http://localhost:3000
```

**🎉 Total Setup Time: 5 minutes → Full-stack development ready!**

---

## 💡 Typical Workflow Example

### Scenario: Develop Admin Dashboard (Issue #877 Backend + #881 Frontend)

#### Morning (9:00-12:00): Backend Development

```bash
# Backend VS Code (Worktree 1)
cd D:/Repositories/meepleai-monorepo

# Create feature branch
git checkout -b feature/admin-dashboard-api

# Start API server
cd apps/api
dotnet run  # Leave running in Terminal 1

# Open new terminal in VS Code (Ctrl+Shift+`)
# Develop AdminDashboardService
code src/Api/Services/AdminDashboardService.cs

# Run tests
dotnet test

# Commit
git add src/Api/Services/AdminDashboardService.cs
git commit -m "feat(backend): AdminDashboardService GetStatsAsync #877"
```

#### Afternoon (13:00-18:00): Frontend Development

```bash
# Frontend VS Code (Worktree 2) - DIFFERENT WINDOW!
cd D:/Repositories/meepleai-monorepo-frontend

# Frontend dev server (calls backend :8080)
cd apps/web
pnpm dev  # Leave running in Terminal 1

# Open new terminal
# Develop AdminLayout component
code src/components/AdminLayout.tsx

# Test
pnpm test

# Commit
git add src/components/AdminLayout.tsx
git commit -m "feat(frontend): AdminLayout component #881"
```

#### Evening (18:00-19:00): Integration Test

```bash
# Both servers already running:
# - Backend: http://localhost:8080 (from morning)
# - Frontend: http://localhost:3000 (from afternoon)

# Browser: http://localhost:3000/admin
# ✅ Frontend calls backend API in real-time
# ✅ Full-stack integration working!

# Push both branches
cd D:/Repositories/meepleai-monorepo
git push origin feature/admin-dashboard-api

cd D:/Repositories/meepleai-monorepo-frontend
git push origin frontend-dev
```

**Day Result**:
- ✅ Backend service implemented
- ✅ Frontend component implemented
- ✅ Integration tested
- ✅ Both committed separately
- ✅ **Zero context switching overhead**

---

## 🎯 Week 1 Roadmap (Next 5 Days)

### Monday: AdminDashboardService Foundation
**Worktree**: Backend (Main)
**Issue**: #877

```bash
cd D:/Repositories/meepleai-monorepo
git checkout -b feature/admin-dashboard-api

# Tasks:
# - Create AdminDashboardService.cs
# - Implement GetStatsAsync() skeleton
# - Setup dependency injection in Program.cs
# - Unit test structure

# Time: 6-8 hours
```

### Tuesday: Dashboard Stats Implementation
**Worktree**: Backend (Main)
**Issue**: #877, #878

```bash
# Continue on feature/admin-dashboard-api

# Tasks:
# - Complete GetStatsAsync() implementation
# - Implement GetActivityAsync()
# - Database queries optimization
# - Unit tests

# Time: 6-8 hours
```

### Wednesday: HybridCache & API Endpoints
**Worktree**: Backend (Main)
**Issue**: #879, #877

```bash
# Tasks:
# - HybridCache integration (1min TTL)
# - API endpoint: GET /api/v1/admin/dashboard/stats
# - API endpoint: GET /api/v1/admin/dashboard/activity
# - Integration tests

# Time: 6-8 hours
```

### Thursday: API Polish & Testing
**Worktree**: Backend (Main)
**Issue**: #880

```bash
# Tasks:
# - Error handling
# - Validation
# - Performance test (<1s response)
# - Coverage verification (90%+)

# Time: 6-8 hours
```

### Friday: Code Review & Frontend Prep
**Worktree**: Both

```bash
# Morning: Backend code review
# - Refactoring
# - Documentation
# - PR creation

# Afternoon: Frontend prep (switch to Frontend worktree!)
cd D:/Repositories/meepleai-monorepo-frontend
git pull origin main --rebase

# - Study backend API contracts
# - Plan AdminLayout component structure
# - Setup component files

# Time: 4h backend + 4h frontend prep
```

**Week 1 Deliverable**: ✅ Backend API complete, ready for frontend integration

---

## 🔧 Terminal Cheat Sheet

### Backend Development (Main Worktree)

```bash
# Navigate
cd D:/Repositories/meepleai-monorepo

# Build
cd apps/api && dotnet build

# Test
dotnet test

# Run API
dotnet run  # :8080

# Watch mode (hot reload)
dotnet watch run

# Coverage
dotnet test /p:CollectCoverage=true
```

### Frontend Development (Frontend Worktree)

```bash
# Navigate
cd D:/Repositories/meepleai-monorepo-frontend

# Install dependencies
cd apps/web && pnpm install

# Dev server
pnpm dev  # :3000

# Test
pnpm test
pnpm test:watch  # Watch mode

# Type checking
pnpm typecheck

# Linting
pnpm lint

# Coverage
pnpm test:coverage
```

---

## 📈 Success Metrics (Week 1)

After Week 1, you should have:

- ✅ 2 active worktrees (main + frontend-dev)
- ✅ Backend API complete (#877-880)
- ✅ 4 commits on feature/admin-dashboard-api
- ✅ Unit tests 90%+ coverage
- ✅ Performance <1s validated
- ✅ Frontend worktree ready for Week 2
- ✅ Zero context switching overhead experienced

**Efficiency Gain**: 10-15% time savings vs sequential workflow

---

## 🎯 Next Steps

### Immediate (Today)
1. ✅ Worktrees created
2. ✅ Documentation created
3. ⏳ Start Week 1 Monday (Issue #877)

### Week 1 (Starting Tomorrow)
- [ ] Backend: AdminDashboardService (#877-880)
- [ ] Daily commits on feature/admin-dashboard-api
- [ ] Friday: PR creation + Week 2 prep

### Week 2 (Starting Next Monday)
- [ ] Switch to frontend worktree
- [ ] Frontend: Dashboard UI (#881-886)
- [ ] Integration with Week 1 backend API

---

**Ready to Start Development!** 🚀

Open both VS Code instances and begin coding!

---

**Document Version**: 1.0
**Status**: Active
**Last Updated**: 2025-01-15
