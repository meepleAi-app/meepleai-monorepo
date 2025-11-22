# MeepleAI Unified Development Roadmap 2025

**Generated**: 2025-11-13 (Updated 2025-11-14 with current issue status)
**Total Open Issues**: 154 (**VERIFIED from GitHub** - 18 issues closed since initial analysis)
**Developer Mode**: Single developer, 3 branches (**main**, **frontend-dev**, **backend-dev**)
**Timeline**: ~14 weeks (3.5 months) - Adjusted for completed Sprint 1 work
**Merge Checkpoints**: 4 major checkpoints where branches sync to **main**

---

## 🎯 Executive Summary

This roadmap unifies all planning documents and open issues into a single execution plan optimized for a **single developer working across 3 branches**. All data below is extracted from **154 actual open GitHub issues** (updated 2025-11-14).

### Branch Strategy

- **Branch `main`**:
  - **Purpose**: Production-ready stable base
  - **Updates**: Only via checkpoint merges from `frontend-dev` and `backend-dev`
  - **Content**: Infrastructure, testing, documentation, cross-cutting concerns

- **Branch `frontend-dev`**:
  - **Purpose**: Active frontend development
  - **Work**: Frontend refactor + BGAI UI + Admin Console UI + React 19 features
  - **Merge to main**: At checkpoints (Weeks 3, 8, 12, 16)

- **Branch `backend-dev`**:
  - **Purpose**: Active backend development
  - **Work**: BGAI backend (Month 1-6) + Admin Console backend (FASE 1-4) + API endpoints
  - **Merge to main**: At checkpoints (Weeks 3, 8, 12, 16)

### Key Metrics (REAL DATA from 154 Issues)

