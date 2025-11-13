# Git Worktree Setup Guide - MeepleAI Monorepo

**Created**: 2025-01-15
**Status**: ✅ Active Worktrees Configured

---

## 🎯 Current Worktree Configuration

### Active Worktrees

```
D:/Repositories/meepleai-monorepo/              (Main Worktree)
├── Branch: main
├── Focus: Backend Development
├── Apps: apps/api/ (primary), apps/web/ (read-only)
└── Purpose: Backend API, services, migrations, tests

D:/Repositories/meepleai-monorepo-frontend/     (Frontend Worktree)
├── Branch: frontend-dev
├── Focus: Frontend Development
├── Apps: apps/web/ (primary), apps/api/ (read-only)
└── Purpose: Frontend UI, components, pages, styles
```

### Verification Commands

```bash
# List all worktrees
git worktree list

# Expected output:
# D:/Repositories/meepleai-monorepo              8ac0ee51 [main]
# D:/Repositories/meepleai-monorepo-frontend     8ac0ee51 [frontend-dev]
```

---

## 📋 Daily Workflow

### Morning: Backend Development (Main Worktree)

```bash
# Terminal 1 - Backend Worktree
cd D:/Repositories/meepleai-monorepo

# Create feature branch for current task
git checkout -b feature/admin-dashboard-api

# Start backend development
cd apps/api
dotnet restore
dotnet run  # API server on :8080

# Development...
# Edit files in apps/api/src/Api/Services/
# Tests: dotnet test

# Commit when ready
git add .
git commit -m "feat(backend): AdminDashboardService implementation #877"
git push origin feature/admin-dashboard-api
```

### Afternoon: Frontend Development (Frontend Worktree)

```bash
# Terminal 2 - Frontend Worktree (SEPARATE directory!)
cd D:/Repositories/meepleai-monorepo-frontend

# Sync with main if needed
git pull origin main --rebase

# Start frontend development
cd apps/web
pnpm install  # First time only
pnpm dev  # Dev server on :3000, calls API :8080

# Development...
# Edit files in apps/web/src/
# Tests: pnpm test

# Commit when ready
git add .
git commit -m "feat(frontend): Dashboard page implementation #885"
git push origin frontend-dev
```

### Parallel Development (Key Advantage!)

**Both servers running simultaneously**:
```bash
# Terminal 1 (Backend)
cd D:/Repositories/meepleai-monorepo/apps/api
dotnet run  # :8080 - Stays running all day

# Terminal 2 (Frontend)
cd D:/Repositories/meepleai-monorepo-frontend/apps/web
pnpm dev  # :3000 - Consumes :8080 API in real-time

# Terminal 3 (Tests - Optional)
cd D:/Repositories/meepleai-monorepo-frontend/apps/web
pnpm test --watch  # Test feedback while developing
```

**Result**:
- ✅ Backend hot reload active (ASP.NET watch mode)
- ✅ Frontend hot reload active (Next.js Fast Refresh)
- ✅ Full-stack development with instant feedback
- ✅ **Zero context switching overhead**

---

## 🔄 Weekly Merge Workflow

### Friday Evening: Integrate Week's Work

```bash
# 1. Merge backend feature to develop/main
cd D:/Repositories/meepleai-monorepo
git checkout main
git pull origin main
git merge feature/admin-dashboard-api
git push origin main

# 2. Merge frontend feature to main
git checkout main
git merge frontend-dev
git push origin main

# 3. Sync frontend worktree with merged main
cd D:/Repositories/meepleai-monorepo-frontend
git checkout frontend-dev
git pull origin main --rebase
git push origin frontend-dev --force-with-lease

# 4. Cleanup merged branches (optional)
git branch -d feature/admin-dashboard-api
```

### Alternative: Pull Request Workflow (Recommended)

