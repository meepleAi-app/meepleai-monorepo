# Add E2E Code Coverage Reporting

**Issue ID**: E2E-012 | **Priorità**: 🟢 BASSA | **Effort**: 4-6 ore

---

## 📋 Problem

Nessuna metrica di **code coverage** per test E2E. Non sappiamo:
- Quali routes sono testate
- Quali componenti sono coperti
- Quali scenari utente mancano

**Situazione**:
- Frontend coverage: 90%+ (unit tests)
- E2E coverage: **Unknown** ❓

---

## 🎯 Impact

- **Visibility**: Non sappiamo coverage gaps
- **Planning**: Difficile prioritizzare nuovi test
- **Metrics**: Nessun tracking nel tempo

---

## ✅ Solution

> **⚠️ Note**: These examples provide the foundation for E2E coverage. Additional setup required for production use (see Limitations section below).

### Option 1: Istanbul + Playwright (NYC Instrumentation)

```bash
# Install Istanbul/NYC for code coverage
pnpm add -D @istanbuljs/nyc-config-typescript nyc babel-plugin-istanbul
```

```javascript
// next.config.js - Add instrumentation for NYC
const nextConfig = {
  webpack: (config, { dev, isServer }) => {
    // Enable Istanbul instrumentation in test mode
    if (process.env.NYC_ENABLE === 'true' && !isServer) {
      config.module.rules.push({
        test: /\.(js|jsx|ts|tsx)$/,
        exclude: /node_modules/,
        use: {
          loader: 'babel-loader',
          options: {
            presets: ['next/babel'],
            plugins: ['istanbul'],
          },
        },
      });
    }
    return config;
  },
};

module.exports = nextConfig;
```

```typescript
// playwright.config.ts
export default defineConfig({
  use: {
    // Enable trace for debugging
    trace: 'on',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  // Test result reporters (NOT for code coverage)
  reporter: [
    ['html'],
    ['json', { outputFile: 'test-results/results.json' }]
  ],
  // Enable code coverage via webServer instrumentation
  webServer: {
    command: 'NYC_ENABLE=true pnpm dev',
    port: 3000,
    reuseExistingServer: false,
  }
});
```

```json
// .nycrc.json - NYC configuration for coverage reporting
{
  "extends": "@istanbuljs/nyc-config-typescript",
  "all": true,
  "include": ["src/**/*.{ts,tsx}"],
  "exclude": [
    "**/*.test.{ts,tsx}",
    "**/*.stories.{ts,tsx}",
    "**/*.spec.{ts,tsx}",
    "src/app/layout.tsx"
  ],
  "reporter": ["html", "lcov", "text", "json"],
  "report-dir": "coverage/e2e",
  "temp-dir": ".nyc_output"
}
```

### Option 2: V8 Coverage (Playwright with CDP)

