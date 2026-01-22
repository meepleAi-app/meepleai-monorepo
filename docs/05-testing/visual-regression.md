# Visual Regression Testing

**Issue**: #2906
**Epic**: #2845 - MeepleAI Design System Integration
**Last Updated**: 2026-01-22

## Overview

Visual regression testing automatically detects unintended visual changes in the UI by comparing screenshots against baseline images. This ensures the MeepleAI design system remains consistent across iterations.

## Why Visual Regression Testing?

- **Design System Integrity**: Detect breaking changes to MeepleAI visual identity
- **Cross-Browser Consistency**: Verify rendering across Chrome, Firefox, Safari
- **Responsive Design**: Validate layouts on desktop, tablet, mobile viewports
- **Confidence in Refactoring**: Safely refactor CSS/styling with automated checks
- **Regression Prevention**: Catch visual bugs before they reach production

## Technology Stack

- **Playwright**: Test automation framework with built-in screenshot comparison
- **toHaveScreenshot()**: Native Playwright assertion for visual testing
- **Baselines**: Stored in `e2e/visual/__screenshots__/`
- **CI Integration**: Automated visual checks in GitHub Actions

## Test Coverage

### Admin Dashboard Components
- ✅ Dashboard overview (full page)
- ✅ MetricsGrid cards
- ✅ Charts (Endpoint, Latency, Time Series, Feedback)
- ✅ Navigation & Top Bar
- ✅ Filters & Controls
- ✅ Background Texture System (Issue #2905)

### Viewports
- ✅ **Desktop**: 1920x1080
- ✅ **Tablet**: 768x1024
- ✅ **Mobile**: 375x667

### Interactive States
- ✅ Hover effects (cards, buttons)
- ✅ Loading states
- ✅ Reduced motion accessibility

## Quick Start

### Run Visual Tests

```bash
# Run visual regression tests (desktop Chrome only)
pnpm test:e2e:visual

# Run with UI mode for debugging
pnpm test:e2e:visual:ui

# Run on all browsers/viewports
pnpm test:e2e:visual:all
```

### Generate/Update Baselines

```bash
# Generate baselines for all tests
pnpm test:e2e:visual:update

# Update specific test baseline
pnpm test:e2e:visual:update -g "dashboard overview"
```

## Workflow

### 1. Initial Baseline Creation

When setting up visual tests for the first time:

```bash
# Generate baseline screenshots
pnpm test:e2e:visual:update

# Review generated baselines in e2e/visual/__screenshots__/
# Commit baselines to git
git add e2e/visual/__screenshots__/
git commit -m "chore: add visual regression baselines for admin dashboard"
```

### 2. Daily Development

After making UI changes:

```bash
# Run visual tests to detect changes
pnpm test:e2e:visual

# If tests fail:
# 1. Review diff report: pnpm test:e2e:report
# 2. Verify change is intentional
# 3. Update baseline if correct: pnpm test:e2e:visual:update
```

### 3. PR Review Process

When reviewing pull requests with UI changes:

1. **CI runs visual tests** automatically
2. **Failures indicate visual changes** - review diffs in test report
3. **Intentional changes** → Approve and update baselines
4. **Unintentional changes** → Request fixes

## Configuration

### Screenshot Comparison Settings

Located in `e2e/visual/admin-dashboard.visual.spec.ts`:

```typescript
await expect(element).toHaveScreenshot('name.png', {
  maxDiffPixels: 100,      // Allow up to 100 pixels difference
  threshold: 0.2,          // 20% per-pixel color difference tolerance
  fullPage: true,          // Capture entire page (optional)
});
```

### Tolerance Guidelines

| Component Type | maxDiffPixels | threshold | Rationale |
|---------------|---------------|-----------|-----------|
| Static UI | 50 | 0.2 | Minimal anti-aliasing variations |
| Charts | 150 | 0.25 | Chart rendering may vary slightly |
| Interactive | 75 | 0.2 | Hover states with transitions |
| Full Page | 100 | 0.2 | Accumulated minor differences |

## Baseline Management

### When to Update Baselines

✅ **Update baselines when**:
- Intentional design changes (new styling, layout updates)
- MeepleAI design system updates
- Adding new features with visual components
- Fixing visual bugs

❌ **Do NOT update baselines when**:
- Tests fail due to actual regression
- Unsure why visual changed
- Changes look wrong or broken

### Baseline Storage

```
e2e/visual/__screenshots__/
├── admin-dashboard-desktop-chrome/
│   ├── admin-dashboard-desktop.png
│   ├── metrics-grid-desktop.png
│   ├── charts-section-desktop.png
│   └── ...
├── admin-dashboard-tablet-chrome/
│   └── ...
└── admin-dashboard-mobile-chrome/
    └── ...
```

**Important**: Commit baselines to git for team collaboration

### Git Operations

```bash
# Add new baselines
git add e2e/visual/__screenshots__/

# Review baseline changes before committing
git diff e2e/visual/__screenshots__/

# Commit baseline updates
git commit -m "chore: update visual baselines after MeepleAI redesign"
```

## Writing Visual Tests

### Test Structure

```typescript
import { expect, test } from '@playwright/test';

test.describe('Component Name - Visual', () => {
  // Set viewport for responsive tests
  test.use({ viewport: { width: 1920, height: 1080 } });

  test('component matches baseline', async ({ page }) => {
    // 1. Navigate to page
    await page.goto('/path');

    // 2. Wait for dynamic content
    await page.waitForLoadState('networkidle');

    // 3. Locate component (optional, for element screenshots)
    const component = page.locator('.component-class');

    // 4. Take screenshot and compare
    await expect(component).toHaveScreenshot('component-name.png', {
      maxDiffPixels: 50,
      threshold: 0.2,
    });
  });
});
```

### Best Practices

#### 1. Wait for Content to Load

```typescript
// ❌ Bad: Screenshot before content loads
await expect(page).toHaveScreenshot('page.png');

// ✅ Good: Wait for network idle
await page.waitForLoadState('networkidle');
await expect(page).toHaveScreenshot('page.png');

// ✅ Good: Wait for specific element
await page.waitForSelector('canvas', { state: 'visible' });
await expect(page).toHaveScreenshot('chart.png');
```

#### 2. Handle Animations

```typescript
// Wait for animations to complete
await element.hover();
await page.waitForTimeout(300); // Wait for transition
await expect(element).toHaveScreenshot('hover-state.png');
```

#### 3. Stabilize Dynamic Content

```typescript
// Mock time-dependent data
await page.addInitScript(() => {
  Date.now = () => 1640995200000; // Fixed timestamp
});

// Mock random values
await page.addInitScript(() => {
  Math.random = () => 0.5; // Fixed random
});
```

#### 4. Test Interactive States

```typescript
test('button hover state', async ({ page }) => {
  const button = page.getByRole('button', { name: 'Submit' });

  // Hover and capture
  await button.hover();
  await page.waitForTimeout(200);

  await expect(button).toHaveScreenshot('button-hover.png');
});
```

#### 5. Accessibility Testing

```typescript
test('reduced motion disables animations', async ({ page }) => {
  // Enable prefers-reduced-motion
  await page.emulateMedia({ reducedMotion: 'reduce' });

  await page.goto('/page');

  await expect(page).toHaveScreenshot('page-reduced-motion.png');
});
```

## CI/CD Integration

### GitHub Actions Workflow

Visual regression tests run automatically on:
- ✅ Pull requests (detect visual changes)
- ✅ Main branch commits (validate baselines)
- ❌ Manual workflow dispatch (on-demand testing)

### Workflow Configuration

```yaml
# .github/workflows/visual-regression.yml
name: Visual Regression Tests

on:
  pull_request:
    paths:
      - 'apps/web/src/**'
      - 'apps/web/e2e/visual/**'
      - 'apps/web/package.json'

jobs:
  visual-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: pnpm/action-setup@v2
      - uses: actions/setup-node@v4
        with:
          node-version: '20'
          cache: 'pnpm'

      - name: Install dependencies
        run: pnpm install

      - name: Install Playwright browsers
        run: pnpm --filter @meepleai/web exec playwright install --with-deps chromium

      - name: Run visual regression tests
        run: pnpm --filter @meepleai/web test:e2e:visual

      - name: Upload test report
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: visual-regression-report
          path: apps/web/playwright-report/
          retention-days: 7

      - name: Upload screenshot diffs
        if: failure()
        uses: actions/upload-artifact@v4
        with:
          name: screenshot-diffs
          path: apps/web/test-results/
          retention-days: 7
```

### Handling CI Failures

When visual tests fail in CI:

1. **Download artifacts** from GitHub Actions
2. **Review screenshot diffs** in test-results/
3. **Determine if change is intentional**
   - **Yes** → Update baselines and commit
   - **No** → Fix the regression

## Troubleshooting

### Common Issues

#### Issue: Tests fail with "Screenshot comparison failed"

**Cause**: Visual difference detected

**Solution**:
```bash
# 1. Review diff report
pnpm test:e2e:report

# 2. If change is intentional:
pnpm test:e2e:visual:update

# 3. Verify updated baseline looks correct
git diff e2e/visual/__screenshots__/
```

#### Issue: Flaky screenshot tests (intermittent failures)

**Cause**: Animations, fonts, or dynamic content not stabilized

**Solution**:
```typescript
// Add explicit waits
await page.waitForLoadState('networkidle');
await page.waitForTimeout(1000); // Wait for animations

// Increase tolerance for flaky components
await expect(element).toHaveScreenshot('name.png', {
  maxDiffPixels: 150, // Increased tolerance
});
```

#### Issue: Font rendering differences across environments

**Cause**: Different font rendering engines (Windows vs Linux vs Mac)

**Solution**:
```typescript
// Use consistent CI environment (e.g., always Linux in CI)
// Document baseline generation environment

// Or increase threshold for text-heavy components
await expect(element).toHaveScreenshot('text-heavy.png', {
  threshold: 0.3, // Higher tolerance for font rendering
});
```

#### Issue: Baselines differ between local and CI

**Cause**: Different screen resolutions, browsers, or fonts

**Solution**:
- Generate baselines in CI environment
- Use Docker for consistent local environment
- Document baseline generation process

### Debugging Tips

```bash
# Run test with UI mode to inspect failures
pnpm test:e2e:visual:ui

# Run single test for faster iteration
pnpm test:e2e:visual -g "dashboard overview"

# Generate HTML report with screenshot diffs
pnpm test:e2e:report
```

## Performance Considerations

- **Baseline Size**: Screenshots add ~200-500KB per image to repository
- **Test Duration**: Visual tests add ~2-5 seconds per screenshot
- **CI Cost**: Run only on relevant changes (CSS, components, visual files)

### Optimization Strategies

1. **Selective Testing**: Only run visual tests for affected components
2. **Parallel Execution**: Use `--project=desktop-chrome` for faster feedback
3. **Incremental Baselines**: Update only changed baselines in PR
4. **Artifact Storage**: Store diff images as GitHub Actions artifacts (auto-cleanup)

## Maintenance

### Monthly Review

- [ ] Review and prune unused baselines
- [ ] Update tolerance thresholds if needed
- [ ] Check for flaky tests
- [ ] Verify CI pipeline efficiency

### Baseline Cleanup

```bash
# Find orphaned baselines (tests deleted but screenshots remain)
find e2e/visual/__screenshots__ -type f -name "*.png"

# Delete orphaned screenshots
git rm e2e/visual/__screenshots__/old-test-name.png
```

## Future Enhancements

- [ ] **CI artifact comparison**: Visual diff viewer in GitHub PR comments
- [ ] **Snapshot history**: Track visual changes over time
- [ ] **Component library**: Automated visual testing for Storybook components
- [ ] **Percy integration**: Cloud-based visual testing with better diff UI
- [ ] **Multi-theme testing**: Light/dark mode visual regression

## Resources

- [Playwright Visual Comparisons](https://playwright.dev/docs/test-snapshots)
- [Best Practices for Visual Testing](https://playwright.dev/docs/best-practices)
- [MeepleAI Design System](../../docs/design-proposals/meepleai-style/)
- [Epic #2845 - Design System Integration](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2845)

---

**Maintained by**: Frontend Team
**Questions?**: Open an issue with label `area:testing`
