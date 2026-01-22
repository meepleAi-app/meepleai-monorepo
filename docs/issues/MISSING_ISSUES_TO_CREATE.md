# 🆕 Missing Issues to Create - Gap Analysis Action Items

**Priority**: Address CRITICAL blockers first (Admin Testing + Infrastructure)
**Timeline**: Create all issues in Week 0 (Prep week before execution)

---

## 🔴 CRITICAL PRIORITY: Admin Dashboard Testing (Phase 1)

### Category: Backend Testing
**Epic**: Admin Dashboard Testing (Phase 1)
**Complexity**: Medium-High
**Total Issues**: 2

#### Issue 1: Backend Unit Tests - Admin Dashboard
```markdown
Title: [Admin Dashboard] Backend Unit Tests - GetAdminMetricsQueryHandler & GetServiceHealthQueryHandler

Epic: Admin Dashboard Testing (Phase 1)

Description:
Comprehensive unit tests for Admin Dashboard backend query handlers.

Acceptance Criteria:
- [ ] Unit tests for GetAdminMetricsQueryHandler
  - [ ] Test metric aggregation logic
  - [ ] Test date range filtering
  - [ ] Test error handling
- [ ] Unit tests for GetServiceHealthQueryHandler
  - [ ] Test service status mapping
  - [ ] Test health check response parsing
  - [ ] Test timeout scenarios
- [ ] Coverage: 90%+ on handlers
- [ ] All tests passing in isolation
- [ ] Fast execution (<5s total)

Labels: kind/test, area/backend, admin, phase-1, priority/critical
Complexity: Medium (M) - 6-8 hours
```

#### Issue 2: Backend Integration Tests - Admin Dashboard
```markdown
Title: [Admin Dashboard] Backend Integration Tests with Testcontainers

Epic: Admin Dashboard Testing (Phase 1)

Description:
Integration tests for Admin Dashboard with real DB and cache using Testcontainers.

Acceptance Criteria:
- [ ] Integration tests with PostgreSQL (Testcontainers)
- [ ] Integration tests with Redis cache (Testcontainers)
- [ ] Test caching behavior (HybridCache)
  - [ ] First call hits DB, second hits cache
  - [ ] Cache expiration works correctly
  - [ ] Cache invalidation on data changes
- [ ] Test full query → handler → DB → response flow
- [ ] Coverage: 85%+ on integration scenarios
- [ ] All tests passing with real dependencies

Labels: kind/test, area/backend, area/db, admin, phase-1, priority/critical
Complexity: Medium (M) - 6-8 hours
```

---

### Category: Frontend Testing
**Total Issues**: 2

#### Issue 3: Frontend Component Tests - Admin Dashboard
```markdown
Title: [Admin Dashboard] Frontend Component Tests - MetricCard, ServiceHealthCard, ActivityFeed

Epic: Admin Dashboard Testing (Phase 1)

Description:
Component tests for all Admin Dashboard React components using Vitest + Testing Library.

Acceptance Criteria:
- [ ] Component tests for MetricCard
  - [ ] Renders correctly with mock data
  - [ ] Displays metric value and trend
  - [ ] Click navigation works
- [ ] Component tests for ServiceHealthCard
  - [ ] Renders all service states (healthy, degraded, down)
  - [ ] Visual indicators match status
  - [ ] Click → detail modal interaction
- [ ] Component tests for ActivityFeed
  - [ ] Renders activity list
  - [ ] Filtering (All vs Errors) works
  - [ ] Real-time update simulation
- [ ] Snapshot tests for all components
- [ ] Coverage: 85%+ on components
- [ ] All tests passing

Labels: kind/test, area/ui, frontend, admin, phase-1, priority/critical
Complexity: Medium (M) - 6-8 hours
```

#### Issue 4: Frontend Integration Tests - Admin Dashboard
```markdown
Title: [Admin Dashboard] Frontend Integration Tests - Dashboard API Integration

Epic: Admin Dashboard Testing (Phase 1)

Description:
Integration tests for Admin Dashboard data fetching with MSW mocks.

Acceptance Criteria:
- [ ] Test dashboard data fetching with TanStack Query
- [ ] Test loading states
- [ ] Test error states (API failure, network error)
- [ ] Test real-time polling (30s interval simulation)
- [ ] Test cache behavior (stale-while-revalidate)
- [ ] MSW handlers for all admin endpoints
- [ ] Coverage: 80%+ on API integration layer
- [ ] All tests passing

Labels: kind/test, area/ui, frontend, admin, phase-1, priority/critical
Complexity: Medium (M) - 5-6 hours
```

