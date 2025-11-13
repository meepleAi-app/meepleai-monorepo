# Implementation Sequence - Detailed Gantt with FE/BE Parallelization

**Generated**: 2025-11-12 21:10
**Total Open Issues**: 155
**Strategy**: Maximize parallel frontend/backend execution

---

## 🎯 Visual Implementation Sequence

### Gantt Chart with Swim Lanes (Frontend/Backend Parallel Execution)

```mermaid
gantt
    title Implementation Sequence - FE/BE Parallel Tracks
    dateFormat YYYY-MM-DD

    section Sprint 1 (7 issues)
    FE #848 Settings Pages :active, fe1_1, 2025-11-12, 2d
    BE #849 User Profile Service :active, be1_1, 2025-11-12, 2d
    Test #850 Settings Tests :done, test1_1, 2025-11-14, 2d
    Test #851 Profile Tests :done, test1_2, 2025-11-16, 2d
    SYNC Sprint 1 :milestone, sync1, 2025-11-25, 1d

    section Sprint 2 (5 issues)
    FE #854 Game Search UI :active, fe2_1, 2025-11-26, 2d
    FE #855 Game Detail Page :active, fe2_2, 2025-11-28, 2d
    BE #852 GameService CRUD :active, be2_1, 2025-11-26, 2d
    BE #853 PDF Upload Pipeline :active, be2_2, 2025-11-28, 2d
    SYNC Sprint 2 :milestone, sync2, 2025-12-09, 1d

    section Sprint 3 (6 issues)
    FE #858 Chat UI Enhancement :active, fe3_1, 2025-12-10, 2d
    FE #859 PDF Citation Display :active, fe3_2, 2025-12-12, 2d
    BE #856 Chat Thread Management :active, be3_1, 2025-12-10, 2d
    BE #857 Game-Specific Context :active, be3_2, 2025-12-12, 2d
    BE #860 Chat Export :active, be3_3, 2025-12-14, 2d
    Test #861 Chat Integration Tests :done, test3_1, 2025-12-16, 2d
    SYNC Sprint 3 :milestone, sync3, 2025-12-23, 1d

    section Sprint 4 (5 issues)
    FE #863 Session Setup Modal :active, fe4_1, 2025-12-24, 2d
    FE #864 Active Session UI :active, fe4_2, 2025-12-26, 2d
    FE #865 Session History Stats :active, fe4_3, 2025-12-28, 2d
    BE #862 GameSessionService :active, be4_1, 2025-12-24, 3d
    SYNC Sprint 4 :milestone, sync4, 2026-01-06, 1d

    section Sprint 5 (5 issues)
    FE #868 Agent Selection UI :active, fe5_1, 2026-01-07, 2d
    BE #866 AI Agents Entity :active, be5_1, 2026-01-07, 2d
    BE #867 Turn-Based Framework :active, be5_2, 2026-01-09, 2d
    BE #869 Move Validation :active, be5_3, 2026-01-11, 2d
    Test #870 Agent System Tests :done, test5_1, 2026-01-13, 2d
    SYNC Sprint 5 :milestone, sync5, 2026-01-20, 1d

    section FASE 1 Dashboard (16 issues)
    FE #883 Dashboard Overview Page :active, f1_fe1, 2026-01-21, 3d
    FE #889 Metrics Visualization :active, f1_fe2, 2026-01-24, 2d
    FE #890 Alert Management UI :active, f1_fe3, 2026-01-26, 2d
    FE #893 System Health Dashboard :active, f1_fe4, 2026-01-28, 2d
    FE #894 User Activity Overview :active, f1_fe5, 2026-01-30, 2d
    FE #895 Quick Stats Cards :active, f1_fe6, 2026-02-01, 1d
    BE #884 Dashboard API :active, f1_be1, 2026-01-21, 3d
    BE #885 Metrics Service :active, f1_be2, 2026-01-24, 2d
    BE #886 Alert Service :active, f1_be3, 2026-01-26, 2d
    BE #891 Health Check Service :active, f1_be4, 2026-01-28, 2d
    BE #892 Analytics Service :active, f1_be5, 2026-01-30, 2d
    Test #887 Dashboard Integration :done, f1_t1, 2026-02-02, 1d
    Test #888 Metrics Tests :done, f1_t2, 2026-02-02, 1d
    MERGE FASE 1 :crit, milestone, merge_f1, 2026-02-03, 1d

    section FASE 2 Monitoring (13 issues)
    FE #898 Monitoring Dashboard :active, f2_fe1, 2026-02-04, 3d
    FE #899 Log Viewer UI :active, f2_fe2, 2026-02-07, 2d
    FE #900 Trace Viewer :active, f2_fe3, 2026-02-09, 2d
    FE #901 Metric Charts :active, f2_fe4, 2026-02-11, 2d
    FE #902 Alert Config UI :active, f2_fe5, 2026-02-13, 2d
    BE #903 Logging Service :active, f2_be1, 2026-02-04, 3d
    BE #904 Tracing Service :active, f2_be2, 2026-02-07, 2d
    BE #905 Metrics Collector :active, f2_be3, 2026-02-09, 2d
    BE #906 Alert Manager :active, f2_be4, 2026-02-11, 2d
    Test #907 Monitoring Tests :done, f2_t1, 2026-02-15, 2d
    MERGE FASE 2 :crit, milestone, merge_f2, 2026-02-17, 1d

    section Month 6 Italian UI (14 issues)
    FE #1013 PDF Viewer Integration :active, m6_fe1, 2026-02-18, 3d
    FE #1014 Citation Jump Feature :active, m6_fe2, 2026-02-21, 2d
    FE #1016 Italian UI Strings :active, m6_fe3, 2026-02-23, 3d
    FE #1017 Game Catalog Page :active, m6_fe4, 2026-02-26, 2d
    BE #1012 Adversarial Dataset :active, m6_be1, 2026-02-18, 3d
    BE #1011 Dataset Annotation :active, m6_be2, 2026-02-21, 3d
    Test #1015 PDF Viewer Tests :done, m6_t1, 2026-02-28, 2d
    Test #1018 E2E Question to Citation :done, m6_t2, 2026-03-02, 2d
    COMPLETION GATE :crit, milestone, gate_m6, 2026-03-04, 1d
```

