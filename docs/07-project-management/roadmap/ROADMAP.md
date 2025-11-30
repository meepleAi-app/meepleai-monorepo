# 🗺️ MeepleAI Master Roadmap 2025-2026

**Ultimo Aggiornamento**: 2025-11-30
**Issue Aperte**: **131**
**Timeline Rimanente**: 8-12 settimane (Dic 2025 - Feb 2026)
**Target MVP**: Fine Febbraio 2026
**Owner**: Engineering Team

---

## 📋 SEQUENZA DI ESECUZIONE

### 🎯 Critical Path (8-12 settimane)

```
Week 1-2   │ P0: UI Components Foundation (7 issue, 14-28h)
           │ ├─ #1828 MeepleAvatar Component
           │ ├─ #1829 BottomNav Component
           │ ├─ #1830 GameCard Component
           │ ├─ #1831 ChatMessage Component
           │ ├─ #1832 ConfidenceBadge Component
           │ ├─ #1833 CitationLink Component
           │ └─ #1834 QuickActions Component
           ↓
Week 3-5   │ P1: Playful Boardroom Pages + Backend Support (15 issue, 110-180h)
           │ ├─ Frontend Track (Sequential):
           │ │  ├─ #1835 Landing Page (Marketing)
           │ │  ├─ #1836 Dashboard Page (Post-Login)
           │ │  ├─ #1838 Game Catalog Page
           │ │  ├─ #1840 Chat Page (Sidebar + Context)
           │ │  └─ #1841 Game Detail Page
           │ └─ Backend Track (Parallel):
           │    ├─ #1797 Golden Dataset (1000 Q&A)
           │    ├─ #996 Annotation: Terraforming Mars
           │    ├─ #997 Annotation: Wingspan
           │    ├─ #998 Annotation: Azul
           │    ├─ #1000 First Accuracy Test
           │    ├─ #1010-1012 Additional Annotations
           │    ├─ #1019 Accuracy Validation (80%)
           │    └─ #1020 Performance Testing (P95 <3s)
           ↓
Week 6-8   │ P2: Testing + MVP Features (23 issue, 100-150h)
           │ ├─ Testing Track:
           │ │  ├─ #1823 Storybook Coverage Phase 2
           │ │  ├─ #1496 Visual Regression Tests
           │ │  ├─ #1497 Browser Matrix
           │ │  ├─ #1498 E2E Code Coverage
           │ │  └─ #1505-1511 Test Quality Improvements
           │ └─ Features Track:
           │    ├─ #989 Base Components Finalization
           │    ├─ #1001 QuestionInputForm
           │    ├─ #1003 GameSelector Dropdown
           │    ├─ #1004 Loading/Error States
           │    ├─ #1008 Error Handling Retry
           │    ├─ #1013 PDF Viewer Integration
           │    ├─ #1014 Citation Jump to Page
           │    ├─ #1016 Italian UI (200+ strings)
           │    ├─ #1017 Game Catalog API
           │    ├─ #1021 Bug Fixes & Polish
           │    ├─ #1022 Documentation Updates
           │    ├─ #1023 Phase 1A Checklist
           │    └─ #1436 Fix SWR + Zustand Duplication
           ↓
Week 9-11  │ P3: Infrastructure + Documentation (20 issue, 120-180h)
           │ ├─ Infrastructure Track:
           │ │  ├─ #1561-1570 HyperDX Migration Epic
           │ │  ├─ #701 Docker Resource Limits
           │ │  ├─ #702 Docker Compose Profiles
           │ │  ├─ #703 Traefik Reverse Proxy
           │ │  ├─ #704 Backup Automation
           │ │  ├─ #705 Infrastructure Monitoring
           │ │  ├─ #706 Operational Runbooks
           │ │  ├─ #707 docker-compose.override.yml
           │ │  └─ #818 Security Scan Review Process
           │ └─ Documentation Track:
           │    ├─ #1681 Update Legacy Documentation
           │    ├─ #1680 Audit Infrastructure Services
           │    ├─ #1679 Cleanup Legacy Comments
           │    ├─ #1677 Remove Obsolete Models
           │    ├─ #1676 Remove Backward Compatibility
           │    ├─ #1675 Implement Missing APIs
           │    ├─ #1674 Resolve Backend TODOs
           │    └─ #1570 Final Verification Checklist
           ↓
Week 12    │ 🚀 PRODUCTION LAUNCH
```

