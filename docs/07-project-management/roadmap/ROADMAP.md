# 🗺️ MeepleAI Master Roadmap

**Ultimo Aggiornamento**: 2025-11-23
**Stato Progetto**: Alpha (DDD 99% completo, 2,070 righe legacy rimosse)
**Issue Aperte**: ~131
**Timeline to MVP**: 6-8 settimane
**Target Launch**: Fine Gennaio 2026

---

## 📋 Executive Summary

MeepleAI è un assistente AI per regolamenti di giochi da tavolo, con focus italiano e target accuracy >95%.

**Stack**: ASP.NET 9, Next.js 16, React 19, PostgreSQL, Qdrant, Redis, OpenRouter, n8n
**Architettura**: 7 Bounded Contexts (DDD/CQRS), 99% migrazione completata
**Fase Attuale**: Alpha - Frontend coverage 66% → 90% (P0 blocker)

### 🎯 Priorità Strategica

**P0 (CRITICAL)** - 5 issue, 36-48h:
- #1255: Frontend Coverage 66%→90% (🔥 GATE per tutto)
- #1454: Request Deduplication Cache
- #1453: Retry Logic Exponential Backoff
- #1434: Centralize Magic Numbers
- #1500: Fix Test Isolation Issues

**P1 (HIGH)** - 28 issue, 180-237h: Test Infrastructure + Backend Testing + E2E
**P2 (MVP)** - 45 issue, 325-432h: Golden Dataset + Italian UI + Features
**P3 (LAUNCH)** - 25 issue, 163-234h: Security + Documentation + Deployment
**DEFERRED** - 28 issue: Post-MVP enhancements

---

## 🚀 Sequenza di Esecuzione

### 📍 P0: CRITICAL BLOCKERS (2-3 giorni)

**MUST COMPLETE FIRST** - Blocking everything else

```
DAY 1:    #1500 (test isolation) + #1434 (magic numbers) [parallel]
DAY 2-3:  #1453 (retry logic) + #1454 (dedup cache) [parallel]
DAY 4-5:  #1255 (frontend coverage 90%) [CRITICAL, not parallel]
```

**Completion Gate**: 100% - MANDATORY per procedere

---

### 📍 P1: HIGH PRIORITY FOUNDATION (2-3 settimane)

**Dipendenze**: P0 al 100%
**Focus**: Testing infrastructure + core quality