---

### Category: E2E Testing
**Total Issues**: 1

#### Issue 5: E2E Tests - Admin Dashboard User Journeys
```markdown
Title: [Admin Dashboard] E2E Tests - Admin Login, Metrics, Service Health, Activity Feed

Epic: Admin Dashboard Testing (Phase 1)

Description:
End-to-end tests for critical admin dashboard user journeys using Playwright.

Acceptance Criteria:
- [ ] Journey 1: Admin login → Dashboard loads
  - [ ] Login with admin credentials
  - [ ] Dashboard page loads with metrics
  - [ ] All cards visible
- [ ] Journey 2: Metric card drill-down navigation
  - [ ] Click metric card
  - [ ] Navigate to detail page
  - [ ] Verify correct data displayed
- [ ] Journey 3: Service health click → details
  - [ ] Click service health card
  - [ ] Detail modal/page opens
  - [ ] Service logs displayed
- [ ] Journey 4: Activity feed filtering
  - [ ] Filter All → Errors
  - [ ] Verify filter works
  - [ ] Reset filter
- [ ] Journey 5: Quick action buttons
  - [ ] Click "Add User" → Navigation works
  - [ ] Click "System Config" → Navigation works
- [ ] Journey 6: Real-time updates
  - [ ] Wait 30s (polling interval)
  - [ ] Verify metrics updated
  - [ ] Verify activity feed updated
- [ ] All tests passing
- [ ] Screenshots captured for visual regression

Labels: kind/test, e2e, admin, phase-1, priority/critical
Complexity: Large (L) - 8-10 hours
```

---

### Category: Visual & Performance Testing
**Total Issues**: 2

#### Issue 6: Visual Regression Tests - Admin Dashboard
```markdown
Title: [Admin Dashboard] Visual Regression Testing with Playwright Screenshots

Epic: Admin Dashboard Testing (Phase 1)

Description:
Setup visual regression testing for Admin Dashboard using Playwright screenshots.

Acceptance Criteria:
- [ ] Baseline screenshots for all Admin Dashboard states
  - [ ] Dashboard default state
  - [ ] Dashboard with errors
  - [ ] Dashboard with loading states
  - [ ] Service health - all states (healthy, degraded, down)
  - [ ] Activity feed - All vs Errors filter
- [ ] Screenshot comparison workflow
- [ ] CI integration (fail on visual changes)
- [ ] Threshold configuration (allow 0.1% diff)
- [ ] Documentation on updating baselines
- [ ] All baseline screenshots approved

Note: If #2852 covers this, expand scope or mark as duplicate.

Labels: kind/test, visual-regression, admin, phase-1, priority/critical
Complexity: Medium (M) - 4-6 hours
```

#### Issue 7: Performance Testing - Admin Dashboard (Lighthouse)
```markdown
Title: [Admin Dashboard] Performance Testing with Lighthouse CI

Epic: Admin Dashboard Testing (Phase 1)

Description:
Performance audit and baseline for Admin Dashboard using Lighthouse CI.

Acceptance Criteria:
- [ ] Lighthouse CI configured for admin pages
- [ ] Baseline audit completed
  - [ ] Performance score: 90+
  - [ ] Accessibility score: 90+
  - [ ] Best Practices score: 90+
  - [ ] SEO score: 90+
- [ ] Core Web Vitals measured
  - [ ] First Contentful Paint < 1.8s
  - [ ] Largest Contentful Paint < 2.5s
  - [ ] Cumulative Layout Shift < 0.1
  - [ ] Total Blocking Time < 300ms
- [ ] CI integration (fail on score drop)
- [ ] Performance budget defined
- [ ] Documentation on performance optimization

Labels: kind/test, performance, admin, phase-1, priority/critical
Complexity: Medium (M) - 5-6 hours
```

---

### Category: Load Testing
**Total Issues**: 1

#### Issue 8: Load Testing - Admin Dashboard (30s Polling)
```markdown
Title: [Admin Dashboard] Load Testing - 100 Concurrent Admins with 30s Polling

Epic: Admin Dashboard Testing (Phase 1)

Description:
Load testing for Admin Dashboard real-time polling scenario using k6.

Acceptance Criteria:
- [ ] k6 load test script created
- [ ] Scenario: 100 concurrent admin users
  - [ ] Each user polls every 30s
  - [ ] Simulate realistic metric queries
  - [ ] Simulate service health checks
- [ ] Performance metrics collected
  - [ ] Response time p95 < 500ms
  - [ ] Response time p99 < 1s
  - [ ] Error rate < 1%
  - [ ] CPU usage < 70%
  - [ ] Memory usage < 80%
- [ ] Caching effectiveness validated
- [ ] Bottlenecks identified and documented
- [ ] CI integration (smoke test on every PR)

Labels: kind/test, load-testing, admin, phase-1, priority/critical
Complexity: Large (L) - 8-10 hours
```

