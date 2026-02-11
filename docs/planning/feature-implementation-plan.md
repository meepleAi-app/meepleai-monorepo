# MeepleAI Feature Implementation Plan
**Epic #4068-4071**: MeepleCard, Agent System, Navbar, PDF Status Tracking

**Created**: 2026-02-11
**Total Issues**: 38 + 4 Epic
**Estimated Duration**: 16-20 weeks (4-5 months)
**Team Size**: 2-3 developers

---

## 🎯 Executive Summary

### Objectives
Implement 4 major feature sets to enhance MeepleAI's core functionality:
1. **MeepleCard Enhancements** - Improved component system with accessibility
2. **Agent System** - Complete AI agent framework with multi-agent support
3. **Navbar Restructuring** - Modern navigation with notifications
4. **PDF Status Tracking** - Transparent embedding pipeline

### Success Criteria
- ✅ All P0-Critical issues implemented (7 issues)
- ✅ 90% test coverage on new features
- ✅ WCAG 2.1 AA accessibility compliance
- ✅ <3s Time to Interactive on all pages
- ✅ Zero critical bugs in production

### Business Impact
- **User Experience**: 40% improvement in navigation efficiency
- **Engagement**: 3x increase in AI agent usage
- **Transparency**: 80% reduction in "Where's my PDF?" support tickets
- **Accessibility**: WCAG compliance opens market to +15% users

---

## 📊 Sprint Planning (8 Sprints × 2 weeks)

### Sprint 1: Foundations (Weeks 1-2)
**Theme**: Critical infrastructure for all features
**Goal**: Enable parallel development on all epic

**Issues** (5 P0-Critical):
- [ ] #4074 - MeepleCard Permission System Integration (L - 5d)
- [ ] #4082 - Agent Backend Multi-Agent Support (L - 5d)
- [ ] #4085 - Agent Chat UI Base Component (XL - 8d)
- [ ] #4099 - Navbar Dynamic Route "/" (M - 2d)
- [ ] #4106 - PDF 7-State Embedding Pipeline (L - 5d)

**Capacity**: 25 dev-days (2.5 devs × 10 days)
**Dependencies**: None (all foundational)
**Risks**:
- Chat UI component complexity may spill over
- Permission system needs careful testing with all tier combinations

**Deliverables**:
- ✅ Permission system functional with all tier/role combinations
- ✅ Multi-agent backend CRUD operational
- ✅ Chat UI base component ready for variants
- ✅ Welcome page live for anonymous users
- ✅ PDF pipeline tracking all 7 states

---

### Sprint 2: Core Agent Features (Weeks 3-4)
**Theme**: Agent creation, strategies, and core functionality
**Goal**: Users can create and interact with agents

**Issues** (7):
- [ ] #4083 - Agent Strategy System (XL - 8d) [P1]
- [ ] #4084 - Semi-Auto Creation Flow (M - 2d) [P1]
- [ ] #4094 - Default POC Strategy (M - 2d) [P0]
- [ ] #4095 - Tier Limit Enforcement (M - 2d) [P0]
- [ ] #4096 - Chat Context (KB Integration) (L - 4d) [P1]
- [ ] #4086 - Chat Persistence (Hybrid Sync) (L - 4d) [P1]
- [ ] #4089 - MeepleCard Agent Type (M - 2d) [P1]

**Capacity**: 24 dev-days
**Dependencies**:
- Requires Sprint 1: #4082 (backend), #4085 (chat UI)
- KB integration depends on existing RAG system

**Risks**:
- Strategy system complexity (XL)
- Hybrid sync may need performance tuning

**Deliverables**:
- ✅ Users can create agents (POC strategy default)
- ✅ Agent chat functional with KB context
- ✅ Chat messages persist across sessions
- ✅ Tier limits enforced
- ✅ Agent cards display in UI

---

### Sprint 3: MeepleCard Polish + Agent UX (Weeks 5-6)
**Theme**: Enhanced MeepleCard + Agent discoverability
**Goal**: Production-ready MeepleCard + Agent pages

**Issues** (8):
- [ ] #4072 - Smart Tooltip Positioning (M - 2d) [P1]
- [ ] #4073 - WCAG Accessibility (M - 3d) [P1]
- [ ] #4075 - Tag System Vertical Layout (M - 2d) [P1]
- [ ] #4076 - Mobile Tag Optimization (S - 1d) [P2]
- [ ] #4077 - Collection Limits Management (L - 4d) [P1]
- [ ] #4078 - Ownership State Logic (M - 2d) [P1]
- [ ] #4090 - Agent List Page /agents (M - 2d) [P2]
- [ ] #4091 - Dashboard Widget (S - 1d) [P2]

