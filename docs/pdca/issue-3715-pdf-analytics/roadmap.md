# Issue #3715: PDF Analytics - Implementation Roadmap

**Epic**: Task #10
**Created**: 2026-02-13
**Duration**: 4 days (32h)
**Status**: Ready for execution

---

## Epic Overview

### **Goal**: Comprehensive PDF analytics with role-based dashboards

**Users Served**:
- **Admin**: System monitoring, bulk operations, failed PDF management
- **User**: Personal usage tracking, retry own failed PDFs
- **Editor**: Proposal-related PDF tracking

**Value Delivered**:
- System health visibility (success rate, processing times)
- User quota transparency (X/Y PDFs this month)
- Failed PDF recovery (manual retry + auto-retry)
- Storage optimization insights (by tier/user/status)

---

## Issue Breakdown (10 Tasks)

```
Epic #3715 (32h total)
├─ Phase 1: Backend Foundation (8.5h)
│  ├─ Task #11: DB Schema (2h) - FOUNDATION
│  ├─ Task #12: Analytics API (3h) - Blocked by #11
│  ├─ Task #13: Failed PDFs API (2h) - Blocked by #11
│  └─ Task #14: Storage Breakdown API (1.5h) - Blocked by #11
│
├─ Phase 2: Background Jobs (6h)
│  ├─ Task #15: Aggregation Job (4h) - Blocked by #11
│  └─ Task #16: Manual Retry Endpoint (2h) - Independent
│
├─ Phase 3: Admin Dashboard (12h)
│  ├─ Task #17: Analytics Page + Stats (4h) - Blocked by #12
│  ├─ Task #18: Charts Components (3h) - Blocked by #12, #14
│  └─ Task #19: Failed PDF Viewer (5h) - Blocked by #13, #16
│
└─ Phase 4: User View (4h)
   └─ Task #20: User Usage Page (4h) - Blocked by #12, #13, #16

Documentation (2h): Integrated in each phase
Testing (4h): Integrated in each phase
```

---

## Dependency Graph

```
Task #11 (DB Schema)
├─→ Task #12 (Analytics API)
│   ├─→ Task #17 (Admin Dashboard)
│   ├─→ Task #18 (Charts)
│   └─→ Task #20 (User Page)
├─→ Task #13 (Failed PDFs API)
│   ├─→ Task #19 (Failed Viewer)
│   └─→ Task #20 (User Page)
├─→ Task #14 (Storage API)
│   └─→ Task #18 (Charts)
└─→ Task #15 (Aggregation Job)

Task #16 (Manual Retry) [Independent]
├─→ Task #19 (Failed Viewer)
└─→ Task #20 (User Page)
```

**Critical Path**: #11 → #12 → #17 (9h)
**Parallelizable**: #16 can start immediately

---

## Timeline (4 Days)

### **Day 1: Backend Foundation** (8.5h)

**Morning** (4h):
```
08:00-10:00 | Task #11: DB Schema + Migrations
            | - Create PdfProcessingMetrics table
            | - Create PdfStateTransitions table
            | - EF Core configurations
            | - Historical backfill script

10:00-11:30 | Task #12: Analytics API (Part 1)
            | - GetPdfAnalyticsQuery/Handler
            | - Basic aggregation queries
```

**Afternoon** (4.5h):
```
13:00-14:30 | Task #12: Analytics API (Part 2)
            | - Complete handler, add caching
            | - Create endpoint, unit tests

14:30-16:30 | Task #13: Failed PDFs API
            | - GetFailedPdfsQuery/Handler
            | - Pagination, filters, role-based

16:30-18:00 | Task #14: Storage Breakdown API
            | - GetStorageBreakdownQuery
            | - Multiple groupBy options
```

**Deliverables**: All backend APIs ready ✅

---

### **Day 2: Jobs + Admin UI Start** (8h)

**Morning** (4h):
```
08:00-12:00 | Task #15: Aggregation Background Job
            | - PdfMetricsAggregationJob (Quartz)
            | - Daily midnight UTC schedule
            | - Historical backfill (90 days)
            | - Testing job execution
```

**Afternoon** (4h):
```
13:00-15:00 | Task #16: Manual Retry Endpoint
            | - Extend RetryPdfProcessingCommand
            | - Add endpoint + permissions
            | - Integration tests

15:00-17:00 | Task #17: Admin Analytics Page (Part 1)
            | - Route setup in AI Platform tab
            | - PdfAnalyticsOverview component
            | - 4 stat cards scaffolding
```

**Deliverables**: Backend complete, Admin UI started ✅

---

### **Day 3: Admin Dashboard Complete** (8h)

**Morning** (4h):
```
08:00-10:00 | Task #17: Admin Analytics (Part 2)
            | - Complete stat cards with API
            | - Loading/error states
            | - Responsive layout

10:00-12:00 | Task #18: Charts Components (Part 1)
            | - UploadsChart (time series)
            | - Setup recharts
```

**Afternoon** (4h):
```
13:00-14:30 | Task #18: Charts (Part 2)
            | - ProcessingTimeChart
            | - StorageBreakdownChart
            | - Integration in dashboard

14:30-18:00 | Task #19: Failed PDF Viewer (Part 1)
            | - FailedPdfTable component
            | - Basic table layout
```

**Deliverables**: Charts done, Failed viewer started ✅

---

### **Day 4: User View + Polish** (7.5h)

**Morning** (3.5h):
```
08:00-11:30 | Task #19: Failed PDF Viewer (Part 2)
            | - Filters component
            | - Bulk actions bar (admin)
            | - Export CSV function
            | - Role-based rendering
            | - Integration tests
```