---

## 🔴 CRITICAL PRIORITY: Test Infrastructure

### Category: Infrastructure Setup
**Total Issues**: 5

#### Issue 9: Playwright Configuration & Best Practices
```markdown
Title: [Infrastructure] Playwright Configuration, Page Objects, and Best Practices

Epic: Test Infrastructure

Description:
Setup Playwright with optimal configuration, page object pattern, and best practices.

Acceptance Criteria:
- [ ] Playwright config for all browsers (Chromium, Firefox, WebKit)
- [ ] Page Object Model (POM) pattern implemented
  - [ ] Base page class
  - [ ] Admin dashboard page objects
  - [ ] Authentication page objects
- [ ] Test helpers and utilities
  - [ ] Login helper
  - [ ] Data seeding helper
  - [ ] Screenshot helper
- [ ] Parallel execution configuration
- [ ] Retry logic for flaky tests
- [ ] Timeout configuration
- [ ] Documentation: Playwright best practices
- [ ] Example tests demonstrating patterns

Labels: kind/infra, testing, infrastructure, priority/critical
Complexity: Medium (M) - 6-8 hours
```

#### Issue 10: Testcontainers Optimization & Parallel Execution
```markdown
Title: [Infrastructure] Testcontainers Optimization for Parallel Test Execution

Epic: Test Infrastructure

Description:
Optimize Testcontainers for fast parallel test execution with container reuse.

Acceptance Criteria:
- [ ] Testcontainers configuration optimized
  - [ ] Container reuse enabled
  - [ ] Singleton pattern for DB containers
  - [ ] Fast startup with cached images
- [ ] Parallel test execution working
  - [ ] Isolated test databases
  - [ ] No cross-test contamination
  - [ ] Cleanup after tests
- [ ] Performance improvements measured
  - [ ] Baseline: Full test suite time
  - [ ] Target: 50% faster with parallelization
- [ ] Documentation: Testcontainers best practices
- [ ] CI configuration updated for parallel execution

Labels: kind/infra, testing, testcontainers, priority/critical
Complexity: Medium (M) - 6-8 hours
```

#### Issue 11: CI/CD Test Pipeline for All Epics
```markdown
Title: [Infrastructure] CI/CD Test Pipeline - Unit, Integration, E2E, Visual, Performance

Epic: Test Infrastructure

Description:
Comprehensive CI/CD pipeline for all test types across all Epics.

Acceptance Criteria:
- [ ] GitHub Actions workflow created
- [ ] Pipeline stages:
  - [ ] Stage 1: Lint + TypeCheck (fail fast)
  - [ ] Stage 2: Backend unit tests
  - [ ] Stage 3: Frontend unit tests
  - [ ] Stage 4: Backend integration tests (Testcontainers)
  - [ ] Stage 5: Frontend integration tests (MSW)
  - [ ] Stage 6: E2E tests (Playwright)
  - [ ] Stage 7: Visual regression (Playwright screenshots)
  - [ ] Stage 8: Performance audit (Lighthouse CI)
- [ ] Parallel execution where possible
- [ ] Test result reporting (PR comments)
- [ ] Coverage reporting (Codecov or similar)
- [ ] Artifact upload (screenshots, reports)
- [ ] Slack notifications on failure
- [ ] Documentation: CI/CD pipeline guide

Labels: kind/infra, ci-cd, testing, priority/critical
Complexity: Large (L) - 10-12 hours
```

#### Issue 12: Test Infrastructure Documentation
```markdown
Title: [Infrastructure] Comprehensive Test Infrastructure Documentation

Epic: Test Infrastructure

Description:
Documentation for all test infrastructure, patterns, and best practices.

Acceptance Criteria:
- [ ] Testing guide created in docs/
  - [ ] Test types overview (unit, integration, e2e)
  - [ ] Running tests locally
  - [ ] Writing new tests (with examples)
  - [ ] Page Object Model guide
  - [ ] Testcontainers usage
  - [ ] MSW mock server usage
- [ ] Best practices documented
  - [ ] Test naming conventions
  - [ ] Test organization
  - [ ] Isolation and cleanup
  - [ ] Performance optimization
- [ ] CI/CD pipeline documentation
  - [ ] Pipeline stages explained
  - [ ] How to debug CI failures
  - [ ] Adding new test jobs
- [ ] Troubleshooting guide
  - [ ] Common issues and solutions
  - [ ] Flaky tests handling
  - [ ] Container issues
- [ ] Documentation reviewed by team

Labels: kind/documentation, testing, infrastructure, priority/high
Complexity: Medium (M) - 6-8 hours
```

