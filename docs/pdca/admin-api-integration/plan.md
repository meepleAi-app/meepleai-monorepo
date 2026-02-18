# Plan: Admin Dashboard API Integration

**Date**: 2026-02-18
**Epic**: Admin Dashboard API Integration (New)
**Related**: Epic #4625 (Admin Dashboard Implementation - Complete)

---

## Hypothesis

**Goal**: Replace mock data in Admin Dashboard components with real backend API endpoints

**Approach**:
1. Create backend endpoints in Administration bounded context (CQRS pattern)
2. Follow existing patterns from AgentDefinitions, UserLibrary bounded contexts
3. Use React Query hooks for data fetching (follow existing admin patterns)
4. Maintain mock data fallbacks for development/testing

**Why This Approach**:
- CQRS pattern is project standard (CLAUDE.md requirement)
- Administration BC already exists with user/audit endpoints
- React Query provides caching, loading states, error handling
- Preserves test isolation with mock data

---

## Expected Outcomes (Quantitative)

### Backend
- **Endpoints**: 9 new API endpoints
- **Implementation Time**: ~8-10 hours total
  - Administration BC: 2h (1 endpoint)
  - Agents analytics: 3h (4 endpoints)
  - SharedGameCatalog: 2h (2 endpoints)
  - KnowledgeBase: 2h (2 endpoints)
  - Testing: 1h
- **Test Coverage**: Maintain 90%+ backend coverage
- **Response Time**: <200ms per endpoint (target)

### Frontend
- **React Query Hooks**: 9 new hooks
- **Implementation Time**: ~4 hours
- **Error Handling**: Loading states, error boundaries
- **Test Updates**: Mock API responses in tests
- **Coverage**: Maintain 85%+ frontend coverage

### Total
- **Estimate**: 12-14 hours
- **Files**: ~25-30 files (backend DTOs, queries, handlers + frontend hooks)
- **Lines**: ~1,500-2,000 lines

---

## Risks & Mitigation

### Risk 1: CQRS Pattern Complexity
**Impact**: High (incorrect pattern = code review rejection)
**Mitigation**:
- Use Context7 for CQRS/MediatR patterns
- Copy existing patterns from UserLibrary BC
- Validate with backend-architect agent

### Risk 2: Database Query Performance
**Impact**: Medium (slow queries = poor UX)
**Mitigation**:
- Use EF Core AsNoTracking for read queries
- Add pagination for large result sets
- Test with realistic data volumes

### Risk 3: Frontend Breaking Changes
**Impact**: Medium (existing tests may break)
**Mitigation**:
- Keep mock data for tests
- Use MSW (Mock Service Worker) for API mocking
- Update tests incrementally

### Risk 4: Missing Data in Existing DB
**Impact**: Low (new endpoints may return empty results)
**Mitigation**:
- Seed database with realistic test data
- Handle empty state gracefully in UI
- Document data requirements

---

## Implementation Phases

### Phase 1: Backend - Administration BC (2h)
**Endpoint**: `GET /api/v1/admin/users/activity-log`

**Files to Create**:
```
BoundedContexts/Administration/Application/
├── DTOs/UserActivityLogDto.cs
├── Queries/GetUserActivityLogQuery.cs
├── Handlers/GetUserActivityLogQueryHandler.cs
└── Validators/GetUserActivityLogQueryValidator.cs
```

**Pattern**: Follow GetAuditLogQuery existing pattern

**Database**: Use existing AuditLog table or create UserActivity aggregate

---

### Phase 2: Backend - Agents Analytics (3h)
**Endpoints**:
- `GET /api/v1/admin/agents/analytics/kpis`
- `GET /api/v1/admin/agents/chat-history`
- `GET /api/v1/admin/agents/models`
- `GET /api/v1/admin/agents/prompts`

**Bounded Context**: Create new `AgentManagement` or extend `Administration`

**Data Sources**:
- KPIs: Aggregate from AgentSession, ChatHistory tables
- Chat History: Query ChatMessage, AgentSession tables
- Models: Configuration table or static config
- Prompts: PromptTemplate existing table

---

### Phase 3: Backend - SharedGameCatalog (2h)
**Endpoints**:
- `GET /api/v1/admin/shared-games?category={}&status={}&search={}`
- `GET /api/v1/admin/shared-games/categories`

**Bounded Context**: SharedGameCatalog (already exists)

**Queries**:
- Extend existing GetSharedGamesQuery with admin filters
- Create GetGameCategoriesQuery

---

### Phase 4: Backend - KnowledgeBase (2h)
**Endpoints**:
- `GET /api/v1/admin/kb/vector-collections`
- `GET /api/v1/admin/kb/processing-queue`

**Bounded Context**: KnowledgeBase or DocumentProcessing

**Data Sources**:
- Vector Collections: Query Qdrant metadata via IVectorStore
- Processing Queue: PdfDocument table with processing status

---

### Phase 5: Frontend - React Query Integration (4h)
**Hooks to Create**:
```typescript
// hooks/queries/admin/
├── useUserActivityLog.ts
├── useAgentAnalytics.ts
├── useChatHistory.ts
├── useAIModels.ts
├── useSystemPrompts.ts
├── useSharedGamesAdmin.ts
├── useGameCategories.ts
├── useVectorCollections.ts
└── useProcessingQueue.ts
```

**Pattern**: Follow existing `useAgentDefinitions`, `useUsers` patterns

**Component Updates**:
- Replace `MOCK_*` constants with `const { data, isLoading, error } = useHook()`
- Add loading skeletons (already implemented)
- Add error states
- Update tests to mock React Query

---

## Validation Criteria

### Backend
- [ ] All endpoints follow CQRS pattern (MediatR only)
- [ ] DTOs validated with FluentValidation
- [ ] Query handlers use AsNoTracking
- [ ] Test coverage ≥90%
- [ ] Response time <200ms (measured)
- [ ] Integration tests with Testcontainers

### Frontend
- [ ] All mock data replaced with API calls
- [ ] Loading states implemented
- [ ] Error boundaries handle API failures
- [ ] Tests mock API responses (MSW or manual mocks)
- [ ] Test coverage ≥85%
- [ ] No console errors

### Documentation
- [ ] API documented in Scalar (OpenAPI)
- [ ] React Query hooks documented
- [ ] Error handling patterns documented
- [ ] Database seeding documented

---

## Success Metrics

**Technical**:
- 9/9 endpoints implemented and tested
- 9/9 components using real data
- 0 regressions in existing functionality
- Build green, tests passing

**Performance**:
- API response time <200ms (p95)
- Frontend initial load <2s
- No N+1 query problems

**Quality**:
- Backend coverage ≥90%
- Frontend coverage ≥85%
- Code review score ≥80%
- Zero critical security issues

---

## Next Actions

1. **Create GitHub Epic & Issues**:
   ```bash
   gh issue create --title "Epic: Admin Dashboard API Integration" --body "[template]"
   gh issue create --title "Backend: Administration BC endpoints" --body "[spec]"
   # ... create all 5 issues
   ```

2. **Set Up PDCA Tracking**:
   - Create docs/pdca/admin-api-integration/ directory ✅
   - Initialize plan.md ✅
   - Prepare do.md template
   - Ready for execution phase

3. **Delegate Implementation** (when ready):
   - Phase 1-4: backend-architect agent
   - Phase 5: frontend-architect agent
   - Validation: quality-engineer agent

---

**Status**: Plan complete, ready for GitHub issue creation and execution.
