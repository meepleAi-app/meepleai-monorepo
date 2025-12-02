# MeepleAI Master Roadmap 2025-2026

**Ultimo Aggiornamento**: 2025-12-02
**Issue Aperte**: **49** (MVP) + **~60** (Deferred)
**Timeline Rimanente**: 6-10 settimane (Dic 2025 - Feb 2026)
**Target MVP**: Fine Febbraio 2026

---

## SEQUENZA DI ESECUZIONE

### Priority Queue (Issue da lavorare)

```
PARALLELIZZAZIONE: Frontend e Backend/Infra possono procedere in parallelo dove indicato

Week 1-2   | FRONTEND TRACK                      | BACKEND/INFRA TRACK
-----------+-------------------------------------+--------------------------------
           | [#989]  Base Components             | [#1000] First Accuracy Test
           | [#994]  Build Optimization          | [#1010] Annotation: Catan+TTR
           | [#1001] QuestionInputForm           | [#1011] Annotation: 7W+Agri+Spl
           | [#1003] GameSelector Dropdown       | [#1012] Adversarial Dataset
           |                                     | [#1019] Accuracy Validation 80%
           |                                     | [#1020] Performance Testing P95

Week 3-4   | FRONTEND TRACK                      | BACKEND/INFRA TRACK
-----------+-------------------------------------+--------------------------------
           | [#1004] Loading/Error States        | [#1021] Bug Fixes & Polish
           | [#1008] Error Handling Retry        | [#1022] Documentation Updates
           | [#1013] PDF Viewer (react-pdf)      | [#1023] Phase 1A Checklist
           | [#1014] Citation Jump to Page       | [#1674] Resolve Backend TODOs
           | [#1016] Italian UI (200+ strings)   | [#1675] Missing Frontend APIs
           | [#1017] Game Catalog Page           | [#1677] Remove Obsolete Models
           | [#1436] Fix SWR+Zustand             | [#1676] Remove Compat Layers

Week 5-6   | TESTING TRACK                       | INFRA/DOCS TRACK
-----------+-------------------------------------+--------------------------------
           | [#1496] Visual Regression Tests     | [#1562] HyperDX Docker Deploy
           | [#1497] Browser Matrix FF/Safari    | [#1563] .NET OpenTelemetry
           | [#1498] E2E Code Coverage           | [#1564] Remove Seq/Jaeger
           | [#1506] Theory Tests Consolidation  | [#1565] Backend Telemetry Test
           | [#1507] Remove Excessive Regions    | [#1566] HyperDX Browser SDK
           | [#1508] Error Boundary Tests        | [#1567] HyperDX Alerts
           | [#1509] Performance Tests FE        | [#1568] Load Testing
           | [#1510] API SDK Integration Tests   | [#1569] HyperDX Docs Update
           | [#1511] Expand Visual Regression    | [#1570] Go-Live Checklist

Week 7-8   | DOCUMENTATION TRACK                 | LOW-PRIORITY BUGS
-----------+-------------------------------------+--------------------------------
           | [#1679] Cleanup Legacy Comments     | [#1817] K6 Performance Tests
           | [#1680] Audit Infrastructure        | [#1820] PDF Test Performance
           | [#1681] Update Legacy Docs          | [#1821] PDF Background Reliability
           | [#1725] LLM Token Tracking          | [#1737] GC.Collect() Tests
```

---

## Issue Dettagliate per Track

### FRONTEND MVP (14 issue)

