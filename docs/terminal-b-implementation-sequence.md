# Terminal B - Sequenza Implementazione Issue

> **Data**: 2026-02-11, 20:30
> **Focus**: Agent System Backend + Multi-Agent AI + Infrastructure
> **Comando**: `/implementa <Issue N>` per ogni issue in sequenza

---

## Fase 1: Fondamenta Critiche (Settimane 1-2)

### Issue Sequence Fase 1

```bash
# Issue 1 - Backend Multi-Agent per Game Support (P0-CRITICAL, 2d)
/implementa 4082 --base-branch main-dev --pr-target main-dev

# Issue 2 - Default POC Strategy Implementation (P0-CRITICAL, 1d)
/implementa 4094 --base-branch main-dev --pr-target main-dev

# Issue 3 - Tier Limit Enforcement (P0-CRITICAL, 1d)
/implementa 4095 --base-branch main-dev --pr-target main-dev

# Issue 4 - Chat UI Base Component (P0-CRITICAL, 3d)
/implementa 4085 --base-branch main-dev --pr-target main-dev

# Issue 5 - 7-State Embedding Pipeline (P0-CRITICAL, 2d)
/implementa 4106 --base-branch main-dev --pr-target main-dev

# Issue 6 - AgentDefinition Data Model (1d)
/implementa 3708 --base-branch main-dev --pr-target main-dev
```

**Durata Totale Fase 1**: ~10 giorni lavorativi

**Checkpoint Sync #1** (dopo completamento):
```bash
# Verificare integration con Terminal A
git checkout main-dev && git pull
pwsh -c "cd apps/web; pnpm test && pnpm typecheck && pnpm lint"

# Test di integrazione:
# - MeepleCard permission system funziona con Agent types
# - Chat UI base renderizza senza errori
# - PDF pipeline 7-state transizioni corrette
# - Agent backend crea/gestisce multi-agent per game
```

---

## Fase 2: Agent Chat + Strategy System (Settimane 3-4)

### Issue Sequence Fase 2

```bash
# Issue 1 - Strategy System (Base/Config/Custom) (XL, 3d)
/implementa 4083 --base-branch main-dev --pr-target main-dev

# Issue 2 - Chat Persistence (Hybrid Sync) (L, 2d)
/implementa 4086 --base-branch main-dev --pr-target main-dev

# Issue 3 - Semi-Auto Creation Flow (M, 1d)
/implementa 4084 --base-branch main-dev --pr-target main-dev

# Issue 4 - Chat Context (KB Integration) (L, 2d)
/implementa 4096 --base-branch main-dev --pr-target main-dev

# Issue 5 - PDF Manual Retry + Error Handling (M, 1d)
/implementa 4107 --base-branch main-dev --pr-target main-dev

# Issue 6 - MeepleCard Agent Type (backend support) (M, 1d)
/implementa 4089 --base-branch main-dev --pr-target main-dev
```

**Durata Totale Fase 2**: ~10 giorni lavorativi

**Checkpoint Sync #2** (dopo completamento):
```bash
git checkout main-dev && git pull
pwsh -c "cd apps/web; pnpm test && pnpm typecheck && pnpm lint"

# Test di integrazione:
# - Agent chat persistence salva/riprende conversazioni
# - KB integration nel chat context funziona
# - PDF retry re-triggera processing pipeline
# - Strategy system crea/configura strategie
# - Semi-auto creation flow completo
```

---

## Fase 3: Agent Views + Multi-Agent Foundation (Settimane 5-6)

### Issue Sequence Fase 3

