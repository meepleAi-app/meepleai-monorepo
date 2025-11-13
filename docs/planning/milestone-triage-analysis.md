# Milestone Triage Analysis
**Date**: 2025-11-13
**Total Issues Without Milestone**: 34

## Summary

| Category | Count | Recommended Milestone |
|----------|-------|----------------------|
| Frontend Refactor (Sprint 1-3) | 13 | MVP Sprint 1, MVP Sprint 2, MVP Sprint 3 |
| Infrastructure/Operational | 13 | No milestone needed |
| Deferred Epics | 7 | No milestone needed |
| Security/Testing | 2 | Month 2: LLM Integration |

---

## Group A: Frontend Refactor Issues → Assign to MVP Sprint 1-3
**Count**: 13 issues
**Rationale**: These have explicit sprint-1, sprint-2, sprint-3 labels and are marked as MVP

| Issue | Title | Labels | Recommended Milestone | Priority |
|-------|-------|--------|----------------------|----------|
| 1090 | Split ChatProvider into Multiple Contexts | sprint-1, mvp, priority-critical | **MVP Sprint 1** | Critical |
| 1091 | Eliminate Inline Styles and Standardize with Design System | sprint-1, mvp, priority-critical | **MVP Sprint 1** | Critical |
| 1092 | Mobile-First Responsive Improvements | sprint-2, mvp, priority-high | **MVP Sprint 2** | High |
| 1093 | Optimize Re-renders and Bundle Size | sprint-2, mvp, priority-high | **MVP Sprint 2** | High |
| 1094 | Accessibility Audit and Fixes | sprint-2, mvp, priority-high | **MVP Sprint 2** | High |
| 1095 | Unified Error Handling System | sprint-2, mvp, priority-high | **MVP Sprint 2** | High |
| 1096 | Standardize Loading States | sprint-2, mvp, priority-high | **MVP Sprint 2** | High |
| 1097 | Set Up Storybook for Component Documentation | sprint-3, mvp, priority-medium | **MVP Sprint 3** | Medium |
| 1098 | Comprehensive Component Unit Tests | sprint-3, mvp, priority-medium | **MVP Sprint 3** | Medium |
| 1099 | Landing Page Performance and UX | sprint-3, mvp, priority-medium | **MVP Sprint 3** | Medium |
| 1100 | Keyboard Shortcuts System | sprint-3, mvp, priority-medium | **MVP Sprint 3** | Medium |
| 1101 | Advanced Search and Filters | sprint-3, mvp, priority-medium | **MVP Sprint 3** | Medium |
| 1102 | Theme Customization System | sprint-3, mvp, priority-medium | **MVP Sprint 3** | Medium |

---

## Group B: Infrastructure/Operational Issues → No Milestone Needed
**Count**: 13 issues
**Rationale**: Infrastructure, Docker, operational tasks can be done anytime, not tied to feature milestones

| Issue | Title | Labels | Recommendation |
|-------|-------|--------|----------------|
| 701 | Add resource limits to all Docker services | main, priority-low | No milestone (infrastructure) |
| 702 | Implement Docker Compose profiles for selective service startup | main, priority-low | No milestone (infrastructure) |
| 703 | Add Traefik reverse proxy layer | main, priority-low | No milestone (infrastructure) |
| 704 | Create backup automation scripts | main, priority-low | No milestone (infrastructure) |
| 705 | Add infrastructure monitoring (cAdvisor + node-exporter) | main, priority-low | No milestone (infrastructure) |
| 706 | Create operational runbooks documentation | main, priority-low | No milestone (documentation) |
| 707 | Add docker-compose.override.yml example | main, priority-low | No milestone (documentation) |
| 709 | Create operational runbooks documentation | main, priority-low | No milestone (documentation - duplicate?) |
| 818 | Establish quarterly security scan review process | main, priority-low, deferred | No milestone (policy) |
| 936 | Spike: POC Infisical Secret Rotation (Phase 2) | main | No milestone (research spike) |

---

## Group C: Deferred Epics → No Milestone Needed
**Count**: 7 issues
**Rationale**: All marked as "deferred" and "priority-low", these are tracking epics for future phases

| Issue | Title | Labels | Recommendation |
|-------|-------|--------|----------------|
| 926 | Epic: Foundation & Quick Wins (Phase 1) | frontend, epic, deferred, priority-low | No milestone (epic tracker) |
| 931 | Epic: React 19 Optimization (Phase 2) | frontend, epic, deferred, priority-low | No milestone (epic tracker) |
| 932 | Epic: Advanced Features (Phase 4) | frontend, epic, deferred, priority-low | No milestone (epic tracker) |
| 933 | Epic: App Router Migration (Phase 3) | frontend, epic, deferred, priority-low | No milestone (epic tracker) |
| 934 | Epic: Design Polish (Phase 5) | frontend, epic, deferred, priority-low | No milestone (epic tracker) |
| 935 | Epic: Performance & Accessibility (Phase 6) | frontend, epic, deferred, priority-low | No milestone (epic tracker) |
| 844 | Epic: UI/UX Automated Testing Roadmap 2025 | testing, deferred, main, priority-low | No milestone (epic tracker) |

---

## Group D: Security/Testing Features → Month 2: LLM Integration
**Count**: 2 issues
**Rationale**: Security and testing infrastructure that should be done during Month 2 backend work

| Issue | Title | Labels | Recommended Milestone | Rationale |
|-------|-------|--------|----------------------|-----------|
| 841 | Implement automated accessibility testing with axe-core | testing, deferred, main, priority-low | **Month 2: LLM Integration** | Testing infrastructure |
| 842 | Implement automated performance testing with Lighthouse CI | testing, performance, deferred, main, priority-low | **Month 2: LLM Integration** | Testing infrastructure |

---

## Group E: Backend Security Issues → Leave Without Milestone
**Count**: 2 issues
**Rationale**: Security features that are not tied to specific milestones

| Issue | Title | Labels | Recommendation |
|-------|-------|--------|----------------|
| 575 | Admin Override for 2FA Locked-Out Users | backend | No milestone (security feature, can be done anytime) |
| 576 | Security Penetration Testing | main | No milestone (continuous security activity) |

---

## Execution Plan

### Issues to Assign (15 total)
- **MVP Sprint 1**: 2 issues (#1090, #1091)
- **MVP Sprint 2**: 5 issues (#1092-#1096)
- **MVP Sprint 3**: 6 issues (#1097-#1102)
- **Month 2**: 2 issues (#841, #842)

### Issues to Leave Without Milestone (19 total)
- Infrastructure: 10 issues (#701-709, #936)
- Deferred Epics: 7 issues (#844, #926, #931-#935)
- Security: 2 issues (#575, #576)

---

## Milestone Assignment Script

See `milestone-assignment-script.sh` for the bulk assignment commands.

