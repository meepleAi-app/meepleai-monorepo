# 🔄 SPRINT Issues - DDD Integration Guide

**Document Version**: 1.0.0
**Last Updated**: 2025-11-11
**Purpose**: Update existing SPRINT issues with DDD refactoring approach

---

## 📋 Executive Summary

**Total SPRINT Issues**: 25 issues (#846-870)
**DDD Integration Status**:
- ✅ SPRINT-1 (5 issues): Updated with Authentication DDD patterns
- ⚠️ SPRINT-2 (5 issues): Requires GameManagement context (#923 prerequisite)
- ⚠️ SPRINT-3 (5 issues): Requires KnowledgeBase extension (#924 prerequisite)
- ⚠️ SPRINT-4 (5 issues): Requires GameManagement context (#923 prerequisite)
- ⚠️ SPRINT-5 (5 issues): Requires AI Agents architecture decision (#925 prerequisite)

**Prerequisite Issues Created**:
- #923: Create GameManagement Bounded Context (8-10h)
- #924: Extend KnowledgeBase with ChatThread (4-6h)
- #925: Define AI Agents Bounded Context (2h decision) - TO BE CREATED

---

## ✅ SPRINT-1: Authentication (Updated)

**DDD Context**: Authentication (Phase 2, 70% complete)
**Status**: All 5 issues updated with DDD patterns
**Ready to Implement**: YES (Authentication context available)

### Updated Issues

#### #846: OAuth Integration Complete ✅
**DDD Approach**:
- Domain: User aggregate with LinkOAuthAccount(), UnlinkOAuthAccount()
- Application: CQRS commands (LinkOAuthAccountCommand, UnlinkOAuthAccountCommand)
- Infrastructure: OAuthService refactored to use application layer
- Effort: 6h → 8h (+2h domain modeling)

#### #847: 2FA/TOTP Management UI ✅
**DDD Approach**:
- Domain: TotpSecret + BackupCode value objects
- User aggregate: Enable2FA(), Verify2FA() methods
- Application: Enable2FACommand, Verify2FAQuery
- Effort: 8h → 10h (+2h domain modeling)

#### #848: Settings Pages - 4 Tabs Implementation ✅
**DDD Integration**:
- UI consumes Application layer via CQRS
- Uses: GetUserProfileQuery, UpdateUserProfileCommand, Enable2FACommand
- No direct domain access from UI
- Effort: 10h (no change, UI layer)

#### #849: User Profile Management Service ✅
**DDD Approach**:
- Application service with CQRS
- Commands: UpdateUserProfileCommand, ChangePasswordCommand
- Queries: GetUserProfileQuery
- Domain: User aggregate methods
- Effort: 6h → 8h (+2h CQRS handlers)

#### #850: Unit Test Suite - Authentication Module ✅
**DDD Test Strategy**:
- Domain tests: User aggregate + value objects (95%+ coverage)
- Application tests: Command/Query handlers (90%+)
- Infrastructure tests: Repositories, OAuth (85%+)
- Effort: 12h → 15h (+3h domain tests)

**SPRINT-1 Total Effort**: 42h → 51h (+9h for DDD, +21%)

---

## ⚠️ SPRINT-2: GameManagement (Blocked)

**DDD Context**: GameManagement (NOT CREATED YET)
**Prerequisite**: Issue #923 (Create GameManagement Bounded Context, 8-10h)
**Status**: Issues updated with prerequisite dependency
**Ready to Implement**: NO (requires #923 first)

### Issues Requiring GameManagement Context

#### #851: Game Entity & Database Schema 🔒
**Blocked By**: #923

**DDD Approach** (After #923 complete):
- Domain: Game aggregate root
  - Properties: Name, MinPlayers, MaxPlayers, EstimatedDuration
  - Methods: AddRuleSpec(), ValidatePlayerCount(), UpdateDetails()
- Value Objects: GameId, PlayerCount, Duration
- Repository: IGameRepository interface

**Tasks Updated**:
- [ ] ⚠️ PREREQUISITE: Complete #923 (GameManagement context)
- [ ] Implement Game aggregate in GameManagement.Domain
- [ ] Add domain validation methods
- [ ] Create IGameRepository interface
- [ ] Implement repository in Infrastructure layer

**Effort**: 6h → 8h (DDD aggregate modeling)

**Alternative (Pragmatic)**:
Can implement with legacy Entity Framework approach, migrate to DDD later.
- Effort: 6h (no change)
- Trade-off: Technical debt, future migration cost

---

#### #852: GameService CRUD Implementation 🔒
**Blocked By**: #923

**DDD Approach** (After #923 complete):
- Application: CreateGameCommand, UpdateGameCommand, DeleteGameCommand
- Queries: GetGameQuery, SearchGamesQuery, GetGameDetailsQuery
- Handlers: CreateGameCommandHandler, etc.
- DTOs: GameDto, GameDetailsDto, SearchGamesResultDto

**Tasks Updated**:
- [ ] ⚠️ PREREQUISITE: Complete #923
- [ ] CreateGameCommand + Handler
- [ ] UpdateGameCommand + Handler
- [ ] DeleteGameCommand + Handler
- [ ] GetGameQuery + Handler
- [ ] SearchGamesQuery + Handler (with filters)

**Effort**: 8h → 12h (CQRS + handlers)

---

#### #855: Game Detail Page - 4 Tabs
**Not Blocked** (UI layer, consumes Application layer)

**DDD Integration**:
- Consumes GameManagement.Application queries
- Uses: GetGameDetailsQuery, GetRuleSpecsQuery
- No domain layer access from UI

**Effort**: 12h (no change)

---

## ⚠️ SPRINT-3: KnowledgeBase/Chat (Partially Blocked)

**DDD Context**: KnowledgeBase (Phase 3, 75% complete) + Extension needed
**Prerequisite**: Issue #924 (Extend KnowledgeBase with ChatThread, 4-6h)
**Status**: Partial - some issues can proceed, others blocked

### Issues Status

#### #856: Chat Thread Management 🔒
**Blocked By**: #924

**DDD Approach** (After #924 complete):
- Domain: ChatThread aggregate
  - Methods: AddMessage(), CloseThread(), ReopenThread()
- Value Objects: ThreadId, ThreadStatus
- Repository: IChatThreadRepository

**Effort**: 6h → 8h (DDD aggregate)

---

#### #857: Game-Specific Chat Context 🔒
**Blocked By**: #924

**DDD Approach**:
- KnowledgeBase.Domain.Services.ChatContextService
- Domain logic: Filter RAG results by game context
- Uses existing VectorSearch + QualityTracking services

**Effort**: 8h → 10h (domain service integration)

---

#### #859: PDF Citation Display Enhancement ✅
**Not Blocked** (Citation value object already exists!)

**DDD Integration**:
- Use existing KnowledgeBase.Domain.ValueObjects.Citation
- No changes needed to domain, just UI enhancement

**Effort**: 4h (no change)

---

#### #858: Chat UI with Thread Sidebar
**Partially Blocked** (UI can proceed, full features need #924)

**DDD Integration**:
- UI consumes ChatThread queries when available
- Can implement basic UI now, full integration after #924

**Effort**: 10h (no change, gradual enhancement)

---

#### #860: Chat Export Functionality
**Depends On**: #924 (ChatThread aggregate)

**DDD Approach**:
- Application: ExportChatCommand
- Domain: ChatThread.Export() method
- Infrastructure: Export service (PDF/CSV generation)

**Effort**: 6h → 8h (DDD command handler)

---

## ⚠️ SPRINT-4: Game Sessions (Blocked)

**DDD Context**: GameManagement (NOT CREATED YET)
**Prerequisite**: Issue #923 (Create GameManagement Bounded Context)
**Status**: All blocked by #923

### Issues Requiring GameManagement Context

#### #861: Game Session Entity & Database 🔒
**Blocked By**: #923

**DDD Approach**:
- Domain: GameSession aggregate
  - Methods: Start(), End(), AddPlayer(), RemovePlayer(), Pause()
  - Invariants: Min/max players, valid state transitions
- Value Objects: SessionId, SessionStatus, PlayerRole
- Repository: IGameSessionRepository

**Effort**: 6h → 8h (DDD aggregate)

---

#### #862: GameSessionService Implementation 🔒
**Blocked By**: #923

**DDD Approach**:
- Application: StartSessionCommand, EndSessionCommand, AddPlayerCommand
- Queries: GetSessionQuery, GetActiveSessionsQuery, GetSessionHistoryQuery
- Handlers: Full CQRS pattern
- DTOs: GameSessionDto, SessionPlayerDto, SessionHistoryDto

**Effort**: 8h → 12h (CQRS handlers)

---

#### #863: Session Setup Modal & UI
**Not Blocked** (UI layer)

**DDD Integration**:
- Consumes StartSessionCommand
- Uses GetGameQuery for game details

**Effort**: 8h (no change)

---

#### #864: Active Session Management UI
**Not Blocked** (UI layer)

**DDD Integration**:
- Consumes GetActiveSessionsQuery
- Uses EndSessionCommand, AddPlayerCommand

**Effort**: 10h (no change)

---

#### #865: Session History & Statistics 🔒
**Blocked By**: #923

**DDD Approach**:
- Application: GetSessionHistoryQuery with filters
- Query Handler: Complex aggregation logic
- DTOs: SessionHistoryDto, SessionStatsDto

**Effort**: 6h → 8h (query handler complexity)

---

## ⚠️ SPRINT-5: AI Agents (Architecture Decision Required)

**DDD Context**: UNDEFINED (requires architectural decision)
**Prerequisite**: Architectural Decision Record - Where do AI Agents fit?
**Status**: Blocked by architecture decision

### Architecture Decision Required

**Question**: Where do AI Agents belong in DDD model?

**Option A: New AgentManagement Bounded Context**
- Pro: Clear separation of concerns
- Pro: AI agents as first-class domain concept
- Con: Additional context to maintain
- Effort: 10-12h context creation

**Option B: Extend KnowledgeBase Context**
- Pro: Agents use RAG/LLM services (already in KnowledgeBase)
- Pro: Reuse existing infrastructure
- Con: KnowledgeBase becomes larger
- Effort: 4-6h extension

**Option C: Extend GameManagement Context**
- Pro: Agents arbitrate games (domain connection clear)
- Pro: Session-Agent relationship natural
- Con: Mixing game rules with AI agents
- Effort: 4-6h extension

**RECOMMENDATION**: **Option B (Extend KnowledgeBase)**
- Rationale: Agents primarily use RAG/LLM (KnowledgeBase responsibility)
- Game Master agent uses VectorSearch + QualityTracking (already in KnowledgeBase)
- Simpler than new context
- Aligns with "Knowledge" concept (agents have domain knowledge)

### Issues Status

#### #866: AI Agents Entity & Configuration
**Requires**: Architecture decision + bounded context

**DDD Approach** (If Option B):
- Domain: Agent aggregate in KnowledgeBase
  - Methods: Configure(), SetPrompt(), ValidateConfiguration()
- Value Objects: AgentType, AgentConfiguration
- Repository: IAgentRepository

**Effort**: 6h → 8h (after decision)

---

#### #867: Game Master Agent Integration
**Requires**: #866 + Architecture decision

**DDD Approach**:
- Application: InvokeAgentCommand
- Domain: Agent.Invoke(context) method
- Uses: VectorSearchDomainService, QualityTrackingDomainService

**Effort**: 10h → 12h (DDD integration)

---

#### #868: Agent Selection UI
**Not Blocked** (UI layer)

**DDD Integration**:
- Consumes GetAvailableAgentsQuery
- Uses InvokeAgentCommand

**Effort**: 8h (no change)

---

#### #869: Move Validation (RuleSpec v2 Integration) 🔒
**Blocked By**: #923 (GameManagement context)

**DDD Approach**:
- Domain: MoveValidationDomainService in GameManagement
- Uses: RuleSpec + GameSession aggregate
- Application: ValidateMoveCommand

**Effort**: 12h → 15h (complex domain logic)

---

#### #870: Integration Test Suite - Full Stack
**Depends On**: All previous SPRINT issues

**DDD Test Strategy**:
- Cross-context integration tests
- Test communication between bounded contexts
- CQRS end-to-end flows

**Effort**: 16h → 20h (DDD cross-context testing)

---

## 📊 Updated Effort Summary

### Original SPRINT Effort (Pre-DDD)

| SPRINT | Issues | Original Effort |
|--------|--------|-----------------|
| SPRINT-1 | 5 | 42h |
| SPRINT-2 | 5 | 40h |
| SPRINT-3 | 5 | 38h |
| SPRINT-4 | 5 | 46h |
| SPRINT-5 | 5 | 52h |
| **TOTAL** | **25** | **218h** |

### Updated Effort with DDD

| SPRINT | Issues | DDD Effort | Delta | Prerequisite |
|--------|--------|------------|-------|--------------|
| SPRINT-1 | 5 | 51h | +9h (+21%) | None (ready) |
| SPRINT-2 | 5 | 48h | +8h (+20%) | #923 (8-10h) |
| SPRINT-3 | 5 | 44h | +6h (+16%) | #924 (4-6h) |
| SPRINT-4 | 5 | 54h | +8h (+17%) | #923 (shared) |
| SPRINT-5 | 5 | 59h | +7h (+13%) | Decision (2h) |
| **TOTAL** | **25** | **256h** | **+38h (+17%)** | **+24-28h** |

**Grand Total with Prerequisites**: 280-284h (vs 218h original, +29%)

---

## 🎯 Implementation Roadmap with DDD

### Phase 0: DDD Prerequisites (2-3 weeks)

**Week 0**: Complete DDD Phase 3 (1 day, 8h)
- Fix KnowledgeBase infrastructure mapping
- Merge to main branch
- **Deliverable**: DDD foundation ready

**Week 1**: Create GameManagement Context (8-10h)
- Issue #923 implementation
- Domain + Application + Infrastructure
- **Deliverable**: GameManagement bounded context
- **Unblocks**: SPRINT-2, SPRINT-4 (10 issues)

**Week 2**: Extend KnowledgeBase + AI Decision (6-8h)
- Issue #924: ChatThread in KnowledgeBase (4-6h)
- AI Agents architecture decision + ADR (2h)
- **Deliverable**: KnowledgeBase extended, AI architecture defined
- **Unblocks**: SPRINT-3, SPRINT-5 (10 issues)

### Phase 1: SPRINT-1 Implementation (1-2 weeks, 51h)

**Ready to Start**: YES (Authentication context available)

**Week 3-4**: Implement SPRINT-1
- #846: OAuth Integration (8h)
- #847: 2FA Management (10h)
- #848: Settings Pages (10h)
- #849: User Profile Service (8h)
- #850: Unit Tests (15h)

**Deliverable**: Authentication features complete with DDD

### Phase 2: SPRINT-2 Implementation (1-2 weeks, 48h)

**Requires**: GameManagement context (#923) complete

**Week 5-6**: Implement SPRINT-2
- #851: Game Entity (8h)
- #852: GameService CRUD (12h)
- #853: PDF Upload Pipeline (8h)
- #854: Game Search UI (8h)
- #855: Game Detail Page (12h)

**Deliverable**: Game management features with DDD

### Phase 3: SPRINT-3 + SPRINT-4 (2-3 weeks, 98h)

**Requires**: KnowledgeBase extension (#924) + GameManagement (#923)

**Week 7-9**: Implement SPRINT-3 + SPRINT-4
- SPRINT-3 Chat features (44h)
- SPRINT-4 Session features (54h)

**Deliverable**: Chat + Session management with DDD

### Phase 4: SPRINT-5 (1-2 weeks, 59h)

**Requires**: AI Agents architecture decision

**Week 10-11**: Implement SPRINT-5
- #866-870: AI Agents features (59h)

**Deliverable**: AI Agents integration complete

**TOTAL TIMELINE**: 11-12 weeks (vs original ~6-7 weeks, +70% time)

---

## 🔀 Alternative: Pragmatic Hybrid Approach

### Strategy
- SPRINT-1: Full DDD (Authentication context ready)
- SPRINT-2/3/4/5: Hybrid implementation (legacy + DDD foundation where available)
- Post-Alpha: Migrate to full DDD incrementally

### Timeline
- Week 0: DDD Phase 3 complete (1 day)
- Week 1-12: Implement all SPRINTs with hybrid approach (original timeline)
- Post-Alpha: Create missing contexts + migrate (30-40h)

### Effort Comparison

| Approach | Upfront Effort | Implementation | Post-Alpha | Total |
|----------|----------------|----------------|------------|-------|
| **Full DDD First** | 24-28h (contexts) | 256h | 0h | 280-284h |
| **Pragmatic Hybrid** | 8h (Phase 3) | 218h | 30-40h | 256-266h |

**Savings**: 14-28h with pragmatic approach
**Trade-off**: Technical debt, future migration needed

---

## 📝 Issue Update Template

### For Blocked Issues (SPRINT-2/4/5)

Add this section to each blocked issue:

```markdown
## ⚠️ DDD Prerequisite Required

**BLOCKED BY**: #<prerequisite-issue-number>

This issue requires a bounded context to be created first following DDD patterns.

### DDD Implementation Approach
[Describe domain entities, value objects, services needed]

### Alternative (Pragmatic Hybrid)
Can be implemented with legacy approach, then migrated to DDD post-alpha.

**Recommendation**: Wait for prerequisite OR use pragmatic hybrid approach.

**See**: claudedocs/sprint_issues_ddd_integration_guide.md for full strategy.
```

### For Ready Issues (SPRINT-1)

Add this section:

```markdown
## ✅ DDD Integration (Authentication Context)

This issue uses the Authentication bounded context from DDD Phase 2.

### Domain Layer
[Aggregates, value objects used]

### Application Layer (CQRS)
[Commands, Queries, Handlers]

### Implementation Notes
[Specific DDD patterns to follow]

**See**: claudedocs/sprint_issues_ddd_integration_guide.md for patterns.
```

---

## 🚨 Critical Dependencies Graph

```
DDD Phase 3 Complete (1 day)
    │
    ├─→ SPRINT-1 Ready ✅ (Authentication context 70%)
    │   └─→ #846, #847, #848, #849, #850
    │
    ├─→ #923: GameManagement Context (8-10h)
    │   ├─→ SPRINT-2 Ready (#851, #852, #855)
    │   └─→ SPRINT-4 Ready (#861, #862, #863, #864, #865)
    │
    ├─→ #924: KnowledgeBase ChatThread (4-6h)
    │   └─→ SPRINT-3 Ready (#856, #857, #858, #859, #860)
    │
    └─→ AI Agents Decision (2h)
        └─→ SPRINT-5 Ready (#866, #867, #868, #869, #870)
```

**Critical Path**:
- DDD Phase 3 → #923 (GameManagement) → SPRINT-2/4
- DDD Phase 3 → #924 (ChatThread) → SPRINT-3
- SPRINT-1 can start immediately (no prerequisites)

---

## 🎯 Recommended Action Plan

### Immediate (This Week)

1. **Complete DDD Phase 3** (1 day, 8h)
   - Fix KnowledgeBase pragmatic mapping
   - Merge to main
   - Status: #923, #924 can start

2. **Update All SPRINT Issues** (1 day, 4-6h)
   - SPRINT-1: Add DDD implementation sections (already done: #846, #847, #848, #849, #850)
   - SPRINT-2/4: Add "Blocked By #923" notice
   - SPRINT-3: Add "Blocked By #924" notice (partial)
   - SPRINT-5: Add "Requires AI decision" notice

3. **Create Missing Prerequisite** (30 min)
   - Issue #925: AI Agents architecture decision
   - Link to SPRINT-5 issues

### Next 2 Weeks

**Decision Point**: Choose implementation strategy

**OPTION A: Full DDD Path**
- Week 1: #923 GameManagement context (8-10h)
- Week 2: #924 KnowledgeBase extension + AI decision (6-8h)
- Week 3+: Implement SPRINTs with full DDD (256h over 10-12 weeks)

**OPTION B: Pragmatic Hybrid** ⭐ RECOMMENDED
- Week 1+: Implement SPRINT-1 with DDD (51h, 1-2 weeks)
- Week 3+: Implement SPRINT-2/3/4/5 with hybrid (167h, 5-6 weeks)
- Post-Alpha: Create missing contexts + migrate (30-40h, 1-2 weeks)

**Savings**: ~4-6 weeks faster to alpha with Option B

---

## 📚 Documentation Updates Needed

### Create ADRs (Architecture Decision Records)

1. **ADR-001**: Pragmatic Hybrid DDD Approach for Alpha
   - Decision: Hybrid implementation acceptable in alpha phase
   - Rationale: Speed to market vs architectural purity
   - Consequences: Technical debt, future migration cost

2. **ADR-002**: AI Agents Bounded Context Location
   - Decision: Extend KnowledgeBase OR new AgentManagement context
   - Rationale: [TBD based on decision]
   - Consequences: [TBD]

3. **ADR-003**: ChatThread in KnowledgeBase Extension
   - Decision: ChatThread as part of KnowledgeBase context
   - Rationale: Chat uses RAG/search (KnowledgeBase responsibility)
   - Consequences: KnowledgeBase scope expansion

### Update Existing Docs

- [ ] `docs/architecture/ddd-bounded-contexts.md` - Add GameManagement, ChatThread, AI Agents
- [ ] `ddd_refactoring_status.md` - Update with SPRINT integration status
- [ ] `project_status.md` - Note DDD + SPRINT alignment strategy

---

## ✅ Success Criteria

### Issue Updates Complete When:
- [ ] All 25 SPRINT issues have DDD integration sections
- [ ] Blocked issues clearly marked with prerequisite (#923, #924)
- [ ] Ready issues (SPRINT-1) have implementation patterns
- [ ] Effort estimates updated (+17% average for DDD)
- [ ] 3 prerequisite issues created (#923, #924, #925)

### Implementation Ready When:
- [ ] DDD Phase 3 complete (1 day)
- [ ] SPRINT-1 issues assignable (Authentication DDD patterns clear)
- [ ] Team understands pragmatic vs full DDD approach
- [ ] Decision made: Full DDD OR Pragmatic Hybrid

---

## 🔍 Issue Update Log

**SPRINT-1 (Updated ✅)**:
- #846: OAuth Integration - Added Authentication DDD patterns
- #847: 2FA Management - Added domain VOs (TotpSecret, BackupCode)
- #848: Settings Pages - Added CQRS consumption notes
- #849: User Profile - Added CQRS commands/queries
- #850: Unit Tests - Added domain test strategy

**SPRINT-2 (Updated ⚠️)**:
- #851: Game Entity - Added prerequisite #923, DDD aggregate approach
- #852: GameService - Added prerequisite #923, CQRS pattern
- #855: Game Detail - No block, consumes Application layer

**SPRINT-3 (Partial ⚠️)**:
- #856: Chat Thread - Added prerequisite #924
- #857: Game-Specific Chat - Added prerequisite #924
- #859: PDF Citation - Can use existing Citation VO
- #858: Chat UI - Can start, full features after #924
- #860: Chat Export - Added prerequisite #924

**SPRINT-4 (Updated ⚠️)**:
- #861-865: All depend on #923 GameManagement context

**SPRINT-5 (Pending ⏳)**:
- Requires AI Agents architecture decision first
- Create #925 prerequisite issue

---

## 🏁 Next Steps

1. **Review This Guide** with team
2. **Choose Strategy**: Full DDD OR Pragmatic Hybrid
3. **Create Issue #925**: AI Agents architecture decision
4. **Update Remaining Issues**: SPRINT-3, SPRINT-4, SPRINT-5 with prerequisite notes
5. **Complete DDD Phase 3**: 1 day effort
6. **Start SPRINT-1**: First DDD-compliant sprint!

**Estimated Time to Update All Issues**: 4-6h remaining (SPRINT-3/4/5 updates + #925 creation)

**Total Prep Time**: 1 day (Phase 3) + 4-6h (issue updates) = **1.5 days before SPRINT-1 start**
