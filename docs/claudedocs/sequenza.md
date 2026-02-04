# Sequenza Implementazione Issue - MeepleAI

**Last Updated**: 2026-02-04
**Total Open Issues**: 110 (-6: #3496-#3502 except #3501, +#3373)
**Active Epics**: 17

---

## 🎯 SEQUENZA ISSUE (Numeri in Ordine di Priorità)

### 📋 Lista Rapida - Issue Sequence

```
FASE 1 - Critical Path (Parallel Streams)
├─ Backend:  3479 → 3483
├─ Frontend: 3376 → 3375 → 3378 → 3379
└─ Tier:     3440 → 3441 → 3442

FASE 2 - Multi-Agent Foundation
├─ Backend:  3493 → 3491 → 3492 → 3494 → 3495
├─ Tutor:    3496 → 3497 → 3498 → 3499 → 3502 → 3501
└─ Frontend: 3373 → 3374 → 3383

FASE 3 - Security & Dashboard
├─ Security: 3330 → 3332 → 3339 → 3340 → 3335 → 3333 → 3338
├─ Dashboard:3316 → 3319 → 3317 → 3318 → 3321 → 3322 → 3323
└─ MeepleCard: 3326 → 3328 → 3329 → 3334 → 3331 → 3336

FASE 4 - RAG Enhancement
├─ Navigation: 3404 → 3405 → 3406 → 3407 → 3408 → 3409 → 3410
├─ Dashboard:  3300 → 3301 → 3302 → 3303 → 3304 → 3305
└─ Docs:       3401 → 3399 → 3398 → 3449 → 3450 → 3451

FASE 5 - Plugin Architecture
├─ Core:     3414 → 3415 → 3416 → 3417
├─ Plugins:  3418 → 3419 → 3420 → 3421 → 3422 → 3423 → 3424
└─ UI:       3425 → 3426 → 3427 → 3428 → 3429 → 3430 → 3431

FASE 6 - Visual Strategy Builder
├─ Setup:    3454 → 3456 → 3457
├─ Blocks:   3458 → 3459 → 3460 → 3461 → 3462
├─ Advanced: 3463 → 3464 → 3465 → 3466 → 3467 → 3468
└─ Duplicates: 3469, 3470, 3471 (close as duplicates)

FASE 7 - Advanced Features
├─ Admin:    3349 → 3350 → 3380 → 3381 → 3382 → 3384 → 3385
├─ AI:       3351 → 3352 → 3353
├─ Sessions: 3342 → 3343 → 3344 → 3345 → 3346 → 3347 → 3354 → 3355
└─ RAG:      3357 → 3358 → 3359 → 3360 → 3361 → 3363

FASE 8 - Infrastructure & Testing
├─ Infra:    3367 → 3368 → 2968 → 2969 → 2970 → 2972 → 2973 → 2974 → 2975 → 2976
└─ E2E:      3082
```

---

## 📊 SUMMARY TABLE - All 119 Open Issues

| Fase | Epic | Issues | SP Est. | Area | Parallelizzazione |
|:----:|------|:------:|:-------:|:----:|:-----------------:|
| **1** | Critical Path | 10 | 41 | BE+FE | 3 streams |
| **2** | Multi-Agent AI | 14 | 56 | BE+PY | 2 streams |
| **3** | Security + Dashboard | 20 | 70 | BE+FE | 3 streams |
| **4** | RAG Enhancement | 20 | 55 | FE+BE | 3 streams |
| **5** | Plugin Architecture | 18 | 85 | BE+FE | 2 streams |
| **6** | Visual Strategy | 18 | 60 | FE | 1 stream |
| **7** | Advanced Features | 19 | 75 | BE+FE | 3 streams |
| **8** | Infrastructure | 11 | 40 | DevOps | 1 stream |
| **TOTAL** | **17 Epics** | **116** | **~470** | - | - |

---

## 🔴 FASE 1 - Critical Path (IMMEDIATE)

**Timeline**: 2-3 settimane | **SP**: 41 | **Parallelizzazione**: 3 stream

### Stream A: Backend Foundation
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 1 | #3479 | Private PDF Upload Endpoint | 3 | - |
| 2 | #3483 | Chat Session Persistence Service | 5 | - |

### Stream B: Agent Creation Flow
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 3 | #3376 | Agent Creation Wizard | 5 | - |
| 4 | #3375 | Agent Session Launch | 3 | #3376 |
| 5 | #3378 | Agent Test Execution UI | 5 | #3375 |
| 6 | #3379 | Agent Test Results History | 5 | #3378 |

### Stream C: Tier-Strategy Completion
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 7 | #3440 | Admin UI for tier-strategy config | 3 | ✅ #3437 closed |
| 8 | #3441 | Tests for tier-strategy-model | 5 | #3440 |
| 9 | #3442 | Documentation tier-strategy | 2 | #3441 |

**Epic References**:
- #3475 User Private Library (1 issue remaining)
- #3386 Agent Creation & Testing Flow (4 issues)
- #3434 Tier-Strategy-Model (3 issues remaining)

---

## 🟠 FASE 2 - Multi-Agent AI System (NEW EPIC)

**Timeline**: 3-4 settimane | **SP**: 56 | **Parallelizzazione**: 2 stream

### Stream A: Context Engineering Foundation
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 10 | #3493 | PostgreSQL Schema Extensions | 5 | - |
| 11 | #3491 | Context Engineering Framework | 8 | #3493 |
| 12 | #3492 | Hybrid Search - Keyword+Vector+Reranking | 8 | #3491 |
| 13 | #3494 | Redis 3-Tier Cache Layer | 5 | #3492 |
| 14 | #3495 | LangGraph Orchestrator Foundation | 8 | #3494 |

### Stream B: Tutor Agent Implementation
| Seq | Issue | Titolo | SP | Status | PR |
|:---:|:-----:|--------|:--:|:------:|:--:|
| 15 | #3496 | Intent Classification System | 5 | ✅ Done | #3540 |
| 16 | #3497 | Multi-Turn Dialogue State Machine | 5 | ✅ Done | #3540 |
| 17 | #3498 | Conversation Memory - Temporal RAG | 5 | ✅ Done | #3540 |
| 18 | #3499 | REST API Endpoint /agents/tutor/query | 3 | ✅ Done | #3540 |
| 19 | #3502 | Hybrid Search Integration | 3 | ✅ Done | #3540 |
| 20 | #3501 | Beta Testing & User Feedback | 3 | ⏳ Ready | - |

### Stream C: Frontend AI Integration
| Seq | Issue | Titolo | SP | Status | PR |
|:---:|:-----:|--------|:--:|:------:|:--:|
| 21 | #3373 | Streaming SSE in Ask page | 3 | ✅ Done | #3542 |
| 22 | #3374 | Cancel processing button UI | 2 | ⏳ Ready | - |
| 23 | #3383 | Cost Estimation Preview | 3 | 🔒 Blocked | #3374 |

**Epic Reference**: #3490 Multi-Agent Game AI System

---

## 🟡 FASE 3 - Security & Dashboard

**Timeline**: 3-4 settimane | **SP**: 70 | **Parallelizzazione**: 3 stream

### Stream A: Security Foundation (#3327)
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 24 | #3330 | Session Limits Enforcement | 5 | - |
| 25 | #3332 | Email Verification | 5 | - |
| 26 | #3339 | Account Lockout | 3 | #3330 |
| 27 | #3340 | Login Device Tracking | 3 | #3339 |
| 28 | #3335 | Tier-Based Feature Access | 5 | #3340 |
| 29 | #3333 | PDF Upload Limits Config UI | 3 | #3335 |
| 30 | #3338 | AI Token Usage Tracking | 5 | #3335 |

### Stream B: Dashboard AI Insights (#3315)
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 31 | #3316 | AiInsightsWidget with RAG | 5 | - |
| 32 | #3319 | AI Insights RAG endpoint | 5 | #3316 |
| 33 | #3317 | WishlistHighlights widget | 2 | - |
| 34 | #3318 | CatalogTrending widget | 2 | - |
| 35 | #3321 | AchievementsWidget | 3 | - |
| 36 | #3322 | ActivityFeed filters | 3 | - |
| 37 | #3323 | Responsive polish | 2 | - |

### Stream C: MeepleCard System (#3325)
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 38 | #3326 | Core component implementation | 5 | - |
| 39 | #3328 | Unit + accessibility tests | 3 | #3326 |
| 40 | #3329 | Storybook documentation | 2 | #3328 |
| 41 | #3334 | SharedGameCatalog integration | 3 | #3329 |
| 42 | #3331 | Migration GameCard → MeepleCard | 3 | #3334 |
| 43 | #3336 | Documentation & usage guide | 2 | #3331 |

**Epic References**:
- #3327 Security & Quota Enforcement
- #3315 AI Insights & Recommendations
- #3320 Gamification & Dashboard Features
- #3325 MeepleCard Universal Card System
- #3306 Dashboard Hub Core (partial)

---

## 🟢 FASE 4 - RAG Enhancement

**Timeline**: 2-3 settimane | **SP**: 55 | **Parallelizzazione**: 3 stream

### Stream A: Navigation Redesign (#3403)
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 44 | #3404 | useScrollSpy hook | 2 | - |
| 45 | #3405 | DashboardSidebar | 3 | #3404 |
| 46 | #3406 | DashboardNav mobile | 3 | #3404 |
| 47 | #3407 | SectionGroup wrapper | 2 | #3405 |
| 48 | #3408 | ProgressIndicator | 2 | #3407 |
| 49 | #3409 | Integration into Dashboard | 3 | #3408 |
| 50 | #3410 | Tests for navigation | 2 | #3409 |

### Stream B: RAG Dashboard Enhancement (#3299)
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 51 | #3300 | Strategy Cards Component | 3 | - |
| 52 | #3301 | Strategy Detail Modal | 3 | #3300 |
| 53 | #3302 | Performance Metrics Dashboard | 5 | #3301 |
| 54 | #3303 | Configuration Panel | 3 | #3302 |
| 55 | #3304 | Backend API Metrics | 5 | #3303 |
| 56 | #3305 | Test Suite | 3 | #3304 |

### Stream C: Documentation & UX
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 57 | #3401 | Documentation Consolidation | 5 | - |
| 58 | #3399 | Data Consistency Audit | 3 | #3401 |
| 59 | #3398 | Metrics Configuration Form | 3 | #3399 |
| 60 | #3449 | Accordion system | 2 | - |
| 61 | #3450 | Global search Cmd+K | 3 | - |
| 62 | #3451 | Breadcrumbs & scroll | 2 | - |

**Epic References**:
- #3403 RAG Dashboard Navigation Redesign
- #3299 RAG Dashboard Enhancement

---

## 🔵 FASE 5 - Plugin Architecture

**Timeline**: 4-5 settimane | **SP**: 85 | **Parallelizzazione**: 2 stream
**Blockers**: Requires FASE 1 (#3434 Epic) completion

### Stream A: Backend Core (#3413)
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 63 | #3414 | Plugin Contract & Interfaces | 5 | FASE 1 |
| 64 | #3415 | DAG Orchestrator | 8 | #3414 |
| 65 | #3416 | Pipeline Definition Schema | 5 | #3414 |
| 66 | #3417 | Plugin Registry Service | 5 | #3415, #3416 |

### Stream B: Plugin Implementation
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 67 | #3418 | Routing Plugins | 5 | #3417 |
| 68 | #3419 | Cache Plugins | 3 | #3417 |
| 69 | #3420 | Retrieval Plugins | 8 | #3417 |
| 70 | #3421 | Evaluation Plugins | 5 | #3420 |
| 71 | #3422 | Generation Plugins | 5 | #3421 |
| 72 | #3423 | Validation Plugins | 5 | #3422 |
| 73 | #3424 | Transform/Filter Plugins | 5 | #3423 |

### Stream C: Frontend UI
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 74 | #3425 | Visual Pipeline Builder | 8 | #3417 |
| 75 | #3426 | Plugin Palette Component | 5 | #3425 |
| 76 | #3427 | Node Configuration Panel | 5 | #3426 |
| 77 | #3428 | Edge Configuration Panel | 3 | #3427 |
| 78 | #3429 | Pipeline Preview/Test | 5 | #3428 |
| 79 | #3430 | Plugin Testing Framework | 5 | #3429 |
| 80 | #3431 | Plugin System Documentation | 3 | #3430 |

**Epic Reference**: #3413 Plugin-Based RAG Pipeline Architecture

---

## 🟣 FASE 6 - Visual Strategy Builder

**Timeline**: 3-4 settimane | **SP**: 60 | **Parallelizzazione**: 1 stream (sequential)
**Blockers**: Requires FASE 5 (#3413 Epic) completion

### Visual RAG Strategy Builder (#3453)
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 81 | #3454 | Block type system & metadata | 5 | FASE 5 |
| 82 | #3456 | Block palette UI (23 blocks) | 5 | #3454 |
| 83 | #3457 | ReactFlow canvas setup | 5 | #3456 |
| 84 | #3458 | Tier 1 blocks (7 essential) | 5 | #3457 |
| 85 | #3459 | Drag-drop mechanics | 3 | #3458 |
| 86 | #3460 | Block connection system | 5 | #3459 |
| 87 | #3461 | Parameter config panel | 3 | #3460 |
| 88 | #3462 | Strategy validation engine | 5 | #3461 |
| 89 | #3463 | Live test API with SSE | 5 | #3462 |
| 90 | #3464 | Save/load/export | 5 | #3463 |
| 91 | #3465 | Tier 2 advanced blocks (6) | 5 | #3464 |
| 92 | #3466 | Tier 3-4 experimental (10) | 5 | #3465 |
| 93 | #3467 | Strategy templates | 3 | #3466 |
| 94 | #3468 | E2E tests | 3 | #3467 |

### ✅ DUPLICATI CHIUSI
| Issue | Duplicate Of | Status |
|:-----:|:------------:|:------:|
| #3469 | #3465 | ✅ Closed |
| #3470 | #3466 | ✅ Closed |
| #3471 | #3467 | ✅ Closed |

**Epic References**:
- #3453 Visual RAG Strategy Builder
- #3412 Custom Strategy Builder (blocked by #3413)

---

## ⚪ FASE 7 - Advanced Features

**Timeline**: 4-5 settimane | **SP**: 75 | **Parallelizzazione**: 3 stream

### Stream A: Admin Tools (#3348 partial)
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 95 | #3349 | User Impersonation | 3 | - |
| 96 | #3350 | Batch Approval | 2 | - |
| 97 | #3380 | Strategy Comparison UI | 5 | FASE 1 |
| 98 | #3381 | Typology Approval Workflow | 3 | - |
| 99 | #3382 | Agent Metrics Dashboard | 5 | FASE 2 |
| 100 | #3384 | Game Image Upload | 3 | - |
| 101 | #3385 | BGG Bulk Import | 5 | - |

### Stream B: AI Enhancements
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 102 | #3351 | Voice-to-Text Input | 3 | - |
| 103 | #3352 | AI Feedback System | 3 | FASE 2 |
| 104 | #3353 | Similar Games Discovery | 5 | FASE 2 |

### Stream C: Game Session Toolkit (#3341)
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 105 | #3342 | Dice Roller | 3 | - |
| 106 | #3343 | Card Deck System | 5 | - |
| 107 | #3344 | Private Notes | 3 | - |
| 108 | #3345 | Timer/Coin/Wheel | 2 | - |
| 109 | #3346 | Offline-First PWA | 5 | - |
| 110 | #3347 | Session Sharing | 3 | - |
| 111 | #3354 | Session Invite Links | 3 | - |
| 112 | #3355 | Version History | 3 | - |

### Stream D: Advanced RAG Strategies (#3356)
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 113 | #3357 | Sentence Window RAG | 3 | FASE 5 |
| 114 | #3358 | Iterative RAG | 5 | #3357 |
| 115 | #3359 | Multi-Agent RAG | 5 | #3358 |
| 116 | #3360 | Step-Back Prompting | 3 | #3359 |
| 117 | #3361 | Query Expansion | 3 | #3360 |
| 118 | #3363 | RAG-Fusion | 5 | #3361 |

**Epic References**:
- #3348 Advanced Features
- #3341 Game Session Toolkit Phase 2
- #3356 Advanced RAG Strategies

---

## ⬛ FASE 8 - Infrastructure & Testing

**Timeline**: 3-4 settimane | **SP**: 40 | **Parallelizzazione**: 1 stream

### Infrastructure (#3366 + #2967)
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 119 | #3367 | Log Aggregation System | 5 | - |
| 120 | #3368 | k6 Load Testing | 3 | - |
| 121 | #2968 | Oracle Cloud Setup | 3 | - |
| 122 | #2969 | GitHub Actions Runner | 3 | #2968 |
| 123 | #2970 | Workflow Migration | 5 | #2969 |
| 124 | #2972 | Performance Monitoring | 3 | #2970 |
| 125 | #2973 | Cost Validation | 2 | #2972 |
| 126 | #2974 | Prometheus + Grafana | 5 | #2973 |
| 127 | #2975 | Troubleshooting Docs | 2 | - |
| 128 | #2976 | Maintenance Automation | 3 | #2974 |

### E2E Testing
| Seq | Issue | Titolo | SP | Dipendenze |
|:---:|:-----:|--------|:--:|:----------:|
| 129 | #3082 | Missing E2E Test Flows (50) | 10 | ALL |

**Epic References**:
- #3366 Infrastructure Enhancements
- #2967 Zero-Cost CI/CD Infrastructure

---

## 📈 PARALLELIZATION MATRIX

### Week-by-Week Execution Plan

```
WEEK 1-2: FASE 1 (41 SP)
├─ Backend Team:    #3479, #3483 ─────────────────────────┐
├─ Frontend Team A: #3376, #3375 ─────────────────────────┤ PARALLEL
├─ Frontend Team B: #3378, #3379 ─────────────────────────┤
└─ DevOps Team:     #3440, #3441, #3442 ──────────────────┘

WEEK 3-5: FASE 2 (56 SP)
├─ Python Team:     #3493 → #3491 → #3492 → #3494 → #3495 ┐
├─ AI Team:         #3496 → #3497 → #3498 → #3499 ────────┤ PARALLEL
└─ Frontend Team:   #3373, #3374, #3383 ──────────────────┘

WEEK 6-9: FASE 3 (70 SP)
├─ Security Team:   #3330 → #3332 → #3339 → #3340 ────────┐
├─ Dashboard Team:  #3316 → #3319, #3317, #3318 ──────────┤ PARALLEL
└─ UI Team:         #3326 → #3328 → #3329 → #3334 ────────┘

WEEK 10-12: FASE 4 (55 SP)
├─ Navigation:      #3404 → #3405 → #3406 → #3407 ────────┐
├─ RAG Dashboard:   #3300 → #3301 → #3302 → #3303 ────────┤ PARALLEL
└─ Documentation:   #3401 → #3399 → #3398 ────────────────┘

WEEK 13-17: FASE 5 (85 SP)
├─ Backend:         #3414 → #3415 → #3416 → #3417 ────────┐
├─ Plugins:         #3418 → #3419 → #3420 → #3421... ─────┤ PARALLEL
└─ Frontend:        #3425 → #3426 → #3427 → #3428 ────────┘

WEEK 18-21: FASE 6 (60 SP)
└─ Visual Builder:  #3454 → #3456 → #3457 → #3458... ─────→ SEQUENTIAL

WEEK 22-26: FASE 7 (75 SP)
├─ Admin:           #3349 → #3350 → #3380 → #3381 ────────┐
├─ AI:              #3351 → #3352 → #3353 ────────────────┤ PARALLEL
├─ Sessions:        #3342 → #3343 → #3344 → #3345 ────────┤
└─ RAG Strategies:  #3357 → #3358 → #3359 → #3360 ────────┘

WEEK 27-30: FASE 8 (40 SP)
├─ Infrastructure:  #3367 → #3368 → #2968 → #2969 ────────┐
└─ Testing:         #3082 (E2E) ──────────────────────────┘ SEQUENTIAL
```

---

## 🏗️ EPIC STATUS OVERVIEW

| Epic | Priorità | Status | Open | Closed | Progress |
|------|:--------:|:------:|:----:|:------:|:--------:|
| #3490 Multi-Agent AI | 🔴 Critical | 🔄 Active | 7 | 5 | 42% |
| #3475 User Private Library | 🔴 Critical | ✅ Near Done | 1 | 3 | 75% |
| #3386 Agent Creation | 🔴 Critical | 🔄 Active | 4 | 2 | 33% |
| #3434 Tier-Strategy-Model | 🔴 Critical | 🔄 Active | 3 | 5 | 62% |
| #3327 Security & Quota | 🟡 High | ⏸️ Ready | 7 | 0 | 0% |
| #3315 AI Insights | 🟡 High | ⏸️ Ready | 7 | 0 | 0% |
| #3306 Dashboard Hub | 🟡 High | 🔄 Active | 7 | 18 | 72% |
| #3325 MeepleCard | 🟡 Medium | ⏸️ Ready | 6 | 0 | 0% |
| #3403 RAG Navigation | 🟡 Medium | ⏸️ Ready | 7 | 0 | 0% |
| #3299 RAG Dashboard | 🟡 Medium | ⏸️ Ready | 6 | 0 | 0% |
| #3413 Plugin Architecture | 🟡 Medium | ⛔ BLOCKED | 18 | 0 | 0% |
| #3453 Visual Strategy | 🟡 Medium | ⛔ BLOCKED | 15 | 0 | 0% |
| #3366 Infrastructure | 🟢 Medium | ⏸️ Ready | 2 | 0 | 0% |
| #3341 Game Session | 🟢 Medium | ⏸️ Future | 6 | 0 | 0% |
| #3348 Advanced Features | 🟢 Medium | ⏸️ Future | 7 | 0 | 0% |
| #3356 Advanced RAG | 🟢 Low | ⏸️ Future | 6 | 0 | 0% |
| #2967 CI/CD Infra | 🟢 Low | ⏸️ Future | 9 | 0 | 0% |

---

## 🎯 IMMEDIATE ACTIONS (This Week)

### 🔴 PRIORITÀ MASSIMA - Start Now

**Team Backend**:
```bash
git checkout main-dev && git pull
git checkout -b feature/issue-3479-private-pdf-upload
# Implement POST /api/v1/documents/private
```

**Team Frontend A**:
```bash
git checkout frontend-dev && git pull
git checkout -b feature/issue-3376-agent-wizard
# Implement Agent Creation Wizard
```

**Team Frontend B**:
```bash
git checkout frontend-dev && git pull
git checkout -b feature/issue-3440-tier-admin-ui
# Implement Admin UI for tier-strategy config
```

### ⚠️ ISSUE DA CHIUDERE (Duplicati)
- #3469 → Duplicate of #3465
- #3470 → Duplicate of #3466
- #3471 → Duplicate of #3467

### 📋 ALTRI ISSUE NON CATEGORIZZATI
| Issue | Titolo | Action |
|:-----:|--------|:------:|
| #3120 | Private Games & Catalog Proposal | Valutare |
| #2703 | S3-compatible storage | Fase 8 |

---

## 📊 METRICS & KPI

### Velocity Target
- **Sprint Velocity**: 25-30 SP/week (3 developers)
- **Issue Close Rate**: 8-12 issues/week
- **Epic Completion**: 1-2 epics/month

### Quality Gates
- **Test Coverage**: 85%+ FE, 90%+ BE
- **Code Review**: 100% PRs reviewed
- **Documentation**: Updated before close

### Timeline Summary
| Fase | Timeline | SP | Issues |
|:----:|:--------:|:--:|:------:|
| 1 | Week 1-2 | 41 | 10 |
| 2 | Week 3-5 | 56 | 14 |
| 3 | Week 6-9 | 70 | 20 |
| 4 | Week 10-12 | 55 | 20 |
| 5 | Week 13-17 | 85 | 18 |
| 6 | Week 18-21 | 60 | 18 |
| 7 | Week 22-26 | 75 | 19 |
| 8 | Week 27-30 | 40 | 11 |
| **TOTAL** | **~30 weeks** | **~470** | **116** |

---

**Maintainer**: PM Agent + Claude Code
**Last Updated**: 2026-02-03
