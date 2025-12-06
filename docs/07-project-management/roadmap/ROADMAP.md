# MeepleAI Development Roadmap

**Last Updated**: 2025-12-06
**Status**: Post-DDD Migration (99% completion)
**Open Issues**: 70+ categorized by priority and type

---

## 🎯 Execution Strategy

Issues are organized by: **Priority** → **Type** → **Dependencies** → **Parallelization**

### Priority Legend
- **P1**: High priority (critical bugs, production issues)
- **P3**: Low priority (optimization, cleanup, documentation)
- **Deferred**: Future phases (EPICs, enhancements)

### Type Classification
- **Frontend**: UI/UX, React components, Next.js pages
- **Backend**: API, services, database, business logic
- **Infrastructure**: Docker, deployment, monitoring, security
- **E2E**: End-to-end testing, integration testing
- **Documentation**: Guides, ADRs, runbooks

### Parallelization Markers
- ⚡ **Parallel**: Can run concurrently with other tasks of same type
- 🔗 **Sequential**: Must complete before dependent tasks
- 🔄 **Both**: Affects multiple layers (frontend + backend)

---

## 📊 Current Status

| Priority | Count | Focus |
|----------|-------|-------|
| **P1** | 2 | K6 performance investigation + HyperDX EPIC |
| **P3** | 11 | Technical debt, optimization, documentation |
| **Deferred** | 57+ | Admin console EPICs, frontend modernization, infrastructure |

---

## 🚀 Phase 1: Critical Issues (P1)

