# Admin Dashboard Visual Regression Testing

**Issue**: #2916
**Implementation**: Playwright Screenshot Comparison
**Threshold**: 0.1% difference (0.001)
**Tool**: Playwright native `.toHaveScreenshot()` API

## Overview

Visual regression testing for Admin Dashboard ensures UI consistency across code changes by comparing screenshots of key dashboard states.

### Relationship with #2852

This implementation (#2916) complements Issue #2852's broader Chromatic visual testing:

- **#2916 (This)**: Playwright E2E screenshot validation for Admin Dashboard user journeys
- **#2852**: Chromatic Storybook component visual review across all 7 application areas

Together, they form a complete visual testing pyramid:
- **Component Level**: Chromatic (Storybook isolation)
- **E2E Level**: Playwright (Real user flows)

## Test Coverage

The visual regression suite covers 8 critical Admin Dashboard states:

1. **Dashboard Default State** - All metrics loaded, system healthy
2. **Dashboard Error State** - Service failures and API errors
3. **Dashboard Loading State** - Skeleton loaders and spinners
4. **Service Health - Healthy** - All services operational (green indicators)
5. **Service Health - Degraded** - Performance issues (yellow/orange indicators)
6. **Service Health - Down** - Service failures (red indicators)
7. **Activity Feed - All Filter** - All event types displayed
8. **Activity Feed - Errors Filter** - Only error events shown

## Running Tests

### Local Development

```bash
# Run visual regression tests (uses existing baselines)
cd apps/web
pnpm test:e2e:visual:admin

# Run with UI mode (interactive debugging)
pnpm test:e2e:visual:admin:ui

# Update baseline screenshots (after intentional UI changes)
pnpm test:e2e:visual:admin:update
```

### CI/CD

Visual regression tests run automatically on:
- Pull requests to `main`, `main-dev`, `frontend-dev`
- Pushes to `main`, `main-dev`
- Manual workflow dispatch

Tests are executed via `.github/workflows/visual-regression.yml` and are **non-blocking** during alpha phase (`continue-on-error: true`).

## Baseline Screenshot Management

### Directory Structure

```
apps/web/
├── e2e/
│   └── visual/
│       ├── admin-dashboard-visual.spec.ts      # Test file
│       └── admin-dashboard-visual.spec.ts-snapshots/
│       └── desktop-chrome/                 # Browser-specific snapshots
│           ├── dashboard-default.png
│           ├── dashboard-errors.png
│           ├── dashboard-loading.png
│           ├── service-health-healthy.png
│           ├── service-health-degraded.png
│           ├── service-health-down.png
│           ├── activity-feed-all.png
│           └── activity-feed-errors.png
```

### When to Update Baselines

Update baseline screenshots when making **intentional** UI changes:

✅ **Update baselines for**:
- Dashboard layout changes
- Color scheme updates
- Typography adjustments
- Component redesigns
- New dashboard features

❌ **Do NOT update baselines for**:
- Unintentional visual regressions
- Rendering bugs
- CSS mistakes
- Broken layouts

### How to Update Baselines

#### 1. Make UI Changes
```bash
# Edit dashboard components
code apps/web/src/app/admin/page.tsx
```

#### 2. Update Baselines Locally
```bash
cd apps/web
pnpm test:e2e:visual:admin:update
```

This command:
- Runs all visual tests
- Captures new screenshots
- Overwrites existing baselines in `e2e/admin-dashboard-visual.spec.ts-snapshots/`

#### 3. Review Changes
```bash
# Compare old vs new screenshots
git diff apps/web/e2e/admin-dashboard-visual.spec.ts-snapshots/
```

**Critical**: Manually review all screenshot changes before committing!

#### 4. Commit Baseline Updates
```bash
git add apps/web/e2e/admin-dashboard-visual.spec.ts-snapshots/
git commit -m "test(admin): update visual regression baselines for dashboard redesign"
```

#### 5. Create PR
Baseline updates should:
- Include clear explanation of UI changes
- Link to design tickets/mockups
- Pass visual regression tests in CI
- Be reviewed by design team (if applicable)

## Threshold Configuration

### Current Setting: 0.1% (0.001)

```typescript
// e2e/admin-dashboard-visual.spec.ts
await expect(page).toHaveScreenshot('dashboard-default.png', {
  fullPage: true,
  threshold: 0.001, // 0.1% difference allowed
});
```

### Why 0.1%?

- **Strict**: Catches tiny visual regressions
- **Realistic**: Allows for anti-aliasing variations between environments
- **Actionable**: Low false positive rate

### Adjusting Threshold

If experiencing frequent false positives (rare):

1. **Investigate First**: Ensure consistent rendering environment
2. **Adjust Specific Test**: Only increase threshold for problematic tests
3. **Document Reasoning**: Add comment explaining threshold adjustment

```typescript
// Example: Relaxed threshold for chart animations
await expect(page).toHaveScreenshot('dashboard-with-charts.png', {
  threshold: 0.002, // 0.2% - charts have rendering variations
});
```

## Troubleshooting

### Tests Failing in CI but Passing Locally

**Cause**: Font rendering differences between local and CI environment

**Solutions**:
1. Use fixed viewport size (already configured)
2. Disable font smoothing in test
3. Use web-safe fonts for dashboard

### Flaky Screenshot Tests

**Cause**: Animations, loading states, or async content

**Solutions**:
1. Wait for stable state before screenshot:
```typescript
await page.waitForLoadState('networkidle');
await page.waitForTimeout(500); // Wait for animations
```

2. Disable animations in test environment:
```typescript
await page.addStyleTag({
  content: '*, *::before, *::after { transition: none !important; animation: none !important; }'
});
```

### Missing Baselines

**Cause**: Running tests before generating initial baselines

**Solution**:
```bash
cd apps/web
pnpm test:e2e:visual:admin:update
git add e2e/admin-dashboard-visual.spec.ts-snapshots/
git commit -m "test(admin): add initial visual regression baselines"
```

### Screenshots Too Large

**Cause**: Full-page screenshots increasing repo size

**Solutions**:
1. Use component-level screenshots (preferred):
```typescript
const dashboard = page.locator('[data-testid="admin-dashboard"]');
await expect(dashboard).toHaveScreenshot('dashboard.png');
```

2. Compress screenshots:
```bash
# Optimize PNG files (use ImageOptim, TinyPNG, or similar)
find e2e/admin-dashboard-visual.spec.ts-snapshots/ -name "*.png" -exec optipng {} \;
```

## CI/CD Integration

### Workflow: `.github/workflows/visual-regression.yml`

```yaml
playwright-visual:
  name: Playwright Visual Snapshots
  runs-on: ubuntu-latest
  steps:
    - name: Run Visual Regression Tests
      run: pnpm test:e2e:visual --project=desktop-chrome
      env:
        CI: true
      continue-on-error: true  # Non-blocking in alpha

    - name: Upload Visual Snapshots
      uses: actions/upload-artifact@v4
      with:
        name: visual-snapshots
        path: apps/web/e2e/admin-dashboard-visual.spec.ts-snapshots/
```

### Artifacts

On test failure, CI uploads:
- **Test Results**: Playwright HTML report
- **Screenshots**: Actual vs Expected comparison
- **Diffs**: Visual diff images highlighting changes
- **Traces**: Full interaction traces for debugging

Access artifacts via:
1. Navigate to failed workflow run
2. Scroll to bottom → "Artifacts" section
3. Download `visual-test-results-{run_number}.zip`
4. Open `playwright-report/index.html` in browser

## Best Practices

### 1. Component-Level Screenshots
```typescript
// ✅ Good: Focused on specific component
const activityFeed = page.locator('[data-testid="activity-feed"]');
await expect(activityFeed).toHaveScreenshot('activity-feed-all.png');

// ❌ Avoid: Full page (unnecessary size)
await expect(page).toHaveScreenshot('entire-page.png', { fullPage: true });
```

### 2. Wait for Stability
```typescript
// ✅ Good: Ensure stable state
await page.waitForLoadState('networkidle');
await page.waitForTimeout(500); // Animations settle
await expect(page).toHaveScreenshot('dashboard.png');

// ❌ Avoid: Capture mid-load
await page.goto('/admin');
await expect(page).toHaveScreenshot('dashboard.png'); // May be loading
```

### 3. Meaningful Names
```typescript
// ✅ Good: Descriptive names
await expect(page).toHaveScreenshot('dashboard-error-state-api-failed.png');

// ❌ Avoid: Generic names
await expect(page).toHaveScreenshot('screenshot1.png');
```

### 4. Test Isolation
```typescript
// ✅ Good: Each test is independent
test('dashboard default', async ({ page }) => {
  await page.goto('/admin');
  await expect(page).toHaveScreenshot('default.png');
});

test('dashboard errors', async ({ page }) => {
  // Start fresh, not dependent on previous test
  await page.route('**/api/v1/admin/stats', route => route.abort());
  await page.goto('/admin');
  await expect(page).toHaveScreenshot('errors.png');
});
```

## Metrics & Monitoring

### Test Duration

Expected test execution times:
- Local: ~2-3 minutes (8 tests)
- CI: ~3-5 minutes (includes setup + parallel execution)

### Screenshot Size

Recommended limits:
- Individual screenshot: <500KB
- Total baseline suite: <5MB

### Pass Rate Target

- **Development**: 95%+ (occasional false positives acceptable)
- **CI**: 90%+ (stricter environment, some tolerance)

## Related Documentation

- [E2E Testing Guide](./e2e-testing.md)
- [Admin Dashboard Architecture](../01-architecture/admin-dashboard.md)
- [Issue #2916](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2916)
- [Issue #2852 - Chromatic Visual Testing](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2852)
- [Playwright Screenshot API](https://playwright.dev/docs/test-snapshots)

## FAQ

### Q: Why Playwright screenshots instead of Chromatic?

**A**: Complementary approaches:
- **Playwright**: E2E user journey validation (this implementation)
- **Chromatic**: Component isolation visual review (#2852)

Both provide value at different testing levels.

### Q: Can I update baselines in CI?

**A**: No. Always update baselines locally and commit changes:
```bash
pnpm test:e2e:visual:admin:update
git add e2e/admin-dashboard-visual.spec.ts-snapshots/
git commit -m "test(admin): update baselines for redesign"
```

### Q: How do I debug visual failures?

**A**: Three approaches:
1. **UI Mode** (interactive): `pnpm test:e2e:visual:admin:ui`
2. **Local Trace**: Download CI artifacts → open `trace.zip` in Playwright UI
3. **Diff Images**: CI artifacts include visual diff images

### Q: Should I update baselines for every UI change?

**A**: Only for **intentional** changes:
- ✅ Dashboard redesign → update baselines
- ✅ New feature added → update baselines
- ❌ Rendering bug → fix bug, keep baselines
- ❌ CSS mistake → fix CSS, keep baselines

### Q: What's the diff between visual regression and screenshot testing?

**A**: Same thing!
- **Visual Regression** = detecting unintended UI changes
- **Screenshot Testing** = the technical implementation method

## Changelog

- **2026-01-23**: Initial implementation (Issue #2916)
  - 8 baseline screenshots for Admin Dashboard states
  - 0.1% threshold configuration
  - Integration with existing visual-regression.yml workflow
  - Non-blocking in alpha phase