---

## 📊 Executive Summary

### 🎯 Nuova Strategia: Execution-First Roadmap

**Approccio**: Sequenza di esecuzione basata su **priorità + dipendenze**

Ogni issue è classificata con:
- **Tipo**: Frontend / Backend / Both / Infrastructure / E2E
- **Priorità**: P0 (Critical) / P1 (High) / P2 (Medium) / P3 (Low) / Deferred
- **Dipendenze**: Blockers espliciti
- **Effort**: Stima ore (Small: 2-4h, Medium: 4-8h, Large: 8-16h, XLarge: 16+h)

### 📊 Distribuzione Issue per Tipo

| Tipo | Count | % | Effort Totale |
|------|-------|---|---------------|
| **Frontend** | 45 | 34% | 200-280h |
| **Backend** | 25 | 19% | 120-180h |
| **Both** | 8 | 6% | 40-60h |
| **Infrastructure** | 18 | 14% | 100-140h |
| **E2E/Testing** | 15 | 11% | 60-90h |
| **Documentation** | 10 | 8% | 40-60h |
| **Deferred** | 60+ | 46% | Post-MVP |
| **TOTAL MVP** | **71** | 54% | **560-810h** |

### 🚀 Progress Overview

| Phase | Issue | Status | Timeline | Gate |
|-------|-------|--------|----------|------|
| **P0: UI Components** | 7 | 🔴 0% | 1-2 settimane | - |
| **P1: Playful Boardroom + Backend** | 15 | 🔴 0% | 3-4 settimane | P0 Complete |
| **P2: Testing + MVP Features** | 23 | 🔴 0% | 2-3 settimane | P1 Complete |
| **P3: Infrastructure + Docs** | 20 | 🔴 0% | 2-3 settimane | P2 Complete |
| **DEFERRED** | 60+ | ⚪ - | Post-MVP | - |
| **TOTAL MVP** | **65** | 🔴 0% | **8-12 settimane** | - |

---

## 🔥 SEQUENZA DI ESECUZIONE DETTAGLIATA

### 📍 P0: UI COMPONENTS FOUNDATION (7 Issue - 1-2 settimane)

**MUST COMPLETE FIRST** - Foundation per tutte le pagine Playful Boardroom

| # | Titolo | Tipo | Effort | Dipendenze | Link |
|---|--------|------|--------|------------|------|
| **#1828** | MeepleAvatar Component with States | Frontend | Small (2-4h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1828) |
| **#1829** | BottomNav Component (Mobile-First) | Frontend | Small (2-4h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1829) |
| **#1830** | GameCard Component (Grid/List variants) | Frontend | Small (2-4h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1830) |
| **#1831** | ChatMessage Component (User/AI) | Frontend | Small (2-4h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1831) |
| **#1832** | ConfidenceBadge Component | Frontend | Small (2-4h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1832) |
| **#1833** | CitationLink Component | Frontend | Small (2-4h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1833) |
| **#1834** | QuickActions Component | Frontend | Small (2-4h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1834) |

**Effort Totale**: 14-28h
**Timeline**: 1-2 settimane (parallelo dove possibile)
**Completion Gate**: 100% - MANDATORY per P1

**Execution Order**:
- **Week 1**: Tutti i componenti in parallelo (sviluppo indipendente)
- **Week 1-2**: Testing + Storybook stories per ogni component
- **Week 2**: Integration testing + accessibility validation

---

### 📍 P1: PLAYFUL BOARDROOM PAGES + BACKEND SUPPORT (15 Issue - 3-4 settimane)

**Dipendenze**: P0 al 100%
**Focus**: Nuove pagine user-facing + Golden dataset per quality

#### P1.1: Frontend Pages (5 issue - 30-60h)

