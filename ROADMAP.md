# MeepleAI Development Roadmap

**Last Updated**: 2025-12-05
**Status**: Post-DDD Migration (99% → 100% completion target)

---

## 🎯 Execution Sequence

Issues are prioritized by: **Business Impact** → **Technical Dependencies** → **Parallelization Opportunities**

### Legend
- **Type**: `Frontend` | `Backend` | `Both` | `Infrastructure` | `E2E`
- **Priority**: `P0` (Critical) | `P1` (High) | `P2` (Medium) | `P3` (Low) | `Deferred`
- **Parallelization**: ⚡ Can run in parallel with same-type tasks

---

## 📊 Priority Matrix

| Priority | Count | Focus |
|----------|-------|-------|
| **P1** | 11 | Critical bugs + HyperDX migration |
| **P3** | 30+ | Optimization, cleanup, documentation |
| **Deferred** | 40+ | Future phases, non-critical enhancements |

---

## 🚀 Phase 1: Critical Fixes & High-Priority Features (P1)

### Wave 1A: Frontend Bugs (Immediate)
| # | Issue | Type | Dependencies | Effort |
|---|-------|------|--------------|--------|
| [#1940](https://github.com/meepleai/meepleai-monorepo/issues/1940) | Fix citation page preservation | Frontend | None | 1-2h |

**Execution**: Start immediately, no blockers

---

### Wave 1B: Infrastructure - HyperDX Migration (P1 EPIC)
**Parent**: [#1561](https://github.com/meepleai/meepleai-monorepo/issues/1561) 🚨 **EPIC: Implement HyperDX Observability Platform**

**Sequential Execution** (strict order due to dependencies):

| Step | Issue | Type | Dependencies | Effort |
|------|-------|------|--------------|--------|
| 1 | [#1562](https://github.com/meepleai/meepleai-monorepo/issues/1562) 🐳 Deploy HyperDX Docker | Infrastructure | None | 0.5d |
| 2 | [#1563](https://github.com/meepleai/meepleai-monorepo/issues/1563) ⚙️ Configure .NET OpenTelemetry | Backend | #1562 | 1d |
| 3 | [#1564](https://github.com/meepleai/meepleai-monorepo/issues/1564) 🗑️ Remove Seq/Jaeger Services | Infrastructure | #1563 | 0.5d |
| 4 | [#1565](https://github.com/meepleai/meepleai-monorepo/issues/1565) 🧪 Integration Testing | E2E | #1564 | 1d |
| 5 | [#1566](https://github.com/meepleai/meepleai-monorepo/issues/1566) ⚛️ Implement HyperDX Browser SDK | Frontend | #1565 | 1d |
| 6 | [#1567](https://github.com/meepleai/meepleai-monorepo/issues/1567) 🚨 Configure Application Alerts | Both | #1566 | 0.5d |
| 7 | [#1568](https://github.com/meepleai/meepleai-monorepo/issues/1568) 📊 Load Testing & Validation | E2E | #1567 | 1d |
| 8 | [#1569](https://github.com/meepleai/meepleai-monorepo/issues/1569) 📚 Update Documentation | Documentation | #1568 | 0.5d |
| 9 | [#1570](https://github.com/meepleai/meepleai-monorepo/issues/1570) ✅ Go-Live Checklist | E2E | #1569 | 0.5d |

**Total Effort**: 7 days
**Execution**: Sequential (each step blocks next)
**Business Value**: Production-ready observability platform

---

### Wave 1C: Backend Enhancement (Parallel with Frontend)
| # | Issue | Type | Dependencies | Effort |
|---|-------|------|--------------|--------|
| ⚡ [#1901](https://github.com/meepleai/meepleai-monorepo/issues/1901) | Advanced PDF Embedding Pipeline | Backend | None | 3-5d |

**Execution**: Can run in parallel with HyperDX steps 1-4 (infrastructure setup)

---

## 🔧 Phase 2: Technical Debt & Optimization (P3)

**Strategy**: Parallelize Frontend/Backend/Infrastructure cleanup

### Wave 2A: Backend Optimization (Parallel)
| # | Issue | Type | Dependencies | Effort |
|---|-------|------|--------------|--------|
| ⚡ [#1820](https://github.com/meepleai/meepleai-monorepo/issues/1820) | Optimize PDF Test Performance | Backend | None | 2-3d |
| ⚡ [#1821](https://github.com/meepleai/meepleai-monorepo/issues/1821) | PDF Background Processing Reliability | Backend | None | 1-2d |
| ⚡ [#1725](https://github.com/meepleai/meepleai-monorepo/issues/1725) | LLM Token Tracking - Advanced Features | Backend | #1694 | 5-7d |

**Execution**: All can run in parallel
**Total Effort**: 5-7 days (parallel execution)

---

### Wave 2B: Code Quality & Cleanup (Sequential recommended)
| # | Issue | Type | Dependencies | Effort |
|---|-------|------|--------------|--------|
| 1 | [#1677](https://github.com/meepleai/meepleai-monorepo/issues/1677) | Remove Obsolete Data Models | Backend | None | 1d |
| 2 | [#1676](https://github.com/meepleai/meepleai-monorepo/issues/1676) | Remove Backward Compatibility Layers | Both | #1677 | 1-2d |
| 3 | [#1679](https://github.com/meepleai/meepleai-monorepo/issues/1679) | Cleanup Legacy Comments | Both | #1676 | 1d |

**Total Effort**: 3-4 days (sequential)
**Rationale**: Dependencies ensure clean refactoring progression

---

### Wave 2C: Documentation & Architecture (Parallel after Wave 2B)
| # | Issue | Type | Dependencies | Effort |
|---|-------|------|--------------|--------|
| ⚡ [#1680](https://github.com/meepleai/meepleai-monorepo/issues/1680) | Audit Infrastructure Services | Documentation | Wave 2B | 2-3d |
| ⚡ [#1681](https://github.com/meepleai/meepleai-monorepo/issues/1681) | Update Legacy Documentation | Documentation | Wave 2A+2B | 1d |

**Execution**: Both can run in parallel after code cleanup
**Total Effort**: 2-3 days (parallel)

---

## 📦 Phase 3: Deferred Features (Future Sprints)

**Status**: Not scheduled for current sprint
**Total Issues**: 40+

### 3A: Frontend Modernization EPICs
| # | Epic | Type | Priority | Status |
|---|------|------|----------|--------|
| [#926](https://github.com/meepleai/meepleai-monorepo/issues/926) | Foundation & Quick Wins | Frontend | P3 | Deferred |
| [#931](https://github.com/meepleai/meepleai-monorepo/issues/931) | React 19 Optimization | Frontend | P3 | Deferred |
| [#933](https://github.com/meepleai/meepleai-monorepo/issues/933) | App Router Migration | Frontend | P3 | Deferred |
| [#932](https://github.com/meepleai/meepleai-monorepo/issues/932) | Advanced Features | Frontend | P3 | Deferred |
| [#934](https://github.com/meepleai/meepleai-monorepo/issues/934) | Design Polish | Frontend | P3 | Deferred |
| [#935](https://github.com/meepleai/meepleai-monorepo/issues/935) | Performance & Accessibility | Frontend | P3 | Deferred |

---

### 3B: Admin Console Enhancement EPICs
| # | Epic | Type | Priority | Status |
|---|------|------|----------|--------|
| [#874](https://github.com/meepleai/meepleai-monorepo/issues/874) | Dashboard Overview (Fase 1) | Both | P3 | Deferred |
| [#890](https://github.com/meepleai/meepleai-monorepo/issues/890) | Infrastructure Monitoring (Fase 2) | Both | P3 | Deferred |
| [#903](https://github.com/meepleai/meepleai-monorepo/issues/903) | Enhanced Management (Fase 3) | Both | P3 | Deferred |

**Sub-Issues**: #875-#904 (30 issues total)
**Rationale**: Admin console is non-critical for MVP

---

### 3C: Infrastructure Enhancements
| # | Issue | Type | Priority | Status |
|---|-------|------|----------|--------|
| [#701](https://github.com/meepleai/meepleai-monorepo/issues/701) | Add resource limits to Docker services | Infrastructure | P3 | Deferred |
| [#702](https://github.com/meepleai/meepleai-monorepo/issues/702) | Docker Compose profiles | Infrastructure | P3 | Deferred |
| [#703](https://github.com/meepleai/meepleai-monorepo/issues/703) | Add Traefik reverse proxy | Infrastructure | P3 | Deferred |
| [#704](https://github.com/meepleai/meepleai-monorepo/issues/704) | Backup automation scripts | Infrastructure | P3 | Deferred |
| [#705](https://github.com/meepleai/meepleai-monorepo/issues/705) | Infrastructure monitoring (cAdvisor) | Infrastructure | P3 | Deferred |
| [#706](https://github.com/meepleai/meepleai-monorepo/issues/706) | Operational runbooks | Documentation | P3 | Deferred |
| [#707](https://github.com/meepleai/meepleai-monorepo/issues/707) | docker-compose.override.yml example | Infrastructure | P3 | Deferred |

---

### 3D: Testing & Security
| # | Issue | Type | Priority | Status |
|---|-------|------|----------|--------|
| [#818](https://github.com/meepleai/meepleai-monorepo/issues/818) | Quarterly security scan review | Infrastructure | P3 | Deferred |
| [#844](https://github.com/meepleai/meepleai-monorepo/issues/844) | UI/UX Automated Testing Roadmap | E2E | P3 | Deferred |
| [#936](https://github.com/meepleai/meepleai-monorepo/issues/936) | POC Infisical Secret Rotation | Infrastructure | P3 | Deferred |

---

## 📈 Sprint Planning Recommendation

### Sprint 1 (Current): Critical Fixes + HyperDX
**Duration**: 2 weeks
**Focus**: P1 issues
**Team**: Full team on HyperDX sequential pipeline

| Week | Focus | Issues |
|------|-------|--------|
| Week 1 | HyperDX Setup + Frontend Bug | #1940, #1562-#1564 |
| Week 2 | HyperDX Integration + Testing | #1565-#1570 |

**Parallel Track**: Backend engineer can start #1901 (PDF embedding) during infrastructure setup

---

### Sprint 2: Optimization & Cleanup
**Duration**: 2 weeks
**Focus**: P3 technical debt
**Team**: Parallel frontend/backend tracks

| Week | Frontend | Backend |
|------|----------|---------|
| Week 1 | - | #1820, #1821, #1725 (start) |
| Week 2 | - | #1677, #1676, #1679 |

**Documentation Track**: #1680, #1681 after code cleanup

---

### Sprint 3+: Feature Development
**Duration**: TBD
**Focus**: Deferred EPICs based on business priorities
**Decision Point**: Product owner prioritizes from Phase 3 backlog

---

## 🎯 Success Metrics

### Phase 1 Completion Criteria
- [ ] #1940 resolved (citation bug fixed)
- [ ] HyperDX fully operational (all 9 sub-issues closed)
- [ ] Seq/Jaeger removed from infrastructure
- [ ] #1901 benchmark complete (70%+ Recall@10)
- [ ] Zero P1 issues remaining

### Phase 2 Completion Criteria
- [ ] Test suite execution <40s (#1820)
- [ ] PDF processing idempotent (#1821)
- [ ] LLM token tracking dashboard operational (#1725)
- [ ] All obsolete code removed (#1677, #1676, #1679)
- [ ] Documentation up-to-date (#1680, #1681)
- [ ] CLAUDE.md shows 100% DDD completion

### Overall Quality Gates
- [ ] 90%+ test coverage maintained
- [ ] CI pipeline <15min
- [ ] Zero build errors
- [ ] Zero security vulnerabilities (high/critical)
- [ ] All ADRs reviewed and current

---

## 🚫 Out of Scope (Current Roadmap)

The following are **NOT** in the current roadmap and should be proposed separately:

1. **New Features**: Any feature not listed in existing issues
2. **Breaking Changes**: API changes requiring client updates
3. **Infrastructure Rewrites**: Major architectural changes (e.g., Kubernetes migration)
4. **Third-Party Integrations**: New external service dependencies
5. **Performance Targets**: Beyond current benchmarks (unless critical)

**Process**: Propose via new GitHub issue → Product owner review → Roadmap update

---

## 📞 Escalation Path

**Blockers**: Report in daily standup → Engineering lead decision within 24h
**Scope Changes**: Requires product owner approval
**Priority Changes**: Only for P0 production incidents

---

## 📚 Related Documentation

- [CLAUDE.md](CLAUDE.md) - Project overview and current status
- [docs/INDEX.md](docs/INDEX.md) - Complete documentation index
- [docs/01-architecture/adr/](docs/01-architecture/adr/) - Architecture decision records
- [GitHub Projects](https://github.com/meepleai/meepleai-monorepo/projects) - Sprint boards

---

**Next Review**: After Sprint 1 completion (estimated 2025-12-19)
