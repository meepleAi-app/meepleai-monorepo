# MeepleAI - Roadmap Parallelo 2 Terminali

> **Data**: 2026-02-11 | **Issues Aperte**: 106 (91 work items + 15 epic containers)
> **Escluse**: #4062, #4065, #4064, #4063, #4054 (in lavorazione separata)
> **Stima Totale**: ~85-95 giorni lavorativi per terminale (~17-19 settimane)

---

## Architettura dei Terminali

| Terminale | Focus Primario | Focus Secondario |
|-----------|---------------|-----------------|
| **Terminal A** | MeepleCard, Navbar, UI Features, PDF Status | Frontend Agent views |
| **Terminal B** | Agent System Core, Multi-Agent AI, AI Platform, Infrastructure | Backend services |

Ogni terminale implementa **frontend + backend** per il proprio dominio.
Sync checkpoint ogni 2 settimane per merge, integration test e risoluzione dipendenze cross-stream.

---

## Legenda Stime

| Size | Ore | Giorni |
|------|-----|--------|
| XS | 2h | 0.25d |
| S | 4h | 0.5d |
| M | 8h | 1d |
| L | 16h | 2d |
| XL | 24h | 3d |
| Unlabeled | 8h | 1d (default) |

---

## Fase 1: Fondamenta Critiche (Settimane 1-2)

> **Obiettivo**: Implementare tutti gli item p0-critical che bloccano le feature successive

### Terminal A - MeepleCard Foundation + Navbar Core

| # | Issue | Titolo | Size | Giorni | Dipendenze |
|---|-------|--------|------|--------|------------|
| 1 | #4074 | MeepleCard - Permission System Integration | L | 2d | Nessuna |
| 2 | #4073 | MeepleCard - WCAG 2.1 AA Accessibility | M | 1d | Nessuna |
| 3 | #4072 | MeepleCard - Smart Tooltip Positioning | M | 1d | Nessuna |
| 4 | #4075 | MeepleCard - Tag System Vertical Layout | M | 1d | Nessuna |
| 5 | #4099 | Navbar - Dynamic Route / (Welcome vs Dashboard) | M | 1d | Nessuna |
| 6 | #4100 | Navbar - Anonymous Catalog Restrictions | S | 0.5d | #4099 |
| 7 | #4097 | Navbar - Dropdown Grouping Structure | M | 1d | Nessuna |
| 8 | #4098 | Navbar - Mobile Hamburger Menu | M | 1d | #4097 |

**Subtotale A Fase 1**: 8.5 giorni

### Terminal B - Agent System Foundation

| # | Issue | Titolo | Size | Giorni | Dipendenze |
|---|-------|--------|------|--------|------------|
| 1 | #4082 | Agent - Backend Multi-Agent per Game Support | L | 2d | Nessuna |
| 2 | #4094 | Agent - Default POC Strategy Implementation | M | 1d | Nessuna |
| 3 | #4095 | Agent - Tier Limit Enforcement | M | 1d | Nessuna |
| 4 | #4085 | Agent - Chat UI Base Component | XL | 3d | #4082 |
| 5 | #4106 | PDF - 7-State Embedding Pipeline | L | 2d | Nessuna |
| 6 | #3708 | AgentDefinition Data Model | M | 1d | #4082 |

**Subtotale B Fase 1**: 10 giorni

### Checkpoint Sync #1 (Fine Settimana 2)

```bash
# Entrambi i terminali:
git checkout main-dev && git pull
pwsh -c "cd apps/web; pnpm test && pnpm typecheck && pnpm lint"
# Verifiche:
# - MeepleCard permission system funziona con Agent types
# - Navbar dynamic routing non interferisce con Agent routes
# - Chat UI base non conflitto con Navbar restructuring
# - PDF pipeline compatibile con Agent KB integration
```

**Test di integrazione Checkpoint 1**:
- [ ] MeepleCard renderizza con permission system attivo
- [ ] Navbar mostra route dinamiche (anon vs auth)
- [ ] Chat UI base renderizza senza errori
- [ ] PDF pipeline 7-state transizioni corrette
- [ ] Agent backend crea/gestisce multi-agent per game
- [ ] Nessun conflitto CSS/layout tra MeepleCard e Chat UI

