# 🗺️ MeepleAI Master Roadmap 2025-2026

**Ultimo Aggiornamento**: 2025-11-21
**Issue Aperte**: **136**
**Timeline Rimanente**: 6-8 settimane (Dic 2025 - Gen 2026)
**Target MVP**: Fine Gennaio 2026
**Owner**: Engineering Team

---

## 📋 Executive Summary

### 🎯 Strategia Attuale

**Approccio a TIER**:
1. ✅ **TIER 0**: Security Critical + Large Refactoring - **COMPLETATO**
2. ✅ **TIER 1**: High Priority Refactoring - **COMPLETATO**
3. 🚀 **TIER 2**: Medium Priority Improvements (3 rimanenti) - **IN CORSO**
4. ⏸️ **TIER 3**: Testing & Quality Assurance (22 issue)
5. ⏸️ **TIER 4**: MVP Features (25 issue)
6. ⏸️ **TIER 5**: Security Audit (2 issue)
7. 📦 **PHASE 2**: Post-MVP (~56 issue) - **DEFERRED**

### 🚀 Progress Overview

| Tier | Issue Aperte | Status | Timeline |
|------|-------------|--------|----------|
| **TIER 2** | 3 | 🟢 66.7% | 1-2 giorni |
| **TIER 3** | 22 | 🔴 0% | 2-3 settimane |
| **TIER 4** | 25 | 🟡 12% | 5-6 settimane |
| **TIER 5** | 2 | 🔴 0% | 1-2 settimane |
| **PHASE 2** | ~56 | ⚪ DEFERRED | Post-MVP |
| **TOTALE** | **~108** | 🟢 20% | **6-8 settimane** |

**Note**: Conteggio preliminare ~108 issue prioritizzate. Le rimanenti ~28 issue potrebbero essere distribuite tra i tier o in backlog.

---

## 🔧 TIER 2: Medium Priority (3 Issue Rimanenti)

**Timeline**: 1-2 giorni
**Status**: 🟢 66.7% completo (6/9)

### Issue Rimanenti

| # | Titolo | Area | Effort | Priorità |
|---|--------|------|--------|----------|
| **#1454** | Request Deduplication Cache | Frontend/API | 6-8h | Medium |
| **#1453** | Retry Logic with Exponential Backoff | Frontend | 4-6h | Medium |
| **#1434** | Centralize Magic Numbers | Frontend | 4-6h | Medium |

**Effort Totale**: 14-20h
**Target**: Completamento entro 2025-11-23

### Completion Criteria
- [ ] Request deduplication riduce API calls del 30%
- [ ] Retry logic con exponential backoff operativo
- [ ] Magic numbers centralizzati in costanti

---

## ✅ TIER 3: Testing & Quality Assurance (22 Issue)

**Timeline**: 2-3 settimane
**Effort**: 88-112 ore
**Status**: 🔴 0% - Ready to start

### 3.1 Core Testing Infrastructure (11 issue)

| # | Titolo | Area | Effort | Priorità |
|---|--------|------|--------|----------|
| **#1255** | Frontend Coverage 66% → 90% | Frontend | 16-20h | 🔥 CRITICAL |
| **#1015** | PDF Viewer Tests | Frontend | 8-10h | High |
| **#1005** | Jest Tests Q&A Components | Frontend | 10-12h | High |
| **#992** | Component Testing (90%+) | Frontend | 12-16h | High |
| **#993** | Responsive Design Testing | Frontend | 8-10h | High |
| **#1020** | Performance Testing (P95 <3s) | Backend | 8-10h | High |
| **#1019** | Accuracy Validation (80%+) | Backend | 12-16h | High |
| **#1018** | E2E Testing (PDF citation) | E2E | 12-16h | High |
| **#1009** | Month 5 E2E Testing | E2E | 10-12h | High |
| **#1000** | First Accuracy Test (baseline) | Backend | 4-6h | High |
| **#999** | Quality Test Implementation | Backend | 8-10h | High |

**Subtotale**: 108-138h

### 3.2 Test Quality Improvements (11 issue)

**HIGH Priority (6 issue)**:
- **#1504** - Split Large Test Files (4-6h)
- **#1503** - Replace Global Fetch Mocks (4-6h)
- **#1502** - Extract SSE Mock Helper (3-4h)
- **#1501** - Create Test Data Factories (4-6h)
- **#1500** - Fix Test Isolation Issues (6-8h)
- **#1499** - Standardize Test Naming (3-4h)

**MEDIUM Priority (5 issue)**:
- #1511, #1510, #1509, #1508, #1505-1507 (~16-22h)

### 3.3 E2E Testing Expansion (9 issue)

