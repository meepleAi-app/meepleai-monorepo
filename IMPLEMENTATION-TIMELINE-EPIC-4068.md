# Epic #4068: Detailed Implementation Timeline

**Realistic breakdown with dependencies, parallel tracks, and milestones**

---

## Timeline Overview

**Optimistic (Parallelized)**: 2-3 weeks (10-15 business days)
**Realistic**: 4-5 weeks (20-25 business days)
**Conservative**: 6-7 weeks (30-35 business days)

**Team Size Assumed**: 2-3 developers + 1 QA + 1 DevOps

---

## Week 1: Foundation & Parallel Tracks

### Day 1 (Monday): Permission Data Model (#4177 - Start)

**Developer 1** (Backend - Critical Path):
- [ ] 09:00-10:00: Fix namespace compilation errors
  - Update all `MeepleAI.Api.*` → `Api.*`
  - Fix ApplicationDbContext → MeepleAiDbContext
  - Resolve UserAccountStatus using statements
- [ ] 10:00-12:00: Complete Permission.cs, PermissionRegistry.cs
  - Verify logic (OR/AND checks)
  - Add state-based permission support
  - Unit tests (PermissionTests.cs)
- [ ] 13:00-15:00: Query handlers (GetUserPermissions, CheckPermission)
  - Implement handlers with MeepleAiDbContext
  - Integration tests
- [ ] 15:00-17:00: API endpoints + DI registration
  - Verify endpoints accessible
  - Test with curl
  - Swagger docs updated