---

## Fase 2: Feature Core P1 - Parte 1 (Settimane 3-4)

> **Obiettivo**: Completare MeepleCard enhancements + Agent chat core + Navbar essentials

### Terminal A - MeepleCard Completion + Navbar Essentials

| # | Issue | Titolo | Size | Giorni | Dipendenze |
|---|-------|--------|------|--------|------------|
| 1 | #4078 | MeepleCard - Ownership State Logic | M | 1d | #4074 |
| 2 | #4079 | MeepleCard - Agent Type Support | M | 1d | #4074 |
| 3 | #4077 | MeepleCard - Collection Limits Management | L | 2d | #4074 |
| 4 | #4076 | MeepleCard - Mobile Tag Optimization | S | 0.5d | #4075 |
| 5 | #4080 | MeepleCard - Context-Aware Tests | M | 1d | #4074, #4078, #4079 |
| 6 | #4081 | MeepleCard - Performance Optimization | S | 0.5d | Tutti MeepleCard |
| 7 | #4103 | Navbar - Notifications Dropdown (Preview) | L | 2d | #4097 |
| 8 | #4101 | Navbar - Dual CTA (Accedi + Registrati) | XS | 0.25d | #4099 |
| 9 | #4113 | Notification System UI | M | 1d | Nessuna |

**Subtotale A Fase 2**: 9.25 giorni

### Terminal B - Agent Chat + Strategy System

| # | Issue | Titolo | Size | Giorni | Dipendenze |
|---|-------|--------|------|--------|------------|
| 1 | #4083 | Agent - Strategy System (Base/Config/Custom) | XL | 3d | #4082 |
| 2 | #4086 | Agent - Chat Persistence (Hybrid Sync) | L | 2d | #4085 |
| 3 | #4084 | Agent - Semi-Auto Creation Flow | M | 1d | #4082, #4083 |
| 4 | #4096 | Agent - Chat Context (KB Integration) | L | 2d | #4085, #4086 |
| 5 | #4107 | PDF - Manual Retry + Error Handling | M | 1d | #4106 |
| 6 | #4089 | MeepleCard - Agent Type (backend support) | M | 1d | #4082 |

**Subtotale B Fase 2**: 10 giorni

### Checkpoint Sync #2 (Fine Settimana 4)

```bash
git checkout main-dev && git pull
pwsh -c "cd apps/web; pnpm test && pnpm typecheck && pnpm lint"
```

**Test di integrazione Checkpoint 2**:
- [ ] MeepleCard mostra Agent type con icona corretta
- [ ] MeepleCard ownership state gestisce game in libreria
- [ ] Collection limits enforcement funziona end-to-end
- [ ] Agent chat persistence salva/riprende conversazioni
- [ ] KB integration nel chat context funziona
- [ ] Notification dropdown mostra preview
- [ ] PDF retry re-triggera processing pipeline
- [ ] Strategy system crea/configura strategie
- [ ] Semi-auto creation flow completo

---

## Fase 3: Feature Core P1 - Parte 2 (Settimane 5-6)

> **Obiettivo**: Chat avanzato + PDF real-time + Navbar notifications + Agent views

### Terminal A - Navbar Notifications + PDF UI + New UI Features

| # | Issue | Titolo | Size | Giorni | Dipendenze |
|---|-------|--------|------|--------|------------|
| 1 | #4104 | Navbar - Notifications Page (10 Types) | XL | 3d | #4103 |
| 2 | #4108 | PDF - Multi-Location Status UI | XL | 3d | #4106 |
| 3 | #4109 | PDF - Real-time Updates (SSE + Polling) | L | 2d | #4106, #4108 |
| 4 | #4117 | Achievement System Display UI | M | 1d | Nessuna |
| 5 | #4114 | Wishlist Management System UI | M | 1d | Nessuna |

**Subtotale A Fase 3**: 10 giorni

### Terminal B - Agent Views + Chat History + Multi-Agent Foundation