| # | Titolo | Tipo | Effort | Deps | Link |
|---|--------|------|--------|------|------|
| **#1835** | Landing Page (Marketing) | Frontend | Large (8-12h) | P0 components | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1835) |
| **#1836** | Dashboard Page (Post-Login) | Frontend | Large (8-12h) | #1835 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1836) |
| **#1838** | Game Catalog Page (Hybrid View) | Frontend | Large (8-12h) | #1836 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1838) |
| **#1840** | Chat Page (Sidebar + Context) | Frontend | Large (8-12h) | #1838, backend dataset | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1840) |
| **#1841** | Game Detail Page (Tabs + Chat) | Frontend | Medium (6-8h) | #1838 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1841) |

**Subtotale**: 38-56h
**Execution**: Sequential con dependencies, alcune parti parallele

#### P1.2: Backend Golden Dataset (10 issue - 80-120h)

| # | Titolo | Tipo | Effort | Deps | Link |
|---|--------|------|--------|------|------|
| **#1797** | Generate Golden Dataset (1000 Q&A) | Backend | XLarge (20-30h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1797) |
| **#996** | Annotation: Terraforming Mars (20 Q&A) | Backend | Medium (6-8h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/996) |
| **#997** | Annotation: Wingspan (15 Q&A) | Backend | Medium (4-6h) | #996 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/997) |
| **#998** | Annotation: Azul (15 Q&A) | Backend | Medium (4-6h) | #997 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/998) |
| **#1000** | First Accuracy Test (Baseline) | Backend | Small (4-6h) | #996-998 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1000) |
| **#1010** | Annotation: Catan expansion + TTR (30 Q&A) | Backend | Large (8-12h) | #1000 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1010) |
| **#1011** | Annotation: 7 Wonders + Agricola + Splendor (30 Q&A) | Backend | Large (8-12h) | #1010 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1011) |
| **#1012** | Adversarial Dataset (50 synthetic) | Backend | Large (8-12h) | #1011 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1012) |
| **#1019** | Accuracy Validation (80% target on 100 Q&A) | Backend | Large (10-14h) | #1012 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1019) |
| **#1020** | Performance Testing (P95 latency <3s) | Backend | Large (8-12h) | #1019 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1020) |

**Subtotale**: 80-118h
**Deliverable**: 1000+ Q&A pairs, 80% accuracy validated, <3s P95 latency

**P1 Effort Totale**: 118-174h
**Timeline**: 3-4 settimane (2 tracks paralleli: Frontend + Backend)
**Completion Gate**: Playful Boardroom live, Golden dataset ready, Accuracy ≥80%

---

### 📍 P2: TESTING + MVP FEATURES (23 Issue - 2-3 settimane)

**Dipendenze**: P1 al 100%
**Focus**: Quality assurance + Feature completion

#### P2.1: Testing Infrastructure (8 issue - 40-60h)

| # | Titolo | Tipo | Effort | Deps | Link |
|---|--------|------|--------|------|------|
| **#1823** | Storybook Coverage Phase 2 - Visual Testing | Frontend | Medium (6-8h) | P0 components | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1823) |
| **#1496** | Visual Regression Tests | Frontend | Medium (6-8h) | #1823 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1496) |
| **#1497** | Browser Matrix (Firefox, Safari) | Frontend | Medium (6-8h) | #1496 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1497) |
| **#1498** | E2E Code Coverage Reporting | Frontend | Medium (4-6h) | #1497 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1498) |
| **#1505** | Reduce Magic Numbers in Tests (backend) | Backend | Small (2-4h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1505) |
| **#1506** | Consolidate with Theory Tests (backend) | Backend | Small (2-4h) | #1505 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1506) |
| **#1507** | Remove Excessive Regions (backend) | Backend | Small (2-4h) | #1506 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1507) |
| **#1508** | Add Error Boundary Tests (frontend) | Frontend | Medium (4-6h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1508) |
| **#1509** | Add Performance Tests (frontend) | Frontend | Medium (4-6h) | #1508 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1509) |
| **#1510** | Add API SDK Integration Tests (frontend) | Frontend | Medium (4-6h) | #1509 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1510) |
| **#1511** | Expand Visual Regression Tests (frontend) | Frontend | Medium (4-6h) | #1510 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1511) |

**Subtotale**: 44-66h (alcuni in parallelo)

#### P2.2: MVP Features Completion (15 issue - 60-90h)