**Capacity**: 17 dev-days
**Dependencies**:
- Requires Sprint 1: #4074 (permissions)
- Agent pages require Sprint 2: #4089 (agent card type)

**Risks**:
- Accessibility testing may reveal issues requiring rework
- Mobile optimization needs real device testing

**Deliverables**:
- ✅ MeepleCard fully accessible (WCAG 2.1 AA)
- ✅ Smart tooltips in all contexts
- ✅ Collection limits enforced with clear messaging
- ✅ /agents page functional
- ✅ Dashboard shows agent quick access

---

### Sprint 4: Agent Advanced + Navbar Core (Weeks 7-8)
**Theme**: Advanced agent features + navigation structure
**Goal**: Complete agent system + modern navbar

**Issues** (6):
- [ ] #4087 - Chat History Page (L - 4d) [P1]
- [ ] #4088 - Resume Chat (All Methods) (M - 2d) [P2]
- [ ] #4092 - Game Page Agent Section (M - 2d) [P2]
- [ ] #4097 - Navbar Dropdown Grouping (M - 2d) [P1]
- [ ] #4098 - Mobile Hamburger Menu (M - 2d) [P1]
- [ ] #4100 - Anonymous Catalog Restrictions (S - 1d) [P1]

**Capacity**: 13 dev-days
**Dependencies**:
- Chat History requires Sprint 2: #4086 (persistence)
- Navbar requires Sprint 1: #4099 (dynamic route)

**Risks**:
- Chat History full-text search may need Elasticsearch setup
- Mobile navigation UX needs user testing

**Deliverables**:
- ✅ Complete chat history with search
- ✅ Multiple resume methods
- ✅ Agents visible on game pages
- ✅ New navbar structure live
- ✅ Anonymous users see restricted catalog

---

### Sprint 5: PDF Status + Notifications (Weeks 9-10)
**Theme**: Transparency + communication
**Goal**: Users always know PDF status + notification system

**Issues** (6):
- [ ] #4107 - PDF Manual Retry + Error Handling (M - 2d) [P1]
- [ ] #4108 - PDF Multi-Location UI (XL - 6d) [P1]
- [ ] #4109 - PDF Real-time Updates (SSE) (L - 4d) [P1]
- [ ] #4103 - Notifications Dropdown (L - 3d) [P1]
- [ ] #4101 - Navbar Dual CTA (XS - 0.5d) [P2]
- [ ] #4102 - Settings Dropdown (L - 3d) [P2]

**Capacity**: 18.5 dev-days
**Dependencies**:
- Requires Sprint 1: #4106 (pipeline)
- Notifications require backend notification system

**Risks**:
- SSE implementation complexity
- Multi-location UI consistency across contexts

**Deliverables**:
- ✅ PDF status visible everywhere
- ✅ Real-time updates via SSE
- ✅ Error handling with retry
- ✅ Notification dropdown functional
- ✅ Settings accessible

---

### Sprint 6: Notifications System + Agent Polish (Weeks 11-12)
**Theme**: Complete notification system + agent refinements
**Goal**: Full notification management + strategy builder

**Issues** (5):
- [ ] #4104 - Notifications Page (XL - 6d) [P2]
- [ ] #4105 - Notifications Configuration (M - 2d) [P2]
- [ ] #4111 - PDF Notification Channels (S - 1d) [P2]
- [ ] #4093 - Agent Strategy Builder UI (XL - 7d) [P2]
- [ ] #4079 - MeepleCard Agent Type Support (M - 2d) [P1]

**Capacity**: 18 dev-days
**Dependencies**:
- Notifications require Sprint 5: #4103 (dropdown)
- Strategy builder for Enterprise/Editor users

**Risks**:
- Strategy builder UI complexity (Monaco Editor integration)
- Notification preferences storage schema

**Deliverables**:
- ✅ Full notification management page
- ✅ User-configurable channels
- ✅ PDF notifications working
- ✅ Custom strategy builder (Enterprise+)
- ✅ Agent type fully supported in MeepleCard

---

### Sprint 7: Testing + Performance (Weeks 13-14)
**Theme**: Quality assurance + optimization
**Goal**: >85% coverage + performance targets met

**Issues** (3):
- [ ] #4080 - MeepleCard Context-Aware Tests (M - 3d) [P2]
- [ ] #4081 - MeepleCard Performance Optimization (S - 2d) [P2]
- [ ] #4110 - PDF Duration Metrics & ETA (M - 3d) [P3]