#### Issue 13: Visual Regression Setup - Expand or Clarify #2852
```markdown
Title: [Infrastructure] Visual Regression Testing Setup - Clarify Scope and Expand

Epic: Test Infrastructure

Description:
Clarify scope of existing #2852 or expand to cover all pages (not just Admin).

Action:
- [ ] Review #2852 body and scope
- [ ] If Admin-only: Keep as-is, create separate issues for other pages
- [ ] If general: Expand to cover all 7 Epics
- [ ] Ensure baseline screenshots for:
  - [ ] Admin Dashboard
  - [ ] User Dashboard
  - [ ] Personal Library
  - [ ] Shared Catalog
  - [ ] Profile & Settings
  - [ ] User Management
  - [ ] Editor Dashboard

Labels: kind/infra, visual-regression, testing, priority/high
Complexity: Small (S) - 2-3 hours (review and decision)
```

---

## 🟡 HIGH PRIORITY: Component Library & Design System

### Category: Component Library
**Total Issues**: 3

#### Issue 14: Storybook Setup & Component Library Foundation
```markdown
Title: [Component Library] Storybook Setup and Foundation

Epic: Component Library & Design System

Description:
Setup Storybook for component library with base configuration and first components.

Acceptance Criteria:
- [ ] Storybook 8 installed and configured
- [ ] Tailwind CSS integration working
- [ ] shadcn/ui components documented
- [ ] Base stories created:
  - [ ] Button variants
  - [ ] Input components
  - [ ] Card components
  - [ ] Modal/Dialog
- [ ] Story organization by category
- [ ] Accessibility checks enabled (a11y addon)
- [ ] Responsive viewport addon configured
- [ ] Documentation: How to add new stories
- [ ] Storybook deployed (Chromatic or static)

Labels: kind/feature, component-library, frontend, storybook, priority/high
Complexity: Medium (M) - 8-10 hours
```

#### Issue 15: Extract Reusable Components from Admin Dashboard
```markdown
Title: [Component Library] Extract Reusable Components from Admin Dashboard

Epic: Component Library & Design System

Description:
Extract and generalize reusable components from Admin Dashboard for component library.

Acceptance Criteria:
- [ ] Identify reusable components in Admin Dashboard
  - [ ] MetricCard → Generic StatCard
  - [ ] ServiceHealthCard → Generic StatusCard
  - [ ] ActivityFeed → Generic ActivityList
  - [ ] QuickActions → Generic ActionGrid
- [ ] Extract components to components/ui/
- [ ] Generalize props and styling
- [ ] Create Storybook stories for each
- [ ] Update Admin Dashboard to use library components
- [ ] Document component APIs
- [ ] All tests passing after refactor

Labels: kind/refactor, component-library, frontend, priority/high
Complexity: Large (L) - 12-15 hours
```

#### Issue 16: Design System Documentation (Figma → Storybook)
```markdown
Title: [Component Library] Design System Documentation - Typography, Colors, Spacing

Epic: Component Library & Design System

Description:
Document design system tokens (colors, typography, spacing) from Figma to Storybook.

Acceptance Criteria:
- [ ] Design tokens documented in Storybook
  - [ ] Color palette with semantic naming
  - [ ] Typography scale (headings, body, labels)
  - [ ] Spacing scale (4px base)
  - [ ] Border radius values
  - [ ] Shadow values
- [ ] Tailwind config documented
- [ ] Component composition examples
- [ ] Responsive breakpoints documented
- [ ] Accessibility guidelines
- [ ] Link to Figma design files
- [ ] Documentation reviewed by design team

Labels: kind/documentation, design-system, component-library, priority/medium
Complexity: Medium (M) - 6-8 hours
```

---

## 🟢 MEDIUM PRIORITY: Performance & Quality

### Category: Performance & Quality
**Total Issues**: 3