| # | Titolo | Tipo | Effort | Deps | Link |
|---|--------|------|--------|------|------|
| **#989** | Base Components Finalization | Frontend | Large (8-10h) | P0 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/989) |
| **#1001** | QuestionInputForm Component | Frontend | Large (8-10h) | #989 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1001) |
| **#1003** | GameSelector Dropdown Component | Frontend | Medium (4-6h) | #989 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1003) |
| **#1004** | Loading and Error States (UI/UX) | Frontend | Medium (4-6h) | #989 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1004) |
| **#1008** | Error Handling and Retry Logic | Frontend | Medium (6-8h) | #1004 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1008) |
| **#1013** | PDF Viewer Integration (react-pdf) | Frontend | Large (8-10h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1013) |
| **#1014** | Citation Click → Jump to Page | Frontend | Medium (6-8h) | #1013 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1014) |
| **#1016** | Italian UI Strings (200+ translations) | Frontend | Large (12-16h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1016) |
| **#1017** | Game Catalog API Integration | Frontend | Large (8-10h) | #1838 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1017) |
| **#1436** | Fix SWR + Zustand State Duplication | Frontend | Medium (6-8h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1436) |
| **#994** | Frontend Build Optimization | Frontend | Large (8-10h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/994) |
| **#1021** | Final Bug Fixes and Polish | Both | Large (12-16h) | All P2 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1021) |
| **#1022** | Documentation Updates (User Guide, README) | Documentation | Large (8-10h) | #1021 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1022) |
| **#1023** | Phase 1A Completion Checklist | Backend | Medium (4-6h) | #1022 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1023) |

**Subtotale**: 102-134h

**P2 Effort Totale**: 146-200h
**Timeline**: 2-3 settimane (2 tracks paralleli: Testing + Features)
**Completion Gate**: All features complete, Tests passing, Documentation updated

---

### 📍 P3: INFRASTRUCTURE + DOCUMENTATION (20 Issue - 2-3 settimane)

**Dipendenze**: P2 al 100%
**Focus**: Observability migration + Production readiness

#### P3.1: HyperDX Migration (10 issue - 60-90h)

| # | Titolo | Tipo | Effort | Deps | Link |
|---|--------|------|--------|------|------|
| **#1561** | EPIC: Implement HyperDX Observability Platform | Infrastructure | - | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1561) |
| **#1562** | Deploy HyperDX Docker Container | Infrastructure | Medium (6-8h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1562) |
| **#1563** | Configure .NET API OpenTelemetry for HyperDX | Infrastructure | Large (8-12h) | #1562 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1563) |
| **#1564** | Remove Seq and Jaeger Services | Infrastructure | Small (2-4h) | #1563 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1564) |
| **#1565** | Integration Testing - Backend Telemetry | Infrastructure | Medium (6-8h) | #1564 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1565) |
| **#1566** | Implement HyperDX Browser SDK (Next.js) | Frontend | Medium (6-8h) | #1565 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1566) |
| **#1567** | Configure HyperDX Application Alerts | Infrastructure | Medium (4-6h) | #1566 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1567) |
| **#1568** | Load Testing and Performance Validation | Infrastructure | Large (8-12h) | #1567 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1568) |
| **#1569** | Update Documentation for HyperDX Migration | Documentation | Medium (4-6h) | #1568 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1569) |
| **#1570** | Final Verification and Go-Live Checklist | Documentation | Medium (4-6h) | #1569 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1570) |

**Subtotale**: 48-70h

#### P3.2: Docker & Infrastructure Improvements (7 issue - 40-60h)

| # | Titolo | Tipo | Effort | Deps | Link |
|---|--------|------|--------|------|------|
| **#701** | Add Resource Limits to Docker Services | Infrastructure | Medium (6-8h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/701) |
| **#702** | Docker Compose Profiles for Selective Startup | Infrastructure | Medium (4-6h) | #701 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/702) |
| **#703** | Add Traefik Reverse Proxy Layer | Infrastructure | Large (8-12h) | #702 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/703) |
| **#704** | Create Backup Automation Scripts | Infrastructure | Medium (6-8h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/704) |
| **#705** | Add Infrastructure Monitoring (cAdvisor + node-exporter) | Infrastructure | Large (8-12h) | #703 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/705) |
| **#706** | Create Operational Runbooks Documentation | Documentation | Medium (4-6h) | #705 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/706) |
| **#707** | Add docker-compose.override.yml Example | Infrastructure | Small (2-4h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/707) |
| **#818** | Security: Quarterly Security Scan Review Process | Infrastructure | Medium (4-6h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/818) |

