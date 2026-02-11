# Terminal B - TODO Checklist

> **Tracking**: 44 issue totali
> **Progress**: 0/44 (0%)
> **Aggiornato**: 2026-02-11, 20:30

---

## Fase 1: Fondamenta Critiche (10 giorni)

### Backend Multi-Agent & Core Services

- [ ] **#4082** - Agent: Backend Multi-Agent per Game Support (L, 2d)
  - Backend: AgentDefinition table, multi-agent per game support
  - API: POST /agents, GET /games/{id}/agents
  - Dependencies: None

- [ ] **#4094** - Agent: Default POC Strategy Implementation (M, 1d)
  - Backend: Default RetrievalOnly strategy auto-assignment
  - Strategy selection logic
  - Dependencies: #4082

- [ ] **#4095** - Agent: Tier Limit Enforcement (M, 1d)
  - Backend: Token quota tracking per tier
  - Rate limiting middleware
  - Dependencies: #4082

- [ ] **#4085** - Agent: Chat UI Base Component (XL, 3d)
  - Frontend: Base chat interface with SSE
  - Message bubbles, input, typing indicator
  - Dependencies: #4082

- [ ] **#4106** - PDF: 7-State Embedding Pipeline (L, 2d)
  - Backend: Pipeline state machine
  - Status transitions: Pending → Uploading → Extracting → Chunking → Embedding → Completed/Failed
  - Dependencies: None

- [ ] **#3708** - Agent: AgentDefinition Data Model (M, 1d)
  - Backend: AgentDefinition entity with strategies
  - DB migration
  - Dependencies: #4082

**Checkpoint #1**: ✅ Tutte le issue Fase 1 complete

---

## Fase 2: Agent Chat + Strategy System (10 giorni)

### Strategy & Persistence

- [ ] **#4083** - Agent: Strategy System (Base/Config/Custom) (XL, 3d)
  - Backend: Strategy CRUD, base strategies
  - Frontend: Strategy selector UI
  - Dependencies: #4082

- [ ] **#4086** - Agent: Chat Persistence (Hybrid Sync) (L, 2d)
  - Frontend: localStorage + API sync
  - Session recovery logic
  - Dependencies: #4085

- [ ] **#4084** - Agent: Semi-Auto Creation Flow (M, 1d)
  - Frontend: Agent creation wizard
  - Game selection + strategy config
  - Dependencies: #4082, #4083

- [ ] **#4096** - Agent: Chat Context (KB Integration) (L, 2d)
  - Frontend: KB status in chat
  - Source citations display
  - Dependencies: #4085, #4086

- [ ] **#4107** - PDF: Manual Retry + Error Handling (M, 1d)
  - Frontend: Retry button UI
  - Error state display
  - Dependencies: #4106

- [ ] **#4089** - MeepleCard: Agent Type (backend support) (M, 1d)
  - Backend: Agent entity type support
  - Frontend: Agent badge in MeepleCard
  - Dependencies: #4082

**Checkpoint #2**: ✅ Tutte le issue Fase 2 complete

---

## Fase 3: Agent Views + Multi-Agent Foundation (9.5 giorni)

### Chat & Agent Management UI

- [ ] **#4087** - Agent: Chat History Page (Timeline + Filters) (L, 2d)
  - Frontend: Timeline view with filters
  - Session list + search
  - Dependencies: #4086

- [ ] **#4088** - Agent: Resume Chat (All Methods) (M, 1d)
  - Frontend: Resume from history/notification/game page
  - Context restoration
  - Dependencies: #4086, #4087

- [ ] **#4090** - Agent: Agent List Page /agents (M, 1d)
  - Frontend: Grid/list of user's agents
  - Filter by status/game
  - Dependencies: #4082

- [ ] **#4091** - Agent: Dashboard Widget Your Agents (S, 0.5d)
  - Frontend: Top 3 recent agents widget
  - Quick access links
  - Dependencies: #4082, #4090

- [ ] **#4092** - Agent: Game Page Agent Section (M, 1d)
  - Frontend: Agent card on game detail page
  - Start chat button
  - Dependencies: #4082

### Multi-Agent AI - Decisore

- [ ] **#3769** - Decisore: Strategic Analysis Engine (M, 1d)
  - Backend: Game state evaluation
  - Strategic analysis algorithms
  - Dependencies: #4082

- [ ] **#3770** - Decisore: Move Suggestion Algorithm (M, 1d)
  - Backend: Move prioritization ranking
  - Confidence scoring
  - Dependencies: #3769

- [ ] **#3772** - Decisore: Game State Parser (M, 1d)
  - Backend: Context assembly from game state
  - Parser for common board game formats
  - Dependencies: #3769

### AI Insights

- [ ] **#3916** - Backend: AI Insights Service (RAG Integration) (M, 1d)
  - Backend: RAG-based insights generation
  - Recommendation engine
  - Dependencies: None

**Checkpoint #3**: ✅ Tutte le issue Fase 3 complete

---

## Fase 4: Strategy Builder + Multi-Agent Core (10 giorni)

### Advanced Agent Features

- [ ] **#4093** - Agent: Strategy Builder UI (XL, 3d)
  - Frontend: Visual strategy configuration
  - Parameter sliders, model selection
  - Dependencies: #4083

- [ ] **#3771** - Decisore: Multi-Model Ensemble (M, 1d)
  - Backend: Multiple LLM consensus
  - Vote aggregation logic
  - Dependencies: #3769, #3770

- [ ] **#3773** - Decisore: REST API Endpoint (M, 1d)
  - Backend: POST /api/v1/agents/decisore/suggest
  - Input validation
  - Dependencies: #3769, #3770, #3772