| # | Issue | Titolo | Size | Giorni | Dipendenze |
|---|-------|--------|------|--------|------------|
| 1 | #4087 | Agent - Chat History Page (Timeline + Filters) | L | 2d | #4086 |
| 2 | #4088 | Agent - Resume Chat (All Methods) | M | 1d | #4086, #4087 |
| 3 | #4090 | Agent - Agent List Page /agents | M | 1d | #4082 |
| 4 | #4091 | Agent - Dashboard Widget Your Agents | S | 0.5d | #4082, #4090 |
| 5 | #4092 | Agent - Game Page Agent Section | M | 1d | #4082 |
| 6 | #3769 | Decisore - Strategic Analysis Engine | M | 1d | #4082 |
| 7 | #3770 | Decisore - Move Suggestion Algorithm | M | 1d | #3769 |
| 8 | #3772 | Decisore - Game State Parser | M | 1d | #3769 |
| 9 | #3916 | Backend: AI Insights Service (RAG Integration) | M | 1d | Nessuna |

**Subtotale B Fase 3**: 9.5 giorni

### Checkpoint Sync #3 (Fine Settimana 6)

```bash
git checkout main-dev && git pull
pwsh -c "cd apps/web; pnpm test && pnpm typecheck && pnpm lint"
```

**Test di integrazione Checkpoint 3**:
- [ ] Notifications page mostra 10 tipi di notifica
- [ ] PDF multi-location UI mostra stato per servizio
- [ ] PDF SSE/polling aggiorna in real-time
- [ ] Chat history timeline con filtri funziona
- [ ] Resume chat riprende da qualsiasi punto
- [ ] Agent list page mostra tutti gli agenti
- [ ] Dashboard widget "Your Agents" funziona
- [ ] Game page mostra sezione agente
- [ ] Decisore strategic analysis engine funziona
- [ ] AI Insights backend service operativo
- [ ] Achievement display UI renderizza badges
- [ ] Wishlist management UI CRUD completo

---

## Fase 4: Feature P2 Enhancement (Settimane 7-8)

> **Obiettivo**: Navbar settings, PDF avanzato, Agent strategy builder, Decisore API

### Terminal A - Navbar Settings + PDF Polish + UI Remaining

| # | Issue | Titolo | Size | Giorni | Dipendenze |
|---|-------|--------|------|--------|------------|
| 1 | #4102 | Navbar - Settings Dropdown (8 Sections) | L | 2d | #4097 |
| 2 | #4105 | Navbar - Notifications Configuration | M | 1d | #4104 |
| 3 | #4110 | PDF - Duration Metrics & ETA | M | 1d | #4108 |
| 4 | #4111 | PDF - Notification Channel Config | S | 0.5d | #4109 |
| 5 | #4116 | 2FA Self-Service UI | M | 1d | Nessuna |
| 6 | #4115 | Play Records Actions UI | M | 1d | Nessuna |
| 7 | #4118 | User Bulk Operations UI | M | 1d | Nessuna |
| 8 | #3919 | Frontend: AI Insights Widget Component | M | 1d | #3916 |
| 9 | #3355 | Game/Document Version History UI | M | 1d | Nessuna |

**Subtotale A Fase 4**: 9.5 giorni

### Terminal B - Agent Strategy + Decisore Complete + Multi-Agent Core

| # | Issue | Titolo | Size | Giorni | Dipendenze |
|---|-------|--------|------|--------|------------|
| 1 | #4093 | Agent - Strategy Builder UI | XL | 3d | #4083 |
| 2 | #3771 | Decisore - Multi-Model Ensemble | M | 1d | #3769, #3770 |
| 3 | #3773 | Decisore - REST API Endpoint | M | 1d | #3769, #3770, #3772 |
| 4 | #3774 | Decisore - Performance Tuning <10s | M | 1d | #3773 |
| 5 | #3776 | Multi-Agent Orchestration & Routing | M | 1d | #4082 |
| 6 | #3777 | Agent Switching Logic & Context | M | 1d | #3776 |
| 7 | #3809 | Agent Builder Form & CRUD | M | 1d | #3708 |
| 8 | #3120 | Private Games & Catalog Proposal System | M | 1d | Nessuna |

**Subtotale B Fase 4**: 10 giorni

### Checkpoint Sync #4 (Fine Settimana 8)

