# 🚀 Sequenza Implementazione Finale - SharedGame Workflows

---

## ✅ FLUSSO 1: Creazione Manuale (COMPLETO)

**Nessuna implementazione necessaria** - Solo validazione

```
✅ /admin → /admin/shared-games → /admin/shared-games/new → Crea → /admin/shared-games/[id]
```

**Action:** Creare solo test E2E (#4231)

---

## ⚠️ FLUSSO 2: PDF → KB → Agent (19 issue da completare)

### **Sequenza Ottimale:**

#### **Week 1-2: Wizard Base** (Epic #4136)
```
Day 1:    #4161 ─→ Wizard Container & State
Day 2:    #4162 ─→ Step 1: Upload PDF
Day 3-4:  #4163 ─→ Step 2: Metadata Extraction
          #4166 ─→ Navigation (parallel)

Day 5:    #4164 ─→ Step 3: BGG Match
Day 6:    #4165 ─→ Step 4: Enrich & Confirm
Day 7:    #4167 ─→ Error Handling
Day 8-9:  #4168 ─→ E2E Tests

✅ Deliverable: Wizard funzionante con nuovi endpoint
```

#### **Week 3: Agent Integration**
```
Day 1:    #4228 ─→ Backend Agent Linking (API)
Day 2-4:  #3809 ─→ Agent Builder UI (form)
Day 5:    #4229 ─→ KB Documents Display (lista)
Day 6-7:  #4230 ─→ Agent Integration (collegamento)

✅ Deliverable: Agent linkabile a SharedGame + KB docs visibili
```

#### **Week 4-5: Enhanced Tracking** (Opzionale)
```
Day 1:    #4215 ─→ 7-State Pipeline
Day 2-3:  #4216 ─→ Error Handling & Retry
          #4217 ─→ Multi-Location UI (parallel)

Day 4-5:  #4218 ─→ SSE Real-Time
Day 6:    #4219 ─→ Metrics & ETA
Day 7:    #4220 ─→ Notifications

✅ Deliverable: Real-time progress con metriche
```

#### **Week 6: Validation**
```
Day 1-2:  #4232 ─→ Integration Tests (backend)
Day 3:    Run all tests
Day 4-5:  Bug fixes + polish

✅ Deliverable: Production-ready completo
```

---

## 📊 Issue per Wave

| Wave | Issues | Giorni | Deliverable |
|------|--------|--------|-------------|
| **Wave 1** | #4161-4168 (8) | 10.5 | Wizard completo |
| **Wave 2** | #4228-4230, #3809 (4) | 7 | Agent integration |
| **Wave 3** | #4215-4220 (6) | 8.5 | Enhanced tracking |
| **Testing** | #4231-4232 (2) | 2.5 | Validation |
| **TOTALE** | **20 issue** | **28.5d** | **6 settimane** |

---

## 🎯 Execution Order (Numerica)

```
START: #4161

Sequenza Frontend:
#4161 → #4162 → #4163 → #4164 → #4165 → #4166 → #4167 → #4168

Sequenza Agent:
#4228 → #3809 → #4229 → #4230

Sequenza Tracking:
#4215 → #4216 → #4217 → #4218 → #4219 → #4220

Sequenza Testing:
#4231 (anytime) → #4232 (after #4228)
```

---

## ⚡ Quick Commands

### **Validate Flusso 1 (Now):**
```bash
cd apps/web && pnpm dev
# Open: http://localhost:3000/admin/shared-games/new
# Test: Fill form → Submit → Verify creation
```

### **Start Implementation (Today):**
```bash
git checkout frontend-dev && git pull
git checkout -b feature/issue-4161-wizard-container
# Implement #4161
```

### **Check Issue Status:**
```bash
gh issue view 4161  # Check details
gh issue list --label "epic-4136"  # Check Epic #4136
gh issue list --assignee @me  # Your issues
```

### **Track Progress:**
```bash
# Update issue
gh issue edit 4161 --add-label "in-progress"

# When done
gh issue close 4161 --comment "Completed and merged"
```

---

## 🎓 Code Patterns da Seguire

### Backend (CQRS)
```csharp
// Command pattern
CreateXCommand → Validator → Handler → IMediator.Send()

// File di riferimento
apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/CreateSharedGameCommand.cs
```

### Frontend (Component)
```tsx
// Component pattern
Client component + API integration + React Query

// File di riferimento
apps/web/src/app/(authenticated)/admin/shared-games/new/client.tsx
```

### Testing
```typescript
// E2E pattern
describe → test → page actions → assertions

// File di riferimento
apps/web/tests/e2e/ (existing patterns)
```

---

## 📌 Links Rapidi

- **Epic #4136:** https://github.com/DegrassiAaron/meepleai-monorepo/issues/4136
- **Epic #4071:** https://github.com/DegrassiAaron/meepleai-monorepo/issues/4071
- **Issue #3809:** https://github.com/DegrassiAaron/meepleai-monorepo/issues/3809
- **Nuove issue:** #4228, #4229, #4230, #4231, #4232

---

**Status:** ✅ Ready to Start | Next: #4161