**Subtotale**: 42-62h

#### P3.3: Documentation & Cleanup (10 issue - 40-60h)

| # | Titolo | Tipo | Effort | Deps | Link |
|---|--------|------|--------|------|------|
| **#1681** | Update Legacy Documentation References | Documentation | Medium (4-6h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1681) |
| **#1680** | Audit Infrastructure Services | Documentation | Medium (4-6h) | #1681 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1680) |
| **#1679** | Cleanup Legacy Comments and Deprecation Markers | Code Quality | Small (2-4h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1679) |
| **#1677** | Remove Obsolete Data Models | Backend | Medium (4-6h) | #1679 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1677) |
| **#1676** | Remove Backward Compatibility Layers | Both | Medium (6-8h) | #1677 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1676) |
| **#1675** | Implement Missing Frontend Backend APIs | Both | Large (8-12h) | - | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1675) |
| **#1674** | Resolve Backend TODO Comments | Backend | Medium (4-6h) | #1675 | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1674) |

**Subtotale**: 32-48h

**P3 Effort Totale**: 122-180h
**Timeline**: 2-3 settimane
**Completion Gate**: HyperDX live, Infrastructure hardened, Documentation complete

---

### 📦 DEFERRED: POST-MVP (60+ Issue - Post Febbraio 2026)

**Status**: ⚪ Non prioritarie per MVP
**Timeline**: 2026 Q2-Q3

#### Epic: Admin Console (4 Phases - 35 issue)

| Epic # | Fase | Issue Count | Effort | Link |
|--------|------|-------------|--------|------|
| **#874** | FASE 1: Dashboard Overview | 10 | 40-60h | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/874) |
| **#890** | FASE 2: Infrastructure Monitoring | 10 | 60-80h | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/890) |
| **#903** | FASE 3: Enhanced Management (API Keys + Users) | 12 | 80-100h | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/903) |
| **#915** | FASE 4: Advanced Features (Reporting + Alerting) | 8 | 60-80h | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/915) |

**Totale Admin Console**: 40 issue, 240-320h

#### Epic: Frontend Modernization (6 Epics - 20+ issue)

| Epic # | Fase | Effort | Link |
|--------|------|--------|------|
| **#926** | Foundation & Quick Wins (Phase 1) | 40-60h | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/926) |
| **#931** | React 19 Optimization (Phase 2) | 60-80h | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/931) |
| **#933** | App Router Migration (Phase 3) | 80-100h | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/933) |
| **#932** | Advanced Features (Phase 4) | 60-80h | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/932) |
| **#934** | Design Polish (Phase 5) | 40-60h | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/934) |
| **#935** | Performance & Accessibility (Phase 6) | 60-80h | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/935) |

**Totale Frontend Modernization**: 340-460h

#### Altri Deferred

| # | Titolo | Effort | Link |
|---|--------|--------|------|
| **#936** | Spike: POC Infisical Secret Rotation (Phase 2) | 16-24h | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/936) |
| **#844** | Epic: UI/UX Automated Testing Roadmap 2025 | 60-80h | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/844) |
| **#1817** | K6 Performance Tests Failed - Investigation | 8-12h | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1817) |
| **#1821** | Improve PDF Background Processing Reliability | 12-16h | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1821) |
| **#1820** | Optimize PDF Upload Test Execution Performance | 8-12h | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1820) |
| **#1737** | Unreliable GC.Collect() in Performance Tests | 4-6h | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1737) |
| **#1725** | Enhancement: LLM Token Tracking - Advanced Features | 12-16h | [Link](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1725) |

**Deferred Effort Totale**: 700-950h
**Rationale**: MVP-first, queste enhanceranno features esistenti ma non sono richieste per launch

---

## 📊 EXECUTION DASHBOARD

