# Private Games - Admin Dashboard Priority Sequence

> **Focus**: Admin Dashboard Demo ASAP
> **Feature**: #3120 - Private Games & Catalog Proposal System
> **Created**: 2026-02-05

---

## Obiettivo

Ottenere una **demo funzionante della dashboard admin** nel minor tempo possibile, rimandando features non essenziali (PDF, AI Chat, Notifications, Frontend user).

---

## Execution Sequence (Admin-First)

```
┌─────────────────────────────────────────────────────────────────────┐
│                   ADMIN DASHBOARD PRIORITY ORDER                     │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  STEP 1: Phase 1 (#3662)                                            │
│  Data Model & Infrastructure                                        │
│  [4-5 days]                                                         │
│       │                                                              │
│       ▼                                                              │
│  STEP 2: Phase 2 (#3663) - LITE                                     │
│  Private Game CRUD (solo Add, no Update/Delete)                     │
│  [2-3 days]                                                         │
│       │                                                              │
│       ▼                                                              │
│  STEP 3: Phase 4 (#3665)                                            │
│  Proposal System (user può proporre)                                │
│  [3-4 days]                                                         │
│       │                                                              │
│       ▼                                                              │
│  STEP 4: Phase 6 (#3667)                                            │
│  Admin Review Enhancements                                          │
│  [3-4 days]                                                         │
│       │                                                              │
│       ▼                                                              │
│  ════════════════════════════════════════════════════════════════   │
│  ▶▶▶  DEMO CHECKPOINT: ADMIN DASHBOARD  ◀◀◀                        │
│  ════════════════════════════════════════════════════════════════   │
│       │                                                              │
│       ▼  (Post-demo, completare feature)                            │
│                                                                      │
│  STEP 5: Phase 2 Complete                                           │
│  Update/Delete private games                                        │
│       │                                                              │
│  STEP 6: Phase 5 (#3666)                                            │
│  Migration Choice Flow                                              │
│       │                                                              │
│  STEP 7: Phase 3 (#3664)                                            │
│  PDF & AI Chat                                                      │
│       │                                                              │
│  STEP 8: Phase 7 (#3668)                                            │
│  Notifications                                                      │
│       │                                                              │
│  STEP 9: Phase 8 (#3669)                                            │
│  Frontend Integration (user-facing)                                 │
│       │                                                              │
│  STEP 10: Phase 9 (#3670)                                           │
│  Testing & Polish                                                   │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Issue Order (Admin Dashboard Demo)

### Critical Path to Demo

| Step | Issue | Scope | Days | Cumulative |
|------|-------|-------|------|------------|
| 1 | #3662 | Data Model completo | 4-5 | 4-5 |
| 2 | #3663 | CRUD Lite (solo Add) | 2-3 | 6-8 |
| 3 | #3665 | Proposal System | 3-4 | 9-12 |
| 4 | #3667 | Admin Review | 3-4 | **12-16** |

**⏱️ Tempo stimato per demo admin: ~14 giorni**

### Post-Demo Completion

| Step | Issue | Scope | Days |
|------|-------|-------|------|
| 5 | #3663 | CRUD Complete | 2 |
| 6 | #3666 | Migration Choice | 3-4 |
| 7 | #3664 | PDF & AI Chat | 3-4 |
| 8 | #3668 | Notifications | 2-3 |
| 9 | #3669 | Frontend | 5-6 |
| 10 | #3670 | Testing | 4-5 |

---

## DEMO: Admin Dashboard

### Pre-requisiti Demo

```bash
# 1. Database con dati di test
cd apps/api/src/Api
dotnet ef database update

# 2. Seed dati demo
# - 1 utente normale con 2 giochi privati
# - 1 gioco con proposta pending
# - 1 gioco con BggId duplicato (per test merge)

# 3. Start API
dotnet run

# 4. Test con Scalar UI
# http://localhost:8080/scalar/v1
```

### Demo Script

```
┌─────────────────────────────────────────────────────────────────────┐
│                        DEMO ADMIN DASHBOARD                          │
├─────────────────────────────────────────────────────────────────────┤

PARTE 1: SETUP (User crea proposta)
═══════════════════════════════════

1. [API] Login come utente normale
   POST /api/v1/auth/login
   { "email": "user@test.com", "password": "..." }

2. [API] Cerca gioco su BGG
   GET /api/v1/bgg/search?query=Everdell