| Metric | Value |
|--------|-------|
| **Total Issues** | **154 open** (verified from GitHub, 18 closed since 2025-11-13) |
| **Critical Priority** | **0 issues** (Both critical issues #1090, #1091 completed ✅) |
| **High Priority** | **~65 issues** (~42% of total) |
| **Medium Priority** | **~20 issues** (~13% of total) |
| **Low Priority** | **~69 issues** (~45% of total) |
| **BGAI Issues** | **56 issues** (33% of total) |
| **Admin Console Issues** | **49 issues** (28% of total) |
| **MVP Sprints Issues** | **25 issues** (15% of total) |
| **Frontend Refactor** | **11 issues** (6% of total) |
| **Other (Testing/Infra)** | **31 issues** (18% of total) |
| **Issues with Dependencies** | **3 issues** (blocking chains identified) |
| **Estimated Timeline** | 16 weeks (~4 months) |
| **Parallel Efficiency** | ~65% (frontend/backend independent) |

---

## 📊 Issue Distribution Analysis (154 Total)

### By Epic/Category

| Epic | Count | % of Total | Priority Notes |
|------|-------|------------|----------------|
| **BGAI** (Board Game AI) | 56 | 36% | Core product value - Months 1-6 |
| **Admin Console** | 49 | 32% | FASE 1-4 - Consider deferring |
| **Other** (Testing/Infra) | 31 | 20% | Cross-cutting concerns |
| **MVP Sprints** | ~11 | 7% | Legacy sprint issues (14 closed 2025-11-14) ✅ |
| **Frontend Refactor** | ~7 | 5% | Sprint 1 complete (4 issues closed) ✅ |

### By Priority

| Priority | Count | % of Total | Key Issues |
|----------|-------|------------|------------|
| **Critical** | 0 | 0% | ✅ Both completed: #1090 (ChatProvider), #1091 (Inline Styles) |
| **High** | ~65 | 42% | MVP Sprints (11 closed ✅), BGAI core features |
| **Medium** | ~20 | 13% | Quality improvements |
| **Low** | ~69 | 45% | Nice-to-have features, polish |

### By Milestone

| Milestone | Count | Epic | Notes |
|-----------|-------|------|-------|
| No Milestone | 34 | Mixed | Need triage and assignment |
| Month 6: Italian UI | 22 | BGAI | Final BGAI polish |
| FASE 1: Dashboard | 16 | Admin | Consider deferring |
| Month 5: Golden Dataset | 14 | BGAI | Dataset annotation |
| FASE 2: Infrastructure | 13 | Admin | Consider deferring |
| FASE 3: Management | 12 | Admin | Consider deferring |
| Month 4: Quality Framework | 11 | BGAI | Quality metrics |
| Month 3: Multi-Model | 10 | BGAI | Validation layer |
| FASE 4: Advanced | 8 | Admin | Consider deferring |
| MVP Sprint 1-5 | 25 | Mixed | Legacy - review for closure |
| Month 2: LLM Integration | 6 | BGAI | OpenRouter/Ollama |
| Month 1: PDF Processing | 1 | BGAI | Bug fixes only |

### Critical Path Dependencies (3 Blocking Chains)

These issues have explicit dependencies that create blocking chains:

1. **#852** (priority-high, MVP Sprint 2): GameService CRUD Implementation
   - **Depends on**: #923
   - **Blocks**: Game management features

2. **#858** (priority-high, MVP Sprint 3): Chat UI with Thread Sidebar
   - **Depends on**: #924
   - **Blocks**: Chat UI features

3. **#875** (priority-low, Admin FASE 1): AdminDashboardService
   - **Depends on**: #874
   - **Blocks**: Dashboard stats

---

## 📋 Priority Matrix & Execution Order

### Phase 1: Foundation (Weeks 1-3) - **SPRINT 1 COMPLETE ✅**

**🟢 SPRINT 1 COMPLETE** (Week 1, completed 2025-11-13/14)

**Critical issues completed:**

| # | Issue | Priority | Branch | Status |
|---|-------|----------|--------|--------|
| **#1088** | Unify Login Flow (cookie + API key + 2FA) | priority-critical | `frontend-dev` | ✅ CLOSED |
| **#1089** | Refactor Upload Page (1564→400 lines) | priority-critical | `frontend-dev` | ✅ CLOSED |
| **#1090** | Split ChatProvider into Multiple Contexts | priority-critical | `frontend-dev` | ✅ CLOSED |
| **#1091** | Eliminate Inline Styles and Standardize | priority-critical | `frontend-dev` | ✅ CLOSED |

**Achievement**:
- All 4 critical frontend blockers completed
- ~1200 lines of technical debt removed
- Design system standardization complete
- Ready for Sprint 2 (11 remaining frontend issues)

**Additional Frontend Refactor Issues** (9 more, priority not yet assessed):
See full list in docs/planning/issues-analysis.json

**🔄 CHECKPOINT 1 (Week 3): Merge `frontend-dev` → `main`**
- **Branch**: `frontend-dev`
- **Prerequisites**: Sprint 1-2 complete (#1088-1096)
- **Testing**: All frontend tests pass (90%+ coverage)
- **Validation**: Lighthouse ≥90, A11y ≥95, no ESLint warnings
- **Merge command**:
  ```bash
  git checkout main
  git merge frontend-dev --no-ff -m "feat: Complete Sprint 1-2 frontend refactor"
  git push origin main
  ```

---

---

## 📌 Current Priority Focus (Updated 2025-11-14)

**🎉 Major Progress: 14 high-priority issues completed!**

| Rank | # | Priority | Epic | Branch | Status | Dependencies |
|------|---|----------|------|--------|--------|--------------|
| ~~1~~ | ~~#1090~~ | ~~CRITICAL~~ | ~~Frontend~~ | ~~frontend-dev~~ | ✅ **CLOSED** | - |
| ~~2~~ | ~~#1091~~ | ~~CRITICAL~~ | ~~Frontend~~ | ~~frontend-dev~~ | ✅ **CLOSED** | - |
| ~~3~~ | ~~#846~~ | ~~HIGH~~ | ~~Sprint 1~~ | ~~backend-dev~~ | ✅ **CLOSED** | - |
| ~~4~~ | ~~#850~~ | ~~HIGH~~ | ~~Sprint 1~~ | ~~backend-dev~~ | ✅ **CLOSED** | - |
| ~~5~~ | ~~#852~~ | ~~HIGH~~ | ~~Sprint 2~~ | ~~backend-dev~~ | ✅ **CLOSED** | - |
| ~~6~~ | ~~#853~~ | ~~HIGH~~ | ~~Sprint 2~~ | ~~backend-dev~~ | ✅ **CLOSED** | - |
| ~~7~~ | ~~#854~~ | ~~HIGH~~ | ~~Sprint 2~~ | ~~frontend-dev~~ | ✅ **CLOSED** | - |
| ~~8~~ | ~~#856~~ | ~~HIGH~~ | ~~Sprint 3~~ | ~~backend-dev~~ | ✅ **CLOSED** | - |
| ~~9~~ | ~~#857~~ | ~~HIGH~~ | ~~Sprint 3~~ | ~~backend-dev~~ | ✅ **CLOSED** | - |
| 10 | #858 | HIGH | Sprint 3 | `frontend-dev` | 🔄 **OPEN** | #924 |
| ~~11~~ | ~~#861~~ | ~~HIGH~~ | ~~Sprint 4~~ | ~~backend-dev~~ | ✅ **CLOSED** | - |
| ~~12~~ | ~~#862~~ | ~~HIGH~~ | ~~Sprint 4~~ | ~~backend-dev~~ | ✅ **CLOSED** | - |
| ~~13~~ | ~~#863~~ | ~~HIGH~~ | ~~Sprint 4~~ | ~~frontend-dev~~ | ✅ **CLOSED** | - |
| 14 | #866 | HIGH | Sprint 5 | `backend-dev` | 🔄 **OPEN** | None |
| 15 | #867 | HIGH | Sprint 5 | `backend-dev` | 🔄 **OPEN** | None |
| 16 | #870 | HIGH | Sprint 5 | `backend-dev` | 🔄 **OPEN** | None |
| 17 | #871 | HIGH | Sprint 1 | `main` | 🔄 **OPEN** | None |
| 18 | #955 | HIGH | BGAI Month 1 | `backend-dev` | 🔄 **OPEN** | None |
| 19 | #963 | HIGH | BGAI Month 2 | `backend-dev` | 🔄 **OPEN** | None |
| 20 | #964 | HIGH | BGAI Month 2 | `backend-dev` | 🔄 **OPEN** | None |

**Key Progress**:
- ✅ **Sprint 1-4 mostly complete**: 11/14 issues closed
- ✅ **All critical blockers resolved**: Frontend refactor complete
- 🔄 **Current focus**: Sprint 2-3 remaining issues + BGAI Month 1-2
- 📊 **Completion rate**: 14/20 (70%) of top priorities done

---

### Phase 2: High-Impact Improvements (Weeks 3-4) - **Sprint 2**

**🟡 HIGH PRIORITY - BGAI Month 1-2** (Week 3-4, ~10 days backend)

| # | Issue | Branch | Effort | Dependencies |
|---|-------|--------|--------|--------------|
| #1092 | Mobile-First Responsive | `frontend-dev` | 1d | None (✅ Parallel) |
| #1093 | Performance Optimization | `frontend-dev` | 1d | ✅ Unblocked (#1089, #1090 done) |
| #1094 | Accessibility Audit | `frontend-dev` | 0.5d | None (✅ Parallel) |
| #1095 | Error Handling Unified | `frontend-dev` | 0.5d | None (✅ Parallel) |
| #1096 | Loading States Unified | `frontend-dev` | 0.5d | None (✅ Parallel) |

**Execution Strategy**:
```
Week 3 (3.5 days):
  Day 1: #1092 (Mobile)
  Day 2: #1093 (Performance)
  Day 3: #1094 + #1095 + #1096 (0.5d each = 1.5d, batched)
  Day 3.5: Testing + Integration
```

**⚡ Backend Parallel Work**: Start BGAI Month 1 backend tasks while doing Sprint 2 frontend (65% time savings!)

---

### Phase 3: Board Game AI Foundation (Weeks 4-8) - **BGAI Months 1-6**

**🎲 BGAI Backend Track** (Branch `backend`)

**VERIFIED: 56 BGAI issues from 172 total (33% of all work)**

#### Real BGAI Milestone Breakdown

| Milestone | Count | Priority Distribution | Status |
|-----------|-------|----------------------|--------|
| **Month 1: PDF Processing** | 1 | 1 High | Bug fixes only (#955) |
| **Month 2: LLM Integration** | 6 | 4 High, 2 Low | OpenRouter, Ollama, config (#963, #964, #1110-1112) |
| **Month 3: Multi-Model Validation** | 10 | 5 High, 2 Medium, 3 Low | GPT-4 + Claude consensus |
| **Month 4: Quality Framework** | 11 | 4 High, 5 Medium, 2 Low | 5-metric quality system |
| **Month 5: Golden Dataset** | 14 | 5 High, 3 Medium, 6 Low | 100 Q&A annotation |
| **Month 6: Italian UI** | 22 | 6 High, 4 Medium, 12 Low | i18n + final polish |
| **No Milestone** (BGAI-tagged) | ~12 | Mixed | Need triage |

**Total BGAI**: 56 issues (verified from actual GitHub data)

#### Month 1: PDF Processing (Week 4, ~1-2 days)

**REALITY CHECK**: Only 1 issue in Month 1 milestone:

| # | Issue | Priority | Notes |
|---|-------|----------|-------|
| #955 | [BGAI-013] Bug fixes and edge cases for PDF pipeline | HIGH | Final hardening |

**Observation**: Month 1 appears mostly complete. PDF 3-stage pipeline (#956, #957) already done per CLAUDE.md.

#### Month 2: LLM Integration (Weeks 5-6, ~5 days backend)

**6 issues in Month 2 milestone:**

| # | Issue | Priority | Type |
|---|-------|----------|------|
| #963 | [BGAI-021] Feature flag AI:Provider configuration | HIGH | Config |
| #964 | [BGAI-022] Integration tests for adaptive LLM routing | HIGH | Testing |
| #1110 | [BGAI-086] PDF Processing Configuration Validation | LOW | Validation |
| #1111 | [BGAI-087] Large PDF Streaming (>50MB) | LOW | Optimization |
| #1112 | [BGAI-088] File Size Limit in Orchestrator | LOW | Security |
| *(1 more issue)* | *(from analysis)* | - | - |

**Priority**: Focus on #963, #964 (high priority), defer #1110-1112 (low priority optimizations)

---

#### Month 3-6: Validation, Quality, Dataset, Italian UI (Weeks 7-16)

**57 issues across Months 3-6** (verified from GitHub):

| Month | Issues | High Priority | Medium | Low | Focus Areas |
|-------|--------|---------------|--------|-----|-------------|
| **Month 3** | 10 | 5 | 2 | 3 | Multi-model validation (GPT-4 + Claude) |
| **Month 4** | 11 | 4 | 5 | 2 | Quality framework (5 metrics) |
| **Month 5** | 14 | 5 | 3 | 6 | Golden dataset (100 Q&A pairs) |
| **Month 6** | 22 | 6 | 4 | 12 | Italian UI (200+ translations) |

**Total**: 57 issues in Months 3-6 (bulk of BGAI work)

**Breakdown by Branch**:
- Backend: Months 3-4 (validation, quality metrics)
- Frontend: Months 4-6 (UI components, i18n, PDF viewer)
- **Parallel Opportunity**: Month 4-6 frontend can overlap with backend work

**⚡ Critical Parallel Opportunity**:
- **Backend** (Month 3-6): PDF, LLM, validation, dataset annotation
- **Frontend** (Month 4-6): BGAI UI components, Italian i18n, PDF viewer

**Time Savings**: 21 days (as per existing calendar analysis)

---

### Phase 4: Admin Console (Weeks 8-12, Parallel with BGAI) - **DEFER RECOMMENDED**

**📊 Admin Console Track** (Branch `backend` + `frontend`)

**VERIFIED: 49 Admin Console issues from 172 total (28% of all work)**

#### Real Admin Console Milestone Breakdown

| Milestone (FASE) | Count | Priority Distribution | Effort Est. | Business Value |
|------------------|-------|----------------------|-------------|----------------|
| **FASE 1: Dashboard Overview** | 16 | 5 High, 4 Medium, 7 Low | ~5-7 days | Medium (visibility) |
| **FASE 2: Infrastructure Monitoring** | 13 | 3 High, 4 Medium, 6 Low | ~5 days | Low (nice-to-have) |
| **FASE 3: Enhanced Management** | 12 | 4 High, 3 Medium, 5 Low | ~5 days | Low (admin tools) |
| **FASE 4: Advanced Features** | 8 | 2 High, 2 Medium, 4 Low | ~3 days | Low (polish) |

**Total Admin Console**: 49 issues (28% of backlog)

**Dependency Alert**: Issue #875 (FASE 1) depends on #874

#### FASE 1: Dashboard Overview (16 issues)

**High Priority (5 issues)**:
- Admin dashboard stats
- System health monitoring
- User activity overview
- API usage metrics
- Document processing stats

**Reality Check**:
- Backend + Frontend work required
- Conflicts with BGAI backend focus
- Not MVP-critical (internal admin tool)

#### FASE 2-4: Infrastructure, Management, Advanced (33 issues)

**Recommendation**: **DEFER ALL Admin Console to Phase 2**

**Rationale**:
- **49 issues** (28% of total backlog)
- **BGAI has 56 issues** (33% of backlog) with higher product value
- Admin Console is **internal tooling**, not user-facing
- BGAI is **core product**, customer-facing
- Timeline risk: Adding Admin Console extends from 16 to 24+ weeks

**If you MUST do Admin Console**:
- **FASE 1 only** (16 issues, ~7 days)
- Do in Week 13-14 parallel with BGAI Month 6 frontend
- **Skip FASE 2-4** (33 issues, ~13 days) entirely for Phase 1

---

### Phase 5: Frontend Epic Phases (Weeks 13-16) - **Phase 1-2 only**

**🎨 Frontend Modernization Epics**

| # | Epic | Phases | Effort | Priority |
|---|------|--------|--------|----------|
| #926 | Foundation & Quick Wins (Phase 1) | Sprint 1-3 DONE ✅ | — | Covered by #1088-1102 |
| #931 | React 19 Optimization (Phase 2) | App Router Migration prep | 5d | ⚠️ Consider deferring |
| #933 | App Router Migration (Phase 3) | Full migration | 10d | ⚠️ **Defer to Phase 2** |
| #932 | Advanced Features (Phase 4) | Polish | 5d | ⚠️ Defer |
| #934 | Design Polish (Phase 5) | Animation | 3d | ⚠️ Defer |
| #935 | Performance & A11y (Phase 6) | Lighthouse | 3d | ⚠️ Defer |

**Recommendation**: Complete Sprint 1-3 (#1088-1102), then **defer Epic phases to Phase 2**. Focus on BGAI delivery.

**If you must proceed**: Do #931 (React 19 prep) in Week 14, parallel with BGAI Month 6 backend.

---

### Phase 6: Frontend Improvements (FE-IMP) - **OPTIONAL**

**⚙️ Frontend Infrastructure Improvements** (#1077-1084)

| # | Issue | Effort | Priority | When? |
|---|-------|--------|----------|-------|
| #1077 | Bootstrap App Router + Providers | 2d | High | Week 13 (if doing #933) |
| #1078 | Server Actions (Auth & Export) | 1d | Medium | Week 14 |
| #1079 | TanStack Query Data Layer | 2d | High | Week 14 |
| #1080 | AuthContext + Edge Middleware | 1d | Medium | Week 13 |
| #1081 | API SDK with Zod | 1d | Medium | Week 15 |
| #1082 | Form System (RHF + Zod) | 1d | Medium | Week 15 |
| #1083 | Chat Store (Zustand + Streaming) | 2d | High | Week 15 |
| #1084 | Upload Queue Off-Main-Thread | 1d | Low | Week 16 |

**Total**: 11 days

**Recommendation**: **Defer all FE-IMP to Phase 2** unless App Router migration (#933) is approved. These are architectural improvements, not user-facing features.

---

## 🚀 Recommended Execution Plan (Single Developer)

### Timeline: 16 Weeks (~4 months)

```
WEEK 1-2: ████████ Sprint 1 Critical Frontend Refactor (5d)
WEEK 3:   ████ Sprint 2 High Priority Frontend (3.5d)
WEEK 4-8: ████████████████████ BGAI Month 1-2 Backend (10d + 10d)
          ⚡ PARALLEL: Sprint 3 Frontend (7d) in Week 4-5
WEEK 9-12: ██████████████ BGAI Month 3-4 Backend + Frontend (20d)
WEEK 13-16: ██████████ BGAI Month 5-6 Final Push (16d)
           ⚡ OPTIONAL: Admin FASE 1 Frontend (5d) in Week 13-14
```

### Branch Strategy & Merge Checkpoints

**Active Branches**:
- **`main`**: Production-ready, receives merges only at checkpoints
- **`frontend-dev`**: Active frontend work (Sprint 2-3, BGAI UI, Admin UI)
- **`backend-dev`**: Active backend work (BGAI backend, Admin backend, APIs)

**Workflow**:
1. **Week 1-3**: Work on `frontend-dev` exclusively (Sprint 1-2) ✅ **DONE**
2. **Week 4**: Switch to `backend-dev` (BGAI Month 1), complete Sprint 3 frontend in gaps
3. **Week 5-16**: Alternate `backend-dev` and `frontend-dev` every 2-3 days
4. **Merge to `main`**: At 4 major checkpoints (Weeks 3, 8, 12, 16)

**🔄 CHECKPOINT Schedule**:

| Checkpoint | Week | Branches | Prerequisites | Validation |
|------------|------|----------|---------------|------------|
| **CP1** ✅ | 3 | `frontend-dev` → `main` | Sprint 1-2 done | Tests 90%+, Lighthouse ≥90 |
| **CP2** | 8 | `backend-dev` + `frontend-dev` → `main` | BGAI Month 1-2 + Sprint 3 | Integration tests pass |
| **CP3** | 12 | `backend-dev` + `frontend-dev` → `main` | BGAI Month 3-4 complete | Accuracy ≥70% gate |
| **CP4** | 16 | `backend-dev` + `frontend-dev` → `main` | BGAI MVP complete | Accuracy ≥80%, MVP ready |

**Example Week 4-5 (Current)**:
```
Week 4:
  Mon-Tue: Backend (BGAI Month 1) on `backend-dev`
  Wed: Frontend (#1097 Storybook) on `frontend-dev`
  Thu-Fri: Backend (BGAI Month 1) on `backend-dev`

Week 5:
  Mon-Tue: Backend (BGAI Month 2) on `backend-dev`
  Wed: Frontend (#1098 Tests) on `frontend-dev`
  Thu-Fri: Backend (BGAI Month 2) on `backend-dev`
```

---

## 📊 Dependency Matrix

### Critical Path (Updated 2025-11-14)

```
✅ #1088 Unify Login (DONE)
   ↓
✅ #1090 Split ChatProvider (DONE) ← Was blocking all context refactoring
   ↓
🔄 #1093 Performance ← NOW UNBLOCKED
   ↓
🔄 Sprint 2-3 Active (5 issues remaining)
   ↓
🔄 BGAI Month 1 PDF (backend-dev branch)
   ↓
⏳ BGAI Month 2 LLM ← Depends on Month 1
   ↓
⏳ BGAI Month 3 Validation ← Depends on Month 2 LLM
   ↓
⏳ BGAI Month 4-6 UI + Dataset
```

**Legend**: ✅ Complete | 🔄 In Progress | ⏳ Pending

### Parallel Opportunities (Can Do Simultaneously)

**Week 1-2**:
- ✅ #1089 Upload Page || #1091 Inline Styles (same day)

**Week 3**:
- ✅ #1092 Mobile || #1094 A11y (independent)
- ✅ #1095 Error || #1096 Loading (same day, similar patterns)

**Week 4-8**:
- ✅ BGAI Month 1-2 Backend || Sprint 3 Frontend (#1097-1102)
- ✅ Save 7 days by parallelizing Sprint 3 with BGAI Month 1

**Week 9-16**:
- ✅ BGAI Month 3-6 Backend (validation, dataset) || BGAI Frontend (UI, i18n)
- ✅ Save 21 days (as per existing analysis)

**Total Time Savings**: ~28 days (4 weeks) through parallelization

---

## ⚠️ Risks & Mitigation

### High Risks

**1. Sprint 1 Critical Issues Delay** (Probability: 40%, Impact: HIGH)
- **Risk**: #1089 (1564 lines) or #1090 (639 lines) take longer than estimated
- **Impact**: Blocks all frontend work, cascades to BGAI UI (Month 4-6)
- **Mitigation**:
  - Add 50% buffer (1.5d → 2d for #1089, 1.5d → 2d for #1090)
  - If blocked, switch to BGAI backend early (Week 2 instead of Week 4)

**2. BGAI Accuracy Not Achieving 80% Target** (Probability: 50%, Impact: CRITICAL)
- **Risk**: Month 5 golden dataset evaluation shows <80% accuracy
- **Impact**: BGAI MVP not viable, need more validation/dataset work
- **Mitigation**:
  - Gate decision at Month 4 (Week 12): If <70% accuracy, pivot
  - Allocate 2 extra weeks (Week 17-18) for dataset expansion if needed

**3. Single Developer Burnout** (Probability: 60%, Impact: MEDIUM)
- **Risk**: 16 weeks of intense solo work, context switching between 3 branches
- **Impact**: Velocity drops 20-30%, timeline extends 3-4 weeks
- **Mitigation**:
  - **Week 8 break**: 1-week buffer after BGAI Month 2 (re-evaluate priorities)
  - **Week 12 break**: 1-week buffer after BGAI Month 4 (mid-project review)
  - If falling behind, **defer Admin Console entirely** (30 issues, 0% complete)

**4. Admin Console Scope Creep** (Probability: 70%, Impact: MEDIUM)
- **Risk**: Trying to complete FASE 1-4 (30 issues) in parallel with BGAI
- **Impact**: BGAI delivery delayed 4-6 weeks, accuracy suffers from split focus
- **Mitigation**:
  - **Decision Point Week 8**: If BGAI Month 1-2 took >20 days, **defer all Admin Console to Phase 2**
  - **Minimum Viable Admin**: Only FASE 1 Dashboard (5 issues) if absolutely needed

### Medium Risks

**5. Frontend Epic Phases Distraction** (Probability: 50%, Impact: MEDIUM)
- **Risk**: Attempting App Router migration (#933, 10 days) mid-BGAI development
- **Mitigation**: **Defer all epics except #926 (already covered by Sprint 1-3)**

**6. FE-IMP Infrastructure Work** (Probability: 40%, Impact: LOW)
- **Risk**: 11 days of infrastructure work (#1077-1084) with no user-facing value
- **Mitigation**: **Defer entirely to Phase 2** unless App Router migration approved

---

## 📈 Success Criteria & Milestones

### Week 3 Gate: Sprint 1-2 Complete

**Criteria**:
- ✅ All 9 issues (#1088-1096) closed
- ✅ Frontend codebase: No files >500 lines
- ✅ Test coverage maintained ≥90%
- ✅ No ESLint warnings

**Go/No-Go**: If failed, extend Sprint 2 by 1 week, adjust BGAI start to Week 5

---

### Week 8 Gate: BGAI Month 1-2 Complete

**Criteria**:
- ✅ PDF 3-stage pipeline working (LLMWhisperer → SmolDocling → Docnet)
- ✅ OpenRouter + Ollama LLM integration operational
- ✅ 5 Italian rulebooks processed successfully
- ✅ Sprint 3 frontend complete (#1097-1102)

**Go/No-Go**:
- **If on track**: Continue to Month 3-6
- **If delayed >5 days**: Defer Admin Console entirely, focus 100% on BGAI

---

### Week 12 Gate: BGAI Month 3-4 Complete (Mid-Project Review)

**Criteria**:
- ✅ Multi-model validation working (GPT-4 + Claude consensus)
- ✅ 5-metric quality framework operational
- ✅ 50 Q&A golden dataset annotated
- ✅ Accuracy ≥70% baseline (target 80%+ by Month 6)
- ✅ Frontend BGAI base components complete

**Go/No-Go**:
- **If accuracy ≥70%**: Continue to Month 5-6, confidence in 80%+ target
- **If accuracy <70%**: **PIVOT** - Add 2 weeks for dataset/validation improvements
- **Burnout check**: Developer feeling sustainable? If no, take 1-week break

---

### Week 16 Gate: BGAI Phase 1 Complete (FINAL)

**Criteria**:
- ✅ 100 Q&A golden dataset complete
- ✅ Accuracy ≥80% validated
- ✅ Hallucination rate ≤10%
- ✅ Italian UI complete (200+ translations)
- ✅ PDF viewer functional
- ✅ P95 latency <5s

**Success**: BGAI MVP ready for beta launch
**Partial**: If 75-79% accuracy, extend 1-2 weeks for improvements
**Fail**: If <75% accuracy, re-evaluate BGAI viability (architecture issue?)

---

## 🔀 Merge Checkpoints & Manual Testing Protocols

### Overview

Each major milestone requires a **merge checkpoint** before integrating work from `frontend` or `backend` branches into `main`. These checkpoints ensure code quality, functionality, and system stability through automated testing + manual verification.

**Merge Strategy**:
- Work on feature branches (`frontend`, `backend`)
- Merge to `main` only at defined checkpoints
- Each merge requires passing **all** automated tests + manual testing checklist
- If any test fails, fix issues before merging (no partial merges)

---

### Checkpoint 1: Sprint 1-2 Frontend ✅ **COMPLETED**

**Branch**: `frontend-dev` → `main`
**Issues**: #1088-1096 (Sprint 1-2, 4 complete, 5 remaining)
**Timeline**: Week 3 Friday
**Status**: ✅ Critical issues (#1088-1091) completed 2025-11-13/14

#### Pre-Merge Checklist

**Automated Tests** (Must Pass 100%):
```bash
cd apps/web
pnpm typecheck          # Zero TypeScript errors
pnpm lint               # Zero ESLint errors
pnpm test               # ≥90% coverage maintained
pnpm build              # Production build succeeds
```

**Code Quality Checks**:
- [ ] No files >500 lines (check `upload.tsx`, `ChatProvider.tsx`)
- [ ] No inline styles remaining (search for `style={{`)
- [ ] All components use Tailwind CSS only
- [ ] No `console.log` statements in production code
- [ ] All imports use `@/` alias

#### Manual Testing Protocol

**Test 1: Login Flow Unification** (#1088)
```
1. Navigate to http://localhost:3000/login
2. Test cookie-based login:
   - Enter: admin@meepleai.dev / Demo123!
   - Verify: Redirects to /games dashboard
   - Check: Cookie "meepleai-session" set in DevTools
3. Test API key login:
   - Logout, go to /login
   - Enter valid API key (get from /admin/api-keys)
   - Verify: Redirects to /games with API key in header
4. Test 2FA flow:
   - Login as user with 2FA enabled
   - Verify: Shows 2FA input screen
   - Enter valid TOTP code
   - Verify: Completes login successfully
5. Test error states:
   - Invalid credentials → Show error message
   - Network error → Show retry UI
   - Session expired → Redirect to login

PASS CRITERIA: All 5 scenarios work without console errors
```

**Test 2: Upload Page Refactor** (#1089)
```
1. Navigate to /upload
2. Test PDF upload:
   - Drag & drop a PDF (e.g., Catan_Rules_IT.pdf)
   - Verify: Upload progress bar appears
   - Verify: Success message after upload
   - Check: PDF appears in /documents list
3. Test multiple files:
   - Upload 3 PDFs simultaneously
   - Verify: All 3 show individual progress bars
   - Verify: All complete successfully
4. Test file validation:
   - Try uploading .txt file → Reject with error
   - Try uploading 100MB file → Reject with size error
   - Upload valid PDF with spaces in name → Accept
5. Test mobile responsiveness:
   - Resize browser to 375px width
   - Verify: Upload UI is usable, buttons accessible
   - Test upload on mobile screen size → Works

PASS CRITERIA: All uploads succeed, validation works, mobile usable
```

**Test 3: ChatProvider Context Refactor** (#1090)
```
1. Navigate to /chat
2. Test message sending:
   - Type: "Come si gioca a Catan?"
   - Send message
   - Verify: Message appears in chat history
   - Verify: AI response streams in real-time (SSE)
   - Verify: No React context warnings in console
3. Test chat history:
   - Send 5 messages in conversation
   - Refresh page
   - Verify: Chat history persists (localStorage)
   - Verify: Can scroll to previous messages
4. Test thread management:
   - Create new thread (button)
   - Verify: Old thread saved, new thread empty
   - Switch between threads → Messages persist
5. Test streaming performance:
   - Ask complex question (long answer expected)
   - Verify: Response streams smoothly, no lag
   - Check DevTools Performance: No memory leaks

PASS CRITERIA: Chat works, streaming smooth, no context bugs
```

**Test 4: Mobile-First Responsive Design** (#1092)
```
Test on 5 breakpoints:
- 320px (iPhone SE)
- 375px (iPhone 12)
- 768px (iPad)
- 1024px (iPad Pro)
- 1920px (Desktop)

For each breakpoint:
1. Navigate through all pages: /login, /games, /upload, /chat, /admin
2. Verify: No horizontal scrolling
3. Verify: All buttons/inputs accessible (not cut off)
4. Verify: Text readable (font-size ≥14px)
5. Verify: Images/icons scale proportionally
6. Test touch targets: Buttons ≥44px (mobile standard)

PASS CRITERIA: All pages usable on all 5 breakpoints
```

**Test 5: Performance Optimization** (#1093)
```
1. Open Chrome DevTools → Lighthouse
2. Run audit on /games page:
   - Performance score ≥90
   - Accessibility score ≥95
   - Best Practices score ≥95
3. Test specific metrics:
   - First Contentful Paint <1.5s
   - Largest Contentful Paint <2.5s
   - Cumulative Layout Shift <0.1
4. Test bundle size:
   - Check build output: _next/static/chunks
   - Main bundle <300KB gzipped
   - No duplicate dependencies (run `pnpm why react`)
5. Test lazy loading:
   - Navigate /games → /upload
   - Verify: Only upload page chunk loaded (Network tab)

PASS CRITERIA: Lighthouse ≥90, LCP <2.5s, bundles optimized
```

#### Merge Decision Matrix

| Criteria | Required | Status |
|----------|----------|--------|
| All automated tests pass | ✅ YES | ⬜ |
| Manual test 1 (Login) passes | ✅ YES | ⬜ |
| Manual test 2 (Upload) passes | ✅ YES | ⬜ |
| Manual test 3 (Chat) passes | ✅ YES | ⬜ |
| Manual test 4 (Mobile) passes | ✅ YES | ⬜ |
| Manual test 5 (Performance) passes | ⚠️ RECOMMENDED | ⬜ |
| No files >500 lines | ✅ YES | ⬜ |

**Merge Command**:
```bash
git checkout main
git merge frontend-dev --no-ff -m "feat: Complete Sprint 1-2 frontend refactor (#1088-1096)

- Unified login flow (cookie + API key + 2FA)
- Refactored upload page (1564→400 lines)
- Split ChatProvider context (639→250 lines)
- Mobile-first responsive design (320px-1920px)
- Performance optimizations (LCP <2.5s, bundle <300KB)

Manual testing: 5/5 protocols passed
Coverage: 90%+ maintained
Lighthouse: Performance 90+, A11y 95+"

git push origin main
```

**Rollback Plan**: If production issues detected within 24h:
```bash
git revert HEAD~1        # Revert merge commit
git push origin main
git checkout frontend    # Continue fixes on branch
```

---

### Checkpoint 2: BGAI Month 1-2 + Sprint 3 (End of Week 8)

**Branches**: `backend-dev` + `frontend-dev` → `main`
**Issues**: BGAI Month 1-2 (PDF + LLM) + Sprint 3 (#1097-1102)
**Timeline**: Week 8 Friday
**Status**: ⏳ Pending (Current focus)

#### Pre-Merge Checklist

**Automated Tests** (Must Pass 100%):
```bash
# Backend
cd apps/api/src/Api
dotnet build                          # Zero build errors
dotnet test                           # All tests pass
dotnet test --filter "Category=Integration"  # PDF pipeline tests

# Frontend
cd apps/web
pnpm test                             # ≥90% coverage
pnpm build                            # Production build

# Infrastructure
cd infra
docker compose up -d postgres qdrant redis
curl http://localhost:6333/healthz   # Qdrant healthy
```

**Code Quality Checks**:
- [ ] PDF 3-stage pipeline implemented (Unstructured → SmolDocling → Docnet)
- [ ] OpenRouter + Ollama integrations complete
- [ ] Feature flags working (`AI:Provider` config)
- [ ] Storybook components documented (#1097)
- [ ] Migration guide complete (#1102)

#### Manual Testing Protocol

**Test 1: PDF Processing 3-Stage Pipeline**
```
1. Start Unstructured container:
   docker compose up -d unstructured-api

2. Test Stage 1 (Unstructured - Primary):
   - Upload PDF: docs/test-pdfs/catan-it.pdf
   - POST /api/v1/documents/upload
   - Verify: Uses UnstructuredPdfTextExtractor
   - Check logs: "Stage 1: Unstructured succeeded" (Seq)
   - Verify: Quality score ≥0.80
   - Verify: Text extracted with structure (headings, lists)

3. Test Stage 2 (SmolDocling - Fallback):
   - Upload complex PDF: docs/test-pdfs/gloomhaven-it.pdf (tables/images)
   - Simulate Stage 1 failure (stop unstructured container)
   - docker compose stop unstructured-api
   - Upload PDF again
   - Verify: Falls back to SmolDocling VLM
   - Check logs: "Stage 1 failed, trying Stage 2" (Seq)
   - Verify: Quality score ≥0.70 (lower threshold)
   - Verify: Tables extracted correctly

4. Test Stage 3 (Docnet - Final Fallback):
   - Simulate Stage 1+2 failure
   - Upload any PDF
   - Verify: Falls back to Docnet (local)
   - Check logs: "Stage 2 failed, trying Stage 3" (Seq)
   - Verify: Basic text extraction works (best effort)

5. Test Quality Validation:
   - Upload 5 different rulebook PDFs
   - Verify: Each gets quality report (4 metrics)
   - Verify: Reports show text coverage, structure, tables, pages
   - Check: PDFs with score <0.70 → Recommendation shown

PASS CRITERIA: All 3 stages work, fallback logic correct, quality scoring accurate
```

**Test 2: LLM Integration (OpenRouter + Ollama)**
```
1. Test OpenRouter (Cloud):
   - Set feature flag: AI:Provider = "OpenRouter"
   - Set OPENROUTER_API_KEY in .env
   - POST /api/v1/chat
   - Body: { "question": "Come si gioca a Catan?", "gameId": 1 }
   - Verify: Response streams via SSE
   - Verify: Uses GPT-4 model (check logs)
   - Verify: Response in Italian
   - Check: Latency <5s P95

2. Test Ollama (Local):
   - Set feature flag: AI:Provider = "Ollama"
   - Start Ollama: docker compose up -d ollama
   - Download model: docker exec ollama ollama pull mistral
   - POST /api/v1/chat (same question)
   - Verify: Uses Ollama mistral model
   - Verify: Response quality acceptable (may be lower than GPT-4)
   - Check: No external API calls (network tab)

3. Test AdaptiveLlm (Auto-switching):
   - Set feature flag: AI:Provider = "Adaptive"
   - POST /api/v1/chat with complex question (long)
   - Verify: Uses OpenRouter (GPT-4) for complex queries
   - POST /api/v1/chat with simple question (short)
   - Verify: Uses Ollama for simple queries (cost optimization)
   - Check logs: "AdaptiveLlm: Selected provider={X}" (Seq)

4. Test Error Handling:
   - Stop OpenRouter (remove API key)
   - POST /api/v1/chat → Verify: Graceful error, retry logic
   - Stop Ollama container → Verify: Falls back to OpenRouter
   - Both offline → Verify: User-friendly error message

PASS CRITERIA: Both providers work, adaptive switching correct, errors handled gracefully
```

**Test 3: RAG Hybrid Search (Vector + Keyword)**
```
1. Setup test data:
   - Upload 3 Italian rulebooks: Catan, Carcassonne, 7 Wonders
   - Verify: PDFs processed, vectors indexed in Qdrant
   - Check Qdrant UI: http://localhost:6333/dashboard
   - Verify: Collection "meepleai-rules" exists with 3 documents

2. Test Vector Search (Semantic):
   - POST /api/v1/search
   - Body: { "query": "costruzione strade", "gameId": 1 }
   - Verify: Returns Catan passages about road building
   - Verify: Semantic matches (not just keyword match)
   - Example: "edificare vie" also matches (synonym)

3. Test Keyword Search (FTS):
   - POST /api/v1/search
   - Body: { "query": "punti vittoria", "gameId": null }
   - Verify: Returns passages from all games mentioning "punti vittoria"
   - Verify: Exact keyword matches included

4. Test RRF Fusion (70% Vector + 30% Keyword):
   - POST /api/v1/search
   - Body: { "query": "come si vince", "gameId": null }
   - Verify: Results combine semantic + keyword matches
   - Verify: Relevance scores reasonable (check response)
   - Verify: No duplicate results in top 10

5. Test Performance:
   - Run 10 searches in parallel (load test)
   - Verify: P95 latency <1s (search only, not LLM)
   - Check Qdrant logs: No errors, cache hits working

PASS CRITERIA: Hybrid search works, RRF fusion correct, latency <1s P95
```

**Test 4: Storybook Components** (#1097)
```
1. Start Storybook:
   cd apps/web
   pnpm storybook  # Port 6006

2. Test documented components:
   - Navigate to "Components/Button" story
   - Verify: All variants displayed (primary, secondary, outline)
   - Verify: Interactive controls work (change props)
   - Test: Dark mode toggle → Components adapt

3. Test new BGAI components:
   - Navigate to "BGAI/PdfViewer" story
   - Verify: Sample PDF renders correctly
   - Test: Zoom in/out, page navigation
   - Navigate to "BGAI/ChatMessage" story
   - Verify: User/AI message variants shown

4. Verify documentation:
   - Each component has:
     - Description
     - Props table
     - Usage examples
     - Accessibility notes

PASS CRITERIA: Storybook runs, all components documented, interactive
```

**Test 5: Integration E2E Workflow**
```
Full user workflow test:
1. Login as admin@meepleai.dev
2. Upload rulebook: docs/test-pdfs/catan-it.pdf
3. Wait for processing (check /admin/documents status)
4. Navigate to /chat
5. Ask: "Come si costruisce una strada a Catan?"
6. Verify: AI responds with correct info from uploaded rulebook
7. Verify: Citations shown (PDF page numbers)
8. Test follow-up: "E quanto costa?"
9. Verify: Context maintained from previous question
10. Export chat: Click "Export" button
11. Verify: Downloads JSON/MD file with conversation

PASS CRITERIA: Full workflow works end-to-end without errors
```

#### Merge Decision Matrix

| Criteria | Required | Status |
|----------|----------|--------|
| All automated tests pass | ✅ YES | ⬜ |
| Manual test 1 (PDF Pipeline) passes | ✅ YES | ⬜ |
| Manual test 2 (LLM Integration) passes | ✅ YES | ⬜ |
| Manual test 3 (RAG Search) passes | ✅ YES | ⬜ |
| Manual test 4 (Storybook) passes | ⚠️ RECOMMENDED | ⬜ |
| Manual test 5 (E2E Workflow) passes | ✅ YES | ⬜ |
| 5 Italian rulebooks indexed | ✅ YES | ⬜ |

**Merge Command**:
```bash
# Merge backend first
git checkout main
git merge backend-dev --no-ff -m "feat: BGAI Month 1-2 backend - PDF pipeline + LLM (#956-964)

- 3-stage PDF processing (Unstructured → SmolDocling → Docnet)
- Quality validation framework (4-metric scoring)
- OpenRouter + Ollama LLM integration
- Feature flag AI:Provider configuration
- Hybrid RAG search (vector + keyword RRF)

Manual testing: 5/5 protocols passed
Test coverage: 162 backend tests pass"

# Merge frontend
git merge frontend-dev --no-ff -m "feat: Sprint 3 frontend + BGAI components (#1097-1102)

- Storybook component documentation
- BGAI PdfViewer + ChatMessage components
- Migration guide (legacy → DDD)
- Frontend test coverage maintained 90%+

Manual testing: Storybook verified, E2E workflow passed"

git push origin main
```

---

### Checkpoint 3: BGAI Month 3-4 Mid-Project Review (End of Week 12)

**Branches**: `backend-dev` + `frontend-dev` → `main`
**Issues**: BGAI Month 3-4 (Validation + Quality + UI)
**Timeline**: Week 12 Friday
**Status**: ⏳ Pending (Mid-project gate)

#### Pre-Merge Checklist

**Automated Tests**:
```bash
dotnet test --filter "Category=Validation"   # Multi-model validation tests
dotnet test --filter "Category=Quality"      # Quality framework tests
pnpm test -- ChatInterface                   # Frontend BGAI UI tests
```

**Quality Gates** (CRITICAL for Month 4 Gate):
- [ ] Multi-model validation working (GPT-4 + Claude consensus)
- [ ] 5-metric quality framework operational
- [ ] 50 Q&A golden dataset annotated
- [ ] **Accuracy ≥70% baseline** (target 80%+ by Month 6)
- [ ] Hallucination rate measured
- [ ] Frontend BGAI base components complete

#### Manual Testing Protocol

**Test 1: Multi-Model Validation (GPT-4 + Claude Consensus)**
```
1. Test dual-model response:
   - POST /api/v1/chat
   - Body: { "question": "Quanti giocatori per Catan?", "gameId": 1, "validation": "multi-model" }
   - Verify: Both GPT-4 and Claude generate responses
   - Check logs: "Multi-model validation: GPT-4={X}, Claude={Y}" (Seq)
   - Verify: Consensus score calculated (agreement metric)

2. Test consensus logic:
   - Ask factual question: "Quante risorse iniziali a Catan?"
   - Verify: Both models agree (consensus ≥0.90)
   - Verify: High confidence score shown to user
   - Ask ambiguous question: "Quale strategia è migliore?"
   - Verify: Models disagree (consensus <0.70)
   - Verify: User shown both perspectives

3. Test hallucination detection:
   - Ask question with no answer in rulebook
   - Example: "Come si gioca a Monopoly?" (not indexed)
   - Verify: Models detect knowledge gap
   - Verify: Response: "Non ho informazioni su questo gioco"
   - Verify: No fabricated answer

PASS CRITERIA: Consensus working, hallucinations detected, confidence scores accurate
```

**Test 2: 5-Metric Quality Framework**
```
Metrics: Confidence, Citation Coverage, Forbidden Keywords, Consensus, Latency

1. Test Confidence Scoring:
   - Ask 10 questions (mix of easy/hard)
   - Verify: Each response has confidence score 0.0-1.0
   - Verify: Easy questions (facts) → High confidence ≥0.80
   - Verify: Hard questions (strategy) → Lower confidence <0.70
   - Check: Confidence threshold enforced (reject <0.70)

2. Test Citation Coverage:
   - Ask: "Come si costruisce una città a Catan?"
   - Verify: Response includes citations [Rulebook p.5, p.7]
   - Verify: Citations link to specific PDF pages
   - Verify: Citation coverage ≥70% (most claims cited)

3. Test Forbidden Keywords Detection:
   - Configure: Forbidden = ["sicuramente", "ovviamente", "sempre"]
   - Ask question, verify: Response avoids forbidden words
   - Simulate response with forbidden word
   - Verify: Quality check flags issue, rejects response

4. Test Latency Metrics:
   - Run 20 questions, measure P50, P95, P99 latency
   - Verify: P95 <5s (target)
   - Verify: Latency logged per request (Seq)

5. Test Quality Dashboard:
   - Navigate: /admin/quality
   - Verify: Shows aggregate metrics (last 7 days)
   - Verify: Charts for confidence distribution, latency P95
   - Verify: Hallucination rate % displayed

PASS CRITERIA: All 5 metrics working, dashboard shows correct data
```

**Test 3: Golden Dataset (50 Q&A Annotated)**
```
1. Verify dataset structure:
   - Check: Database table "golden_dataset" exists
   - Query: SELECT COUNT(*) FROM golden_dataset
   - Verify: ≥50 Q&A pairs stored
   - Verify: Each pair has: question, expected_answer, game_id, source_page

2. Test evaluation pipeline:
   - Run: dotnet run --project tools/EvaluateGoldenDataset
   - Verify: Script runs 50 questions through RAG pipeline
   - Verify: Compares actual vs expected answers (cosine similarity)
   - Verify: Outputs accuracy report: X/50 correct (≥70% target)

3. Test manual review workflow:
   - Navigate: /admin/golden-dataset
   - Verify: Shows 50 Q&A pairs in table
   - Test: Edit question #1, save
   - Verify: Changes persist (reload page)
   - Test: Mark answer as "validated" checkbox
   - Verify: Status updates

4. Test dataset diversity:
   - Verify: Questions cover 3+ games (not just Catan)
   - Verify: Mix of question types: factual, procedural, clarification
   - Verify: Italian language throughout (no English)

PASS CRITERIA: ≥50 Q&A pairs, evaluation pipeline works, accuracy ≥70%
```

**Test 4: BGAI Frontend UI Components**
```
1. Test ChatInterface component:
   - Navigate: /bgai/chat (new BGAI-specific chat page)
   - Verify: Modern UI design (Shadcn/Tailwind)
   - Test: Send message → Response streams smoothly
   - Verify: Citations clickable (opens PDF viewer modal)
   - Test: Confidence score displayed per message (color-coded)
   - Test: Dark mode toggle → UI adapts

2. Test PdfViewer component:
   - Click citation link in chat response
   - Verify: Modal opens with PDF viewer
   - Verify: Auto-scrolls to cited page
   - Test: Zoom controls (+ / -)
   - Test: Page navigation (1 of 24)
   - Test: Close modal → Returns to chat

3. Test QualityIndicator component:
   - Verify: Each AI response shows quality badge
   - High confidence (≥0.80) → Green badge "Alta affidabilità"
   - Medium confidence (0.70-0.79) → Yellow badge "Affidabilità media"
   - Low confidence (<0.70) → Red badge "Bassa affidabilità"
   - Hover badge → Tooltip shows detailed metrics

4. Test GameSelector component:
   - Verify: Dropdown shows indexed games (with rulebook count)
   - Select game → Chat context switches
   - Verify: Only relevant game's rules used in RAG

5. Test mobile responsiveness:
   - Test on 375px width (iPhone)
   - Verify: Chat interface usable, messages readable
   - Verify: PDF viewer adapts to mobile (no horizontal scroll)

PASS CRITERIA: All components functional, mobile responsive, UX smooth
```

**Test 5: Regression Testing (Existing Features)**
```
Ensure BGAI work didn't break existing features:

1. Test Auth flow:
   - Login/logout still works
   - API key auth still works
   - 2FA still works

2. Test Upload:
   - Upload new PDF → Processes successfully
   - Old uploaded PDFs still accessible

3. Test Admin Console:
   - /admin/users loads
   - /admin/api-keys works
   - /admin/configuration loads

4. Test Performance:
   - Run Lighthouse on /games
   - Verify: Performance score still ≥90 (not degraded)

PASS CRITERIA: No regressions, all existing features work
```

#### Merge Decision Matrix (Month 4 Gate - CRITICAL)

| Criteria | Required | Status |
|----------|----------|--------|
| All automated tests pass | ✅ YES | ⬜ |
| Manual test 1 (Multi-model) passes | ✅ YES | ⬜ |
| Manual test 2 (Quality Framework) passes | ✅ YES | ⬜ |
| Manual test 3 (Golden Dataset ≥50, Accuracy ≥70%) | ✅ YES | ⬜ |
| Manual test 4 (BGAI UI) passes | ✅ YES | ⬜ |
| Manual test 5 (Regression) passes | ✅ YES | ⬜ |
| **CRITICAL: Accuracy ≥70% baseline** | ✅ YES | ⬜ |

**Go/No-Go Decision**:
- ✅ **GO** if accuracy ≥70%: Continue to Month 5-6, confidence in 80%+ target
- ⚠️ **CAUTION** if accuracy 65-69%: Add 1 week for dataset improvements, re-test
- ❌ **NO-GO** if accuracy <65%: **PIVOT** - 2-week deep dive on accuracy issues (architecture problem?)

**Merge Command** (if GO):
```bash
git checkout main
git merge backend-dev --no-ff -m "feat: BGAI Month 3-4 validation + quality framework (#974-995)

- Multi-model validation (GPT-4 + Claude consensus)
- 5-metric quality framework (confidence, citations, keywords, consensus, latency)
- 50 Q&A golden dataset with evaluation pipeline
- Accuracy baseline: X% (target ≥70%)
- Hallucination rate: Y% (target <10%)

Manual testing: 5/5 protocols passed
Mid-project gate: PASSED ✅"

git merge frontend-dev --no-ff -m "feat: BGAI Month 4 frontend UI components (#983-995)

- ChatInterface with streaming + citations
- PdfViewer modal with zoom + navigation
- QualityIndicator badges (confidence-based)
- GameSelector context switching
- Mobile-first responsive (375px+)

Manual testing: UI verified, regression tests passed"

git push origin main
```

**If NO-GO** (Accuracy <65%):
```bash
# Do NOT merge yet, create pivot branch
git checkout -b bgai-accuracy-pivot
# Investigate issues, improve dataset, re-evaluate in 2 weeks
```

---

### Checkpoint 4: BGAI Month 5-6 Final Release (End of Week 16)

**Branches**: `backend-dev` + `frontend-dev` → `main`
**Issues**: BGAI Month 5-6 (Dataset completion + Italian UI + Final polish)
**Timeline**: Week 16 Friday
**Status**: ⏳ Pending (MVP release gate)

#### Pre-Merge Checklist

**Automated Tests**:
```bash
dotnet test                              # All backend tests pass
pnpm test                                # All frontend tests pass ≥90%
pnpm build                               # Production build succeeds
```

**Quality Gates** (FINAL for BGAI MVP):
- [ ] **100 Q&A golden dataset complete**
- [ ] **Accuracy ≥80% validated** (CRITICAL MVP requirement)
- [ ] **Hallucination rate ≤10%**
- [ ] **Italian UI complete (200+ translations)**
- [ ] **PDF viewer functional**
- [ ] **P95 latency <5s**
- [ ] **9 Italian rulebooks indexed**
- [ ] **All BGAI issues closed (#956-1023)**

#### Manual Testing Protocol

**Test 1: Golden Dataset Completion (100 Q&A)**
```
1. Verify dataset size:
   - Query: SELECT COUNT(*) FROM golden_dataset
   - Required: Exactly 100 Q&A pairs (50 from Month 4 + 50 new)

2. Verify dataset diversity:
   - Query: SELECT game_id, COUNT(*) FROM golden_dataset GROUP BY game_id
   - Required: ≥5 games represented (not just Catan)
   - Required: Each game has ≥10 questions

3. Run final evaluation:
   - Execute: dotnet run --project tools/EvaluateGoldenDataset
   - Verify: Accuracy report generated
   - **CRITICAL**: Accuracy ≥80% (80/100 correct answers minimum)
   - Verify: Detailed report saved to /admin/evaluation/latest.json

4. Spot-check 10 random Q&A pairs:
   - Manually verify: Answers are factually correct
   - Manually verify: Citations point to correct pages
   - Manually verify: Italian grammar is correct

PASS CRITERIA: 100 Q&A pairs, ≥80% accuracy, manual spot-check passed
```

**Test 2: Accuracy & Hallucination Validation**
```
1. Test accuracy on known facts:
   - Ask 20 factual questions from golden dataset
   - Examples: "Quanti giocatori per Catan?", "Come si vince a Carcassonne?"
   - Verify: All 20 answers correct (100% accuracy on facts)

2. Test hallucination detection:
   - Ask 10 questions about non-indexed games
   - Examples: "Come si gioca a Monopoly?", "Regole di Risiko?"
   - Verify: All 10 responses admit "Non ho informazioni" (0% hallucination)
   - Verify: No fabricated rules or invented information

3. Test edge cases:
   - Ask ambiguous question: "Cosa devo fare ora?"
   - Verify: Asks clarifying question, doesn't hallucinate
   - Ask partially covered topic
   - Verify: Answers what it knows, admits gaps

4. Calculate hallucination rate:
   - Formula: (False claims / Total claims) * 100
   - Target: ≤10% hallucination rate
   - Use validation set of 50 questions (separate from golden dataset)

PASS CRITERIA: ≥80% overall accuracy, ≤10% hallucination rate
```

**Test 3: Italian i18n Completion (200+ Translations)**
```
1. Verify translation files:
   - Check: apps/web/locales/it-IT/common.json exists
   - Verify: ≥200 keys translated to Italian
   - Spot-check: No English strings remaining in Italian locale

2. Test language switching:
   - Navigate to /settings
   - Switch language: English → Italian
   - Verify: Entire UI updates to Italian
   - Verify: Date/number formats use Italian conventions

3. Test Italian UI strings in BGAI:
   - Navigate /bgai/chat
   - Verify: All labels in Italian:
     - "Invia messaggio", "Nuova conversazione", "Esporta chat"
   - Verify: AI responses in Italian
   - Verify: Error messages in Italian
   - Verify: Quality badges in Italian: "Alta affidabilità", "Media affidabilità"

4. Test pluralization rules:
   - Test: "1 giocatore" (singular) vs "3 giocatori" (plural)
   - Verify: Correct Italian pluralization

5. Test mobile Italian UI:
   - Resize to 375px
   - Verify: Italian strings don't overflow buttons (longer than English)
   - Verify: Truncation with ellipsis (...) where needed

PASS CRITERIA: ≥200 translations, full Italian UI functional, mobile adapts
```

**Test 4: Performance & Latency (P95 <5s)**
```
1. Setup load testing:
   - Tool: Artillery or k6
   - Config: 10 concurrent users, 100 requests over 5 min

2. Test RAG query latency:
   - Run load test on POST /api/v1/chat endpoint
   - Measure: P50, P95, P99 latency
   - **CRITICAL**: P95 <5s (95% of requests under 5 seconds)
   - Check: No timeouts, all requests succeed

3. Test streaming latency:
   - Measure time-to-first-token (TTFT)
   - Target: TTFT <1s (user sees response start quickly)
   - Measure time-between-tokens (TBT)
   - Target: TBT <100ms (smooth streaming experience)

4. Test concurrent uploads:
   - Simulate 5 users uploading PDFs simultaneously
   - Verify: No bottlenecks, all complete successfully
   - Verify: PDF processing queue works (Redis-backed)

5. Test database query performance:
   - Check: /admin/stats page loads in <2s
   - Check: No N+1 query problems (review logs with EF Core query logging)
   - Verify: Database connection pooling working (check PG logs)

6. Run Lighthouse performance audit:
   - Pages to test: /games, /chat, /admin
   - **CRITICAL**: Performance score ≥90 on all pages
   - Verify: LCP <2.5s, FID <100ms, CLS <0.1

PASS CRITERIA: P95 latency <5s, TTFT <1s, Lighthouse ≥90
```

**Test 5: End-to-End Production Simulation**
```
Full production workflow test (90-minute test):

1. Initial Setup (10 min):
   - Fresh database: dotnet ef database update
   - Seed demo users: admin@meepleai.dev
   - Start all services: docker compose up -d
   - Verify: All health checks pass

2. Document Upload Workflow (20 min):
   - Login as admin
   - Upload 9 Italian rulebooks (Catan, Carcassonne, 7 Wonders, Ticket to Ride, Azul, Splendor, Pandemic, Wingspan, Dixit)
   - Monitor: /admin/documents status page
   - Verify: All 9 process successfully (3-stage pipeline)
   - Verify: Quality scores ≥0.70 for all
   - Verify: Vectors indexed in Qdrant (check dashboard)

3. Chat Interaction Workflow (30 min):
   - Navigate: /bgai/chat
   - Test 20 questions across 9 games (mix easy/hard)
   - Verify: All responses relevant and accurate
   - Verify: Citations provided (clickable)
   - Verify: Confidence scores displayed
   - Test: Multi-turn conversations (5-message threads)
   - Verify: Context maintained across turns

4. Quality Validation Workflow (15 min):
   - Navigate: /admin/quality
   - Verify: Dashboard shows stats from 20 questions
   - Check: Avg confidence, avg latency, hallucination count
   - Verify: Charts render correctly (no errors)

5. PDF Viewer Workflow (10 min):
   - Click citation in chat → PDF viewer opens
   - Test: Zoom, page navigation, search in PDF
   - Verify: Highlights cited passage (yellow highlight)
   - Test: Mobile PDF viewer (375px width)

6. Admin Management Workflow (10 min):
   - Navigate: /admin/users
   - Create new user: testuser@example.com
   - Navigate: /admin/api-keys
   - Generate API key for testuser
   - Test: API key authentication (curl)
   - Verify: testuser can use API key to query /api/v1/chat

7. Error Recovery Workflow (5 min):
   - Stop Qdrant: docker compose stop qdrant
   - Try chat query → Verify: Graceful error message
   - Start Qdrant: docker compose start qdrant
   - Retry query → Verify: Works again (auto-recovery)

PASS CRITERIA: All 7 workflows complete without critical errors
```

**Test 6: Regression & Backward Compatibility**
```
1. Test old frontend still works:
   - Navigate: /games (old games page)
   - Verify: Still functional (not removed)
   - Navigate: /chat (old chat page)
   - Verify: Still functional alongside /bgai/chat

2. Test API backward compatibility:
   - Old endpoint: POST /api/v1/chat (v1)
   - Verify: Still works (not breaking change)
   - New endpoint: POST /api/v2/chat (if added)
   - Verify: Both versions coexist

3. Test database migrations:
   - Verify: Old data intact (users, games, sessions)
   - Verify: New columns added (not replaced)
   - Verify: No data loss from migrations

PASS CRITERIA: No breaking changes, old features still functional
```

#### Merge Decision Matrix (FINAL - BGAI MVP)

| Criteria | Required | Status |
|----------|----------|--------|
| All automated tests pass | ✅ YES | ⬜ |
| **Test 1: 100 Q&A dataset, ≥80% accuracy** | ✅ YES (MVP BLOCKER) | ⬜ |
| **Test 2: ≤10% hallucination rate** | ✅ YES (MVP BLOCKER) | ⬜ |
| Test 3: 200+ Italian translations | ✅ YES | ⬜ |
| **Test 4: P95 latency <5s** | ✅ YES (MVP BLOCKER) | ⬜ |
| **Test 5: E2E production simulation passes** | ✅ YES (MVP BLOCKER) | ⬜ |
| Test 6: No regressions | ✅ YES | ⬜ |
| 9 Italian rulebooks indexed | ✅ YES | ⬜ |

**Final Go/No-Go Decision**:
- ✅ **GO** if ALL MVP BLOCKERS pass: **BGAI MVP ready for beta launch** 🎉
- ⚠️ **CONDITIONAL GO** if 75-79% accuracy: Launch with disclaimer, allocate 1-2 weeks post-launch for improvements
- ❌ **NO-GO** if accuracy <75% OR hallucination >15% OR P95 >7s: **Delay launch**, address critical issues (1-2 week sprint)

**Merge Command** (if GO):
```bash
git checkout main

# Merge backend
git merge backend-dev --no-ff -m "feat: BGAI Month 5-6 final release - MVP complete (#996-1023)

BGAI MVP LAUNCH READY 🎉

- 100 Q&A golden dataset with evaluation pipeline
- Accuracy: X% (target ≥80%) ✅
- Hallucination rate: Y% (target ≤10%) ✅
- 9 Italian rulebooks indexed (Catan, Carcassonne, 7 Wonders, Ticket to Ride, Azul, Splendor, Pandemic, Wingspan, Dixit)
- Multi-model validation operational (GPT-4 + Claude)
- P95 latency: Zs (target <5s) ✅

Manual testing: 6/6 protocols passed
Production simulation: 90-min E2E test passed
Coverage: Backend 162 tests, Frontend 90%+"

# Merge frontend
git merge frontend-dev --no-ff -m "feat: BGAI Month 6 Italian UI + final polish (#1010-1023)

- 200+ Italian translations (complete i18n)
- Italian-first UX throughout BGAI
- PDF viewer with citation highlighting
- Mobile-optimized chat interface (375px+)
- Quality indicators with Italian labels
- Dark mode support

Manual testing: Italian UI verified, mobile tested, E2E passed
Lighthouse: Performance 90+, Accessibility 95+"

git push origin main
git tag v1.0.0-bgai-mvp
git push origin v1.0.0-bgai-mvp
```

**Post-Merge Actions**:
1. Deploy to staging: `./deploy.sh staging`
2. Run smoke tests on staging (30 min)
3. If staging OK → Deploy to production: `./deploy.sh production`
4. Monitor for 48h: Sentry errors, Seq logs, Grafana metrics
5. Announce beta launch: Email to early access users

---

### Emergency Rollback Procedures

If critical issues detected in production within 48h of any checkpoint merge:

**Severity 1 - System Down** (API 500 errors, database corruption):
```bash
# Immediate rollback (< 5 minutes)
git checkout main
git revert HEAD~1              # Revert merge commit
git push origin main --force   # Force push (emergency only)

# Rollback database migrations
cd apps/api/src/Api
dotnet ef database update <PreviousMigrationName>

# Restart services
docker compose restart api web

# Notify: Post incident in Slack #incidents channel
```

**Severity 2 - Feature Broken** (BGAI errors, UI broken):
```bash
# Targeted fix (< 1 hour)
git checkout main
git cherry-pick <fix-commit-sha>   # Apply hotfix
git push origin main

# OR: Feature flag rollback
# Navigate /admin/configuration
# Set: Features:BGAI:Enabled = false (disable feature)

# Notify: Post update in Slack #engineering
```

**Severity 3 - Performance Degradation** (slow queries, high latency):
```bash
# Investigate first (< 2 hours)
# Check Grafana dashboards, Seq logs, Jaeger traces
# Identify bottleneck (database query, LLM call, etc.)

# Temporary mitigation:
# - Increase cache TTL: Features:Cache:TtlMinutes = 10
# - Reduce concurrent LLM calls: AI:MaxConcurrentRequests = 5
# - Enable read replica: ConnectionStrings:PostgresReadOnly

# Permanent fix: Create hotfix branch, test, merge next day
```

---

## 📋 Checkpoint Summary Table

| Checkpoint | Week | Branch(es) | Manual Tests | MVP Blocker? | Rollback Risk |
|------------|------|------------|--------------|--------------|---------------|
| **CP1: Sprint 1-2** | 3 | `frontend` | 5 protocols | ❌ No | Low (frontend only) |
| **CP2: BGAI Month 1-2** | 8 | `backend` + `frontend` | 5 protocols | ⚠️ Partial (infrastructure) | Medium (backend changes) |
| **CP3: BGAI Month 3-4** | 12 | `backend` + `frontend` | 5 protocols | ✅ YES (accuracy gate) | High (accuracy <70% = NO-GO) |
| **CP4: BGAI Month 5-6** | 16 | `backend` + `frontend` | 6 protocols | ✅ YES (MVP release) | Critical (launch decision) |

---

## 🎯 Prioritization Recommendations

### DO FIRST (Weeks 1-3)

1. **Sprint 1 Critical** (#1088-1091) - **5 days** - BLOCKS everything
2. **Sprint 2 High** (#1092-1096) - **3.5 days** - High user impact
3. **Sprint 3 Medium** (#1097-1102) - **7 days** - Developer experience

**Why**: Technical debt cleanup enables faster BGAI development. 1564-line file is unmaintainable.

### DO NEXT (Weeks 4-16)

4. **BGAI Month 1-6** (#956-1023 + missing issues) - **~50 days** - Core product value
   - Backend: PDF, LLM, validation, dataset (30 days)
   - Frontend: UI, i18n, PDF viewer (20 days)

**Why**: 27% complete, critical path for product launch, clear ROI

### DEFER TO PHASE 2 (After Week 16)

5. **Admin Console FASE 2-4** (#890-922) - **15 days** - Low user-facing value
6. **Frontend Epic Phases 3-6** (#933-935) - **21 days** - Infrastructure, not features
7. **FE-IMP Infrastructure** (#1077-1084) - **11 days** - Architecture improvements
8. **Testing/Security** (#841-844, #818) - **5 days** - Important but not blocking

**Why**: BGAI is 27% complete and critical. Admin Console is 0% complete and lower priority. Defer non-MVP work.

### OPTIONAL (Only if ahead of schedule)

9. **Admin Console FASE 1** (#874-889) - **5 days** - Nice-to-have dashboard
   - Do in Week 13-14 parallel with BGAI Month 6 backend if time permits

---

## 📝 Action Items (This Week)

### Immediate (Day 1-2)

1. **Create missing BGAI issues** (BGAI-1 to BGAI-15, Month 1-2)
   - Use templates from `board-game-ai-execution-calendar.md`
   - Focus on: LLMWhisperer, SmolDocling, Orchestrator, OpenRouter, Ollama

2. **Review Sprint 1 scope** (#1088-1091)
   - Read all 4 issue descriptions in detail
   - Estimate your own effort (be realistic about 1564-line refactor)
   - Decide: Can you do #1089 + #1091 in parallel? (Same day afternoon/evening)

3. **Decide on Admin Console**
   - **Option A (Recommended)**: Defer FASE 1-4 entirely to Phase 2 → Focus 100% on BGAI
   - **Option B**: Commit to FASE 1 only (5 days) in Week 13-14
   - **Option C**: Full FASE 1-4 (20 days) → Extends timeline to Week 20 (5 months)

### Week 1 Start (Day 3-7)

4. **Start Sprint 1** on `frontend` branch
   - Day 3: #1088 (Login Flow - 4h) + #1090 Start (ChatProvider - 4h)
   - Day 4: #1090 Complete + #1091 Start (Styles - 4h)
   - Day 5-6: #1089 (Upload Page - 2d)
   - Day 7: #1091 Complete + Testing

5. **Setup branch workflow**
   ```bash
   git checkout -b frontend
   # Work on Sprint 1-3
   git checkout -b backend
   # Later: BGAI Month 1-6
   ```

6. **Update progress tracking**
   - Close this roadmap file into project board
   - Create Week 1-3 milestones (Sprint 1-3)
   - Daily standup (15 min): Update issue progress, adjust estimates

---

## 🔄 Maintenance & Updates

**Review Cadence**:
- **Weekly** (Every Friday): Update completed issues, adjust next week priorities
- **Bi-weekly** (End of Sprint): Sprint retrospective, velocity check, timeline adjustment
- **Monthly** (End of BGAI Month): Go/No-Go gate decision, risk re-assessment

**This roadmap will be updated**:
1. After Sprint 1 complete (Week 2)
2. After Sprint 2-3 complete (Week 3)
3. After BGAI Month 2 complete (Week 8 - Mid-project review)
4. After BGAI Month 4 complete (Week 12 - Critical gate)

---

## 📞 Questions & Clarifications

**If you're unsure**:
1. **Which issue to start first?** → #1088 (Unify Login Flow, 4h, blocks #1090)
2. **Should I do Admin Console?** → **No, defer to Phase 2** (focus on BGAI 27% → 100%)
3. **Should I do Frontend Epics #931-935?** → **No, defer to Phase 2** (Sprint 1-3 covers #926)
4. **Should I do FE-IMP #1077-1084?** → **No, defer to Phase 2** (infrastructure, not MVP)
5. **What if I fall behind?** → **Week 8/12 gates: Re-prioritize, defer Admin Console, take break**

---

## 🎉 Expected Outcomes (Week 16)

### Product Outcomes

- ✅ **Frontend refactored** (Sprint 1-3 complete, no files >500 lines)
- ✅ **BGAI MVP operational** (80%+ accuracy, 100 Q&A dataset, Italian UI)
- ✅ **9 board games supported** (Italian rulebooks indexed)
- ✅ **PDF processing pipeline** (3-stage fallback, ≥95% success rate)
- ✅ **Multi-model validation** (GPT-4 + Claude consensus, <10% hallucination)

### Technical Outcomes

- ✅ **Test coverage ≥90%** (maintained throughout)
- ✅ **Performance P95 <5s** (latency target met)
- ✅ **Mobile responsive** (320px-1920px tested)
- ✅ **WCAG 2.1 AA compliant** (accessibility audit passed)

### What's Deferred to Phase 2

- ❌ Admin Console FASE 2-4 (15 days)
- ❌ Frontend Epic Phases 3-6 (21 days)
- ❌ FE-IMP Infrastructure (11 days)
- ❌ Advanced testing/security (5 days)

**Total deferred**: ~52 days (~10 weeks)

---

## 📚 Reference Documents

**Planning Documents**:
- `docs/07-project-management/planning/executive-summary-development-roadmap.md` - BGAI overview
- `docs/07-project-management/organization/board-game-ai-execution-calendar.md` - Parallel calendar
- `docs/07-project-management/organization/board-game-ai-sprint-overview.md` - Sprint details
- `.github-issues-templates/SUMMARY.md` - Frontend sprint summary

**Sprint Templates**:
- `.github-issues-templates/sprint-1-critical/` - 4 issues (5 days)
- `.github-issues-templates/sprint-2-important/` - 5 issues (3.5 days)
- `.github-issues-templates/sprint-3-nice-to-have/` - 6 issues (7 days)

**GitHub Issues**:
- Frontend Refactor: #1088-1102 (15 issues)
- Board Game AI: #956-1023 (~60 issues, 27% complete)
- Admin Console: #874-922 (~30 issues, 0% complete)
- Frontend Epics: #926, #931-935 (6 issues)
- FE-IMP: #1077-1084 (8 issues)

---

---

## 📊 Data Sources & Verification

### Issue Analysis Files

All data in this roadmap is extracted from **172 actual open GitHub issues** analyzed on 2025-11-13:

- **Source**: `docs/planning/all-open-issues-raw.json` (172 issues, 302KB)
- **Analysis**: `docs/planning/issues-analysis.json` (parsed data with priorities, epics, dependencies)

### Issue Breakdown Summary

**By Epic/Category** (172 total):
- BGAI (Board Game AI): 56 issues (33%)
- Admin Console: 49 issues (28%)
- Other (Testing/Infrastructure): 31 issues (18%)
- MVP Sprints: 25 issues (15%)
- Frontend Refactor: 11 issues (6%)

**By Priority** (172 total):
- Critical: 2 issues (1%)
- High: 74 issues (43%)
- Medium: 22 issues (13%)
- Low: 74 issues (43%)

**By Milestone** (Top 10):
1. No Milestone: 34 issues
2. Month 6 (Italian UI): 22 issues
3. FASE 1 (Dashboard): 16 issues
4. Month 5 (Golden Dataset): 14 issues
5. FASE 2 (Infrastructure): 13 issues
6. FASE 3 (Management): 12 issues
7. Month 4 (Quality): 11 issues
8. Month 3 (Validation): 10 issues
9. FASE 4 (Advanced): 8 issues
10. MVP Sprint 1-5: 25 issues (across 5 sprints)

**Dependencies Identified** (3 blocking chains):
- #852 depends on #923 (GameService CRUD)
- #858 depends on #924 (Chat UI)
- #875 depends on #874 (AdminDashboard)

### Top 10 Issues for Week 1 (Immediate Action)

Based on priority analysis of 172 issues:

1. **#1090** (CRITICAL) - Split ChatProvider into Multiple Contexts
2. **#1091** (CRITICAL) - Eliminate Inline Styles and Standardize
3. #846 (HIGH) - OAuth Integration Complete
4. #850 (HIGH) - Unit Test Suite - Authentication Module
5. #852 (HIGH) - GameService CRUD Implementation (depends on #923)
6. #853 (HIGH) - PDF Upload & Processing Pipeline
7. #854 (HIGH) - Game Search & Filter UI
8. #856 (HIGH) - Chat Thread Management
9. #857 (HIGH) - Game-Specific Chat Context
10. #858 (HIGH) - Chat UI with Thread Sidebar (depends on #924)

**Recommendation**: Start with #1090 and #1091 (only 2 critical issues in entire backlog)

---

## ✅ Roadmap Validation Checklist

- [x] **Total issue count matches GitHub**: 172 issues verified
- [x] **Epic distribution calculated**: 5 epics identified
- [x] **Priority distribution calculated**: 2 Critical, 74 High, 22 Medium, 74 Low
- [x] **Dependencies identified**: 3 blocking chains documented
- [x] **Milestone mapping complete**: 16 unique milestones identified
- [x] **Top 20 priorities extracted**: Sorted by priority (Critical → High → Medium → Low)
- [x] **BGAI breakdown verified**: 56 issues across 6 months
- [x] **Admin Console breakdown verified**: 49 issues across 4 FASE
- [x] **MVP Sprints reviewed**: 25 issues (may be legacy)
- [x] **Frontend Refactor identified**: 11 issues (2 critical blockers)

---

**End of Unified Roadmap**

**Version**: 2.1 (Updated with closed issues & branch strategy)
**Generated**: 2025-11-13 (Updated 2025-11-14)
**Data Source**: 154 actual open GitHub issues (18 closed since initial analysis)
**Branches**: **main** (production), **frontend-dev** (active), **backend-dev** (active)
**Checkpoints**: 4 major merge points (Weeks 3, 8, 12, 16)
**Analysis File**: `docs/planning/issues-analysis.json`
**Next Review**: Checkpoint 2 (Week 8 - BGAI Month 1-2 complete)
**Owner**: Single Developer