---

## 📊 Parallel Execution Analysis

### Sprint 1: MVP Foundation (Nov 12-25)

**Parallel Execution**: ✅ 1 FE + 1 BE stream

| Track | Issues | Timeline | Can Start |
|-------|--------|----------|-----------|
| 🔵 **Frontend** | 1 | Nov 12-14 | Immediately |
| 🟢 **Backend** | 1 | Nov 12-14 | Immediately |
| 🟡 **Testing** | 3 | Nov 14-20 | After FE/BE complete |
| ⚡ **Sync Point** | - | Nov 25 | Merge to main |

**Issues**:
- 🔵 FE #848: [SPRINT-1] Settings Pages - 4 Tabs Implementation
- 🟢 BE #849: [SPRINT-1] User Profile Management Service
- 🟡 Test #850, #851: Settings & Profile tests

**Dependencies**: Testing waits for FE/BE completion

---

### Sprint 2: Game Management (Nov 26 - Dec 9)

**Parallel Execution**: ✅✅ 2 FE + 3 BE streams (max parallelization!)

| Track | Issues | Timeline | Parallel |
|-------|--------|----------|----------|
| 🔵 **Frontend** | 2 | Nov 26 - Dec 2 | Yes ⚡ |
| 🟢 **Backend** | 3 | Nov 26 - Dec 4 | Yes ⚡ |
| ⚡ **Sync Point** | - | Dec 9 | Integration |

