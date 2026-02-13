# Plan: Epic #4071 Completion - PDF Status Tracking

**Date**: 2026-02-13
**PM Agent**: Session Start
**Strategy**: Parallel Implementation + Quality Gates

---

## Hypothesis

### Goal Statement
Complete Epic #4071 (PDF Status Tracking) by implementing the remaining 2 issues:
- **#4219**: Duration Metrics & ETA Calculation (P2-Medium, 1.5d)
- **#4220**: Multi-Channel Notification System (P2-Medium, 1d)

### Why This Approach?
1. **Parallel Execution**: Both issues depend on #4216 (already closed), no cross-dependencies
2. **Backend-First Pattern**: Proven successful in #4215-#4218 completion
3. **Quality Focus**: Epic completion requires production-ready code (>90% coverage)
4. **Documentation Culture**: PDCA cycle ensures learnings are captured

---

## Expected Outcomes (Quantitative)

| Metric | Expected Value | Validation Method |
|--------|----------------|-------------------|
| **Test Coverage** | >90% backend | `dotnet test /p:CollectCoverage=true` |
| **Frontend Coverage** | >85% | `pnpm test:coverage` |
| **Implementation Time** | 2.5 days | Time tracking via task completion |
| **New API Endpoints** | 2 | `/api/v1/documents/{id}/metrics`, `/api/v1/notifications/preferences` |
| **Domain Events** | 3 handlers | PdfStateChanged, PdfFailed, PdfRetryInitiated |
| **Integration Tests** | 100% pass | CI pipeline validation |
| **E2E Tests** | 100% pass | Playwright full pipeline test |

---

## Technical Architecture

### Issue #4219 - Duration Metrics & ETA

**Domain Layer**:
```csharp
// PdfDocument.cs extensions
public DateTime? UploadingStartedAt { get; private set; }
public DateTime? ExtractingStartedAt { get; private set; }
public DateTime? ChunkingStartedAt { get; private set; }
public DateTime? EmbeddingStartedAt { get; private set; }
public DateTime? IndexingStartedAt { get; private set; }

public int ProgressPercentage => CalculateProgress();
public TimeSpan? EstimatedTimeRemaining { get; private set; }

public void UpdateETA()
{
    // MVP: Static calculation (2s/page × remaining states)
    // Future: ML-based predictor from historical data
}
```

**Application Layer**:
- Query: `GetPdfMetricsQuery` → `GetPdfMetricsQueryHandler`
- Response: `PdfMetricsDto` with timing data, progress %, ETA

**API Layer**:
- Endpoint: `GET /api/v1/documents/{id}/metrics`
- Auth: `RequireSession()`
- Response: 200 OK with metrics | 404 Not Found

**Frontend**:
- Hook: `usePdfMetrics(documentId)`
- Component: `<PdfProgressBar progress={%} eta={timespan} />`

### Issue #4220 - Multi-Channel Notifications

**Domain Layer** (UserNotifications BC):
```csharp
// NotificationPreferences.cs
public class NotificationPreferences : Entity<Guid>
{
    public Guid UserId { get; private set; }

    // Email preferences
    public bool EmailOnDocumentReady { get; private set; } = true;
    public bool EmailOnDocumentFailed { get; private set; } = true;

    // Push preferences
    public bool PushOnDocumentReady { get; private set; } = true;

    // In-app preferences
    public bool InAppOnDocumentReady { get; private set; } = true;
}
```

**Infrastructure Layer**:
```csharp
// EventHandlers/PdfNotificationEventHandler.cs
internal class PdfNotificationEventHandler :
    IDomainEventHandler<PdfStateChangedEvent>,
    IDomainEventHandler<PdfFailedEvent>,
    IDomainEventHandler<PdfRetryInitiatedEvent>
{
    // Dispatch notifications based on user preferences
}
```

**Email Template**: HTML with game name, document name, status badge, action link

**API Layer**:
- GET `/api/v1/notifications/preferences`
- PUT `/api/v1/notifications/preferences`

**Frontend**:
- Settings page: Toggle switches per channel+event type
- Notification Center: Display in-app notifications

---

## Task Breakdown & Dependencies

### Task Hierarchy
```
Epic #4071 Completion
├─ Task #1: Issue #4219 (Metrics) [PARALLEL]
│  ├─ Domain: Add timing fields
│  ├─ Application: Query + Handler
│  ├─ Infrastructure: EF Core config
│  ├─ API: Metrics endpoint
│  ├─ Frontend: Hook + Progress bar
│  └─ Tests: Unit + Integration + E2E
│
├─ Task #2: Issue #4220 (Notifications) [PARALLEL]
│  ├─ Domain: Preferences entity + Event handlers
│  ├─ Application: Commands + Queries
│  ├─ Infrastructure: Email template + Service integration
│  ├─ API: Preferences CRUD
│  ├─ Frontend: Settings UI
│  └─ Tests: Unit + Integration + E2E
│
├─ Task #3: Quality Validation [SEQUENTIAL - depends on #1, #2]
│  ├─ Coverage validation (>90% backend, >85% frontend)
│  ├─ Integration tests (metrics + notifications)
│  ├─ E2E test (full pipeline with notifications)
│  ├─ Security review (no sensitive data leaks)
│  ├─ Performance validation (metrics <200ms)
│  └─ Manual testing (real PDF upload)
│
└─ Task #4: Documentation [SEQUENTIAL - depends on #3]
   ├─ Close Epic #4071 on GitHub
   ├─ Update CLAUDE.md with patterns
   ├─ Create PDCA check.md + act.md
   ├─ Update API docs (Scalar)
   └─ Session memory cleanup
```

