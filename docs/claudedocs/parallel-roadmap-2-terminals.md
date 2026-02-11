# MeepleAI 2-Terminal Parallel Roadmap
**Total Open Issues**: 96 (excluding 5 in-progress)
**Total Estimated Effort**: 172-230 days (34-46 weeks)
**Strategy**: Maximize parallelization while respecting hard dependencies

---

## Terminal 1: Critical Path (Foundation → Agents → Dashboard)
**Focus**: User-facing features with sequential dependencies
**Total Effort**: 90-120 days

```
Week 1-4   ████████████ MeepleCard Epic #1 (12-15d)
           │            ├─ #4074 Permission System (P0, 2d) ⚠️
           │            ├─ #4073 WCAG Accessibility (P1, 1.5d)
           │            ├─ #4078 Ownership State (P1, 1.5d)
           │            ├─ #4079 Agent Type (P1, 1d) ← BLOCKS Agent System
           │            ├─ #4077 Collection Limits (P1, 2d)
           │            └─ #4072, #4075, #4076, #4080, #4081 (5d)
           │
           ████████ Navbar Core (4d parallel)
           │        ├─ #4099 Dynamic Route (P0, 1.5d) ⚠️
           │        ├─ #4097 Dropdown Structure (P1, 1.5d)
           │        └─ #4098 Mobile Menu (P1, 1.5d)
           │
Week 5-8   ████████████████████ Agent System Epic #2 Backend (10d)
           │                    ├─ #4082 Multi-Agent per Game (P0, 3d) ⚠️
           │                    ├─ #4094 Default POC Strategy (P0, 1.5d) ⚠️
           │                    ├─ #4095 Tier Limit Enforcement (P0, 1d) ⚠️
           │                    ├─ #4083 Strategy System (P1, 4d)
           │                    └─ #4084 Semi-Auto Creation (P1, 1.5d)
           │                    │
           │                    ████████████ Agent Frontend (8d)
           │                    ├─ #4085 Chat UI Base (P0, 4d) ⚠️
           │                    ├─ #4086 Chat Persistence (P1, 2.5d)
           │                    ├─ #4087 Chat History Page (P1, 2.5d)
           │                    └─ #4089 MeepleCard Agent Type (P1, 1d)
           │
Week 9-12  ████████████████ Dashboard & Gamification (12-18d)
           │                ├─ #3902 AI Insights & Recommendations Epic (4-6d)
           │                ├─ #3916 Backend: AI Insights Service (P1, 2-3d)
           │                ├─ #3919 Frontend: AI Insights Widget (P1, 2d)
           │                ├─ #3320 Gamification Features (8-10d)
           │                └─ Requires: Agent #4085, #4096
           │
Week 13-16 ████████████ Agent System Phase 2 (8d)
           │            ├─ #4088 Resume Chat (P2, 1.5d)
           │            ├─ #4090 Agent List Page (P2, 1.5d)
           │            ├─ #4091 Dashboard Widget (P2, 0.5d)
           │            ├─ #4092 Game Page Section (P2, 1.5d)
           │            ├─ #4096 Chat Context KB (P1, 2d)
           │            └─ #4093 Strategy Builder UI (P2, 3d) (optional defer)
           │
           ████████████████ Multi-Agent Integration (14d)
           │                ├─ #3776 Orchestration & Routing (P1, 4d)
           │                ├─ #3777 Agent Switching Logic (P1, 3d)
           │                ├─ #3778 Multi-Agent Dashboard UI (P1, 3d)
           │                ├─ #3779 E2E Testing Suite (P1, 2d)
           │                └─ #3780 Documentation (P2, 2d)
           │
Week 17-30 ██████████████████████████████████████ Multi-Agent Decisore (21d)
           │                                      ├─ #3769 Strategic Analysis (P1, 4d)
           │                                      ├─ #3770 Move Suggestion (P1, 4d)
           │                                      ├─ #3772 Game State Parser (P1, 3d)
           │                                      ├─ #3773 REST API Endpoint (P1, 2d)
           │                                      ├─ #3774 Performance Tuning (P1, 3d)
           │                                      ├─ #3771 Multi-Model Ensemble (P2, 3d)
           │                                      └─ #3775 Beta Testing (P2, 2d)
           │
           ████████ Testing & Validation (6d parallel)
           │        ├─ #3763 Arbitro Testing (P2, 2d)
           │        ├─ #3809 Agent Builder CRUD (P1, 3d)
           │        └─ #3874 Arbitro Performance (1d)

LEGEND: ⚠️ = Critical Path Blocker | P0 = Highest Priority | P1 = High | P2 = Medium
```

