# [E2E-011] Browser Matrix Implementation

**Issue**: #1497
**Priority**: 🟢 P2 (LOW)
**Status**: ✅ Implemented
**Effort**: 4-6h

## Overview

Extended Playwright E2E test suite from Chrome-only to multi-browser matrix covering Chrome, Firefox, and Safari across desktop, mobile, and tablet viewports.

## Implementation Strategy

**Chosen Approach**: Modified Option 1 - Strategic Multi-Browser Coverage

### Browser Matrix

| Viewport | Chrome | Firefox | Safari | Rationale |
|----------|--------|---------|--------|-----------|
| **Desktop** (1920×1080) | ✅ | ✅ | ✅ | Full cross-browser coverage for desktop users |
| **Mobile** (390×844) | ✅ | ❌ | ✅ | Chrome (Android) + Safari (iOS) cover 95%+ mobile market |
| **Tablet** (1024×1366) | ✅ | ❌ | ❌ | Cost optimization - Chrome sufficient for tablet testing |

**Total Projects**: 6 (increased from 3)
- Desktop: `desktop-chrome`, `desktop-firefox`, `desktop-safari`
- Mobile: `mobile-chrome`, `mobile-safari`
- Tablet: `tablet-chrome`

### Rationale

1. **Desktop Multi-Browser**: Critical for cross-browser compatibility testing
2. **Mobile Chrome + Safari**: Covers Android and iOS ecosystems (dominant market share)
3. **Tablet Chrome Only**: Diminishing returns for Firefox/Safari on tablets
4. **CI Time Impact**: ~+5-7 minutes (acceptable for P2 priority)

## Changes Made

### 1. Playwright Configuration (`apps/web/playwright.config.ts`)

```typescript
projects: [
  // Desktop - Multi-browser
  { name: 'desktop-chrome', use: { ...devices['Desktop Chrome'], viewport: { width: 1920, height: 1080 } } },
  { name: 'desktop-firefox', use: { ...devices['Desktop Firefox'], viewport: { width: 1920, height: 1080 } } },
  { name: 'desktop-safari', use: { ...devices['Desktop Safari'], viewport: { width: 1920, height: 1080 } } },

  // Mobile - Chrome + Safari
  { name: 'mobile-chrome', use: { ...devices['Pixel 5'], viewport: { width: 390, height: 844 } } },
  { name: 'mobile-safari', use: { ...devices['iPhone 13'], viewport: { width: 390, height: 844 } } },

  // Tablet - Chrome only
  { name: 'tablet-chrome', use: { ...devices['Galaxy Tab S4'], viewport: { width: 1024, height: 1366 } } },
]
```

### 2. CI Browser Installation (`.github/actions/setup-playwright/action.yml`)

```yaml
# Before
run: pnpm exec playwright install --with-deps chromium

# After
run: pnpm exec playwright install --with-deps chromium firefox webkit
```

## Local Testing

### Install Browsers
```bash
cd apps/web
pnpm exec playwright install chromium firefox webkit
```

### Run Tests

```bash
# All browsers
pnpm test:e2e

# Specific browser
pnpm test:e2e --project=desktop-firefox
pnpm test:e2e --project=mobile-safari

# Specific test across all browsers
pnpm test:e2e e2e/auth.spec.ts

# UI mode for debugging
pnpm test:e2e:ui
```

### List Available Projects
```bash
pnpm exec playwright test --list-projects
```

## CI Integration

### Automatic Execution

E2E tests run automatically in CI on:
- Pull requests modifying `apps/web/**`
- Push to `main` branch
- Nightly schedule (2 AM UTC)
- Manual workflow dispatch

### Execution Strategy

```yaml
# CI runs all 6 browser projects sequentially
- desktop-chrome
- desktop-firefox
- desktop-safari
- mobile-chrome
- mobile-safari
- tablet-chrome
```

