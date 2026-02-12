# 📊 Tabella Sequenza Issue - SharedGame Workflows

**Generated:** 2026-02-12 | **Total Issues:** 23

---

## 🎯 Legenda

| Symbol | Significato |
|--------|-------------|
| ✅ | Implementato/Completo |
| ⏳ | Issue aperta (da fare) |
| 🔴 | Blocker (priorità critica) |
| 🟡 | Importante (priorità alta) |
| 🟢 | Enhancement (priorità media) |

---

## 📋 Sequenza Completa per Flusso 2

### **Wave 1: PDF Wizard Frontend** (2 settimane)

| # | Issue | Status | Priority | Size | Dependencies | Week |
|---|-------|--------|----------|------|--------------|------|
| **#4161** | Wizard Container & State | ⏳ | 🔴 P0 | M | - | W1 |
| **#4162** | Step 1: Upload PDF | ⏳ | 🔴 P0 | M | #4161 | W1 |
| **#4163** | Step 2: Metadata Extraction | ⏳ | 🔴 P0 | L | #4162 | W1 |
| #4166 | Navigation & Progress | ⏳ | 🔴 P0 | M | #4161 | W1 (parallel) |
| **#4164** | Step 3: BGG Match | ⏳ | 🔴 P0 | M | #4163 | W2 |
| **#4165** | Step 4: Enrich & Confirm | ⏳ | 🔴 P0 | M | #4164 | W2 |
| #4167 | Error Handling | ⏳ | 🔴 P0 | M | #4161-4165 | W2 |
| #4168 | Wizard E2E Tests | ⏳ | 🔴 P0 | L | #4161-4167 | W2 |

**Subtotal:** 8 issue | 10.5 giorni | **Deliverable:** Wizard funzionante

---

### **Wave 2: Agent Integration** (1 settimana)

| # | Issue | Status | Priority | Size | Dependencies | Day |
|---|-------|--------|----------|------|--------------|-----|
| **#4228** | Backend - Agent Linking | ⏳ **NEW** | 🟡 P1 | M | - | D1 |
| **#3809** | Agent Builder UI | ⏳ | 🟡 P1 | L | - | D2-4 |
| **#4229** | KB Documents Display | ⏳ **NEW** | 🟡 P1 | M | - | D5 (parallel) |
| **#4230** | Agent Integration Frontend | ⏳ **NEW** | 🟡 P1 | L | #4228, #3809 | D6-7 |

**Subtotal:** 4 issue | 7 giorni | **Deliverable:** Agent linkabile + KB visibile

---

### **Wave 3: PDF Status Tracking** (2 settimane)

| # | Issue | Status | Priority | Size | Dependencies | Week |
|---|-------|--------|----------|------|--------------|------|
| **#4215** | 7-State Pipeline | ⏳ | 🔴 P0 | M | - | W1 D1 |
| #4216 | Error Handling & Retry | ⏳ | 🟡 P1 | M | #4215 | W1 D2-3 |
| #4217 | Multi-Location Status UI | ⏳ | 🟡 P1 | L | #4215 | W1 D2-4 |
| #4218 | Real-Time SSE Updates | ⏳ | 🟡 P1 | M | #4216, #4217 | W2 D1-2 |
| #4219 | Duration Metrics & ETA | ⏳ | 🟢 P2 | M | #4216 | W2 D3 |
| #4220 | Multi-Channel Notifications | ⏳ | 🟢 P2 | M | #4216 | W2 D4 |

**Subtotal:** 6 issue | 8.5 giorni | **Deliverable:** Real-time tracking completo

---

### **Wave 4: Testing & Validation** (Parallel con Wave 3)

| # | Issue | Status | Priority | Size | Dependencies | Timeline |
|---|-------|--------|----------|------|--------------|----------|
| **#4231** | E2E - Flusso Manuale | ⏳ **NEW** | 🟡 P1 | M | - | Anytime |
| **#4232** | Integration - Workflows | ⏳ **NEW** | 🟡 P1 | M | #4228 | After Wave 2 |

**Subtotal:** 2 issue | 2.5 giorni | **Deliverable:** Test coverage completo

---

## 🗺️ Gantt Chart (Simplified)

```
Week 1-2: Epic #4136 Wizard Frontend
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
#4161 ████
#4162     ████
#4163         ██████
#4166 ████ (parallel)
#4164             ████
#4165                 ████
#4167                     ████
#4168                         ████████

Week 3: Agent Integration
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
#4228 ████
#3809   ████████████
#4229           ████ (parallel)
#4230                 ████████

Week 4-5: PDF Tracking (Optional)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
#4215 ████
#4216     ██████
#4217     ████████
#4218             ██████
#4219                   ████
#4220                       ████

Testing (Parallel)
━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
#4231 ████ (anytime)
#4232                 ████████ (after Wave 2)
```

---

## 🎯 Critical Path Analysis

### **Minimum Viable (Flusso 2 base):**
```
Path: #4161 → #4162 → #4163 → #4164 → #4165 → #4168
Duration: 9.5 giorni (2 settimane)
Deliverable: Wizard funzionante
```

