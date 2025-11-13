# MeepleAI Unified Development Roadmap 2025

**Generated**: 2025-11-13
**Total Open Issues**: 163
**Developer Mode**: Single developer, 3 branches (main, frontend, backend)
**Timeline**: ~16 weeks (4 months) - Optimized for parallel execution

---

## 🎯 Executive Summary

This roadmap unifies all planning documents and open issues into a single execution plan optimized for a **single developer working across 3 branches**:

- **Branch `main`**: Infrastructure, testing, documentation, cross-cutting concerns
- **Branch `frontend`**: Frontend refactor (Sprint 1-3) + BGAI UI + Admin Console UI
- **Branch `backend`**: BGAI backend (Month 1-6) + Admin Console backend (FASE 1-4)

### Key Metrics

| Metric | Value |
|--------|-------|
| **Total Issues** | 163 open |
| **Critical Path Issues** | 4 (Sprint 1 Frontend) |
| **BGAI Issues** | ~60 (27% complete, 23/86 closed) |
| **Admin Console Issues** | ~30 (0% complete, new epic) |
| **Frontend Refactor** | 15 (Sprint 1-3) |
| **Estimated Timeline** | 16 weeks (~4 months) |
| **Parallel Efficiency** | ~65% (frontend/backend independent) |

---

## 📋 Priority Matrix & Execution Order

### Phase 1: Foundation (Weeks 1-3) - **MUST DO FIRST**

**🔴 CRITICAL - Sprint 1 Frontend Refactor** (Week 1-2, ~5 days)

| # | Issue | Branch | Effort | Can Parallelize? |
|---|-------|--------|--------|------------------|
| #1088 | Unify Login Flow | `frontend` | 4h | ❌ Blocks #1090 |
| #1089 | Refactor Upload Page (1564→400 lines) | `frontend` | 2d | ✅ Parallel with #1090 |
| #1090 | Split ChatProvider (639→250 lines) | `frontend` | 1.5d | ⚠️ Depends on #1088 |
| #1091 | Eliminate Inline Styles | `frontend` | 1d | ✅ Parallel with #1089 |

**Why Critical**: These 4 issues block all frontend work. 1564-line upload.tsx and 639-line ChatProvider are technical debt bombs.

**Execution Strategy for Single Developer**:
```
Week 1 (5 days):
  Day 1: #1088 (Login Flow - 4h) → #1090 Start (ChatProvider - 4h)
  Day 2: #1090 Complete (ChatProvider - 4h) → #1091 Start (Styles - 4h)
  Day 3-4: #1089 (Upload Page - 2d)
  Day 5: #1091 Complete + Testing
```

**⚡ Parallel Opportunity**: If you can work 2 tasks/day:
- Morning: #1089 (Upload) | Afternoon: #1091 (Styles) → Save 1 day

---

### Phase 2: High-Impact Improvements (Weeks 3-4) - **Sprint 2**

**🟡 HIGH PRIORITY - Sprint 2 Frontend** (Week 3, ~3.5 days)

| # | Issue | Branch | Effort | Dependencies |
|---|-------|--------|--------|--------------|
| #1092 | Mobile-First Responsive | `frontend` | 1d | None (✅ Parallel) |
| #1093 | Performance Optimization | `frontend` | 1d | ⚠️ Better after #1089, #1090 |
| #1094 | Accessibility Audit | `frontend` | 0.5d | None (✅ Parallel) |
| #1095 | Error Handling Unified | `frontend` | 0.5d | None (✅ Parallel) |
| #1096 | Loading States Unified | `frontend` | 0.5d | None (✅ Parallel) |

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

### Phase 3: Board Game AI Foundation (Weeks 4-8) - **BGAI Month 1-2**

**🎲 BGAI Backend Track** (Branch `backend`)

#### Month 1: PDF Processing (Weeks 4-5, ~10 days backend)

| # | Issue | Effort | Parallel Frontend? |
|---|-------|--------|-------------------|
| #957 | Documentation (README, API) | 1d | ✅ Sprint 3 (#1097-1102) |
| #956 | Code Review Checklist | 0.5d | ✅ |
| #955 | Bug Fixes & Edge Cases (PDF) | 1d | ✅ |

**Missing Issues**: Based on `board-game-ai-execution-calendar.md`, also need:
- BGAI-1 to BGAI-15: LLMWhisperer, SmolDocling, Orchestrator (not created yet)
- **Action Required**: Create these 15 issues from sprint templates

#### Month 2: LLM Integration (Weeks 6-7, ~10 days backend)

