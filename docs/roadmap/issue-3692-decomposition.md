# Issue #3692 Decomposition - Token Management System

**Date**: 2026-02-06
**Status**: Issue reopened and split into 3 sub-issues

## Problem Analysis

Issue #3692 (Token Management System) was closed prematurely with:
- ✅ **Frontend**: 100% complete (6/6 components, UI tests)
- ❌ **Backend**: 0% implemented (data model, endpoints, integration tests)

The system is non-functional without the backend implementation.

## Solution: Decomposition into 3 Sub-Issues

### #3786 - Backend Data Model ⏳
**Duration**: 2 giorni | **Priority**: High | **Labels**: `kind/feature`, `area/admin`, `backend`

**Scope**:
- 4 Domain entities: `TokenTier`, `UserTokenUsage`, `TokenTierRepository`, Token Tracking Service
- 2 EF Core migrations with indexes
- Token tracking middleware (intercept LLM API calls)
- Auto-block logic at 100%, soft warning at 80%
- Monthly reset background job
- Redis caching for real-time usage tracking

**Deliverables**:
- Complete DDD-compliant data layer
- Repository pattern with CRUD operations
- HTTP middleware for automatic token tracking
- Unit tests: 90%+ coverage
- Integration tests: DB operations + tracking service

**Dependencies**: Depends on #3689 (Layout - completed) | Blocks #3787

---

### #3787 - Backend API Endpoints ⏳
**Duration**: 2 giorni | **Priority**: High | **Labels**: `kind/feature`, `area/admin`, `backend`

**Scope**:
- 5 RESTful endpoints following CQRS pattern:
  1. `GET /api/v1/admin/resources/tokens` - Balance + usage statistics
  2. `GET /api/v1/admin/resources/tokens/tiers` - Tier configurations
  3. `PUT /api/v1/admin/resources/tokens/tiers/{id}` - Update tier limits
  4. `POST /api/v1/admin/resources/tokens/add-credits` - Add € credits
  5. `GET /api/v1/admin/resources/tokens/consumption` - Graph data (7/30 days)

**Technical Requirements**:
- All endpoints use `IMediator.Send()` (CQRS pattern)
- FluentValidation for all commands/queries
- Admin role authorization on all endpoints
- Proper HTTP status codes (200, 201, 400, 401, 403, 404, 429)
- Audit trail for write operations
- Cache management (5-min TTL for tier configs)

**Deliverables**:
- 5 Query/Command handlers with validators
- Endpoint mappings in `AdminResourcesEndpoints.cs`
- Response DTOs: `TokenBalanceResponseDto`, `TokenTierDto`, `TokenConsumptionDataDto`
- Unit tests: 90%+ coverage
- Integration tests: All endpoints with Testcontainers

**Dependencies**: **MUST complete #3786 first** | Blocks #3788

---

### #3788 - Testing Coverage ⏳
**Duration**: 1 giorno | **Priority**: High | **Labels**: `kind/test`, `area/admin`, `backend`

**Scope**:
- **Integration Tests** (Testcontainers: PostgreSQL + Redis):
  - Token tracking service (5+ tests)
  - Repository operations (CRUD, performance, caching)
  - API endpoints (7+ tests per endpoint)
  - Authorization enforcement
  - Validation error handling

- **E2E Tests** (Playwright):
  - View token balance
  - Update tier limits
  - Add credits modal
  - Token usage alerts
  - Auto-block UI behavior

- **Performance Tests**:
  - 1000 concurrent requests (p95 < 200ms)
  - Monthly reset for 10K users (< 30s)

**Coverage Goals**:
| Component | Current | Target |
|-----------|---------|--------|
| Domain entities | 85% | 95% |
| Repository | 70% | 90% |
| Handlers | 80% | 95% |
| Endpoints | 0% | 90% |
| Tracking service | 0% | 95% |
| **Overall** | ~60% | **90%+** |

**Deliverables**:
- 25+ integration tests
- 5+ E2E tests
- Performance benchmarks validated
- All tests CI/CD compatible
- Zero flaky tests

**Dependencies**: **MUST complete #3786 + #3787 first** | Blocks #3692 final closure

---

## Implementation Order

```
[#3689 Layout] ✅ DONE
    ↓
[#3786 Data Model] ⏳ 2 days
    ↓
[#3787 API Endpoints] ⏳ 2 days
    ↓
[#3788 Testing] ⏳ 1 day
    ↓
[#3692 Complete] ✅ Final closure
```

**Total Duration**: 5 giorni (2+2+1)

## Technical Stack

### Backend
- **.NET 9**: ASP.NET Minimal APIs + MediatR (CQRS)
- **PostgreSQL 16**: Primary data store
- **Redis**: Real-time usage tracking cache
- **EF Core**: ORM with migrations
- **FluentValidation**: Input validation
- **xUnit + Testcontainers**: Integration testing

### Frontend (Already Complete)
- **Next.js 15**: App Router + React 19
- **Tailwind + shadcn/ui**: UI components
- **Zustand**: State management
- **React Query**: Server state
- **Vitest + Playwright**: Testing

## Success Criteria

✅ **Definition of Done**:
1. All 3 sub-issues (#3786, #3787, #3788) closed
2. Backend coverage: 90%+ (from current ~60%)
3. All acceptance criteria in #3692 met:
   - Users auto-blocked at 100% usage ✓
   - Warning at 80% usage ✓
   - 90%+ test coverage ✓
4. Frontend + Backend fully integrated
5. E2E tests pass in CI/CD
6. Documentation updated

## Links

- **Parent Epic**: #3685 (Admin Enterprise Dashboard)
- **Original Issue**: #3692 (Token Management System)
- **Sub-Issues**: #3786, #3787, #3788
- **Specification**: `docs/04-features/admin-dashboard-enterprise/SPECIFICATION.md#21-tokens-tab`
- **Mockup**: `docs/04-features/admin-dashboard-enterprise/mockups/02-resources.html`

## Lessons Learned

### 🔴 Problem: Premature Issue Closure
- Issue was closed with only frontend complete (50% of work)
- Backend (data model, endpoints, tests) was 0% implemented
- System is non-functional without backend

### ✅ Solution: Issue Decomposition
- Split large issue into 3 focused, sequential sub-issues
- Clear dependencies and blocking relationships
- Each sub-issue has specific deliverables and acceptance criteria

### 📝 Best Practices for Future
1. **Never close issues with incomplete components**
   - Frontend + Backend + Tests must ALL be complete
   - Verify acceptance criteria checklist before closing

2. **Use sub-issues for large epics**
   - Easier to track progress
   - Clear dependencies and ordering
   - Smaller, focused PRs

3. **Test coverage is mandatory**
   - Integration tests required before closure
   - E2E tests validate complete user flows
   - Performance benchmarks for high-load systems

4. **Document decomposition decisions**
   - Why was issue split?
   - What is the implementation order?
   - How do sub-issues relate to parent?

---

**Created**: 2026-02-06
**Last Updated**: 2026-02-06
