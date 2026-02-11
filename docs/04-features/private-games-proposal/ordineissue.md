# Private Games & Catalog Proposal - Issue Sequence & Demo Checkpoints

> **Feature**: #3120 - Private Games & Catalog Proposal System
> **Created**: 2026-02-05
> **Total Issues**: 9 phases

---

## Issue Execution Order

```
┌─────────────────────────────────────────────────────────────────────┐
│                        EXECUTION SEQUENCE                            │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  Phase 1 (#3662)                                                    │
│  Data Model & Infrastructure                                        │
│       │                                                              │
│       ▼                                                              │
│  Phase 2 (#3663)                                                    │
│  Private Game CRUD                                                  │
│       │                                                              │
│       ▼                                                              │
│  Phase 3 (#3664)  ◄─────  DEMO CHECKPOINT 1: MVP                   │
│  PDF & AI Chat                                                      │
│       │                                                              │
│       ▼                                                              │
│  Phase 4 (#3665)                                                    │
│  Proposal System                                                    │
│       │                                                              │
│       ▼                                                              │
│  Phase 5 (#3666)                                                    │
│  Migration Choice Flow                                              │
│       │                                                              │
│       ▼                                                              │
│  Phase 6 (#3667)  ◄─────  DEMO CHECKPOINT 2: Backend Complete      │
│  Admin Review Enhancements                                          │
│       │                                                              │
│       ▼                                                              │
│  Phase 7 (#3668)                                                    │
│  Notifications                                                      │
│       │                                                              │
│       ▼                                                              │
│  Phase 8 (#3669)  ◄─────  DEMO CHECKPOINT 3: Full Feature          │
│  Frontend Integration                                               │
│       │                                                              │
│       ▼                                                              │
│  Phase 9 (#3670)  ◄─────  DEMO CHECKPOINT 4: Production Ready      │
│  Testing & Polish                                                   │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
```

---

## Detailed Issue Sequence

### Priority Order

| # | Issue | Title | Depends On | Est. Days |
|---|-------|-------|------------|-----------|
| 1 | #3662 | Phase 1: Data Model & Core Infrastructure | - | 4-5 |
| 2 | #3663 | Phase 2: Private Game CRUD | #3662 | 4-5 |
| 3 | #3664 | Phase 3: PDF & AI Chat | #3663 | 3-4 |
| 4 | #3665 | Phase 4: Proposal System | #3664 | 4-5 |
| 5 | #3666 | Phase 5: Migration Choice Flow | #3665 | 3-4 |
| 6 | #3667 | Phase 6: Admin Review Enhancements | #3666 | 3-4 |
| 7 | #3668 | Phase 7: Notifications | #3667 | 2-3 |
| 8 | #3669 | Phase 8: Frontend Integration | #3668 | 5-6 |
| 9 | #3670 | Phase 9: Testing & Polish | #3669 | 4-5 |

---

## Demo Checkpoints

