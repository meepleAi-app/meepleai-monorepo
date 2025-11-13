# 🚀 Quick Start: Frontend + BGAI Implementation

**Full Details**: [`implementation-roadmap.md`](./implementation-roadmap.md) (942 righe)

---

## 📊 Panoramica

### Cosa Implementiamo
- **8 issue FE-IMP**: Modernizzazione frontend (App Router, TanStack Query, Zustand, Server Actions)
- **69 issue BGAI**: Board Game AI Month 1-6 (PDF → LLM → Validation → Quality → Dataset → Italian UI)
- **39 issue Admin Console**: Dashboard, Infrastructure, Management, Reports

### Strategia
✅ **Lavoro parallelo** su 3 worktree
✅ **5 checkpoint** di merge e test manuali integrati
✅ **10 settimane** di implementazione

---

## 🔧 Setup Worktree (Giorno 1)

```bash
# 1. Frontend worktree
cd /home/user/meepleai-monorepo
git worktree add ../meepleai-frontend-worktree claude/frontend-modernization

# 2. Backend worktree
git worktree add ../meepleai-backend-worktree claude/backend-bgai-months-4-6

# 3. Main worktree (Admin Console)
# Resta su: claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo

# Verifica
git worktree list
```

**Output**:
```
/home/user/meepleai-monorepo              (claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo)
/home/user/meepleai-frontend-worktree    (claude/frontend-modernization)
/home/user/meepleai-backend-worktree     (claude/backend-bgai-months-4-6)
```

---

## 📅 Timeline 10 Settimane

| Fase | Settimane | Frontend | Backend | Admin Console | Checkpoint |
|------|-----------|----------|---------|---------------|------------|
| **0** | 0 | Setup worktree | Setup worktree | Setup worktree | - |
| **1** | 1-2 | FE-IMP-001-003<br/>(App Router, Query) | BGAI #983-987<br/>(Quality Framework) | Admin #884-889<br/>(Dashboard) | **CP1** ✓ |
| **2** | 3-4 | FE-IMP-004-006<br/>(Auth, SDK, Forms) | BGAI #996-1006<br/>(Dataset + API) | Admin #890-902<br/>(Infrastructure) | **CP2** ✓ |
| **3** | 5-6 | FE-IMP-007 + BGAI UI<br/>(Chat Store + Q&A) | BGAI #1007<br/>(SSE Streaming) | Admin #903-914<br/>(Management) | **CP3** ✓ |
| **4** | 7-8 | FE-IMP-008 + Italian<br/>(Upload + PDF) | BGAI #1012<br/>(Adversarial) | Admin #915-922<br/>(Reports) | **CP4** ✓ |
| **5** | 9-10 | BGAI Testing<br/>(E2E + Responsive) | BGAI #1019-1023<br/>(Validation) | Epics #926-936<br/>(Polish) | **FINAL** ✓ |

---

## 🎯 Issue per Worktree

### 🎨 Frontend Worktree (`claude/frontend-modernization`)
**Issue**: FE-IMP-001 → 008 + BGAI frontend (39 issue totali)

| ID | Titolo | Settimana | Dipendenze |
|----|--------|-----------|------------|
| FE-IMP-001 | App Router Bootstrap | S1 | - |
| FE-IMP-002 | Server Actions | S1 | FE-IMP-001 |
| FE-IMP-003 | TanStack Query | S1 | FE-IMP-001 |
| FE-IMP-004 | AuthContext + Middleware | S3 | FE-IMP-003 |
| FE-IMP-005 | API SDK modulare | S3 | FE-IMP-003 |
| FE-IMP-006 | Form System (RHF) | S3 | FE-IMP-004 |
| FE-IMP-007 | Chat Store Zustand | S5 | FE-IMP-005 |
| FE-IMP-008 | Upload Queue Worker | S7 | FE-IMP-005 |
| #989-995 | BGAI Components + Testing | S1-2 | FE-IMP-001 |
| #1001-1008 | Q&A Interface + SSE | S5-6 | FE-IMP-007 |
| #1010-1011 | Dataset annotations | S7 | - |
| #1013-1017 | PDF Viewer + Italian UI | S7-8 | - |
| #1018 | E2E Testing | S9 | All above |
| #992-994 | Testing + Optimization | S9-10 | All above |

### 🔧 Backend Worktree (`claude/backend-bgai-months-4-6`)
**Issue**: BGAI #983-1023 backend (30 issue)

| ID | Titolo | Settimana | Dipendenze |
|----|--------|-----------|------------|
| #983-987 | Quality Framework | S1-2 | - |
| #996-998 | Dataset annotations (50 Q&A) | S3 | - |
| #999-1000 | Accuracy baseline | S3-4 | #996-998 |
| #1006 | API `/board-game-ai/ask` | S4 | #983-987 |
| #1007 | Streaming SSE endpoint | S5-6 | #1006 |
| #1012 | Adversarial dataset (50 queries) | S7 | #996-1000 |
| #1019 | Accuracy validation (≥80%) | S9 | #1012 |
| #1020 | Performance testing (P95 <3s) | S9 | All above |
| #1021-1023 | Bug fixes + Docs + Completion | S10 | All above |