**Key Milestones**:
- ✅ Week 4: MeepleCard complete → Unblocks Agent System
- ✅ Week 8: Agent System Backend → Enables Chat UI
- ✅ Week 12: Dashboard with AI Insights → User-facing MVP
- ✅ Week 16: Multi-Agent Integration → Advanced features ready
- ✅ Week 30: Complete Multi-Agent System → Full AI capabilities

---

## Terminal 2: Parallel Track (UI Polish → PDF → AI Platform)
**Focus**: Independent features without blocking Terminal 1
**Total Effort**: 82-110 days

```
Week 1-4   ████████████ Gap Analysis UI (8-12d)
           │            ├─ #4113 Notification System UI (2d) ← BLOCKS Navbar
           │            ├─ #4114 Wishlist Management UI (2d)
           │            ├─ #4115 Play Records Actions UI (1.5d)
           │            ├─ #4116 2FA Self-Service UI (2d)
           │            ├─ #4117 Achievement Display UI (2d)
           │            └─ #4118 User Bulk Operations UI (2d)
           │
Week 5-8   ████████████████ PDF Status Epic #4 (9-12d)
           │                ├─ #4106 7-State Pipeline (P0, 3d) ⚠️
           │                ├─ #4108 Multi-Location Status UI (P1, 3d)
           │                ├─ #4109 Real-time Updates SSE (P1, 2d)
           │                ├─ #4107 Manual Retry + Errors (P1, 1.5d)
           │                ├─ #4110 Duration Metrics & ETA (P3, 1d)
           │                └─ #4111 Notification Channel (P2, 0.5d)
           │
Week 9-12  ████████████ Navbar Notifications (8d)
           │            ├─ #4103 Notifications Dropdown (P1, 2d)
           │            ├─ #4104 Notifications Page (P2, 3d)
           │            ├─ #4105 Notifications Config (P2, 1d)
           │            └─ #4102 Settings Dropdown (P2, 2.5d)
           │
           ████ Quick Wins (1d parallel)
           │    ├─ #4101 Dual CTA (P2, 0.25d)
           │    ├─ #4100 Anonymous Restrictions (P1, 0.5d)
           │    ├─ #4076 Mobile Tag Optimization (P2, 0.5d)
           │    └─ #4081 Performance Optimization (P2, 0.5d)
           │
Week 13-20 ████████████████ RAG Data Model & Strategy (8d)
           │                ├─ #3708 AgentDefinition Data Model (3d)
           │                ├─ #3358 Iterative RAG Strategy (5d)
           │                └─ Feeds: Multi-Agent Orchestration (#3776)
           │
Week 21-30 ██████████████████████████████ RAG UI Layer (15d)
           │                              ├─ #3709 Agent Builder UI (3d)
           │                              ├─ #3710 Agent Playground (3d)
           │                              ├─ #3711 Strategy Editor (3d)
           │                              ├─ #3712 Visual Pipeline Builder (5d)
           │                              └─ #3713 Agent Catalog & Stats (3d)
           │
           ████████████ RAG Analytics (11d parallel)
           │            ├─ #3714 Chat Analytics (3d)
           │            ├─ #3715 PDF Analytics (2d)
           │            ├─ #3716 Model Performance (3d)
           │            ├─ #3717 A/B Testing Framework (3d)
           │            └─ #3718 Testing - AI Platform (2d)
           │
Week 31-34 ████████████ Infrastructure & Testing (9-12d)
           │            ├─ #2968-#2973 CI/CD Migration (1d)
           │            ├─ #3367 Log Aggregation (5d)
           │            ├─ #3368 k6 Load Testing (3d)
           │            └─ #3082 E2E Test Flows (ongoing)
           │
           ████████ Misc Features (4-6d parallel)
           │        ├─ #3120 Private Games Proposal (3-4d)
           │        ├─ #3355 Version History UI (3d)
           │        └─ #3894 EntityListView Polish (1d)
```

**Key Milestones**:
- ✅ Week 4: Gap Analysis complete → Unblocks Navbar Notifications
- ✅ Week 8: PDF Status Tracking → Production-ready document pipeline
- ✅ Week 12: Navbar Complete → Navigation system finalized
- ✅ Week 20: RAG Data Model → Enables Advanced AI UI
- ✅ Week 30: RAG Platform Complete → Full AI analytics
- ✅ Week 34: Infrastructure Optimized → Zero-cost CI/CD

---

## Synchronization Points (Cross-Terminal Dependencies)

