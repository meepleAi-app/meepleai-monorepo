# Epic #4070 Follow-up - Implementation Sequence

**Created**: 2026-02-12
**PM Agent**: Optimized execution plan for 5 follow-up issues
**Total Estimate**: ~3 weeks

---

## 🎯 Execution Strategy

**Approach**: Quick Wins → Foundation → Critical Path → Enhancement

**Rationale**:
1. **Build Momentum**: Start with easy wins (#4127, #4128) to validate workflow
2. **Prepare Foundation**: Toast system (#4129) ready for API error handling
3. **Critical Path**: Clean codebase enables focused API work (#4126)
4. **Future Value**: API complete unlocks enhancement features (#4130)

---

## 📅 Implementation Timeline

### Week 1: Quick Wins + Foundation (Days 1-4)

#### Day 1 (Morning) - Task #2: Router Refactoring
```bash
/implementa 4127
```
- **Issue**: #4127 - Use Next.js router for agent navigation
- **Priority**: P1-High
- **Estimate**: 2 hours
- **Files**: 2 files (agents/page.tsx, your-agents-widget.tsx)
- **Risk**: Low
- **Dependencies**: None

**Deliverable**: Faster navigation, no full page reloads

---

#### Day 1 (Afternoon) - Task #3: Test Fixes
```bash
/implementa 4128
```
- **Issue**: #4128 - Fix AgentChat async test timeouts
- **Priority**: P1-High
- **Estimate**: 4 hours
- **Files**: 1 file (AgentChat.test.tsx)
- **Risk**: Low-Medium (requires debugging)
- **Dependencies**: None
- **Parallel**: Can overlap with #4127

**Deliverable**: 36/36 tests passing (100% pass rate)

---

#### Day 2 - Task #4: Toast Notifications
```bash
/implementa 4129
```
- **Issue**: #4129 - Add toast notifications for agent actions
- **Priority**: P2-Medium
- **Estimate**: 4 hours
- **Files**: 2 files (AgentChatSidebar.tsx, AgentChat.tsx)
- **Risk**: Low
- **Dependencies**: None

**Deliverable**: User feedback system for chat actions

---

### Week 2-3: Critical Path (Days 3-14)

#### Days 3-14 - Task #5: API Integration
```bash
/implementa 4126
```
- **Issue**: #4126 - API Integration: Agent Chat SSE + React Query
- **Priority**: P0-Critical
- **Estimate**: 2 weeks (10 working days)
- **Scope**: Backend + Frontend full integration
- **Risk**: High (complex, production blocker)
- **Dependencies**: ✅ #4127, #4128, #4129 (foundation ready)

**Backend Work** (Week 2):
- Command: `SendAgentMessageCommand` (CQRS)
- Handler: SSE streaming with confidence scores
- Endpoints: POST /agents/{id}/chat, GET /agents, GET /agents/recent
- Tests: Integration tests for SSE

**Frontend Work** (Week 2-3):
- React Query hooks: useAgents(), useAgentChat(), useRecentAgents()
- Remove mock data from 5 files
- SSE client implementation with EventSource
- Error handling with toast notifications

**Deliverable**: Production-ready agent chat with real backend

---

### Week 4: Enhancement (Days 15-16)

#### Days 15-16 - Task #6: PDF Viewer Integration
```bash
/implementa 4130
```
- **Issue**: #4130 - Integrate PDF viewer with chat citations
- **Priority**: P3-Low
- **Estimate**: 2 days
- **Files**: 2 files (AgentMessage.tsx, ChatMessage.tsx)
- **Risk**: Low
- **Dependencies**: ⚠️ BLOCKS on #4126 (needs real citations from API)

**Deliverable**: Click citation → PDF scroll to reference

---

## 🔄 Dependency Graph

```
        ┌─────────┐     ┌─────────┐     ┌─────────┐
        │ #4127   │     │ #4128   │     │ #4129   │
Day 1-2 │ Router  │     │ Tests   │     │ Toasts  │
        │ (2h)    │     │ (4h)    │     │ (4h)    │
        └────┬────┘     └────┬────┘     └────┬────┘
             │               │               │
             └───────────┬───┴───────────────┘
                         ↓
                   ┌──────────┐
Day 3-14           │  #4126   │
                   │   API    │
                   │  (2 weeks)│
                   └─────┬────┘
                         ↓
                   ┌──────────┐
Day 15-16          │  #4130   │
                   │   PDF    │
                   │  (2 days) │
                   └──────────┘
```

---

## 💡 Execution Commands (Copy-Paste Ready)

### Week 1: Quick Wins
```bash
# Day 1 Morning (2 hours)
/implementa 4127

# Day 1 Afternoon (4 hours)
/implementa 4128

# Day 2 (4 hours)
/implementa 4129

# Checkpoint
git status
git log --oneline -5
pnpm test && pnpm typecheck
```

### Week 2-3: Critical Path
```bash
# Days 3-14 (2 weeks)
/implementa 4126

# Post-implementation validation
cd apps/api/src/Api && dotnet test
cd apps/web && pnpm test && pnpm build
```

### Week 4: Enhancement (Optional)
```bash
# Days 15-16 (2 days) - Only if #4126 complete
/implementa 4130
```

---

## ⚡ Parallel Execution Option (2 Developers)

**If you have 2 developers, run Week 1 in parallel**:

### Developer A:
```bash
/implementa 4127  # Day 1: Router (2h)
/implementa 4129  # Day 1: Toasts (4h)
```

### Developer B:
```bash
/implementa 4128  # Day 1: Tests (4h)
```

**Result**: Week 1 complete in 1 day instead of 2

---

## 📊 Progress Tracking

### Daily Checklist

**Day 1**:
- [ ] `/implementa 4127` complete
- [ ] `/implementa 4128` complete
- [ ] Router: No window.location.href usage
- [ ] Tests: 36/36 passing

**Day 2**:
- [ ] `/implementa 4129` complete
- [ ] Toast system integrated
- [ ] Error feedback working

**Days 3-14**:
- [ ] `/implementa 4126` in progress
- [ ] Backend SSE endpoint complete
- [ ] Frontend React Query integrated
- [ ] All mock data removed
- [ ] Integration tests passing

**Days 15-16** (Optional):
- [ ] `/implementa 4130` complete
- [ ] Citation → PDF scroll working

---

## 🎯 Quality Gates

**After Each Issue**:
```bash
# Backend
cd apps/api/src/Api
dotnet build --no-restore
dotnet test --no-build

# Frontend
cd apps/web
pnpm build
pnpm test
pnpm typecheck
pnpm lint

# Git
git status  # Should be clean after each /implementa
git log -1  # Verify commit message
```

**Before Moving to Next Issue**:
- ✅ All tests passing
- ✅ No TypeScript errors
- ✅ No ESLint warnings
- ✅ PR merged and branch deleted

---

## 🚨 Risk Mitigation

### Issue #4126 Risks (API Integration)

**Risk 1**: SSE streaming complexity
- **Mitigation**: Research official EventSource patterns with context7 first
- **Fallback**: Implement polling as backup, add SSE later

**Risk 2**: React Query learning curve
- **Mitigation**: Study existing React Query usage in codebase
- **Pattern**: Copy from library pages (games, collections)

**Risk 3**: Mock data removal breaks existing features
- **Mitigation**: Feature flags for gradual rollout
- **Testing**: Comprehensive E2E tests before removing mocks

### Issue #4128 Risks (Test Fixes)

**Risk 1**: Cannot fix fake timer issue
- **Mitigation**: Use real timers with increased timeout
- **Fallback**: Skip flaky tests temporarily, document as known issue

---

## 📈 Success Metrics

**Week 1 End**:
- ✅ 3 issues complete (#4127, #4128, #4129)
- ✅ All tests passing (100%)
- ✅ Navigation optimized
- ✅ Toast system ready

**Week 2-3 End**:
- ✅ API integration complete (#4126)
- ✅ No mock data remaining
- ✅ Production-ready agent chat
- ✅ React Query throughout

**Week 4 End**:
- ✅ PDF integration complete (#4130)
- ✅ All Epic #4070 technical debt resolved
- ✅ 100% production ready

---

## 🎬 Getting Started

**Execute this command sequence**:

```bash
# Week 1 Day 1
/implementa 4127
/implementa 4128

# Week 1 Day 2
/implementa 4129

# Week 2-3
/implementa 4126

# Week 4 (optional)
/implementa 4130
```

**After each command completes**:
- Review PR
- Approve and merge
- Verify tests pass
- Move to next issue

---

## 📚 Reference

- **Epic**: #4070 (CLOSED)
- **Code Review**: claudedocs/epic-4070-code-review.md
- **Issues**: #4126-#4130
- **Task Tracker**: Claude Code task list

**Next Command**: `/implementa 4127` (Start Day 1)