```bash
git checkout main-dev && git pull
pwsh -c "cd apps/web; pnpm test && pnpm typecheck && pnpm lint"
```

**Test di integrazione Checkpoint 4**:
- [ ] Settings dropdown 8 sezioni funzionali
- [ ] Notifications configuration salva preferenze
- [ ] PDF duration metrics mostra ETA accurato
- [ ] 2FA self-service enrollment/disable
- [ ] Play records complete CRUD actions
- [ ] User bulk operations (select, action, confirm)
- [ ] AI Insights widget mostra suggerimenti RAG
- [ ] Strategy Builder UI crea/edita strategie visivamente
- [ ] Decisore REST API risponde in <10s
- [ ] Multi-agent orchestration routing funziona
- [ ] Agent switching preserva contesto
- [ ] Agent Builder form CRUD completo
- [ ] Version history UI mostra diff

---

## Fase 5: AI Platform & Advanced (Settimane 9-12)

> **Obiettivo**: AI Platform features, advanced agent tools, testing coverage

### Terminal A - AI Platform Frontend + Testing

| # | Issue | Titolo | Size | Giorni | Dipendenze |
|---|-------|--------|------|--------|------------|
| 1 | #3709 | Agent Builder UI | M | 1d | #3708, #3809 |
| 2 | #3710 | Agent Playground | M | 1d | #4085, #3709 |
| 3 | #3711 | Strategy Editor | M | 1d | #4083, #4093 |
| 4 | #3712 | Visual Pipeline Builder | L | 2d | #3708 |
| 5 | #3713 | Agent Catalog & Usage Stats | M | 1d | #4090, #3708 |
| 6 | #3714 | Chat Analytics | M | 1d | #4087 |
| 7 | #3715 | PDF Analytics | M | 1d | #4108 |
| 8 | #3716 | Model Performance Tracking | M | 1d | #3708 |
| 9 | #3717 | A/B Testing Framework | M | 1d | Nessuna |
| 10 | #3778 | Unified Multi-Agent Dashboard UI | M | 1d | #3776, #3777 |
| 11 | #3894 | EntityListView Test Coverage & Polish | M | 1d | Nessuna |
| 12 | #3775 | Decisore Beta Testing & Expert Validation | M | 1d | #3773 |
| 13 | #3763 | Arbitro Testing & User Feedback | M | 1d | Nessuna |

**Subtotale A Fase 5**: 14 giorni (3.5 settimane)

### Terminal B - AI Platform Backend + Testing + Infra Start

| # | Issue | Titolo | Size | Giorni | Dipendenze |
|---|-------|--------|------|--------|------------|
| 1 | #3874 | Arbitro Performance Benchmark Tests | M | 1d | Nessuna |
| 2 | #3779 | E2E Testing Suite - All Agent Workflows | L | 2d | #4085, #4086, #4087 |
| 3 | #3780 | Complete Documentation & User Guide | M | 1d | Tutte Agent |
| 4 | #3358 | Iterative RAG Strategy | L | 2d | Nessuna |
| 5 | #3082 | Missing E2E Test Flows (50 flows) | L | 2d | Varie |
| 6 | #2968 | Oracle Cloud Setup & VM Provisioning | M | 1d | Nessuna |
| 7 | #2969 | GitHub Actions Runner Installation | M | 1d | #2968 |
| 8 | #2970 | Workflow Migration to Self-Hosted | M | 1d | #2969 |
| 9 | #2972 | Performance Monitoring & Reliability | M | 1d | #2968 |
| 10 | #2973 | Cost Validation GitHub Billing | M | 1d | #2970 |
| 11 | #3367 | Log Aggregation System | L | 2d | #2968 |
| 12 | #3368 | k6 Load Testing Infrastructure | M | 1d | #2968 |

**Subtotale B Fase 5**: 16 giorni (4 settimane)

### Checkpoint Sync #5 (Fine Settimana 12)

```bash
git checkout main-dev && git pull
pwsh -c "cd apps/web; pnpm test && pnpm typecheck && pnpm lint"
```

