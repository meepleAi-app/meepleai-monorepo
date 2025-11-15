# Code Quality Roadmap 2025
**MeepleAI Monorepo - Code Quality & Technical Excellence**

**Last Updated:** 2025-11-15
**Status:** Active
**Target:** 90%+ test coverage, zero tech debt by Q2 2025

---

## Executive Summary

This roadmap tracks all code quality initiatives for the MeepleAI monorepo, including refactoring, testing, security improvements, and architectural migrations. Current focus: completing DDD/CQRS migration (99% complete) and establishing comprehensive test coverage.

**Key Metrics:**
- **Test Coverage:** Frontend 90.03%, Backend 90%+ ✅
- **DDD Migration:** 99% complete (7/7 contexts)
- **Open Quality Issues:** 48 tracked issues
- **Priority Distribution:** P1: 8 issues | P2: 4 issues | P3: 2 issues

---

## 1. Architecture & DDD Migration

### 1.1 CQRS Pattern Completion (P2 - High Priority)

**Status:** 99% complete, final migrations in progress

#### Open Issues

| Issue | Title | Priority | Est. Time | Labels |
|-------|-------|----------|-----------|--------|
| [#1191](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1191) | Complete OAuth Callback Migration to CQRS | P2 | 6-8h | `area/auth`, `ddd`, `cqrs` |
| [#1189](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1189) | Migrate RuleSpec Comment/Diff Services to CQRS | P2 | 6-8h | `area/rulespec`, `ddd`, `cqrs`, `refactoring` |
| [#1188](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1188) | Migrate Agent Services to CQRS Pattern | P2 | 10-14h | `area/ai`, `ddd`, `cqrs`, `legacy-code` |
| [#1186](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1186) | Implement Streaming Query Handlers for RAG/QA | P1 | 8-12h | `rag`, `ddd`, `cqrs`, `streaming` |

**Total Effort:** ~30-42 hours

**Recent Completions (Nov 15, 2025):**
- ✅ #1219: RuleSpecService → CQRS (575 lines migrated)
- ✅ #1214: ChatService → CQRS (431 lines migrated)
- ✅ 2,070 lines of legacy services eliminated

### 1.2 Domain Events Implementation (P2)

| Issue | Title | Priority | Est. Time | Labels |
|-------|-------|----------|-----------|--------|
| [#1190](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1190) | Implement Domain Events for All Aggregates | P2 | 8-12h | `ddd`, `refactoring`, `domain-events` |

**Scope:** 42 TODO domain events across 7 bounded contexts for audit trails and cross-context communication.

---

## 2. Refactoring & Technical Debt

### 2.1 High Priority Tech Debt (P1-P3)

| Issue | Title | Priority | Est. Time | Labels |
|-------|-------|----------|-----------|--------|
| [#1194](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1194) | Centralize Error Handling with Middleware | P3 | 6-8h | `refactoring`, `tech-debt`, `middleware`, `error-handling` |
| [#1187](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1187) | Replace Hardcoded Configuration Values | P1 | 4-6h | `configuration`, `refactoring`, `tech-debt` |

**Total Effort:** ~10-14 hours

**Details:**
- **#1194:** Consolidate scattered try-catch blocks across endpoints into centralized middleware for standardized error responses
- **#1187:** Move hardcoded session expiration, rate limits, timeouts to dynamic database configuration (SystemConfiguration bounded context)

### 2.2 Frontend Architecture Improvements (Month 6 - Deferred)

**Milestone:** Month 6: Italian UI | Due: 2026-01-15

| Issue | Title | Priority | Labels |
|-------|-------|----------|--------|
| [#1084](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1084) | Upload Queue Off-Main-Thread | Low | `frontend`, `performance`, `pdf`, `deferred`, `month-6` |
| [#1083](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1083) | Chat Store con Zustand + Streaming Hook | Low | `frontend`, `chat`, `performance`, `deferred`, `month-6` |
| [#1082](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1082) | Form System (RHF + Zod) | Low | `frontend`, `forms`, `accessibility`, `ux`, `deferred`, `month-6` |
| [#1081](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1081) | API SDK modulare con Zod | Low | `frontend`, `api`, `tech-debt`, `documentation`, `deferred`, `month-6` |
| [#1080](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1080) | AuthContext + Edge Middleware | Low | `frontend`, `auth`, `middleware`, `quality`, `deferred`, `month-6` |
| [#1079](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1079) | TanStack Query Data Layer | Low | `frontend`, `data-layer`, `tech-debt`, `deferred`, `month-6` |
| [#1078](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1078) | Server Actions per Auth & Export | Low | `frontend`, `auth`, `server-actions`, `deferred`, `month-6` |
| [#1077](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1077) | Bootstrap App Router + Shared Providers | Low | `frontend`, `architecture`, `deferred`, `month-6` |

**Total Issues:** 8 deferred to Month 6
**Focus:** Modern React patterns (App Router, Server Actions, TanStack Query, Zustand)

---

## 3. Security Improvements

### 3.1 Authentication & Authorization (P3)

| Issue | Title | Priority | Est. Time | Labels |
|-------|-------|----------|-----------|--------|
| [#1193](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1193) | Improve Session Authorization and Rate Limiting | P3 | 4-6h | `area/security`, `area/auth`, `kind/security` |

**Scope:** Add authorization checks and rate limiting to session management endpoints.

**Recent Security Achievements:**
- ✅ #1220: 954 security issues analyzed and resolved
- ✅ CodeQL SAST enabled in CI/CD
- ✅ Dependabot weekly scans active

---

## 4. Testing & Quality Assurance

### 4.1 Current Coverage Status

**Frontend:** 90.03% coverage ✅ (4,033 tests)
**Backend:** 90%+ coverage ✅ (162 tests)
**E2E:** 30 Playwright tests
**Total Tests:** 4,225

### 4.2 Backend Testing (High Priority)

**Milestone:** Month 6: Italian UI

| Issue | Title | Priority | Labels |
|-------|-------|----------|--------|
| [#1020](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1020) | Performance testing (P95 latency <3s) | High | `backend`, `testing`, `performance`, `board-game-ai`, `mvp` |
| [#1019](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1019) | Accuracy validation (80% target on 100 Q&A) | High | `backend`, `testing`, `quality`, `board-game-ai`, `mvp` |
| [#1018](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1018) | End-to-end testing (question → PDF citation) | High | `backend`, `testing`, `e2e`, `board-game-ai`, `mvp` |
| [#1009](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1009) | Month 5 E2E testing | High | `backend`, `testing`, `e2e`, `board-game-ai`, `mvp` |
| [#1000](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1000) | Run first accuracy test (baseline measurement) | High | `backend`, `testing`, `quality`, `board-game-ai`, `mvp` |
| [#999](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1999) | Quality test implementation (accuracy validation) | High | `backend`, `testing`, `quality`, `board-game-ai`, `mvp` |
| [#995](https://github.com/DegrassiAaron/meepleai-monorepo/issues/995) | Month 4 integration testing | High | `backend`, `testing`, `e2e`, `board-game-ai`, `mvp` |

**Total Issues:** 7 backend testing issues

### 4.3 Frontend Testing (High Priority)

| Issue | Title | Priority | Labels |
|-------|-------|----------|--------|
| [#1015](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1015) | PDF viewer tests (Jest + Playwright) | High | `frontend`, `testing`, `board-game-ai`, `mvp` |
| [#1005](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1005) | Jest tests for Q&A components (20 tests) | High | `frontend`, `testing`, `board-game-ai`, `mvp` |
| [#993](https://github.com/DegrassiAaron/meepleai-monorepo/issues/993) | Responsive design testing (320px-1920px) | High | `frontend`, `testing`, `board-game-ai`, `mvp` |
| [#992](https://github.com/DegrassiAaron/meepleai-monorepo/issues/992) | Frontend component testing (Jest 90%+) | High | `frontend`, `testing`, `board-game-ai`, `mvp` |

**Total Issues:** 4 frontend testing issues

**Recent Testing Achievements (Nov 15, 2025):**
- ✅ #1213: 189 comprehensive unit tests added (7 components, 95%+ coverage)
- ✅ Zero TypeScript errors, ESLint compliance verified

### 4.4 Admin Console Testing (Optional - Low Priority)

**Milestone:** FASE 1: Dashboard Overview | Due: 2025-11-25

| Issue | Title | Priority | Labels |
|-------|-------|----------|--------|
| [#895](https://github.com/DegrassiAaron/meepleai-monorepo/issues/895) | Unit tests InfrastructureMonitoringService (90%+) | Low | `testing`, `admin-console`, `fase-2-infrastructure`, `deferred` |
| [#889](https://github.com/DegrassiAaron/meepleai-monorepo/issues/889) | Performance (<1s) + Accessibility (WCAG AA) | Low | `testing`, `performance`, `admin-console`, `fase-1-dashboard`, `optional` |
| [#888](https://github.com/DegrassiAaron/meepleai-monorepo/issues/888) | E2E Playwright test - Dashboard flow | Low | `testing`, `admin-console`, `fase-1-dashboard`, `optional` |
| [#887](https://github.com/DegrassiAaron/meepleai-monorepo/issues/887) | Jest tests dashboard components (90%+ coverage) | Low | `testing`, `admin-console`, `fase-1-dashboard`, `optional` |
| [#880](https://github.com/DegrassiAaron/meepleai-monorepo/issues/880) | Unit tests AdminDashboardService (90%+ coverage) | Low | `testing`, `admin-console`, `fase-1-dashboard`, `optional` |

**Total Issues:** 5 admin console testing issues (optional)

---

## 5. Performance Optimization

### 5.1 Completed Performance Improvements (PERF-05 to PERF-11)

- ✅ HybridCache L1+L2 (5min TTL)
- ✅ AsNoTracking (30% faster reads)
- ✅ Sentence chunking (20% better RAG)
- ✅ Query expansion + RRF (15-25% recall boost)
- ✅ Connection pools (PG: 10-100, Redis: 3 retries)
- ✅ Brotli/Gzip (60-80% compression)

### 5.2 Open Performance Issues

| Issue | Title | Priority | Labels |
|-------|-------|----------|--------|
| [#1020](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1020) | Performance testing (P95 latency <3s) | High | `backend`, `testing`, `performance`, `mvp` |
| [#1084](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1084) | Upload Queue Off-Main-Thread | Low | `frontend`, `performance`, `pdf`, `deferred` |
| [#1083](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1083) | Chat Store con Zustand + Streaming Hook | Low | `frontend`, `chat`, `performance`, `deferred` |
| [#889](https://github.com/DegrassiAaron/meepleai-monorepo/issues/889) | Dashboard Performance (<1s) | Low | `testing`, `performance`, `admin-console`, `optional` |
| [#879](https://github.com/DegrassiAaron/meepleai-monorepo/issues/879) | HybridCache for dashboard stats (1min TTL) | Low | `backend`, `performance`, `admin-console`, `optional` |

---

## 6. Documentation & Knowledge Management

### 6.1 Documentation Updates

| Issue | Title | Priority | Labels |
|-------|-------|----------|--------|
| [#1022](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1022) | Documentation updates (user guide, README) | High | `documentation`, `backend`, `mvp`, `board-game-ai` |

**Recent Documentation Achievements:**
- ✅ #1218: Comprehensive README.md files added to all major folders
- ✅ #1216: Documentation reorganized and cleaned up
- ✅ 115+ docs, 800+ pages indexed in docs/INDEX.md

---

## 7. Admin Console & Infrastructure Monitoring

### 7.1 FASE 1: Dashboard Overview (Due: 2025-11-25)

**Status:** Optional features, low priority

| Issue | Title | Labels |
|-------|-------|--------|
| [#886](https://github.com/DegrassiAaron/meepleai-monorepo/issues/886) | Dashboard API integration + 30s polling | `frontend`, `admin-console`, `fase-1-dashboard`, `optional` |
| [#885](https://github.com/DegrassiAaron/meepleai-monorepo/issues/885) | /pages/admin/index.tsx - Dashboard page | `frontend`, `admin-console`, `fase-1-dashboard`, `optional` |
| [#884](https://github.com/DegrassiAaron/meepleai-monorepo/issues/884) | ActivityFeed component | `frontend`, `admin-console`, `fase-1-dashboard`, `component`, `optional` |
| [#883](https://github.com/DegrassiAaron/meepleai-monorepo/issues/883) | MetricsGrid component (4x3 grid) | `frontend`, `admin-console`, `fase-1-dashboard`, `component`, `optional` |
| [#882](https://github.com/DegrassiAaron/meepleai-monorepo/issues/882) | StatCard reusable component | `frontend`, `admin-console`, `fase-1-dashboard`, `component`, `optional` |
| [#881](https://github.com/DegrassiAaron/meepleai-monorepo/issues/881) | AdminLayout component | `frontend`, `admin-console`, `fase-1-dashboard`, `optional` |
| [#878](https://github.com/DegrassiAaron/meepleai-monorepo/issues/878) | Activity Feed Service | `backend`, `admin-console`, `fase-1-dashboard`, `optional` |

**Total Issues:** 7 dashboard issues (optional)

### 7.2 FASE 2: Infrastructure Monitoring (Due: 2025-12-09)

**Status:** Deferred

| Issue | Title | Labels |
|-------|-------|--------|
| [#894](https://github.com/DegrassiAaron/meepleai-monorepo/issues/894) | GET /api/v1/admin/infrastructure/details | `backend`, `admin-console`, `fase-2-infrastructure`, `deferred` |
| [#893](https://github.com/DegrassiAaron/meepleai-monorepo/issues/893) | Prometheus client integration | `backend`, `admin-console`, `fase-2-infrastructure`, `deferred` |
| [#892](https://github.com/DegrassiAaron/meepleai-monorepo/issues/892) | Extend /health endpoints with detailed metrics | `backend`, `admin-console`, `fase-2-infrastructure`, `deferred` |
| [#891](https://github.com/DegrassiAaron/meepleai-monorepo/issues/891) | InfrastructureMonitoringService.cs | `backend`, `admin-console`, `fase-2-infrastructure`, `deferred` |
| [#890](https://github.com/DegrassiAaron/meepleai-monorepo/issues/890) | FASE 2 Epic: Multi-Service Health Checks | `admin-console`, `fase-2-infrastructure`, `epic`, `deferred` |

**Total Issues:** 5 infrastructure monitoring issues (deferred)

---

## 8. Phase Completion & Milestones

### 8.1 Phase 1A Completion

| Issue | Title | Priority | Labels |
|-------|-------|----------|--------|
| [#1023](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1023) | Phase 1A completion checklist | High | `backend`, `mvp`, `board-game-ai`, `month-6`, `quality` |

**Gate:** Technology viability validation before proceeding to Beta testing

---

## 9. Priority Matrix & Execution Plan

### 9.1 Immediate Priorities (Sprint 1-2)

**Focus:** Complete DDD migration, fix P1 tech debt, establish test baselines

| Priority | Issues | Total Effort |
|----------|--------|--------------|
| **P1** | #1186 (Streaming CQRS), #1187 (Config hardcoding) | ~12-18h |
| **P2** | #1191, #1189, #1188, #1190 (DDD completion) | ~30-42h |
| **High (Testing)** | #1020, #1019, #1018 (Performance, Accuracy, E2E) | TBD |

**Total Estimated Effort:** ~42-60 hours

### 9.2 Short-Term (Month 6: Nov-Dec 2025)

- Complete all P1/P2 DDD migrations
- Establish test baselines (performance, accuracy)
- Document Phase 1A completion (#1023)
- Update user documentation (#1022)

### 9.3 Medium-Term (Month 6-7: Jan-Feb 2026)

- Execute deferred frontend improvements (#1077-1084)
- Complete MVP testing suite (#992-1020)
- Implement optional admin console features (#881-889)

### 9.4 Long-Term (Q2 2026)

- FASE 2 infrastructure monitoring (#890-895)
- Zero tech debt goal
- 95%+ test coverage across all layers

---

## 10. Success Criteria

### 10.1 Code Quality Metrics

- ✅ **Test Coverage:** 90%+ frontend, 90%+ backend (ACHIEVED)
- ✅ **DDD Migration:** 99% complete (TARGET: 100% by end of Sprint 2)
- ⏳ **Tech Debt:** 14 open issues (TARGET: 0 P1 issues by end of Month 6)
- ⏳ **Security:** 1 open issue (TARGET: 0 P1-P2 security issues)

### 10.2 Performance Targets

- ✅ **CI/CD:** ~14min total (38% faster than baseline)
- ⏳ **P95 Latency:** <3s for RAG queries (#1020)
- ⏳ **Accuracy:** 80%+ on 100 Q&A dataset (#1019)
- ⏳ **Dashboard:** <1s load time (#889)

### 10.3 Phase Gates

- **Phase 1A:** Technology viability validated ✅
- **Beta Testing:** 2-4 weeks (Q1 2026)
- **Production:** 10,000 MAU target, >99.5% uptime SLA (Q2 2026)

---

## 11. Risk Assessment

### 11.1 High Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| DDD migration delays (1% remaining) | High | Prioritize P2 issues, allocate dedicated time |
| Test coverage regression | High | Enforce 90%+ in CI/CD, block PRs below threshold |
| Frontend architecture debt (8 deferred issues) | Medium | Plan Month 6 sprint, allocate 2-3 weeks |

### 11.2 Medium Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| Performance testing not establishing baselines | Medium | Prioritize #1020, #1019 in Sprint 1 |
| Admin console optional features scope creep | Medium | Keep FASE 1 optional, FASE 2 deferred |
| Documentation drift | Low | Monthly docs review, update on major changes |

---

## 12. Tracking & Reporting

### 12.1 Weekly Review

- **Metrics:** Test coverage %, open P1/P2 issues, CI/CD duration
- **Burndown:** DDD migration completion, tech debt reduction
- **Blockers:** Identify and escalate blockers within 24h

### 12.2 Monthly Retrospective

- **Achievements:** Closed issues, improved metrics
- **Learnings:** What worked, what didn't
- **Adjustments:** Reprioritize roadmap based on learnings

### 12.3 Dashboard

**GitHub Project Board:** [Code Quality 2025](https://github.com/DegrassiAaron/meepleai-monorepo/projects)

**Labels for Tracking:**
- `tech-debt` (14 issues)
- `testing` (16 issues)
- `refactoring` (4 issues)
- `performance` (5 issues)
- `security` (1 issue)
- `ddd` / `cqrs` (5 issues)

---

## 13. Resources & Ownership

### 13.1 Team Allocation

- **DDD/CQRS Migration:** Engineering Lead (40h)
- **Testing Infrastructure:** QA Lead (80h)
- **Frontend Architecture:** Frontend Lead (60h - Month 6)
- **Security:** Security Lead (10h)

### 13.2 External Dependencies

- **Testcontainers:** Docker environment for integration tests
- **GitHub Actions:** CI/CD runners (14min/run)
- **CodeQL:** Security scanning (weekly)

---

## Appendix A: Issue Summary by Category

### Architecture & DDD (5 issues)
- #1191, #1190, #1189, #1188, #1186

### Refactoring & Tech Debt (6 issues)
- #1194, #1187, #1084, #1083, #1082, #1081, #1080, #1079

### Security (1 issue)
- #1193

### Testing - Backend (7 issues)
- #1020, #1019, #1018, #1009, #1000, #999, #995

### Testing - Frontend (4 issues)
- #1015, #1005, #993, #992

### Testing - Admin Console (5 issues)
- #895, #889, #888, #887, #880

### Frontend Architecture (8 issues)
- #1084, #1083, #1082, #1081, #1080, #1079, #1078, #1077

### Admin Console - FASE 1 (7 issues)
- #886, #885, #884, #883, #882, #881, #878

### Admin Console - FASE 2 (5 issues)
- #894, #893, #892, #891, #890

### Documentation (1 issue)
- #1022

### Milestones (1 issue)
- #1023

**Total Open Quality Issues:** 48

---

## Appendix B: Recently Completed (Nov 15, 2025)

### Critical Fixes
- ✅ #1217 / #1215: P1 deadlock vulnerability (async rate limiting)
- ✅ #1215: Preserve IDs in legacy chat message hydration

### DDD Migration
- ✅ #1219 / #1185: RuleSpecService → CQRS (575 lines)
- ✅ #1214 / #1184: ChatService → CQRS (431 lines)

### Testing
- ✅ #1213 / #1098: 189 unit tests added (7 components, 95%+ coverage)

### Frontend
- ✅ #1209 / #1099: Landing page performance (67% fewer Framer Motion hooks)
- ✅ #1207 / #1100: Keyboard shortcuts system (7+ shortcuts)
- ✅ #1206 / #1101: Advanced search with fuzzy matching (Cmd+K)

### Documentation & Infrastructure
- ✅ #1220: 954 security issues analyzed
- ✅ #1218: README.md files added to all major folders
- ✅ #1216: Documentation reorganized and cleaned up

### Configuration
- ✅ #1208: RateLimitService.GetConfigForRole → async
- ✅ #1212 / #1187: Config values externalized (partial)
- ✅ #1211 / #1192: AsNoTracking optimization applied

---

**Version:** 1.0
**Owner:** Engineering Lead
**Next Review:** 2025-11-22
**Contact:** @DegrassiAaron

---

*This roadmap is a living document. Updates are made weekly based on completed work, new issues, and changing priorities. All issue links point to the live GitHub repository.*