**Issues**:
- 🔵 FE #854: [SPRINT-2] Game Search & Filter UI
- 🔵 FE #855: [SPRINT-2] Game Detail Page - 4 Tabs
- 🟢 BE #852: [SPRINT-2] GameService CRUD Implementation
- 🟢 BE #853: [SPRINT-2] PDF Upload & Processing Pipeline
- 🟢 BE #942: Additional backend work

**Strategy**:
- Week 1: Start all FE + all BE in parallel
- Week 2: Integration testing + merge

---

### Sprint 3: Chat Enhancement (Dec 10-23)

**Parallel Execution**: ✅✅ 2 FE + 3 BE streams

| Track | Issues | Timeline | Parallel |
|-------|--------|----------|----------|
| 🔵 **Frontend** | 2 | Dec 10-14 | Yes ⚡ |
| 🟢 **Backend** | 3 | Dec 10-16 | Yes ⚡ |
| 🟡 **Testing** | 1 | Dec 16-18 | After integration |

**Issues**:
- 🔵 FE #858: Chat UI Enhancement
- 🔵 FE #859: PDF Citation Display Enhancement
- 🟢 BE #856: Chat Thread Management
- 🟢 BE #857: Game-Specific Chat Context
- 🟢 BE #860: Chat Export Functionality

---

### FASE 1: Dashboard Overview (Jan 21 - Feb 3)

**Parallel Execution**: ✅✅✅✅✅ 6 FE + 5 BE streams (MAXIMUM PARALLELIZATION!)

| Track | Issues | Est. Days | Parallel Capacity |
|-------|--------|-----------|-------------------|
| 🔵 **Frontend** | 6 issues | 12 days | 6 streams ⚡⚡⚡ |
| 🟢 **Backend** | 5 issues | 12 days | 5 streams ⚡⚡⚡ |
| 🟡 **Testing** | 4 issues | 4 days | After FE/BE |
| 🔴 **Integration** | - | 1 day | Final merge |

**Frontend Issues**:
1. #883: Dashboard Overview Page (3d)
2. #889: Metrics Visualization Components (2d)
3. #890: Alert Management UI (2d)
4. #893: System Health Dashboard (2d)
5. #894: User Activity Overview (2d)
6. #895: Quick Stats Cards (1d)

**Backend Issues**:
1. #884: Dashboard API Endpoints (3d)
2. #885: Metrics Collection Service (2d)
3. #886: Alert Management Service (2d)
4. #891: Health Check Service (2d)
5. #892: Analytics Aggregation Service (2d)

**Strategy**:
- **Week 1** (Jan 21-27): Start ALL 6 FE + ALL 5 BE simultaneously
- **Week 2** (Jan 28-Feb 3): Complete work + 4 integration tests
- **Feb 3**: MERGE checkpoint (backend first, then frontend)

**Team Allocation**:
- Frontend dev: Can work on 2-3 issues/week
- Backend dev: Can work on 2-3 issues/week
- QA: Integration testing after both tracks complete

---

### FASE 2: Infrastructure Monitoring (Feb 4-17)

**Parallel Execution**: ✅✅✅✅ 5 FE + 4 BE streams

| Track | Issues | Parallel |
|-------|--------|----------|
| 🔵 **Frontend** | 5 | Yes ⚡⚡⚡⚡ |
| 🟢 **Backend** | 4 | Yes ⚡⚡⚡⚡ |
| 🟡 **Testing** | 3 | Sequential |

**Key Issues**:
- FE: Monitoring Dashboard, Log Viewer, Trace Viewer, Metric Charts, Alert Config
- BE: Logging Service, Tracing Service, Metrics Collector, Alert Manager

---

### Month 6: Italian UI (Feb 18 - Mar 4)

**Parallel Execution**: ✅ 3 FE + 2 BE streams

**Current Sprint Focus** - High Priority!

| Track | Issues | Priority | Parallel |
|-------|--------|----------|----------|
| 🔵 **Frontend** | 4 | P0 | Yes ⚡⚡ |
| 🟢 **Backend** | 2 | P0 | Yes ⚡ |
| 🟡 **Testing** | 2 | P0 | After FE/BE |

