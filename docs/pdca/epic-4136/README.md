# Epic #4136: PDF Wizard - PM Agent Implementation Plan

> **Status**: 📋 Planning Complete - Ready for Execution
> **Created**: 2026-02-12
> **Timeline**: 3-4 settimane (15-18 giorni lavorativi)
> **Issues**: 23 sub-issues in 3 fasi sequenziali

---

## 🎯 Quick Navigation

| Document | Purpose |
|----------|---------|
| **[plan.md](./plan.md)** | PDCA Plan: Hypothesis, risks, quality gates |
| **[EXECUTION_QUICK_START.md](./EXECUTION_QUICK_START.md)** | Copy-paste commands ready per ogni settimana |
| **do.md** | Work log: 試行錯誤, errors, solutions (created during execution) |
| **check.md** | Evaluation: results vs expectations (created after completion) |
| **act.md** | Improvements: formalized patterns, CLAUDE.md updates (created after completion) |

---

## 📊 Epic Overview

**Epic #4136**: PDF Wizard per SharedGameCatalog con BGG Integration

**Features**:
- ✅ Wizard multi-step per admin/editor
- ✅ Upload PDF locale con estrazione AI
- ✅ Integrazione BGG (search + enrichment)
- ✅ Bulk import JSON con SSE progress
- ✅ Approval workflow Editor → Admin
- ✅ Duplicate detection con warnings

**Sub-Issues**: 23 totali
- Phase 1 (Backend): 7 issues (#4154-#4160)
- Phase 2 (Frontend): 8 issues (#4161-#4168)
- Phase 3 (Bulk Import): 8 issues (#4169-#4176)

---

## 🚀 Start Execution

### Immediate Next Action

```bash
/implementa 4154
```

This starts **Issue #4154: Backend - Upload PDF Command & Handler** (Day 1, Week 1).

### Execution Order

**Sequential Critical Path**:
```
#4154 → #4155 → #4156 → #4157 → [#4158 || #4159] → #4160
  ↓ (endpoints ready)
#4161 → [#4162 || #4163 || #4164 || #4165] → #4166 → #4167 → #4168
  ↓ (wizard complete)
#4169 → #4170 → #4171 || #4172 → [#4173 || #4174 || #4175] → #4176
```

**Parallel Opportunities** (6 points):
1. Day 5: `#4158 || #4159` (2 terminals)
2. Days 7-8: `#4162 || #4163 || #4164 || #4165` (4 terminals - MAXIMUM)
3. Day 10: `#4167 || #4160` (2 terminals)
4. Day 15: `#4171 || #4172` (2 terminals)
5. Days 16-17: `#4173 || #4174 || #4175` (3 terminals)

---

## 📋 Task Tracking

**View All Tasks**:
```bash
/tasks
```

**Current Status**: 27 tasks created
- 3 Phase tasks (macro)
- 23 Sub-issue tasks (atomic)
- 1 Documentation task

**Dependencies**: Critical path modeled with task blocking

---

## 🛡️ Quality Gates (Per Issue)

**Backend** (#4154-#4160, #4169-#4171):
```bash
✅ dotnet build --no-restore
✅ dotnet test ≥90% coverage
✅ Integration tests green
✅ /code-review:code-review score ≥80%
```

**Frontend** (#4161-#4168, #4172-#4176):
```bash
✅ pnpm typecheck
✅ pnpm test ≥85% coverage
✅ pnpm lint
✅ /code-review:code-review score ≥80%
✅ E2E tests green (for #4168, #4176)
```

**MANDATORY**: Code review BEFORE merge for ogni issue

---

## 📈 Progress Metrics

**Target Completion**:
- Week 1: 7 issues (30%)
- Week 2: +8 issues (65%)
- Week 3: +8 issues (100%)
- Week 4: Testing + docs

**Daily Velocity**: ~1.5 issues/day average

**Critical Milestones**:
- Day 5: Backend wizard complete → Frontend can start
- Day 12: Frontend wizard complete → Feature usable
- Day 17: Bulk import complete → Epic feature-complete
- Day 21: Documentation complete → Epic closable

---

## 💡 Key Learnings (Pre-Execution)

**Existing Patterns to Follow**:
- ✅ CQRS: 30+ command handlers already in SharedGameCatalog
- ✅ Upload: BulkImportGamesCommand pattern exists
- ✅ BGG: IBggApiClient + ImportGameFromBggCommand exists
- ✅ OCR: EnhancedPdfProcessingOrchestrator production-ready
- ✅ Approval: SubmitSharedGameForApprovalCommand exists

**Risk Areas**:
- ⚠️ SmolDocling OCR accuracy → Confidence scoring + manual fallback
- ⚠️ BGG API timeouts → Retry logic + manual BGG ID
- ⚠️ Wizard state complexity → Zustand centralized store
- ⚠️ Bulk import performance → Batching + SSE progress

**MCP Tool Strategy**:
- Phase 1: `serena` + `context7` (backend patterns)
- Phase 2: `magic` + `context7` + `playwright` (UI + E2E)
- Phase 3: `serena` + `sequential` (bulk + debugging)

---

## 🔗 References

- **Epic Issue**: [#4136](https://github.com/meepleAi-app/meepleai-monorepo/issues/4136)
- **Spec Document**: [epic-4136-breakdown.md](../../04-features/admin-game-import/epic-4136-breakdown.md)
- **Task List**: Use `/tasks` command
- **PDCA Docs**: This directory (`docs/pdca/epic-4136/`)

---

**Ready to Execute**: Run `/implementa 4154` to start Week 1, Day 1 🚀