3. [API] Aggiungi come gioco privato
   POST /api/v1/user-library/private-games
   { "bggId": 199792 }

   → Response: { "id": "<privateGameId>", "title": "Everdell", ... }

4. [API] Proponi al catalogo
   POST /api/v1/user-library/private-games/<id>/propose-to-catalog
   { "notes": "Bellissimo gioco worker placement!" }

   → Response: { "shareRequestId": "<requestId>", "status": "Pending" }


PARTE 2: ADMIN REVIEW
═══════════════════════════════════

5. [API] Login come admin
   POST /api/v1/auth/login
   { "email": "admin@test.com", "password": "..." }

6. [API] Visualizza coda proposte
   GET /api/v1/admin/share-requests?type=NewGameProposal&status=Pending

   → Response: Lista proposte con dettagli gioco privato

7. [API] Check duplicati (se BggId presente)
   GET /api/v1/admin/private-games/<privateGameId>/check-duplicates

   → Response:
   {
     "hasDuplicate": false,
     "recommendation": "ApproveAsNew"
   }

   OPPURE (se duplicato):
   {
     "hasDuplicate": true,
     "existingSharedGameId": "<guid>",
     "existingSharedGameTitle": "Everdell",
     "recommendation": "MergeKnowledgeBase"
   }

8. [API] Prendi in carico review
   POST /api/v1/admin/share-requests/<requestId>/start-review

   → Response: { "status": "InReview", "reviewLockedUntil": "..." }

9. [API] Approva proposta
   POST /api/v1/admin/share-requests/<requestId>/approve-game-proposal
   {
     "action": "ApproveAsNew",
     "adminNotes": "Ottimo gioco, aggiunto al catalogo"
   }

   → Response:
   {
     "shareRequestId": "<guid>",
     "status": "Approved",
     "createdSharedGameId": "<newGameId>",
     "migrationTrackerId": "<migrationId>"
   }


PARTE 3: VERIFICA RISULTATO
═══════════════════════════════════

10. [API] Verifica SharedGame creato
    GET /api/v1/games/<createdSharedGameId>

    → Response: Nuovo gioco nel catalogo pubblico

11. [API] Verifica ProposalMigration creata
    GET /api/v1/user-library/migrations/pending (come utente)

    → Response: Lista con scelta da fare

└─────────────────────────────────────────────────────────────────────┘
```

### Scenari Demo Alternativi

#### Scenario A: Merge Knowledge Base
```bash
# Prerequisito: Everdell già esiste in SharedGames

# Admin check duplicati
GET /api/v1/admin/private-games/<id>/check-duplicates
→ { "hasDuplicate": true, "existingSharedGameId": "..." }

# Admin approva con merge
POST /api/v1/admin/share-requests/<id>/approve-game-proposal
{ "action": "MergeKnowledgeBase" }

# Risultato: PDFs del gioco privato aggiunti al gioco esistente
```

#### Scenario B: Approve as Variant
```bash
# Caso: Edizione diversa dello stesso gioco

POST /api/v1/admin/share-requests/<id>/approve-game-proposal
{
  "action": "ApproveAsVariant",
  "variantSuffix": "Collector's Edition"
}

# Risultato: Creato "Everdell (Collector's Edition)" come nuovo SharedGame
```

#### Scenario C: Reject Proposal
```bash
POST /api/v1/admin/share-requests/<id>/reject
{
  "reason": "Il gioco è già presente nel catalogo con altro nome"
}

# Risultato: Proposta rifiutata, utente notificato
```

---

## API Endpoints Required for Demo

### Must Have (Demo Critical)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/v1/bgg/search` | BGG search (già esiste) |
| `POST` | `/api/v1/user-library/private-games` | Add private game |
| `POST` | `/api/v1/user-library/private-games/{id}/propose-to-catalog` | Create proposal |
| `GET` | `/api/v1/admin/share-requests` | List proposals (filter by type) |
| `GET` | `/api/v1/admin/private-games/{id}/check-duplicates` | Check duplicates |
| `POST` | `/api/v1/admin/share-requests/{id}/start-review` | Lock for review |
| `POST` | `/api/v1/admin/share-requests/{id}/approve-game-proposal` | Approve with action |
| `POST` | `/api/v1/admin/share-requests/{id}/reject` | Reject proposal |

### Nice to Have (Post-Demo)

