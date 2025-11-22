# Piano Roadmap - MeepleAI Monorepo

**Repository**: [DegrassiAaron/meepleai-monorepo](https://github.com/DegrassiAaron/meepleai-monorepo/issues)
**Total Open Issues**: 133
**Last Updated**: 2025-11-21

---

## 📊 Executive Summary

### Issues by Priority
- **🔴 High Priority**: 15 issues (HyperDX migration, critical testing, BGAI features)
- **🟡 Medium Priority**: 14 issues (Testing improvements, frontend refactoring)
- **🟢 Low Priority**: 30+ issues (Deferred features, Phase 4 advanced features)
- **⚪ Unspecified**: 70+ issues (Various maintenance and enhancements)

### Issues by Area
- **🔭 Observability (HyperDX Migration)**: 10 issues (#1561-#1570)
- **🧪 Testing Framework**: 20+ issues (E2E, Unit, Integration, Performance)
- **🎮 Board Game AI (BGAI)**: 25+ issues (Phase 1A, MVP features)
- **👨‍💼 Admin Console**: 20+ issues (Phases 2-4, mostly deferred)
- **🔐 Security**: 2 issues (Penetration testing, 2FA admin override)
- **🏗️ Infrastructure**: 8+ issues (Docker, monitoring, backups)

---

## 🔴 High Priority Issues

### 🚀 EPIC: HyperDX Observability Migration (#1561)
**Status**: 🔥 Critical - In Progress
**Goal**: Replace Seq + Jaeger with unified HyperDX platform

#### HyperDX Implementation Tasks
| # | Task | Area | Status |
|---|------|------|--------|
| 1570 | ✅ Final Verification and Go-Live Checklist | Documentation | Open |
| 1569 | 📚 Update Documentation for HyperDX Migration | Documentation | Open |
| 1568 | 📊 Load Testing and Performance Validation | Testing | Open |
| 1567 | 🚨 Configure HyperDX Application Alerts | Infrastructure | Open |
| 1566 | ⚛️ Implement HyperDX Browser SDK (Next.js) | Frontend | Open |
| 1565 | 🧪 Integration Testing - Backend Telemetry | Testing | Open |
| 1564 | 🗑️ Remove Seq and Jaeger Services | Infrastructure | Open |
| 1563 | ⚙️ Configure .NET API OpenTelemetry for HyperDX | Backend | Open |
| 1562 | 🐳 Deploy HyperDX Docker Container | Infrastructure | Open |

**Impact**: Unified observability, reduced infrastructure complexity, improved debugging

---

### 🐛 Critical Bug Fixes

#### #1550 - Backend Test Suite Failures
**Priority**: 🔴 High
**Status**: 144 tests failing + 1 crash
**Impact**: Blocking CI/CD pipeline
**Action Required**: Immediate investigation and fix

---

### 🧪 High Priority Testing

#### Frontend E2E Tests
| # | Task | Description | Status |
|---|------|-------------|--------|
| 1491 | ✅ Fix Demo Login Tests | Currently skipped, needs fixing | Open |
| 1490 | 🔐 Add RBAC Authorization Tests | 10+ tests for role-based access | Open |

#### Board Game AI Quality Tests
| # | Task | Description | Status |
|---|------|-------------|--------|
| 1000 | Run first accuracy test | Baseline measurement for RAG quality | Open |
| 999 | Quality test implementation | Accuracy validation framework | Open |

#### Board Game AI Annotation Tasks
| # | Task | Dataset | Q&A Pairs | Status |
|---|------|---------|-----------|--------|
| 998 | Annotation: Azul | Azul rules | 15 pairs | Open |
| 997 | Annotation: Wingspan | Wingspan rules | 15 pairs | Open |
| 996 | Annotation: Terraforming Mars | TM rules | 20 pairs | Open |

---

### 🎮 Board Game AI - MVP Frontend Components
| # | Component | Description | Status |
|---|-----------|-------------|--------|
| 1004 | Loading and error states | UI/UX improvements | Open |
| 1003 | GameSelector dropdown | Game selection component | Open |
| 1002 | ResponseCard component | Answer display with citations | Open |
| 1001 | QuestionInputForm | Question input interface | Open |

---

## 🟡 Medium Priority Issues

### 🧪 Testing Framework Improvements

#### Backend Testing
| # | Task | Description | Status |
|---|------|-------------|--------|
| 1507 | 📁 Remove Excessive Regions | Code organization cleanup | Open |
| 1506 | 📊 Consolidate with Theory Tests | Reduce test duplication | Open |
| 1505 | 🔢 Reduce Magic Numbers | Improve test readability | Open |

#### Frontend Testing
| # | Task | Description | Status |
|---|------|-------------|--------|
| 1511 | 🎨 Expand Visual Regression Tests | Chromatic test coverage | Open |
| 1510 | 🔌 Add API SDK Integration Tests | API client testing | Open |
| 1509 | ⚡ Add Performance Tests | Frontend performance benchmarks | Open |
| 1508 | 🛡️ Add Error Boundary Tests | Error handling coverage | Open |

#### E2E Testing
| # | Task | Description | Status |
|---|------|-------------|--------|
| 1494 | ❌ Add Negative Test Scenarios | 15+ negative tests | Open |
| 1493 | ⏱️ Reduce Hardcoded Timeouts | 67 occurrences to fix | Open |
| 1492 | 📦 Complete POM Migration | 30 test files to migrate | Open |

---

### 🔄 Frontend Refactoring

#### #1436 - Fix SWR + Zustand State Duplication in Chat
**Priority**: 🟡 Medium
**Area**: Frontend
**Type**: Refactoring
**Impact**: Improved state management, reduced duplication

---

### 📉 Code Quality

#### #1255 - Frontend Coverage Dropped to 66%
**Priority**: 🟡 Medium
**Target**: 90% coverage
**Current**: 66%
**Action**: Add missing tests, improve coverage

---

### 🎮 Board Game AI - Phase 1A

#### Month 4-5 Tasks
| # | Task | Area | Status |
|---|------|------|--------|
| 1023 | BGAI Phase 1A Completion | Meta | Open |
| 1022 | Documentation updates | Docs | Open |
| 1021 | Bug fixes and polish | Bug Fix | Open |
| 1020 | Performance optimization | Performance | Open |
| 1019 | Validation testing | Testing | Open |
| 1018 | Integration testing | Testing | Open |
| 995 | Month 4 integration testing | E2E Testing | Open |
| 994 | Frontend build optimization | DevOps | Open |

---

## 🟢 Low Priority & Deferred Issues

### 👨‍💼 Admin Console - Phase 4 (Advanced Features)

#### EPIC #915 - Reporting System + Advanced Alerting
**Status**: Deferred
**Priority**: 🟢 Low

| # | Task | Area | Status |
|---|------|------|--------|
| 920 | Report builder UI | Frontend | Deferred |
| 919 | Unit tests ReportingService | Testing | Deferred |
| 918 | Email delivery integration | Backend | Deferred |
| 917 | Report templates | Backend | Deferred |
| 916 | ReportingService.cs | Backend | Deferred |

---

### 👨‍💼 Admin Console - Phase 3 (User Management)

#### Testing & Components
| # | Task | Area | Status |
|---|------|------|--------|
| 914 | E2E + Security audit + Stress test | Testing | Deferred |
| 913 | Jest tests management components | Testing | Deferred |
| 912 | BulkActionBar component | Frontend | Deferred |
| 911 | UserActivityTimeline component | Frontend | Deferred |
| 910 | FilterPanel component | Frontend | Deferred |

---

### 👨‍💼 Admin Console - Phase 2 (Infrastructure Monitoring)

#### EPIC #890 - Multi-Service Health Checks
**Status**: Deferred
**Priority**: 🟢 Low

| # | Task | Area | Status |
|---|------|------|--------|
| 895 | Unit tests InfrastructureMonitoringService | Testing | Deferred |
| 894 | GET /api/v1/admin/infrastructure/details | Backend | Deferred |
| 893 | Prometheus client integration | Backend | Deferred |
| 892 | Extend /health endpoints | Backend | Deferred |
| 891 | InfrastructureMonitoringService.cs | Backend | Deferred |
| 889 | Performance + Accessibility testing | Testing | Optional |
| 888 | E2E Playwright test - Dashboard | Testing | Optional |
| 887 | Jest tests dashboard components | Testing | Optional |
| 886 | Dashboard API integration | Frontend | Optional |
| 885 | Dashboard page | Frontend | Optional |

---

### 🏗️ Infrastructure Enhancements

| # | Task | Priority | Status |
|---|------|----------|--------|
| 706 | 📖 Create operational runbooks | Low | Open |
| 705 | 📊 Add infrastructure monitoring (cAdvisor + node-exporter) | Low | Open |
| 704 | 💾 Create backup automation scripts | Low | Deferred |
| 703 | 🌐 Add Traefik reverse proxy layer | Low | Open |
| 702 | 🎯 Docker Compose profiles for selective startup | Low | Open |
| 701 | ⚡ Add resource limits to Docker services | Low | Open |

---

### 🔐 Security Tasks

| # | Task | Priority | Status |
|---|------|----------|--------|
| 576 | SEC-05: AUTH-07 Security Penetration Testing | Unspecified | Open |
| 575 | AUTH-08: Admin Override for 2FA Locked-Out Users | Unspecified | Open |

---

## 📅 Suggested Execution Plan

### Phase 1: Critical Stabilization (1-2 weeks)
**Goal**: Fix critical bugs and stabilize test suite

1. **#1550** - Fix backend test suite failures (144 tests + 1 crash)
2. **#1491** - Fix demo login tests
3. **#1255** - Restore frontend coverage to 90%

**Success Criteria**: All tests passing, coverage at 90%+

---

### Phase 2: HyperDX Migration (2-3 weeks)
**Goal**: Complete unified observability platform

**Week 1-2: Implementation**
1. **#1562** - Deploy HyperDX Docker container
2. **#1563** - Configure .NET API OpenTelemetry
3. **#1566** - Implement HyperDX Browser SDK
4. **#1567** - Configure application alerts
5. **#1565** - Integration testing

**Week 3: Validation & Cutover**
6. **#1568** - Load testing and performance validation
7. **#1564** - Remove Seq and Jaeger services
8. **#1569** - Update documentation
9. **#1570** - Final verification and go-live

**Success Criteria**: HyperDX operational, legacy services removed, documentation updated

---

### Phase 3: Testing Framework Enhancement (2-3 weeks)
**Goal**: Improve test quality and coverage

**Backend Tests**
- **#1507** - Remove excessive regions
- **#1506** - Consolidate with Theory tests
- **#1505** - Reduce magic numbers

**Frontend Tests**
- **#1511** - Expand visual regression tests
- **#1510** - Add API SDK integration tests
- **#1509** - Add performance tests
- **#1508** - Add error boundary tests

**E2E Tests**
- **#1494** - Add negative test scenarios (15+ tests)
- **#1493** - Reduce hardcoded timeouts (67 occurrences)
- **#1492** - Complete POM migration (30 files)
- **#1490** - Add RBAC authorization tests (10+ tests)

**Success Criteria**: 90%+ coverage maintained, improved test maintainability

---

### Phase 4: Board Game AI MVP (3-4 weeks)
**Goal**: Complete BGAI Phase 1A and achieve baseline quality

**Week 1: Dataset Preparation**
- **#996** - Annotate Terraforming Mars (20 Q&A pairs)
- **#997** - Annotate Wingspan (15 Q&A pairs)
- **#998** - Annotate Azul (15 Q&A pairs)

**Week 2: Frontend Components**
- **#1001** - QuestionInputForm component
- **#1003** - GameSelector dropdown component
- **#1002** - ResponseCard component
- **#1004** - Loading and error states

**Week 3: Quality Framework**
- **#999** - Quality test implementation
- **#1000** - Run first accuracy test (baseline)

**Week 4: Integration & Testing**
- **#995** - Month 4 integration testing
- **#1018-#1023** - BGAI Phase 1A completion tasks

**Success Criteria**: MVP frontend operational, baseline accuracy measured

---

### Phase 5: Frontend Optimization (1-2 weeks)
**Goal**: Improve state management and build performance

- **#1436** - Fix SWR + Zustand state duplication in Chat
- **#994** - Frontend build optimization

**Success Criteria**: Reduced state duplication, faster build times

---

### Phase 6: Security & Infrastructure (Ongoing)
**Goal**: Harden security and improve infrastructure

**Security**
- **#576** - Security penetration testing
- **#575** - Admin override for 2FA locked-out users

**Infrastructure**
- **#701-#706** - Docker optimization, monitoring, backups, runbooks

**Success Criteria**: Security audit passed, infrastructure hardened

---

### Phase 7+: Deferred Features (Future)
**Goal**: Advanced admin console features

- **Phase 2**: Infrastructure monitoring (#890-#895)
- **Phase 3**: User management enhancements (#910-#914)
- **Phase 4**: Reporting system (#915-#920)

**Timeline**: Q2-Q3 2025 (after MVP launch)

---

## 📊 Metrics & Success Criteria

### Quality Gates
- ✅ **Test Coverage**: ≥90% (frontend + backend)
- ✅ **Test Pass Rate**: 100% (0 failing tests)
- ✅ **Build Time**: <5 minutes
- ✅ **E2E Test Stability**: <5% flakiness
- ✅ **Code Quality**: 0 critical SonarQube issues

### Observability Metrics
- ✅ **Log Ingestion**: <2s latency
- ✅ **Trace Completion**: >95% success rate
- ✅ **Alert Delivery**: <1 minute
- ✅ **Dashboard Load Time**: <3s

### BGAI Quality Metrics
- ✅ **Accuracy**: ≥80% on golden dataset
- ✅ **Hallucination Rate**: ≤10%
- ✅ **Confidence Score**: ≥0.70 average
- ✅ **Citation Accuracy**: ≥80%
- ✅ **Response Latency**: ≤3s P95

---

## 🎯 Priority Matrix

### Urgent & Important (Do First)
- #1550 - Backend test suite failures
- #1561-#1570 - HyperDX migration epic
- #1491 - Fix demo login tests
- #1255 - Frontend coverage restoration

### Important but Not Urgent (Schedule)
- #1490-#1494 - E2E testing improvements
- #1505-#1511 - Testing framework enhancements
- #996-#1004 - BGAI MVP features
- #1436 - Frontend state management refactoring

### Urgent but Not Important (Delegate/Defer)
- #576 - Security penetration testing
- #701-#706 - Infrastructure enhancements

### Neither Urgent nor Important (Deferred)
- #885-#895 - Admin console Phase 2
- #910-#914 - Admin console Phase 3
- #915-#920 - Admin console Phase 4

---

## 📝 Notes & Considerations

### Technical Debt
- **State Management**: SWR + Zustand duplication needs refactoring (#1436)
- **Test Quality**: Magic numbers, excessive regions, hardcoded timeouts
- **Coverage Gaps**: Frontend coverage dropped from 90% to 66%

### Blockers & Dependencies
- **#1550** blocks CI/CD pipeline - must fix first
- **HyperDX migration** depends on #1562 Docker deployment
- **BGAI quality testing** depends on annotation tasks (#996-#998)

### Resource Allocation
- **Backend Team**: Focus on HyperDX migration + test fixes
- **Frontend Team**: Focus on coverage restoration + BGAI components
- **QA Team**: Focus on E2E improvements + quality framework

### Risk Mitigation
- **Test Suite Stability**: Fix #1550 immediately to unblock CI/CD
- **HyperDX Migration**: Parallel running with Seq/Jaeger during testing
- **BGAI Quality**: Establish baseline before feature development

---

## 🔗 Related Documentation

- **Architecture**: [docs/01-architecture/overview/system-architecture.md](docs/01-architecture/overview/system-architecture.md)
- **Testing Guide**: [docs/02-development/testing/README.md](docs/02-development/testing/README.md)
- **BGAI Spec**: [docs/03-api/board-game-ai-api-specification.md](docs/03-api/board-game-ai-api-specification.md)
- **ADRs**: [docs/01-architecture/adr/](docs/01-architecture/adr/)
- **Complete Index**: [docs/INDEX.md](docs/INDEX.md)

---

**Last Generated**: 2025-11-21
**Next Review**: Weekly (Mondays)
**Owner**: Engineering Lead
**Status**: 🟢 Active Development