**Test di integrazione Checkpoint 5**:
- [ ] Agent Builder UI crea agenti end-to-end
- [ ] Agent Playground testa strategie interattivamente
- [ ] Strategy Editor modifica visivamente configurazioni
- [ ] Visual Pipeline Builder costruisce pipeline drag-and-drop
- [ ] Agent Catalog mostra statistiche uso
- [ ] Chat Analytics mostra metriche conversazioni
- [ ] PDF Analytics mostra statistiche processing
- [ ] A/B Testing framework funzionale
- [ ] Multi-Agent Dashboard unificato operativo
- [ ] E2E test suite agent workflows green
- [ ] 50 E2E test flows implementati
- [ ] Oracle Cloud VM operativa
- [ ] CI/CD su self-hosted runner
- [ ] Log aggregation funzionante
- [ ] k6 load tests eseguibili

---

## Fase 6: Infrastructure & Polish (Settimane 13-14)

> **Obiettivo**: Infrastructure remaining, monitoring, maintenance, documentazione

### Terminal A - UI Polish + Final Testing

| # | Issue | Titolo | Size | Giorni | Dipendenze |
|---|-------|--------|------|--------|------------|
| 1 | #3718 | Testing - AI Platform (comprehensive) | L | 2d | Fase 5 completa |
| 2 | UI regression test suite | Tutti i componenti | - | 2d | Tutte le fasi |
| 3 | Accessibility audit finale | WCAG 2.1 AA | - | 1d | Tutte le fasi |

**Subtotale A Fase 6**: 5 giorni

### Terminal B - Infrastructure Completion

| # | Issue | Titolo | Size | Giorni | Dipendenze |
|---|-------|--------|------|--------|------------|
| 1 | #2974 | Setup Monitoring (Prometheus + Grafana) | M | 1d | #2968 |
| 2 | #2975 | Document Troubleshooting Procedures | M | 1d | Infra completa |
| 3 | #2976 | Maintenance Schedule Automation | M | 1d | #2974 |

**Subtotale B Fase 6**: 3 giorni

### Checkpoint Sync #6 - Finale (Fine Settimana 14)

```bash
git checkout main-dev && git pull
# Full regression test
pwsh -c "cd apps/web; pnpm test && pnpm build && pnpm typecheck && pnpm lint"
# E2E full suite
pwsh -c "cd apps/web; pnpm test:e2e"
```

**Test finali**:
- [ ] Full build success
- [ ] All unit tests green
- [ ] All E2E tests green
- [ ] WCAG 2.1 AA compliance verified
- [ ] Performance benchmarks met
- [ ] Monitoring dashboards operational
- [ ] Documentation complete

---

## Riepilogo per Issue

### Epic #1: MeepleCard Enhancements (#4068) - Terminal A Fase 1-2

| Issue | Titolo | Size | Fase | Terminal |
|-------|--------|------|------|----------|
| #4074 | Permission System Integration | L | 1 | A |
| #4073 | WCAG 2.1 AA Accessibility | M | 1 | A |
| #4072 | Smart Tooltip Positioning | M | 1 | A |
| #4075 | Tag System Vertical Layout | M | 1 | A |
| #4078 | Ownership State Logic | M | 2 | A |
| #4079 | Agent Type Support | M | 2 | A |
| #4077 | Collection Limits Management | L | 2 | A |
| #4076 | Mobile Tag Optimization | S | 2 | A |
| #4080 | Context-Aware Tests | M | 2 | A |
| #4081 | Performance Optimization | S | 2 | A |

**Totale**: 10 issues, ~12d

### Epic #2: Agent System (#4069) - Terminal B Fase 1-4

| Issue | Titolo | Size | Fase | Terminal |
|-------|--------|------|------|----------|
| #4082 | Backend Multi-Agent per Game | L | 1 | B |
| #4094 | Default POC Strategy | M | 1 | B |
| #4095 | Tier Limit Enforcement | M | 1 | B |
| #4085 | Chat UI Base Component | XL | 1 | B |
| #4083 | Strategy System | XL | 2 | B |
| #4086 | Chat Persistence | L | 2 | B |
| #4084 | Semi-Auto Creation Flow | M | 2 | B |
| #4096 | Chat Context KB Integration | L | 2 | B |
| #4089 | MeepleCard Agent Type | M | 2 | B |
| #4087 | Chat History Page | L | 3 | B |
| #4088 | Resume Chat | M | 3 | B |
| #4090 | Agent List Page | M | 3 | B |
| #4091 | Dashboard Widget | S | 3 | B |
| #4092 | Game Page Agent Section | M | 3 | B |
| #4093 | Strategy Builder UI | XL | 4 | B |

