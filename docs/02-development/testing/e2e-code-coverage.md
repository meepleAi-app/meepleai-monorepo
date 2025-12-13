# E2E Code Coverage with Playwright

**Issue**: [#1498](https://github.com/Meeple-AI/meepleai-monorepo/issues/1498) - E2E Code Coverage Reporting
**Status**: ✅ Implemented
**Implementation**: @bgotink/playwright-coverage (V8 native coverage)

---

## Overview

E2E code coverage tracks which parts of the frontend JavaScript code are executed during end-to-end tests. This helps identify:
- Untested code paths in the application
- Critical user flows that lack test coverage
- Areas where additional E2E tests would add value

**Coverage Scope**: Chromium browsers only (desktop-chrome, mobile-chrome, tablet-chrome)

---

## Quick Start

### Run E2E Tests with Coverage

```bash
# Run all Chromium E2E tests with coverage
cd apps/web
pnpm test:e2e:coverage

# Open HTML coverage report
pnpm test:e2e:coverage:report
```

### View Coverage Reports

After running tests, coverage reports are generated in `apps/web/coverage-e2e/`:

```
apps/web/coverage-e2e/
├── html/              # HTML report (open index.html in browser)
├── lcov.info          # LCOV format (for CI/CD tools)
├── coverage.json      # JSON format (for programmatic access)
└── text-summary.txt   # Text summary (console output)
```

---

## Implementation Details

### Architecture

**Tool**: [@bgotink/playwright-coverage](https://github.com/bgotink/playwright-coverage)
**Coverage Method**: V8 native coverage API (no build instrumentation required)
**Browsers Supported**: Chromium only (V8 JavaScript engine)

**Why V8 Coverage?**
- ✅ No build modifications needed (works with Next.js 16 Turbopack)
- ✅ Zero performance overhead during builds
- ✅ Simple integration (just import a different test function)
- ❌ Chromium-only (acceptable trade-off for E2E coverage)

### File Structure

```
apps/web/
├── e2e/
│   ├── test.ts                # Central test export with coverage
│   ├── fixtures/
│   │   └── auth.ts            # Auth fixtures (merged with coverage)
│   └── *.spec.ts              # Test files (import from ./test.ts)
├── playwright.config.ts       # Coverage reporter configuration
└── coverage-e2e/              # Generated coverage reports (gitignored)
```

### Configuration

**Playwright Config** (`playwright.config.ts`):
```typescript
import { defineCoverageReporterConfig } from '@bgotink/playwright-coverage';

export default defineConfig({
  reporter: [
    ['list'], // or 'dot' in CI
    [
      '@bgotink/playwright-coverage',
      defineCoverageReporterConfig({
        sourceRoot: path.resolve(__dirname, '../..'),
        exclude: [
          '**/e2e/**',
          '**/__tests__/**',
          '**/*.test.{ts,tsx}',
          '**/*.spec.{ts,tsx}',
          // ... more exclusions
        ],
        resultDir: path.resolve(__dirname, 'coverage-e2e'),
        reports: ['lcov', 'html', 'json', 'text-summary'],
        watermarks: {
          statements: [30, 60],
          functions: [30, 60],
          branches: [30, 60],
          lines: [30, 60],
        },
      }),
    ],
  ],
});
```

**Test Import** (`e2e/test.ts`):
```typescript
import { mergeTests } from '@playwright/test';
import { test as testWithCoverage } from '@bgotink/playwright-coverage';
import { test as testWithAuth } from './fixtures/auth';

export const test = mergeTests(testWithCoverage, testWithAuth);
export { expect } from '@playwright/test';
```

**Test File Usage**:
```typescript
// OLD: import { test, expect } from './fixtures/auth';
// NEW: import { test, expect } from './test';

import { test, expect } from './test';

test('My E2E test', async ({ page }) => {
  // Coverage is automatically tracked for Chromium browsers
  await page.goto('/');
  // ... test logic
});
```

---

## Coverage Thresholds

**Current Thresholds** (Conservative baseline - Issue #1498):
- Statements: 30% (target: 60%)
- Functions: 30% (target: 60%)
- Branches: 30% (target: 60%)
- Lines: 30% (target: 60%)

**Watermarks**:
- Red: < 30% (low coverage)
- Yellow: 30-60% (medium coverage)
- Green: > 60% (high coverage)

**Gradual Increase Strategy**:
1. Phase 1 (Current): 30% threshold - Establish baseline
2. Phase 2 (Q1 2025): 40% threshold - Add critical path tests
3. Phase 3 (Q2 2025): 50% threshold - Expand to secondary flows
4. Phase 4 (Q3 2025): 60% threshold - Comprehensive coverage

---

## CI/CD Integration

### GitHub Actions

E2E coverage runs automatically in CI for Chromium browsers:

```yaml
# .github/workflows/ci.yml
- name: Run E2E Tests with Coverage
  run: pnpm test:e2e:coverage
  working-directory: apps/web

- name: Upload Coverage Report
  uses: actions/upload-artifact@v4
  with:
    name: e2e-coverage-report
    path: apps/web/coverage-e2e/
```

### Coverage Reports

- **HTML Report**: Downloadable artifact from GitHub Actions
- **LCOV Report**: `coverage-e2e/lcov.info` (compatible with Codecov, Coveralls)
- **Text Summary**: Printed to console during CI run

---

## Browser Coverage Matrix

| Browser | Coverage Tracked | Reason |
|---------|-----------------|--------|
| ✅ Desktop Chrome | Yes | V8 engine |
| ✅ Mobile Chrome | Yes | V8 engine |
| ✅ Tablet Chrome | Yes | V8 engine |
| ❌ Desktop Firefox | No | SpiderMonkey engine (no V8 API) |
| ❌ Mobile Safari | No | JavaScriptCore engine (no V8 API) |
| ❌ Desktop Safari | No | JavaScriptCore engine (no V8 API) |

**Note**: Coverage is Chromium-only, but all browsers are still tested for functionality. Code execution paths don't vary significantly by browser, so Chromium coverage is representative.

---

## Excluded Files

Coverage excludes the following (configured in `playwright.config.ts`):

- **Test files**: `e2e/**`, `__tests__/**`, `*.test.ts`, `*.spec.ts`
- **Generated code**: `generated/**`, `.next/**`, `node_modules/**`
- **Build artifacts**: `dist/**`, `build/**`, `coverage/**`
- **Configuration**: `*.config.js`, `*.config.ts`, `scripts/**`
- **Storybook**: `.storybook/**`, `*.stories.ts`

---

## Troubleshooting

### Coverage Not Generated

**Symptom**: No `coverage-e2e/` directory after running tests

**Solutions**:
1. **Check test import**: Tests must import from `./test.ts`, not `@playwright/test` directly
2. **Check browser**: Coverage only works on Chromium browsers
3. **Check test execution**: Tests must actually run and complete
4. **Check reporter config**: Verify `@bgotink/playwright-coverage` is in `reporter` array

### Coverage Reports Empty

**Symptom**: Coverage report shows 0% for all files

**Solutions**:
1. **Check source root**: Verify `sourceRoot` points to repository root
2. **Check exclude patterns**: Ensure source files aren't excluded
3. **Check test navigation**: Tests must actually navigate to pages (not just API calls)
4. **Check Next.js dev server**: Ensure dev server is running and accessible

### TypeScript Errors

**Symptom**: TS2339: Property 'adminPage' does not exist on type

**Solutions**:
1. **Update test import**: Change from `'./fixtures/auth'` to `'./test'`
2. **Verify merge**: Ensure `e2e/test.ts` merges both coverage and auth fixtures
3. **Restart TS server**: Restart IDE TypeScript language server

---

## Best Practices

### Writing Coverage-Friendly Tests

✅ **DO**:
- Import from `./test.ts` for coverage tracking
- Test real user journeys (navigation, interactions, assertions)
- Run on Chromium browsers for coverage (use `--project=desktop-chrome`)
- Focus on critical user paths first

❌ **DON'T**:
- Import directly from `@playwright/test` (no coverage)
- Test only API endpoints (no frontend coverage)
- Expect coverage from Firefox/Safari tests (Chromium only)
- Aim for 100% coverage (unrealistic for E2E tests)

### Coverage Goals

E2E tests should focus on **high-value user journeys**, not comprehensive coverage:

**Priority 1** (Must Cover):
- Authentication flows (login, logout, registration)
- Core features (chat, game browsing, PDF upload)
- Critical business paths (setup guide generation)

**Priority 2** (Should Cover):
- Secondary features (settings, profile, admin)
- Error handling and edge cases
- Accessibility features

**Priority 3** (Nice to Have):
- Rarely used features
- Experimental/beta features
- Internal tooling pages

### Interpreting Coverage Metrics

**Coverage != Quality**:
- 30% E2E coverage with critical paths tested > 80% coverage of trivial code
- Focus on **path coverage** (user journeys) over **line coverage** (code execution)
- Combine with unit tests (90%+ coverage) for comprehensive quality

**Realistic E2E Coverage Targets**:
- 30-40%: Good baseline (critical paths covered)
- 40-50%: Excellent (critical + secondary paths)
- 60%+: Outstanding (comprehensive user journey coverage)

---

## Migration Guide

### Updating Existing Tests

**Step 1**: Update imports in test files
```typescript
// Before
import { test, expect } from './fixtures/auth';

// After
import { test, expect } from './test';
```

**Step 2**: Verify fixtures still work
```typescript
// Auth fixtures should still be available
test('Admin test', async ({ adminPage }) => {
  await adminPage.goto('/admin');
  // ... test logic
});
```

**Step 3**: Run tests to verify coverage
```bash
pnpm test:e2e:coverage
pnpm test:e2e:coverage:report
```

### Gradual Rollout Strategy

**Phase 1**: Pilot (1-2 test files)
- Update `setup.spec.ts` to use coverage-enabled test
- Verify coverage reports are generated
- Document any issues

**Phase 2**: Critical Paths (10-15 test files)
- Update authentication tests
- Update core feature tests (chat, games, PDF)
- Establish baseline coverage metrics

**Phase 3**: Full Rollout (all test files)
- Update remaining test files
- Set coverage thresholds in CI
- Monitor and improve coverage over time

---

## References

- **Package**: [@bgotink/playwright-coverage](https://github.com/bgotink/playwright-coverage)
- **Playwright Coverage API**: [page.coverage](https://playwright.dev/docs/api/class-coverage)
- **Istanbul Reports**: [istanbul-reports](https://github.com/istanbuljs/istanbuljs/tree/master/packages/istanbul-reports)
- **Issue**: [#1498 - E2E Code Coverage Reporting](https://github.com/Meeple-AI/meepleai-monorepo/issues/1498)

---

## FAQs

**Q: Why only Chromium coverage?**
A: V8 coverage API is Chromium-specific. Firefox/Safari use different JavaScript engines without equivalent APIs. Alternative (Istanbul) requires complex build instrumentation.

**Q: Should we run E2E tests on all browsers or just Chromium?**
A: Run **all browsers** for functional testing, but only Chromium contributes to coverage. Other browsers validate cross-browser compatibility.

**Q: How does this relate to unit test coverage?**
A: Complementary. Unit tests (90%+ coverage) test individual functions. E2E tests (30-60% coverage) test integrated user journeys. Together they provide comprehensive quality assurance.

**Q: Can we use this for API coverage?**
A: No. This tracks frontend JavaScript only. For API coverage, use backend testing tools (dotnet test with coverlet in `apps/api`).

**Q: What if coverage drops below threshold?**
A: CI will fail. Either fix the coverage (add tests) or adjust threshold (with justification in PR).

---

**Last Updated**: 2025-12-13T10:59:23.970Z
**Owner**: Engineering Lead
**Status**: ✅ Production Ready

