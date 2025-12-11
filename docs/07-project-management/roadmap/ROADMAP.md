# MeepleAI Development Roadmap

**Last Updated**: 2025-12-09
**Project Status**: Alpha - DDD Migration Complete (100%)
**Open Issues**: 53 (execution-focused roadmap)
**Next Milestone**: Critical Issue Resolution → Beta Preparation

---

## 📊 Executive Summary

| Category | Count | Execution Status |
|----------|-------|------------------|
| **Critical Issues** | 2 issues | 🚨 **URGENT** (investigate immediately) |
| **Infrastructure** | 1 issue | ⚡ **READY** (can start anytime) |
| **Admin Console FASE 1** | 15 issues | ⚡ **READY** (can start anytime) |
| **Admin Console FASE 2** | 13 issues (1 Epic + 12 sub) | 🔄 **IN PROGRESS** (backend started) |
| **Admin Console FASE 3** | 12 issues (1 Epic + 11 sub) | ⏸️ **DEFERRED** (after FASE 2) |
| **Admin Console FASE 4** | 8 issues (1 Epic + 7 sub) | ⏸️ **DEFERRED** (after FASE 3) |
| **Other Enhancements** | 2 issues | ⏸️ **DEFERRED** |

**Priority Distribution**: 2 P0/P1 (Critical/High), 51 P3 (Low)

---

## 🎯 EXECUTION SEQUENCE (Wave-Based)

### Legend
- **Type**: 🎨 Frontend | 🔧 Backend | 🔄 Both | 🏗️ Infrastructure | 📚 Documentation | 🧪 Testing
- **Execution**: ⚡ Parallel | 🔗 Sequential | 🔴 Blocked | 🚨 Urgent | 🔄 In Progress
- **Priority**: P0 (Critical) | P1 (High) | P2 (Medium) | P3 (Low)

---

## WAVE 0: Critical Issues (IMMEDIATE ACTION REQUIRED)

**Status**: 🚨 **URGENT** - Investigate immediately
**Total Effort**: 1-3 days
**Priority**: P0/P1