```bash
# 1. Push both branches
cd D:/Repositories/meepleai-monorepo
git push origin feature/admin-dashboard-api

cd D:/Repositories/meepleai-monorepo-frontend
git push origin frontend-dev

# 2. Create PRs
gh pr create --base main --head feature/admin-dashboard-api \
  --title "Backend: Admin Dashboard API" \
  --body "Implements AdminDashboardService (#877-880)"

gh pr create --base main --head frontend-dev \
  --title "Frontend: Dashboard UI" \
  --body "Implements Dashboard page and components (#881-886)"

# 3. Review & merge on GitHub

# 4. Pull main in both worktrees
cd D:/Repositories/meepleai-monorepo
git checkout main && git pull

cd D:/Repositories/meepleai-monorepo-frontend
git checkout frontend-dev
git pull origin main --rebase
```

---

## 🎨 VS Code Setup (2 Instances)

### Backend Instance (Worktree Main)

**Open Folder**: `D:/Repositories/meepleai-monorepo`

**Workspace Settings** (.vscode/settings.json):
```json
{
  "files.exclude": {
    "apps/web": true  // Hide frontend in backend workspace
  },
  "terminal.integrated.cwd": "${workspaceFolder}/apps/api"
}
```

**Recommended Extensions**:
- C# Dev Kit
- .NET Core Test Explorer
- REST Client (test API endpoints)

### Frontend Instance (Worktree Frontend)

**Open Folder**: `D:/Repositories/meepleai-monorepo-frontend`

**Workspace Settings** (.vscode/settings.json):
```json
{
  "files.exclude": {
    "apps/api": true  // Hide backend in frontend workspace
  },
  "terminal.integrated.cwd": "${workspaceFolder}/apps/web"
}
```

**Recommended Extensions**:
- ESLint
- Prettier
- Tailwind CSS IntelliSense
- Jest Runner
- Error Lens

---

## 🚀 Quick Commands Reference

### Worktree Management

```bash
# List worktrees
git worktree list

# Add new worktree
git worktree add <path> <branch>
git worktree add ../meepleai-backend-dev -b backend-dev

# Remove worktree
git worktree remove <path>
git worktree remove ../meepleai-monorepo-frontend

# Prune deleted worktrees
git worktree prune

# Lock worktree (prevent removal)
git worktree lock <path>
git worktree unlock <path>
```

### Branch Operations

```bash
# In main worktree
cd D:/Repositories/meepleai-monorepo
git checkout -b feature/new-backend-feature

# In frontend worktree (independent)
cd D:/Repositories/meepleai-monorepo-frontend
git checkout -b feature/new-frontend-feature

# Both branches can exist and be worked on simultaneously!
```

### Synchronization

```bash
# Update frontend worktree with backend changes
cd D:/Repositories/meepleai-monorepo-frontend
git fetch origin
git rebase origin/main

# Update main worktree with frontend changes
cd D:/Repositories/meepleai-monorepo
git fetch origin
git merge origin/frontend-dev
```

---

## ⚠️ Important Notes

### DO's ✅

- ✅ **Keep worktrees in sync**: Pull from main regularly
- ✅ **Use feature branches**: Create branches for each task
- ✅ **Commit frequently**: Small, atomic commits
- ✅ **Test before push**: Run tests in each worktree
- ✅ **Document changes**: Clear commit messages
- ✅ **Weekly merges**: Integrate work every Friday

### DON'Ts ❌

- ❌ **Don't edit same file**: Backend worktree → apps/api only, Frontend worktree → apps/web only
- ❌ **Don't delete .git/**: Shared across worktrees
- ❌ **Don't force push main**: Only force-with-lease on feature branches
- ❌ **Don't forget to sync**: Pull main regularly to avoid conflicts
- ❌ **Don't work on main**: Always use feature branches

---

## 🐛 Troubleshooting

### Issue: "worktree already exists"

```bash
# Remove existing worktree
git worktree remove D:/Repositories/meepleai-monorepo-frontend

# Prune references
git worktree prune

# Try again
git worktree add D:/Repositories/meepleai-monorepo-frontend -b frontend-dev
```

### Issue: "branch already exists"