#### Issue 17: Performance Testing Infrastructure (Lighthouse CI)
```markdown
Title: [Infrastructure] Performance Testing Infrastructure with Lighthouse CI

Epic: Performance & Quality

Description:
Setup Lighthouse CI for all pages with performance budgets and CI integration.

Acceptance Criteria:
- [ ] Lighthouse CI configured for all 7 pages
  - [ ] Admin Dashboard
  - [ ] User Dashboard
  - [ ] Personal Library
  - [ ] Shared Catalog
  - [ ] Profile & Settings
  - [ ] User Management
  - [ ] Editor Dashboard
- [ ] Performance budgets defined
  - [ ] FCP < 1.8s
  - [ ] LCP < 2.5s
  - [ ] TBT < 300ms
  - [ ] CLS < 0.1
- [ ] CI integration (fail on budget exceeded)
- [ ] Performance reports in PR comments
- [ ] Documentation: Performance optimization guide

Labels: kind/infra, performance, testing, priority/medium
Complexity: Medium (M) - 6-8 hours
```

#### Issue 18: Load Testing Setup & Scenarios (k6)
```markdown
Title: [Infrastructure] Load Testing Setup with k6 for All APIs

Epic: Performance & Quality

Description:
Setup k6 load testing framework with realistic scenarios for all API endpoints.

Acceptance Criteria:
- [ ] k6 installed and configured
- [ ] Load test scenarios created:
  - [ ] Scenario 1: User dashboard polling (30s interval)
  - [ ] Scenario 2: Library browsing with pagination
  - [ ] Scenario 3: Catalog search with filters
  - [ ] Scenario 4: Concurrent admin actions
- [ ] Performance baselines established
  - [ ] Response time p95, p99
  - [ ] Throughput (requests/sec)
  - [ ] Error rate
- [ ] CI integration (smoke test on PR)
- [ ] Documentation: Load testing guide
- [ ] Grafana dashboard for results

Labels: kind/infra, load-testing, performance, priority/medium
Complexity: Large (L) - 10-12 hours
```

#### Issue 19: Accessibility Audit & WCAG 2.1 AA Compliance
```markdown
Title: [Quality] Accessibility Audit & WCAG 2.1 AA Compliance for All Pages

Epic: Performance & Quality

Description:
Comprehensive accessibility audit for all pages with WCAG 2.1 AA compliance.

Acceptance Criteria:
- [ ] Accessibility audit completed for all 7 pages
  - [ ] Automated testing with axe-core
  - [ ] Manual keyboard navigation testing
  - [ ] Screen reader testing (NVDA/JAWS)
- [ ] WCAG 2.1 AA compliance achieved
  - [ ] All color contrasts meet 4.5:1 minimum
  - [ ] All interactive elements keyboard accessible
  - [ ] Focus indicators visible
  - [ ] Alt text for all images
  - [ ] ARIA labels where needed
- [ ] Accessibility issues documented and fixed
- [ ] CI integration (axe-core in tests)
- [ ] Documentation: Accessibility guidelines

Labels: kind/quality, accessibility, a11y, priority/medium
Complexity: Large (L) - 12-15 hours
```

---

## 📋 Summary

### Issues to Create by Priority

| Priority | Category | Count | Estimated Hours |
|----------|----------|-------|-----------------|
| 🔴 CRITICAL | Admin Dashboard Testing | 8 | 48-60 hours |
| 🔴 CRITICAL | Test Infrastructure | 5 | 28-36 hours |
| 🟡 HIGH | Component Library | 3 | 26-33 hours |
| 🟢 MEDIUM | Performance & Quality | 3 | 28-35 hours |
| **TOTAL** | | **19** | **130-164 hours** |

### Estimated Timeline with 2-3 Developers
- **Week 0 (Prep)**: Create all 19 issues (~4 hours)
- **Week 1-2 (Phase 1)**: Admin Testing + Infrastructure (8+5 = 13 issues)
  - With 2 devs: 2 weeks
  - With 3 devs: 1.5 weeks
- **Week 3-4 (Parallel)**: Component Library (3 issues) + Start Epic 2
- **Week 7-8 (Quality)**: Performance & Quality (3 issues)

---

## ✅ Action Checklist

### This Week (Week 0)
- [ ] Review this document with team
- [ ] Create 8 Admin Dashboard Testing issues (#1-8)
- [ ] Create 5 Test Infrastructure issues (#9-13)
- [ ] Create 3 Component Library issues (#14-16)
- [ ] Create 3 Performance & Quality issues (#17-19)
- [ ] Update IMPLEMENTATION_ROADMAP.md to reflect new Phase 0
- [ ] Link all issues to appropriate Epics

### Next Week (Week 1)
- [ ] Assign Week 1-2 issues to developers
- [ ] Begin Admin Dashboard testing implementation
- [ ] Begin infrastructure setup in parallel
- [ ] Daily standup to track progress

---

**Document Created**: 2026-01-22
**Total New Issues**: 19
**Priority Focus**: Admin Testing (8) + Infrastructure (5) = 13 CRITICAL issues