**Issues**:
- 🔵 FE #1013: PDF Viewer Integration (react-pdf) - 3d
- 🔵 FE #1014: Citation Click → Jump to Page - 2d
- 🔵 FE #1016: Complete Italian UI Strings (200+) - 3d
- 🔵 FE #1017: Game Catalog Page - 2d
- 🟢 BE #1011: Annotation (7 Wonders, Agricola, Splendor) - 3d
- 🟢 BE #1012: Adversarial Dataset (50 queries) - 3d
- 🟡 Test #1015: PDF Viewer Tests
- 🟡 Test #1018: E2E Question → Citation

---

## 🔄 Implementation Flow Diagram

```mermaid
graph TB
    subgraph Sprint1["Sprint 1 - Foundation"]
        FE1[FE #848<br/>Settings Pages]
        BE1[BE #849<br/>User Profile]
        T1[Test #850-851<br/>Integration]
        FE1 -.parallel.-> BE1
        FE1 --> T1
        BE1 --> T1
    end

    subgraph Sprint2["Sprint 2 - Game Management"]
        FE2a[FE #854<br/>Search UI]
        FE2b[FE #855<br/>Detail Page]
        BE2a[BE #852<br/>GameService]
        BE2b[BE #853<br/>PDF Pipeline]
        FE2a -.parallel.-> BE2a
        FE2b -.parallel.-> BE2b
    end

    subgraph FASE1["FASE 1 - Dashboard (MAX PARALLEL)"]
        FE_TRACK["🔵 Frontend Track<br/>6 issues in parallel"]
        BE_TRACK["🟢 Backend Track<br/>5 issues in parallel"]
        TEST_TRACK["🟡 Testing Track<br/>4 issues sequential"]
        FE_TRACK -.parallel.-> BE_TRACK
        FE_TRACK --> TEST_TRACK
        BE_TRACK --> TEST_TRACK
    end

    subgraph Month6["Month 6 - Italian UI (CURRENT)"]
        M6_FE["🔵 FE: PDF Viewer<br/>Italian Strings<br/>Game Catalog"]
        M6_BE["🟢 BE: Dataset<br/>Annotation"]
        M6_TEST["🟡 Test: E2E<br/>PDF Tests"]
        M6_FE -.parallel.-> M6_BE
        M6_FE --> M6_TEST
        M6_BE --> M6_TEST
    end

    Sprint1 ==> Sprint2
    Sprint2 ==> FASE1
    FASE1 ==> Month6

    style FE1 fill:#4A90E2
    style BE1 fill:#7ED321
    style FE_TRACK fill:#4A90E2
    style BE_TRACK fill:#7ED321
    style TEST_TRACK fill:#F5A623
    style M6_FE fill:#4A90E2
    style M6_BE fill:#7ED321
```

---

## 📅 Detailed Week-by-Week Schedule

### Week 1-2: Sprint 1 (Nov 12-25)

**Nov 12-14 (Tue-Thu)**: Parallel Start
- 🔵 FE Team: #848 Settings Pages implementation
- 🟢 BE Team: #849 User Profile Service implementation

**Nov 15-20 (Fri-Wed)**: Testing Phase
- 🟡 QA Team: #850, #851 Settings & Profile tests
- 🔵🟢 Teams: Bug fixes, integration adjustments

**Nov 25 (Mon)**: **SYNC POINT 1**
```bash
git merge backend-dev --no-ff
git merge frontend-dev --no-ff
git tag v1.0-sprint1
```

---

### Week 3-4: Sprint 2 (Nov 26 - Dec 9)

**Nov 26-28 (Tue-Thu)**: Parallel Phase 1
- 🔵 FE: #854 Game Search UI
- 🟢 BE: #852 GameService CRUD

**Nov 29 - Dec 4 (Fri-Wed)**: Parallel Phase 2
- 🔵 FE: #855 Game Detail Page (4 tabs)
- 🟢 BE: #853 PDF Upload Pipeline