### Critical Path Timeline

```
Week 1-2    │ P0: UI Components Foundation (7 issue, 14-28h)
            │ ├─ #1828-1834 All components in parallel
            │ └─ Integration + Accessibility testing
            ↓
Week 3-5    │ P1: Playful Boardroom + Backend (15 issue, 118-174h)
            │ ├─ Frontend Track (Sequential):
            │ │  Landing → Dashboard → Catalog → Chat → Detail
            │ └─ Backend Track (Parallel):
            │    Golden Dataset + Annotations + Accuracy tests
            ↓ GATE: Playful Boardroom Live + 80% Accuracy
            ↓
Week 6-8    │ P2: Testing + Features (23 issue, 146-200h)
            │ ├─ Testing Track: Storybook → E2E → Visual
            │ └─ Features Track: Components + PDF + i18n
            ↓ GATE: All features complete, Tests passing
            ↓
Week 9-11   │ P3: Infrastructure + Docs (20 issue, 122-180h)
            │ ├─ HyperDX Migration (sequential)
            │ ├─ Docker Improvements (parallel)
            │ └─ Documentation Cleanup (parallel)
            ↓ GATE: Production ready
            ↓
Week 12     │ 🚀 PRODUCTION LAUNCH
```

### Resource Allocation by Type

| Type | P0 | P1 | P2 | P3 | Total Issue | Total Effort |
|------|----|----|----|----|-------------|--------------|
| **Frontend** | 7 | 5 | 11 | 1 | **24** (37%) | 180-260h |
| **Backend** | 0 | 10 | 3 | 1 | **14** (22%) | 120-180h |
| **Both** | 0 | 0 | 3 | 3 | **6** (9%) | 40-60h |
| **Infrastructure** | 0 | 0 | 0 | 16 | **16** (25%) | 90-132h |
| **Testing** | 0 | 0 | 11 | 0 | **11** (17%) | 44-66h |
| **Documentation** | 0 | 0 | 2 | 6 | **8** (12%) | 32-48h |
| **Total MVP** | **7** | **15** | **23** | **20** | **65** | **506-746h** |

**Team Size**: 2-3 developers full-time
**Timeline**: 8-12 settimane
**Velocity Required**: 5-8 issue/settimana

### Dependencies Graph

```
P0 UI Components (#1828-1834)
│
├─ P1.1 Frontend Pages (#1835-1841)
│  │
│  └─ P2.2 MVP Features (#989, 1001-1004, 1008, 1013-1014, 1016-1017, 1436, 994)
│     │
│     └─ P3.3 Documentation Cleanup (#1681, 1680, 1679, 1677, 1676, 1675, 1674)
│
└─ P1.2 Backend Dataset (#1797, 996-998, 1000, 1010-1012, 1019-1020)
   │
   └─ P2.1 Testing Infrastructure (#1823, 1496-1498, 1505-1511)
      │
      └─ P3.1 HyperDX Migration (#1561-1570)
         │
         └─ P3.2 Docker & Infra (#701-707, 818)
            │
            └─ 🚀 PRODUCTION LAUNCH
```

---

## 🚨 CRITICAL SUCCESS FACTORS

### 🔥 Top 5 Risks & Mitigations