```bash
# Issue 1 - Chat History Page (Timeline + Filters) (L, 2d)
/implementa 4087 --base-branch main-dev --pr-target main-dev

# Issue 2 - Resume Chat (All Methods) (M, 1d)
/implementa 4088 --base-branch main-dev --pr-target main-dev

# Issue 3 - Agent List Page /agents (M, 1d)
/implementa 4090 --base-branch main-dev --pr-target main-dev

# Issue 4 - Dashboard Widget Your Agents (S, 0.5d)
/implementa 4091 --base-branch main-dev --pr-target main-dev

# Issue 5 - Game Page Agent Section (M, 1d)
/implementa 4092 --base-branch main-dev --pr-target main-dev

# Issue 6 - Decisore Strategic Analysis Engine (M, 1d)
/implementa 3769 --base-branch main-dev --pr-target main-dev

# Issue 7 - Decisore Move Suggestion Algorithm (M, 1d)
/implementa 3770 --base-branch main-dev --pr-target main-dev

# Issue 8 - Decisore Game State Parser (M, 1d)
/implementa 3772 --base-branch main-dev --pr-target main-dev

# Issue 9 - Backend: AI Insights Service (RAG) (M, 1d)
/implementa 3916 --base-branch main-dev --pr-target main-dev
```

**Durata Totale Fase 3**: ~9.5 giorni lavorativi

**Checkpoint Sync #3** (dopo completamento):
```bash
git checkout main-dev && git pull
pwsh -c "cd apps/web; pnpm test && pnpm typecheck && pnpm lint"

# Test di integrazione:
# - Chat history timeline con filtri funziona
# - Resume chat riprende da qualsiasi punto
# - Agent list page mostra tutti gli agenti
# - Dashboard widget "Your Agents" funziona
# - Game page mostra sezione agente
# - Decisore strategic analysis engine funziona
# - AI Insights backend service operativo
```

---

## Fase 4: Strategy Builder + Multi-Agent Core (Settimane 7-8)

### Issue Sequence Fase 4

```bash
# Issue 1 - Strategy Builder UI (XL, 3d)
/implementa 4093 --base-branch main-dev --pr-target main-dev

# Issue 2 - Decisore Multi-Model Ensemble (M, 1d)
/implementa 3771 --base-branch main-dev --pr-target main-dev

# Issue 3 - Decisore REST API Endpoint (M, 1d)
/implementa 3773 --base-branch main-dev --pr-target main-dev

# Issue 4 - Decisore Performance Tuning <10s (M, 1d)
/implementa 3774 --base-branch main-dev --pr-target main-dev

# Issue 5 - Multi-Agent Orchestration & Routing (M, 1d)
/implementa 3776 --base-branch main-dev --pr-target main-dev

# Issue 6 - Agent Switching Logic & Context (M, 1d)
/implementa 3777 --base-branch main-dev --pr-target main-dev

# Issue 7 - Agent Builder Form & CRUD (M, 1d)
/implementa 3809 --base-branch main-dev --pr-target main-dev

# Issue 8 - Private Games & Catalog Proposal (M, 1d)
/implementa 3120 --base-branch main-dev --pr-target main-dev
```

**Durata Totale Fase 4**: ~10 giorni lavorativi

**Checkpoint Sync #4** (dopo completamento):
```bash
git checkout main-dev && git pull
pwsh -c "cd apps/web; pnpm test && pnpm typecheck && pnpm lint"

# Test di integrazione:
# - Strategy Builder UI crea/edita strategie visivamente
# - Decisore REST API risponde in <10s
# - Multi-agent orchestration routing funziona
# - Agent switching preserva contesto
# - Agent Builder form CRUD completo
```

---

## Fase 5: AI Backend + Testing + Infra (Settimane 9-12)

### Issue Sequence Fase 5

```bash
# AI Testing
/implementa 3874 --base-branch main-dev --pr-target main-dev  # Arbitro Performance Benchmark
/implementa 3779 --base-branch main-dev --pr-target main-dev  # E2E Testing Suite - All Agent
/implementa 3780 --base-branch main-dev --pr-target main-dev  # Documentation & User Guide

# RAG & Testing
/implementa 3358 --base-branch main-dev --pr-target main-dev  # Iterative RAG Strategy
/implementa 3082 --base-branch main-dev --pr-target main-dev  # Missing E2E Test Flows (50)

# Infrastructure (Epic #2967)
/implementa 2968 --base-branch main-dev --pr-target main-dev  # Oracle Cloud Setup & VM
/implementa 2969 --base-branch main-dev --pr-target main-dev  # GitHub Actions Runner Install
/implementa 2970 --base-branch main-dev --pr-target main-dev  # Workflow Migration to Self-Hosted
/implementa 2972 --base-branch main-dev --pr-target main-dev  # Performance Monitoring
/implementa 2973 --base-branch main-dev --pr-target main-dev  # Cost Validation GitHub Billing
/implementa 3367 --base-branch main-dev --pr-target main-dev  # Log Aggregation System
/implementa 3368 --base-branch main-dev --pr-target main-dev  # k6 Load Testing
```