| Week | Terminal 1 → Terminal 2 | Terminal 2 → Terminal 1 |
|------|------------------------|------------------------|
| **4** | MeepleCard #4079 → *Unblocks Agent #4089* | Gap Analysis #4113 → *Enables Navbar #4103* |
| **8** | Agent Backend #4082-#4095 → *Enables Chat UI #4085* | PDF Status #4106 → *Ready for notifications* |
| **12** | - | Navbar #4102-#4105 → *User settings complete* |
| **16** | Agent KB Integration #4096 → *Feeds Dashboard AI* | - |
| **20** | - | RAG Strategy #3358 → *Feeds Orchestration #3776* |
| **30** | Multi-Agent #3776-#3780 → *Uses RAG* | RAG Platform #3708-#3718 → *Supports Agents* |

---

## Critical Path Analysis

### Hard Blockers (Sequential Dependencies)
1. **MeepleCard → Agent System**
   - #4074 Permission → #4078 Ownership → #4079 Agent Type → #4089 MeepleCard Agent
   - **Impact**: 5 days delay in Terminal 1 cascades to Week 5-8
   - **Mitigation**: Start #4074 immediately, prioritize over all other work

2. **Agent Backend → Agent Frontend**
   - #4082-#4095 Backend → #4085 Chat UI → #4086-#4089 Frontend
   - **Impact**: 10 days delay blocks Dashboard integration
   - **Mitigation**: Frontend can prep components while backend develops

3. **Gap Analysis → Navbar Notifications**
   - #4113 Notification System UI → #4103 Notifications Dropdown
   - **Impact**: 2 days delay in Terminal 2 Navbar completion
   - **Mitigation**: Soft blocker, can mock notification data initially

