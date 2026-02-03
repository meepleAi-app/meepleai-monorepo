# Plan: Analisi Flussi Critici - Admin & User Journeys

**Data**: 2026-02-02
**PM Agent**: Analisi issue aperte e gap per supportare due flussi critici

## Hypothesis

Analizzando le 101 issue aperte nel progetto, identifico gap significativi per completare i flussi:

### FLUSSO 1 - Admin Game Creation
```
Log admin → Dashboard Personale → Wizard Creazione Gioco → Upload PDF →
Gioco disponibile in Shared Library
```

### FLUSSO 2 - User Private Collection
```
Log user → Dashboard Personale → Aggiungi Gioco a Collection → Upload PDF Privato →
Crea Chat con Agente → Chat History
```

## Expected Outcomes

### Coverage Gap Analysis
- ✅ Identificare issue esistenti che coprono i flussi
- ✅ Identificare gap (issue mancanti)
- ✅ Proporre struttura epic (nuove/estensioni)
- ✅ Definire ordine esecuzione con parallelizzazione

### Deliverables
1. **Gap Report**: Issue mancanti per ciascun flusso
2. **Epic Structure**: 1 epic nuova + 2 epic estese
3. **Execution Plan**: Roadmap 3 weeks con parallelizzazione
4. **sequenza.md Update**: Consolidamento aggiornato

## Issue Coverage Analysis

### FLUSSO 1 - Admin Game Creation

#### ✅ Issue Esistenti Utilizzabili
| Issue | Status | Titolo | Area | SP |
|-------|--------|--------|------|:--:|
| #3307-#3314 | Mixed | Dashboard Hub skeleton e widgets | Frontend | 21 |
| #3372 | CLOSED | Link PDF to Game during creation | Backend+FE | 3 |
| #3369-#3371 | CLOSED | PDF processing UI components | Frontend | 6 |
| #3370 | OPEN | usePdfProcessingProgress hook | Frontend | 2 |
| #3324 | OPEN | SSE Infrastructure (BLOCKER) | Backend | 5 |

**Coverage**: ~75% (fondazioni esistono, manca pubblicazione Shared Library)

#### ❌ Gap - Issue Mancanti

| ID | Titolo | Area | SP | Descrizione |
|----|--------|------|:--:|-------------|
| **Issue A** | Admin Wizard - Publish to Shared Library | Frontend | 3 | Step finale wizard per pubblicare in SharedGameCatalog |
| **Issue B** | SharedGameCatalog Publication Workflow | Backend | 5 | Endpoint e logica pubblicazione con approval status |
| **Issue C** | Game Approval Status Management UI | Frontend | 2 | UI per visualizzare/gestire stati approvazione |

**Gap Totale**: 3 issues, 10 SP

### FLUSSO 2 - User Private Collection

#### ✅ Issue Esistenti Utilizzabili
| Issue | Status | Titolo | Area | SP |
|-------|--------|--------|------|:--:|
| #3307-#3314 | Mixed | Dashboard Hub (base comune) | Frontend | 21 |
| #3375 | OPEN | Agent Session Launch | Frontend | 3 |
| #3376 | OPEN | Agent Creation Wizard | Frontend | 5 |
| #3312 | CLOSED | ChatHistorySection widget | Frontend | 2 |

**Coverage**: ~40% (dashboard base + agent creation, manca collection management)

#### ❌ Gap - Issue Mancanti

| ID | Titolo | Area | SP | Descrizione |
|----|--------|------|:--:|-------------|
| **Issue D** | User Collection Dashboard | Frontend | 5 | Layout, lista collezioni personali, statistiche |
| **Issue E** | Add Game to Collection Wizard | Frontend | 5 | Multi-step: search game → upload PDF privato → conferma |
| **Issue F** | UserLibraryEntry PDF Association | Backend | 3 | Estendi UserLibraryEntry per supportare PDF privati |
| **Issue G** | Private PDF Upload Endpoint | Backend | 3 | Endpoint dedicato per PDF associati a UserLibraryEntry |
| **Issue H** | Chat Session Persistence Service | Backend | 5 | Save/load chat sessions, associazione game/user |
| **Issue I** | Chat History Integration | Frontend | 3 | Integrazione widget #3312 con backend persistenza |

**Gap Totale**: 6 issues, 24 SP

## Epic Structure

### Epic 1 (NUOVA): "User Private Library & Collections Management"

**Obiettivo**: User può gestire collezioni personali e aggiungere giochi con PDF privati