### Dependency Graph
```
#4219 (Metrics) ─────┐
                      ├──→ Task #3 (Quality) ──→ Task #4 (Documentation)
#4220 (Notifications) ┘
```

**Critical Path**: Task #1 OR Task #2 → Task #3 → Task #4 (2.5 days total)

---

## Risks & Mitigation Strategies

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **UserNotifications BC integration issues** | Medium | High | Review BC interfaces first, create integration plan, mock if needed |
| **ETA calculation inaccuracy (static MVP)** | High | Low | Document as known limitation, plan ML predictor for Phase 2 |
| **Email template rendering issues** | Medium | Medium | Test with Gmail, Outlook, Apple Mail; use email-tested HTML templates |
| **Performance degradation (metrics endpoint)** | Low | Medium | Cache metrics calculation, validate <200ms response time |
| **Test coverage gaps** | Low | High | Write tests alongside implementation (TDD), validate with coverage reports |
| **Frontend hydration errors** | Low | Medium | Use deterministic mock data, avoid Math.random() |

---

## Quality Gates (Must Pass Before Completion)

### Gate 1: Implementation Complete
- [ ] All acceptance criteria checked in #4219
- [ ] All acceptance criteria checked in #4220
- [ ] No compilation errors
- [ ] No linting errors

### Gate 2: Testing Complete
- [ ] Backend test coverage >90%
- [ ] Frontend test coverage >85%
- [ ] All unit tests pass
- [ ] All integration tests pass
- [ ] E2E pipeline test passes

### Gate 3: Security & Performance
- [ ] Security review: No sensitive data in responses
- [ ] Performance: Metrics endpoint <200ms
- [ ] Notification dispatch async (no blocking)
- [ ] WCAG 2.1 AA compliance (frontend)

### Gate 4: Documentation Complete
- [ ] Epic #4071 closed on GitHub
- [ ] CLAUDE.md updated with new patterns
- [ ] PDCA cycle complete (check.md + act.md)
- [ ] API documentation updated

---

## Implementation Strategy

### Parallel Execution (Tasks #1 + #2)
**Why**: Both issues are independent (no shared code/dependencies)

**Approach**:
1. Use `/implementa` for each issue in separate sessions/terminals
2. Task #1 focuses on backend metrics calculation + frontend display
3. Task #2 focuses on notification preferences + event handlers
4. Merge PRs sequentially after individual completion

### Sequential Validation (Tasks #3 + #4)
**Why**: Quality validation requires both implementations complete

**Approach**:
1. Wait for both PRs merged to parent branch
2. Run comprehensive test suite on integrated code
3. Manual testing with real PDF upload
4. Document learnings and close epic

---

## Tools & Resources

### Development Tools
- **MCP Servers**: Serena (memory), Context7 (docs), Sequential (reasoning)
- **Testing**: xUnit, Testcontainers, Playwright
- **Coverage**: Coverlet, Vitest
- **API Docs**: Scalar

### Implementation Commands
```bash
# Issue #4219
/implementa --issue 4219 --strategy backend-first

# Issue #4220
/implementa --issue 4220 --strategy backend-first

# Quality Validation
dotnet test --filter "Category=Unit|Integration" /p:CollectCoverage=true
pnpm test && pnpm test:coverage
pnpm test:e2e

# Documentation
gh issue edit 4071 --body-file docs/pdca/epic-4071-completion/act.md
```

---

## Success Criteria (Definition of Done)

### Epic #4071 Complete When:
1. ✅ All 6 sub-issues closed (#4215-#4220)
2. ✅ Test coverage targets met (>90% backend, >85% frontend)
3. ✅ All quality gates passed
4. ✅ Documentation complete (PDCA cycle, CLAUDE.md)
5. ✅ Epic #4071 closed on GitHub with summary
6. ✅ Session memory archived with learnings

---

## Next Steps (Execution)

1. **NOW**: Start Task #1 and Task #2 in parallel
   ```bash
   /implementa --issue 4219 --strategy backend-first
   /implementa --issue 4220 --strategy backend-first
   ```

2. **After #1 + #2**: Run Task #3 (Quality Validation)
3. **After #3**: Run Task #4 (Documentation)
4. **Final**: Close Epic #4071, archive session

**Estimated Completion**: 2026-02-15 EOD (2.5 days from now)
