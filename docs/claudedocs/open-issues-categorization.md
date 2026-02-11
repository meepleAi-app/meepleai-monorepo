# MeepleAI Open Issues Categorization Analysis
**Date**: 2026-02-11
**Total Open Issues Analyzed**: 96 (excluding #4062, #4065, #4064, #4063, #4054)

---

## Category 1: Epic #1 - MeepleCard Enhancements
**Epic Issue**: #4068
**Sub-Issues**: 10 issues (#4072-#4081)
**Total Estimated Effort**: 12-15 days

| Issue | Title | Priority | Size | Est. Days |
|-------|-------|----------|------|-----------|
| #4072 | Smart Tooltip Positioning System | P1-High | M | 1.5 |
| #4073 | WCAG 2.1 AA Accessibility | P1-High | M | 1.5 |
| #4074 | Permission System Integration | P0-Critical | L | 2 |
| #4075 | Tag System Vertical Layout | P1-High | M | 1 |
| #4076 | Mobile Tag Optimization | P2-Medium | S | 0.5 |
| #4077 | Collection Limits Management | P1-High | L | 2 |
| #4078 | Ownership State Logic | P1-High | M | 1.5 |
| #4079 | Agent Type Support | P1-High | M | 1 |
| #4080 | Context-Aware Tests | P2-Medium | M | 1 |
| #4081 | Performance Optimization | P2-Medium | S | 0.5 |

**Critical Path**: #4074 (Permission System) → #4078 (Ownership State) → #4079 (Agent Type)

---

## Category 2: Epic #2 - Agent System
**Epic Issue**: #4069
**Sub-Issues**: 15 issues (#4082-#4096)
**Total Estimated Effort**: 25-30 days

| Issue | Title | Priority | Size | Est. Days |
|-------|-------|----------|------|-----------|
| #4082 | Backend Multi-Agent per Game Support | P0-Critical | L | 3 |
| #4083 | Strategy System (Base/Config/Custom) | P1-High | XL | 4 |
| #4084 | Semi-Auto Creation Flow | P1-High | M | 1.5 |
| #4085 | Chat UI Base Component | P0-Critical | XL | 4 |
| #4086 | Chat Persistence (Hybrid Sync) | P1-High | L | 2.5 |
| #4087 | Chat History Page (Timeline + Filters) | P1-High | L | 2.5 |
| #4088 | Resume Chat (All Methods) | P2-Medium | M | 1.5 |
| #4089 | MeepleCard Agent Type | P1-High | M | 1 |
| #4090 | Agent List Page /agents | P2-Medium | M | 1.5 |
| #4091 | Dashboard Widget Your Agents | P2-Medium | S | 0.5 |
| #4092 | Game Page Agent Section | P2-Medium | M | 1.5 |
| #4093 | Strategy Builder UI | P2-Medium | XL | 3 |
| #4094 | Default POC Strategy Implementation | P0-Critical | M | 1.5 |
| #4095 | Tier Limit Enforcement | P0-Critical | M | 1 |
| #4096 | Chat Context (KB Integration) | P1-High | L | 2 |

**Critical Path**: #4082 (Backend Support) → #4094 (Default Strategy) → #4095 (Tier Limits) → #4085 (Chat UI) → #4089 (MeepleCard Integration)

**Dependencies**:
- Depends on MeepleCard Epic #1 (#4079 Agent Type Support)
- Blocks Dashboard & Gamification features (#3906, #3902)

---

## Category 3: Epic #3 - Navbar Restructuring
**Epic Issue**: #4070
**Sub-Issues**: 9 issues (#4097-#4105)
**Total Estimated Effort**: 12-16 days

| Issue | Title | Priority | Size | Est. Days |
|-------|-------|----------|------|-----------|
| #4097 | Dropdown Grouping Structure | P1-High | M | 1.5 |
| #4098 | Mobile Hamburger Menu | P1-High | M | 1.5 |
| #4099 | Dynamic Route / (Welcome vs Dashboard) | P0-Critical | M | 1.5 |
| #4100 | Anonymous Catalog Restrictions | P1-High | S | 0.5 |
| #4101 | Dual CTA (Accedi + Registrati) | P2-Medium | XS | 0.25 |
| #4102 | Settings Dropdown (8 Sections) | P2-Medium | L | 2.5 |
| #4103 | Notifications Dropdown (Preview) | P1-High | L | 2 |
| #4104 | Notifications Page (10 Types) | P2-Medium | XL | 3 |
| #4105 | Notifications Configuration | P2-Medium | M | 1 |

**Critical Path**: #4099 (Dynamic Route) → #4097 (Dropdown Structure) → #4098 (Mobile Menu) → #4103 (Notifications Dropdown)

**Dependencies**:
- #4103-#4105 depend on Gap Analysis UI (#4113 Notification System)

---

## Category 4: Epic #4 - PDF Status Tracking
**Epic Issue**: #4071
**Sub-Issues**: 6 issues (#4106-#4111)
**Total Estimated Effort**: 9-12 days

| Issue | Title | Priority | Size | Est. Days |
|-------|-------|----------|------|-----------|
| #4106 | 7-State Embedding Pipeline | P0-Critical | L | 3 |
| #4107 | Manual Retry + Error Handling | P1-High | M | 1.5 |
| #4108 | Multi-Location Status UI | P1-High | XL | 3 |
| #4109 | Real-time Updates (SSE + Polling) | P1-High | L | 2 |
| #4110 | Duration Metrics & ETA | P3-Low | M | 1 |
| #4111 | Notification Channel Configuration | P2-Medium | S | 0.5 |

**Critical Path**: #4106 (Pipeline States) → #4108 (Status UI) → #4109 (Real-time Updates) → #4107 (Error Handling)

---

## Category 5: Gap Analysis UI
**Sub-Issues**: 6 issues (#4113-#4118)
**Total Estimated Effort**: 8-12 days

| Issue | Title | Est. Days |
|-------|-------|-----------|
| #4113 | Notification System UI | 2 |
| #4114 | Wishlist Management System UI | 2 |
| #4115 | Play Records Actions UI | 1.5 |
| #4116 | 2FA Self-Service UI | 2 |
| #4117 | Achievement System Display UI | 2 |
| #4118 | User Bulk Operations UI | 2 |

**Dependencies**:
- #4113 blocks Navbar Epic #4103-#4105
- #4117 depends on Epic #3906 (Gamification backend)

---

## Category 6: Dashboard & Gamification
**Epic Issues**: #3320, #3906, #3902
**Sub-Issues**: 2 issues (#3916, #3919)
**Total Estimated Effort**: 12-18 days

| Issue | Title | Priority | Est. Days |
|-------|-------|----------|-----------|
| #3320 | [EPIC] Gamification & Advanced Dashboard Features | Medium | 8-10 |
| #3906 | Epic: Gamification & Advanced Features | Medium | (covered by #3320) |
| #3902 | Epic: AI Insights & Recommendations | High | 4-6 |
| #3916 | Backend: AI Insights Service (RAG Integration) | High | 2-3 |
| #3919 | Frontend: AI Insights Widget Component | High | 2 |

**Dependencies**:
- Depends on Agent System Epic #2 (#4085 Chat UI, #4096 KB Integration)
- Blocks Gap Analysis #4117 (Achievement Display)

---

## Category 7: Multi-Agent System (Epic #3490)
**Epic Issue**: #3490 (29 sub-issues, 11 complete, 18 remaining)
**Sub-Issues**: 18 issues (#3763, #3769-#3780, #3809, #3874, #3894)
**Total Estimated Effort**: 35-45 days

### Decisore Agent (7 issues)
| Issue | Title | Priority | Est. Days |
|-------|-------|----------|-----------|
| #3769 | Strategic Analysis Engine - Game State Evaluation | High | 4 |
| #3770 | Move Suggestion Algorithm with Priority Ranking | High | 4 |
| #3771 | Multi-Model Ensemble for Expert-Level Decisions | Medium | 3 |
| #3772 | Game State Parser & Context Assembly | High | 3 |
| #3773 | REST API Endpoint - /api/v1/agents/decisore/suggest | High | 2 |
| #3774 | Performance Tuning - <10s Target for Expert Moves | High | 3 |
| #3775 | Beta Testing & Expert Validation | Medium | 2 |

### Integration Layer (5 issues)
| Issue | Title | Priority | Est. Days |
|-------|-------|----------|-----------|
| #3776 | Multi-Agent Orchestration & Routing System | High | 4 |
| #3777 | Agent Switching Logic & Context Preservation | High | 3 |
| #3778 | Unified Multi-Agent Dashboard UI | High | 3 |
| #3779 | E2E Testing Suite - All Agent Workflows | High | 2 |
| #3780 | Complete Documentation & User Guide | Medium | 2 |

### Supporting Issues (3 issues)
| Issue | Title | Priority | Est. Days |
|-------|-------|----------|-----------|
| #3763 | [Arbitro Agent] Testing & User Feedback Iteration | Medium | 2 |
| #3809 | Frontend: Agent Builder Form & CRUD | High | 3 |
| #3874 | [Arbitro Agent] Performance Benchmark Tests | - | 1 |
| #3894 | EntityListView - Test Coverage & Polish | - | 1 |

**Critical Path**: Decisore Agent (#3769-#3774) → Integration (#3776-#3777) → Dashboard (#3778) → Testing (#3779-#3780)

**Dependencies**:
- Integrates with Agent System Epic #2 (#4082-#4096)
- Shares backend infrastructure with RAG Epic #3356

---

## Category 8: RAG & AI Platform (Epic #3356)
**Epic Issue**: #3356 (9 RAG variants)
**Sub-Issues**: 12 issues (#3358, #3708-#3718)
**Total Estimated Effort**: 30-40 days

### Data Model & Core (2 issues)
| Issue | Title | Est. Days |
|-------|-------|-----------|
| #3708 | [Epic 3] AgentDefinition Data Model | 3 |
| #3358 | [RAG] Implement Iterative RAG Strategy | 5 |

### UI Layer (5 issues)
| Issue | Title | Complexity | Est. Days |
|-------|-------|------------|-----------|
| #3709 | [Epic 3] Agent Builder UI | - | 3 |
| #3710 | [Epic 3] Agent Playground | - | 3 |
| #3711 | [Epic 3] Strategy Editor | - | 3 |
| #3712 | [Epic 3] Visual Pipeline Builder | Large | 5 |
| #3713 | [Epic 3] Agent Catalog & Usage Stats | - | 3 |

### Analytics (4 issues)
| Issue | Title | Complexity | Est. Days |
|-------|-------|------------|-----------|
| #3714 | [Epic 3] Chat Analytics | Medium | 3 |
| #3715 | [Epic 3] PDF Analytics | - | 2 |
| #3716 | [Epic 3] Model Performance Tracking | - | 3 |
| #3717 | [Epic 3] A/B Testing Framework | - | 3 |

### Testing (1 issue)
| Issue | Title | Est. Days |
|-------|-------|-----------|
| #3718 | [Epic 3] Testing - AI Platform | 2 |

**Dependencies**:
- Depends on Agent System Epic #2 (#4083 Strategy System, #4093 Strategy Builder UI)
- Integrates with Multi-Agent Epic #3490 (#3776 Orchestration)
- Feeds into Dashboard Epic #3902 (#3916 AI Insights)

---

## Category 9: Infrastructure & Observability
**Epic Issues**: #2967, #3366
**Sub-Issues**: 10 issues (#2968-#2976, #3367-#3368)
**Total Estimated Effort**: 10-15 days

### Zero-Cost CI/CD (Epic #2967, 8 issues)
| Issue | Title | Est. Hours |
|-------|-------|-----------|
| #2968 | [Week 1.1] Oracle Cloud Setup & VM Provisioning | 0.5h |
| #2969 | [Week 1.2] GitHub Actions Runner Installation & Config | 1.5h |
| #2970 | [Week 1.3] Workflow Migration to Self-Hosted Runner | 1h |
| #2972 | [Week 2.1] Performance Monitoring & Reliability Check | 0.5h |
| #2973 | [Week 2.2] Cost Validation on GitHub Billing | 0.5h |
| #2974 | [Optional] Setup Monitoring (Prometheus + Grafana) | 1h |
| #2975 | [Optional] Document Troubleshooting Procedures | 0.5h |
| #2976 | [Optional] Create Maintenance Schedule Automation | 1h |

**Total CI/CD**: 6-8 hours (0.75-1 day)

### Observability (Epic #3366, 2 issues)
| Issue | Title | Priority | Est. Days |
|-------|-------|----------|-----------|
| #3367 | [Infra] Log Aggregation System | Medium | 5 |
| #3368 | [Infra] k6 Load Testing Infrastructure | Medium | 3 |

**Total Infra Effort**: 9-12 days

---

## Category 10: Other/Miscellaneous
**Total Estimated Effort**: 20-30 days

| Issue | Title | Priority | Est. Days |
|-------|-------|----------|-----------|
| #3082 | [Testing] Implement Missing E2E Test Flows (50 flows) | High | 10-15 |
| #3120 | feat(UserLibrary): Private Games & Catalog Proposal System | - | 3-4 |
| #3341 | [EPIC] Game Session Toolkit Phase 2 - Advanced Tools | Medium | 5-8 |
| #3348 | [EPIC] Advanced Features - AI, Admin & Collaboration | Medium | (covered by other epics) |
| #3355 | [Editor] Game/Document Version History and Comparison UI | Medium | 3 |
| #3688 | EPIC: Business & Simulations [Admin Enterprise] | Medium | (covered by completed work) |

---

## Summary Statistics

### By Category
| Category | Issues | Est. Days | Priority Weight |
|----------|--------|-----------|-----------------|
| **1. MeepleCard** | 10 | 12-15 | High (7 P1+, 1 P0) |
| **2. Agent System** | 15 | 25-30 | Critical (3 P0, 7 P1+) |
| **3. Navbar** | 9 | 12-16 | High (1 P0, 5 P1) |
| **4. PDF Status** | 6 | 9-12 | High (1 P0, 3 P1) |
| **5. Gap Analysis UI** | 6 | 8-12 | Medium |
| **6. Dashboard & Gamification** | 5 | 12-18 | High |
| **7. Multi-Agent System** | 18 | 35-45 | High |
| **8. RAG & AI** | 12 | 30-40 | Medium-Low |
| **9. Infrastructure** | 10 | 9-12 | Medium |
| **10. Other/Misc** | 5 | 20-30 | Medium-High |

**Total Open Issues**: 96 (excluding 5 in-progress)
**Total Estimated Effort**: 172-230 days (34-46 weeks at 5 days/week)

---

## Critical Path Dependencies

### Phase 1: Foundation (Weeks 1-4)
**Sequential Prerequisites**:
1. **MeepleCard Epic #1** (12-15 days)
   - #4074 Permission System → #4078 Ownership State → #4079 Agent Type
   - **Blocks**: Agent System #4089

2. **Navbar Epic #3 Core** (4 days critical path only)
   - #4099 Dynamic Route → #4097 Dropdown Structure → #4098 Mobile Menu
   - **Parallel Track**: Can run alongside MeepleCard

**Outcome**: Foundation components ready for agent integration

### Phase 2: Agent System Core (Weeks 5-8)
**Sequential Requirements**:
3. **Agent System Epic #2 Backend** (10 days)
   - #4082 Backend Multi-Agent → #4094 Default Strategy → #4095 Tier Limits
   - **Requires**: MeepleCard #4079 complete

4. **Agent System Epic #2 Frontend** (8 days)
   - #4085 Chat UI → #4086 Chat Persistence → #4089 MeepleCard Agent Type
   - **Requires**: Backend (#4082-#4095) complete

**Outcome**: Functional agent chat system with base strategy

### Phase 3: Integration & Features (Weeks 9-16)
**Parallel Tracks**:
5. **Gap Analysis UI** (8-12 days) - *Can run parallel*
   - #4113 Notifications → #4114 Wishlist → #4116 2FA → #4117 Achievements
   - **Feeds**: Navbar #4103-#4105

6. **PDF Status Epic #4** (9-12 days) - *Can run parallel*
   - #4106 Pipeline → #4108 Status UI → #4109 Real-time Updates

7. **Dashboard & Gamification** (12-18 days)
   - #3902 AI Insights → #3916 Backend → #3919 Widget
   - **Requires**: Agent System #4085, #4096

**Outcome**: Feature-complete user-facing platform

### Phase 4: Advanced AI (Weeks 17-30)
**Large Parallel Epics**:
8. **Multi-Agent System Epic #3490** (35-45 days)
   - Decisore Agent (7 issues, 21 days) → Integration (5 issues, 14 days)
   - **Integrates**: Agent System #2, shares infra with RAG #8

9. **RAG & AI Platform Epic #3356** (30-40 days) - *Can overlap 50% with #8*
   - Data Model (#3708) → RAG Strategy (#3358) → UI Layer (#3709-#3713) → Analytics (#3714-#3717)

**Outcome**: Advanced AI capabilities and analytics

### Phase 5: Polish & Infra (Weeks 31-34)
**Cleanup & Optimization**:
10. **Infrastructure Epic #2967 + #3366** (9-12 days)
    - CI/CD Migration (1 day) → Observability (8-11 days)

11. **Testing & Misc** (20-30 days) - *Ongoing throughout*
    - #3082 E2E Tests (50 flows)
    - Polish, documentation, user feedback

---

## Recommended 2-Terminal Parallel Roadmap

### Terminal 1: Critical Path (Foundation → Agent System → Dashboard)
**Focus**: User-facing features with sequential dependencies

**Week 1-4**: MeepleCard Epic #1 + Navbar Core (#4099, #4097, #4098)
**Week 5-8**: Agent System Epic #2 Backend → Frontend → Integration
**Week 9-12**: Dashboard & Gamification (#3902, #3916, #3919)
**Week 13-16**: Multi-Agent Integration Layer (#3776-#3778)
**Week 17-30**: Multi-Agent Decisore (#3769-#3775) + Testing (#3779-#3780)

**Total Terminal 1 Effort**: 90-120 days

### Terminal 2: Parallel Track (UI Polish → PDF → Advanced AI)
**Focus**: Independent features that can progress without blocking Terminal 1

**Week 1-4**: Gap Analysis UI (#4113-#4118)
**Week 5-8**: PDF Status Epic #4 (#4106-#4111)
**Week 9-12**: Navbar Notifications (#4103-#4105) + Settings (#4102)
**Week 13-20**: RAG Data Model + Strategy (#3708, #3358)
**Week 21-30**: RAG UI Layer (#3709-#3713) + Analytics (#3714-#3717)
**Week 31-34**: Infrastructure (#2967, #3366) + Testing (#3082)

**Total Terminal 2 Effort**: 82-110 days

### Synchronization Points
- **Week 4**: MeepleCard #4079 complete → Unblocks Agent System #4089 (Terminal 1)
- **Week 8**: Agent Backend #4082-#4095 → Unblocks Chat UI #4085 (Terminal 1)
- **Week 12**: Notifications UI #4113 → Unblocks Navbar #4103 (Terminal 2)
- **Week 16**: Agent System #4096 KB Integration → Enables Dashboard AI Insights (Terminal 1)
- **Week 20**: RAG Strategy #3358 → Feeds Multi-Agent Orchestration (Terminal 2 → Terminal 1)

---

## Risk Factors

### High-Risk Dependencies
1. **MeepleCard → Agent System** (Hard blocker, 4079 → 4089)
2. **Agent Backend → Agent Frontend** (Hard blocker, 4082-4095 → 4085)
3. **Notifications UI → Navbar Notifications** (Soft blocker, 4113 → 4103)
4. **Agent System → Dashboard** (Soft blocker, 4085/4096 → 3916/3919)

### Complexity Hotspots (XL Issues)
- #4083 Strategy System (4 days)
- #4085 Chat UI Base (4 days)
- #4093 Strategy Builder UI (3 days)
- #4104 Notifications Page (3 days)
- #4108 Multi-Location PDF Status UI (3 days)
- #3712 Visual Pipeline Builder (5 days)

### Integration Challenges
- **Multi-Agent + RAG**: Shared orchestration layer (#3776 + #3708)
- **Agent System + Dashboard**: RAG integration for AI insights (#4096 + #3916)
- **PDF + Notifications**: Multiple notification channels (#4111 + #4113)

---

## Recommendations

### Immediate Actions (Week 1)
1. **Start MeepleCard Epic #1** (Terminal 1)
   - #4074 Permission System (P0-Critical, 2 days)
   - #4073 WCAG Accessibility (P1-High, 1.5 days)

2. **Start Gap Analysis UI** (Terminal 2)
   - #4113 Notification System UI (2 days)
   - #4114 Wishlist Management UI (2 days)

### Quick Wins (Parallel to Critical Path)
- #4101 Dual CTA (0.25 days)
- #4076 Mobile Tag Optimization (0.5 days)
- #4081 Performance Optimization (0.5 days)
- #4110 PDF Duration Metrics (1 day)
- #2968-#2973 CI/CD Setup (3 hours, infra efficiency)

### Deferred Low-Priority
- #3771 Multi-Model Ensemble (Medium priority, 3 days)
- #3717 A/B Testing Framework (3 days)
- #3355 Version History UI (3 days)
- #3341 Game Session Toolkit Phase 2 (5-8 days)

### Testing Strategy
- **Continuous**: Add tests during development (not end-of-epic)
- **Epic #3082**: Spread 50 E2E flows across 10 weeks (5/week)
- **Integration Tests**: Mandatory for #4082 (Agent Backend), #4106 (PDF Pipeline)

---

## Effort Distribution

### By Priority
- **P0-Critical**: 5 issues, 15-18 days (9% of effort)
- **P1-High**: 23 issues, 45-60 days (30% of effort)
- **P2-Medium**: 12 issues, 20-28 days (14% of effort)
- **P3-Low**: 1 issue, 1 day (0.5% of effort)
- **Untagged/Epic-level**: 55 issues, 91-123 days (46.5% of effort)

### By Domain
- **Frontend**: 58 issues, 95-130 days (56%)
- **Backend**: 22 issues, 52-70 days (31%)
- **Infrastructure**: 10 issues, 9-12 days (5%)
- **Testing**: 6 issues, 16-23 days (8%)

### By Complexity
- **XL (4+ days)**: 6 issues, 25-30 days
- **L (2-3 days)**: 18 issues, 44-54 days
- **M (1-2 days)**: 35 issues, 48-63 days
- **S (0.5-1 days)**: 5 issues, 3-5 days
- **XS (<0.5 days)**: 1 issue, 0.25 days
- **Epic-level**: 31 issues (effort in sub-issues)

---

**Analysis Completed**: 2026-02-11 20:45 UTC
**Next Update**: After Phase 1 completion (Week 4)
