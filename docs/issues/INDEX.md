# Legacy Code Cleanup Issues - MeepleAI Monorepo

**Generated:** 2025-11-23
**Source:** Legacy code analysis (60+ findings across codebase)
**Total Issues:** 10 issues (3 Critical, 4 Medium, 3 Low)

---

## Overview

This directory contains issues identified during a comprehensive legacy code audit. These issues track technical debt, deprecated code, backward compatibility layers, and cleanup tasks remaining after the 99% complete DDD migration.

### Completion Roadmap

```
IMMEDIATE (1-2 sprints) → SHORT-TERM (1-2 months) → MEDIUM-TERM (3-6 months) → LONG-TERM (Post-Beta)
     ↓                            ↓                          ↓                         ↓
  Issues #001-003             Issues #010-013            Issues #020,022           Issue #021,023
  (3 Critical)                (4 Medium)                 (2 Low)                   (1 Low)
```

---

## 🔴 CRITICAL Priority (IMMEDIATE - 1-2 Sprints)

These issues have the highest impact and should be resolved first.

| ID | Issue | Effort | Impact | Files |
|----|-------|--------|--------|-------|
| **#001** | [Consolidate Duplicate React Components](001-consolidate-duplicate-components.md) | 2-3 days | HIGH | 12 components in 2 locations |
| **#002** | [Remove Deprecated Profile Page](002-remove-deprecated-profile-page.md) | 0.5 days | MEDIUM | `/profile` → `/settings` redirect |
| **#003** | [Update Component Imports](003-update-component-imports.md) | 1-2 days | HIGH | 50-100+ import statements |

**Total Effort:** 4-5.5 days
**Key Deliverables:**
- Zero duplicate components
- Consistent import paths
- Profile page removed
- All tests passing

**Dependencies:** #003 depends on #001

---

## 🟡 MEDIUM Priority (SHORT-TERM - 1-2 Months)

These issues address technical debt and incomplete features.

| ID | Issue | Effort | Impact | Category |
|----|-------|--------|--------|----------|
| **#010** | [Resolve Backend TODO Comments](010-resolve-backend-todos.md) | 3-5 days | HIGH | 35+ TODOs across backend |
| **#011** | [Implement Missing Frontend APIs](011-implement-missing-frontend-apis.md) | 5-7 days | HIGH | Settings, Games, Search APIs |
| **#012** | [Remove Backward Compatibility Layers](012-remove-backward-compatibility-layers.md) | 3-4 days | MEDIUM | 8 DTO conversions + Zustand |
| **#013** | [Remove Obsolete Data Models](013-remove-obsolete-models.md) | 1-2 days | LOW | RuleSpecV0, legacy models |

**Total Effort:** 12-18 days
**Key Deliverables:**
- All critical TODOs implemented
- Settings preferences functional
- Clean DTO usage (no conversions)
- Legacy models removed

**Dependencies:** #012 requires frontend migration first

---

## 🟢 LOW Priority (MEDIUM-LONG TERM - 3+ Months)

These issues improve code quality and maintainability but don't block development.

| ID | Issue | Effort | Impact | Category |
|----|-------|--------|--------|----------|
| **#020** | [Fix Test Infrastructure Issues](020-fix-test-infrastructure-issues.md) | 2-3 days | MEDIUM | 4 skipped tests |
| **#021** | [Cleanup Legacy Comments](021-cleanup-legacy-comments-and-deprecations.md) | 1-2 days | LOW | Deprecation markers |
| **#022** | [Audit Infrastructure Services](022-audit-infrastructure-services.md) | 2-3 days | LOW | 39 service files |
| **#023** | [Update Legacy Documentation](023-update-legacy-documentation.md) | 1 day | MEDIUM | All docs/ files |

**Total Effort:** 6-9 days
**Key Deliverables:**
- All tests enabled and passing
- Clean codebase (no stale comments)
- Services documented and categorized
- Documentation accurate and up-to-date

**Dependencies:** #023 should be done LAST (after all code changes)

---

## Summary Statistics

### By Priority
- **Critical:** 3 issues, 4-5.5 days
- **Medium:** 4 issues, 12-18 days
- **Low:** 3 issues, 6-9 days
- **TOTAL:** 10 issues, 22-32.5 days (~4-6 weeks)

