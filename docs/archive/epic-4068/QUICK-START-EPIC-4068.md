# Epic #4068: Quick Start Guide

**Get Epic #4068 running in 15 minutes**

---

## Prerequisites

- .NET 9 SDK installed
- Node.js 20+ & pnpm installed
- Docker Desktop running
- Git repository cloned

---

## 5-Minute Backend Setup

```bash
# 1. Start infrastructure
cd infra
docker compose up -d postgres redis

# 2. Apply migrations (creates Status column, indexes)
cd ../apps/api/src/Api
dotnet ef database update

# 3. Run backend
dotnet run

# Expected: Listening on http://localhost:8080

# 4. Test permission endpoint
curl http://localhost:8080/api/v1/permissions/me
# Expected: 401 Unauthorized (needs auth) ✓
```

**Backend ready!** ✅

---

## 5-Minute Frontend Setup

```bash
# 1. Install dependencies
cd apps/web
pnpm install

# 2. Run frontend
pnpm dev

# Expected: Ready on http://localhost:3000

# 3. Open browser
# Navigate to: http://localhost:3000/games
```

**Frontend ready!** ✅

---

## 5-Minute Test Run

```bash
# Backend tests
cd apps/api/src/Api
dotnet test --filter "Epic=4068"

# Frontend tests
cd apps/web
pnpm test -- Tag Permission Agent

# Expected: All tests passing ✓
```

**Tests passing!** ✅

---

## Quick Feature Tour

### Permission System

1. **Login** as test user (free@example.com / password)
2. **Navigate** to /games
3. **Observe**: Wishlist button visible, bulk select hidden (Free tier)
4. **Logout, login** as pro@example.com
5. **Observe**: Bulk select checkbox now visible (Pro tier)

### Tag System

1. **Navigate** to /games
2. **Observe**: Tags on left edge of cards (New, Sale, etc.)
3. **Hover** "+N" badge to see overflow tooltip

### Smart Tooltips

1. **Hover** any info icon (ℹ️)
2. **Observe**: Tooltip positions itself intelligently
3. **Scroll** to bottom of page, hover tooltip near edge
4. **Observe**: Tooltip flips to stay on-screen

### Agent Metadata

1. **Navigate** to /agents (requires Pro tier account)
2. **Observe**: Agent cards show status badge, model info, stats
3. **Hover** model badge to see parameters

### Collection Limits

1. **Navigate** to /collection
2. **Observe**: Progress bars for games and storage
3. **Check** color coding (green/yellow/red)

---

## Common First-Time Issues

### Issue: "dotnet ef database update" fails

**Solution**: Ensure PostgreSQL is running
```bash
docker compose ps postgres
# Expected: Up (healthy)
```

### Issue: Frontend shows "Permission loading forever"

**Solution**: Backend not running or CORS issue
```bash
# Check backend health
curl http://localhost:8080/health

# Check CORS configured for localhost:3000
```

### Issue: "Module not found: @/types/permissions"

**Solution**: TypeScript path not configured
```bash
# Verify tsconfig.json has:
# "paths": { "@/*": ["./src/*"] }

# Restart TypeScript server in VS Code:
# Ctrl+Shift+P → "TypeScript: Restart TS Server"
```

---

## Next Steps

**After Quick Start**:

1. **Read**: [Implementation Guide](docs/02-development/epic-4068-implementation-guide.md)
2. **Explore**: [Examples](examples/epic-4068/)
3. **Implement**: Pick an issue from #4177-#4186
4. **Test**: Follow [E2E Test Scenarios](docs/05-testing/frontend/epic-4068-e2e-test-scenarios.md)

---

## Development Commands

```bash
# Backend
cd apps/api/src/Api

dotnet build                    # Build
dotnet run                      # Run
dotnet test                     # All tests
dotnet test --filter Epic=4068  # Epic tests only
dotnet ef migrations add Name   # Create migration
dotnet ef database update       # Apply migrations

# Frontend
cd apps/web

pnpm dev                        # Run dev server
pnpm build                      # Production build
pnpm test                       # All tests
pnpm test -- Tag                # Tag tests only
pnpm typecheck                  # TypeScript check
pnpm lint                       # ESLint
pnpm storybook                  # Component explorer

# Docker
cd infra

docker compose up -d                      # Start all
docker compose logs -f api                # View API logs
docker compose down                       # Stop all
docker compose down -v                    # Stop + delete volumes (⚠️ data loss!)

# Examples
cd examples/epic-4068

pnpm install                    # Install
pnpm dev                        # Run examples
pnpm test                       # Test examples
```

---

## Quick Reference

**API Endpoints**:
- GET /api/v1/permissions/me → User permissions
- GET /api/v1/permissions/check?feature=X → Check feature
- GET /health/epic-4068 → Health check

**Test Users** (seed data):
- free@example.com (Free tier, User role)
- normal@example.com (Normal tier, User role)
- pro@example.com (Pro tier, User role)
- admin@example.com (Free tier, Admin role)

Password: `password` (all accounts)

**Environment Variables**:
- See `.env.epic-4068.example` for complete list
- Copy to `.env.development` and fill in values

---

## Troubleshooting

**Problem**: Can't start? → `docker compose ps` (check PostgreSQL/Redis running)
**Problem**: Build fails? → `dotnet clean && dotnet build` or `rm -rf .next && pnpm build`
**Problem**: Tests fail? → Check [Troubleshooting Guide](docs/08-troubleshooting/epic-4068-common-issues.md)

---

## Get Help

- **Documentation**: See [Master Index](EPIC-4068-MASTER-IMPLEMENTATION-INDEX.md)
- **Issues**: https://github.com/DegrassiAaron/meepleai-monorepo/issues?q=epic%3A4068
- **Team**: #epic-4068 Slack channel
- **Email**: dev@meepleai.com

---

**Ready to build!** 🚀

Time from zero to running Epic #4068: **~15 minutes**
