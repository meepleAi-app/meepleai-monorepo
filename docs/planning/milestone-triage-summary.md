# Milestone Triage Summary - Complete Issue List
**Date**: 2025-11-13
**Total Issues Analyzed**: 34

## Quick Stats
- ✅ **Issues to Assign Milestone**: 15 (44%)
- ⏸️ **Issues Left Without Milestone**: 19 (56%)

---

## Complete Issue Categorization Table

| Issue | Title | Current Milestone | Recommended Milestone | Rationale | Action |
|-------|-------|-------------------|----------------------|-----------|--------|
| **1090** | Split ChatProvider into Multiple Contexts | `null` | **MVP Sprint 1** | Sprint-1 label, priority-critical, MVP scope | ✅ Assign |
| **1091** | Eliminate Inline Styles and Standardize | `null` | **MVP Sprint 1** | Sprint-1 label, priority-critical, MVP scope | ✅ Assign |
| **1092** | Mobile-First Responsive Improvements | `null` | **MVP Sprint 2** | Sprint-2 label, priority-high, MVP scope | ✅ Assign |
| **1093** | Optimize Re-renders and Bundle Size | `null` | **MVP Sprint 2** | Sprint-2 label, priority-high, MVP scope | ✅ Assign |
| **1094** | Accessibility Audit and Fixes | `null` | **MVP Sprint 2** | Sprint-2 label, priority-high, MVP scope | ✅ Assign |
| **1095** | Unified Error Handling System | `null` | **MVP Sprint 2** | Sprint-2 label, priority-high, MVP scope | ✅ Assign |
| **1096** | Standardize Loading States | `null` | **MVP Sprint 2** | Sprint-2 label, priority-high, MVP scope | ✅ Assign |
| **1097** | Set Up Storybook | `null` | **MVP Sprint 3** | Sprint-3 label, priority-medium, MVP scope | ✅ Assign |
| **1098** | Comprehensive Component Unit Tests | `null` | **MVP Sprint 3** | Sprint-3 label, priority-medium, MVP scope | ✅ Assign |
| **1099** | Landing Page Performance and UX | `null` | **MVP Sprint 3** | Sprint-3 label, priority-medium, MVP scope | ✅ Assign |
| **1100** | Keyboard Shortcuts System | `null` | **MVP Sprint 3** | Sprint-3 label, priority-medium, MVP scope | ✅ Assign |
| **1101** | Advanced Search and Filters | `null` | **MVP Sprint 3** | Sprint-3 label, priority-medium, MVP scope | ✅ Assign |
| **1102** | Theme Customization System | `null` | **MVP Sprint 3** | Sprint-3 label, priority-medium, MVP scope | ✅ Assign |
| **841** | Automated accessibility testing (axe-core) | `null` | **Month 2: LLM Integration** | Testing infrastructure for Month 2 | ✅ Assign |
| **842** | Automated performance testing (Lighthouse CI) | `null` | **Month 2: LLM Integration** | Testing infrastructure for Month 2 | ✅ Assign |
| **575** | Admin Override for 2FA Locked-Out Users | `null` | **No milestone** | Backend security feature, not time-bound | ⏸️ Leave |
| **576** | Security Penetration Testing | `null` | **No milestone** | Continuous security activity, not time-bound | ⏸️ Leave |
| **701** | Add resource limits to Docker services | `null` | **No milestone** | Infrastructure, can be done anytime | ⏸️ Leave |
| **702** | Docker Compose profiles | `null` | **No milestone** | Infrastructure, can be done anytime | ⏸️ Leave |
| **703** | Add Traefik reverse proxy | `null` | **No milestone** | Infrastructure, can be done anytime | ⏸️ Leave |
| **704** | Create backup automation scripts | `null` | **No milestone** | Infrastructure, can be done anytime | ⏸️ Leave |
| **705** | Add infrastructure monitoring | `null` | **No milestone** | Infrastructure, can be done anytime | ⏸️ Leave |
| **706** | Create operational runbooks | `null` | **No milestone** | Documentation, can be done anytime | ⏸️ Leave |
| **707** | docker-compose.override.yml example | `null` | **No milestone** | Documentation, can be done anytime | ⏸️ Leave |
| **709** | Create operational runbooks (duplicate?) | `null` | **No milestone** | Documentation, can be done anytime | ⏸️ Leave |
| **818** | Quarterly security scan review process | `null` | **No milestone** | Policy/process, deferred, priority-low | ⏸️ Leave |
| **844** | Epic: UI/UX Automated Testing Roadmap | `null` | **No milestone** | Epic tracker, deferred, priority-low | ⏸️ Leave |
| **926** | Epic: Foundation & Quick Wins (Phase 1) | `null` | **No milestone** | Epic tracker, deferred, priority-low | ⏸️ Leave |
| **931** | Epic: React 19 Optimization (Phase 2) | `null` | **No milestone** | Epic tracker, deferred, priority-low | ⏸️ Leave |
| **932** | Epic: Advanced Features (Phase 4) | `null` | **No milestone** | Epic tracker, deferred, priority-low | ⏸️ Leave |
| **933** | Epic: App Router Migration (Phase 3) | `null` | **No milestone** | Epic tracker, deferred, priority-low | ⏸️ Leave |
| **934** | Epic: Design Polish (Phase 5) | `null` | **No milestone** | Epic tracker, deferred, priority-low | ⏸️ Leave |
| **935** | Epic: Performance & Accessibility (Phase 6) | `null` | **No milestone** | Epic tracker, deferred, priority-low | ⏸️ Leave |
| **936** | Spike: POC Infisical Secret Rotation | `null` | **No milestone** | Research spike, not time-bound | ⏸️ Leave |