**Dec 5-8 (Thu-Sun)**: Integration
- Integration testing
- Bug fixes

**Dec 9 (Mon)**: **SYNC POINT 2**

---

### Week 5-6: Sprint 3 (Dec 10-23)

**Maximum Parallelization**: 2 FE + 3 BE concurrent

**Dec 10-12**:
- 🔵 FE #858: Chat UI Enhancement
- 🟢 BE #856: Chat Thread Management

**Dec 13-15**:
- 🔵 FE #859: PDF Citation Display
- 🟢 BE #857: Game-Specific Context

**Dec 16-18**:
- 🟢 BE #860: Chat Export
- 🟡 Test #861: Integration

**Dec 23**: **SYNC POINT 3**

---

### Week 7-8: FASE 1 Dashboard (Jan 21 - Feb 3)

**🚀 MAXIMUM PARALLEL CAPACITY**: 6 FE + 5 BE = 11 concurrent work items!

**Week 1 (Jan 21-27)**: All Hands On Deck
- 🔵 FE Team starts ALL 6 frontend issues:
  - #883 Dashboard Overview (3d)
  - #889 Metrics Visualization (2d)
  - #890 Alert Management UI (2d)
  - #893 System Health (2d)
  - #894 User Activity (2d)
  - #895 Quick Stats (1d)

- 🟢 BE Team starts ALL 5 backend issues:
  - #884 Dashboard API (3d)
  - #885 Metrics Service (2d)
  - #886 Alert Service (2d)
  - #891 Health Check (2d)
  - #892 Analytics (2d)

**Week 2 (Jan 28 - Feb 3)**: Testing & Integration
- 🟡 QA: All 4 integration tests
- 🔴 Integration: Merge both tracks

**Feb 3**: **CRITICAL MERGE POINT** - FASE 1 Complete

---

## ⚡ Parallelization Strategy

### Work Stream Capacity by Milestone

| Milestone | FE Issues | BE Issues | Test Issues | Parallel Streams | Efficiency |
|-----------|-----------|-----------|-------------|------------------|------------|
| Sprint 1 | 1 | 1 | 3 | ⚡ 1 stream | 50% |
| Sprint 2 | 2 | 3 | 0 | ⚡⚡ 2 streams | 100% |
| Sprint 3 | 2 | 3 | 1 | ⚡⚡ 2 streams | 100% |
| Sprint 4 | 3 | 2 | 0 | ⚡⚡ 2 streams | 100% |
| Sprint 5 | 1 | 3 | 1 | ⚡ 1 stream | 75% |
| **FASE 1** | **6** | **5** | **4** | **⚡⚡⚡⚡⚡ 5 streams** | **150%+** |
| FASE 2 | 5 | 4 | 3 | ⚡⚡⚡⚡ 4 streams | 125% |
| FASE 3 | 5 | 3 | 3 | ⚡⚡⚡ 3 streams | 100% |
| Month 6 | 4 | 2 | 2 | ⚡⚡ 2 streams | 75% |

**Legend**:
- ⚡ = 1 parallel stream (FE + BE working simultaneously)
- Efficiency = (FE+BE concurrent work) / sequential work

---

## 🎯 Critical Dependencies

### Must Complete Before Others

**Foundation** (Sprint 1-2):
- BE #849 User Profile Service → Required for user management in all features
- BE #852 GameService → Required for all game-related features
- FE #848 Settings Pages → Required for user preferences in later features

**Blockers** (Month 3-6):
- BE Multi-Model Validation (Month 3) → Must complete before Month 6 accuracy testing
- BE Dataset Annotation (Month 5) → Required for Month 6 completion checklist

### Can Run Independently (High Parallelization)

**FASE 1-2** (Dashboard + Monitoring):
- All dashboard FE/BE issues are independent
- Can start all simultaneously
- Only testing waits for completion

**Month 6** (Current Sprint):
- PDF Viewer (FE) independent from Dataset (BE)
- Italian strings (FE) independent from Annotation (BE)
- High parallel capacity