### 🏠 Main Worktree (`claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo`)
**Issue**: Admin Console #884-922 + Frontend Epics #926-936 (45 issue)

| ID | Titolo | Settimana | Dipendenze |
|----|--------|-----------|------------|
| #884-889 | FASE 1: Dashboard | S1-2 | - |
| #890-902 | FASE 2: Infrastructure | S3-4 | #884-889 |
| #903-914 | FASE 3: Management | S5-6 | #890-902 |
| #915-922 | FASE 4: Advanced Reports | S7-8 | #903-914 |
| #926-936 | Frontend Epics (Polish) | S9-10 | All FE-IMP |

---

## ✅ Checkpoint Protocol

### Prima di Ogni Checkpoint

```bash
# 1. Backend tests
cd /home/user/meepleai-backend-worktree
dotnet test && git push

# 2. Frontend tests
cd /home/user/meepleai-frontend-worktree
pnpm test && pnpm build && git push

# 3. Main tests
cd /home/user/meepleai-monorepo
pnpm test && git push

# 4. Merge nel main worktree
git pull origin claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo
git merge claude/backend-bgai-months-4-6 --no-ff -m "CP#: Backend"
git merge claude/frontend-modernization --no-ff -m "CP#: Frontend"

# 5. Full test suite
pnpm test && cd apps/api/src/Api && dotnet test && cd - && pnpm build

# 6. Test manuali (vedi sezione sotto)

# 7. Push + Rebase altri worktree
git push -u origin claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo
```

### Dopo Merge: Rebase Worktree

```bash
# Backend worktree
cd /home/user/meepleai-backend-worktree
git fetch origin claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo
git rebase origin/claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo

# Frontend worktree
cd /home/user/meepleai-frontend-worktree
git fetch origin claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo
git rebase origin/claude/analyze-frontend-issues-011CV5re59xZsma3sqrD1BMo
```

---

## 🧪 Test Manuali per Checkpoint

### CP1 (Fine Settimana 2): Foundation
```
✓ App Router serve / e /chat
✓ TanStack Query carica user/games/chats senza duplicati
✓ Admin Dashboard mostra ActivityFeed con polling 30s
✓ Quality Framework: Grafana dashboard visibile
✓ Prometheus metrics scrapabili
```

### CP2 (Fine Settimana 4): Data Layer + BGAI API
```
✓ POST /api/v1/board-game-ai/ask → risposta con answer + confidence + citations
✓ Accuracy test su 50 Q&A ≥75%
✓ Auth: anonimo → /chat → redirect /login (server-side)
✓ Login con form RHF → Server Action funziona
✓ Admin Infrastructure: ServiceHealthMatrix mostra tutti servizi
```

### CP3 (Fine Settimana 6): Q&A Flow
```
✓ Selezionare game → Fare domanda → Streaming SSE token-by-token
✓ ResponseCard mostra answer + confidence + citations
✓ Chat Store: solo componenti interessati re-render
✓ Admin Management: creare API key, bulk export CSV
```

### CP4 (Fine Settimana 8): Upload + PDF + Italian
```
✓ Upload 10 PDF → UI responsive (FPS >50)
✓ Click citation → PDF viewer apre a pagina corretta
✓ Cambiare lingua → Italiano → 200+ strings tradotte
✓ Admin Reports: builder UI + schedule + email test
```

### CP FINAL (Fine Settimana 10): Production Ready
```
✓ User journey completo: login → domanda → PDF citation
✓ Accuracy su 100 Q&A ≥80%
✓ P95 latency <3s
✓ Lighthouse: Performance ≥90, Accessibility ≥95
✓ Coverage: Frontend 90%+, Backend 90%+
✓ Zero critical bugs
✓ Adversarial testing (domande ambigue/off-topic)
```

---

## 🎯 Metriche di Successo

| Metrica | Target | Checkpoint | Strumento |
|---------|--------|------------|-----------|
| **Test Coverage** | ≥90% | Tutti | Jest, xUnit |
| **BGAI Accuracy** | ≥80% | FINAL | Test suite |
| **API Latency P95** | <3s | CP4, FINAL | Prometheus |
| **Lighthouse Performance** | ≥90 | FINAL | Chrome DevTools |
| **Lighthouse Accessibility** | ≥95 | FINAL | Chrome DevTools |
| **Bundle Size** | <500KB | CP4, FINAL | `pnpm build` |
| **Build Time** | <2min | Tutti | `time pnpm build` |

---

## 🔥 Comandi Rapidi

### Switch Worktree
```bash
# Frontend
cd /home/user/meepleai-frontend-worktree && git status

# Backend
cd /home/user/meepleai-backend-worktree && git status

# Main (Admin Console)
cd /home/user/meepleai-monorepo && git status
```

### Dev Servers
```bash
# Frontend (porta 3000)
cd /home/user/meepleai-frontend-worktree && pnpm dev

# Backend (porta 8080)
cd /home/user/meepleai-backend-worktree && cd apps/api/src/Api && dotnet run

# Docker services
cd /home/user/meepleai-monorepo/infra && docker compose up -d
```