#### P1.1: Test Infrastructure (10 issue, 40-52h)
- Split test files (#1504)
- Replace global mocks (#1503)
- Test data factories (#1501)
- Component testing 90%+ (#992)
- PDF viewer tests (#1015)

#### P1.2: Backend Testing & Quality (8 issue, 48-64h)
- Performance P95<3s (#1020)
- Accuracy validation 80%+ (#1019)
- Quality metrics implementation (#999)
- E2E PDF citation (#1018)
- Build optimization (#994)

#### P1.3: E2E Testing Expansion (10 issue, 46-62h)
- RBAC tests (#1490)
- Browser matrix (#1492)
- POM migration (#1493-1494)
- E2E coverage 80%+ (#1495)
- Visual regression (#1496)

**Effort**: 180-237h total
**Completion Gate**: Frontend ≥90%, Backend tests pass, E2E suite stable

---

### 📍 P2: MVP FEATURES (4-5 settimane)

**Dipendenze**: P1 al 100% (especially #1255 coverage gate)
**Focus**: Golden dataset + Italian UI + Feature completion

#### P2.1: Golden Dataset Annotation (8 issue, 52-64h)
- Catan rules 50 Q&A (#996)
- Ticket to Ride 50 Q&A (#997)
- Wingspan 50 Q&A (#998)
- Quality validation (#1002)
- Dataset export (#1006)

**Deliverable**: 150 Q&A pairs validated (3 games × 50)

#### P2.2: Italian UI & Localization (12 issue, 68-88h)
- Italian translation 100% (#1016) - 200+ strings
- i18n framework setup (#1007)
- Language switcher (#1008)
- Validation/error messages (#1010-1012)
- Translation coverage tests (#1028)

**Deliverable**: UI 100% italiano, testata

#### P2.3: PDF Viewer & Game Catalog (10 issue, 56-72h)
- PDF viewer integration (#1013)
- Citation jump to page (#1014) - ✅ DONE
- PDF annotations UI (#1030)
- Game catalog page (#1017)
- Game detail page (#1033)
- Search & filter (#1034)

#### P2.4: Feature Polish & Completion (15 issue, 76-100h)
- Bug fixes & polish (#1021)
- Loading states (#1037)
- Error handling UI (#1038)
- Accessibility audit WCAG 2.1 (#1040)
- Keyboard navigation (#1041)
- Mobile responsiveness (#1043)
- Dark mode (#1044)
- SEO optimization (#1047)

**Effort**: 325-432h total
**Completion Gate**: Tutte le features MVP operative, 150 Q&A pairs ready

---

### 📍 P3: SECURITY & LAUNCH PREP (2-3 settimane)

**Dipendenze**: P2 al 100%
**Focus**: Security audit + documentation + final polish

#### P3.1: Security Audit & Hardening (8 issue, 72-96h)
- Security penetration testing (#576) - 🔥 External auditor, 24-40h
- Admin 2FA override (#575)
- OWASP Top 10 verification (#1051)
- SQL injection/XSS prevention (#1052-1053)
- API rate limiting (#1055)

**Gate**: Security report approved, 0 critical/high vulnerabilities

#### P3.2: Documentation & User Guides (10 issue, 48-64h)
- User manual Italian (#1057)
- Admin guide (#1058)
- API documentation OpenAPI (#1059)
- Deployment guide (#1060)
- Video tutorials (#1064)

#### P3.3: Launch Preparation (7 issue, 36-48h)
- Production environment setup (#1066)
- Database migration plan (#1067)
- Monitoring dashboard (#1069)
- Load testing production (#1071)

**Effort**: 163-234h total
**Completion Gate**: Security approved, docs complete, production ready

---

## 📊 EXECUTION DASHBOARD

### Critical Path Timeline

```
Week 1-2    │ P0: Critical Blockers (5 issue, 36-48h)
            │ └─ #1255 Frontend Coverage 90% 🔥
            ↓
Week 3-5    │ P1: High Priority Foundation (28 issue, 180-237h)
            │ ├─ Test Infrastructure (10 issue)
            │ ├─ Backend Testing (8 issue)
            │ └─ E2E Expansion (10 issue)
            ↓ GATE: Frontend ≥90% Coverage
            ↓
Week 6-10   │ P2: MVP Features (45 issue, 325-432h)
            │ ├─ Golden Dataset (8 issue, 52-64h)
            │ ├─ Italian UI (12 issue, 68-88h)
            │ ├─ PDF & Catalog (10 issue, 56-72h)
            │ └─ Feature Polish (15 issue, 76-100h)
            ↓ GATE: All features complete, 150 Q&A ready
            ↓
Week 11-13  │ P3: Security & Launch (25 issue, 163-234h)
            │ ├─ Security Audit (8 issue, 72-96h)
            │ ├─ Documentation (10 issue, 48-64h)
            │ └─ Launch Prep (7 issue, 36-48h)
            ↓ GATE: Security approved
            ↓
Week 14     │ 🚀 PRODUCTION LAUNCH
```

### Resource Allocation

| Type | P0 | P1 | P2 | P3 | Total | Effort |
|------|----|----|----|----|-------|--------|
| **Frontend** | 3 | 18 | 32 | 8 | 61 (47%) | 280-360h |
| **Backend** | 0 | 8 | 14 | 12 | 34 (26%) | 180-240h |
| **Both** | 1 | 5 | 6 | 8 | 20 (15%) | 100-140h |
| **Infrastructure** | 0 | 1 | 2 | 8 | 11 (8%) | 60-80h |
| **E2E** | 1 | 14 | 3 | 2 | 20 (15%) | 84-110h |
| **MVP Total** | **5** | **28** | **45** | **25** | **103** | **704-930h** |

**Team Size**: 2-3 developers full-time
**Velocity Required**: 12-15 issue/settimana

---

## 🚨 CRITICAL SUCCESS FACTORS

### 🔥 Top 5 Risks & Mitigations

| Risk | Impact | Probability | Mitigation |
|------|--------|-------------|------------|
| **#1255 Coverage slippage** | 🔴 CRITICAL | Medium | Daily tracking, pair programming, 2 devs |
| **Security audit failure** | 🔴 CRITICAL | Low | Start #576 early (Week 11), external auditor |
| **Golden dataset quality** | 🟡 HIGH | Medium | 2+ reviewers, quality rubric |
| **Accuracy <80%** | 🟡 HIGH | Medium | Early baseline (#1000), iterative tuning |
| **Scope creep** | 🟢 MEDIUM | Medium | Strict DEFERRED labels, weekly review |

### 🎯 Quality Gates

| Gate | Criteria | Blocker | Phase |
|------|----------|---------|-------|
| **G1: P0 Complete** | All 5 P0 closed, coverage ≥90% | Yes | Week 2 |
| **G2: Test Foundation** | P1.1-P1.3 pass, E2E stable | Yes | Week 5 |
| **G3: Features Complete** | 150 Q&A, Italian 100%, PDF live | Yes | Week 10 |
| **G4: Security Approved** | Pen test pass, 0 crit/high vulns | Yes | Week 13 |
| **G5: Launch Ready** | Docs, monitoring, rollback plan | Yes | Week 14 |

### 📈 KPI Targets

| KPI | Current | Target | Gate |
|-----|---------|--------|------|
| **Frontend Coverage** | 66% | ≥90% | G1 🔥 |
| **Backend Coverage** | 90%+ | ≥90% | ✅ Met |
| **E2E Coverage** | 60% | ≥80% | G2 |
| **Accuracy (Golden)** | 75% | ≥80% | G3 |
| **P95 Latency** | 2.5s | <3s | ✅ Met |
| **Lighthouse Score** | 90+ | ≥90 | ✅ Met |
| **Security Vulns (Crit/High)** | 0 | 0 | G4 |

---

## 🎉 NEXT ACTIONS (Prossime 48h)

### Immediate Priorities (P0 - Week 1)

**Day 1-2** (2025-11-23 to 2025-11-24):
1. ✅ **#1500** - Fix Test Isolation Issues (6-8h)
2. ✅ **#1434** - Centralize Magic Numbers (4-6h)
3. 🚀 **Preparare** #1255 plan (frontend coverage 90%)

**Day 3-4** (2025-11-25 to 2025-11-26):
4. ✅ **#1453** - Retry Logic Exponential Backoff (4-6h)
5. ✅ **#1454** - Request Deduplication Cache (6-8h)

**Day 5-7** (2025-11-27 to 2025-11-29):
6. 🔥 **#1255** - Frontend Coverage 66%→90% (16-20h) - **CRITICAL GATE**

### Week 2 Actions (P1 Start)

**Prerequisiti**: P0 al 100%

**Parallel Tracks**:
- **Track 1 (Frontend)**: #1504 → #1503 → #1502 (test infra)
- **Track 2 (Backend)**: #1000 → #999 → #1020 (quality tests)
- **Track 3 (E2E)**: #1491 → #1490 → #1492 (E2E expansion)

**Goal**: 10-12 issue P1 completate

---

## 📦 DEFERRED: POST-MVP (28 Issue)

**Status**: ⚪ Non prioritarie per MVP
**Timeline**: 2026 Q2-Q3

### Epic #1300: Admin Dashboard v2.0 (12 issue, 96-128h)
Advanced admin features, KPI real-time, bulk operations, ML insights

### Epic #1301: Frontend Modernization Extended (6 issue, 48-72h)
Server Components, streaming SSR, edge runtime, advanced caching

### Epic #1302: Infrastructure Hardening (5 issue, 60-80h)
Resource limits, Traefik reverse proxy, automated backups, HA setup

### Additional Enhancements (5 issue, 40-60h)
GraphQL API, WebSocket real-time, PWA, Multi-tenancy, Advanced caching

**Rationale**: MVP-first, these enhance existing features but not required for launch

---

## 📝 CHANGELOG

### v14.0 (2025-11-23) - CLEANUP & SIMPLIFICATION

**Changes**:
- ✅ Semplificato roadmap da 498 a 280 righe
- ✅ Rimossi 9 file markdown obsoleti/duplicati
- ✅ Consolidata documentazione in struttura chiara
- ✅ Focus su execution invece di planning teorico
- ✅ Mantenuti dettagli tecnici nei file di implementazione

**File Rimossi**:
- Stub files: team_structure.md, org_chart.md, roles_playbook.md, onboarding_guide.md
- Duplicati: bgai-issue-tracking-summary.md, manual-issue-creation-guide.md (x2)
- Obsoleti: board-game-ai-execution-calendar.md, board-game-ai-sprint-overview.md

---

## 📚 DOCUMENT NAVIGATION

**Per dettagli tecnici backend**: `planning/backend-implementation-plan.md`
**Per dettagli tecnici frontend**: `planning/frontend-implementation-plan.md`
**Per quick start**: `planning/QUICK-START.md`
**Per overview generale**: `README.md`

**Documentazione Completa**: Vedi `/docs/INDEX.md` (115 docs, 800+ pagine)

---

**Versione**: 14.0 (Cleanup & Simplification)
**Owner**: Engineering Team
**Issue Totali**: 131 (103 MVP + 28 Deferred)
**Effort to MVP**: 704-930h (6-8 settimane, 2-3 devs)
**Target Launch**: Fine Gennaio 2026

---

🚀 **"Execute with precision, deliver with excellence"** 🚀