**Afternoon** (4h):
```
13:00-17:00 | Task #20: User PDF Usage Page
            | - Route: /settings/usage#pdfs tab
            | - QuotaProgressCard
            | - MyFailedPdfsCard (simplified)
            | - ProcessingHistoryTimeline
            | - API integration + tests
```

**Deliverables**: All features complete ✅

---

## Parallel Work Opportunities

### **Day 1** (Backend):
```
Can Parallelize:
- Task #11 (DB) → solo dev
- After #11 complete:
  - Task #12 + Task #13 (2 devs in parallel)
  - OR Task #16 (independent, can start anytime)

Time Savings: ~2h if 2 devs
```

### **Day 2** (Jobs + UI):
```
Can Parallelize:
- Task #15 (Backend job) + Task #17 (Frontend UI)
- Different domains, no conflicts

Time Savings: ~2h
```

### **Day 3** (Charts + Viewer):
```
Can Parallelize:
- Task #18 (Charts) + Task #19 (Table)
- Both frontend, minimal conflicts

Time Savings: ~2h
```

**Total Parallelization**: 6h savings (28h vs 32h with 2 devs)

---

## Risk Assessment

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **Aggregation Performance** | Medium | High | Index optimization, limit backfill |
| **Charts Library Issues** | Low | Medium | Recharts already used in project |
| **Role Filtering Bugs** | Medium | High | Comprehensive permission tests |
| **Data Privacy Leaks** | Low | Critical | Security review, role-based tests |
| **Storage Calculation Wrong** | Medium | Medium | Cross-check with S3 API |

**Mitigation Strategy**:
- Task #11: Performance test aggregation with 100K PDFs
- Task #17-20: Security review for role-based access
- All tasks: Unit tests ≥90% backend, ≥85% frontend

---

## Success Metrics

### **Functional** (DoD)
- [ ] Admin dashboard shows system-wide metrics
- [ ] User page shows personal usage + quota
- [ ] Failed PDFs viewable (admin: all, user: own)
- [ ] Manual retry works (button → processing)
- [ ] Auto-retry runs 3x max
- [ ] Storage breakdown accurate (matches S3)
- [ ] Export CSV generates correct data

### **Performance**
- [ ] Dashboard load <2s (10K PDFs)
- [ ] Analytics API <500ms
- [ ] Charts render <1s
- [ ] Aggregation job <5min (100K PDFs)

### **Quality**
- [ ] Test coverage ≥90% backend
- [ ] Test coverage ≥85% frontend
- [ ] No security vulnerabilities (role filtering tested)
- [ ] Mobile responsive (all breakpoints)
- [ ] Accessibility WCAG 2.1 AA

---

## Phase Gates

### **Phase 1 → Phase 2** (End of Day 1)
**Gate Criteria**:
- [ ] All 4 backend APIs functional
- [ ] Unit tests passing (≥90%)
- [ ] Performance tested (10K PDF query <500ms)
- [ ] Code review approved

**Blocker**: If APIs fail performance test, optimize before Phase 2

---

### **Phase 2 → Phase 3** (End of Day 2)
**Gate Criteria**:
- [ ] Aggregation job runs successfully
- [ ] Historical backfill complete (90 days)
- [ ] Manual retry endpoint working
- [ ] Integration tests passing

**Blocker**: If aggregation takes >5min, optimize queries

---

### **Phase 3 → Phase 4** (End of Day 3)
**Gate Criteria**:
- [ ] Admin dashboard functional
- [ ] All charts rendering
- [ ] Failed PDF viewer working
- [ ] E2E test: Admin flow passing

**Blocker**: If charts have rendering issues, debug before User page

---

### **Phase 4 → Completion** (End of Day 4)
**Gate Criteria**:
- [ ] User page functional
- [ ] All role-based views tested
- [ ] Export CSV working
- [ ] Full E2E tests passing (admin + user flows)
- [ ] Documentation complete

**Blocker**: None expected (all dependencies resolved)

---

## Rollout Strategy

### **Alpha** (Internal Testing - Day 4 afternoon)
```
Users: Admin only (1-2 admins)
Features: Full dashboard
Duration: 2-3 days
Goal: Catch edge cases, validate performance
```

### **Beta** (Selected Users - Week 2)
```
Users: Admin + 10 beta users
Features: Admin + User pages
Duration: 1 week
Goal: Gather feedback, iterate
```

### **GA** (General Availability - Week 3)
```
Users: All (Admin, User, Editor)
Features: Complete
Announcement: In-app notification + email
```

---

## Monitoring & Alerts

### **Post-Launch Monitoring**
```yaml
Metrics to Track:
  - Dashboard load time (target: <2s)
  - API response times (target: <500ms)
  - Aggregation job duration (target: <5min)
  - Failed PDF retry success rate (target: >80%)

Alerts:
  - Dashboard load >5s (1 min, 3 occurrences) → Page Admins
  - Aggregation job fails → Email DevOps
  - Failed PDF count >100 (daily) → Investigate pipeline
  - Storage >80% capacity → Notify admins
```

---

## Next Steps

### **Immediate** (Before Starting):
- [ ] Review roadmap with team
- [ ] Assign tasks to developers
- [ ] Set up Quartz.NET if not configured
- [ ] Verify recharts library version

### **Day 1 Morning** (Start):
- [ ] Create feature branch: `feature/epic-3715-pdf-analytics`
- [ ] Start Task #11 (DB Schema)
- [ ] Parallel: Task #16 (Manual Retry - independent)

### **Daily Standup** (During):
- Review progress vs roadmap
- Unblock dependencies
- Adjust timeline if needed

---

**Epic Ready**: ✅
**Sub-Issues Created**: 10
**Dependencies Mapped**: ✅
**Roadmap Complete**: ✅

**Ready to start implementation?** 🚀