**Developer 2** (Frontend - Parallel Track - #4186 Tooltip):
- [ ] 09:00-11:00: Implement calculateOptimalPosition() algorithm
  - Viewport boundary detection
  - Auto-flip logic (top/bottom/left/right)
  - Unit tests (< 16ms requirement)
- [ ] 11:00-13:00: useSmartTooltip() hook
  - Debounced scroll/resize
  - RAF for smooth updates
- [ ] 14:00-17:00: SmartTooltip component
  - Integration with positioning hook
  - Visual testing

**Developer 3** (Frontend - Parallel Track - #4181 Tags):
- [ ] 09:00-11:00: Tag types and presets
  - types/tags.ts
  - lib/tags/presets.ts (game/agent/document)
- [ ] 11:00-14:00: TagBadge, TagOverflow components
  - Icon + text rendering
  - Tooltip integration
- [ ] 14:00-17:00: TagStrip component
  - Left edge positioning
  - Max 3 + overflow logic
  - Storybook stories

**DevOps**:
- [ ] Setup CI/CD pipeline (.github/workflows/epic-4068-ci.yml)
- [ ] Configure test databases
- [ ] Prepare monitoring (Prometheus/Grafana)

**QA**:
- [ ] Review acceptance criteria for all 10 issues
- [ ] Prepare test data (users with different tiers/roles)
- [ ] Setup E2E test environment

**Daily Standup**: 08:45 (15 min)
**Code Review**: 16:00-17:00 (reviews from previous day)

---

### Day 2 (Tuesday): Permission Backend Completion (#4177 - Finish)

**Developer 1** (Backend):
- [ ] 09:00-10:30: Database migration script
  - AddUserAccountStatus migration
  - Test migration on dev database
  - Verify indexes created
- [ ] 10:30-12:00: Update User entity
  - Suspend/Ban/Unban methods
  - CanAuthenticate() uses Status
  - Domain events for tier/role changes
- [ ] 13:00-15:00: Backend tests to 90%+ coverage
  - Unit tests: Permission, UserTier, Role
  - Integration tests: Queries
  - Verify all scenarios passing
- [ ] 15:00-17:00: Code review prep & documentation
  - PR description for #4177
  - Update CHANGELOG.md
  - API documentation (Scalar)

**Developer 2** (Frontend - Tooltip #4186 - Continue):
- [ ] 09:00-12:00: Performance optimization
  - Verify < 16ms positioning
  - Debounce scroll/resize (100ms)
  - IntersectionObserver for visibility
- [ ] 13:00-15:00: Tests
  - Unit tests: positioning.test.ts
  - Performance tests (< 16ms assertions)
- [ ] 15:00-17:00: Documentation
  - Component API docs
  - Usage examples

**Developer 3** (Frontend - Tags #4181 - Continue):
- [ ] 09:00-12:00: Tag utilities
  - createTagsFromKeys()
  - sortTagsByPriority()
  - Entity-specific logic
- [ ] 13:00-16:00: Tests
  - TagStrip.test.tsx
  - TagBadge.test.tsx
  - Integration tests
- [ ] 16:00-17:00: Storybook polish
  - All variants
  - Dark mode
  - Responsive

**QA**:
- [ ] Test #4177 implementation (permission API endpoints)
- [ ] Verify Free/Pro/Admin user flows
- [ ] Document bugs found

**Milestone**: ✅ Issue #4177 complete, PR ready

---

### Day 3 (Wednesday): Permission Frontend & Accessibility

**Developer 1** (Backend - #4178 Permission Hooks - Start):
- [ ] 09:00-10:00: Review & merge #4177 PR
- [ ] 10:00-12:00: Start #4184 (Agent Metadata - Backend)
  - Agent status field (if needed)
  - Agent invocation tracking
  - Statistics queries
- [ ] 13:00-17:00: Backend support for #4184
  - Agent metadata API endpoints
  - Real-time status updates (prep for WebSocket)

**Developer 2** (Frontend - #4180 Tooltip Accessibility - Start):
- [ ] 09:00-12:00: AccessibleTooltip component
  - Keyboard navigation (Tab/Enter/Escape)
  - ARIA attributes (aria-describedby, role="tooltip")
  - Focus management
- [ ] 13:00-15:00: Mobile touch support
  - Tap-to-show (not hover)
  - Dismiss button for mobile
  - Touch device detection
- [ ] 15:00-17:00: WCAG 2.1 AA compliance
  - Color contrast verification (≥4.5:1)
  - Screen reader testing prep
  - axe-core audit

**Developer 3** (Frontend - #4178 Permission Hooks - Start):
- [ ] 09:00-11:00: Types (types/permissions.ts)
  - UserTier, UserRole, UserAccountStatus
  - UserPermissions interface
  - Helper functions (hasMinimumTier, isAdmin)
- [ ] 11:00-14:00: API client (lib/api/permissions.ts)
  - getUserPermissions()
  - checkPermission()
  - Error handling
- [ ] 14:00-17:00: PermissionContext
  - React context with usePermissions()
  - React Query integration
  - Caching (5min stale time)

**QA**:
- [ ] Test #4186 (tooltip positioning)
- [ ] Test #4181 (tag system)
- [ ] Accessibility audit (manual keyboard testing)

**Milestone**: ✅ Issues #4186, #4181 complete, #4178, #4180 in progress

---

### Day 4 (Thursday): Integration & Agent Metadata

**Developer 1** (Backend - #4184 Agent Metadata - Continue):
- [ ] 09:00-12:00: Agent statistics aggregation
  - Invocation count tracking
  - Average response time calculation
  - Last executed timestamp
- [ ] 13:00-17:00: Tests
  - Agent metadata query tests
  - Performance tests (stats aggregation)

**Developer 2** (Frontend - #4180 Accessibility - Complete):
- [ ] 09:00-11:00: Screen reader announcements
  - aria-live regions
  - Status messages
- [ ] 11:00-13:00: High contrast mode support
  - Windows High Contrast
  - CSS adjustments
- [ ] 13:00-15:00: Tests
  - Accessibility tests (axe-core)
  - Keyboard navigation E2E
- [ ] 15:00-17:00: Documentation & PR
  - WCAG checklist completion
  - PR for #4180

**Developer 3** (Frontend - #4178 Permission Hooks - Complete):
- [ ] 09:00-11:00: PermissionGate, TierGate components
- [ ] 11:00-13:00: App layout integration
  - Wrap app in PermissionProvider
  - Test permission loading
- [ ] 13:00-16:00: Tests
  - PermissionContext.test.tsx
  - PermissionGate.test.tsx
- [ ] 16:00-17:00: PR for #4178

**QA**:
- [ ] Accessibility full audit (#4180)
- [ ] WCAG 2.1 AA checklist verification
- [ ] Screen reader testing (NVDA, VoiceOver)

**Milestone**: ✅ #4180, #4178 complete and merged

---

### Day 5 (Friday): MeepleCard Integration

**All Developers** (Pair Programming - #4179 MeepleCard Permission Integration):
- [ ] 09:00-11:00: MeepleCard props update
  - Add agentMetadata, tags props
  - Permission-aware features (showWishlist, selectable, draggable)
- [ ] 11:00-13:00: Integration logic
  - Use usePermissions() hook
  - Filter quick actions by role/permission
  - Conditional feature rendering
- [ ] 14:00-16:00: Tests
  - MeepleCard integration tests
  - Permission combinations (Free/Pro/Admin)
  - Visual regression (Chromatic)
- [ ] 16:00-17:00: PR & code review

**QA**:
- [ ] Test complete permission flows
- [ ] Verify Free → Pro upgrade path
- [ ] Test Admin quick actions

**Milestone**: ✅ Week 1 complete - Permission system foundation ready

**Weekend**: Rest / Optional overtime if behind schedule

---

## Week 2: Feature Completion

### Day 6-7 (Monday-Tuesday): Remaining Integrations

**Developer 1** (Frontend - #4182 Tag Integration):
- [ ] Integrate TagStrip into MeepleCard variants
- [ ] Test all 5 variants (grid/list/compact/featured/hero)
- [ ] Responsive behavior
- [ ] PR & merge

**Developer 2** (Frontend - #4183 Collection Limits):
- [ ] CollectionLimitIndicator component
- [ ] Progress bars with color coding
- [ ] Warning states (75%, 90%)
- [ ] Integration into collection page
- [ ] Tests & PR

**Developer 3** (Frontend - #4184 Agent Metadata - Complete):
- [ ] AgentStatusBadge component
- [ ] AgentModelInfo component
- [ ] AgentStatsDisplay component
- [ ] Integration into MeepleCard (agent entity)
- [ ] Tests & PR

**DevOps**:
- [ ] Setup staging environment
- [ ] Apply migrations to staging
- [ ] Deploy current progress

**QA**:
- [ ] Test #4182, #4183, #4184
- [ ] Regression testing (ensure old features still work)

**Milestone**: ✅ All individual features complete

---

### Day 8-9 (Wednesday-Thursday): Integration Testing (#4185 - Start)

**All Developers** (Integration Testing):
- [ ] E2E test suite
  - Permission flows (Free/Normal/Pro/Enterprise)
  - Tag overflow scenarios
  - Tooltip positioning edge cases
  - Agent metadata display
  - Collection limits warnings
- [ ] Visual regression (Chromatic)
  - All MeepleCard variants
  - All entity types
  - Responsive breakpoints
- [ ] Accessibility audit
  - axe-core: 0 violations
  - Lighthouse: ≥95 score
  - Manual screen reader testing
- [ ] Performance tests
  - Tooltip positioning < 16ms
  - Card render < 100ms
  - Permission API < 100ms p95
  - Bundle size < 15KB impact

**DevOps**:
- [ ] Load testing (k6 script)
  - 200 concurrent users
  - Permission API stress test
- [ ] Security audit script
  - Run epic-4068-security-audit.sh
  - Fix any findings

**QA**:
- [ ] Complete E2E test scenarios (25+)
- [ ] Accessibility checklist 100%
- [ ] Performance benchmarks verified

---

### Day 10 (Friday): Documentation & Polish (#4185 - Finish)

**All Developers**:
- [ ] Update docs/frontend/components/meeple-card.md
- [ ] Create CHANGELOG entry
- [ ] Update README with Epic #4068 features
- [ ] Review all code comments
- [ ] Polish Storybook stories
- [ ] Create demo video/screenshots

**DevOps**:
- [ ] Production deployment prep
  - Terraform plan review
  - Nginx config review
  - Backup strategy verified

**QA**:
- [ ] Final smoke tests
- [ ] User acceptance testing scenarios
- [ ] Sign-off for deployment

**Milestone**: ✅ Week 2 complete - All 10 issues ready for production

---

## Week 3: Deployment & Stabilization

### Day 11 (Monday): Pre-Production Deploy

- [ ] 09:00: Team meeting - Deployment go/no-go decision
- [ ] 10:00-12:00: Deploy to staging
  - Apply database migrations
  - Deploy backend + frontend
  - Verify health checks
- [ ] 13:00-15:00: Staging smoke tests
  - All test accounts (Free/Pro/Admin)
  - Critical user flows
  - Performance checks
- [ ] 15:00-17:00: Fix any staging issues

---

### Day 12 (Tuesday): Production Deploy (Blue-Green)

- [ ] 09:00-10:00: Pre-deployment checklist review
- [ ] 10:00-11:00: Database backup
- [ ] 11:00-12:00: Apply production migration
- [ ] 13:00-14:00: Deploy Green environment
- [ ] 14:00-15:00: Smoke test Green
- [ ] 15:00-16:00: Switch 10% traffic to Green (canary)
- [ ] 16:00-17:00: Monitor metrics (10% rollout)

**After Hours**:
- [ ] 18:00: Increase to 50% traffic (if no issues)
- [ ] 20:00: Monitor metrics
- [ ] 22:00: Switch 100% to Green (if stable)

---

### Day 13-15 (Wed-Fri): Monitoring & Iteration

- [ ] Monitor production metrics (48 hours)
  - Permission API latency (p95 < 100ms)
  - Error rate (< 1%)
  - Tier upgrade conversions
  - User feedback
- [ ] Address production bugs (priority: critical)
- [ ] Performance tuning if needed
  - Add Redis caching if latency high
  - Database query optimization
- [ ] Collect user feedback
- [ ] Iterate on permission mappings if needed

**Milestone**: ✅ Production deployment successful, stable for 48 hours

---

### Day 16-20 (Week 4): Post-Launch

- [ ] Week 1 post-launch review
  - Metrics analysis
  - User feedback synthesis
  - Bug triage and prioritization
- [ ] Refinements based on feedback
- [ ] Documentation updates
- [ ] Knowledge transfer (team training)
- [ ] Close Epic #4068 🎉

---

## Dependency-Aware Schedule

### Critical Path (Sequential - Cannot Parallelize)

```
Day 1-2: #4177 (Permission Model) → Must complete first
Day 3-4: #4178 (Permission Hooks) → Depends on #4177
Day 5: #4179 (MeepleCard Perms) → Depends on #4178
Day 8-10: #4185 (Testing) → Depends on all above
```

**Total Critical Path**: 10 days minimum

---

### Parallel Tracks (Can Run Concurrently with Critical Path)

**Track A - Tooltip System**:
```
Day 1-2: #4186 (Positioning) → Independent
Day 3-4: #4180 (Accessibility) → Depends on #4186
```

**Track B - Tag System**:
```
Day 1-2: #4181 (Component) → Independent
Day 5: #4182 (Integration) → Depends on #4181
```

**Track C - Features**:
```
Day 2-3: #4184 (Agent Metadata) → Independent
Day 6-7: #4183 (Collection Limits) → Depends on #4178 (permission limits)
```

---

## Risk Mitigation

### High-Risk Items

**Risk 1**: Database migration fails in production
- **Mitigation**: Test on staging first, have rollback script ready
- **Contingency**: Rollback to Blue environment (< 5 minutes)

**Risk 2**: Backend compilation issues persist
- **Mitigation**: Allocate extra time Day 1 for namespace fixes
- **Contingency**: Simplify implementation (remove complex features)

**Risk 3**: Accessibility audit finds critical violations
- **Mitigation**: Run axe-core daily during development
- **Contingency**: Allocate Day 9-10 for accessibility fixes

**Risk 4**: Performance targets not met (tooltip > 16ms)
- **Mitigation**: Continuous performance monitoring
- **Contingency**: Simplify tooltip positioning algorithm, disable collision detection

---

## Velocity Tracking

**Story Points by Issue**:
- #4177: 8 pts (complex, critical path)
- #4178: 8 pts (complex, many components)
- #4179: 5 pts (integration, dependent)
- #4186: 8 pts (algorithm, performance)
- #4180: 5 pts (accessibility, testing)
- #4181: 8 pts (component with features)
- #4182: 3 pts (simple integration)
- #4183: 5 pts (UI indicators)
- #4184: 6 pts (multiple components)
- #4185: 8 pts (testing, documentation)

**Total**: 64 story points

**Team Velocity**: Assuming 15-20 points/week (3 devs)
- Optimistic: 64/20 = 3.2 weeks
- Realistic: 64/15 = 4.3 weeks
- **Estimate**: 4-5 weeks ✓ (aligns with original)

---

## Daily Checklist Template

### Every Day:

**Morning (08:45-09:00)**:
- [ ] Daily standup (15 min)
  - What did you complete yesterday?
  - What are you working on today?
  - Any blockers?

**During Day**:
- [ ] Commit frequently (atomic commits)
- [ ] Run tests before committing
- [ ] Update issue progress on GitHub
- [ ] Communicate blockers immediately

**End of Day (16:00-17:00)**:
- [ ] Code review (review others' PRs)
- [ ] Update issue status (in_progress, needs_review, done)
- [ ] Document decisions made
- [ ] Push all code (don't leave uncommitted changes)

**Evening** (Optional):
- [ ] Read tomorrow's tasks
- [ ] Prepare questions for standup

---

## Weekly Milestones

**Week 1 End**:
- ✅ #4177 (Permission Model) merged
- ✅ #4186 (Tooltip Positioning) merged
- ✅ #4181 (Tag Component) merged
- ✅ #4178 (Permission Hooks) in review
- ✅ #4184 (Agent Metadata) ~80% complete

**Week 2 End**:
- ✅ All 9 implementation issues merged
- ✅ #4185 (Testing) 80% complete
- ✅ Documentation updated
- ✅ Staging deployment successful

**Week 3 End**:
- ✅ #4185 (Testing) 100% complete
- ✅ Production deployment successful
- ✅ 48 hours stable monitoring
- ✅ User feedback positive

**Week 4 End**:
- ✅ All bugs addressed
- ✅ Performance optimized
- ✅ Epic #4068 closed 🎉

---

## Success Criteria by Week

**Week 1**: Foundation complete
- Permission backend functional
- Frontend components created
- Tests passing (unit level)

**Week 2**: Integration complete
- All features integrated in MeepleCard
- E2E tests passing
- Accessibility compliant

**Week 3**: Production ready
- Deployed to production
- Metrics green
- No critical bugs

**Week 4**: Epic complete
- User satisfaction >80%
- Tier upgrade conversions measured
- Epic closed

---

## Resource Allocation

### Developer Time (Total)

- Backend: ~80 hours (2 weeks × 40 hours)
- Frontend: ~120 hours (3 weeks × 40 hours)
- Testing: ~40 hours (QA full-time week 2-3)
- DevOps: ~20 hours (part-time throughout)
- **Total**: ~260 hours

### Infrastructure Costs

- Staging environment: $50/month
- Production caching (Redis): $20/month
- Monitoring (Grafana Cloud): $30/month
- Load testing (k6 Cloud): $25/month
- **Total**: ~$125/month

---

## Conclusion

**Epic #4068 Timeline**: 4-5 weeks realistic
- Week 1: Foundation (permissions, tags, tooltips in parallel)
- Week 2: Integration (MeepleCard, limits, agent metadata)
- Week 3: Deployment (staging → production with monitoring)
- Week 4: Stabilization (bug fixes, optimization, close epic)

**Critical Path**: 10 days minimum (permission system sequential)
**Parallel Tracks**: Save ~10 days (3 tracks running concurrently)

**With 2-3 developers + QA + DevOps**: 4-5 weeks total ✅

**Ready to start Day 1!** 🚀