### Pre-Checkpoint Tests
```bash
cd /home/user/meepleai-monorepo
pnpm test:coverage        # Frontend
cd apps/api/src/Api && dotnet test  # Backend
cd ../../web && pnpm build          # Build
pnpm test:e2e                       # E2E
```

---

## ⚠️ Regole Importanti

### 1. **Merge Order**: Backend → Frontend → Main
Sempre merge backend prima di frontend per evitare conflitti API.

### 2. **Test Before Merge**
Zero merge se tests non passano nel worktree di origine.

### 3. **Rebase After Merge**
Sempre rebase worktree su main aggiornato dopo checkpoint.

### 4. **Communication**
Commit message con prefix: `feat(fe-imp):`, `feat(bgai):`, `feat(admin):`

### 5. **Breaking Changes**
Documentare in commit body se API o types cambiano.

---

## 📁 Struttura File Nuovi

### Frontend (FE-IMP)
```
apps/web/
├── app/                           # FE-IMP-001
│   ├── layout.tsx
│   ├── page.tsx
│   ├── chat/page.tsx
│   ├── actions/auth.ts            # FE-IMP-002
│   └── providers.tsx
├── lib/
│   ├── clients/                   # FE-IMP-005
│   │   ├── authClient.ts
│   │   ├── chatClient.ts
│   │   └── uploadClient.ts
│   └── hooks/
│       └── useCurrentUser.ts      # FE-IMP-003
├── components/
│   ├── forms/                     # FE-IMP-006
│   │   ├── Form.tsx
│   │   └── FormField.tsx
│   └── board-game-ai/             # BGAI #1001-1008
│       ├── QuestionInputForm.tsx
│       ├── ResponseCard.tsx
│       └── GameSelector.tsx
├── store/                         # FE-IMP-007
│   ├── chatStore.ts
│   └── slices/
│       ├── sessionSlice.ts
│       ├── gameSlice.ts
│       └── messagesSlice.ts
└── middleware.ts                  # FE-IMP-004
```

### Backend (BGAI)
```
apps/api/src/Api/BoundedContexts/
└── BoardGameAI/                   # Nuovo bounded context
    ├── Domain/
    │   ├── Question.cs
    │   └── Answer.cs
    ├── Application/
    │   ├── Commands/
    │   │   └── AskQuestionCommand.cs
    │   └── Handlers/
    │       └── AskQuestionHandler.cs
    └── Infrastructure/
        └── Repositories/
            └── QuestionRepository.cs
```

---

## 🆘 Troubleshooting

### Conflitti di Merge
1. Identificare file conflittuali: `git status`
2. Priorità decisioni: **Backend > Frontend > Main**
3. Review con `git diff`
4. Test dopo risoluzione: `pnpm test && dotnet test`

### Worktree Non Funziona
```bash
# Rimuovere e ricreare
git worktree remove ../meepleai-frontend-worktree
git worktree add ../meepleai-frontend-worktree claude/frontend-modernization
```

### Test Falliscono Dopo Merge
1. Verificare coverage non sceso sotto 90%
2. Controllare breaking changes in commit body
3. Eseguire `pnpm test --clearCache`
4. Se persistono: rollback merge, fix nel worktree, retry

---

## 📚 Documenti di Riferimento

- **Roadmap Completa**: [`implementation-roadmap.md`](./implementation-roadmap.md)
- **Issue FE-IMP**: [`issues.md`](./issues.md)
- **Piano Generale**: [`plan.md`](./plan.md)
- **CLAUDE.md**: [`/CLAUDE.md`](/CLAUDE.md)
- **Issue Tracker**: [`docs/07-project-management/planning/issue-status-tracker.md`](../07-project-management/planning/issue-status-tracker.md)

---

## 🚀 Prossimi Step

### 1. **ORA** (Giorno 1)
```bash
# Setup worktree
cd /home/user/meepleai-monorepo
git worktree add ../meepleai-frontend-worktree claude/frontend-modernization
git worktree add ../meepleai-backend-worktree claude/backend-bgai-months-4-6
git worktree list
```

### 2. **Settimana 1** (Giorni 1-7)
- **Frontend**: FE-IMP-001 (App Router) - 2 giorni
- **Frontend**: FE-IMP-002 (Server Actions) - 2 giorni
- **Frontend**: FE-IMP-003 (TanStack Query) - 2 giorni
- **Backend**: BGAI #983-987 (Quality Framework) - 5 giorni paralleli
- **Main**: Admin #884-889 (Dashboard) - 5 giorni paralleli

### 3. **Fine Settimana 2**
- **Checkpoint 1**: Merge + Test manuali
- Decision: GO/NO-GO per FASE 2

---

**Version**: 1.0
**Created**: 2025-11-13
**Owner**: Engineering Team
**Status**: 🟢 Ready to Start

**Quick Command to Start**:
```bash
cd /home/user/meepleai-monorepo && \
git worktree add ../meepleai-frontend-worktree claude/frontend-modernization && \
git worktree add ../meepleai-backend-worktree claude/backend-bgai-months-4-6 && \
echo "✅ Worktree setup complete! See QUICK-START-ROADMAP.md for next steps"
```