**Totale**: 15 issues, ~24d

### Epic #3: Navbar Restructuring (#4070) - Terminal A Fase 1-4

| Issue | Titolo | Size | Fase | Terminal |
|-------|--------|------|------|----------|
| #4099 | Dynamic Route / | M | 1 | A |
| #4100 | Anonymous Catalog Restrictions | S | 1 | A |
| #4097 | Dropdown Grouping Structure | M | 1 | A |
| #4098 | Mobile Hamburger Menu | M | 1 | A |
| #4103 | Notifications Dropdown | L | 2 | A |
| #4101 | Dual CTA | XS | 2 | A |
| #4104 | Notifications Page (10 Types) | XL | 3 | A |
| #4102 | Settings Dropdown (8 Sections) | L | 4 | A |
| #4105 | Notifications Configuration | M | 4 | A |

**Totale**: 9 issues, ~12d

### Epic #4: PDF Status Tracking (#4071) - Terminal A/B Fase 1-4

| Issue | Titolo | Size | Fase | Terminal |
|-------|--------|------|------|----------|
| #4106 | 7-State Embedding Pipeline | L | 1 | B |
| #4107 | Manual Retry + Error Handling | M | 2 | B |
| #4108 | Multi-Location Status UI | XL | 3 | A |
| #4109 | Real-time Updates (SSE + Polling) | L | 3 | A |
| #4110 | Duration Metrics & ETA | M | 4 | A |
| #4111 | Notification Channel Config | S | 4 | A |

**Totale**: 6 issues, ~9.5d

### New UI Features - Terminal A Fase 2-4

| Issue | Titolo | Size | Fase | Terminal |
|-------|--------|------|------|----------|
| #4118 | User Bulk Operations UI | M | 4 | A |
| #4117 | Achievement System Display UI | M | 3 | A |
| #4116 | 2FA Self-Service UI | M | 4 | A |
| #4115 | Play Records Actions UI | M | 4 | A |
| #4114 | Wishlist Management System UI | M | 3 | A |
| #4113 | Notification System UI | M | 2 | A |

**Totale**: 6 issues, ~6d

### Multi-Agent AI System (#3490) - Terminal B Fase 3-5

| Issue | Titolo | Size | Fase | Terminal |
|-------|--------|------|------|----------|
| #3769 | Decisore - Strategic Analysis Engine | M | 3 | B |
| #3770 | Decisore - Move Suggestion Algorithm | M | 3 | B |
| #3772 | Decisore - Game State Parser | M | 3 | B |
| #3771 | Decisore - Multi-Model Ensemble | M | 4 | B |
| #3773 | Decisore - REST API Endpoint | M | 4 | B |
| #3774 | Decisore - Performance Tuning <10s | M | 4 | B |
| #3776 | Multi-Agent Orchestration & Routing | M | 4 | B |
| #3777 | Agent Switching Logic & Context | M | 4 | B |
| #3809 | Agent Builder Form & CRUD | M | 4 | B |
| #3874 | Arbitro Performance Benchmark | M | 5 | B |
| #3763 | Arbitro Testing & User Feedback | M | 5 | A |
| #3775 | Decisore Beta Testing | M | 5 | A |
| #3778 | Unified Multi-Agent Dashboard UI | M | 5 | A |
| #3779 | E2E Testing Suite - All Agent | L | 5 | B |
| #3780 | Documentation & User Guide | M | 5 | B |

**Totale**: 15 issues, ~17d

### AI Platform (#3708-3717) - Terminal A/B Fase 5

| Issue | Titolo | Size | Fase | Terminal |
|-------|--------|------|------|----------|
| #3708 | AgentDefinition Data Model | M | 1 | B |
| #3709 | Agent Builder UI | M | 5 | A |
| #3710 | Agent Playground | M | 5 | A |
| #3711 | Strategy Editor | M | 5 | A |
| #3712 | Visual Pipeline Builder | L | 5 | A |
| #3713 | Agent Catalog & Usage Stats | M | 5 | A |
| #3714 | Chat Analytics | M | 5 | A |
| #3715 | PDF Analytics | M | 5 | A |
| #3716 | Model Performance Tracking | M | 5 | A |
| #3717 | A/B Testing Framework | M | 5 | A |