---

## 📋 Daily Execution Checklist

### Morning (Every Day)

**Frontend Worktree**:
```bash
cd ../meepleai-frontend
git fetch origin
git rebase origin/main frontend-dev
pnpm install  # if needed
```

**Backend Worktree**:
```bash
cd ../meepleai-backend
git fetch origin
git rebase origin/main backend-dev
dotnet restore  # if needed
```

### During Day

**Before Starting Work on Issue**:
- [ ] Check issue in GitHub Project board
- [ ] Verify no blocking dependencies
- [ ] Create feature branch: `feature/issue-{number}-short-name`
- [ ] Update issue status to "In Progress" in project

**While Working**:
- [ ] Commit frequently (atomic commits)
- [ ] Run tests locally before pushing
- [ ] Update issue with progress comments

### Evening (End of Day)

**Push Work**:
```bash
git push origin feature/issue-{number}  # Push feature branch
```

**Update Project**:
- [ ] Add comment on issue with progress
- [ ] Update checklist items if applicable

---

## 🔀 End-of-Sprint Integration

### Every 2 Weeks (Sync Points)

**Day Before Sync** (e.g., Nov 24 for Sprint 1):

1. **Verify Both Tracks**:
```bash
# Frontend
cd ../meepleai-frontend
pnpm test  # Must pass!
pnpm build  # Must succeed!

# Backend
cd ../meepleai-backend
dotnet test  # Must pass!
dotnet build  # Must succeed!
```

2. **Merge to Main** (from main worktree):
```bash
cd meepleai-monorepo
git checkout main
git pull origin main

# Backend FIRST (API contracts)
git merge backend-dev --no-ff -m "Merge: Sprint 1 Backend"

# Frontend SECOND (consumes backend)
git merge frontend-dev --no-ff -m "Merge: Sprint 1 Frontend"

# Tag release
git tag -a v1.0-sprint1 -m "Sprint 1: MVP Foundation"
git push origin main --tags
```

3. **Update Project**:
- Bulk select Sprint 1 issues
- Set Status → Done
- Add completion comment

---

## 📊 Timeline Summary

| Phase | Duration | Start | End | Parallel Capacity |
|-------|----------|-------|-----|-------------------|
| Sprint 1 | 2 weeks | Nov 12 | Nov 25 | ⚡ Low |
| Sprint 2-5 | 8 weeks | Nov 26 | Jan 20 | ⚡⚡ Medium |
| FASE 1 | 2 weeks | Jan 21 | Feb 3 | ⚡⚡⚡⚡⚡ Maximum |
| FASE 2-3 | 4 weeks | Feb 4 | Mar 3 | ⚡⚡⚡⚡ High |
| Month 6 | 2 weeks | Mar 4 | Mar 17 | ⚡⚡ Medium |
| **TOTAL** | **~18 weeks** | **Nov 12** | **Mar 17** | **Avg: 2-3 streams** |

**With Parallelization**: Sequential would be ~28 weeks, parallel reduces to **18 weeks** (36% faster!)

---

## 🎯 Next Actions

### This Week (Sprint 1)
- [ ] Start #848 (FE) and #849 (BE) simultaneously
- [ ] Daily standup to sync FE/BE progress
- [ ] Integration testing #850, #851 after FE/BE complete
- [ ] Prepare for Nov 25 sync point

### Next Sprint (Sprint 2)
- [ ] Higher parallelization (2 FE + 3 BE)
- [ ] PDF pipeline (BE) can run while Game UI (FE) develops
- [ ] Plan for Dec 9 integration

### February (FASE 1) - Plan Ahead!
- [ ] **Maximum parallelization milestone**
- [ ] Allocate full team capacity
- [ ] Prepare for 11 concurrent work items
- [ ] Schedule integration testing resources

---

**Last Updated**: 2025-11-12 21:10
**Total Sync Points**: 12
**Parallel Milestones**: 12/15 (80%)