| # | Issue | Effort | Notes |
|---|-------|--------|-------|
| #963 | Feature Flag AI:Provider Config | 1d | OpenRouter vs Ollama toggle |
| #964 | Integration Tests (Adaptive LLM) | 2d | |

**Missing Issues**: BGAI-16 to BGAI-24 (OpenRouter, Ollama, AdaptiveLlm) - **Create from templates**

---

#### Month 3-6: Validation, Quality, Dataset (Weeks 8-16)

**Existing Issues** (Board Game AI Month 3-6):

| Month | Issues | Count | Branch | Status |
|-------|--------|-------|--------|--------|
| **Month 3** | #974-982 | 9 | `backend` | Multi-model validation |
| **Month 4** | #983-995 | 13 | `backend` + `frontend` | Quality framework + UI |
| **Month 5** | #996-1009 | 14 | `backend` + `frontend` | Golden dataset + Q&A UI |
| **Month 6** | #1010-1023 | 14 | `backend` + `frontend` | Italian UI + Completion |

**Total BGAI**: 60 issues (27% complete, 37 remaining)

**⚡ Critical Parallel Opportunity**:
- **Backend** (Month 3-6): PDF, LLM, validation, dataset annotation
- **Frontend** (Month 4-6): BGAI UI components, Italian i18n, PDF viewer

**Time Savings**: 21 days (as per existing calendar analysis)

---

### Phase 4: Admin Console (Weeks 8-12, Parallel with BGAI) - **OPTIONAL/DEFER**

**📊 Admin Console Track** (Branch `backend` + `frontend`)

#### FASE 1: Dashboard Overview (Week 8, ~5 days)

| # | Issue | Branch | Effort | Parallel BGAI? |
|---|-------|--------|--------|---------------|
| #875-880 | Backend (AdminDashboardService, stats) | `backend` | 3d | ⚠️ Conflicts with BGAI backend |
| #881-889 | Frontend (Dashboard UI, components) | `frontend` | 5d | ✅ Parallel with BGAI backend |

#### FASE 2-4: Infrastructure, Management, Advanced (Weeks 9-12, ~15 days)

| FASE | Issues | Effort | Recommendation |
|------|--------|--------|----------------|
| FASE 2 | #890-902 (Infrastructure Monitoring) | 5d | ⚠️ **Defer to Phase 2** (not MVP) |
| FASE 3 | #903-914 (API Keys, User Mgmt, Bulk Ops) | 5d | ⚠️ **Defer to Phase 2** |
| FASE 4 | #915-922 (Reporting, Advanced Alerts) | 5d | ⚠️ **Defer to Phase 2** |

**Recommendation**: **Deprioritize Admin Console**. Focus on BGAI (27% complete, critical path). Admin Console is 0% complete and lower business value.

**If you must do Admin Console**, do FASE 1 only (Dashboard) in parallel with BGAI Month 4-5 frontend work.

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

### Branch Strategy

**Workflow**:
1. **Week 1-3**: Work on `frontend` branch exclusively (Sprint 1-2)
2. **Week 4**: Switch to `backend` branch (BGAI Month 1), complete Sprint 3 frontend in gaps
3. **Week 5-16**: Alternate `backend` (BGAI backend) and `frontend` (BGAI UI) every 2-3 days
4. **Merge to `main`**: End of each sprint/month milestone

**Example Week 4-5**:
```
Week 4:
  Mon-Tue: Backend (BGAI-1, BGAI-2) on `backend` branch
  Wed: Frontend (#1097 Storybook) on `frontend` branch
  Thu-Fri: Backend (BGAI-3, BGAI-4) on `backend` branch

Week 5:
  Mon-Tue: Backend (BGAI-5, BGAI-6) on `backend` branch
  Wed: Frontend (#1098 Tests) on `frontend` branch
  Thu-Fri: Backend (BGAI-8 Orchestrator) on `backend` branch
```

---

## 📊 Dependency Matrix

### Critical Path (Cannot Parallelize)

```
#1088 Unify Login
   ↓
#1090 Split ChatProvider ← BLOCKS all context refactoring
   ↓
#1093 Performance ← Better with #1090 complete
   ↓
Sprint 2-3 Complete
   ↓
BGAI Month 1 PDF ← Backend independent
   ↓
BGAI Month 2 LLM ← Depends on Month 1
   ↓
BGAI Month 3 Validation ← Depends on Month 2 LLM
   ↓
BGAI Month 4-6 UI + Dataset
```

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

**End of Unified Roadmap**

**Version**: 1.0
**Generated**: 2025-11-13
**Next Review**: After Sprint 1 complete (Week 2)
**Owner**: Single Developer