- **#1490** - RBAC Authorization Tests (4-6h) - HIGH
- **#1491** - Fix Demo Login Tests (2-3h) - HIGH
- **#1492-1498** - Browser matrix, POM migration, coverage (~30-40h)

### Completion Criteria

**Core Testing**:
- [ ] Frontend coverage ≥90% (#1255) 🔥 **GATE per TIER 4**
- [ ] Component tests 90%+ (#992)
- [ ] Performance P95 <3s (#1020)
- [ ] Accuracy ≥80% (#1019)
- [ ] E2E tests passing (#1018, #1009)

**Quality Improvements**:
- [ ] Test isolation fixed (#1500)
- [ ] Test data factories (#1501)
- [ ] Fetch mocks replaced (#1503)

**E2E Expansion**:
- [ ] RBAC tests (#1490)
- [ ] Demo login fixed (#1491)
- [ ] POM migration (#1492)

---

## 🎯 TIER 4: MVP Features (25 Issue)

**Timeline**: 5-6 settimane
**Effort**: 190-250 ore
**Status**: 🟡 ~12% (~3/25 partial)

### 4.1 Month 4: Quality Framework (5 issue)

| # | Titolo | Status | Effort |
|---|--------|--------|--------|
| **#989** | Base Components | 🔄 PARTIAL | 8-10h |
| **#992** | Component Testing 90%+ | ⏸️ (TIER 3) | 12-16h |
| **#993** | Responsive Testing | ⏸️ (TIER 3) | 8-10h |
| **#994** | Build Optimization | ⏸️ PENDING | 8-10h |
| **#995** | Integration Testing | ⏸️ PENDING | 10-12h |

### 4.2 Month 5: Golden Dataset (8 issue)

| # | Titolo | Effort | Descrizione |
|---|--------|--------|-------------|
| **#996** | Annotate Catan Rules | 10-12h | 50 Q&A pairs |
| **#997** | Annotate Ticket to Ride | 10-12h | 50 Q&A pairs |
| **#998** | Annotate Wingspan Rules | 10-12h | 50 Q&A pairs |
| **#999** | Quality Test Implementation | (TIER 3) | 5-metric framework |
| **#1000** | First Accuracy Test | (TIER 3) | Baseline validation |
| **#1001** | QuestionInputForm Component | 8-10h | UI component |
| **#1005** | Jest Tests Q&A | (TIER 3) | Component tests |
| **#1009** | E2E Testing | (TIER 3) | User journeys |

**Effort Totale**: 48-56h (+ TIER 3 testing)

### 4.3 Month 6: Italian UI & Polish (12 issue)

| # | Titolo | Status | Effort | Descrizione |
|---|--------|--------|--------|-------------|
| **#1013** | PDF Viewer Integration | 🔄 PARTIAL | 8-10h | Finalize viewer |
| **#1014** | Citation Jump to Page | ✅ DONE | - | Completed |
| **#1015** | PDF Viewer Tests | ⏸️ (TIER 3) | - | Testing |
| **#1016** | Italian UI Translation | 🔄 PARTIAL | 12-16h | 200+ strings |
| **#1017** | Game Catalog Page | ⏸️ PENDING | 8-10h | Browse games |
| **#1018** | E2E PDF Citation | ⏸️ (TIER 3) | - | Testing |
| **#1019** | Accuracy Validation | ⏸️ (TIER 3) | - | Testing |
| **#1020** | Performance Testing | ⏸️ (TIER 3) | - | Testing |
| **#1021** | Bug Fixes & Polish | ⏸️ PENDING | 12-16h | Final polish |
| **#1022** | Documentation Updates | ⏸️ PENDING | 8-10h | User docs |
| **#1023** | Completion Checklist | ⏸️ PENDING | 4-6h | Launch prep |

### Completion Criteria

- [ ] Golden dataset: 150 Q&A pairs (3 games × 50)
- [ ] QuestionInputForm component completo
- [ ] PDF viewer con citation jump funzionante
- [ ] Italian UI 100% tradotta (200+ strings)
- [ ] Game catalog page live
- [ ] Documentazione utente completa
- [ ] Accuracy ≥80% su golden dataset

---

## 🔐 TIER 5: Security Audit & Launch (2 Issue)

**Timeline**: 1-2 settimane
**Effort**: 32-50 ore
**Status**: 🔴 0% - **MANDATORY per Production**

| # | Titolo | Area | Effort | Priorità |
|---|--------|------|--------|----------|
| **#576** | Security Penetration Testing | Security | 24-40h | 🔥 CRITICAL |
| **#575** | Admin 2FA Override Tool | Auth | 8-10h | HIGH |

### Completion Criteria

- [ ] Penetration testing da auditor esterno
- [ ] OWASP Top 10 vulnerabilities verificate
- [ ] Security report approvato (0 critical/high)
- [ ] Admin 2FA override implementato
- [ ] Production deployment checklist approvato

**🚀 GATE**: Production launch richiede 100% completion TIER 5

---

## 📦 PHASE 2: Post-MVP (~56 Issue - DEFERRED)

**Timeline**: 2026 H2
**Status**: ⚪ DEFERRED - Focus su MVP
**Totale Issue**: ~56

### Major Epics (Deferred)

#### Epic #1300: Admin Dashboard v2.0 (~48 issue)
- **FASE 1-4**: Dashboard, Infrastructure, Management, Advanced
- **Effort**: 12-16 settimane post-MVP
- **Status**: Dashboard base operativo, advanced features deferred

**Categorie**:
- Dashboard avanzato (visualizzazioni, KPI real-time)
- Infrastructure hardening (backup, HA, disaster recovery)
- Management tools (bulk operations, workflows)
- Advanced features (ML insights, recommendations)

#### Epic #1301: Frontend Modernization Extended (~6 issue)
- **Focus**: Advanced patterns (già completata migrazione Next.js 16 + React 19)
- **Status**: Core modernization complete, advanced patterns deferred

**Include**:
- Server Components avanzati
- Streaming SSR optimization
- Edge Runtime features
- Advanced caching strategies

#### Epic #1302: Infrastructure Hardening (~5 issue)
- **Focus**: Docker resource limits, profiles, Traefik, backups
- **Effort**: 4-6 settimane post-MVP
- **Status**: 16 Docker services operativi, hardening deferred

**Include**:
- Resource limits e monitoring
- Docker profiles per environment
- Traefik reverse proxy
- Automated backups

---

## 📊 Progress Metrics

### Overall Timeline

```
┌─────────────────────────────────────────────────────────────┐
│                    Timeline to MVP (6-8 weeks)              │
├─────────────────────────────────────────────────────────────┤
│ Week 1       │ ✅ TIER 2 Complete (3 issue)                 │
│ Week 2-4     │ ⏸️ TIER 3 Testing (22 issue)                │
│ Week 5-10    │ ⏸️ TIER 4 MVP Features (25 issue)           │
│ Week 11-12   │ ⏸️ TIER 5 Security Audit (2 issue)          │
│ Post-MVP     │ 📦 PHASE 2 (~56 issue) - DEFERRED           │
└─────────────────────────────────────────────────────────────┘
```

### Effort Estimation

| Tier | Issue Aperte | Effort (ore) | Settimane | Status |
|------|-------------|--------------|-----------|--------|
| **TIER 2** | 3 | 14-20h | 0.5 | 🟢 IN PROGRESS |
| **TIER 3** | 22 | 88-112h | 2-3 | Ready |
| **TIER 4** | ~25 | 190-250h | 5-6 | Waiting |
| **TIER 5** | 2 | 32-50h | 1-2 | Waiting |
| **TOTALE** | **52** | **324-432h** | **9-12** | 🟢 On Track |

**Velocity Attuale**: 10-12 issue/giorno (accelerata)
**Timeline Realistica**: 6-8 settimane (metà/fine Gennaio 2026)
**Assumption**: 1-2 developers full-time

### Key Performance Indicators

**Code Quality**:
- ✅ TypeScript `any`: 0 files
- ✅ Security Issues: 0 critical
- ✅ Backend Coverage: 90%+
- ⏸️ Frontend Coverage: 66% → 90% target (TIER 3 #1255)

**Performance**:
- 🟢 P95 Latency: ~2.5s (Target: <3s)
- ✅ TTFT Streaming: ~800ms (Target: <1s)
- 🟡 Accuracy: ~75% (Target: ≥80%, TIER 4)
- ✅ Hallucination: <5% (Target: ≤10%)
- ✅ Lighthouse: 90+ (Target: ≥90)

---

## 🚨 Critical Path & Dependencies

### Dependency Flow

```
TIER 2 (1-2d) → TIER 3 (2-3w) → TIER 4 (5-6w) → TIER 5 (1-2w) → LAUNCH
   3 issue        22 issue         25 issue        2 issue
```

### Critical Gates

1. **TIER 2 → TIER 3**: Nessun gate, può partire subito dopo TIER 2
2. **TIER 3 → TIER 4**: Frontend coverage ≥90% (#1255) 🔥 **CRITICAL**
3. **TIER 4 → TIER 5**: Tutte le features MVP complete
4. **TIER 5 → LAUNCH**: Security audit approvato

### Current Blockers

**TIER 2** (3 issue - 14-20h):
- #1454 - Request Deduplication Cache
- #1453 - Retry Logic with Exponential Backoff
- #1434 - Centralize Magic Numbers

**TIER 3** (Gate per TIER 4):
- #1255 - Frontend Coverage 66%→90% 🔥 **CRITICAL PATH**

---

## 🚨 Risk Assessment

### 🔴 HIGH RISK

**1. Frontend Coverage 66%** (Target: 90% - Issue #1255)
- **Impact**: CRITICAL - Gate per TIER 4, regression risk
- **Mitigation**: Dedicare 16-20h prioritarie in TIER 3
- **Status**: ⚠️ Non iniziato, pianificato Week 2

### 🟡 MEDIUM RISK

**2. Dataset Annotation Quality** (150 Q&A pairs - 3 games)
- **Impact**: HIGH - Affects accuracy validation
- **Mitigation**: Rigorous review, multiple annotators, quality rubric
- **Timeline**: TIER 4 Month 5

**3. Accuracy Target <80%**
- **Impact**: HIGH - MVP launch blocker
- **Mitigation**: Early baseline test (#1000), iterative improvements
- **Current**: ~75%, need +5% improvement

### 🟢 LOW RISK

**4. Scope Creep** (~56 Phase 2 issues)
- **Impact**: LOW - Time waste
- **Mitigation**: Strict "DEFERRED" labels, clear tier separation
- **Status**: Controlled

**5. Performance P95 >3s**
- **Impact**: MEDIUM - User experience
- **Current**: ~2.5s (within target)
- **Status**: 🟢 Monitored

---

## 🎯 MVP Launch Checklist

### Pre-Launch Requirements

**TIER 2** (3 issue):
- [ ] Request deduplication cache (#1454)
- [ ] Retry logic with exponential backoff (#1453)
- [ ] Magic numbers centralized (#1434)

**TIER 3** (22 issue):
- [ ] Frontend coverage ≥90% (#1255) 🔥
- [ ] Component tests passing (#992)
- [ ] E2E tests complete (#1018, #1009)
- [ ] Performance P95 <3s (#1020)
- [ ] Test quality improvements (6 HIGH priority)

**TIER 4** (25 issue):
- [ ] Golden dataset annotato (150 Q&A, 3 games)
- [ ] Accuracy ≥80% (#1019)
- [ ] PDF viewer completo (#1013)
- [ ] Italian UI 100% (#1016)
- [ ] Game catalog live (#1017)
- [ ] Documentazione utente (#1022)

**TIER 5** (2 issue):
- [ ] Penetration testing completo (#576)
- [ ] Security audit approvato (0 critical/high)
- [ ] Admin 2FA override (#575)
- [ ] Production deployment checklist

### 🚀 Production Launch Gate

**Required**:
1. ✅ Tutti i TIER 2-5 completati (52 issue)
2. ✅ Tutti i KPI raggiunti
3. ✅ Security audit passed
4. ✅ Documentazione completa
5. ✅ Rollback plan pronto
6. ✅ Monitoring operazionale

**Target Launch**: **Fine Gennaio 2026**

---

## 🎉 Next Steps

### Immediate Actions (Prossime 24-48h)

**Priority 1**: Completare TIER 2
1. #1454 - Request Deduplication Cache (6-8h)
2. #1453 - Retry Logic with Exponential Backoff (4-6h)
3. #1434 - Centralize Magic Numbers (4-6h)

**Target**: TIER 2 al 100% entro 2025-11-23

**Priority 2**: Preparare TIER 3
- Review #1255 (Frontend Coverage) - pianificare approccio
- Setup test infrastructure improvements (#1500-1504)
- Preparare test data factories (#1501)

### Key Takeaways

1. 🚀 **TIER 2 quasi completo** - Solo 3 issue (1-2 giorni)
2. ⚠️ **Frontend Coverage è CRITICAL** - #1255 gate per TIER 4
3. 📊 **52 issue al MVP** - Timeline realistica 6-8 settimane
4. 📦 **56 issue deferred** - Scope control mantenuto
5. ⚡ **Velocity accelerata** - 10-12 issue/giorno

### Success Vision

> **"Quality first, features fast, security always"**
>
> MVP robusto, testato, sicuro - pronto per produzione.

---

**🚀 Andiamo verso l'MVP! 🚀**

---

**Versione**: 12.0 (Streamlined - Focus su Issue Aperte)
**Owner**: Engineering Team
**Ultimo Aggiornamento**: 2025-11-21
**Issue Aperte**: 136 (~108 prioritizzate in TIER, ~28 backlog)
**Effort to MVP**: ~324-432h (6-8 settimane)
**Target Launch**: Fine Gennaio 2026
