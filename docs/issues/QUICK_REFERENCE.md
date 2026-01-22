# ⚡ Quick Reference - Implementation Sequence

**Last Updated**: 2026-01-22

---

## 🎯 What to Work On Next (main-dev)

### THIS WEEK (Week 0) ✅
- ✅ **19 issues created** (#2911-2931)
- [ ] Assign to Sprint 1 milestone
- [ ] Team planning (2h)

### NEXT WEEK (Week 1) 🔴 CRITICAL START
**Start Monday with**:
1. **#2919** - Playwright Config (DevOps, 6-8h) ⭐
2. **#2920** - Testcontainers (DevOps, 6-8h) ⭐
3. **#2911** - Admin Backend Unit Tests (Backend, 6-8h) ⭐
4. **#2760** - MSW Infrastructure (Backend, 4-6h)

**Why these first**: Block ALL testing across ALL epics

---

## 📋 Issue Cheat Sheet

### By Week

**Week 1** (Infrastructure + Admin Backend):
- `#2919, #2920, #2760, #2911, #2912, #2923`

**Week 2** (Admin Frontend Testing + CI/CD):
- `#2913, #2914, #2915, #2916, #2917, #2918, #2921, #2922`

**Week 3** (User Dashboard Backend):
- `#2854, #2855, #2856 + Epic #2718 review`

**Week 4** (User Dashboard Testing + Library Backend):
- `#2861, #2862, #2863, #2864, #2865`

**Week 5** (Library Testing + Catalog Backend):
- `#2870, #2871, #2872` ⚠️ Coordinate with Epic #2718

**Week 6** (Catalog Testing):
- `#2877 + Epic #2718 merge coordination`

**Week 7+** (Profile, User Mgmt, Editor, Quality):
- `#2878-2883, #2884-2891, #2892-2897, #2924-2931, #2927-2929`

---

## 🔀 Branch Status

### main-dev (Backend/Infra Work)
**Current**: Ready for Week 1 infrastructure work
**Next**: Start #2919, #2920, #2911 on Monday

### frontend-dev (Epic #2718 UI)
**Current**: Milestone 6 - Frontend (#2743-2750) 🔄
**Progress**: 22/34 issues (65% Epic complete)
**Next**: Continue frontend work, coordinate backend needs in Week 3

---

## ⚠️ Coordination Points

### Week 3 - Epic #2718 Backend Support
**Action**: Backend Dev 2 reviews Epic #2718 APIs, fixes gaps
**Sync**: Daily with frontend-dev on API contracts

### Week 6 - SharedGameCatalog BC
**Action**: Merge Epic #2718 before starting Epic 4 backend
**Sync**: Schema review, migration coordination
**Risk**: HIGH - both epics touch same BC

---

## 🎯 Priority Levels

### 🔴 CRITICAL (Week 1-2) - 14 issues
BLOCKING all testing: `#2760, #2911-2923`

### 🟡 HIGH (Week 3-4) - 11 issues
Enable Epic 2: `#2854-2856, #2861-2862, #2863-2865`

### 🟢 MEDIUM (Week 5-6) - 5 issues
Epic 3-4 backend: `#2870-2872, #2877`

### 🔵 LOW (Week 7+) - 29 issues
Profile, User Mgmt, Editor, Quality: `#2878-2897, #2924-2931, #2927-2929`

---

## 📊 Effort Summary

| Phase | Issues | Hours | Team | Duration |
|-------|--------|-------|------|----------|
| Week 1-2 | 14 | 88-113 | 3 people | 2 weeks |
| Week 3-4 | 11 | 52-75 | 2-3 people | 2 weeks |
| Week 5-6 | 5 | 38-50 | 2 people | 2 weeks |
| Week 7-9 | 29 | 130-160 | 2-3 people | 3 weeks |
| **TOTAL** | **59** | **308-398** | **2-3 avg** | **9 weeks** |

---

## 🔗 Quick Links

**Planning Docs**:
- [IMPLEMENTATION_ROADMAP.md](./IMPLEMENTATION_ROADMAP.md) - Full roadmap with all phases
- [GAP_ANALYSIS_REPORT.md](./GAP_ANALYSIS_REPORT.md) - Detailed gap findings
- [MISSING_ISSUES_TO_CREATE.md](./MISSING_ISSUES_TO_CREATE.md) - 19 issue specs (used to create #2911-2931)
- [IMPLEMENTATION_SEQUENCE.md](./IMPLEMENTATION_SEQUENCE.md) - Detailed sequence with dependencies
- [WEEK_BY_WEEK_PLAN.md](./WEEK_BY_WEEK_PLAN.md) - Week-by-week execution plan

**GitHub**:
- [Epic #2718 - Game Sharing](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2718) - In progress on frontend-dev
- [Epic #2823 - Game Detail](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2823) - Issues #2824-2843
- [Open Issues](https://github.com/DegrassiAaron/meepleai-monorepo/issues) - All 105 open issues

---

## 💡 Key Decisions

### Decision 1: Infrastructure First ✅
**Chosen**: Option A - Infrastructure before features
**Rationale**: Establishes testing patterns, enables quality, unblocks all future work
**Trade-off**: 2 weeks before feature delivery, but better long-term

### Decision 2: Epic #2718 Support in Week 3 ✅
**Chosen**: Allocate Backend Dev 2 to support Epic #2718
**Rationale**: Enable frontend-dev to complete without blockers
**Trade-off**: Slower on Epic 2-3 backend, but better coordination

### Decision 3: Shared Catalog Coordination ✅
**Chosen**: Merge Epic #2718 before Epic 4 backend
**Rationale**: Avoid BC conflicts, single source of truth for SharedGameCatalog
**Trade-off**: Epic 4 delayed until Epic #2718 merged, but cleaner architecture

---

## 🚀 Next Actions (In Order)

1. [ ] Review this Quick Reference with team
2. [ ] Assign Sprint 1 issues to developers:
   - DevOps: #2919, #2920, #2921, #2923, #2922
   - Backend: #2760, #2911, #2912
   - QA: #2913-2918
3. [ ] Create Sprint 1 milestone on GitHub
4. [ ] Schedule daily standups (9:00 AM, 15 min)
5. [ ] Setup Slack channel: #sprint-1-infrastructure
6. [ ] Monday morning: Kick off Week 1 with #2919, #2920, #2911

---

**Priority**: Start Week 1 Monday with Infrastructure (#2919, #2920)
**First Deliverable**: End Week 2 - Test infrastructure + Admin testing complete
**First Feature Delivery**: End Week 4 - User Dashboard backend complete