### By Category
- **Frontend:** 3 issues (#001, #002, #003)
- **Backend:** 3 issues (#010, #012, #013)
- **Full Stack:** 1 issue (#011)
- **Testing:** 1 issue (#020)
- **Documentation:** 2 issues (#021, #023)
- **Architecture:** 1 issue (#022)

### By Type
- **Code Cleanup:** 5 issues
- **Feature Completion:** 2 issues
- **Documentation:** 2 issues
- **Testing:** 1 issue

---

## Recommended Execution Order

### Sprint 1-2 (IMMEDIATE)
```
Week 1:
  Day 1-3: Issue #001 (Consolidate Duplicate Components)
  Day 4-5: Issue #003 (Update Component Imports)

Week 2:
  Day 1: Issue #002 (Remove Profile Page)
  Day 2-3: Start Issue #010 (Backend TODOs - Critical only)
  Day 4-5: Continue Issue #010
```

### Sprint 3-4 (SHORT-TERM)
```
Week 3-4:
  - Complete Issue #010 (Backend TODOs)
  - Issue #011 (Missing APIs - Settings priority first)

Week 5-6:
  - Complete Issue #011
  - Issue #013 (Remove Obsolete Models - quick win)
```

### Sprint 5-6 (MEDIUM-TERM)
```
Week 7-8:
  - Issue #012 (Backward Compatibility - requires frontend ready)
  - Issue #020 (Fix Test Issues)

Week 9-10:
  - Issue #022 (Audit Services)
  - Issue #021 (Cleanup Comments)
```

### Post-Beta (LONG-TERM)
```
After all code changes:
  - Issue #023 (Update Documentation)
```

---

## Issue Templates

Each issue follows this structure:

```markdown
# Issue #XXX: Title

**Priority:** 🔴/🟡/🟢
**Category:** Area of codebase
**Estimated Effort:** Time estimate
**Sprint:** Timeframe

## Summary
Brief description

## Impact
Why it matters

## Tasks
- [ ] Checklist of work items

## Success Criteria
- [ ] Definition of done

## Related Issues
Links to dependencies

## References
File paths and code locations
```

---

## Progress Tracking

### Completion Status

| Issue | Status | Assignee | Start Date | End Date | Notes |
|-------|--------|----------|------------|----------|-------|
| #001  | 📋 Open | TBD | TBD | TBD | - |
| #002  | 📋 Open | TBD | TBD | TBD | - |
| #003  | 📋 Open | TBD | TBD | TBD | Blocked by #001 |
| #010  | 📋 Open | TBD | TBD | TBD | - |
| #011  | 📋 Open | TBD | TBD | TBD | - |
| #012  | 📋 Open | TBD | TBD | TBD | Needs frontend migration |
| #013  | 📋 Open | TBD | TBD | TBD | - |
| #020  | 📋 Open | TBD | TBD | TBD | - |
| #021  | 📋 Open | TBD | TBD | TBD | - |
| #022  | 📋 Open | TBD | TBD | TBD | - |
| #023  | 📋 Open | TBD | TBD | TBD | Do last |

**Status Legend:**
- 📋 Open - Not started
- 🏗️ In Progress - Currently being worked on
- ✅ Completed - Done and verified
- 🚫 Blocked - Waiting on dependency

---

## How to Use These Issues

### For Developers

1. **Pick an issue** based on current sprint priorities
2. **Read the full issue** in its dedicated markdown file
3. **Follow the task checklist** step by step
4. **Update progress** in this INDEX.md
5. **Mark completed** when success criteria met

### For Project Managers

1. **Assign issues** to sprints based on priority
2. **Track progress** using the completion status table
3. **Manage dependencies** (e.g., #003 depends on #001)
4. **Adjust timeline** if issues take longer than estimated

### For Code Reviewers

1. **Reference issue number** in PR descriptions
2. **Verify success criteria** met before approving
3. **Check related issues** for context
4. **Update issue status** when PR merged

---

## Related Documentation

- **Legacy Code Analysis Report:** Source of all these issues (60+ findings)
- **CLAUDE.md:** Project overview (DDD 99% → 100% after completion)
- **docs/INDEX.md:** Full documentation index
- **ADRs:** Architecture decision records (context for some issues)

---

## Quick Links

### Critical Issues
- [#001 Consolidate Duplicate Components](001-consolidate-duplicate-components.md) 🔴
- [#002 Remove Deprecated Profile Page](002-remove-deprecated-profile-page.md) 🔴
- [#003 Update Component Imports](003-update-component-imports.md) 🔴

### Medium Priority
- [#010 Resolve Backend TODOs](010-resolve-backend-todos.md) 🟡
- [#011 Implement Missing Frontend APIs](011-implement-missing-frontend-apis.md) 🟡
- [#012 Remove Backward Compatibility Layers](012-remove-backward-compatibility-layers.md) 🟡
- [#013 Remove Obsolete Models](013-remove-obsolete-models.md) 🟡

### Low Priority
- [#020 Fix Test Infrastructure Issues](020-fix-test-infrastructure-issues.md) 🟢
- [#021 Cleanup Legacy Comments](021-cleanup-legacy-comments-and-deprecations.md) 🟢
- [#022 Audit Infrastructure Services](022-audit-infrastructure-services.md) 🟢
- [#023 Update Legacy Documentation](023-update-legacy-documentation.md) 🟢

---

## Contact

**Questions about these issues?**
- Review the full legacy code analysis report
- Check related ADRs for architectural context
- Consult CLAUDE.md for project overview

**Found additional legacy code?**
- Create a new issue following the template format
- Add to this index
- Categorize by priority

---

**Last Updated:** 2025-11-23
**Next Review:** After completing critical issues (#001-003)
**Maintainer:** Engineering Lead