> **⚠️ IMPORTANT NOTE**: Playwright does NOT have a built-in `page.coverage` API like Puppeteer. The code example below is **incorrect** and will throw `TypeError: page.coverage is not a function` at runtime.
>
> To collect V8 coverage with Playwright, you must use the Chrome DevTools Protocol (CDP) directly via `page.context().newCDPSession()`. This requires significant additional setup and is not recommended for most use cases. **Option 1 (Istanbul/NYC) is the recommended approach for Playwright**.
>
> This section is kept for reference but should not be used as-is. See [Playwright CDP documentation](https://playwright.dev/docs/api/class-cdpsession) for the correct implementation.

```bash
# Install V8 to Istanbul converter (if using CDP approach)
pnpm add -D v8-to-istanbul
```

```typescript
// ❌ INCORRECT - This code uses Puppeteer API, not Playwright
// tests/fixtures/coverage.ts - Collect coverage in test hooks
import { test as base, Page } from '@playwright/test';
import fs from 'fs/promises';
import path from 'path';
import v8toIstanbul from 'v8-to-istanbul';

type CoverageFixtures = {
  autoTestFixture: void;
};

export const test = base.extend<CoverageFixtures>({
  autoTestFixture: [
    async ({ page }, use, testInfo) => {
      // ❌ ERROR: page.coverage does not exist in Playwright
      // This is a Puppeteer API, not Playwright API
      await page.coverage.startJSCoverage({
        resetOnNavigation: false,
        reportAnonymousScripts: true,
      });

      await use();

      // ❌ ERROR: page.coverage does not exist in Playwright
      const coverage = await page.coverage.stopJSCoverage();

      if (coverage.length > 0) {
        const coverageDir = path.join(process.cwd(), '.nyc_output');
        await fs.mkdir(coverageDir, { recursive: true });

        // Save raw V8 coverage
        const testFile = testInfo.titlePath.join('-').replace(/\s+/g, '-');
        const coverageFile = path.join(
          coverageDir,
          `coverage-${testFile}-${Date.now()}.json`
        );

        // Filter only app source files (exclude node_modules, build artifacts)
        const filteredCoverage = coverage.filter(entry =>
          entry.url.includes('/src/') &&
          !entry.url.includes('node_modules') &&
          !entry.url.includes('webpack')
        );

        await fs.writeFile(
          coverageFile,
          JSON.stringify(filteredCoverage, null, 2)
        );
      }
    },
    { auto: true },
  ],
});

export { expect } from '@playwright/test';
```

```typescript
// playwright.config.ts
import { defineConfig } from '@playwright/test';

export default defineConfig({
  use: {
    trace: 'on',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  reporter: [
    ['html'],
    ['json', { outputFile: 'test-results/results.json' }]
  ],
});
```

```typescript
// e2e/example.spec.ts - Use the coverage fixture
import { test, expect } from '../tests/fixtures/coverage';

test('homepage loads', async ({ page }) => {
  await page.goto('http://localhost:3000');
  await expect(page).toHaveTitle(/MeepleAI/);
});
```

### Generate Report

```bash
# Option 1 (NYC): Generate report from Istanbul coverage
npx nyc report --reporter=html --reporter=text --reporter=lcov

# Option 2 (V8): Convert V8 coverage to Istanbul format, then generate report
node scripts/convert-v8-coverage.js  # See script below
npx nyc report --reporter=html --reporter=text --reporter=lcov

# View HTML report
open coverage/e2e/index.html

# Merge with unit test coverage (optional)
npx nyc merge coverage/unit coverage/e2e --output coverage/merged
npx nyc report --temp-dir coverage/merged --reporter=html
```

**V8 to Istanbul Converter Script** (Option 2):

```javascript
// scripts/convert-v8-coverage.js
const v8toIstanbul = require('v8-to-istanbul');
const fs = require('fs').promises;
const path = require('path');
const glob = require('glob');

async function convertV8ToIstanbul() {
  const coverageDir = path.join(process.cwd(), '.nyc_output');
  const files = glob.sync(`${coverageDir}/coverage-*.json`);

  for (const file of files) {
    const v8Coverage = JSON.parse(await fs.readFile(file, 'utf-8'));

    for (const entry of v8Coverage) {
      if (!entry.url.startsWith('file://')) continue;

      const filePath = entry.url.replace('file://', '');
      const converter = v8toIstanbul(filePath);
      await converter.load();
      converter.applyCoverage(entry.functions);

      const istanbulCoverage = converter.toIstanbul();
      const outputFile = file.replace('.json', '-istanbul.json');
      await fs.writeFile(outputFile, JSON.stringify(istanbulCoverage, null, 2));
    }
  }

  console.log(`Converted ${files.length} V8 coverage files to Istanbul format`);
}

convertV8ToIstanbul().catch(console.error);
```

---

## ⚠️ Limitations & Troubleshooting

### Known Limitations

1. **E2E Coverage ≠ Unit Test Coverage**
   - E2E measures code executed during user journeys
   - Unit tests measure isolated component/function coverage
   - Different metrics, complementary approaches

2. **External Scripts Not Covered**
   - Third-party libraries (node_modules)
   - CDN-loaded scripts
   - Browser extensions

3. **SSR vs CSR Coverage**
   - Server-Side Rendering (SSR) components harder to track
   - Client-Side Rendering (CSR) easier via V8 coverage
   - Next.js hybrid rendering adds complexity

4. **Source Maps Required**
   - Production builds need source maps enabled
   - Add `productionBrowserSourceMaps: true` to next.config.js
   - Increases build size (consider separate test builds)

### Common Issues

| Issue | Solution |
|-------|----------|
| "No coverage data collected" | Ensure source maps enabled, check `NYC_ENABLE=true` |
| "Cannot find module 'babel-loader'" | Run `pnpm add -D babel-loader` |
| "Coverage shows 0% for all files" | Check file path filters in `.nycrc.json` |
| "V8 coverage URLs don't match" | Use `file://` protocol, verify path mapping |
| "Playwright tests timeout" | Instrumentation slows tests, increase timeout |

### Best Practices

1. **Separate Test Environment**: Use dedicated config for coverage runs
2. **CI Integration**: Store coverage artifacts, track trends over time
3. **Coverage Thresholds**: Start at 60%, gradually increase to 80%+
4. **Selective Testing**: Focus on critical user journeys first
5. **Performance Impact**: Instrumentation adds 20-30% overhead

---

## 📝 Checklist

- [ ] Install coverage tooling (NYC or V8-to-Istanbul)
- [ ] Configure Next.js instrumentation (Option 1) or Playwright fixtures (Option 2)
- [ ] Update Playwright config for coverage
- [ ] Run tests with coverage enabled
- [ ] Generate and verify HTML report
- [ ] Add to CI (report artifact + trend tracking)
- [ ] Set coverage targets (start 60%, target 80%+)
- [ ] Document critical paths to test
- [ ] Dashboard integration (optional: Codecov, Coveralls)

---

## 📚 References

- [Playwright Coverage API](https://playwright.dev/docs/api/class-coverage)
- [NYC Configuration](https://github.com/istanbuljs/nyc#configuration-files)
- [V8 to Istanbul Converter](https://github.com/istanbuljs/v8-to-istanbul)
- [Next.js Source Maps](https://nextjs.org/docs/advanced-features/source-maps)

---

**Target**: E2E coverage report + dashboard + 80% critical path coverage