**Totale**: 10 issues, ~12d

### AI Insights & Other (#3902) - Terminal A/B Fase 3-5

| Issue | Titolo | Size | Fase | Terminal |
|-------|--------|------|------|----------|
| #3916 | Backend: AI Insights Service | M | 3 | B |
| #3919 | Frontend: AI Insights Widget | M | 4 | A |
| #3894 | EntityListView Test Coverage | M | 5 | A |
| #3355 | Version History UI | M | 4 | A |
| #3120 | Private Games & Catalog Proposal | M | 4 | B |
| #3358 | Iterative RAG Strategy | L | 5 | B |
| #3082 | Missing E2E Test Flows (50) | L | 5 | B |
| #3718 | Testing - AI Platform | L | 6 | A |

**Totale**: 8 issues, ~12d

### Infrastructure (#2967, #3366) - Terminal B Fase 5-6

| Issue | Titolo | Size | Fase | Terminal |
|-------|--------|------|------|----------|
| #2968 | Oracle Cloud Setup & VM | M | 5 | B |
| #2969 | GitHub Actions Runner Install | M | 5 | B |
| #2970 | Workflow Migration to Self-Hosted | M | 5 | B |
| #2972 | Performance Monitoring | M | 5 | B |
| #2973 | Cost Validation GitHub Billing | M | 5 | B |
| #3367 | Log Aggregation System | L | 5 | B |
| #3368 | k6 Load Testing | M | 5 | B |
| #2974 | Setup Monitoring (Prometheus+Grafana) | M | 6 | B |
| #2975 | Document Troubleshooting | M | 6 | B |
| #2976 | Maintenance Schedule Automation | M | 6 | B |

**Totale**: 10 issues, ~12d

---

## Grafico Timeline

```
Settimana:  1    2    3    4    5    6    7    8    9   10   11   12   13   14
            |----|----|----|----|----|----|----|----|----|----|----|----|----|----|

Terminal A: [=== Fase 1: MeepleCard + Navbar ===][= Fase 2: MC Complete + Nav =]
            [                                    ][  #4078 #4079 #4077 #4076   ]
            [ #4074 #4073 #4072 #4075            ][  #4080 #4081 #4103 #4101  ]
            [ #4099 #4100 #4097 #4098            ][  #4113                     ]
                                    |CP1|                              |CP2|

            [== Fase 3: Nav Notif + PDF UI ====][=== Fase 4: Settings + UI ====]
            [ #4104 #4108 #4109 #4117 #4114    ][ #4102 #4105 #4110 #4111     ]
            [                                  ][ #4116 #4115 #4118 #3919     ]
            [                                  ][ #3355                        ]
                                    |CP3|                              |CP4|

            [========= Fase 5: AI Platform Frontend + Testing =========][F6]
            [ #3709 #3710 #3711 #3712 #3713 #3714 #3715 #3716 #3717  ][ Tests]
            [ #3778 #3894 #3775 #3763                                  ][ A11y]
                                                               |CP5|   |CP6|


Terminal B: [==== Fase 1: Agent Foundation ====][== Fase 2: Agent Chat+Strategy =]
            [ #4082 #4094 #4095 #4085          ][ #4083 #4086 #4084 #4096       ]
            [ #4106 #3708                      ][ #4107 #4089                    ]
                                    |CP1|                              |CP2|

            [== Fase 3: Agent Views + Decisore ==][== Fase 4: Strategy+MA Core =]
            [ #4087 #4088 #4090 #4091 #4092      ][ #4093 #3771 #3773 #3774    ]
            [ #3769 #3770 #3772 #3916            ][ #3776 #3777 #3809 #3120    ]
                                    |CP3|                              |CP4|

            [============ Fase 5: AI Backend + Testing + Infra ============][F6]
            [ #3874 #3779 #3780 #3358 #3082                                ][ Mon]
            [ #2968 #2969 #2970 #2972 #2973 #3367 #3368                    ][ Doc]
                                                               |CP5|       |CP6|
```

