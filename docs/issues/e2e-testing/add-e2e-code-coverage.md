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

### Option 1: Istanbul + Playwright

```typescript
// playwright.config.ts
export default defineConfig({
  use: {
    // Enable coverage collection
    trace: 'on',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  // Add coverage reporting
  reporter: [
    ['html'],
    ['json', { outputFile: 'test-results/coverage.json' }],
    ['lcov', { outputFile: 'coverage/lcov.info' }]
  ]
});
```

### Option 2: Playwright Coverage Plugin

```bash
# Install
pnpm add -D @playwright/test-coverage

# Run with coverage
pnpm test:e2e --coverage
```

### Generate Report

```bash
# Generate HTML report
npx nyc report --reporter=html

# View report
open coverage/index.html
```

---

## 📝 Checklist

- [ ] Install coverage tooling
- [ ] Configure Playwright for coverage
- [ ] Run tests with coverage enabled
- [ ] Generate HTML report
- [ ] Add to CI (report artifact)
- [ ] Set coverage targets (>80%)
- [ ] Dashboard integration (optional)

---

**Target**: E2E coverage report + dashboard
