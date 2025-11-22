# Add Browser Matrix Testing

**Issue ID**: E2E-011 | **Priorità**: 🟢 BASSA | **Effort**: 4-6 ore

---

## 📋 Problem

Test eseguiti solo su **Chromium**. Firefox e Safari non testati.

```typescript
// playwright.config.ts
projects: [
  { name: 'chromium', use: { ...devices['Desktop Chrome'] } }
  // ❌ Missing: Firefox, Safari, Edge
]
```

**Rischi**:
- Browser-specific bugs non rilevati
- CSS compatibility issues
- JavaScript engine differenze

---

## 🎯 Impact

- **Coverage**: Solo 1/4 browsers testato
- **Compatibility**: Safari/Firefox bugs in produzione
- **User Base**: ~30% users su Firefox/Safari non testati

---

## ✅ Solution

### Update Playwright Config

```typescript
// playwright.config.ts
export default defineConfig({
  projects: [
    { name: 'chromium', use: { ...devices['Desktop Chrome'] } },
    { name: 'firefox', use: { ...devices['Desktop Firefox'] } },
    { name: 'webkit', use: { ...devices['Desktop Safari'] } }, // Safari
    // Optional: Edge
    { name: 'edge', use: { ...devices['Desktop Edge'], channel: 'msedge' } },
    // Optional: Mobile
    { name: 'mobile-chrome', use: { ...devices['Pixel 5'] } },
    { name: 'mobile-safari', use: { ...devices['iPhone 13'] } },
  ]
});
```

### CI Strategy

```yaml
# Run Chromium always, others weekly
- name: E2E Chromium (daily)
  run: pnpm test:e2e --project=chromium

- name: E2E All Browsers (weekly)
  if: github.event.schedule == '0 0 * * 0' # Sunday
  run: pnpm test:e2e
```

---

## 📝 Checklist

- [ ] Add Firefox to config
- [ ] Add WebKit (Safari) to config
- [ ] Test locally on all 3 browsers
- [ ] Configure CI matrix
- [ ] Document browser-specific issues
- [ ] Add mobile viewports (optional)

---

**Target**: 3+ browsers (Chrome, Firefox, Safari)