---

## Dipendenze Cross-Terminal

| Da (Terminal) | A (Terminal) | Motivo | Quando |
|---------------|-------------|--------|--------|
| B: #4082 Agent Backend | A: #4079 MeepleCard Agent Type | Agent type data model | CP1 |
| B: #4085 Chat UI Base | A: #4104 Nav Notifications | Shared notification system | CP2 |
| B: #4083 Strategy System | A: #3711 Strategy Editor | Strategy data model | CP4 |
| B: #3708 AgentDefinition | A: #3709 Agent Builder UI | Data model API | CP4 |
| B: #3916 AI Insights Backend | A: #3919 AI Insights Widget | API endpoints | CP3→4 |
| B: #3776 Multi-Agent Orch. | A: #3778 Multi-Agent Dashboard | Orchestration API | CP4→5 |
| B: #4106 PDF Pipeline | A: #4108 PDF Multi-Location UI | Status API | CP1→3 |

**Regola**: Le dipendenze cross-terminal devono essere risolte al checkpoint precedente.

---

## Statistiche Finali

| Metrica | Terminal A | Terminal B | Totale |
|---------|-----------|-----------|--------|
| Issues | 44 | 47 | 91 |
| Giorni stimati | ~57d | ~59d | ~116d |
| Settimane | ~11.5 | ~12 | ~12 (parallelo) |
| Fasi | 6 | 6 | 6 |
| Checkpoints | 6 | 6 | 6 |

### Distribuzione per Priorita

| Priorita | Count | Percentuale |
|----------|-------|-------------|
| p0-critical | 7 | 8% |
| p1-high | 27 | 30% |
| p2-medium | 18 | 20% |
| p3-low | 1 | 1% |
| Unlabeled | 38 | 41% |

### Distribuzione per Epic

| Epic | Issues | Giorni | % Totale |
|------|--------|--------|----------|
| MeepleCard (#4068) | 10 | 12d | 10% |
| Agent System (#4069) | 15 | 24d | 21% |
| Navbar (#4070) | 9 | 12d | 10% |
| PDF Status (#4071) | 6 | 9.5d | 8% |
| New UI Features | 6 | 6d | 5% |
| Multi-Agent AI (#3490) | 15 | 17d | 15% |
| AI Platform | 10 | 12d | 10% |
| AI Insights & Other | 8 | 12d | 10% |
| Infrastructure (#2967) | 10 | 12d | 10% |

---

## Comandi Rapidi per Ogni Fase

### Inizio Fase (entrambi i terminali)
```bash
git checkout main-dev && git pull
git checkout -b feature/fase-N-terminal-X
```

### Checkpoint Sync
```bash
# Merge feature branch
git checkout main-dev && git pull
git merge --no-ff feature/fase-N-terminal-X
# Run full test suite
pwsh -c "cd apps/web; pnpm test && pnpm typecheck && pnpm lint && pnpm build"
# Prune
git branch -D feature/fase-N-terminal-X
git remote prune origin
```

### Recovery da Conflitto
```bash
# Se conflitto durante merge al checkpoint
git merge --abort
# Comunicare con l'altro terminale
# Risolvere manualmente
git merge --continue
```

---

## Note Operative

1. **Branch Strategy**: Ogni issue ha il proprio feature branch da `main-dev`. Non creare branch per fase intera.
2. **PR Target**: Sempre verso `main-dev` (o parent branch se sotto-branch).
3. **Code Review**: Obbligatorio per ogni PR prima del merge (`/code-review:code-review`).
4. **Test**: Ogni issue deve avere test (unit + integration). Target: 90% backend, 85% frontend.
5. **Checkpoint**: Entrambi i terminali devono completare le issue della fase PRIMA del checkpoint.
6. **Conflitti**: Se un terminale e bloccato da una dipendenza cross-terminal, procedere con issue indipendenti.
7. **Comunicazione**: I checkpoint servono anche per allineare scope e risolvere problemi emersi.

---

*Generato: 2026-02-11 | Prossimo aggiornamento: al completamento di ogni checkpoint*
