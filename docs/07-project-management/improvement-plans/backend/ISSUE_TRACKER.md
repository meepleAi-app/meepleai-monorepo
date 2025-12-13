# API Improvements - Issue Tracker

**Created**: 2025-11-15
**Last Updated**: 2025-12-13T10:59:23.970Z
**Total Issues**: 12 (Issues #1183-#1194)
**Estimated Effort**: 78-110 hours

---

## 📊 Progress Overview

| Phase | Total | Completed | In Progress | Not Started |
|-------|-------|-----------|-------------|-------------|
| Phase 1 (P0) | 1 | 0 | 0 | 1 |
| Phase 2 (P1) | 4 | 0 | 0 | 4 |
| Phase 3 (P2) | 4 | 0 | 0 | 4 |
| Phase 4 (P3) | 3 | 0 | 0 | 3 |
| **TOTAL** | **12** | **0** | **0** | **12** |

---

## 🔴 PHASE 1 - CRITICAL (Blocca Produzione)

### ✅ Issue #1: [P0] Fix Deadlock Risk in RateLimitService

**Status**: ⬜ Not Started
**Priority**: 🔴 P0 - Critical
**Assignee**: _[TBD]_
**Estimated**: 2-3 hours
**Actual**: _[TBD]_

**Description**:
Critical deadlock risk in `RateLimitService.GetConfigForRole()` due to blocking async calls using `.Result`.

**Location**:
- File: `apps/api/src/Api/Services/RateLimitService.cs`
- Lines: 160-161

**Tasks**:
- [ ] Make `GetConfigForRole()` async
- [ ] Update all callers (AuthEndpoints.cs:347, 527-528, 579-580)
- [ ] Add unit tests for async behavior
- [ ] Load test with 100+ concurrent requests
- [ ] Verify no deadlocks
- [ ] Deploy hotfix

**GitHub Issue**: #1183
**PR**: #_____
**Started**: _____
**Completed**: _____

**Notes**:
```
[Add notes here]
```

---

## 🟠 PHASE 2 - HIGH PRIORITY (Architectural Improvements)

### ✅ Issue #2: [DDD] Migrate ChatService to CQRS Pattern

**Status**: ⬜ Not Started
**Priority**: 🟠 P1 - High
**Assignee**: _[TBD]_
**Estimated**: 12-16 hours
**Actual**: _[TBD]_

**Description**:
Migrate `ChatService` (431 lines) to DDD/CQRS pattern. Complete KnowledgeBase context (95% → 100%).

**Files**:
- `apps/api/src/Api/Services/ChatService.cs` (TO BE REMOVED)
- `apps/api/src/Api/Routing/ChatEndpoints.cs`
- `apps/api/src/Api/Routing/AiEndpoints.cs`

**Tasks**:
- [ ] Create Commands: CreateChat, AddMessage, UpdateMessage, DeleteChat, DeleteMessage
- [ ] Create Queries: GetChatById, GetUserChats, GetUserChatsByGame, GetChatHistory
- [ ] Create Handlers for all commands/queries
- [ ] Create ChatMessageInvalidationDomainService
- [ ] Update ChatEndpoints.cs to use IMediator
- [ ] Update AiEndpoints.cs to use IMediator
- [ ] Run tests (maintain 90%+ coverage)
- [ ] Remove ChatService.cs
- [ ] Remove service registration

**GitHub Issue**: #1184
**PR**: #_____
**Started**: _____
**Completed**: _____

**Notes**:
```
[Add notes here]
```

---

### ✅ Issue #3: [DDD] Migrate RuleSpecService to CQRS Pattern

**Status**: ⬜ Not Started
**Priority**: 🟠 P1 - High
**Assignee**: _[TBD]_
**Estimated**: 10-14 hours
**Actual**: _[TBD]_

**Description**:
Migrate `RuleSpecService` (575+ lines) to DDD/CQRS pattern for GameManagement context.

**Files**:
- `apps/api/src/Api/Services/RuleSpecService.cs` (TO BE REMOVED)
- `apps/api/src/Api/Routing/RuleSpecEndpoints.cs`
- `apps/api/src/Api/Routing/PdfEndpoints.cs`

**Tasks**:
- [ ] Create Commands: UpdateRuleSpec, GenerateFromPdf, CreateVersion, ExportRuleSpecs
- [ ] Create Queries: GetRuleSpec, GetVersion, GetVersionHistory, GetVersionTimeline
- [ ] Create Domain Services: RuleSpecVersioning, RuleAtomParsing
- [ ] Create bounded context structure
- [ ] Update RuleSpecEndpoints.cs
- [ ] Update PdfEndpoints.cs
- [ ] Run tests
- [ ] Remove RuleSpecService.cs

**GitHub Issue**: #1185
**PR**: #_____
**Started**: _____
**Completed**: _____

**Notes**:
```
[Add notes here]
```

---

### ✅ Issue #4: [DDD] Implement Streaming Query Handlers for RAG/QA

**Status**: ⬜ Not Started
**Priority**: 🟠 P1 - High
**Assignee**: _[TBD]_
**Estimated**: 8-12 hours
**Actual**: _[TBD]_

**Description**:
Migrate streaming RAG/QA services to CQRS pattern using `IAsyncEnumerable<T>` handlers.

**Files**:
- `apps/api/src/Api/Services/StreamingRagService.cs` (TO BE REMOVED)
- `apps/api/src/Api/Services/StreamingQaService.cs` (TO BE REMOVED)
- `apps/api/src/Api/Services/SetupGuideService.cs` (TO BE REMOVED)
- `apps/api/src/Api/Routing/AiEndpoints.cs`

**Tasks**:
- [ ] Create StreamExplainQuery + Handler (IStreamRequestHandler)
- [ ] Create StreamQaQuery + Handler (IStreamRequestHandler)
- [ ] Create GenerateSetupGuideQuery + Handler
- [ ] Update AiEndpoints.cs to use IMediator.CreateStream()
- [ ] Test SSE (Server-Sent Events) streaming
- [ ] Load test streaming endpoints
- [ ] Remove services

**GitHub Issue**: #1186
**PR**: #_____
**Started**: _____
**Completed**: _____

**Notes**:
```
[Add notes here]
```

---

### ✅ Issue #5: [Config] Replace Hardcoded Configuration Values

**Status**: ⬜ Not Started
**Priority**: 🟠 P1 - High
**Assignee**: _[TBD]_
**Estimated**: 4-6 hours
**Actual**: _[TBD]_

**Description**:
Replace hardcoded configuration values with dynamic configuration using `IConfigurationService`.

**Locations**:
- `AuthEndpoints.cs:149` - Session expiration (30 days)
- `AuthEndpoints.cs:527-528, 579-580` - OAuth rate limits
- `AiEndpoints.cs` - AI agent timeouts
- `PdfEndpoints.cs` - Max file size

**Tasks**:
- [ ] Add DB configuration entries
- [ ] Update AuthEndpoints session expiration
- [ ] Update OAuth rate limit config
- [ ] Update AI agent timeouts
- [ ] Update PDF max file size
- [ ] Test configuration fallback chain
- [ ] Update admin UI for runtime config

**GitHub Issue**: #1187
**PR**: #_____
**Started**: _____
**Completed**: _____

**Notes**:
```
[Add notes here]
```

---

## 🟡 PHASE 3 - MEDIUM PRIORITY (DDD Completion)

### ✅ Issue #6: [DDD] Migrate Agent Services to CQRS Pattern

**Status**: ⬜ Not Started
**Priority**: 🟡 P2 - Medium
**Assignee**: _[TBD]_
**Estimated**: 10-14 hours
**Actual**: _[TBD]_

**Description**:
Migrate agent-related services to CQRS pattern in KnowledgeBase bounded context.

**Services**:
- ChessAgentService.cs
- ChessKnowledgeService.cs
- FollowUpQuestionService.cs
- AgentFeedbackService.cs

**Tasks**:
- [ ] Verify existing handlers: CreateAgent, ConfigureAgent, InvokeAgent
- [ ] Create ProvideAgentFeedbackCommand + Handler
- [ ] Create InvokeChessAgentCommand + Handler
- [ ] Create GetChessKnowledgeQuery + Handler
- [ ] Create GenerateFollowUpQuestionsQuery + Handler
- [ ] Update AiEndpoints.cs chess endpoints
- [ ] Update feedback endpoint
- [ ] Remove services

**GitHub Issue**: #1188
**PR**: #_____
**Started**: _____
**Completed**: _____

**Notes**:
```
[Add notes here]
```

---

### ✅ Issue #7: [DDD] Migrate RuleSpec Comment/Diff Services to CQRS

**Status**: ⬜ Not Started
**Priority**: 🟡 P2 - Medium
**Assignee**: _[TBD]_
**Estimated**: 6-8 hours
**Actual**: _[TBD]_

**Description**:
Migrate comment and diff services for RuleSpec to CQRS pattern.

**Services**:
- RuleSpecCommentService.cs
- RuleSpecDiffService.cs
- RuleCommentService.cs

**Tasks**:
- [ ] Create comment commands: Create, Reply, Update, Delete, Resolve, Unresolve
- [ ] Create comment queries: GetComments, GetCommentsForLine
- [ ] Create ComputeRuleSpecDiffQuery + Handler
- [ ] Keep RuleSpecDiffDomainService in domain layer
- [ ] Update RuleSpecEndpoints.cs
- [ ] Test threaded comments
- [ ] Remove services

**GitHub Issue**: #1189
**PR**: #_____
**Started**: _____
**Completed**: _____

**Notes**:
```
[Add notes here]
```

---

### ✅ Issue #8: [DDD] Implement Domain Events for All Aggregates

**Status**: ⬜ Not Started
**Priority**: 🟡 P2 - Medium
**Assignee**: _[TBD]_
**Estimated**: 8-12 hours
**Actual**: _[TBD]_

**Description**:
Implement 42 TODO domain events across all bounded contexts.

**Event Categories**:
- Authentication: 8 events
- GameManagement: 7 events
- KnowledgeBase: 6 events
- WorkflowIntegration: 2 events
- Others: 19 events

**Tasks**:
- [ ] Define events in Domain/Events/
- [ ] Authentication events (8): UserRegistered, UserLoggedIn, UserLoggedOut, PasswordChanged, 2FA, ApiKeyCreated, SessionExpired
- [ ] GameManagement events (7): GameCreated, GameUpdated, SessionStarted, Paused, Resumed, Completed, PlayerAdded
- [ ] KnowledgeBase events (6): ChatThreadCreated, Closed, MessageAdded, AgentCreated, Configured, DocumentIndexed
- [ ] Create event handlers in Application/EventHandlers/
- [ ] Wire up MediatR notifications
- [ ] Test event publishing
- [ ] Resolve all 42 TODOs

**GitHub Issue**: #1190
**PR**: #_____
**Started**: _____
**Completed**: _____

**Notes**:
```
[Add notes here]
```

---

### ✅ Issue #9: [Auth] Complete OAuth Callback Migration to CQRS

**Status**: ⬜ Not Started
**Priority**: 🟡 P2 - Medium
**Assignee**: _[TBD]_
**Estimated**: 6-8 hours
**Actual**: _[TBD]_

**Description**:
Complete OAuth migration by implementing `HandleOAuthCallbackCommand` handler.

**Files**:
- `apps/api/src/Api/Routing/AuthEndpoints.cs:564` (TODO comment)
- `apps/api/src/Api/Services/OAuthService.cs`

**Tasks**:
- [ ] Create HandleOAuthCallbackCommand
- [ ] Create HandleOAuthCallbackCommandHandler
- [ ] Move business logic from OAuthService to handler
- [ ] Keep OAuthService as infrastructure adapter
- [ ] Update AuthEndpoints.cs:564
- [ ] Test OAuth flow (Google, Discord, GitHub)
- [ ] Remove TODO comment

**GitHub Issue**: #1191
**PR**: #_____
**Started**: _____
**Completed**: _____

**Notes**:
```
[Add notes here]
```

---

## 🟢 PHASE 4 - LOW PRIORITY (Technical Debt)

### ✅ Issue #10: [Performance] Add AsNoTracking to Read-Only Queries

**Status**: ⬜ Not Started
**Priority**: 🟢 P3 - Low
**Assignee**: _[TBD]_
**Estimated**: 2-3 hours
**Actual**: _[TBD]_

**Description**:
Add `.AsNoTracking()` to read-only queries for 30% performance improvement.

**Locations**:
- PdfEndpoints.cs:217-230 (PDF text retrieval)
- PdfEndpoints.cs:318-326 (PDF progress query)
- Audit all _dbContext.* queries

**Tasks**:
- [ ] Audit all queries in endpoints
- [ ] Add AsNoTracking to read-only queries
- [ ] Ensure commands don't use AsNoTracking
- [ ] Benchmark performance before/after
- [ ] Document improvements

**GitHub Issue**: #1192
**PR**: #_____
**Started**: _____
**Completed**: _____

**Notes**:
```
[Add notes here]
```

---

### ✅ Issue #11: [Security] Improve Session Authorization and Rate Limiting

**Status**: ⬜ Not Started
**Priority**: 🟢 P3 - Low
**Assignee**: _[TBD]_
**Estimated**: 4-6 hours
**Actual**: _[TBD]_

**Description**:
Add authorization checks and rate limiting to session management endpoints.

**Files**:
- `apps/api/src/Api/Routing/AuthEndpoints.cs:748-856`

**Issues**:
- GET /auth/sessions/status - No authorization check
- POST /auth/sessions/extend - No rate limiting
- Direct DB access instead of CQRS

**Tasks**:
- [ ] Create GetSessionStatusQuery + Handler (with auth check)
- [ ] Create ExtendSessionCommand + Handler (with rate limiting)
- [ ] Create RevokeSessionCommand + Handler
- [ ] Update endpoints to use MediatR
- [ ] Test authorization scenarios
- [ ] Add audit logging

**GitHub Issue**: #1193
**PR**: #_____
**Started**: _____
**Completed**: _____

**Notes**:
```
[Add notes here]
```

---

### ✅ Issue #12: [Refactor] Centralize Error Handling with Middleware

**Status**: ⬜ Not Started
**Priority**: 🟢 P3 - Low
**Assignee**: _[TBD]_
**Estimated**: 6-8 hours
**Actual**: _[TBD]_

**Description**:
Centralize error handling to reduce code duplication and improve consistency.

**Issues**:
- Scattered try-catch blocks
- Duplicate error handling (DRY violation)
- Inconsistent error responses
- Potential information leakage

**Tasks**:
- [ ] Create GlobalExceptionHandlerMiddleware
- [ ] Create ApiExceptionFilterAttribute
- [ ] Optional: Implement Result<T> pattern
- [ ] Register middleware in Program.cs
- [ ] Remove try-catch from endpoints
- [ ] Standardize error response format
- [ ] Ensure no sensitive data leakage
- [ ] Update tests

**GitHub Issue**: #1194
**PR**: #_____
**Started**: _____
**Completed**: _____

**Notes**:
```
[Add notes here]
```

---

## 📈 Velocity Tracking

### Sprint 24 (Target: Issues #1-5)
- **Planned**: 52-63 hours
- **Actual**: _____
- **Velocity**: _____
- **Notes**: _____

### Sprint 25 (Target: Issues #6-9)
- **Planned**: 30-42 hours
- **Actual**: _____
- **Velocity**: _____
- **Notes**: _____

### Sprint 26 (Target: Issues #10-11)
- **Planned**: 6-9 hours
- **Actual**: _____
- **Velocity**: _____
- **Notes**: _____

### Sprint 27 (Target: Issue #12)
- **Planned**: 6-8 hours
- **Actual**: _____
- **Velocity**: _____
- **Notes**: _____

---

## 🎯 Milestones

- [ ] **Hotfix v1.0.1**: Issue #1 completed
- [ ] **DDD 100%**: Issues #2-5 completed
- [ ] **Domain Events**: Issue #8 completed
- [ ] **All High Priority**: Issues #1-5 completed
- [ ] **All Medium Priority**: Issues #6-9 completed
- [ ] **All Low Priority**: Issues #10-12 completed
- [ ] **Production Ready**: All 12 issues completed

---

## 📝 Meeting Notes

### 2025-11-15 - Kickoff
- API analysis completed
- 12 issues identified
- Priorities assigned
- Next: Start Issue #1 (critical)

---

## 🔗 Quick Links

- **Full Issue Templates**: `docs/improve-backend/issues-templates.md`
- **Issue Tracker**: `docs/improve-backend/ISSUE_TRACKER.md`
- **GitHub Issues**: https://github.com/DegrassiAaron/meepleai-monorepo/issues?q=is%3Aissue+label%3Addd%2Ccqrs
- **Issue #1183**: [P0] Fix Deadlock Risk in RateLimitService
- **Issue #1184**: [DDD] Migrate ChatService to CQRS Pattern
- **Issue #1185**: [DDD] Migrate RuleSpecService to CQRS Pattern
- **Issue #1186**: [DDD] Implement Streaming Query Handlers for RAG/QA
- **Issue #1187**: [Config] Replace Hardcoded Configuration Values
- **Issue #1188**: [DDD] Migrate Agent Services to CQRS Pattern
- **Issue #1189**: [DDD] Migrate RuleSpec Comment/Diff Services to CQRS
- **Issue #1190**: [DDD] Implement Domain Events for All Aggregates
- **Issue #1191**: [Auth] Complete OAuth Callback Migration to CQRS
- **Issue #1192**: [Performance] Add AsNoTracking to Read-Only Queries
- **Issue #1193**: [Security] Improve Session Authorization and Rate Limiting
- **Issue #1194**: [Refactor] Centralize Error Handling with Middleware

---

**How to Use This File**:
1. Update status for each issue (⬜ Not Started, 🟡 In Progress, ✅ Completed)
2. Add GitHub issue numbers when created
3. Add PR numbers when opened
4. Track actual hours spent
5. Add notes for blockers/decisions
6. Update progress overview table
7. Commit changes to track history