| Issue | Titolo | Area | SP | Priority |
|-------|--------|------|:--:|:--------:|
| Issue D | User Collection Dashboard | Frontend | 5 | 🔴 Critical |
| Issue E | Add Game to Collection Wizard | Frontend | 5 | 🔴 Critical |
| Issue F | UserLibraryEntry PDF Association | Backend | 3 | 🔴 Critical |
| Issue G | Private PDF Upload Endpoint | Backend | 3 | 🔴 Critical |

**Totale**: 4 issues, 16 SP

**Rationale**:
- Focus esclusivo su user-facing collection management
- Separa chiaramente logica privata (user) da condivisa (admin)
- Dimensione epic equilibrata

### Epic 2 (ESTENDI #3386): "Agent Creation & Testing Flow"

**Obiettivo**: User/Admin può creare agenti, testare, e visualizzare storia chat

#### Issue Esistenti (già in epic)
11 issues, 45 SP (come da Epic #3386 attuale)

#### Issue NUOVE da Aggiungere
| Issue | Titolo | Area | SP | Priority |
|-------|--------|------|:--:|:--------:|
| Issue H | Chat Session Persistence Service | Backend | 5 | 🟠 High |
| Issue I | Chat History Integration | Frontend | 3 | 🟠 High |

**Totale Estensione**: 2 issues, 8 SP
**Epic Totale**: 13 issues, 53 SP

**Rationale**:
- Epic #3386 già focalizzata su agent/chat workflows
- Issue H-I completano naturalmente il flusso chat esistente
- Mantiene coerenza tematica epic

### Epic 3 (ESTENDI #3306): "Dashboard Hub & Game Management"

**Obiettivo**: Dashboard unificato per user/admin con gestione giochi completa

#### Issue Esistenti (già in epic)
8 issues, 21 SP (6 completate, 2 aperte)

#### Issue NUOVE da Aggiungere
| Issue | Titolo | Area | SP | Priority |
|-------|--------|------|:--:|:--------:|
| Issue A | Admin Wizard - Publish to Shared Library | Frontend | 3 | 🔴 Critical |
| Issue B | SharedGameCatalog Publication Workflow | Backend | 5 | 🔴 Critical |
| Issue C | Game Approval Status Management UI | Frontend | 2 | 🟠 High |

**Totale Estensione**: 3 issues, 10 SP
**Epic Totale**: 11 issues, 31 SP

**Rationale**:
- Epic #3306 è punto di partenza per ENTRAMBI i flussi (user + admin)
- Issue A-C completano workflow admin → shared library
- Evita proliferazione epic per issue strettamente correlate

## Execution Plan - 3 Weeks with Parallelization

### Week 1 - Fondazioni (Sequential, BLOCKER)

**Prerequisiti per entrambi i flussi**:

| Issue | Titolo | Area | SP | Dependencies |
|-------|--------|------|:--:|--------------|
| #3324 | SSE Infrastructure | Backend | 5 | None |
| #3370 | usePdfProcessingProgress hook | Frontend | 2 | #3324 |

**Totale**: 7 SP, 5-7 giorni
**Critical Path**: SSE blocca progress real-time per PDF e chat

### Week 2 - Parallel Streams (Efficiency Gain ~60%)

#### Stream A - Admin Flow (Parallel)
**Frontend Track**:
- Issue A: Admin Wizard - Publish (3 SP)
- Issue C: Approval Status UI (2 SP)

**Backend Track** (parallelo):
- Issue B: Publication Workflow (5 SP)

**Totale Stream**: 10 SP, parallelizzati → 5 giorni

#### Stream B - User Collection (Parallel)
**Frontend Track**:
- Issue D: Collection Dashboard (5 SP)
- Issue E: Add Game Wizard (5 SP)

**Backend Track** (parallelo):
- Issue F: UserLibraryEntry PDF (3 SP)
- Issue G: Private PDF Upload (3 SP)

**Totale Stream**: 16 SP, parallelizzati → 8 giorni

#### Stream C - Agent Foundation (Parallel)
**Frontend Track**:
- #3376: Agent Creation Wizard (5 SP)
- #3375: Agent Session Launch (3 SP)

**Totale Stream**: 8 SP → 8 giorni

**Week 2 Summary**:
- Sequential: 34 SP → 34 giorni
- Parallel (3 streams): 34 SP → ~8 giorni
- **Time Saving**: ~70%

### Week 3 - Integration & Chat (Dependent on Week 2)

**Backend Track** (parallel):
- Issue H: Chat Session Persistence (5 SP)

**Frontend Track** (parallel):
- Issue I: Chat History Integration (3 SP)

**Totale**: 8 SP, parallelizzati → 5 giorni

**Dependencies**:
- Requires Stream B (Collection) + Stream C (Agent) completati
- Integration finale dei due flussi

## Timeline Summary

| Phase | Duration | SP | Parallelization | Efficiency Gain |
|-------|----------|:--:|:---------------:|:---------------:|
| Week 1 | 5-7 days | 7 | Sequential (blocker) | N/A |
| Week 2 | 8-10 days | 34 | 3 streams | ~70% |
| Week 3 | 5-7 days | 8 | 2 tracks | ~40% |
| **TOTAL** | **18-24 days** | **49** | **Optimized** | **~65% overall** |

**Traditional Sequential**: ~49 days (1 SP/day)
**Parallel Optimized**: ~21 days (2.3 SP/day)
**Time Saved**: ~28 days (~4 weeks)

## Risks & Mitigation

### Risk 1: SSE Infrastructure (#3324) Delays
**Impact**: Blocca entrambi i flussi (PDF progress + chat streaming)
**Probability**: Medium
**Mitigation**:
- Priorità massima Week 1
- Fallback: Polling meccanismo temporaneo

### Risk 2: Collection Backend Complexity (Issue F-G)
**Impact**: Può estendersi oltre 6 SP stimati
**Probability**: Medium
**Mitigation**:
- Spike tecnico 2h per validare schema UserLibraryEntry
- Test DB migrations in dev environment

### Risk 3: Epic Proliferation Resistance
**Impact**: Team potrebbe preferire issue standalone vs nuova epic
**Probability**: Low
**Mitigation**:
- Epic "User Collections" è ben definita (4 issue, focus chiaro)
- Alternativa: Tag "user-collections" su issue senza epic

### Risk 4: Frontend Wizard Complexity (Issue E)
**Impact**: Multi-step wizard richiede state management avanzato
**Probability**: Medium
**Mitigation**:
- Riuso pattern da #3376 Agent Wizard (già definito)
- Zustand store per wizard context

## Definition of Done

### FLUSSO 1 - Admin (Issue A-C)
- [ ] Admin può creare gioco da dashboard personale
- [ ] Wizard permette upload PDF principale
- [ ] Gioco viene pubblicato in SharedGameCatalog
- [ ] Stato approvazione visualizzabile e gestibile
- [ ] Test E2E: wizard completo → gioco in catalog

### FLUSSO 2 - User (Issue D-I)
- [ ] User può visualizzare collezioni personali
- [ ] Wizard permette aggiunta gioco con PDF privato
- [ ] PDF associato correttamente a UserLibraryEntry
- [ ] User può creare chat con agente sul gioco
- [ ] Chat salvata automaticamente in history
- [ ] Test E2E: add game → upload PDF → chat → history

### Cross-Cutting
- [ ] SSE infrastructure funzionante (#3324)
- [ ] Progress PDF real-time visualizzato (#3370)
- [ ] Parallelizzazione backend/frontend verificata
- [ ] Documentation aggiornata (sequenza.md, roadmap.md)

## Next Steps

1. **Brainstorm Epic Nuova** (5 min):
   - Usare `/sc:brainstorm` per definire Epic "User Private Library & Collections"
   - Confermare scope e issue D-G

2. **Creare Epic su GitHub** (10 min):
   - Epic nuova con Issue D-G
   - Estendere Epic #3386 con Issue H-I
   - Estendere Epic #3306 con Issue A-C

3. **Creare Issue su GitHub** (30 min):
   - 9 issue nuove con dettagli tecnici
   - Link alle epic appropriate
   - Assign priority tags

4. **Aggiornare sequenza.md** (15 min):
   - Consolidare Week 2 con Issue A-I
   - Aggiungere Week 3 per Integration & Chat
   - Update tabella stato con nuove issue

5. **Sync con User** (5 min):
   - Presentare piano
   - Confermare priorità flussi
   - Approval per procedere

## Success Metrics

### Coverage
- FLUSSO 1: 75% → 100% (+25%)
- FLUSSO 2: 40% → 100% (+60%)

### Efficiency
- Parallelization: 3 concurrent streams Week 2
- Time-to-completion: ~3 weeks vs 7 weeks sequential (~60% faster)

### Epic Health
- Epic nuova: 4 issues, 16 SP (focused, manageable)
- Epic estese: +10 SP (#3306), +8 SP (#3386)
- No epic > 60 SP (mantiene dimensioni gestibili)