**Durata Totale Fase 5**: ~16 giorni lavorativi

**Checkpoint Sync #5** (dopo completamento):
```bash
git checkout main-dev && git pull
pwsh -c "cd apps/web; pnpm test && pnpm typecheck && pnpm lint"

# Test di integrazione:
# - E2E test suite agent workflows green
# - 50 E2E test flows implementati
# - Oracle Cloud VM operativa
# - CI/CD su self-hosted runner
# - Log aggregation funzionante
# - k6 load tests eseguibili
```

---

## Fase 6: Infrastructure Completion + Documentation (Settimane 13-14)

### Issue Sequence Fase 6

```bash
# Monitoring & Maintenance
/implementa 2974 --base-branch main-dev --pr-target main-dev  # Setup Monitoring (Prometheus+Grafana)
/implementa 2975 --base-branch main-dev --pr-target main-dev  # Document Troubleshooting
/implementa 2976 --base-branch main-dev --pr-target main-dev  # Maintenance Schedule Automation
```

**Durata Totale Fase 6**: ~3 giorni lavorativi

**Checkpoint Sync #6 - Finale**:
```bash
git checkout main-dev && git pull
pwsh -c "cd apps/web; pnpm test && pnpm build && pnpm typecheck && pnpm lint"
pwsh -c "cd apps/web; pnpm test:e2e"

# Test finali:
# - Full build success
# - All unit tests green
# - All E2E tests green
# - Performance benchmarks met
# - Monitoring dashboards operational
# - Documentation complete
```

---

## Riepilogo Terminal B

| Fase | Issue Count | Giorni | Settimane |
|------|-------------|--------|-----------|
| Fase 1 (Fondamenta) | 6 | 10d | 2w |
| Fase 2 (Chat + Strategy) | 6 | 10d | 2w |
| Fase 3 (Views + Multi-Agent) | 9 | 9.5d | 2w |
| Fase 4 (Strategy Builder + MA Core) | 8 | 10d | 2w |
| Fase 5 (AI Backend + Infra) | 12 | 16d | 4w |
| Fase 6 (Completion) | 3 | 3d | 0.6w |
| **TOTALE** | **44** | **58.5d** | **~12w** |

---

## Comandi Rapidi

### Esecuzione Sequenza Completa
```bash
# Fase 1 (P0 Critical)
/implementa 4082 && /implementa 4094 && /implementa 4095 && /implementa 4085 && /implementa 4106 && /implementa 3708

# Fase 2 (Agent Chat)
/implementa 4083 && /implementa 4086 && /implementa 4084 && /implementa 4096 && /implementa 4107 && /implementa 4089

# Fase 3 (Agent Views)
/implementa 4087 && /implementa 4088 && /implementa 4090 && /implementa 4091 && /implementa 4092 && /implementa 3769 && /implementa 3770 && /implementa 3772 && /implementa 3916

# Fase 4 (Strategy + Core)
/implementa 4093 && /implementa 3771 && /implementa 3773 && /implementa 3774 && /implementa 3776 && /implementa 3777 && /implementa 3809 && /implementa 3120

# Fase 5 (Testing + Infra)
/implementa 3874 && /implementa 3779 && /implementa 3780 && /implementa 3358 && /implementa 3082 && /implementa 2968 && /implementa 2969 && /implementa 2970 && /implementa 2972 && /implementa 2973 && /implementa 3367 && /implementa 3368

# Fase 6 (Completion)
/implementa 2974 && /implementa 2975 && /implementa 2976
```

### Checkpoint dopo ogni Fase
```bash
# Sync con main-dev
git checkout main-dev && git pull

# Full validation
pwsh -c "cd apps/web; pnpm test && pnpm typecheck && pnpm lint"

# Verificare issue dependencies completate
gh issue list --state open --label "p0-critical" --limit 20
```