---

## Milestone Distribution After Assignment

| Milestone | Issues to Add | New Total |
|-----------|---------------|-----------|
| MVP Sprint 1 | +2 | Check current count |
| MVP Sprint 2 | +5 | Check current count |
| MVP Sprint 3 | +6 | Check current count |
| Month 2: LLM Integration | +2 | Check current count |
| No milestone | 19 | (infrastructure, epics, security) |

---

## Labels Analysis

### Issues by Sprint Label
- **sprint-1**: 2 issues (both assigned to MVP Sprint 1)
- **sprint-2**: 5 issues (all assigned to MVP Sprint 2)
- **sprint-3**: 6 issues (all assigned to MVP Sprint 3)

### Issues by Priority Label
- **priority-critical**: 2 issues → MVP Sprint 1
- **priority-high**: 5 issues → MVP Sprint 2
- **priority-medium**: 6 issues → MVP Sprint 3
- **priority-low**: 21 issues → No milestone (infrastructure/deferred)

### Issues by Epic Label
- **frontend**: 20 issues (13 sprint + 7 deferred epics)
- **testing**: 3 issues (2 to Month 2, 1 deferred epic)
- **main**: 13 issues (all infrastructure/operational)
- **backend**: 1 issue (security feature)

---

## Execution Steps

### 1. Review and Confirm
```bash
# Review the analysis
cat docs/planning/milestone-triage-analysis.md
cat docs/planning/milestone-triage-summary.md
```

### 2. Execute Bulk Assignment
```bash
# Make script executable
chmod +x docs/planning/milestone-assignment-script.sh

# Run bulk assignment
./docs/planning/milestone-assignment-script.sh
```

### 3. Verify Results
```bash
# Check issues were assigned correctly
gh issue list --milestone "MVP Sprint 1" --repo DegrassiAaron/meepleai-monorepo
gh issue list --milestone "MVP Sprint 2" --repo DegrassiAaron/meepleai-monorepo
gh issue list --milestone "MVP Sprint 3" --repo DegrassiAaron/meepleai-monorepo
gh issue list --milestone "Month 2: LLM Integration" --repo DegrassiAaron/meepleai-monorepo

# Check remaining issues without milestone
gh issue list --search "no:milestone is:open" --repo DegrassiAaron/meepleai-monorepo
```

### 4. Update Project Board (Optional)
- Move assigned issues to appropriate columns
- Update sprint planning views
- Communicate changes to team

---

## Notes

### Why Leave 19 Issues Without Milestone?

1. **Infrastructure (10 issues)**:
   - Docker, monitoring, backup, Traefik configurations
   - Can be implemented anytime without blocking features
   - Better managed as operational tasks, not tied to releases

2. **Deferred Epics (7 issues)**:
   - Meta-issues tracking future phases
   - Explicitly marked as "deferred" and "priority-low"
   - Will be scheduled when capacity allows

3. **Security (2 issues)**:
   - Continuous activities not tied to specific releases
   - Should be done based on security assessment schedule
   - Not feature-dependent

### Potential Duplicate Detection
- **#706 and #709**: Both titled "Create operational runbooks documentation"
  - Consider closing one as duplicate after verification

---

## Related Documentation
- Unified Roadmap: `docs/planning/unified-roadmap-2025.md`
- Frontend Refactor Guide: Check repository for sprint planning docs
- Month 1-6 BGAI Roadmap: Check BGAI documentation