```bash
# Delete branch and recreate worktree
git branch -D frontend-dev
git worktree add D:/Repositories/meepleai-monorepo-frontend -b frontend-dev
```

### Issue: "worktree locked"

```bash
git worktree unlock D:/Repositories/meepleai-monorepo-frontend
git worktree remove D:/Repositories/meepleai-monorepo-frontend
```

### Issue: Merge conflicts between worktrees

```bash
# In main worktree
cd D:/Repositories/meepleai-monorepo
git checkout main
git pull origin main

# In frontend worktree
cd D:/Repositories/meepleai-monorepo-frontend
git checkout frontend-dev
git rebase main

# Resolve conflicts if any
git add .
git rebase --continue
```

---

## 📊 Performance Comparison

### Without Worktree (Sequential)

```
Task: Develop backend API + frontend UI

1. Work on backend (2 hours)
2. Git stash (save work)
3. Switch to frontend branch
4. Git stash pop
5. Work on frontend (2 hours)
6. Git stash
7. Switch back to backend
8. Git stash pop
9. Continue backend...

Total time: 4h work + 0.5h context switching = 4.5h
```

### With Worktree (Parallel)

```
Task: Develop backend API + frontend UI

Terminal 1 (Backend Worktree):
1. Work on backend (2 hours)
2. Commit directly (no stash needed)
3. Continue working...

Terminal 2 (Frontend Worktree) - Simultaneously:
1. Work on frontend (2 hours)
2. Commit directly (no stash needed)
3. Continue working...

Total time: 4h work + 0h context switching = 4h
Efficiency gain: 12.5% + parallel builds/tests
```

---

## 🎓 Best Practices

### 1. Branch Naming Convention

```bash
# Backend branches (Main worktree)
feature/admin-dashboard-api
feature/infrastructure-monitoring
fix/auth-validation-bug

# Frontend branches (Frontend worktree)
frontend-dev  # Main frontend integration branch
feature/dashboard-ui
feature/shadcn-foundation
fix/accessibility-contrast
```

### 2. Commit Message Convention

```bash
# Backend commits
git commit -m "feat(backend): AdminDashboardService GetStatsAsync #877"
git commit -m "fix(api): Null check in InfrastructureMonitoring #894"
git commit -m "test(backend): AdminDashboardService unit tests #880"

# Frontend commits
git commit -m "feat(frontend): AdminLayout component #881"
git commit -m "style(ui): Update StatCard accessibility #882"
git commit -m "test(frontend): Dashboard E2E tests #888"
```

### 3. Testing Before Merge

```bash
# In backend worktree
cd D:/Repositories/meepleai-monorepo
dotnet test  # All tests must pass

# In frontend worktree
cd D:/Repositories/meepleai-monorepo-frontend
pnpm test  # All tests must pass
pnpm typecheck  # No TypeScript errors
pnpm lint  # No linting errors

# Integration test (both worktrees)
# Backend: dotnet run :8080
# Frontend: pnpm dev :3000 → Test full stack manually
```

---

## 📚 Additional Resources

- **Git Worktree Docs**: https://git-scm.com/docs/git-worktree
- **Calendario Sviluppo**: `claudedocs/calendario-sviluppo-1-persona-2025.md`
- **Frontend Roadmap**: `claudedocs/frontend-improvement-roadmap-2025.md`
- **Issue Summary**: `claudedocs/frontend-issues-summary.md`

---

## ✅ Verification Checklist

After setup, verify:

- [ ] `git worktree list` shows 2 worktrees
- [ ] Main worktree on `main` branch
- [ ] Frontend worktree on `frontend-dev` branch
- [ ] Backend can run: `cd apps/api && dotnet run`
- [ ] Frontend can run: `cd apps/web && pnpm dev`
- [ ] Both dev servers running simultaneously
- [ ] Frontend can call backend API (http://localhost:8080)
- [ ] Commits work independently in each worktree

**Status**: ✅ Ready for Development!

---

**Last Updated**: 2025-01-15
**Maintained By**: Development Team
**Next Review**: After Phase 1 completion
