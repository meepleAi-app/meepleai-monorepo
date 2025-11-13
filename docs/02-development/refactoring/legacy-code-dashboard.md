# 🎯 Legacy Code Removal Dashboard

**Last Updated**: 2025-11-13
**Status**: 🔴 In Progress (99% → 100%)

---

## 📊 Overall Progress

```
DDD Refactoring:  [████████████████████░] 99% → 100%
Endpoints:        [████░░░░░░░░░░░░░░░░]  20% (2/10 files)
Legacy Services:  [░░░░░░░░░░░░░░░░░░░░]   4% (100+/105 files)
Code Lines:       [░░░░░░░░░░░░░░░░░░░░]   3% (~800/24,000 lines)
```

**Target Completion**: ~20 weeks (5 months)

---

## 🎯 Endpoint Files Status

| File | Lines | Legacy % | Status | Priority | Effort |
|------|-------|----------|--------|----------|--------|
| **KnowledgeBaseEndpoints.cs** | 165 | 0% | ✅ DONE | - | 0 |
| **UserProfileEndpoints.cs** | 208 | 0% | ✅ DONE | - | 0 |
| **AiEndpoints.cs** | ~2,400 | 100% | ⬜ TODO | 🔴 CRITICAL | 4-5w |
| **RuleSpecEndpoints.cs** | 573 | 100% | ⬜ TODO | 🔴 CRITICAL | 2w |
| **ChatEndpoints.cs** | 370 | 95% | ⬜ TODO | 🔴 CRITICAL | 1.5w |
| **AuthEndpoints.cs** | 926 | 60% | ⬜ TODO | 🟠 HIGH | 2-3w |
| **PdfEndpoints.cs** | 488 | 70% | ⬜ TODO | 🟠 HIGH | 2w |
| **AdminEndpoints.cs** | 2,423 | 70% | ⬜ TODO | 🟡 MEDIUM | 3-4w |
| **GameEndpoints.cs** | 243 | 5% | ⬜ TODO | 🟡 MEDIUM | 1h |
| **UserManagementEndpoints.cs** | - | 0% | ✅ REMOVED | - | - |

**Total Legacy Code**: ~7,430 lines in 7 files

---

## 🗂️ Bounded Contexts Status

| Context | Handlers | Endpoints Migrated | Legacy Services | Status |
|---------|----------|-------------------|-----------------|--------|
| **GameManagement** | 15+ | ✅ 95% (Game CRUD) | 10 services | 🟡 95% |
| **DocumentProcessing** | 8+ | ✅ 30% (PDF Index) | 5 services | 🟡 30% |
| **Authentication** | 6+ | ✅ 40% (Reg/Login) | 20 services | 🟠 40% |
| **KnowledgeBase** | 2 | ✅ 5% (Search) | 40 services | 🔴 5% |
| **WorkflowIntegration** | 3+ | ⚠️ Partial | 5 services | 🟡 50% |
| **SystemConfiguration** | 5+ | ⚠️ Partial | 5 services | 🟡 60% |
| **Administration** | 5+ | ⚠️ Partial | 10 services | 🟠 30% |

---

## 📅 Migration Timeline

### Phase 1: CRITICAL (Weeks 1-9) 🔴

```
Week 1-5:  AiEndpoints.cs        [⬜⬜⬜⬜⬜] KnowledgeBase    2,400 lines
Week 6-7:  RuleSpecEndpoints.cs  [⬜⬜] GameManagement       573 lines
Week 8-9:  ChatEndpoints.cs      [⬜⬜] KnowledgeBase        370 lines
```

**Impact**: Core user-facing AI functionality

---

### Phase 2: HIGH (Weeks 10-14) 🟠

```
Week 10-12: AuthEndpoints.cs     [⬜⬜⬜] Authentication     926 lines
Week 13-14: PdfEndpoints.cs      [⬜⬜] DocumentProcessing 488 lines
```

**Impact**: Security and document processing

---

### Phase 3: MEDIUM (Weeks 15-18) 🟡

```
Week 15-18: AdminEndpoints.cs    [⬜⬜⬜⬜] Administration   2,423 lines
Week 18:    GameEndpoints.cs     [⬜] GameManagement        (1 call fix)
```

**Impact**: Admin features

---

### Phase 4: CLEANUP (Weeks 19-20) 🧹

```
Week 19-20: Remove ~100 legacy service files
```

**Impact**: Clean codebase

---

## 🚨 Top 3 Legacy Hotspots

### 1. 🔥 AiEndpoints.cs - 2,400 LINES
**Why Critical**: Main AI/RAG functionality, affects ALL users
**Services Used**: 10+ (RagService, ChatService, LlmService, etc.)
**Complexity**: HIGH - Multiple sub-features (QA, streaming, chess, BGG)

### 2. 🔥 AdminEndpoints.cs - 2,423 LINES
**Why Important**: Admin features, low user impact but large codebase
**Services Used**: 15+ (PromptService, AlertingService, CacheService, etc.)
**Complexity**: MEDIUM - Many features but isolated

### 3. 🔥 AuthEndpoints.cs - 926 LINES (60% legacy)
**Why Critical**: Security-sensitive, affects all authentication flows
**Services Used**: 10+ (OAuthService, TotpService, SessionService, etc.)
**Complexity**: HIGH - 2FA, OAuth, password reset flows

---

## 📦 Services to Keep (Per CLAUDE.md)

| Service | Type | Reason |
|---------|------|--------|
| **RagService** | Orchestration | RAG pipeline coordinator |
| **ConfigurationService** | Orchestration | Runtime config management |
| **AdminStatsService** | Orchestration | Stats aggregation |
| **AlertingService** | Infrastructure | Multi-channel alerting |