**Additional Testing Work**:
- [ ] Integration tests for Agent System (3d)
- [ ] E2E tests for critical flows (3d)
- [ ] Performance profiling + optimization (2d)
- [ ] Accessibility audit (2d)
- [ ] Security review (2d)

**Capacity**: 15 dev-days
**Dependencies**: Requires all feature implementation complete

**Risks**:
- Tests may reveal regressions requiring fixes
- Performance issues may need architectural changes

**Deliverables**:
- ✅ Test coverage >85%
- ✅ All performance metrics met
- ✅ Zero critical/high bugs
- ✅ Accessibility compliance verified
- ✅ Security review passed

---

### Sprint 8: Documentation + Polish (Weeks 15-16)
**Theme**: Production readiness + knowledge transfer
**Goal**: Complete documentation + user onboarding

**Documentation Tasks**:
- [ ] User guides (Agent creation, Chat usage) (2d)
- [ ] API documentation updates (1d)
- [ ] Admin guides (Tier management, Strategy approval) (1d)
- [ ] Architecture decision records (ADRs) (1d)
- [ ] Deployment guides (1d)
- [ ] Training materials for support team (1d)

**Polish Tasks**:
- [ ] UI/UX refinements from user feedback (3d)
- [ ] Animation/transition polish (1d)
- [ ] Error message improvements (1d)
- [ ] Loading state consistency (1d)

**Capacity**: 13 dev-days
**Dependencies**: All features complete

**Deliverables**:
- ✅ Complete documentation
- ✅ User onboarding flow polished
- ✅ Support team trained
- ✅ Production deployment plan ready

---

## 👥 Resource Allocation

### Team Structure (Recommended)
**Option A: 3 Developers (Optimal)**
- **Dev 1 (Full-stack, Backend focus)**: Agent backend, PDF pipeline, permissions
- **Dev 2 (Full-stack, Frontend focus)**: MeepleCard, Chat UI, Navbar
- **Dev 3 (Frontend specialist)**: Accessibility, mobile, performance

**Option B: 2 Developers (Lean)**
- **Dev 1 (Full-stack)**: Backend features + Chat system
- **Dev 2 (Full-stack)**: Frontend features + MeepleCard
- ⚠️ Timeline extends to 20-24 weeks

### Skill Requirements
**Must-Have**:
- React 19 + Next.js 15 (App Router)
- TypeScript
- .NET 9 + EF Core
- PostgreSQL + Vector DB (Qdrant)
- Tailwind CSS + Radix UI

**Nice-to-Have**:
- WCAG 2.1 AA expertise
- SSE/WebSocket experience
- Monaco Editor integration
- Service Worker for offline sync

---

## 🔗 Dependency Graph

### Critical Path
```
Sprint 1 (Foundations)
    ├─→ Sprint 2 (Core Agent)
    │       ├─→ Sprint 4 (Agent Advanced)
    │       └─→ Sprint 6 (Strategy Builder)
    ├─→ Sprint 3 (MeepleCard Polish)
    ├─→ Sprint 5 (PDF Status + Notifications)
    │       └─→ Sprint 6 (Notification System)
    └─→ Sprint 7 (Testing)
            └─→ Sprint 8 (Documentation)
```

