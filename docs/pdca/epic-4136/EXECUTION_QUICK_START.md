# Epic #4136 - Quick Start Execution Guide

> **Copy-Paste Commands**: Esegui in ordine sequenziale, rispettando le dipendenze

---

## ⚡ Week 1: Backend Foundation (5 giorni)

### Day 1-4: Sequential Critical Path
```bash
/implementa 4154  # Upload PDF Command (1d) → FIRST BLOCKER
/implementa 4155  # Extract Metadata Query (1-2d) → Requires #4154
/implementa 4156  # BGG Enrichment Command (1d) → Requires #4155
/implementa 4157  # Wizard Endpoints (1d) → Requires #4154, #4155, #4156
```

### Day 5: Parallel Execution ⚡⚡
```bash
# Terminal 1
/implementa 4158  # Duplicate Detection (1d)

# Terminal 2 (concurrent)
/implementa 4159  # Approval Workflow (1d)
```

**Phase 1 Complete**: All backend wizard endpoints operational ✅

---

## 🎨 Week 2: Frontend Wizard (7 giorni)

### Day 6: Container Foundation
```bash
/implementa 4161  # Wizard Container & State (1d) → Requires #4157
```

### Days 7-8: Parallel UI Steps ⚡⚡⚡⚡ (MAXIMUM)
```bash
# Terminal 1
/implementa 4162  # Step 1: Upload PDF (1d)

# Terminal 2 (concurrent)
/implementa 4163  # Step 2: Metadata Extraction (1d)

# Terminal 3 (concurrent)
/implementa 4164  # Step 3: BGG Match (1-2d)

# Terminal 4 (concurrent)
/implementa 4165  # Step 4: Enrich & Confirm (1-2d)
```

### Day 9: Navigation Integration
```bash
/implementa 4166  # Wizard Navigation & Progress (0.5d)
```

### Day 10: Parallel Polish ⚡⚡
```bash
# Terminal 1
/implementa 4167  # Error Handling (1d)

# Terminal 2 (concurrent)
/implementa 4160  # Backend E2E Tests (1d)
```

### Days 11-12: Frontend E2E
```bash
/implementa 4168  # Frontend E2E Playwright Tests (1d)
```

**Phase 2 Complete**: Wizard UI funzionante end-to-end ✅

---

## 📦 Week 3: Bulk Import (5 giorni)

### Days 13-14: Backend Sequential
```bash
/implementa 4169  # Bulk Import Command (1d)
/implementa 4170  # SSE Progress Endpoint (1d)
```

### Day 15: Parallel Backend + Frontend ⚡⚡
```bash
# Terminal 1
/implementa 4171  # Bulk Import Endpoint (0.5d)

# Terminal 2 (concurrent)
/implementa 4172  # Bulk Upload UI (1d)
```

### Days 16-17: Parallel Frontend ⚡⚡⚡
```bash
# Terminal 1
/implementa 4173  # Bulk Preview & Validation (1d)

# Terminal 2 (concurrent)
/implementa 4174  # Bulk Progress SSE (1d)

# Terminal 3 (concurrent)
/implementa 4175  # Bulk Results Summary (0.5d)
```

**Phase 3 Complete**: Bulk import operativo con SSE ✅

---

## 🧪 Week 4: Final Testing & Docs (3 giorni)

### Day 18: Bulk E2E
```bash
/implementa 4176  # Bulk E2E Tests (1d)
```

### Days 19-21: Documentation & Closure
```bash
# API Documentation
# - Complete Swagger annotations
# - Create Postman collection
# - Write API reference docs

# User Guide
# - Wizard flow screenshots
# - Bulk import JSON examples
# - Troubleshooting guide

# Epic Closure
# - Verify all 23 PRs merged
# - Update Epic #4136 acceptance criteria all ✅
# - Close Epic #4136
# - Update MEMORY.md
```

**Epic Complete**: 23/23 issues closed 🏆

---

## 📊 Progress Tracking Commands

### Check Task Status
```bash
/tasks  # Show all tasks with status
```

### Check Epic Progress
```bash
gh issue view 4136 --repo meepleAi-app/meepleai-monorepo
```

### Check Sub-Issue Status
```bash
gh issue list --repo meepleAi-app/meepleai-monorepo --search "is:issue label:Epic-4136" --json number,state,title
```

### Test Status
```bash
# Backend
pwsh -c "cd apps/api/src/Api; dotnet test /p:CollectCoverage=true"

# Frontend
pwsh -c "cd apps/web; pnpm test; pnpm test:coverage"
```

---

## 🎯 Daily Checklist

**Every Morning**:
- [ ] `git status && git pull`
- [ ] Check memory: `read_memory("session/checkpoint")`
- [ ] Review yesterday: `read_memory("execution/epic-4136/do")`
- [ ] Plan today's issues

**During Work**:
- [ ] Execute `/implementa [issue-number]`
- [ ] Checkpoint every 30 min
- [ ] Document 試行錯誤 in do.md
- [ ] Update task progress

**Every Evening**:
- [ ] Run all tests (backend + frontend)
- [ ] think_about_task_adherence()
- [ ] Update docs/pdca/epic-4136/do.md
- [ ] write_memory("session/checkpoint")
- [ ] write_memory("execution/epic-4136/do", daily_summary)

---

## 🚨 Emergency Protocols

### Build Broken
```bash
# Backend
pwsh -c "cd apps/api/src/Api; dotnet clean; dotnet restore; dotnet build"

# Frontend
pwsh -c "cd apps/web; rm -rf .next; pnpm install; pnpm build"
```

### Test Failures
```bash
# NEVER skip tests - investigate and fix
dotnet test --filter "FullyQualifiedName~[FailingTest]"
/sc:sequential "Analyze test failure: [error]"
```

### Code Review <80%
```bash
# Fix critical issues (confidence ≥70%)
# Max 3 iterations
# If still failing → AskUserQuestion
```

### Blocker Detection
```bash
# If SmolDocling OCR fails → Switch to manual input
# If BGG API timeout → Manual BGG ID fallback
# If approval breaks → Rollback #4159, hotfix
```

---

**Start Now**: `/implementa 4154`