| # | Titolo | Effort | Deps | Status |
|---|--------|--------|------|--------|
| [#989](https://github.com/DegrassiAaron/meepleai-monorepo/issues/989) | Base Components Finalization | Large | - | Open |
| [#994](https://github.com/DegrassiAaron/meepleai-monorepo/issues/994) | Frontend Build Optimization | Large | - | Open |
| [#1001](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1001) | QuestionInputForm Component | Large | #989 | Open |
| [#1003](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1003) | GameSelector Dropdown | Medium | #989 | Open |
| [#1004](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1004) | Loading/Error States | Medium | #989 | Open |
| [#1008](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1008) | Error Handling Retry | Medium | #1004 | Open |
| [#1013](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1013) | PDF Viewer (react-pdf) | Large | - | Open |
| [#1014](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1014) | Citation Jump to Page | Medium | #1013 | Open |
| [#1016](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1016) | Italian UI (200+ strings) | Large | - | Open |
| [#1017](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1017) | Game Catalog Page | Large | - | Open |
| [#1436](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1436) | Fix SWR + Zustand Duplication | Medium | - | Open |

### BACKEND MVP (10 issue)

| # | Titolo | Effort | Deps | Status |
|---|--------|--------|------|--------|
| [#1000](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1000) | First Accuracy Test (Baseline) | Small | - | Open |
| [#1010](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1010) | Annotation: Catan+TTR (30 Q&A) | Large | #1000 | Open |
| [#1011](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1011) | Annotation: 7W+Agricola+Splendor | Large | #1010 | Open |
| [#1012](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1012) | Adversarial Dataset (50 synthetic) | Large | #1011 | Open |
| [#1019](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1019) | Accuracy Validation (80% on 100 Q&A) | Large | #1012 | Open |
| [#1020](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1020) | Performance Testing (P95 <3s) | Large | #1019 | Open |
| [#1021](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1021) | Final Bug Fixes & Polish | Large | All | Open |
| [#1022](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1022) | Documentation Updates | Large | #1021 | Open |
| [#1023](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1023) | Phase 1A Completion Checklist | Medium | #1022 | Open |

### TESTING (9 issue)

| # | Titolo | Effort | Deps | Status |
|---|--------|--------|------|--------|
| [#1496](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1496) | Visual Regression Tests | Medium | - | Open |
| [#1497](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1497) | Browser Matrix (Firefox, Safari) | Medium | #1496 | Open |
| [#1498](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1498) | E2E Code Coverage Reporting | Medium | #1497 | Open |
| [#1506](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1506) | Consolidate with Theory Tests | Small | - | Open |
| [#1507](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1507) | Remove Excessive Regions | Small | #1506 | Open |
| [#1508](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1508) | Error Boundary Tests | Medium | - | Open |
| [#1509](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1509) | Performance Tests (frontend) | Medium | #1508 | Open |
| [#1510](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1510) | API SDK Integration Tests | Medium | #1509 | Open |
| [#1511](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1511) | Expand Visual Regression Tests | Medium | #1510 | Open |

### INFRASTRUCTURE - HyperDX Epic (9 issue)

| # | Titolo | Effort | Deps | Status |
|---|--------|--------|------|--------|
| [#1561](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1561) | EPIC: HyperDX Observability | - | - | Open |
| [#1562](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1562) | Deploy HyperDX Docker | Medium | - | Open |
| [#1563](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1563) | .NET OpenTelemetry Config | Large | #1562 | Open |
| [#1564](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1564) | Remove Seq/Jaeger Services | Small | #1563 | Open |
| [#1565](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1565) | Backend Telemetry Testing | Medium | #1564 | Open |
| [#1566](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1566) | HyperDX Browser SDK (Next.js) | Medium | #1565 | Open |
| [#1567](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1567) | HyperDX Application Alerts | Medium | #1566 | Open |
| [#1568](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1568) | Load Testing Validation | Large | #1567 | Open |
| [#1569](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1569) | HyperDX Docs Update | Medium | #1568 | Open |
| [#1570](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1570) | Final Go-Live Checklist | Medium | #1569 | Open |

### DOCUMENTATION & CLEANUP (7 issue)

| # | Titolo | Effort | Deps | Status |
|---|--------|--------|------|--------|
| [#1674](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1674) | Resolve Backend TODOs | Medium | - | Open |
| [#1675](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1675) | Implement Missing APIs | Large | - | Open |
| [#1676](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1676) | Remove Backward Compat Layers | Medium | #1677 | Open |
| [#1677](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1677) | Remove Obsolete Data Models | Medium | #1679 | Open |
| [#1679](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1679) | Cleanup Legacy Comments | Small | - | Open |
| [#1680](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1680) | Audit Infrastructure Services | Medium | #1681 | Open |
| [#1681](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1681) | Update Legacy Documentation | Medium | - | Open |

### LOW-PRIORITY BUGS (4 issue)

| # | Titolo | Effort | Deps | Status |
|---|--------|--------|------|--------|
| [#1725](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1725) | LLM Token Tracking Advanced | Medium | - | Open |
| [#1737](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1737) | GC.Collect() Performance Tests | Small | - | Open |
| [#1817](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1817) | K6 Performance Tests Failed | Medium | - | Open |
| [#1820](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1820) | PDF Upload Test Performance | Small | - | Open |
| [#1821](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1821) | PDF Background Reliability | Medium | - | Open |

---

## DEFERRED: POST-MVP (~60 Issue)

### Epic: Frontend Modernization (Deferred)

| # | Titolo | Link |
|---|--------|------|
| [#932](https://github.com/DegrassiAaron/meepleai-monorepo/issues/932) | Epic: Advanced Features (Phase 4) | Deferred |
| [#933](https://github.com/DegrassiAaron/meepleai-monorepo/issues/933) | Epic: App Router Migration (Phase 3) | Deferred |
| [#934](https://github.com/DegrassiAaron/meepleai-monorepo/issues/934) | Epic: Design Polish (Phase 5) | Deferred |
| [#935](https://github.com/DegrassiAaron/meepleai-monorepo/issues/935) | Epic: Performance & Accessibility (Phase 6) | Deferred |
| [#936](https://github.com/DegrassiAaron/meepleai-monorepo/issues/936) | Spike: Infisical Secret Rotation | Deferred |

---

## Execution Dashboard

### Progress Overview

| Phase | Issue | Completati | Timeline |
|-------|-------|-----------|----------|
| P0: UI Components | 7 | **7/7** | **DONE** |
| P1: Playful Boardroom + Dataset | 15 | **6/15** | Week 1-2 |
| P2: Testing + Features | 23 | **2/23** | Week 3-6 |
| P3: Infrastructure + Docs | 20 | **0/20** | Week 7-8 |
| **TOTAL MVP** | **49 remaining** | - | **6-10 weeks** |

### KPI Targets

| KPI | Current | Target | Status |
|-----|---------|--------|--------|
| Frontend Coverage | 90%+ | >=90% | Met |
| Backend Coverage | 90%+ | >=90% | Met |
| E2E Coverage | 60% | >=80% | In Progress |
| Accuracy (Golden) | 75% | >=80% | In Progress |
| P95 Latency | 2.5s | <3s | Met |

---

## Changelog

### v15.0 (2025-12-02) - ISSUE CLEANUP

- Rimossi 82 issue chiusi (P0 UI Components, P1 Pages, ecc.)
- Riorganizzazione in sequenza di esecuzione parallelizzata
- **49 issue MVP** rimanenti, ~60 deferred
- Timeline aggiornata: 6-10 settimane

---

**Versione**: 15.0
**Ultimo Aggiornamento**: 2025-12-02
**Issue MVP Rimanenti**: 49
**Target Launch**: Febbraio 2026
