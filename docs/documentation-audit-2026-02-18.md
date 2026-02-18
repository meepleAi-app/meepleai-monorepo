# Documentation Audit Report - 2026-02-18

**Scope**: Consolidamento documentazione, gap codice-docs, rimozione ridondanze

---

## Executive Summary

| Metrica | Valore |
|---------|--------|
| **Directory docs attive** | 12 (+ archive, pdca, roadmap, evaluation-reports, templates, migrations) |
| **File .md totali in docs/** | ~150+ |
| **Link rotti nei README/INDEX** | 40+ (tutte le referenze con path numerati) |
| **Bounded contexts nel codice** | 12 |
| **Bounded contexts documentati** | 10 (mancano 2) |
| **RAG docs (file)** | 90 |
| **RAG variants duplicati** | 12 coppie (numbered + unnumbered) |
| **File INDEX duplicato con README** | 1 (docs/INDEX.md = ~80% overlap con docs/README.md) |
| **Health Score Documentazione** | 5.5/10 |

---

## 1. PROBLEMA CRITICO: Path Rotti

### Struttura Reale vs Referenziata

I file `docs/README.md`, `docs/INDEX.md` e `CLAUDE.md` referenziano **path con prefisso numerico** che **non esistono**:

| Path Referenziato | Path Reale | Status |
|-------------------|------------|--------|
| `docs/01-architecture/` | `docs/architecture/` | ROTTO |
| `docs/02-development/` | `docs/development/` | ROTTO |
| `docs/03-api/` | `docs/api/` | ROTTO |
| `docs/04-deployment/` | `docs/deployment/` | ROTTO |
| `docs/05-testing/` | `docs/testing/` | ROTTO |
| `docs/06-security/` | `docs/security/` | ROTTO |
| `docs/07-frontend/` | `docs/frontend/` | ROTTO |
| `docs/08-infrastructure/` | **NON ESISTE** | MANCANTE |
| `docs/09-bounded-contexts/` | `docs/bounded-contexts/` | ROTTO |
| `docs/10-user-guides/` | `docs/user-guides/` | ROTTO |
| `docs/11-user-flows/` | **NON ESISTE** | MANCANTE |
| `docs/quality/` | **NON ESISTE** | MANCANTE |

**Impatto**: Tutta la navigazione nei README e INDEX e' rotta. Nessun link funziona.

### File Referenziati ma Inesistenti
- `docs/ROADMAP-GUIDE.md` - referenziato in README.md:48
- `docs/DEVELOPMENT-ROADMAP.html` - referenziato in README.md:12
- `docs/02-development/coding-standards.md` - referenziato in CLAUDE.md:186
- `docs/02-development/operational-guide.md` - referenziato in INDEX.md:55
- `docs/02-development/guida-visualcode.md` - referenziato in INDEX.md:52

---

## 2. GAP Codice-Documentazione

### 2.1 Bounded Contexts: 12 nel codice, 10 documentati

**Nel codice** (`apps/api/src/Api/BoundedContexts/`):

| # | Context | Documentato? | File docs |
|---|---------|-------------|-----------|
| 1 | Administration | ✅ | `administration.md` |
| 2 | Authentication | ✅ | `authentication.md` |
| 3 | **BusinessSimulations** | **NO** | MANCANTE |
| 4 | DocumentProcessing | ✅ | `document-processing.md` |
| 5 | **Gamification** | **NO** | MANCANTE |
| 6 | GameManagement | ✅ | `game-management.md` |
| 7 | KnowledgeBase | ✅ | `knowledge-base.md` |
| 8 | SessionTracking | ✅ (file esiste) | `session-tracking.md` (non nel README) |
| 9 | SharedGameCatalog | ✅ | `shared-game-catalog.md` |
| 10 | SystemConfiguration | ✅ | `system-configuration.md` |
| 11 | UserLibrary | ✅ | `user-library.md` |
| 12 | UserNotifications | ✅ | `user-notifications.md` |
| - | WorkflowIntegration | ✅ | `workflow-integration.md` |

**Gap**:
- `BusinessSimulations`: Context nel codice, zero documentazione
- `Gamification`: Context nel codice (Achievements, Badges, Leaderboards), zero documentazione
- `SessionTracking`: File esiste ma non listato nel README bounded-contexts (dice "10 contexts")
- CLAUDE.md dice "11 contexts" ma ne lista solo 11 (manca BusinessSimulations e Gamification)

### 2.2 RAG: Documentazione > Implementazione

Il sistema RAG e' documentato come architettura TOMAC-RAG a 6 layer, ma solo il **POC (~55%)** e' implementato:

| Layer | Documentato | Implementato |
|-------|-------------|--------------|
| L1 Routing (3D) | ✅ Dettagliato | **NO** |
| L2 Semantic Cache | ✅ Dettagliato | Parziale (Redis) |
| L3 Retrieval | ✅ Dettagliato | ✅ (Hybrid search POC) |
| L4 CRAG Evaluation | ✅ Dettagliato | **NO** |
| L5 Generation | ✅ Dettagliato | ✅ (HybridLlmService) |
| L6 Validation | ✅ Dettagliato | **NO** |
| Plugin System | ✅ Estensivo (11 file) | **NO** |

### 2.3 Frontend: Componenti Non Documentati

| Area Frontend | Componenti nel Codice | Documentati |
|---------------|----------------------|-------------|
| Admin dashboard | 50+ componenti | Parziale (admin-dashboard-guide.md) |
| Session toolkit | 14 componenti | ✅ (README nel codice) |
| Library management | 35+ componenti | NO |
| Accessible components | 4 componenti WCAG | NO (solo README nel codice) |
| AI components | budget-badge | NO |

### 2.4 MeepleCard: Referenza Rotta in CLAUDE.md

CLAUDE.md linea 329 referenzia:
- `docs/frontend/components/meeple-card.md` - **NON ESISTE** a quel path
- `docs/design-system/cards.md` - **NON ESISTE**

I doc MeepleCard esistono in: `docs/frontend/meeple-card-v2-design-tokens.md`

---

## 3. RIDONDANZE IDENTIFICATE

### 3.1 docs/README.md vs docs/INDEX.md (~80% overlap)

Entrambi servono come indice principale con struttura diversa:
- **README.md** (291 righe): Organizzato per ruolo (Developer, Architect, DevOps, QA)
- **INDEX.md** (322 righe): Organizzato per risorsa (ADR, API, Bounded Contexts)

**Raccomandazione**: Consolidare in un solo README.md, eliminare INDEX.md.

### 3.2 CLAUDE.md vs docs/development/README.md (~70% overlap)

| Sezione | Righe in CLAUDE.md | Righe in development/README.md | Overlap |
|---------|-------------------|-------------------------------|---------|
| Architecture/CQRS | 20 | 25 | 100% (identico) |
| Quick Start | 15 | 20 | 90% |
| Secret Management | 16 | 30+ | 80% |
| Code Standards C# | 8 | 25 | 70% |
| Code Standards TS | 8 | 20 | 65% |
| Common Commands | 15 | 30 | 60% |
| Git Workflow | 20 | in git-workflow.md (259) | 60% |

**Raccomandazione**: CLAUDE.md deve essere conciso (quick reference). Spostare dettagli a docs/ con link.

### 3.3 RAG Variants: 12 Coppie Duplicate

File numerati e file con nome identico che coesistono:

| Numerato | Duplicato | Contenuto |
|----------|-----------|-----------|
| `01-semantic-cache.md` | `semantic-cache.md` | Stesso |
| `02-contextual-embeddings.md` | `contextual-embeddings.md` | Stesso |
| `03-metadata-filtering.md` | `metadata-filtering.md` | Stesso |
| `04-cross-encoder-reranking.md` | `cross-encoder-reranking.md` | Stesso |
| `05-hybrid-search.md` | `hybrid-search.md` | Stesso |
| `06-advanced-rag.md` | `advanced-rag.md` | Stesso |
| `07-sentence-window.md` | `sentence-window.md` | Stesso |
| `08-colbert-reranking.md` | `colbert-reranking.md` | Stesso |
| `09-cot-rag.md` | `chain-of-thought-rag.md` | Stesso |
| `10-query-decomposition.md` | `query-decomposition.md` | Stesso |
| `14-step-back-prompting.md` | `step-back-prompting.md` | Stesso |
| `15-query-expansion.md` | `query-expansion.md` | Stesso |

**Raccomandazione**: Eliminare i file numerati, tenere quelli con nome descrittivo.

### 3.4 RAG Entry Points: 4 File con ~60% Overlap

| File | Righe | Ruolo Dichiarato |
|------|-------|------------------|
| `README.md` | ~314 | Navigation hub |
| `00-overview.md` | ~131 | Executive summary |
| `HOW-IT-WORKS.md` | ~350 | Technical walkthrough |
| `SUMMARY.md` | ~192 | Session summary |

Tutti e 4 descrivono la stessa architettura a 6 layer con le stesse tabelle.

**Raccomandazione**: Unire README + 00-overview. Archiviare SUMMARY come storico.

### 3.5 RAG Plugin Docs: 11 File Non Implementati

`docs/api/rag/plugins/` contiene 11 file per un sistema plugin **non implementato nel codice**.

**Raccomandazione**: Spostare in `docs/api/rag/future/plugins/` con nota "roadmap".

### 3.6 Deployment: Possibile Ridondanza Interna

`docs/deployment/` ha 28 file tra cui:
- `deployment-cheatsheet.md` vs `deployment-quick-reference.md` (probabilmente simili)
- `docker-quickstart.md` vs `docs/development/docker/` (se esiste)
- `monitoring-quickstart.md` vs `monitoring-reference.md`
- `howto-secrets.md` vs `secrets-management.md`

**Raccomandazione**: Audit specifico di deployment/ per consolidare.

### 3.7 S3 Storage: 4 File in architecture/

- `s3-complete-guide.md`
- `s3-quickstart.md`
- `s3-storage-operations-runbook.md`
- `s3-storage-options.md`

Piu' la sezione S3 in CLAUDE.md. Possibile consolidamento in 1-2 file.

### 3.8 Dashboard Hub: 5+ File in frontend/

- `DASHBOARD-HUB-INDEX.md`
- `DASHBOARD-HUB-QUICK-REFERENCE.md`
- `dashboard-collection-centric-option-a.md`
- `dashboard-hub-implementation-plan.md`
- `dashboard-overview-hub.md`

Probabilmente storico da archiviare se il dashboard e' implementato.

---

## 4. FILE DA ARCHIVIARE/ELIMINARE

### 4.1 Candidati Archivio

| File | Motivo |
|------|--------|
| `docs/CLEANUP-SUMMARY.md` | Report storico completato |
| `docs/pdca/admin-dashboard/` | PDCA completato (final-summary.md) |
| `docs/api/rag/SUMMARY.md` | Summary di ricerca, non operativo |
| `docs/api/rag/DELIVERABLES.md` | Deliverables di ricerca |
| `docs/frontend/dashboard-*` (5 file) | Se dashboard implementato |
| `docs/evaluation-reports/baseline-2026-02.md` | Spostare in quality/ |

### 4.2 Candidati Eliminazione (duplicati puri)

| File da Eliminare | Duplica |
|-------------------|---------|
| `docs/INDEX.md` | `docs/README.md` (dopo consolidamento) |
| 12 RAG variant numerati | Versioni con nome (vedi 3.3) |
| `docs/api/rag/00-overview.md` | Merge in README.md |

### 4.3 Directory Mancanti da Creare o Rimuovere da Referenze

| Directory | Azione |
|-----------|--------|
| `docs/quality/` | Creare O rimuovere da README |
| `docs/infrastructure/` | Creare (merge da deployment?) O rimuovere da README |
| `docs/user-flows/` | Creare O rimuovere da README |

---

## 5. PIANO DI AZIONE

### Fase 1: Fix Critici (Path Rotti) - PRIORITA' MASSIMA

1. **Aggiornare docs/README.md**: Sostituire tutti i path `0X-nome/` con path reali (`nome/`)
2. **Eliminare docs/INDEX.md**: Consolidare contenuto unico in README.md
3. **Aggiornare CLAUDE.md**: Fix referenze rotte (bounded contexts count, MeepleCard path, coding-standards path)
4. **Rimuovere referenze a directory inesistenti** (08-infrastructure, 11-user-flows, quality)

### Fase 2: Colmare Gap Codice-Docs

5. **Creare `docs/bounded-contexts/business-simulations.md`** dal template
6. **Creare `docs/bounded-contexts/gamification.md`** dal template
7. **Aggiornare `docs/bounded-contexts/README.md`**: 10 -> 12 contexts, aggiungere SessionTracking alla tabella
8. **Aggiornare CLAUDE.md**: DDD Bounded Contexts da 11 a 12+, aggiungere BusinessSimulations e Gamification

### Fase 3: Rimuovere Ridondanze

9. **RAG variants**: Eliminare 12 file numerati duplicati
10. **RAG entry points**: Merge README + 00-overview, archiviare SUMMARY
11. **RAG plugins**: Spostare in `docs/api/rag/future/`
12. **Archiviare**: CLEANUP-SUMMARY.md, PDCA admin-dashboard completato
13. **Dashboard Hub docs**: Valutare se archiviare (5 file)

### Fase 4: Ottimizzazione

14. **Audit deployment/**: Consolidare cheatsheet/quickref/quickstart
15. **Audit S3 docs**: Consolidare 4 file in 1-2
16. **CLAUDE.md slim-down**: Rimuovere dettagli duplicati, lasciare quick-ref con link a docs/

---

## 6. STIMA IMPATTO

| Metrica | Prima | Dopo | Risparmio |
|---------|-------|------|-----------|
| Link rotti | 40+ | 0 | -100% |
| File RAG variants | 49 | ~37 | -12 file |
| File RAG totali | 90 | ~60 | -33% |
| File index duplicati | 2 | 1 | -1 file |
| Bounded contexts gap | 2 non documentati | 0 | +2 docs |
| CLAUDE.md overlap | ~70 righe duplicate | ~20 (con link) | -50 righe |
| Navigabilita' | Rotta | Funzionante | Critico |

---

**Generato**: 2026-02-18
**Tipo**: Audit Documentazione READ-ONLY + Piano di Azione
**Prossimo Step**: Approvazione utente per procedere con le fasi