- [ ] **#3774** - Decisore: Performance Tuning <10s (M, 1d)
  - Backend: Query optimization
  - Caching strategy
  - Dependencies: #3773

- [ ] **#3776** - Multi-Agent: Orchestration & Routing (M, 1d)
  - Backend: Agent coordination logic
  - Request routing by context
  - Dependencies: #4082

- [ ] **#3777** - Multi-Agent: Switching Logic & Context (M, 1d)
  - Backend: Context preservation on switch
  - Agent transition handling
  - Dependencies: #3776

- [ ] **#3809** - Agent: Builder Form & CRUD (M, 1d)
  - Frontend: Agent creation/edit forms
  - Validation rules
  - Dependencies: #3708

- [ ] **#3120** - Private Games & Catalog Proposal System (M, 1d)
  - Backend: Proposal workflow
  - Frontend: Already done in #4054 ✅
  - Dependencies: None

**Checkpoint #4**: ✅ Tutte le issue Fase 4 complete

---

## Fase 5: AI Backend + Testing + Infra (16 giorni)

### AI Platform Testing

- [ ] **#3874** - Arbitro: Performance Benchmark Tests (M, 1d)
  - Backend: Load testing for Arbitro agent
  - Performance metrics
  - Dependencies: None

- [ ] **#3779** - E2E Testing Suite - All Agent Workflows (L, 2d)
  - Frontend: Comprehensive E2E tests
  - All agent interaction flows
  - Dependencies: #4085, #4086, #4087

- [ ] **#3780** - Complete Documentation & User Guide (M, 1d)
  - Docs: Agent system user guide
  - API documentation
  - Dependencies: Tutte Agent

### RAG Enhancement

- [ ] **#3358** - Iterative RAG Strategy (L, 2d)
  - Backend: Multi-step retrieval
  - Refinement loop
  - Dependencies: None

### E2E Testing

- [ ] **#3082** - Missing E2E Test Flows (50 flows) (L, 2d)
  - Frontend: 50 user journey tests
  - Full app coverage
  - Dependencies: Varie

### Infrastructure (Epic #2967)

- [ ] **#2968** - Oracle Cloud Setup & VM Provisioning (M, 1d)
  - Infra: Oracle Cloud Free Tier VM
  - Network configuration
  - Dependencies: None

- [ ] **#2969** - GitHub Actions Runner Installation (M, 1d)
  - Infra: Self-hosted runner setup
  - Docker configuration
  - Dependencies: #2968

- [ ] **#2970** - Workflow Migration to Self-Hosted (M, 1d)
  - Infra: Move CI/CD to self-hosted
  - Update GitHub Actions YAML
  - Dependencies: #2969

- [ ] **#2972** - Performance Monitoring & Reliability (M, 1d)
  - Infra: Uptime monitoring
  - Performance metrics
  - Dependencies: #2968

- [ ] **#2973** - Cost Validation GitHub Billing (M, 1d)
  - Infra: Billing analysis
  - Cost tracking
  - Dependencies: #2970

- [ ] **#3367** - Log Aggregation System (L, 2d)
  - Infra: Centralized logging
  - Log retention policy
  - Dependencies: #2968

- [ ] **#3368** - k6 Load Testing Infrastructure (M, 1d)
  - Infra: k6 setup
  - Load test scenarios
  - Dependencies: #2968

**Checkpoint #5**: ✅ Tutte le issue Fase 5 complete

---

## Fase 6: Infrastructure Completion (3 giorni)

### Monitoring & Maintenance

- [ ] **#2974** - Setup Monitoring (Prometheus + Grafana) (M, 1d)
  - Infra: Prometheus scraping
  - Grafana dashboards
  - Dependencies: #2968

- [ ] **#2975** - Document Troubleshooting Procedures (M, 1d)
  - Docs: Runbooks for common issues
  - Incident response guide
  - Dependencies: Infra completa

- [ ] **#2976** - Maintenance Schedule Automation (M, 1d)
  - Infra: Automated backup/cleanup
  - Health checks
  - Dependencies: #2974

**Checkpoint #6 - FINALE**: ✅ Terminal B completo

---

## Progress Tracking

### By Epic

- [ ] **Epic #4069 - Agent System** (15 issue)
  - Fase 1: 4/15 (27%)
  - Fase 2: 6/15 (40%)
  - Fase 3: 5/15 (33%)

- [ ] **Epic #3490 - Multi-Agent AI** (15 issue)
  - Fase 3: 3/15 (20%)
  - Fase 4: 4/15 (27%)
  - Fase 5: 2/15 (13%)
  - Fase 6: 1/15 (7%)

- [ ] **Epic #2967 - Infrastructure** (10 issue)
  - Fase 5: 7/10 (70%)
  - Fase 6: 3/10 (30%)

- [ ] **Altri** (4 issue)
  - RAG, Testing, Documentation

### By Priority

- [ ] **P0-Critical** (7 issue): #4082, #4094, #4095, #4085, #4106
- [ ] **P1-High** (8 issue): #4086, #4096, #4087, #4090, etc.
- [ ] **P2-Medium** (18 issue): #4091, #4093, #3874, etc.
- [ ] **P3-Low/Unlabeled** (11 issue): Infrastructure, docs

### Overall Progress

```
[░░░░░░░░░░░░░░░░░░░░] 0/44 (0%)
```

**Estimated Completion**: ~12 settimane (3 mesi) se 1 developer full-time

---

*Generato: 2026-02-11 | Aggiorna checkbox dopo ogni /implementa*
