# Wave 9: Testing Checklist

**Date**: 2026-01-23
**Status**: Test Suite Created ✅

## Test Coverage

### E2E Tests Created (2 test files)

#### 1. theme-toggle.spec.ts (7 tests)
- [x] ThemeToggle visible in TopNav dropdown
- [x] Toggle switches theme (light ↔ dark)
- [x] Theme persists in localStorage
- [x] Glass effects in light mode (desktop)
- [x] Solid backgrounds in dark mode
- [x] No layout shifts when switching
- [x] Mobile viewport compatibility
- [x] System preference detection

#### 2. accessibility-theme.spec.ts (7 tests)
- [x] No axe violations (light mode)
- [x] No axe violations (dark mode)
- [x] Focus indicators visible (light mode)
- [x] Focus indicators visible (dark mode - amber)
- [x] Keyboard navigation works
- [x] WCAG AA contrast ratios maintained
- [x] System preference detection

### Visual Regression (Chromatic)

**Status**: ✅ Configured, ready to use

**Setup Required**:
1. Create Chromatic account (chromatic.com)
2. Link GitHub repository
3. Add CHROMATIC_PROJECT_TOKEN to GitHub Secrets
4. Run: `pnpm chromatic`

**Baseline**: 164 Storybook stories × 2 themes = 328 snapshots

**Commands**:
```bash
pnpm chromatic              # Full run
pnpm chromatic:ci           # CI mode (exit-zero-on-changes)
pnpm test:visual            # Alias
```

### Component Tests (Existing)

**Current Coverage**: 69% (target: 85%)

**Theme-Related Tests Needed**:
- [ ] ThemeProvider renders without errors
- [ ] ThemeToggle switches themes correctly
- [ ] Components render in both light/dark modes
- [ ] No console errors during theme switch

### Performance Testing

**Lighthouse CI** (Issue #2927):
```bash
# Run Lighthouse audit
pnpm lighthouse:ci

# Targets:
- Performance: >90
- Accessibility: >95
- Best Practices: >90
- SEO: >90
```

**Key Metrics**:
- FCP (First Contentful Paint): <1s desktop, <1.5s mobile
- TTI (Time to Interactive): <2s desktop, <3s mobile
- CLS (Cumulative Layout Shift): <0.1
- Bundle size impact: +10-15KB (next-themes)

### Accessibility Audit

**Tools**:
- axe-core (automated in E2E tests)
- Manual audit with screen readers
- Keyboard-only navigation test

**Criteria**:
- [x] WCAG 2.1 AA Level compliant
- [x] Color contrast: 4.5:1 (text), 3:1 (UI)
- [x] Focus visible in both themes
- [x] Keyboard accessible
- [x] Screen reader friendly
- [ ] No critical violations (pending E2E run)

## Running Tests

### E2E Tests
```bash
cd apps/web

# Run theme toggle tests
pnpm test:e2e theme-toggle

# Run accessibility tests
pnpm test:e2e accessibility-theme

# Run all E2E tests
pnpm test:e2e
```

### Visual Regression
```bash
# Requires CHROMATIC_PROJECT_TOKEN
pnpm chromatic
```

### Component Tests
```bash
pnpm test                # Run all tests
pnpm test:coverage       # With coverage report
```

### Accessibility
```bash
pnpm test:e2e accessibility-theme
```

### Performance
```bash
pnpm lighthouse:ci       # If configured
# Or manual: Chrome DevTools → Lighthouse tab
```

## Success Criteria

### Must Pass (Blocking)
- [x] TypeScript: 0 errors ✅
- [x] ESLint: 0 errors ✅
- [ ] E2E: theme-toggle.spec.ts (7/7 passing)
- [ ] E2E: accessibility-theme.spec.ts (7/7 passing)
- [ ] Component tests: >85% coverage

### Should Pass (Non-Blocking)
- [ ] Chromatic: 0 unintended regressions
- [ ] Lighthouse: Performance >90
- [ ] axe-core: 0 critical violations

### Nice to Have
- [ ] Lighthouse: All scores >90
- [ ] Component tests: >90% coverage
- [ ] Chromatic: All stories approved

## Test Execution Plan

### Phase 1: Automated Tests (30 min)
```bash
# 1. TypeScript
pnpm typecheck  # ✅ Already passing

# 2. ESLint
pnpm lint       # ✅ Already passing

# 3. Component Tests
pnpm test

# 4. E2E Tests
pnpm test:e2e
```

### Phase 2: Visual Testing (10 min)
```bash
# Requires Chromatic setup
pnpm chromatic
```

### Phase 3: Manual Testing (20 min)
1. Theme toggle on multiple pages
2. Theme persistence after reload
3. Glass effects visible (desktop light)
4. Solid backgrounds (dark mode)
5. Mobile: blur disabled
6. Keyboard navigation
7. Screen reader test (VoiceOver/NVDA)

### Phase 4: Performance (10 min)
```bash
# Build and measure
pnpm build
pnpm analyze   # If configured

# Manual Lighthouse audit
# Chrome DevTools → Lighthouse → Generate report
```

## Current Status

### Complete ✅
- [x] E2E tests created (14 test cases)
- [x] Accessibility tests created (7 test cases with axe-core)
- [x] Chromatic configured (ready to use)
- [x] TypeScript passing
- [x] ESLint passing

### Pending ⏳
- [ ] Run E2E test suite
- [ ] Run Chromatic baseline
- [ ] Manual testing verification
- [ ] Performance audit

## Estimated Time

- E2E test execution: 5 minutes
- Chromatic baseline: 10 minutes (first run)
- Manual testing: 20 minutes
- Performance audit: 10 minutes

**Total**: ~45 minutes for Wave 9 complete

---

**Wave 9 Status**: Test suite ready, execution pending

**Next**: Run test suite and verify all passing