### Soft Blockers (Integration Dependencies)
1. **Agent System → Dashboard** (#4085, #4096 → #3916, #3919)
   - Dashboard can use mock data, real integration in Week 12

2. **RAG → Multi-Agent** (#3358, #3708 → #3776)
   - Multi-Agent can use basic strategies, advanced RAG in Week 20

3. **Notifications UI → Multiple Features** (#4113 → #4103, #4111, #4117)
   - Can develop in parallel with mock interfaces

---

## Risk Assessment & Mitigation

### High-Risk Issues (Complexity + Priority)
| Issue | Risk Factor | Mitigation |
|-------|-------------|------------|
| #4082 Backend Multi-Agent | P0, 3d, complex architecture | Prototype in Week 1-2, validate design |
| #4085 Chat UI Base | P0, 4d, XL complexity | Use existing chat libraries (shadcn/ui) |
| #4106 PDF 7-State Pipeline | P0, 3d, state machine complexity | FSM pattern, state diagram first |
| #4083 Strategy System | P1, 4d, XL complexity | Start with simple config, iterate |
| #3776 Multi-Agent Orchestration | P1, 4d, integration complexity | Design state machine, test in isolation |

### Mitigation Strategies
1. **Prototyping Phase** (Week 0): Design #4082, #4085, #4106 before implementation
2. **Incremental Testing**: Add tests during development, not end-of-epic
3. **Mock Data Strategy**: Terminal 2 uses mocks to avoid blocking on Terminal 1
4. **Code Review Gates**: All P0 issues require pre-merge review
5. **Weekly Sync**: Terminal 1 & 2 sync every Friday to align integration points

---

## Effort Distribution

### By Terminal
- **Terminal 1**: 90-120 days (52% of total effort)
  - Foundation: 16-19 days
  - Agent System: 28-38 days
  - Dashboard: 12-18 days
  - Multi-Agent: 34-45 days

- **Terminal 2**: 82-110 days (48% of total effort)
  - UI Polish: 8-13 days
  - PDF Status: 9-12 days
  - Navbar: 8-9 days
  - RAG Platform: 30-40 days
  - Infrastructure: 9-12 days
  - Misc: 18-24 days

### By Priority
- **P0-Critical**: 15-18 days (9%) → **Start immediately**
- **P1-High**: 45-60 days (30%) → **Core roadmap**
- **P2-Medium**: 20-28 days (14%) → **After P0/P1**
- **P3-Low**: 1 day (0.5%) → **Defer**
- **Epic-level**: 91-123 days (46.5%) → **Mix of priorities**

### By Domain
- **Frontend**: 95-130 days (56%)
- **Backend**: 52-70 days (31%)
- **Infrastructure**: 9-12 days (5%)
- **Testing**: 16-23 days (8%)

---

## Quick Wins (Week 1-2 Parallel to Critical Path)

### Terminal 1 Quick Wins (2 hours)
- #4076 Mobile Tag Optimization (0.5d) - Visual polish
- #4081 Performance Optimization (0.5d) - MeepleCard rendering

### Terminal 2 Quick Wins (1 day)
- #4101 Dual CTA Navbar (0.25d) - Auth UX improvement
- #4100 Anonymous Catalog Restrictions (0.5d) - Security
- #4110 PDF Duration Metrics (1d) - User feedback

**ROI**: 1.5 days effort → 5 issues closed, visible user improvements

---

## Testing Strategy

### Continuous Testing (Integrated with Development)
- **Unit Tests**: Mandatory for all handlers, validators, services
- **Integration Tests**: Testcontainers for backend features
- **E2E Tests**: Playwright for critical user flows
- **Accessibility**: axe-core validation for all UI components

### Epic #3082: 50 E2E Test Flows (10-15 days)
Distribute across 10 weeks (5 flows/week):
- **Week 1-2**: Auth flows (login, register, 2FA, OAuth)
- **Week 3-4**: Game catalog (search, filter, detail, wishlist)
- **Week 5-6**: Agent system (create, chat, strategy)
- **Week 7-8**: PDF pipeline (upload, process, status, retry)
- **Week 9-10**: Dashboard (stats, notifications, gamification)

**Goal**: Maintain 85%+ frontend, 90%+ backend coverage

---

## Deferred Low-Priority (Post-MVP)

### Technical Debt (6-10 days)
- #3771 Multi-Model Ensemble (P2, 3d) - Advanced AI, defer to post-launch
- #3717 A/B Testing Framework (3d) - Analytics optimization, not MVP
- #3355 Version History UI (3d) - Power user feature, defer
- #3341 Game Session Toolkit Phase 2 (5-8d) - Enhancement, not critical

### Rationale
- Focus 100% effort on core user flows (Weeks 1-16)
- Advanced features after stable platform (Weeks 17-30)
- Technical debt & polish in final phase (Weeks 31-34)

---

## Success Metrics

### Week 4 Checkpoint
- ✅ MeepleCard Epic #1 complete (10 issues closed)
- ✅ Navbar core navigation functional (#4099, #4097, #4098)
- ✅ Gap Analysis UI 50% complete (#4113-#4114)
- ✅ Zero blockers for Agent System start

### Week 8 Checkpoint
- ✅ Agent System Backend production-ready (#4082-#4095)
- ✅ Agent Chat UI functional (#4085-#4089)
- ✅ PDF Status Tracking complete (#4106-#4111)
- ✅ 15 E2E tests passing

### Week 12 Checkpoint (MVP)
- ✅ Dashboard with AI Insights live (#3902, #3916, #3919)
- ✅ Gamification features active (#3320)
- ✅ Navbar fully functional (#4097-#4105)
- ✅ 30 E2E tests passing
- ✅ User feedback loop active

### Week 16 Checkpoint
- ✅ Multi-Agent Integration operational (#3776-#3778)
- ✅ Agent System Phase 2 complete (#4088-#4096)
- ✅ RAG Data Model implemented (#3708, #3358)
- ✅ 40 E2E tests passing

### Week 30 Checkpoint (Full AI Platform)
- ✅ Multi-Agent Decisore complete (#3769-#3775)
- ✅ RAG UI Platform operational (#3709-#3713)
- ✅ RAG Analytics dashboards (#3714-#3717)
- ✅ 50 E2E tests passing

### Week 34 Completion
- ✅ Zero-cost CI/CD operational (#2968-#2973)
- ✅ Infrastructure observability (#3367-#3368)
- ✅ All epics closed, documentation complete
- ✅ Production-ready platform

---

## Recommendations

### Immediate Actions (This Week)
1. **Terminal 1**: Start #4074 Permission System (P0, 2d) - Critical path blocker
2. **Terminal 2**: Start #4113 Notification System UI (2d) - Unblocks Navbar
3. **Prepare**: Design documents for #4082 Backend Multi-Agent, #4085 Chat UI
4. **Quick Win**: #4101 Dual CTA (0.25d) - Ship today

### Weekly Rhythm
- **Monday**: Review roadmap, align terminals, adjust estimates
- **Wednesday**: Mid-week sync, resolve blockers
- **Friday**: Cross-terminal integration check, plan next week

### Monthly Milestones
- **Month 1** (Week 1-4): Foundation complete, Agent System ready
- **Month 2** (Week 5-8): Agent System backend/frontend operational
- **Month 3** (Week 9-12): Dashboard MVP, user-facing features complete
- **Month 4** (Week 13-16): Advanced Agent features, Multi-Agent integration
- **Months 5-7** (Week 17-30): Full AI platform (Multi-Agent + RAG)
- **Month 8** (Week 31-34): Infrastructure optimization, polish

---

**Roadmap Created**: 2026-02-11 20:45 UTC
**Next Review**: After Week 4 checkpoint
**Estimated Completion**: 34-46 weeks (8-11 months at 5 days/week)