These are wrapped in CQRS handlers but NOT removed.

---

## 📦 Services by Bounded Context

### KnowledgeBase (~40 services) 🔴
```
Priority: CRITICAL
Main Services:
  • ChatService (CRUD + messages)
  • StreamingQaService, StreamingRagService
  • HybridSearchService, KeywordSearchService
  • LlmService, EmbeddingService, QdrantService
  • PromptTemplateService, PromptEvaluationService
  • ChessAgentService, ChessKnowledgeService
```

### Authentication (~20 services) 🟠
```
Priority: HIGH
Main Services:
  • OAuthService (Google/Discord/GitHub)
  • TotpService (2FA)
  • SessionManagementService, SessionCacheService
  • PasswordResetService
  • ApiKeyManagementService
```

### GameManagement (~10 services) 🟡
```
Priority: HIGH
Main Services:
  • RuleSpecService (CRUD, versioning)
  • RuleCommentService, RuleSpecCommentService
  • RuleSpecDiffService
  • BggApiService
```

### DocumentProcessing (~5 services) 🟠
```
Priority: MEDIUM
Main Services:
  • PdfStorageService (CRUD)
```

### Administration (~10 services) 🟡
```
Priority: MEDIUM
Main Services:
  • AuditService
  • CacheMetricsRecorder, CacheWarmingService
  • RateLimitService
  • BackgroundTaskService
```

### WorkflowIntegration (~5 services) 🟡
```
Priority: MEDIUM
Main Services:
  • N8nConfigService, N8nTemplateService
  • WorkflowErrorLoggingService
```

### SystemConfiguration (~5 services) 🟡
```
Priority: MEDIUM
Main Services:
  • FeatureFlagService
  • DynamicTtlStrategy
```

---

## ✅ Weekly Checklist

### Week N: {Endpoint File Name}

**Preparation**
- [ ] Read endpoint file thoroughly
- [ ] Identify all legacy service dependencies
- [ ] Map endpoints to bounded context
- [ ] Review existing domain logic

**Implementation**
- [ ] Create Commands/Queries (Application layer)
- [ ] Create Handlers with MediatR
- [ ] Register in DI (Service Extensions)
- [ ] Write unit tests (handlers)
- [ ] Write integration tests (endpoints)

**Migration**
- [ ] Update endpoints to use `IMediator.Send()`
- [ ] Remove legacy service injections
- [ ] Verify all tests pass (≥90% coverage)
- [ ] Run build (`dotnet build`)

**Cleanup**
- [ ] Remove legacy service files (if ALL endpoints migrated)
- [ ] Remove DI registrations
- [ ] Update documentation
- [ ] Commit changes

**Quality Gates**
- [ ] Zero build errors
- [ ] Zero build warnings
- [ ] All tests passing
- [ ] Coverage ≥90%
- [ ] PR approved

---

## 🎖️ Definition of Done

Migration is **COMPLETE** when:

### Code ✅
- [x] 10/10 endpoint files use only `IMediator`
- [x] ~100 legacy services removed (keep 4 orchestration + infrastructure)
- [x] All Commands/Queries in bounded contexts
- [x] Zero direct service injection in endpoints

### Quality ✅
- [x] All tests passing (xUnit + Testcontainers)
- [x] Coverage ≥90% maintained
- [x] Zero build errors/warnings
- [x] Performance benchmarks met

### Documentation ✅
- [x] CLAUDE.md updated to 100% DDD
- [x] Architecture docs updated
- [x] This dashboard marked "COMPLETED"

---

## 📈 Metrics Tracking

### Update This Section Weekly

**Week of YYYY-MM-DD**:
- Endpoints migrated: X/10 (+Y this week)
- Legacy services removed: X/105 (+Y this week)
- Test coverage: X%
- Blockers: None / {description}

**Week of YYYY-MM-DD**:
- Endpoints migrated: X/10 (+Y this week)
- Legacy services removed: X/105 (+Y this week)
- Test coverage: X%
- Blockers: None / {description}

---

## 🚦 Risk Status

| Risk | Status | Mitigation |
|------|--------|------------|
| Breaking changes | 🟡 MEDIUM | Feature flags, gradual rollout |
| Coverage drop | 🟢 LOW | Pre-commit checks, PR reviews |
| Performance regression | 🟢 LOW | Benchmarks, load testing |
| Timeline overrun | 🟡 MEDIUM | Prioritize critical first |

---

## 📚 Quick Links

- [Full Inventory Document](./legacy-code-inventory-and-removal-plan.md)
- [CLAUDE.md](../../CLAUDE.md)
- [DDD Quick Reference](../ddd-quick-reference.md)
- [Architecture Overview](../architecture/board-game-ai-architecture-overview.md)

---

## 🏆 Progress History

| Date | Milestone | Lines Removed | Status |
|------|-----------|---------------|--------|
| 2025-11-11 | DDD Migration Start | 0 | 🟡 Started |
| 2025-11-13 | Inventory Complete | 0 | 📊 Documented |
| YYYY-MM-DD | Phase 1 Complete | ~3,350 | ⬜ Pending |
| YYYY-MM-DD | Phase 2 Complete | ~5,000 | ⬜ Pending |
| YYYY-MM-DD | Phase 3 Complete | ~7,500 | ⬜ Pending |
| YYYY-MM-DD | **100% DDD** | ~24,000 | ⬜ Pending |

---

**Status Legend**:
- 🔴 CRITICAL - Must do first
- 🟠 HIGH - Important, do second
- 🟡 MEDIUM - Do third
- 🟢 LOW - Nice to have
- ✅ DONE - Completed
- ⬜ TODO - Not started
- 🚧 IN PROGRESS - Currently working