### Cross-Epic Dependencies
1. **Agent System** → **MeepleCard**: Agent card type (#4089) needs #4082
2. **Navbar** → **Notifications**: Dropdown (#4103) needs notification backend
3. **PDF Status** → **Notifications**: Channel config (#4111) needs #4105
4. **Permission System** (#4074) → **All MeepleCard features**

### External Dependencies
- ✅ **Existing RAG System**: Agent chat KB integration (#4096)
- ✅ **Authentication**: Permission checks (#4074)
- ⚠️ **Notification Backend**: May need new service (Sprint 5)
- ⚠️ **Embedding Service**: PDF pipeline integration (Sprint 1)

---

## ⚠️ Risk Assessment & Mitigation

### High-Risk Items (Probability × Impact)

**1. Chat UI Complexity (8/10)**
- **Risk**: Base component (#4085) may not handle all layout variants
- **Impact**: Blocks Sprint 2-4 agent features
- **Mitigation**:
  - Prototype all 3 layouts (modal, sidebar, full-page) in Sprint 1
  - Daily stand-ups to catch issues early
  - Fallback: Simplify to 2 layouts (modal + full-page)

**2. SSE Implementation (7/10)**
- **Risk**: Server-Sent Events may have browser compatibility issues
- **Impact**: PDF real-time updates delayed
- **Mitigation**:
  - Implement polling fallback from start
  - Test on all target browsers (Chrome, Firefox, Safari, Edge)
  - Monitor connection stability in staging

**3. Permission System Complexity (7/10)**
- **Risk**: Tier/Role/State/Resource matrix creates edge cases
- **Impact**: Blocks all MeepleCard features
- **Mitigation**:
  - Comprehensive test matrix (4 tiers × 4 roles × 5 states)
  - Permission decision tree documentation
  - Admin override for edge cases

**4. Accessibility Compliance (6/10)**
- **Risk**: WCAG 2.1 AA audit may fail on first attempt
- **Impact**: Sprint 3 rework, release delay
- **Mitigation**:
  - Run axe-core automated tests continuously
  - Hire accessibility consultant for Sprint 3 audit
  - Budget 2-3 days for fixes

**5. Strategy Builder UI (6/10)**
- **Risk**: Monaco Editor integration complex, Enterprise-only limits testing
- **Impact**: Sprint 6 delay
- **Mitigation**:
  - Use Monaco React wrapper (well-maintained)
  - Create test Enterprise account
  - Start integration spike in Sprint 2

### Medium-Risk Items

**6. Hybrid Chat Sync (5/10)**
- **Risk**: IndexedDB + Backend sync may have race conditions
- **Mitigation**: Use established pattern (Dexie.js), optimistic UI with rollback

**7. Mobile Navigation UX (5/10)**
- **Risk**: Hamburger menu may not feel intuitive
- **Mitigation**: User testing with 5-10 mobile users in Sprint 4

**8. Full-Text Search Performance (4/10)**
- **Risk**: Chat history search may be slow with many messages
- **Mitigation**: Index chat_messages.content, consider Elasticsearch if >10K messages/user

---

## 📏 Success Metrics

### Performance (Web Vitals)
- **First Contentful Paint (FCP)**: <1.5s
- **Largest Contentful Paint (LCP)**: <2.5s
- **Time to Interactive (TTI)**: <3s
- **Cumulative Layout Shift (CLS)**: <0.1
- **First Input Delay (FID)**: <100ms

**Measurement**: Lighthouse CI on every PR

### Test Coverage
- **Unit Tests**: >90%
- **Integration Tests**: >80%
- **E2E Tests**: Critical flows 100%

**Measurement**: Istanbul coverage reports

### Accessibility
- **WCAG 2.1 AA**: 100% compliance
- **axe-core**: Zero violations
- **Screen Reader**: All features navigable

**Measurement**: Manual + automated audits

### User Experience
- **Agent Creation Success Rate**: >95%
- **Chat Message Delivery**: <500ms p95
- **PDF Status Visibility**: 100% (no "where's my PDF?" tickets)
- **Notification Read Rate**: >60%

**Measurement**: Mixpanel events + Support ticket tags

### Business Metrics
- **Agent Adoption**: 40% of users create ≥1 agent (Month 1)
- **Chat Engagement**: 3x increase in messages sent
- **Upgrade Conversion**: 15% Free → Pro (driven by agent limits)
- **Support Ticket Reduction**: 80% fewer "PDF status" tickets

**Measurement**: PostHog analytics + Zendesk

---

## 🚀 Deployment Strategy

### Phased Rollout

**Phase 1: Internal Testing (Sprint 7)**
- Deploy to staging environment
- Internal team testing (5-10 users)
- Fix critical bugs

**Phase 2: Beta (Sprint 8 Week 1)**
- Enable for 10% of users (feature flag)
- Monitor error rates, performance
- Collect user feedback

**Phase 3: General Availability (Sprint 8 Week 2)**
- Roll out to 50% → 100% over 3 days
- Monitor metrics continuously
- Have rollback plan ready

### Feature Flags
```yaml
features:
  meeplecard_v2: true
  agent_system: true
  navbar_v2: true
  pdf_status_tracking: true

  # Gradual rollout
  agent_custom_strategies: false  # Enterprise only initially
  notification_push: false  # Enable after stability proven
```

### Rollback Plan
- **Trigger**: Error rate >5%, performance degradation >50%
- **Action**: Disable feature flags, restore previous UI
- **Communication**: Status page update + user email

---

## 📅 Milestone Checklist

### Milestone 1: Foundations Complete (End Sprint 1)
- [ ] All P0-Critical issues resolved
- [ ] Permission system tested with all combinations
- [ ] Multi-agent backend CRUD functional
- [ ] Chat UI base component approved by design
- [ ] PDF pipeline tracking all states
- [ ] Welcome page live for anonymous users

### Milestone 2: Agent System MVP (End Sprint 2)
- [ ] Users can create agents
- [ ] Chat functional with KB context
- [ ] Messages persist across sessions
- [ ] Tier limits enforced
- [ ] Agent cards display correctly

### Milestone 3: Enhanced UX (End Sprint 3)
- [ ] MeepleCard WCAG compliant
- [ ] Smart tooltips working
- [ ] Collection limits enforced
- [ ] /agents page live
- [ ] Mobile optimization complete

### Milestone 4: Complete Agent Experience (End Sprint 4)
- [ ] Chat history with search
- [ ] Multiple resume methods
- [ ] Agents on game pages
- [ ] New navbar structure
- [ ] Anonymous catalog restrictions

### Milestone 5: Transparency & Communication (End Sprint 5)
- [ ] PDF status visible everywhere
- [ ] Real-time SSE updates
- [ ] Error handling with retry
- [ ] Notification dropdown
- [ ] Settings accessible

### Milestone 6: Full Feature Set (End Sprint 6)
- [ ] Notification management complete
- [ ] User-configurable channels
- [ ] Custom strategy builder (Enterprise)
- [ ] All features implemented

### Milestone 7: Quality Assured (End Sprint 7)
- [ ] Test coverage >85%
- [ ] Performance targets met
- [ ] Zero critical bugs
- [ ] Accessibility verified
- [ ] Security review passed

### Milestone 8: Production Ready (End Sprint 8)
- [ ] Documentation complete
- [ ] User onboarding polished
- [ ] Support team trained
- [ ] Deployed to production
- [ ] Monitoring dashboards live

---

## 📞 Communication Plan

### Daily Stand-ups (15 min)
- What did you complete yesterday?
- What will you work on today?
- Any blockers?

### Sprint Planning (2 hours)
- Review sprint goals
- Assign issues to developers
- Estimate capacity vs workload

### Sprint Review (1 hour)
- Demo completed features
- Stakeholder feedback
- Adjust priorities for next sprint

### Sprint Retrospective (1 hour)
- What went well?
- What could improve?
- Action items for next sprint

### Weekly Status Report
- To: Product Manager, Stakeholders
- Format: Progress %, blockers, risks, next week goals

---

## 🔧 Technical Debt Management

### Created During Implementation
**Acceptable**:
- Temporary feature flags (removed post-rollout)
- Console.log debugging (removed before merge)
- Hardcoded test data (replaced with fixtures)

**Must Fix Before Release**:
- TODO comments in production code
- Disabled tests
- Performance workarounds
- Security bypasses

### Tracking
- Label: `tech-debt` on GitHub issues
- Monthly review: Prioritize high-impact debt
- Reserve 10% sprint capacity for debt reduction

---

## 📖 Knowledge Transfer

### Documentation Artifacts
1. **Architecture Decision Records (ADRs)**: Why we chose SSE over WebSockets, etc.
2. **API Documentation**: OpenAPI specs for all new endpoints
3. **Component Library**: Storybook for MeepleCard variants
4. **User Guides**: How to create agents, use chat, manage notifications
5. **Admin Guides**: Tier management, strategy approval workflows
6. **Runbooks**: Deployment, rollback, troubleshooting procedures

### Training Sessions
- **Week 15**: Developer handoff (architecture deep dive)
- **Week 16**: Support team training (common issues, escalation)
- **Week 16**: Product team demo (feature walkthrough)

---

## ✅ Definition of Done

### Feature-Level
- [ ] All acceptance criteria met
- [ ] Unit tests written (>90% coverage)
- [ ] Integration tests written
- [ ] E2E test for critical path
- [ ] Code reviewed by 2 developers
- [ ] Design reviewed by designer
- [ ] Accessibility tested (manual + automated)
- [ ] Performance tested (Lighthouse >90)
- [ ] Documentation updated
- [ ] Deployed to staging
- [ ] Product Manager approval

### Sprint-Level
- [ ] All sprint issues completed or moved
- [ ] Sprint goals achieved
- [ ] Demo completed
- [ ] Retrospective action items documented
- [ ] Next sprint planned

### Release-Level
- [ ] All features tested end-to-end
- [ ] Security review passed
- [ ] Performance benchmarks met
- [ ] Documentation complete
- [ ] Monitoring dashboards configured
- [ ] Rollback plan tested
- [ ] Stakeholder sign-off
- [ ] Deployed to production

---

**Document Version**: 1.0
**Last Updated**: 2026-02-11
**Next Review**: End of Sprint 1 (2026-02-25)
