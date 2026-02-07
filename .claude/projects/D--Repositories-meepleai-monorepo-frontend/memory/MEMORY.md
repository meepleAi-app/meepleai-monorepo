# MeepleAI Frontend - Session Memory

## Key Patterns
- **PowerShell for scripts on Windows**: Use `pwsh -File script.ps1` instead of inline `-c` for complex logic (escaping issues with bash→pwsh)
- **GitHub CLI**: `gh issue list/view` works well for issue analysis. Use `--json` + `--jq` for structured data
- **Docs location**: Project roadmaps go in `docs/roadmap/`, analysis in `docs/claudedocs/` or `docs/analysis/`
- **EnsureCreatedAsync vs MigrateAsync**: For Testcontainers isolated databases, use `EnsureCreatedAsync()` - it creates all tables from model without needing migration history
- **IHybridCacheService Mock**: Use concrete type in Moq setup, not `It.IsAnyType` - example: `Setup(c => c.GetOrCreateAsync<TokenLimitsCacheDto>(...)).Returns<string, Func<CancellationToken, Task<TokenLimitsCacheDto>>, ...>(async (key, factory, ...) => await factory(ct))`

## Project State (2026-02-07, 13:30)
- **Bug Status**: 1 bug critico aperto (#3782 Auth endpoint deserialization)
- Admin Enterprise Dashboard: **56% completato** (Epic 1: 5/9 task done)
  - #3685 Core: 5/9 sub chiuse (#3689 Layout ✅, #3690 Security ✅, #3691 Audit ✅, #3692 Token Mgmt ✅, #3693 Batch Jobs ✅)
  - #3686 User: 0/7 cb, 0/10 sub (blocked by E1)
  - #3687 AI: 0/9 cb, 0/11 sub (blocked by E1)
  - #3688 Business: 0/8 cb, 0/10 sub (blocked by E2+3)
  - **Remaining E1**: #3694 (Overview KPIs), #3695 (Resources), #3696 (Operations), #3697 (Testing)
- Epic completati: 8 epic chiusi (added #3692 Token Management)
- Epic incomplete: #3490 (11/29 done), #3453, #3413 (8/14 checkbox)
- 80 issue aperte
- **Testing Progress**: TokenTier + TokenTracking integration tests fixed (10/10 passing)

## Recently Completed (7 Feb 2026, 13:30) - INTEGRATION TESTS FIX
- **Critical Bug Fix**: GameLabelEntity.IsPredefined missing column name mapping
  - Added `.HasColumnName("is_predefined")` to GameLabelEntityConfiguration
  - Created migration `20260207125516_FixGameLabelIsPredefinedColumnName`
  - This was blocking ALL `EnsureCreatedAsync()` calls in tests
- **TokenTierRepositoryTests**: Fixed 5 integration tests
  - Switched from `MigrateAsync()` to `EnsureCreatedAsync()` (better for isolated test DBs)
  - All CRUD operations + soft delete tests passing
- **TokenTrackingServiceTests**: Fixed 5 integration tests
  - Fixed IHybridCacheService mock setup (was returning null)
  - Mock now properly invokes factory function for cache bypass
  - All token tracking + limit enforcement tests passing

## Previous Session (7 Feb 2026, 01:00)
- **Epic #3692**: Token Management System COMPLETATO
  - #3786: Backend Data Model (PR #3789 merged)
  - #3787: Backend API Endpoints (PR #3790 auto-merge)
  - #3788: Testing Coverage (35+ tests, 90%+ target)

## Known Issues
- **#3782 CRITICAL**: POST /api/v1/auth/* endpoints fail with JSON deserialization (aperto 2026-02-06 18:24)
- **Integration test performance**: Administration context integration tests timeout >2min (needs investigation)

## Patterns Learned
- `gh issue edit -F file.md` works better than `--body` for multiline content
- EF Core InMemory: always `SaveChangesAsync` before `AsNoTracking` queries
- EF Core tracking: check `ChangeTracker.Entries<T>()` before `Update()`
- FluentValidation: `NotEmpty()` on `Guid?` doesn't catch `Guid.Empty`
- `StrategyModelMapping.Default()` changed to DeepSeek/Anthropic — tests must match
- **EF Core Value Objects**: Use `OwnsOne()` for complex types (TierLimits, TierPricing)
- **JSON Collections**: Use `HasConversion` with JsonSerializer for List<T> → jsonb column
- **Handler Accessibility**: Handlers must be `internal` if using `internal` repositories (CS0051)
- **Sonar S927**: Parameter names must match interface (`cancellationToken` not `ct`)
- **IHybridCacheService**: Requires reference types - wrap tuples in record class for caching
- **Column Name Mapping**: ALWAYS add `.HasColumnName()` when using snake_case in filters/indexes
- **HasFilter vs Column Name**: EF Core filters use SQL column names, not property names!

## Git Workflow
- Main dev branch: `main-dev`, frontend: `frontend-dev`
- PR target: ALWAYS to parent branch, not main
- Track parent: `git config branch.<feature>.parent <parent>`

## Reference Files
- Backend roadmap template: `D:\Repositories\meepleai-monorepo-backend\docs\04-features\admin-dashboard-enterprise\ROADMAP.html`