### DEMO 1: MVP - Private Games with AI Chat
**After**: Phase 3 (#3664)
**Duration**: ~12 days cumulative
**Branch**: `feature/issue-3120-private-games-mvp`

#### Demo Script
```
1. LOGIN come utente normale

2. SEARCH BGG
   - Vai a "Aggiungi gioco" → "Cerca su BGG"
   - Cerca "Wingspan" (o gioco non nel catalogo)
   - Mostra risultati BGG

3. ADD PRIVATE GAME (BGG)
   - Clicca "Aggiungi alla libreria"
   - Verifica gioco appare con badge "Privato"
   - Mostra dettagli gioco (dati da BGG)

4. ADD MANUAL GAME
   - Vai a "Aggiungi gioco" → "Crea manualmente"
   - Compila: "Prototipo Indie", 2-4 giocatori
   - Salva e verifica badge "Privato"

5. UPLOAD PDF
   - Apri gioco privato
   - Upload regolamento PDF
   - Verifica upload completato

6. AI CHAT
   - Apri chat per gioco privato
   - Chiedi: "Come si vince a questo gioco?"
   - Mostra risposta AI basata su PDF caricato
```

#### API Endpoints da Testare
```bash
# BGG Search
GET /api/v1/bgg/search?query=wingspan

# Add Private Game (BGG)
POST /api/v1/user-library/private-games
{ "bggId": 266192 }

# Add Private Game (Manual)
POST /api/v1/user-library/private-games
{ "title": "Prototipo Indie", "minPlayers": 2, "maxPlayers": 4 }

# Upload PDF
POST /api/v1/documents/upload
{ "privateGameId": "<guid>", "file": <pdf> }

# AI Chat
POST /api/v1/chat
{ "privateGameId": "<guid>", "message": "Come si vince?" }
```

#### Success Criteria
- [ ] BGG search restituisce risultati
- [ ] Gioco privato appare in libreria con badge
- [ ] PDF upload funziona per giochi privati
- [ ] AI Chat risponde con contesto dal PDF
- [ ] Nessuna regressione su giochi SharedGame

---

### DEMO 2: Backend Complete - Proposal System
**After**: Phase 6 (#3667)
**Duration**: ~25 days cumulative
**Branch**: `feature/issue-3120-proposals`

#### Demo Script
```
1. UTENTE: PROPONI GIOCO
   - Login come utente
   - Apri gioco privato con PDF caricato
   - Clicca "Proponi al catalogo"
   - Aggiungi note: "Ottimo gioco indie da fiera"
   - Invia proposta

2. ADMIN: REVIEW QUEUE
   - Login come admin
   - Vai a "Admin → Proposte giochi"
   - Mostra proposta in coda

3. ADMIN: CHECK DUPLICATES
   - Clicca "Verifica duplicati"
   - Se gioco ha BggId, mostra check duplicati
   - Mostra opzioni: Approva/Unisci KB/Rifiuta

4. ADMIN: APPROVE
   - Seleziona "Approva come nuovo"
   - Aggiungi note admin
   - Conferma approvazione

5. UTENTE: MIGRATION CHOICE
   - Login come utente
   - Mostra notifica: "Gioco approvato!"
   - Mostra modal scelta:
     - "Collega al catalogo" vs "Mantieni privato"
   - Seleziona opzione
   - Verifica risultato
```

#### API Endpoints da Testare
```bash
# Propose to Catalog
POST /api/v1/user-library/private-games/<id>/propose-to-catalog
{ "notes": "Ottimo gioco..." }

# Get My Proposals
GET /api/v1/user-library/proposals

# Admin: Check Duplicates
GET /api/v1/admin/private-games/<id>/check-duplicates

# Admin: Approve Proposal
POST /api/v1/admin/share-requests/<id>/approve-game-proposal
{ "action": "ApproveAsNew", "adminNotes": "..." }

# User: Handle Migration Choice
POST /api/v1/user-library/migrations/<id>/choose
{ "choice": "LinkToCatalog" }
```

#### Success Criteria
- [ ] Proposta creata correttamente
- [ ] Proposta appare in admin queue
- [ ] Check duplicati funziona
- [ ] Approvazione crea SharedGame
- [ ] Utente può scegliere migrazione
- [ ] LibraryEntry aggiornata se LinkToCatalog

---

### DEMO 3: Full Feature - Frontend Complete
**After**: Phase 8 (#3669)
**Duration**: ~35 days cumulative
**Branch**: `feature/issue-3120-frontend`

#### Demo Script
```
1. USER FLOW COMPLETO
   a. Login
   b. BGG Search Dialog → Add private game
   c. Manual Add Form → Add prototype
   d. Private Game Card → Edit details
   e. Upload PDF → View documents
   f. AI Chat → Ask questions
   g. Propose to Catalog → Track status
   h. Migration Choice Modal → Make decision

2. ADMIN FLOW COMPLETO
   a. Login as admin
   b. Proposals Dashboard → Filter/Sort
   c. Review Details → Full game info
   d. Duplicate Check → Visual indicator
   e. Approval Actions → All 3 options
   f. Merge KB → PDF transfer visual

3. NOTIFICATION FLOW
   a. Proposal submitted → User notified
   b. Proposal approved → User notified with action
   c. Migration choice → Confirmation
```

#### Frontend Components da Verificare
- [ ] `BggSearchDialog.tsx` - Ricerca funzionale
- [ ] `AddPrivateGameForm.tsx` - Validazione form
- [ ] `PrivateGameCard.tsx` - Badge e azioni
- [ ] `ProposeGameModal.tsx` - Flow proposta
- [ ] `MigrationChoiceModal.tsx` - Scelta chiara
- [ ] `/library/proposals` - Dashboard proposte
- [ ] Responsive design (mobile/tablet)
- [ ] Accessibility (keyboard, screen reader)

#### Success Criteria
- [ ] Tutti i componenti UI funzionanti
- [ ] Form validation client-side
- [ ] Loading states appropriati
- [ ] Error handling user-friendly
- [ ] Design coerente con design system

---

### DEMO 4: Production Ready
**After**: Phase 9 (#3670)
**Duration**: ~40 days cumulative
**Branch**: `main-dev` (merged)

#### Demo Script
```
1. REGRESSION CHECK
   - Verifica tutti i flow esistenti funzionano
   - SharedGame add/edit/delete
   - PDF upload per SharedGame
   - AI Chat per SharedGame
   - Admin BGG import

2. PERFORMANCE CHECK
   - BGG Search < 3s
   - Private Game CRUD < 2s
   - AI Chat first response < 5s
   - Page load < 2s

3. LOAD TEST (opzionale)
   - 10 utenti concorrenti
   - Mix di operazioni
   - Nessun errore 5xx

4. EDGE CASES
   - Rate limiting verification
   - Quota enforcement
   - Duplicate BggId handling
   - Large PDF upload
```

#### Test Coverage Report
```bash
# Backend
dotnet test /p:CollectCoverage=true

# Frontend
pnpm test:coverage
```

#### Success Criteria
- [ ] 90%+ test coverage backend
- [ ] 85%+ test coverage frontend
- [ ] Zero regression
- [ ] Performance benchmarks met
- [ ] Documentation complete
- [ ] API docs (Scalar) updated

---

## Branch Strategy

```
main-dev
    │
    ├── feature/issue-3662-phase1-data-model
    │       │
    │       └── PR → main-dev (after Phase 1)
    │
    ├── feature/issue-3663-phase2-crud
    │       │
    │       └── PR → main-dev (after Phase 2)
    │
    ├── feature/issue-3664-phase3-pdf-chat
    │       │
    │       └── PR → main-dev (after Phase 3 - MVP DEMO)
    │
    └── ... (continue per ogni fase)
```

---

## Quick Reference

### Start Working
```bash
# Check current status
gh issue list --label "kind/feature" --search "#3120"

# Start phase 1
git checkout main-dev && git pull
git checkout -b feature/issue-3662-phase1-data-model
git config branch.feature/issue-3662-phase1-data-model.parent main-dev
```

### After Each Phase
```bash
# Commit e PR
git push -u origin feature/issue-<N>-<name>
gh pr create --base main-dev --title "Phase N: <Title>" --body "Closes #<issue>"

# After merge, cleanup
git checkout main-dev && git pull
git branch -D feature/issue-<N>-<name>
```

### Demo Preparation
```bash
# Ensure clean environment
docker compose down -v
docker compose up -d postgres qdrant redis

# Apply migrations
cd apps/api/src/Api && dotnet ef database update

# Start services
dotnet run  # Terminal 1
cd ../../../web && pnpm dev  # Terminal 2
```

---

## Status Tracking

| Phase | Issue | Status | Demo Ready |
|-------|-------|--------|------------|
| 1 | #3662 | ⏳ Pending | - |
| 2 | #3663 | ⏳ Pending | - |
| 3 | #3664 | ⏳ Pending | DEMO 1 |
| 4 | #3665 | ⏳ Pending | - |
| 5 | #3666 | ⏳ Pending | - |
| 6 | #3667 | ⏳ Pending | DEMO 2 |
| 7 | #3668 | ⏳ Pending | - |
| 8 | #3669 | ⏳ Pending | DEMO 3 |
| 9 | #3670 | ⏳ Pending | DEMO 4 |

**Legend**: ⏳ Pending | 🔄 In Progress | ✅ Complete | ❌ Blocked
