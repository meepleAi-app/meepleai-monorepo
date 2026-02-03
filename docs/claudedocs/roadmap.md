# MeepleAI - Roadmap Completa

**Data**: 2026-02-01
**Issue Aperte**: 101 (+12 nuove)
**Story Points Totali**: ~325 SP

---

## 📊 EXECUTIVE SUMMARY

### Flusso USER Principale
```
Creazione Gioco → Caricamento PDF → Stato PDF→Vettore → Creare Agente (Strategy/Template/Model) → Domande con KB
```

### Flusso ADMIN Principale
```
BGG Import → Upload PDF → Edit Gioco → Test Agente (Strategie/Modelli) → Visualizza Risultati
```

| Step | Backend | Frontend | Status |
|------|:-------:|:--------:|:------:|
| 1. Creazione Gioco | ✅ Completo | ⚠️ Parziale | 🟡 80% |
| 2. Caricamento PDF | ✅ Completo | ✅ Completo | 🟢 95% |
| 3. Stato PDF→Vettore | ✅ Completo | ❌ Mancante | 🔴 50% |
| 4. Creare Agente | ⚠️ Parziale | ❌ Mancante | 🔴 40% |
| 5. Domande con KB | ✅ Completo | ⚠️ Parziale | 🟡 60% |
| 6. Test Agente (Admin) | ⚠️ Parziale | ❌ Mancante | 🔴 30% |

### Issue per Priorità
| Priority | Count | SP |
|----------|:-----:|:--:|
| 🔴 Critical | 17 | 48 |
| 🟠 High | 18 | 74 |
| 🟡 Medium | 50 | ~158 |
| 🟢 Low | 17 | ~48 |

---

## 🎯 EPIC ATTIVE (12)