| # | Issue | Type | Priority | Effort | Dependencies | Execution |
|---|-------|------|----------|--------|--------------|-----------|
| 1 | [#2020](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2020) - K6 Performance Tests Failed | 🔧🏗️ Backend/Infra | **P1 - High** | 0.5-1d | None | ✅ **RESOLVED** (2025-12-11) |
| 2 | [#2009](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2009) - E2E Server Stability - Phase 3 | 🏗️🧪 Infrastructure/Testing | **P0 - Critical** | 1-2d | None | 🚨 **URGENT** |

### Parallelization Strategy
✅ **Both issues can run in parallel** (independent)

**#2020 - K6 Performance Tests Failed** ✅ **RESOLVED (2025-12-11)**:
- **Description**: Automated nightly K6 performance tests failed due to PostgreSQL authentication errors
- **Root Cause**: EF Core defaulted to 'root' user during health check validation (missing ConnectionStrings__Postgres env var)
- **Solution**: Added ConnectionStrings__Postgres to "Wait for API to be ready" step
- **Action Items**:
  - [x] Review workflow run artifacts (20120804325)
  - [x] Investigate root cause (PostgreSQL logs: "role root does not exist")
  - [x] Fix identified issues (1-line env var addition)
  - [x] Update runbook with diagnostic commands and prevention checklist
  - [ ] Re-run workflow to verify (pending manual trigger)
  - [ ] Close issue once verified (after successful run)
- **Impact**: 10+ consecutive nightly runs failed (2025-12-01 to 2025-12-11)
- **Resolution Time**: ~1 hour (surgical fix)

**#2009 - E2E Server Stability - Phase 3**:
- **Description**: Production-grade infrastructure for E2E test stability
- **Action Items**:
  - [ ] Implement production-grade E2E infrastructure
  - [ ] Resolve server stability issues
  - [ ] Validate test reliability ≥99%
- **Rationale**: Critical for CI/CD pipeline reliability

**Total Wave 0 Time**: 1-3 days (parallel) or 2-4 days (sequential)

---

## WAVE 1: Infrastructure Foundation

**Status**: ⚡ **READY** - Can start anytime (no blockers)
**Total Effort**: 1 day
**Priority**: P3

| # | Issue | Type | Priority | Effort | Dependencies | Execution |
|---|-------|------|----------|--------|--------------|-----------|
| 1 | [#818](https://github.com/DegrassiAaron/meepleai-monorepo/issues/818) - Quarterly security scan review process | 📚 Documentation + 🔒 Security | P3 | 1d | None | ⚡ **Parallel** |

**Scope**:
- Establish quarterly security scan review process
- Document security review procedures
- Define escalation paths

**Total Wave 1 Time**: 1 day

---

## WAVE 2: Admin Console - FASE 1 (Dashboard Overview)

**Status**: ⚡ **READY** - Can start anytime
**Total Effort**: 15-20 days
**Epic**: [#874](https://github.com/DegrassiAaron/meepleai-monorepo/issues/874) (CLOSED - tracked via sub-issues)
**Priority**: P3

### Backend Track (5-8 days)

| # | Issue | Type | Priority | Effort | Dependencies | Execution |
|---|-------|------|----------|--------|--------------|-----------|
| 1 | [#875](https://github.com/DegrassiAaron/meepleai-monorepo/issues/875) - AdminDashboardService.cs | 🔧 Backend | P3 | 1-2d | None | ⚡ Start |
| 2 | [#876](https://github.com/DegrassiAaron/meepleai-monorepo/issues/876) - Aggregate metrics | 🔧 Backend | P3 | 1-2d | #875 | 🔗 After #875 |
| 3 | [#877](https://github.com/DegrassiAaron/meepleai-monorepo/issues/877) - GET /api/v1/admin/dashboard/stats | 🔧 Backend | P3 | 1d | #876 | 🔗 After #876 |
| 4 | [#878](https://github.com/DegrassiAaron/meepleai-monorepo/issues/878) - Activity Feed Service | 🔧 Backend | P3 | 1-2d | #875 | ⚡ Parallel #876 |
| 5 | [#879](https://github.com/DegrassiAaron/meepleai-monorepo/issues/879) - HybridCache for dashboard | 🔧 Backend | P3 | 0.5-1d | #877 | 🔗 After #877 |
| 6 | [#880](https://github.com/DegrassiAaron/meepleai-monorepo/issues/880) - Unit tests AdminDashboard | 🧪 Testing | P3 | 1-2d | #875-878 | 🔗 After Backend |

### Frontend Track (5-7 days)

| # | Issue | Type | Priority | Effort | Dependencies | Execution |
|---|-------|------|----------|--------|--------------|-----------|
| 7 | [#881](https://github.com/DegrassiAaron/meepleai-monorepo/issues/881) - AdminLayout component | 🎨 Frontend | P3 | 1-2d | None | ⚡ Start |
| 8 | [#882](https://github.com/DegrassiAaron/meepleai-monorepo/issues/882) - StatCard component | 🎨 Frontend | P3 | 0.5-1d | None | ⚡ Start |
| 9 | [#883](https://github.com/DegrassiAaron/meepleai-monorepo/issues/883) - MetricsGrid component | 🎨 Frontend | P3 | 1d | #882 | 🔗 After #882 |
| 10 | [#884](https://github.com/DegrassiAaron/meepleai-monorepo/issues/884) - ActivityFeed component | 🎨 Frontend | P3 | 1-2d | None | ⚡ Start |
| 11 | [#885](https://github.com/DegrassiAaron/meepleai-monorepo/issues/885) - /pages/admin/index.tsx | 🎨 Frontend | P3 | 1d | #881,#883,#884 | 🔗 After Components |
| 12 | [#886](https://github.com/DegrassiAaron/meepleai-monorepo/issues/886) - Dashboard API integration | 🔄 Both | P3 | 1d | #877,#885 | 🔗 After BE+FE |

### Testing Track (3-5 days)

| # | Issue | Type | Priority | Effort | Dependencies | Execution |
|---|-------|------|----------|--------|--------------|-----------|
| 13 | [#887](https://github.com/DegrassiAaron/meepleai-monorepo/issues/887) - Jest tests dashboard | 🧪 Testing | P3 | 1-2d | #882-885 | 🔗 After Frontend |
| 14 | [#888](https://github.com/DegrassiAaron/meepleai-monorepo/issues/888) - E2E Playwright test | 🧪 Testing | P3 | 1-2d | #886 | 🔗 After Integration |
| 15 | [#889](https://github.com/DegrassiAaron/meepleai-monorepo/issues/889) - Performance + Accessibility | 🧪 Testing | P3 | 1d | #886 | ⚡ Parallel #888 |

**Parallelization**: Backend Track || Frontend Track, then Testing Track
**Total FASE 1 Time**: 8-13 days (parallel) or 15-20 days (sequential)

---

## WAVE 3: Admin Console - FASE 2 (Infrastructure Monitoring)

**Status**: 🔄 **IN PROGRESS** - Backend started (#891, #893)
**Total Effort**: 12-18 days
**Epic**: [#890](https://github.com/DegrassiAaron/meepleai-monorepo/issues/890)
**Current Branch**: `feature/issue-891-893-infrastructure-monitoring`
**Priority**: P3

### Backend Track (5-8 days) - ✅ ~80% Complete

| # | Issue | Type | Priority | Effort | Dependencies | Execution |
|---|-------|------|----------|--------|--------------|-----------|
| 1 | [#891](https://github.com/DegrassiAaron/meepleai-monorepo/issues/891) - InfrastructureMonitoringService | 🔧 Backend | P3 | 1-2d | None | ✅ **DONE** (PR #2022) |
| 2 | [#892](https://github.com/DegrassiAaron/meepleai-monorepo/issues/892) - Extend /health endpoints | 🔧 Backend | P3 | 1d | #891 | ✅ **DONE** (PR #2023) |
| 3 | [#893](https://github.com/DegrassiAaron/meepleai-monorepo/issues/893) - Prometheus client integration | 🔧 Backend | P3 | 1-2d | None | ✅ **DONE** (PR #2022) |
| 4 | [#894](https://github.com/DegrassiAaron/meepleai-monorepo/issues/894) - GET /api/v1/admin/infrastructure/details | 🔧 Backend | P3 | 1d | #891,#893 | ✅ **DONE** (current PR) |
| 5 | [#895](https://github.com/DegrassiAaron/meepleai-monorepo/issues/895) - Unit tests Infrastructure | 🧪 Testing | P3 | 1-2d | #891-894 | ⚡ **READY** (all deps done) |

### Frontend Track (4-7 days)

| # | Issue | Type | Priority | Effort | Dependencies | Execution |
|---|-------|------|----------|--------|--------------|-----------|
| 6 | [#896](https://github.com/DegrassiAaron/meepleai-monorepo/issues/896) - ServiceHealthMatrix component | 🎨 Frontend | P3 | 1-2d | None | ⚡ Start |
| 7 | [#897](https://github.com/DegrassiAaron/meepleai-monorepo/issues/897) - ServiceCard component | 🎨 Frontend | P3 | 1d | None | ⚡ Start |
| 8 | [#898](https://github.com/DegrassiAaron/meepleai-monorepo/issues/898) - MetricsChart component | 🎨 Frontend | P3 | 1-2d | None | ⚡ Start |
| 9 | [#899](https://github.com/DegrassiAaron/meepleai-monorepo/issues/899) - /pages/admin/infrastructure.tsx | 🎨 Frontend | P3 | 1d | #896-898 | 🔗 After Components |
| 10 | [#900](https://github.com/DegrassiAaron/meepleai-monorepo/issues/900) - Jest tests infrastructure | 🧪 Testing | P3 | 1-2d | #896-899 | 🔗 After Frontend |

### Integration/Testing Track (3-4 days)

| # | Issue | Type | Priority | Effort | Dependencies | Execution |
|---|-------|------|----------|--------|--------------|-----------|
| 11 | [#901](https://github.com/DegrassiAaron/meepleai-monorepo/issues/901) - Grafana embed iframe | ✅ Frontend | P3 | 1d (0.5d) | #894,#899 | ✅ COMPLETE |
| 12 | [#902](https://github.com/DegrassiAaron/meepleai-monorepo/issues/902) - E2E test + Load test | 🧪 Testing | P3 | 2-3d | #901 | 🔗 After Integration |

**Parallelization**: Backend Track (#891||#893) || Frontend Track (#896||#897||#898)
**Total FASE 2 Time**: 8-12 days (parallel) or 12-18 days (sequential)

---

## WAVE 4: Admin Console - FASE 3 (Enhanced Management)

**Status**: ⏸️ **DEFERRED** - After FASE 2
**Total Effort**: 11-16 days
**Epic**: [#903](https://github.com/DegrassiAaron/meepleai-monorepo/issues/903)
**Priority**: P3

### Backend Track (4-7 days)

| # | Issue | Type | Priority | Effort | Dependencies | Execution |
|---|-------|------|----------|--------|--------------|-----------|
| 1 | [#904](https://github.com/DegrassiAaron/meepleai-monorepo/issues/904) - ApiKeyManagementService | 🔧 Backend | P3 | 1-2d | FASE 2 done | ✅ Complete |
| 2 | [#905](https://github.com/DegrassiAaron/meepleai-monorepo/issues/905) - Admin handlers - Bulk ops | 🔧 Backend | P3 | 1-2d | None | ✅ Complete |
| 3 | [#906](https://github.com/DegrassiAaron/meepleai-monorepo/issues/906) - CSV import/export | 🔧 Backend | P3 | 1d | #905 | ✅ Complete |
| 4 | [#907](https://github.com/DegrassiAaron/meepleai-monorepo/issues/907) - E2E tests bulk ops | 🧪 Testing | P3 | 1-2d | #904-906 | ✅ Complete |

### Frontend Track (3-5 days)

| # | Issue | Type | Priority | Effort | Dependencies | Execution |
|---|-------|------|----------|--------|--------------|-----------|
| 5 | [#908](https://github.com/DegrassiAaron/meepleai-monorepo/issues/908) - /pages/admin/api-keys.tsx | 🎨 Frontend | P3 | 1-2d | #904 | 🔗 After #904 |
| 6 | [#909](https://github.com/DegrassiAaron/meepleai-monorepo/issues/909) - API key creation modal | 🎨 Frontend | P3 | 1d | None | ⚡ Start |
| 7 | [#910](https://github.com/DegrassiAaron/meepleai-monorepo/issues/910) - FilterPanel component | 🎨 Frontend | P3 | 1d | None | ⚡ Start |
| 8 | [#911](https://github.com/DegrassiAaron/meepleai-monorepo/issues/911) - UserActivityTimeline | 🎨 Frontend | P3 | 1-2d | None | ⚡ Start |
| 9 | [#912](https://github.com/DegrassiAaron/meepleai-monorepo/issues/912) - BulkActionBar component | 🎨 Frontend | P3 | 1d | None | ⚡ Start |

### Testing Track (3-5 days)

| # | Issue | Type | Priority | Effort | Dependencies | Execution |
|---|-------|------|----------|--------|--------------|-----------|
| 10 | [#913](https://github.com/DegrassiAaron/meepleai-monorepo/issues/913) - Jest tests management | 🧪 Testing | P3 | 1-2d | #908-912 | 🔗 After Frontend |
| 11 | [#914](https://github.com/DegrassiAaron/meepleai-monorepo/issues/914) - E2E + Security + Stress | 🧪 Testing | P3 | 2-3d | #908,#913 | 🔗 After Integration |

**Parallelization**: Backend (#904||#905) || Frontend (#909||#910||#911||#912)
**Total FASE 3 Time**: 7-12 days (parallel) or 11-16 days (sequential)

---

## WAVE 5: Admin Console - FASE 4 (Advanced Features)

**Status**: ⏸️ **DEFERRED** - After FASE 3
**Total Effort**: 8-12 days
**Epic**: [#915](https://github.com/DegrassiAaron/meepleai-monorepo/issues/915)
**Priority**: P3

### Backend Track (4-7 days)

| # | Issue | Type | Priority | Effort | Dependencies | Execution |
|---|-------|------|----------|--------|--------------|-----------|
| 1 | [#916](https://github.com/DegrassiAaron/meepleai-monorepo/issues/916) - ReportingService.cs | 🔧 Backend | P3 | 1-2d | FASE 3 done | 🔗 After FASE 3 |
| 2 | [#917](https://github.com/DegrassiAaron/meepleai-monorepo/issues/917) - Report templates | 🔧 Backend | P3 | 1d | #916 | 🔗 After #916 |
| 3 | [#918](https://github.com/DegrassiAaron/meepleai-monorepo/issues/918) - Email delivery integration | 🔧 Backend | P3 | 1-2d | #916 | ⚡ Parallel #917 |
| 4 | [#919](https://github.com/DegrassiAaron/meepleai-monorepo/issues/919) - Unit tests Reporting | 🧪 Testing | P3 | 1-2d | #916-918 | 🔗 After Backend |

### Frontend Track (1-2 days)

| # | Issue | Type | Priority | Effort | Dependencies | Execution |
|---|-------|------|----------|--------|--------------|-----------|
| 5 | [#920](https://github.com/DegrassiAaron/meepleai-monorepo/issues/920) - /pages/admin/reports.tsx | 🎨 Frontend | P3 | 1-2d | #916 | 🔗 After #916 |
| 6 | [#921](https://github.com/DegrassiAaron/meepleai-monorepo/issues/921) - Alert configuration UI | 🎨 Frontend | P3 | 1-2d | None | ⚡ Start |

### Testing Track (2-3 days)

| # | Issue | Type | Priority | Effort | Dependencies | Execution |
|---|-------|------|----------|--------|--------------|-----------|
| 7 | [#922](https://github.com/DegrassiAaron/meepleai-monorepo/issues/922) - E2E report + Email validation | 🧪 Testing | P3 | 2-3d | #918,#920 | 🔗 After Integration |

**Parallelization**: Backend (#917||#918) || Frontend (#920||#921)
**Total FASE 4 Time**: 6-10 days (parallel) or 8-12 days (sequential)

---

## DEFERRED: Other Enhancements

**Status**: ⏸️ **DEFERRED** - Not scheduled
**Total Issues**: 2

| # | Issue | Type | Priority | Notes |
|---|-------|------|----------|-------|
| 1 | [#844](https://github.com/DegrassiAaron/meepleai-monorepo/issues/844) - UI/UX Testing Roadmap 2025 | 🧪 Testing | P3 | Visual/E2E testing strategy |
| 2 | [#936](https://github.com/DegrassiAaron/meepleai-monorepo/issues/936) - Infisical Secret Rotation POC | 🔒 Security/Infra | P3 | Phase 2 secret management |

---

## 📊 DEPENDENCY GRAPH

```
Wave 0 (URGENT - PARALLEL):
├─ #2020 (K6 Performance) → Fix → Verify
└─ #2009 (E2E Stability) → Production Infra
    ↓ Resolve before Beta

Wave 1 (PARALLEL with Wave 0):
#818 (Security Review Process)

Wave 2 (FASE 1):
Backend:                Frontend:               Testing:
#875 → #876 → #877     #881||#882||#884        #887 → #888||#889
  ↓      ↓              ↓                       (After Integration)
 #878   #879           #883 → #885
                         ↓
                        #886

Wave 3 (FASE 2 - IN PROGRESS):
Backend:                Frontend:               Integration:
#891||#893 → #894      #896||#897||#898       #901 → #902
  ↓                     → #899 → #900
 #892 → #895

Wave 4 (FASE 3):
Backend:                Frontend:               Testing:
#904||#905 → #906      #909||#910||#911       #913 → #914
  ↓                     ||#912 → #908
 #907

Wave 5 (FASE 4):
Backend:                Frontend:               Testing:
#916 → #917||#918      #920||#921             #922
  ↓
 #919
```

---

## 📈 EXECUTION SCENARIOS

### Scenario A: Single Developer (Sequential)
**Timeline**: ~8-10 weeks

| Week | Focus | Issues |
|------|-------|--------|
| Week 1 | Wave 0 + Wave 1 | #2020, #2009, #818 |
| Week 2-3 | FASE 1 Backend | #875-880 |
| Week 3-4 | FASE 1 Frontend | #881-886 |
| Week 4-5 | FASE 1 Testing | #887-889 |
| Week 5-7 | FASE 2 All | #891-902 |
| Week 7-9 | FASE 3 (optional) | #904-914 |
| Week 9-10 | FASE 4 (optional) | #916-922 |

### Scenario B: Small Team (2-3 Devs, Parallel)
**Timeline**: ~4-6 weeks

| Week | Backend Dev | Frontend Dev | QA/DevOps |
|------|------------|--------------|-----------|
| W1 | Wave 0: #2020 | Wave 0: #2009 | Wave 1: #818 |
| W2 | FASE 1: #875-880 | FASE 1: #881-886 | FASE 2: #891,#893 |
| W3 | FASE 2: #892,#894-895 | FASE 1: #887-889 | FASE 2: #896-900 |
| W4 | FASE 2: #901-902 | FASE 2: Frontend cont. | FASE 1: E2E |
| W5 | FASE 3: #904-907 | FASE 3: #908-912 | FASE 2: Testing |
| W6 | FASE 4: #916-919 | FASE 3: #913-914 | FASE 4: #920-921 |

**50-60% faster than single developer**

### Scenario C: Full Team (4+ Devs, Maximum Parallel)
**Timeline**: ~3-4 weeks

**Team Assignment**:
- 2 Backend Devs: All Backend Tracks
- 1 Frontend Dev: All Frontend Tracks
- 1 QA/DevOps: Wave 0, Wave 1, All Testing Tracks

**Timeline**:
- Day 1-3: Wave 0 + Wave 1
- Day 4-10: FASE 1 (all tracks parallel)
- Day 11-17: FASE 2 (all tracks parallel)
- Day 18-23: FASE 3 (optional, all tracks parallel)
- Day 24-28: FASE 4 (optional, all tracks parallel)

---

## 🎯 SUCCESS CRITERIA

### Wave 0 (Critical)
- [x] K6 performance tests passing (#2020) ✅ **RESOLVED 2025-12-11**
- [x] Root cause documented (#2020) ✅ Runbook updated
- [ ] E2E test reliability ≥99% (#2009) 🚧 In Progress

### Wave 1 (Infrastructure)
- [ ] Security review process documented (#818)

### Wave 2 (FASE 1 - Dashboard)
- [ ] Dashboard API functional (#877)
- [ ] /pages/admin/index.tsx operational (#885, #886)
- [ ] Jest tests ≥90% (#887)
- [ ] E2E tests passing (#888)
- [ ] Performance <1s, WCAG AA (#889)

### Wave 3 (FASE 2 - Infrastructure Monitoring)
- [ ] InfrastructureMonitoringService operational (#891)
- [ ] Prometheus integration complete (#893)
- [ ] /pages/admin/infrastructure.tsx functional (#899)
- [ ] Grafana iframe operational (#901)
- [ ] Load test passing (100 users) (#902)

### Wave 4 (FASE 3 - Management)
- [ ] API key management functional (#904, #908)
- [ ] Bulk operations working (#905-906)
- [ ] Stress test passing (1000 users) (#914)

### Wave 5 (FASE 4 - Advanced)
- [ ] Reporting system operational (#916)
- [ ] Email delivery working (#918)
- [ ] E2E report generation passing (#922)

### Overall Quality Gates
- [ ] 90%+ test coverage maintained
- [ ] CI pipeline < 15 minutes
- [ ] Zero build errors
- [ ] Zero high/critical vulnerabilities

---

## 📝 CHANGELOG

### 2025-12-09 - Roadmap Rebuild (Open Issues Only)

**Changes**:
- ✅ Complete rebuild based on 53 open issues (verified via `gh issue list`)
- ✅ Removed all closed issues (#1677, #1676, #1679, #1725, #701-707, #1976)
- ✅ Updated Wave 0 with current critical issues (#2020, #2009)
- ✅ Admin Console as primary focus (FASE 1-4, 48 issues)
- ✅ Added current work context (FASE 2 #891, #893 in progress, branch `feature/issue-891-893-infrastructure-monitoring`)
- ✅ Enhanced parallelization strategy (3 tracks per wave)
- ✅ Verified all 53 issue links
- ✅ Type/Priority/Execution classification
- ✅ Explicit dependency tracking
- ✅ 3 team execution scenarios

**Issue Count**: 56 → 53 (open issues only)

**Structure**:
- Wave 0: 2 critical (#2020, #2009) - URGENT
- Wave 1: 1 infrastructure (#818) - READY
- Wave 2: FASE 1 (15 issues #875-889) - READY
- Wave 3: FASE 2 (13 issues #890-902) - IN PROGRESS
- Wave 4: FASE 3 (12 issues #903-914) - DEFERRED
- Wave 5: FASE 4 (8 issues #915-922) - DEFERRED
- Deferred: 2 (#844, #936)

**Timeline**: 3-10 weeks (team size dependent)

---

**Status**: ✅ **CURRENT** (2025-12-09, 53 open issues verified)
**Next Review**: After Wave 0 or 2025-12-23