| Risk | Impact | Probability | Mitigation | Owner |
|------|--------|-------------|------------|-------|
| **UI Components delay blocks P1** | 🔴 CRITICAL | Medium | Parallel development, daily standup tracking | Frontend Lead |
| **Golden Dataset quality insufficient** | 🟡 HIGH | Medium | 2+ reviewers per annotation, quality rubric | Product |
| **HyperDX migration disrupts observability** | 🟡 HIGH | Low | Blue-green deployment, rollback plan ready | DevOps |
| **Accuracy <80% on validation** | 🟡 HIGH | Medium | Early baseline (#1000), iterative tuning | ML Team |
| **Testing phase finds critical bugs** | 🟢 MEDIUM | High | Early testing during P1, continuous QA | QA Team |

### 🎯 Quality Gates

| Gate | Criteria | Blocker | Phase |
|------|----------|---------|-------|
| **G1: P0 Complete** | All 7 UI components ready, tested, documented | Yes | Week 2 |
| **G2: P1 Complete** | Playful Boardroom live, Golden dataset 1000+, Accuracy ≥80% | Yes | Week 5 |
| **G3: P2 Complete** | All MVP features working, Tests passing, Documentation updated | Yes | Week 8 |
| **G4: P3 Complete** | HyperDX live, Infrastructure hardened, Runbooks ready | Yes | Week 11 |
| **G5: Launch Ready** | All gates passed, final verification checklist complete | Yes | Week 12 |

### 📈 KPI Targets

| KPI | Current | Target | Gate |
|-----|---------|--------|------|
| **Frontend Coverage** | 90%+ | ≥90% | ✅ Met |
| **Backend Coverage** | 90%+ | ≥90% | ✅ Met |
| **E2E Coverage** | 60% | ≥80% | G3 |
| **Accuracy (Golden)** | 75% | ≥80% | G2 |
| **P95 Latency** | 2.5s | <3s | ✅ Met |
| **TTFT Streaming** | 800ms | <1s | ✅ Met |
| **Lighthouse Score** | 90+ | ≥90 | ✅ Met |
| **Playful Boardroom Pages** | 0/5 | 5/5 | G2 🔥 |

---

## 🎉 NEXT ACTIONS (Prossime 48h)

### Immediate Priorities (P0 - Week 1)

**Day 1-2** (2025-12-01 to 2025-12-02):
1. 🚀 **#1828** - MeepleAvatar Component (2-4h) - Foundation component
2. 🚀 **#1829** - BottomNav Component (2-4h) - Mobile navigation
3. 🚀 **#1830** - GameCard Component (2-4h) - Core display component

**Day 3-4** (2025-12-03 to 2025-12-04):
4. 🚀 **#1831** - ChatMessage Component (2-4h) - Chat interface
5. 🚀 **#1832** - ConfidenceBadge Component (2-4h) - Quality indicator
6. 🚀 **#1833** - CitationLink Component (2-4h) - Citation references

**Day 5-7** (2025-12-05 to 2025-12-07):
7. 🚀 **#1834** - QuickActions Component (2-4h) - User actions
8. ✅ **P0 Integration Testing** - All components together
9. ✅ **P0 Accessibility Validation** - WCAG compliance check

### Week 2 Actions (P1 Start)

**Prerequisiti**: P0 al 100%

**Parallel Tracks**:
- **Track 1 (Frontend)**: #1835 Landing Page → #1836 Dashboard
- **Track 2 (Backend)**: #1797 Golden Dataset generation → #996 Terraforming Mars annotation

**Goal Week 2**: 3-4 issue P1 completate, foundation pronta

---

## 📝 CHANGELOG

### v14.0 (2025-11-30) - PLAYFUL BOARDROOM FOCUS

**Nuova Struttura**:
- ✅ 131 issue da GitHub (65 MVP + 60+ Deferred)
- ✅ Focus principale su Playful Boardroom (nuove pagine user-facing)
- ✅ UI Components come critical foundation (P0)
- ✅ Golden Dataset backend support parallelo
- ✅ HyperDX migration in P3 (infrastructure ready)
- ✅ Timeline realistica 8-12 settimane
- ✅ Tutti i link GitHub inclusi

**Differenze da v13.0**:
- Priorità spostata da copertura test a nuove feature
- Playful Boardroom (5 pagine) come P1 core
- UI Components (7 componenti) come P0 foundation
- HyperDX migration da P1 a P3 (dopo features complete)
- Deferred espanso (60+ issue vs 28 precedenti)
- Admin Console (40 issue) rinviato post-MVP

**Rationale**:
Focus su **user-facing features** per MVP. Playful Boardroom offre valore immediato agli utenti, testing e infrastructure seguono dopo feature delivery.

---

**Versione**: 14.0 (Playful Boardroom Focus)
**Owner**: Engineering Team
**Ultimo Aggiornamento**: 2025-11-30
**Issue Totali**: 131 (65 MVP + 66 Deferred)
**Effort to MVP**: 506-746h (8-12 settimane, 2-3 devs)
**Target Launch**: Fine Febbraio 2026

---

🚀 **"Execute with precision, deliver with excellence"** 🚀