| # | Epic | Priority | Issues | SP |
|---|------|:--------:|:------:|:--:|
| [#3386](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3386) | **Agent Creation & Testing Flow** | 🔴 Critical | 11 | 45 |
| [#3306](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3306) | User Dashboard Hub Core - MVP | 🔴 Critical | 8 | 21 |
| [#3315](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3315) | AI Insights & Recommendations | 🟠 High | 4 | 14 |
| [#3299](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3299) | RAG Dashboard Enhancement | 🟠 High | 6 | 22 |
| [#3327](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3327) | User Flow Gaps - Security & Quota | 🟠 High | 5 | 21 |
| [#3325](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3325) | MeepleCard - Universal Card System | 🟠 High | 5 | - |
| [#3005](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3005) | Test Coverage & Quality Improvement | 🟠 High | 2 | - |
| [#3320](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3320) | Gamification & Advanced Dashboard | 🟡 Medium | 4 | 11 |
| [#3341](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3341) | Game Session Toolkit Phase 2 | 🟡 Medium | 6 | 21 |
| [#3348](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3348) | Advanced Features - AI, Admin & Collaboration | 🟡 Medium | 6 | 22 |
| [#3366](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3366) | Infrastructure Enhancements | 🟡 Medium | 2 | 8 |
| [#3356](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3356) | Advanced RAG Strategies (9 Variants) | 🟢 Low | 9 | 35 |
| [#2967](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2967) | Zero-Cost CI/CD Infrastructure | 🟡 Medium | 7 | - |

---

## 🔴 CRITICAL PRIORITY (17 issues - 48 SP)

### 🆕 Agent Creation & Testing Flow - EPIC #3386

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3375](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3375) | Agent Session Launch API Integration | Frontend | 3 | #3386 |
| [#3376](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3376) | Agent Creation with Strategy/Template/Model Selection | Frontend | 5 | #3386 |
| [#3377](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3377) | Models Tier Endpoint (Free/Normal/Premium) | Backend | 3 | #3386 |
| [#3378](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3378) | Agent Test Execution UI for Admin | Frontend | 5 | #3386 |
| [#3379](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3379) | Agent Test Results History & Persistence | Backend+FE | 5 | #3386 |

### Flusso Prioritario - PDF Processing UI

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3371](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3371) | [PDF] Fix ProcessingProgressSchema mismatch | Frontend | 1 | - |
| [#3370](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3370) | [PDF] Frontend: usePdfProcessingProgress hook | Frontend | 2 | - |
| [#3369](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3369) | [PDF] Frontend: Processing Progress UI Component | Frontend | 3 | - |

### Dashboard Hub Core - MVP

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3307](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3307) | Create route structure and layout skeleton | Frontend | 2 | #3306 |
| [#3308](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3308) | HeroStats component with 4 KPI cards | Frontend | 3 | #3306 |
| [#3309](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3309) | ActiveSessionsWidget | Frontend | 2 | #3306 |
| [#3310](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3310) | LibrarySnapshot component | Frontend | 3 | #3306 |
| [#3312](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3312) | ChatHistorySection | Frontend | 2 | #3306 |
| [#3313](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3313) | QuickActionsGrid | Frontend | 1 | #3306 |
| [#3314](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3314) | Backend: Dashboard aggregated API endpoint | Backend | 5 | #3306 |

---

## 🟠 HIGH PRIORITY (20 issues - 74 SP)

### 🆕 Agent Creation & Testing Flow (High Priority)

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3380](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3380) | Strategy Comparison Side-by-Side UI | Frontend | 5 | #3386 |
| [#3381](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3381) | Typology Approval Workflow Endpoint | Backend | 3 | #3386 |
| [#3382](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3382) | Agent Metrics Dashboard | Frontend | 5 | #3386 |
| [#3383](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3383) | Cost Estimation Preview Before Launch | Frontend+BE | 3 | #3386 |

### AI & Dashboard

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3311](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3311) | ActivityFeed timeline | Frontend | 3 | #3306 |
| [#3316](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3316) | AiInsightsWidget with RAG-powered suggestions | Frontend | 5 | #3315 |
| [#3319](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3319) | Backend: AI Insights RAG endpoint | Backend | 5 | #3315 |
| [#3324](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3324) | SSE real-time dashboard updates | Backend | 5 | - |

### Flusso Prioritario

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3372](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3372) | [Game] Link PDF to Game during creation | Backend+FE | 3 | - |

### Security & Quota

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3330](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3330) | Implement Session Limits Enforcement | Backend+FE | 5 | #3327 |
| [#3332](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3332) | Implement Email Verification for New Users | Backend+FE | 5 | #3327 |
| [#3339](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3339) | Account Lockout After Failed Login Attempts | Backend | 3 | #3327 |

### MeepleCard System

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3326](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3326) | Core component implementation & refinement | Frontend | - | #3325 |
| [#3331](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3331) | Migration: Replace GameCard with MeepleCard | Frontend | - | #3325 |

### Testing

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3082](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3082) | Implement Missing E2E Test Flows (50 flows) | Testing | - | #3005 |

---

## 🟡 MEDIUM PRIORITY (50 issues - ~158 SP)

### 🆕 Agent Creation & Testing Flow (Medium Priority)

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3384](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3384) | Game Image Upload Component | Frontend | 3 | #3386 |
| [#3385](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3385) | BGG Bulk Import Feature | Frontend+BE | 5 | #3386 |

### Dashboard Widgets

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3317](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3317) | WishlistHighlights widget | Frontend | 2 | #3315 |
| [#3318](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3318) | CatalogTrending widget | Frontend | 2 | #3315 |
| [#3321](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3321) | AchievementsWidget with badges | Frontend | 3 | #3320 |
| [#3322](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3322) | Advanced ActivityFeed filters and search | Frontend | 3 | #3320 |
| [#3323](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3323) | Responsive polish and performance optimization | Frontend | 2 | #3320 |

### RAG Dashboard Enhancement

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3300](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3300) | Strategy Cards Component - 6 RAG Strategies UI | Frontend | 3 | #3299 |
| [#3301](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3301) | Strategy Detail Modal | Frontend | 3 | #3299 |
| [#3302](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3302) | Performance Metrics Dashboard | Frontend | 5 | #3299 |
| [#3303](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3303) | Configuration Panel - Strategy Parameters | Frontend | 3 | #3299 |
| [#3304](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3304) | Backend API - Metrics, Config, Streaming | Backend | 5 | #3299 |
| [#3305](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3305) | RAG Dashboard Test Suite | Testing | 3 | #3299 |

### Game Session Toolkit Phase 2

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3342](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3342) | Dice Roller Component & Backend | Frontend+BE | 3 | #3341 |
| [#3343](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3343) | Card Deck System (Standard + Custom) | Frontend+BE | 5 | #3341 |
| [#3344](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3344) | Private Notes with Obscurement | Frontend+BE | 3 | #3341 |
| [#3346](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3346) | Offline-First PWA with Service Worker | Frontend | 5 | #3341 |

### Advanced Features - AI, Admin & Collaboration

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3338](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3338) | AI Token Usage Tracking per User | Frontend+BE | 5 | #3348 |
| [#3340](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3340) | Login Device Tracking and Management | Frontend+BE | 3 | #3348 |
| [#3349](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3349) | User Impersonation for Support/Debug | Frontend+BE | 3 | #3348 |
| [#3350](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3350) | Batch Approval/Rejection for Games | Frontend+BE | 2 | #3348 |
| [#3352](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3352) | AI Response Feedback System (Thumbs Up/Down) | Frontend+BE | 3 | #3348 |
| [#3353](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3353) | Similar Games Discovery with RAG | Frontend+BE | 5 | #3348 |
| [#3354](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3354) | Session Invite Links for Collaborative Play | Frontend+BE | 3 | #3348 |
| [#3355](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3355) | Game/Document Version History and Comparison UI | Frontend | 3 | #3348 |

### Admin & Configuration

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3333](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3333) | PDF Upload Limits Configuration UI | Frontend+BE | 3 | #3327 |
| [#3335](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3335) | Tier-Based Feature Access (Free/Normal/Premium) | Frontend+BE | 5 | #3327 |

### MeepleCard System

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3328](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3328) | Unit tests & accessibility tests | Frontend | - | #3325 |
| [#3329](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3329) | Storybook documentation & visual testing | Frontend | - | #3325 |
| [#3334](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3334) | Integration with SharedGameCatalog & Dashboard | Frontend | - | #3325 |
| [#3336](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3336) | Documentation & usage guide | Frontend | - | #3325 |

### Infrastructure

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3367](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3367) | Log Aggregation System | Infra | 5 | #3366 |
| [#3368](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3368) | k6 Load Testing Infrastructure | Infra | 3 | #3366 |
| [#2703](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2703) | S3-compatible object storage | Backend | - | - |

### CI/CD Infrastructure

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#2968](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2968) | Oracle Cloud Setup & VM Provisioning | Infra | - | #2967 |
| [#2969](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2969) | GitHub Actions Runner Installation | Infra | - | #2967 |
| [#2970](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2970) | Workflow Migration to Self-Hosted Runner | Infra | - | #2967 |
| [#2972](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2972) | Performance Monitoring & Reliability Check | Infra | - | #2967 |
| [#2973](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2973) | Cost Validation on GitHub Billing | Infra | - | #2967 |
| [#2974](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2974) | Setup Monitoring (Prometheus + Grafana) | Infra | - | #2967 |
| [#2975](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2975) | Document Troubleshooting Procedures | Docs | - | #2967 |
| [#2976](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2976) | Create Maintenance Schedule Automation | Infra | - | #2967 |

### Flusso Prioritario - Chat

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3373](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3373) | [Chat] Integrate streaming SSE in Ask page | Frontend | 3 | - |
| [#3374](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3374) | [PDF] Cancel processing button UI | Frontend | 2 | - |

### Other

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3120](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3120) | Private Games & Catalog Proposal System | Feature | - | - |

---

## 🟢 LOW PRIORITY (17 issues - ~48 SP)

### Game Session Toolkit Phase 2

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3345](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3345) | Timer, Coin Flip, Wheel Spinner Tools | Frontend | 2 | #3341 |
| [#3347](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3347) | Session Sharing (PDF Export, Social) | Frontend+BE | 3 | #3341 |

### AI Features

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3351](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3351) | Voice-to-Text Input for AI Questions | Frontend | 3 | #3348 |

### Advanced RAG Strategies (9 Variants)

| # | Titolo | Area | SP | Epic |
|---|--------|------|:--:|:----:|
| [#3357](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3357) | Implement Sentence Window RAG Strategy | Backend | 3 | #3356 |
| [#3358](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3358) | Implement Iterative RAG Strategy | Backend | 5 | #3356 |
| [#3359](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3359) | Implement Multi-Agent RAG Strategy | Backend | 5 | #3356 |
| [#3360](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3360) | Implement Step-Back Prompting Strategy | Backend | 3 | #3356 |
| [#3361](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3361) | Implement Query Expansion Strategy | Backend | 3 | #3356 |
| [#3362](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3362) | Implement Multi-Agent RAG Strategy | Backend | 5 | #3356 |
| [#3363](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3363) | Implement RAG-Fusion Strategy | Backend | 5 | #3356 |
| [#3364](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3364) | Implement Step-Back Prompting Strategy | Backend | 3 | #3356 |
| [#3365](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3365) | Implement Query Expansion Strategy | Backend | 3 | #3356 |

---

## 🗓️ ROADMAP SEQUENZIALE - FLUSSO PRIORITARIO

### PHASE 0: Prerequisiti Infrastrutturali
**Obiettivo**: SSE infrastructure ready

```
┌─────────────────────────────────────────────────────────────────────────┐
│  BACKEND                                                                 │
│  ──────                                                                  │
│  [#3324] SSE real-time updates ────────────────────────────────► 5 SP   │
│          Prerequisito per progress real-time                            │
└─────────────────────────────────────────────────────────────────────────┘
```

---

### PHASE 1: Creazione Gioco Completa
**Obiettivo**: Gioco creato con PDF associato

```
┌─────────────────────────────────┬───────────────────────────────────────┐
│  BACKEND                        │  FRONTEND                             │
│  ────────                       │  ─────────                            │
│                                 │                                       │
│  [#3372] Link PDF to Game ──────┼──► Fix GameCreationStep.tsx          │
│          CreateGameCommand +    │      usare pdfId                      │
│          pdfId field (3 SP)     │                                       │
│                                 │                                       │
└─────────────────────────────────┴───────────────────────────────────────┘
                                  │
                           ═══════╧═══════
                           ║ CHECKPOINT 1 ║
                           ║ Merge → main ║
                           ═══════════════
```

---

### PHASE 2: Visualizzazione Stato Processing
**Obiettivo**: Utente vede progresso PDF→Vettore

```
┌─────────────────────────────────┬───────────────────────────────────────┐
│  BACKEND                        │  FRONTEND                             │
│  ────────                       │  ─────────                            │
│                                 │                                       │
│  (già completo)                 │  [#3371] Fix Schema ────────────► 1 SP│
│  GET /pdfs/{id}/progress ✅     │         pdf.schemas.ts                │
│                                 │                                       │
│                                 │  [#3370] Hook ──────────────────► 2 SP│
│                                 │         usePdfProcessingProgress      │
│                                 │                                       │
│                                 │  [#3369] Component ─────────────► 3 SP│
│                                 │         PdfProcessingProgressBar      │
│                                 │                                       │
│                                 │  [#3374] Cancel Button ─────────► 2 SP│
│                                 │         cancelProcessing()            │
│                                 │                                       │
└─────────────────────────────────┴───────────────────────────────────────┘
                                  │
                           ═══════╧═══════
                           ║ CHECKPOINT 2 ║
                           ║ Merge → main ║
                           ═══════════════
```

---

### PHASE 3: Chat con KB del Gioco
**Obiettivo**: Domande funzionanti con risposte da PDF

```
┌─────────────────────────────────┬───────────────────────────────────────┐
│  BACKEND                        │  FRONTEND                             │
│  ────────                       │  ─────────                            │
│                                 │                                       │
│  (già completo)                 │  [#3373] Streaming SSE ──────────► 3 SP│
│  POST /agents/qa/stream ✅      │         BoardGameAskClient            │
│  POST /knowledge-base/ask ✅    │         useStreamingChat              │
│                                 │                                       │
│  [#3319] AI Insights endpoint   │  Citazioni + Confidence               │
│          (opzionale) 5 SP       │  (già presente, da integrare)         │
│                                 │                                       │
└─────────────────────────────────┴───────────────────────────────────────┘
                                  │
                           ═══════╧═══════
                           ║ CHECKPOINT 3 ║
                           ║ FLUSSO COMPLETO
                           ═══════════════
```

---

## 📈 TIMELINE PARALLELA

```
Settimana 1
══════════════════════════════════════════════════════════════════════════

Giorno 1-2
┌──────────────────────────────────┬──────────────────────────────────────┐
│         BACKEND                  │           FRONTEND                   │
├──────────────────────────────────┼──────────────────────────────────────┤
│ [#3324] SSE Infrastructure       │ [#3371] Fix Schema                   │
│ 5 SP                             │ 1 SP                                 │
├──────────────────────────────────┼──────────────────────────────────────┤
│ [#3372] PDF-Game Link            │ [#3372] GameCreationStep fix         │
│ 2 SP                             │ 1 SP                                 │
└──────────────────────────────────┴──────────────────────────────────────┘
                                   ↓
                        ════════════════════════
                        ║    CHECKPOINT 1      ║
                        ║ Merge main-dev→main  ║
                        ║ Test: Game+PDF link  ║
                        ════════════════════════

Giorno 3-4
┌──────────────────────────────────┬──────────────────────────────────────┐
│         BACKEND                  │           FRONTEND                   │
├──────────────────────────────────┼──────────────────────────────────────┤
│ [#3314] Dashboard API            │ [#3370] usePdfProcessingProgress     │
│ 5 SP                             │ 2 SP                                 │
│                                  ├──────────────────────────────────────┤
│                                  │ [#3369] PdfProcessingProgressBar     │
│                                  │ 3 SP                                 │
│                                  ├──────────────────────────────────────┤
│                                  │ [#3374] Cancel Button                │
│                                  │ 2 SP                                 │
└──────────────────────────────────┴──────────────────────────────────────┘
                                   ↓
                        ════════════════════════
                        ║    CHECKPOINT 2      ║
                        ║ Merge main-dev→main  ║
                        ║ Test: Progress UI    ║
                        ════════════════════════

Giorno 5
┌──────────────────────────────────┬──────────────────────────────────────┐
│         BACKEND                  │           FRONTEND                   │
├──────────────────────────────────┼──────────────────────────────────────┤
│ [#3319] AI Insights endpoint     │ [#3373] Streaming Chat               │
│ 5 SP                             │ 3 SP                                 │
└──────────────────────────────────┴──────────────────────────────────────┘
                                   ↓
                        ════════════════════════
                        ║    CHECKPOINT 3      ║
                        ║ FLUSSO COMPLETO ✅   ║
                        ════════════════════════
```

---

## 📊 RIEPILOGO PER AREA

### Frontend (45 issues)

| Category | Count | Priority Issues |
|----------|:-----:|-----------------|
| Dashboard Hub | 12 | #3307-#3323 |
| PDF Processing | 4 | #3369-#3371, #3374 |
| RAG Dashboard | 4 | #3300-#3303 |
| MeepleCard | 5 | #3326-#3336 |
| Game Session | 6 | #3342-#3347 |
| Other | 14 | Various |

### Backend (28 issues)

| Category | Count | Priority Issues |
|----------|:-----:|-----------------|
| Dashboard API | 2 | #3314, #3319 |
| Infrastructure | 3 | #3324, #3367, #3368 |
| Security | 3 | #3330, #3332, #3339 |
| RAG Strategies | 9 | #3357-#3365 |
| Other | 11 | Various |

### Infrastructure (10 issues)

| Category | Count | Priority Issues |
|----------|:-----:|-----------------|
| CI/CD | 7 | #2968-#2976 |
| Monitoring | 3 | #3366-#3368, #2974 |

### Testing (6 issues)

| Category | Count | Priority Issues |
|----------|:-----:|-----------------|
| E2E Tests | 1 | #3082 |
| Coverage | 1 | #3005 |
| RAG Tests | 1 | #3305 |
| Component Tests | 3 | #3328-#3329 |

---

## ✅ DEFINIZIONE DI DONE - Flusso Completo

### Scenario End-to-End

```gherkin
Scenario: Utente crea gioco, carica PDF, fa domanda
  Given sono autenticato come Editor

  When creo un nuovo gioco "Catan" dal wizard
  And carico il PDF del regolamento (10 pagine)
  Then vedo il progresso: Uploading → Extracting → Chunking → Embedding → Indexing
  And dopo ~2 minuti vedo "Completed ✓"

  When vado alla pagina chat del gioco
  And chiedo "Come si costruisce una strada?"
  Then ricevo una risposta con citazione dalla pagina 5
  And vedo il confidence score ≥70%
```

### Checklist Tecnica

- [ ] `POST /api/v1/games` accetta `pdfId` opzionale
- [ ] `GET /api/v1/pdfs/{id}/progress` restituisce `ProcessingProgress` completo
- [ ] Frontend visualizza progress bar con tutti gli step
- [ ] Frontend permette cancel processing
- [ ] `POST /api/v1/agents/qa` filtra per `gameId` corretto
- [ ] Risposte includono `citations` con `pageNumber`
- [ ] Test E2E copre l'intero flusso

---

## 📎 RIFERIMENTI FILE CHIAVE

### Backend
- `apps/api/src/Api/Routing/GameEndpoints.cs:103-111`
- `apps/api/src/Api/Routing/PdfEndpoints.cs:138-145`
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.cs`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Handlers/AskQuestionQueryHandler.cs`

### Frontend
- `apps/web/src/app/admin/wizard/steps/GameCreationStep.tsx`
- `apps/web/src/components/pdf/PdfUploadForm.tsx`
- `apps/web/src/lib/api/schemas/pdf.schemas.ts`
- `apps/web/src/app/(public)/board-game-ai/ask/BoardGameAskClient.tsx`

---

*Roadmap aggiornata da Claude Code - 2026-02-01*
*Totale Issue: 101 | Story Points: ~325 SP*
*+12 nuove issue per Agent Creation & Testing Flow (EPIC #3386)*