| Method | Endpoint | Description |
|--------|----------|-------------|
| `PUT` | `/api/v1/user-library/private-games/{id}` | Update private game |
| `DELETE` | `/api/v1/user-library/private-games/{id}` | Delete private game |
| `GET` | `/api/v1/user-library/migrations/pending` | Get pending migrations |
| `POST` | `/api/v1/user-library/migrations/{id}/choose` | Submit choice |

---

## Implementation Checklist (Admin Demo)

### Step 1: Data Model (#3662) - FULL
- [ ] PrivateGameEntity with factory methods
- [ ] Database migration (PrivateGames table)
- [ ] UserLibraryEntry FK changes
- [ ] PrivateGameRepository
- [ ] Unit tests

### Step 2: CRUD Lite (#3663 partial)
- [ ] AddPrivateGameCommand + Handler
- [ ] AddPrivateGameCommandValidator
- [ ] Auto-redirect to SharedGame logic
- [ ] Duplicate detection (same user, same BggId)
- [ ] `POST /private-games` endpoint
- [ ] Integration test: add from BGG

### Step 3: Proposal System (#3665)
- [ ] Extend ContributionType enum
- [ ] ShareRequest.SourcePrivateGameId
- [ ] Migration for ShareRequest changes
- [ ] ProposePrivateGameCommand + Handler
- [ ] `POST /private-games/{id}/propose-to-catalog` endpoint
- [ ] GetMyProposalsQuery (optional for demo)

### Step 4: Admin Review (#3667)
- [ ] ProposalApprovalAction enum
- [ ] ProposalMigration entity + migration
- [ ] CheckPrivateGameDuplicatesQuery
- [ ] ApproveGameProposalCommand + Handler
- [ ] Admin endpoints:
  - [ ] `GET /admin/share-requests?type=NewGameProposal`
  - [ ] `GET /admin/private-games/{id}/check-duplicates`
  - [ ] `POST /admin/share-requests/{id}/approve-game-proposal`
- [ ] Integration tests

---

## Demo Success Criteria

| Criterion | Required |
|-----------|----------|
| User può aggiungere gioco privato da BGG | ✅ |
| User può proporre gioco al catalogo | ✅ |
| Admin vede proposte in coda | ✅ |
| Admin può verificare duplicati | ✅ |
| Admin può approvare (ApproveAsNew) | ✅ |
| Admin può approvare (MergeKnowledgeBase) | ✅ |
| Admin può approvare (ApproveAsVariant) | ✅ |
| Admin può rifiutare | ✅ |
| SharedGame creato dopo approvazione | ✅ |
| ProposalMigration creata per scelta utente | ✅ |
| Nessuna regressione su ShareRequest esistenti | ✅ |

---

## Quick Start

```bash
# 1. Create branch for admin demo
git checkout main-dev && git pull
git checkout -b feature/issue-3120-admin-dashboard-demo
git config branch.feature/issue-3120-admin-dashboard-demo.parent main-dev

# 2. Work through steps 1-4 sequentially
# Step 1: Data Model
# Step 2: Add Private Game
# Step 3: Proposal System
# Step 4: Admin Review

# 3. Test demo flow
cd apps/api/src/Api
dotnet run

# 4. Open Scalar UI
# http://localhost:8080/scalar/v1

# 5. Execute demo script above
```

---

## Status Tracking (Admin Demo)

| Step | Issue | Task | Status |
|------|-------|------|--------|
| 1 | #3662 | PrivateGameEntity | ⏳ |
| 1 | #3662 | Migration | ⏳ |
| 1 | #3662 | Repository | ⏳ |
| 2 | #3663 | AddPrivateGameCommand | ⏳ |
| 2 | #3663 | Endpoint | ⏳ |
| 3 | #3665 | ProposeCommand | ⏳ |
| 3 | #3665 | Endpoint | ⏳ |
| 4 | #3667 | CheckDuplicatesQuery | ⏳ |
| 4 | #3667 | ApproveCommand | ⏳ |
| 4 | #3667 | Admin Endpoints | ⏳ |
| **DEMO** | - | Ready | ⏳ |

**Legend**: ⏳ Pending | 🔄 In Progress | ✅ Complete | ❌ Blocked

---

## Notes

- **PDF/AI Chat differiti**: Non necessari per demo admin
- **Notifications differite**: Verifica manuale per demo
- **Frontend differito**: Demo via Scalar/Postman
- **Migration Choice semplificata**: Solo creazione record, scelta post-demo