### 1.1 Performance Investigation (Immediate)
| # | Issue | Type | Priority | Effort | Dependencies |
|---|-------|------|----------|--------|--------------|
| [#1954](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1954) | 🚨 K6 Performance Tests Failed | E2E | P1 | 1-2h | None |

**Action**: Investigate K6 test failure from 2025-12-06 nightly run
**Execution**: ⚡ Start immediately, no blockers

---

### 1.2 HyperDX Observability Platform (EPIC - High Priority)
| # | Issue | Type | Priority | Status | Notes |
|---|-------|------|----------|--------|-------|
| [#1561](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1561) | 🚀 EPIC: Implement HyperDX | Infrastructure | P1 (marked P3) | ✅ Sub-issues #1562-#1570 closed | Meta-issue tracking |

**Sub-Issues Status**: All 9 implementation issues (#1562-#1570) are **CLOSED** ✅
- Week 1 (Infrastructure/Backend): #1562, #1563, #1564, #1565 - DONE
- Week 2 (Frontend/Docs): #1566, #1567, #1568, #1569, #1570 - DONE

**Remaining Work**:
- Close parent EPIC #1561 after final verification
- Update documentation references if needed
- Archive implementation plan docs

---

## 🔧 Phase 2: Technical Debt & Optimization (P3)

### 2.1 Backend Optimization (Parallel Track)
| # | Issue | Type | Priority | Effort | Dependencies |
|---|-------|------|----------|--------|--------------|
| ⚡ [#1820](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1820) | Optimize PDF Test Performance | Backend/Testing | P3 | 2-3d | None |
| ⚡ [#1821](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1821) | PDF Background Processing Reliability | Backend | P3 | 1-2d | None |
| ⚡ [#1725](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1725) | LLM Token Tracking - Advanced Features | Backend/Observability | P3 | 5-7d | None |

**Execution**: All 3 can run in parallel
**Total Effort**: 5-7 days (parallel execution)

---

### 2.2 Code Quality & Cleanup (Sequential Recommended)
| # | Issue | Type | Priority | Effort | Dependencies |
|---|-------|------|----------|--------|--------------|
| 1 | [#1677](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1677) | Remove Obsolete Data Models | Backend | P3 | 1d | None |
| 2 | [#1676](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1676) | Remove Backward Compatibility Layers | Both | P3 | 1-2d | 🔗 #1677 |
| 3 | [#1679](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1679) | Cleanup Legacy Comments | Both | P3 | 1d | 🔗 #1676 |

**Execution**: 🔗 Sequential (dependencies ensure clean refactoring)
**Total Effort**: 3-4 days
**Rationale**: Each step builds on previous cleanup

---

### 2.3 Documentation & Architecture (Parallel after 2.2)
| # | Issue | Type | Priority | Effort | Dependencies |
|---|-------|------|----------|--------|--------------|
| ⚡ [#1680](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1680) | Audit Infrastructure Services | Documentation/Architecture | P3 | 2-3d | Wave 2.2 complete |
| ⚡ [#1681](https://github.com/DegrassiAaron/meepleai-monorepo/issues/1681) | Update Legacy Documentation | Documentation | P3 | 1d | Wave 2.1 + 2.2 complete |

**Execution**: ⚡ Both can run in parallel after code cleanup
**Total Effort**: 2-3 days (parallel)

---

## 📦 Phase 3: Deferred Features & EPICs (Future Sprints)

**Total**: 57+ issues across multiple EPICs
**Status**: Not scheduled for current sprint
**Decision Point**: Product owner prioritizes based on business needs

---

### 3.1 Frontend Modernization EPICs (6 EPICs, ~20 sub-issues)
| # | Epic | Priority | Status | Sub-Issues |
|---|------|----------|--------|------------|
| [#926](https://github.com/DegrassiAaron/meepleai-monorepo/issues/926) | Foundation & Quick Wins (Phase 1) | P3 | Deferred | TBD |
| [#931](https://github.com/DegrassiAaron/meepleai-monorepo/issues/931) | React 19 Optimization (Phase 2) | P3 | Deferred | TBD |
| [#933](https://github.com/DegrassiAaron/meepleai-monorepo/issues/933) | App Router Migration (Phase 3) | P3 | Deferred | TBD |
| [#932](https://github.com/DegrassiAaron/meepleai-monorepo/issues/932) | Advanced Features (Phase 4) | P3 | Deferred | TBD |
| [#934](https://github.com/DegrassiAaron/meepleai-monorepo/issues/934) | Design Polish (Phase 5) | P3 | Deferred | TBD |
| [#935](https://github.com/DegrassiAaron/meepleai-monorepo/issues/935) | Performance & Accessibility (Phase 6) | P3 | Deferred | TBD |

**Rationale**: Frontend modernization deferred until core platform stability

---

### 3.2 Admin Console Enhancement EPICs (3 EPICs, 37 sub-issues)
| # | Epic | Priority | Status | Sub-Issues |
|---|------|----------|--------|------------|
| [#874](https://github.com/DegrassiAaron/meepleai-monorepo/issues/874) | Dashboard Overview (Fase 1) | P3 | Deferred | [#875](https://github.com/DegrassiAaron/meepleai-monorepo/issues/875)-[#889](https://github.com/DegrassiAaron/meepleai-monorepo/issues/889) (15 issues) |
| [#890](https://github.com/DegrassiAaron/meepleai-monorepo/issues/890) | Infrastructure Monitoring (Fase 2) | P3 | Deferred | [#891](https://github.com/DegrassiAaron/meepleai-monorepo/issues/891)-[#902](https://github.com/DegrassiAaron/meepleai-monorepo/issues/902) (12 issues) |
| [#903](https://github.com/DegrassiAaron/meepleai-monorepo/issues/903) | Enhanced Management (Fase 3) | P3 | Deferred | [#904](https://github.com/DegrassiAaron/meepleai-monorepo/issues/904)-[#914](https://github.com/DegrassiAaron/meepleai-monorepo/issues/914) (11 issues) |
| [#915](https://github.com/DegrassiAaron/meepleai-monorepo/issues/915) | Advanced Features (Fase 4) | P3 | Deferred | [#916](https://github.com/DegrassiAaron/meepleai-monorepo/issues/916)-[#922](https://github.com/DegrassiAaron/meepleai-monorepo/issues/922) (7 issues) |

**Total Sub-Issues**: 45 (Dashboard, Monitoring, Management, Alerting, Reports)
**Rationale**: Admin console non-critical for MVP, deferred to Phase 3+

---

### 3.3 Infrastructure Enhancements (7 Issues)
| # | Issue | Priority | Status |
|---|-------|----------|--------|
| [#701](https://github.com/DegrassiAaron/meepleai-monorepo/issues/701) | Add resource limits to Docker services | P3 | Deferred |
| [#702](https://github.com/DegrassiAaron/meepleai-monorepo/issues/702) | Docker Compose profiles | P3 | Deferred |
| [#703](https://github.com/DegrassiAaron/meepleai-monorepo/issues/703) | Add Traefik reverse proxy | P3 | Deferred |
| [#704](https://github.com/DegrassiAaron/meepleai-monorepo/issues/704) | Backup automation scripts | P3 | Deferred |
| [#705](https://github.com/DegrassiAaron/meepleai-monorepo/issues/705) | Infrastructure monitoring (cAdvisor) | P3 | Deferred |
| [#706](https://github.com/DegrassiAaron/meepleai-monorepo/issues/706) | Operational runbooks | P3 | Deferred |
| [#707](https://github.com/DegrassiAaron/meepleai-monorepo/issues/707) | docker-compose.override.yml example | P3 | Deferred |

**Scope**: Resource optimization, service profiles, reverse proxy, monitoring
**Rationale**: Infrastructure improvements deferred until production scale requirements

---

### 3.4 Testing & Security (3 Issues)
| # | Issue | Priority | Status |
|---|-------|----------|--------|
| [#818](https://github.com/DegrassiAaron/meepleai-monorepo/issues/818) | Quarterly security scan review process | P3 | Deferred |
| [#844](https://github.com/DegrassiAaron/meepleai-monorepo/issues/844) | UI/UX Automated Testing Roadmap 2025 | P3 | Deferred |
| [#936](https://github.com/DegrassiAaron/meepleai-monorepo/issues/936) | POC Infisical Secret Rotation | P3 | Deferred |

**Scope**: Security processes, UI/UX testing strategy, secrets management
**Rationale**: Process improvements deferred to future phases

---

## 📈 Sprint Planning Recommendation

### Sprint Current: Critical Fixes + Technical Debt
**Duration**: 2 weeks
**Focus**: P1 issues + selected P3 cleanup

| Week | Focus | Issues |
|------|-------|--------|
| **Week 1** | P1 Critical + Backend Optimization | #1954 (K6), #1561 (HyperDX EPIC closure), #1820, #1821, #1725 (start) |
| **Week 2** | Code Cleanup + Documentation | #1677, #1676, #1679, #1680, #1681 |

**Parallel Tracks**:
- **Backend**: #1820, #1821, #1725 (can run concurrently)
- **Sequential**: #1677 → #1676 → #1679 (dependency chain)
- **Documentation**: #1680, #1681 (parallel after Week 1)

---

### Sprint Next: Feature Selection
**Duration**: 2-4 weeks
**Focus**: Product owner prioritizes from Phase 3 backlog
**Decision Point**: Choose between:
1. **Frontend Modernization** (React 19, App Router)
2. **Admin Console** (Dashboard, Monitoring)
3. **Infrastructure** (Traefik, Resource Limits, Profiles)

---

## 🎯 Success Metrics

### Phase 1 Completion Criteria (P1)
- [ ] K6 performance tests passing (#1954 resolved)
- [ ] HyperDX EPIC #1561 closed (all sub-issues verified)
- [ ] Zero P1 issues remaining

### Phase 2 Completion Criteria (P3 Selected Issues)
- [ ] PDF test suite execution <40s (#1820)
- [ ] PDF processing idempotent and reliable (#1821)
- [ ] LLM token tracking operational (#1725)
- [ ] Obsolete code removed (#1677, #1676, #1679)
- [ ] Documentation updated (#1680, #1681)
- [ ] CLAUDE.md reflects 100% DDD completion

### Overall Quality Gates
- [ ] 90%+ test coverage maintained
- [ ] CI pipeline <15min
- [ ] Zero build errors
- [ ] Zero security vulnerabilities (high/critical)
- [ ] All ADRs reviewed and current

---

## 🚫 Out of Scope (Current Roadmap)

The following are **NOT** in the current roadmap and require separate proposal:

1. **New Features**: Any feature not in existing open issues
2. **Breaking Changes**: API changes requiring client updates
3. **Infrastructure Rewrites**: Major architectural changes (e.g., Kubernetes)
4. **Third-Party Integrations**: New external service dependencies beyond current stack
5. **Performance Targets**: Beyond current benchmarks (unless critical like #1954)

**Process**: Propose via GitHub issue → Product owner review → Roadmap update

---

## 📞 Escalation Path

**Blockers**: Report in daily standup → Engineering lead decision within 24h
**Scope Changes**: Require product owner approval
**Priority Changes**: Only for P0 production incidents

---

## 📚 Related Documentation

- [CLAUDE.md](../../../CLAUDE.md) - Project overview and current status
- [docs/INDEX.md](../../INDEX.md) - Complete documentation index
- [Architecture ADRs](../../01-architecture/adr/) - Architecture decision records
- [Testing Guide](../../02-development/testing/test-writing-guide.md) - Testing standards
- [GitHub Projects](https://github.com/meepleai/meepleai-monorepo/projects) - Sprint boards

---

## 📝 Issue Tracking Guidelines

### GitHub Issue Format
All issues follow this structure:
- **Title**: `[Priority] Emoji Description`
- **Labels**: `priority: {high|low}`, `type`, `area`
- **Body**: Problem, proposed solution, acceptance criteria, dependencies

### Link Validation
All issue links use format: `https://github.com/DegrassiAaron/meepleai-monorepo/issues/{number}`

### Status Tracking
- **Open**: Active or planned
- **Closed**: Completed and verified
- **Deferred**: Future phases (labeled appropriately)

---

**Next Review**: After Sprint completion (estimated 2025-12-20)
**Roadmap Refresh**: Every 2 weeks or after major milestone completion
