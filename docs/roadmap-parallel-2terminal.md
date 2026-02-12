# MeepleAI - Roadmap Parallelo 2 Terminali

> **Data**: 2026-02-11 (AGGIORNATO)
> **Status**: ✅ Fasi 1-4 COMPLETATE | Rimangono Fasi 5-6
> **Issues Aperte nel Roadmap**: 45 (32 work items + 13 epic/infra)
> **Stima Rimanente**: ~30-35 giorni lavorativi per terminale (~6-7 settimane)

---

## 🎯 Sequenza Issue da Risolvere

### Priorità Immediata (Fase 5a - Settimana 1-2)

**Terminal A - UI Completion**

1. #4117 - Achievement System Display UI (M, 1d)x
2. #4114 - Wishlist Management System UI (M, 1d)x
3. #4116 - 2FA Self-Service UI (M, 1d)x
4. #4118 - User Bulk Operations UI (M, 1d)x
5. #3919 - Frontend: AI Insights Widget Component (M, 1d) - _dipende da #3916_

**Terminal B - Multi-Agent Foundation**

1. #3708 - AgentDefinition Data Model (M, 1d)
2. #3916 - Backend: AI Insights Service (RAG Integration) (M, 1d)
3. #3769 - Decisore - Strategic Analysis Engine (M, 1d)
4. #3770 - Decisore - Move Suggestion Algorithm (M, 1d) - _dipende da #3769_
5. #3772 - Decisore - Game State Parser (M, 1d) - _dipende da #3769_

**Subtotale**: 10 issues, ~10d (5d per terminale)

---

### AI Platform Core (Fase 5b - Settimana 3-4)

**Terminal A - AI Platform UI**

1. #3709 - Agent Builder UI (M, 1d) - _dipende da #3708_
2. #3710 - Agent Playground (M, 1d)
3. #3711 - Strategy Editor (M, 1d)
4. #3712 - Visual Pipeline Builder (L, 2d)
5. #3713 - Agent Catalog & Usage Stats (M, 1d)
6. #3714 - Chat Analytics (M, 1d)
7. #3715 - PDF Analytics (M, 1d)
8. #3716 - Model Performance Tracking (M, 1d)
9. #3717 - A/B Testing Framework (M, 1d)

**Terminal B - Multi-Agent Complete**

1. #3771 - Decisore - Multi-Model Ensemble (M, 1d) - _dipende da #3769, #3770_
2. #3773 - Decisore - REST API Endpoint (M, 1d) - _dipende da #3769, #3770, #3772_
3. #3774 - Decisore - Performance Tuning <10s (M, 1d) - _dipende da #3773_
4. #3776 - Multi-Agent Orchestration & Routing (M, 1d)
5. #3777 - Agent Switching Logic & Context (M, 1d) - _dipende da #3776_
6. #3809 - Agent Builder Form & CRUD (M, 1d) - _dipende da #3708_

**Subtotale**: 15 issues, ~15d (9d Term A, 6d Term B)

---

### Testing & Infrastructure (Fase 5c-6 - Settimana 5-7)

**Terminal A - Testing & Polish**

1. #3778 - Unified Multi-Agent Dashboard UI (M, 1d) - _dipende da #3776, #3777_
2. #3894 - EntityListView Test Coverage & Polish (M, 1d)
3. #3775 - Decisore Beta Testing & Expert Validation (M, 1d) - _dipende da #3773_
4. #3763 - Arbitro Testing & User Feedback (M, 1d)
5. #3718 - Testing - AI Platform (comprehensive) (L, 2d)

**Terminal B - Infrastructure & Monitoring**

1. #3874 - Arbitro Performance Benchmark Tests (M, 1d)
2. #3779 - E2E Testing Suite - All Agent Workflows (L, 2d)
3. #3780 - Complete Documentation & User Guide (M, 1d)
4. #3358 - Iterative RAG Strategy (L, 2d)
5. #3082 - Missing E2E Test Flows (50 flows) (L, 2d)
6. #2968 - Oracle Cloud Setup & VM Provisioning (M, 1d)
7. #2969 - GitHub Actions Runner Installation (M, 1d) - _dipende da #2968_
8. #2970 - Workflow Migration to Self-Hosted (M, 1d) - _dipende da #2969_
9. #2972 - Performance Monitoring & Reliability (M, 1d) - _dipende da #2968_
10. #2973 - Cost Validation GitHub Billing (M, 1d) - _dipende da #2970_
11. #3367 - Log Aggregation System (L, 2d) - _dipende da #2968_
12. #3368 - k6 Load Testing Infrastructure (M, 1d) - _dipende da #2968_
13. #2974 - Setup Monitoring (Prometheus + Grafana) (M, 1d) - _dipende da #2968_
14. #2975 - Document Troubleshooting Procedures (M, 1d)
15. #2976 - Maintenance Schedule Automation (M, 1d) - _dipende da #2974_