### **Complete Integration:**
```
Path: Epic #4136 → #4228 → #3809 → #4230
Duration: 19 giorni (4 settimane)
Deliverable: Flusso 2 completo con agent
```

### **Full Enhancement:**
```
Path: Above + Epic #4071
Duration: 28 giorni (6 settimane)
Deliverable: Production-ready con monitoring
```

---

## 📈 Progress Tracking

### Sprint Milestones

| Sprint | Goal | Issues | Success Metric |
|--------|------|--------|----------------|
| **Sprint 1** | Wizard Steps 1-3 | #4161-4163, #4166 | ✅ Upload → Metadata funzionante |
| **Sprint 2** | Wizard Complete | #4164-4168 | ✅ E2E wizard test passa |
| **Sprint 3** | Agent Integration | #4228, #3809, #4229, #4230 | ✅ Agent linkabile e visibile |
| **Sprint 4** | Tracking Foundation | #4215-4217 | ✅ SSE progress funzionante |
| **Sprint 5** | Tracking Enhanced | #4218-4220 | ✅ Metriche e notifiche |
| **Sprint 6** | Testing Complete | #4231, #4232 | ✅ Tutti test green |

### Issue Status Dashboard

**By Priority:**
- 🔴 P0 (Critical): 9 issue (#4161-4168, #4215)
- 🟡 P1 (High): 11 issue (#4228-4232, #4216-4218, #3809)
- 🟢 P2 (Medium): 3 issue (#4219-4220, performance tests)

**By Type:**
- Frontend: 12 issue
- Backend: 8 issue
- Testing: 3 issue

**By Epic:**
- Epic #4136 (Wizard): 8 issue
- Epic #4071 (Tracking): 6 issue
- Agent Integration: 4 issue
- Testing: 2 issue
- Standalone: 3 issue

---

## 🚀 Quick Start Guide

### **Today (Day 0):**
```bash
✅ Issue create (#4228-4232)
✅ Roadmap creata
✅ Test plan creato
→ Validare Flusso 1 manualmente
```

### **This Week (Day 1-5):**
```bash
→ Start #4161 (Wizard Container)
→ Start #4228 (Backend Linking) parallel
→ Create #4231 test file structure
```

### **Next Week (Day 6-10):**
```bash
→ Complete #4162-4166 (Wizard steps)
→ Complete #3809 (Agent Builder)
→ Start #4229 (KB docs)
```

### **Week 3 (Day 11-15):**
```bash
→ Complete #4167-4168 (Wizard finale)
→ Complete #4230 (Agent integration)
→ Run #4231 E2E tests
```

### **Week 4+ (Enhancement):**
```bash
→ Optional: Epic #4071 (PDF tracking)
→ Run complete test suite (#4232)
```

---

## 📊 Dependency Matrix

| Issue | Blocks | Blocked By |
|-------|--------|------------|
| #4161 | #4162, #4166 | - |
| #4162 | #4163 | #4161 |
| #4163 | #4164 | #4162 |
| #4164 | #4165 | #4163 |
| #4165 | #4167, #4168 | #4164 |
| #4166 | #4167 | #4161 |
| #4167 | #4168 | #4165, #4166 |
| #4168 | - | #4167 |
| #4228 | #4230, #4232 | - |
| #3809 | #4230 | - |
| #4229 | - | - |
| #4230 | - | #4228, #3809 |
| #4215 | #4216-4220 | - |

---

## ✅ Success Checklist

### Flusso 1 (Manuale)
- [ ] E2E test #4231 created and passing
- [ ] Manual validation: Create game → View detail → Success
- [ ] Performance: <2s creation time
- [ ] No errors in console/logs

### Flusso 2 (PDF + Agent)
- [ ] Epic #4136 completed (#4161-4168)
- [ ] Agent integration completed (#4228, #3809, #4229, #4230)
- [ ] E2E test covering full flow passing
- [ ] Performance: <30s PDF → Game creation
- [ ] KB documents visible in UI
- [ ] Agent creatable and linkable

### Quality Gates
- [ ] Backend test coverage ≥90%
- [ ] Frontend test coverage ≥85%
- [ ] Integration tests ≥85%
- [ ] E2E tests for critical paths 100%
- [ ] No TypeScript errors
- [ ] No console warnings in production

---

## 📈 Metrics & KPIs

| Metric | Current | Target | Status |
|--------|---------|--------|--------|
| **Flusso 1 Coverage** | ❓ | 100% | ⏳ After #4231 |
| **Flusso 2 Coverage** | 0% | 100% | ⏳ After Wave 1-3 |
| **Backend Tests** | ❓ | ≥90% | ⏳ Verify |
| **Frontend Tests** | ❓ | ≥85% | ⏳ Verify |
| **Issues Completed** | 0/23 | 23/23 | 0% |
| **Timeline Progress** | Day 0 | Week 6 | 0% |

---

**Next Update:** End of Sprint 1 (2 weeks)
**Owner:** PM Agent
**Documents:**
- 📍 Roadmap: `roadmap-shared-game-workflows.md`
- 🧪 Test Plan: `test-plan-shared-game-workflows.md`
- 📊 This Table: `issue-sequence-table.md`