---

## Note Operative Terminal B

### Backend vs Frontend Split
Questo repository è **frontend-only**. Alcune issue richiedono backend work:

**⚠️ Issue che richiedono backend API** (da coordinare):
- #4082 Backend Multi-Agent per Game Support
- #4094 Default POC Strategy Implementation
- #4095 Tier Limit Enforcement
- #4106 7-State Embedding Pipeline
- #3708 AgentDefinition Data Model
- #4083 Strategy System
- #3769-3774 Decisore (tutti)
- #3776-3777 Multi-Agent Orchestration
- #3916 AI Insights Service
- #3358 Iterative RAG Strategy
- #2968-2976 Infrastructure (tutti)

**✅ Issue frontend-only**:
- #4085 Chat UI Base Component
- #4086 Chat Persistence (Hybrid Sync - localStorage)
- #4084 Semi-Auto Creation Flow
- #4096 Chat Context (KB Integration)
- #4107 PDF Manual Retry + Error Handling (UI)
- #4089 MeepleCard Agent Type (UI only)
- #4087 Chat History Page
- #4088 Resume Chat
- #4090 Agent List Page
- #4091 Dashboard Widget
- #4092 Game Page Agent Section
- #4093 Strategy Builder UI
- #3809 Agent Builder Form & CRUD (UI)
- #3120 Private Games & Catalog Proposal (UI)
- #3874 Arbitro Performance Benchmark (tests)
- #3779 E2E Testing Suite
- #3780 Documentation
- #3082 Missing E2E Test Flows

### Strategia Implementazione

**Opzione A - Frontend First** (raccomandato per questo repo):
1. Implementare tutte le issue frontend-only (~18 issue)
2. Creare issue placeholder/stub per backend dependencies
3. Mock API responses per testing
4. Coordinare con backend team per API implementation

**Opzione B - Bloccare su Dependencies**:
1. Implementare solo issue senza backend dependencies
2. Marcare le altre come "blocked" con label
3. Attendere backend completion

**Opzione C - Monorepo Completo** (se hai accesso):
1. Switchare al repo backend: `D:\Repositories\meepleai-monorepo-backend`
2. Implementare backend API per ogni issue
3. Tornare a frontend per UI integration

### Priorità Suggerita

**Alta priorità** (frontend-only, può essere fatto ora):
1. #4085 Chat UI Base Component
2. #4087 Chat History Page
3. #4090 Agent List Page
4. #4093 Strategy Builder UI
5. #3779 E2E Testing Suite
6. #3082 Missing E2E Test Flows

**Media priorità** (richiede backend, ma può usare mock):
1. #4086 Chat Persistence
2. #4088 Resume Chat
3. #4091 Dashboard Widget
4. #4092 Game Page Agent Section

**Bloccata** (richiede backend API completo):
1. #4082 Backend Multi-Agent
2. #4094 Default POC Strategy
3. #4095 Tier Limit Enforcement
4. #4106 7-State Embedding Pipeline
5. #3708 AgentDefinition Data Model
6. #4083 Strategy System
7. Tutte le issue Infrastructure (#2968-2976)

---

## Prossimi Passi Consigliati

### Scenario 1: Continua con Frontend-Only Issue

```bash
# Inizia con Chat UI Base Component (più alto impatto, frontend-only)
/implementa 4085
```

### Scenario 2: Coordinate Backend First

```bash
# Switch a backend repo (se disponibile)
cd D:\Repositories\meepleai-monorepo-backend

# Implementa backend API per #4082
/implementa 4082 --base-branch main-dev --pr-target main-dev

# Torna a frontend
cd D:\Repositories\meepleai-monorepo-frontend
/implementa 4085  # Ora può usare API reali
```

### Scenario 3: Mock-First Development

```bash
# Implementa UI con mock API
/implementa 4085  # Chat UI con mock responses
/implementa 4087  # Chat History con mock data
/implementa 4090  # Agent List con mock agents

# Quando backend ready, replace mock con API reali
```

---

**Generato**: 2026-02-11, 20:30
**Prossimo Aggiornamento**: Dopo ogni checkpoint sync
