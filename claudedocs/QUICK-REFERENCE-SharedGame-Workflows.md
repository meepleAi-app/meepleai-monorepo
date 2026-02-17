# ⚡ Quick Reference: SharedGame Workflows

**1-Page Summary** | Updated: 2026-02-12

---

## 🎯 Obiettivo

Completare **2 flussi admin** per gestione SharedGame:
1. **Manuale:** Creazione senza PDF
2. **PDF+Agent:** Upload PDF → Embedding → KB → Agent

---

## ✅ Status Flussi

| Flusso | Completamento | Gap | Action |
|--------|---------------|-----|--------|
| **Flusso 1** | **100%** ✅ | Solo test E2E | Create #4231 |
| **Flusso 2** | **60%** ⏳ | 19 issue aperte | Complete 3 waves |

---

## 📋 Issue Create Oggi

| # | Titolo | Type | Estimate |
|---|--------|------|----------|
| **#4228** | Backend Agent Linking | Backend | 1d |
| **#4229** | KB Documents Display | Frontend | 1d |
| **#4230** | Agent Integration FE | Frontend | 2d |
| **#4231** | E2E Flusso Manuale | Test | 1d |
| **#4232** | Integration Tests | Test | 1.5d |

**Totale:** 5 nuove issue | 6.5 giorni

---

## 🗓️ Timeline Veloce

```
Week 1-2: Epic #4136 Wizard (#4161-4168)     → Wizard OK
Week 3:   Agent Integration (#4228-4230+3809) → Agent OK
Week 4-5: Epic #4071 Tracking (#4215-4220)    → Enhancement
Week 6:   Testing (#4231-4232)                → Validation
```

**Total:** 6 settimane | 23 issue

---

## 🎯 Critical Path (3 settimane MVP)

```
MUST HAVE (Flusso 2 base):
  ├─ Week 1-2: #4161 → #4168 (Wizard)
  └─ Week 3:   #4228 + #3809 (Agent)

NICE TO HAVE (Enhancement):
  └─ Week 4-5: #4215 → #4220 (Tracking)
```

---

## 🔥 Start Immediately

**Priorità 1 (Questa settimana):**
1. #4161 - Wizard Container
2. #4228 - Backend Agent Linking
3. #4231 - Create test file structure

**Parallel Tracks:**
- Frontend Dev: #4161 → #4162 → #4163
- Backend Dev: #4228 (1d) → Support frontend
- QA: #4231 test structure + fixtures

---

## 📊 Dependency Graph (Simplified)

```
#4161 Wizard Container
    ├─→ #4162 Upload
    │       └─→ #4163 Metadata
    │               └─→ #4164 BGG
    │                       └─→ #4165 Confirm
    ├─→ #4166 Navigation
    └─→ #4167 Errors
            └─→ #4168 Tests

#4228 Backend Link + #3809 Agent Builder
    └─→ #4230 Agent Integration

#4229 KB Docs (independent)

#4215 Pipeline
    └─→ #4216, #4217, #4218, #4219, #4220
```

---

## 📍 Resources

| Documento | Scopo |
|-----------|-------|
| `roadmap-shared-game-workflows.md` | Timeline dettagliato |
| `test-plan-shared-game-workflows.md` | Test completi |
| `issue-sequence-table.md` | Tabella 23 issue |
| `QUICK-REFERENCE-SharedGame-Workflows.md` | Questa pagina |

**GitHub Issues:**
- Nuove: #4228, #4229, #4230, #4231, #4232
- Epic #4136: #4161-4168
- Epic #4071: #4215-4220
- Agent: #3809

---

## ⚡ Decision Matrix

### Scenario 1: "Voglio solo Flusso 2 base (no enhancement)"
**Action:** Complete Epic #4136 + #4228 + #3809 + #4230
**Timeline:** 3 settimane
**Issues:** 12

### Scenario 2: "Voglio tutto production-ready"
**Action:** Complete tutte le 23 issue
**Timeline:** 6 settimane
**Issues:** 23

### Scenario 3: "Validare Flusso 1 prima di procedere"
**Action:** Create #4231 → Test → Validate
**Timeline:** 1 giorno
**Issues:** 1

---

## 🏁 Next Command

```bash
# Validate Flusso 1 exists
cd apps/web && pnpm dev
# Open: http://localhost:3000/admin/shared-games/new

# Start Epic #4136
git checkout frontend-dev && git pull
git checkout -b feature/issue-4161-wizard-container

# Start testing
cd apps/web/tests/e2e
mkdir -p admin && touch admin/shared-game-manual-flow.spec.ts
```

---

**PM Agent Recommendation:** Start #4161 (Wizard Container) **NOW**