**Subtotale**: 20 issues, ~23d (6d Term A, 17d Term B)

---

### Totale Rimanente

| Fase                     | Issues | Giorni Term A | Giorni Term B | Settimane |
| ------------------------ | ------ | ------------- | ------------- | --------- |
| **5a - Immediata**       | 10     | 5d            | 5d            | 1-2       |
| **5b - AI Platform**     | 15     | 9d            | 6d            | 3-4       |
| **5c-6 - Testing+Infra** | 20     | 6d            | 17d           | 5-7       |
| **TOTALE**               | **45** | **20d**       | **28d**       | **7**     |

---

## ✅ Stato Completamento

### Fasi Completate (1-4)

**Epic #1: MeepleCard Enhancements** ✅ 10/10 issues (#4072-#4081)
**Epic #2: Agent System Core** ✅ 15/15 issues (#4082-#4096)
**Epic #3: Navbar Restructuring** ✅ 9/9 issues (#4097-#4105)
**Epic #4: PDF Status Tracking** ✅ 6/6 issues (#4106-#4111)
**New UI Features** ✅ 2/6 issues (#4113, #4115)

**Totale completato**: 42 issues | ~44 giorni di lavoro

---

## Architettura Terminali Rimanenti (Fasi 5-6)

| Terminale      | Focus Primario                                  |
| -------------- | ----------------------------------------------- |
| **Terminal A** | AI Platform UI, Testing, Polish                 |
| **Terminal B** | Multi-Agent Backend, Infrastructure, Monitoring |

---

## Legenda Stime

| Size      | Ore | Giorni       |
| --------- | --- | ------------ |
| XS        | 2h  | 0.25d        |
| S         | 4h  | 0.5d         |
| M         | 8h  | 1d           |
| L         | 16h | 2d           |
| XL        | 24h | 3d           |
| Unlabeled | 8h  | 1d (default) |

---

## Fase 5: AI Platform & Advanced (Settimane 1-4)

> **Obiettivo**: AI Platform features, advanced agent tools, testing coverage

### Terminal A - AI Platform Frontend + Testing

| #   | Issue | Titolo                                    | Size | Giorni | Dipendenze   |
| --- | ----- | ----------------------------------------- | ---- | ------ | ------------ |
| 1   | #3709 | Agent Builder UI                          | M    | 1d     | #3708, #3809 |
| 2   | #3710 | Agent Playground                          | M    | 1d     | #4085, #3709 |
| 3   | #3711 | Strategy Editor                           | M    | 1d     | #4083, #4093 |
| 4   | #3712 | Visual Pipeline Builder                   | L    | 2d     | #3708        |
| 5   | #3713 | Agent Catalog & Usage Stats               | M    | 1d     | #4090, #3708 |
| 6   | #3714 | Chat Analytics                            | M    | 1d     | #4087        |
| 7   | #3715 | PDF Analytics                             | M    | 1d     | #4108        |
| 8   | #3716 | Model Performance Tracking                | M    | 1d     | #3708        |
| 9   | #3717 | A/B Testing Framework                     | M    | 1d     | Nessuna      |
| 10  | #3778 | Unified Multi-Agent Dashboard UI          | M    | 1d     | #3776, #3777 |
| 11  | #3894 | EntityListView Test Coverage & Polish     | M    | 1d     | Nessuna      |
| 12  | #3775 | Decisore Beta Testing & Expert Validation | M    | 1d     | #3773        |
| 13  | #3763 | Arbitro Testing & User Feedback           | M    | 1d     | Nessuna      |

**Subtotale A Fase 5**: 14 giorni (3.5 settimane)

### Terminal B - AI Platform Backend + Testing + Infra Start

| #   | Issue | Titolo                                  | Size | Giorni | Dipendenze          |
| --- | ----- | --------------------------------------- | ---- | ------ | ------------------- |
| 1   | #3874 | Arbitro Performance Benchmark Tests     | M    | 1d     | Nessuna             |
| 2   | #3779 | E2E Testing Suite - All Agent Workflows | L    | 2d     | #4085, #4086, #4087 |
| 3   | #3780 | Complete Documentation & User Guide     | M    | 1d     | Tutte Agent         |
| 4   | #3358 | Iterative RAG Strategy                  | L    | 2d     | Nessuna             |
| 5   | #3082 | Missing E2E Test Flows (50 flows)       | L    | 2d     | Varie               |
| 6   | #2968 | Oracle Cloud Setup & VM Provisioning    | M    | 1d     | Nessuna             |
| 7   | #2969 | GitHub Actions Runner Installation      | M    | 1d     | #2968               |
| 8   | #2970 | Workflow Migration to Self-Hosted       | M    | 1d     | #2969               |
| 9   | #2972 | Performance Monitoring & Reliability    | M    | 1d     | #2968               |
| 10  | #2973 | Cost Validation GitHub Billing          | M    | 1d     | #2970               |
| 11  | #3367 | Log Aggregation System                  | L    | 2d     | #2968               |
| 12  | #3368 | k6 Load Testing Infrastructure          | M    | 1d     | #2968               |

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

| #   | Issue                      | Titolo                                | Size | Giorni | Dipendenze      |
| --- | -------------------------- | ------------------------------------- | ---- | ------ | --------------- |
| 1   | #3718                      | Testing - AI Platform (comprehensive) | L    | 2d     | Fase 5 completa |
| 2   | UI regression test suite   | Tutti i componenti                    | -    | 2d     | Tutte le fasi   |
| 3   | Accessibility audit finale | WCAG 2.1 AA                           | -    | 1d     | Tutte le fasi   |

**Subtotale A Fase 6**: 5 giorni

### Terminal B - Infrastructure Completion

| #   | Issue | Titolo                                  | Size | Giorni | Dipendenze     |
| --- | ----- | --------------------------------------- | ---- | ------ | -------------- |
| 1   | #2974 | Setup Monitoring (Prometheus + Grafana) | M    | 1d     | #2968          |
| 2   | #2975 | Document Troubleshooting Procedures     | M    | 1d     | Infra completa |
| 3   | #2976 | Maintenance Schedule Automation         | M    | 1d     | #2974          |

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

### New UI Features Rimanenti - Fase 5a

| Issue | Titolo                        | Size | Fase | Terminal |
| ----- | ----------------------------- | ---- | ---- | -------- |
| #4118 | User Bulk Operations UI       | M    | 5a   | A        |
| #4117 | Achievement System Display UI | M    | 5a   | A        |
| #4116 | 2FA Self-Service UI           | M    | 5a   | A        |
| #4114 | Wishlist Management System UI | M    | 5a   | A        |

**Totale**: 4 issues, ~4d (2 completate: #4113, #4115)

### Multi-Agent AI System (#3490) - Terminal B Fase 3-5

| Issue | Titolo                               | Size | Fase | Terminal |
| ----- | ------------------------------------ | ---- | ---- | -------- |
| #3769 | Decisore - Strategic Analysis Engine | M    | 3    | B        |
| #3770 | Decisore - Move Suggestion Algorithm | M    | 3    | B        |
| #3772 | Decisore - Game State Parser         | M    | 3    | B        |
| #3771 | Decisore - Multi-Model Ensemble      | M    | 4    | B        |
| #3773 | Decisore - REST API Endpoint         | M    | 4    | B        |
| #3774 | Decisore - Performance Tuning <10s   | M    | 4    | B        |
| #3776 | Multi-Agent Orchestration & Routing  | M    | 4    | B        |
| #3777 | Agent Switching Logic & Context      | M    | 4    | B        |
| #3809 | Agent Builder Form & CRUD            | M    | 4    | B        |
| #3874 | Arbitro Performance Benchmark        | M    | 5    | B        |
| #3763 | Arbitro Testing & User Feedback      | M    | 5    | A        |
| #3775 | Decisore Beta Testing                | M    | 5    | A        |
| #3778 | Unified Multi-Agent Dashboard UI     | M    | 5    | A        |
| #3779 | E2E Testing Suite - All Agent        | L    | 5    | B        |
| #3780 | Documentation & User Guide           | M    | 5    | B        |

**Totale**: 15 issues, ~17d

### AI Platform (#3708-3717) - Terminal A/B Fase 5

| Issue | Titolo                      | Size | Fase | Terminal |
| ----- | --------------------------- | ---- | ---- | -------- |
| #3708 | AgentDefinition Data Model  | M    | 1    | B        |
| #3709 | Agent Builder UI            | M    | 5    | A        |
| #3710 | Agent Playground            | M    | 5    | A        |
| #3711 | Strategy Editor             | M    | 5    | A        |
| #3712 | Visual Pipeline Builder     | L    | 5    | A        |
| #3713 | Agent Catalog & Usage Stats | M    | 5    | A        |
| #3714 | Chat Analytics              | M    | 5    | A        |
| #3715 | PDF Analytics               | M    | 5    | A        |
| #3716 | Model Performance Tracking  | M    | 5    | A        |
| #3717 | A/B Testing Framework       | M    | 5    | A        |

**Totale**: 10 issues, ~12d

### AI Insights & Other (#3902) - Terminal A/B Fase 3-5

| Issue | Titolo                           | Size | Fase | Terminal |
| ----- | -------------------------------- | ---- | ---- | -------- |
| #3916 | Backend: AI Insights Service     | M    | 3    | B        |
| #3919 | Frontend: AI Insights Widget     | M    | 4    | A        |
| #3894 | EntityListView Test Coverage     | M    | 5    | A        |
| #3355 | Version History UI               | M    | 4    | A        |
| #3120 | Private Games & Catalog Proposal | M    | 4    | B        |
| #3358 | Iterative RAG Strategy           | L    | 5    | B        |
| #3082 | Missing E2E Test Flows (50)      | L    | 5    | B        |
| #3718 | Testing - AI Platform            | L    | 6    | A        |

**Totale**: 8 issues, ~12d

### Infrastructure (#2967, #3366) - Terminal B Fase 5-6

| Issue | Titolo                                | Size | Fase | Terminal |
| ----- | ------------------------------------- | ---- | ---- | -------- |
| #2968 | Oracle Cloud Setup & VM               | M    | 5    | B        |
| #2969 | GitHub Actions Runner Install         | M    | 5    | B        |
| #2970 | Workflow Migration to Self-Hosted     | M    | 5    | B        |
| #2972 | Performance Monitoring                | M    | 5    | B        |
| #2973 | Cost Validation GitHub Billing        | M    | 5    | B        |
| #3367 | Log Aggregation System                | L    | 5    | B        |
| #3368 | k6 Load Testing                       | M    | 5    | B        |
| #2974 | Setup Monitoring (Prometheus+Grafana) | M    | 6    | B        |
| #2975 | Document Troubleshooting              | M    | 6    | B        |
| #2976 | Maintenance Schedule Automation       | M    | 6    | B        |

**Totale**: 10 issues, ~12d

---

## Statistiche Rimanenti

### Epic Completati ✅

- **Epic #1: MeepleCard** (#4068) - 10 issues, ~12d ✅
- **Epic #2: Agent System Core** (#4069) - 15 issues, ~24d ✅
- **Epic #3: Navbar Restructuring** (#4070) - 9 issues, ~12d ✅
- **Epic #4: PDF Status** (#4071) - 6 issues, ~9.5d ✅
- **New UI Features** - 2 issues completate (#4113, #4115)

**Totale completato**: 42 issues, ~57.5d

---

### Issues Rimanenti (45)

| Metrica            | Terminal A | Terminal B | Totale           |
| ------------------ | ---------- | ---------- | ---------------- |
| **Issues**         | 18         | 27         | 45               |
| **Giorni stimati** | ~20d       | ~28d       | ~28d (parallelo) |
| **Settimane**      | ~4         | ~5.6       | ~6-7             |
| **Fasi**           | 2 (5+6)    | 2 (5+6)    | 2                |

### Distribuzione per Area

| Area                          | Issues | Giorni | % Rimanente |
| ----------------------------- | ------ | ------ | ----------- |
| New UI Features (rimanenti)   | 4      | 4d     | 14%         |
| Multi-Agent AI (#3490)        | 15     | 17d    | 36%         |
| AI Platform (#3708-3717)      | 10     | 12d    | 25%         |
| AI Insights & Other           | 6      | 8d     | 16%         |
| Infrastructure (#2967, #3366) | 10     | 12d    | 25%         |

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

_Generato: 2026-02-11 | Prossimo aggiornamento: al completamento di ogni checkpoint_