**Workers**: 1 (sequential execution in CI for stability - Issue #1868)
**Retries**: 2 on failure
**Timeout**: 20 minutes total

## Performance Impact

### CI Time Estimates

| Metric | Before | After | Change |
|--------|--------|-------|--------|
| Browser Projects | 3 | 6 | +100% |
| Est. E2E Duration | ~12min | ~17-19min | +5-7min |
| Total CI Duration | ~14min | ~19-21min | +36% |

**Acceptable**: P2 priority allows for increased CI time in exchange for comprehensive browser coverage.

## Browser Coverage

### Market Share Coverage

| Platform | Browsers | Combined Market Share |
|----------|----------|----------------------|
| Desktop | Chrome, Firefox, Safari | ~95% |
| Mobile | Chrome (Android), Safari (iOS) | ~99% |
| Tablet | Chrome | ~65% (sufficient) |

## Verification

### Test Matrix Validation

```bash
# Verify all projects run
pnpm exec playwright test --list --project=desktop-chrome
pnpm exec playwright test --list --project=desktop-firefox
pnpm exec playwright test --list --project=desktop-safari
pnpm exec playwright test --list --project=mobile-chrome
pnpm exec playwright test --list --project=mobile-safari
pnpm exec playwright test --list --project=tablet-chrome
```

### Quick Smoke Test

```bash
# Run setup test across all browsers
pnpm exec playwright test e2e/setup.spec.ts
```

## Migration Notes

### Breaking Changes

None. Existing test files run unchanged across all browser projects.

### Project Name Changes

| Old Name | New Name(s) | Migration |
|----------|-------------|-----------|
| `mobile` | `mobile-chrome`, `mobile-safari` | Auto-migrated |
| `tablet` | `tablet-chrome` | Auto-migrated |
| `desktop` | `desktop-chrome`, `desktop-firefox`, `desktop-safari` | Auto-migrated |

### CI Considerations

1. **Caching**: Playwright browser cache updated to include all 3 browsers
2. **Dependencies**: System dependencies installed for Firefox and WebKit
3. **Disk Space**: +165MB for Firefox and WebKit binaries
4. **Memory**: No significant impact (sequential execution)

## Troubleshooting

### Firefox/WebKit Installation Issues

```bash
# Reinstall with system dependencies
pnpm exec playwright install --with-deps firefox webkit

# Check installation
pnpm exec playwright --version
```

### Browser-Specific Test Failures

```bash
# Run specific browser with traces
pnpm test:e2e --project=desktop-firefox --trace=on

# Debug specific browser
pnpm test:e2e:ui --project=desktop-safari
```

### CI Cache Issues

If CI fails to install browsers, clear the cache:
- GitHub Actions: Settings → Actions → Caches → Delete `playwright-*` caches

## Future Enhancements

### Phase 2 (Optional - P3)

- **Tablet Multi-Browser**: Add Firefox/Safari for tablet (if market share increases)
- **Mobile Firefox**: Add if market share exceeds 5%
- **Parallel Execution**: Enable parallel browser testing (requires stability improvements)
- **Sharding**: Distribute tests across browsers for faster CI

### Monitoring

Track browser-specific failure rates to identify:
- Browser-specific bugs
- Flaky tests in specific browsers
- Performance differences across browsers

## References

- **Issue**: #1497
- **Playwright Docs**: https://playwright.dev/docs/test-projects
- **Browser Devices**: https://playwright.dev/docs/emulation#devices
- **CI Workflow**: `.github/workflows/ci.yml`
- **Testing Strategy**: `docs/02-development/testing/testing-strategy.md`

## Definition of Done

✅ Firefox and Safari added to desktop browser matrix
✅ Safari added to mobile browser matrix
✅ Playwright config updated with 6 projects
✅ CI action updated to install all browsers
✅ Local testing verified across all browsers
✅ Documentation created and updated
✅ No new warnings introduced
✅ CI pipeline validates browser matrix

---

**Implemented**: 2025-12-05
**Verified**: Local + CI
**Impact**: Medium (CI time +5-7min, coverage +100%)
