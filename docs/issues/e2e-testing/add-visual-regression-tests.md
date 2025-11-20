# Add Visual Regression Tests

**Issue ID**: E2E-010 | **Priorità**: 🟢 BASSA | **Effort**: 8-12 ore

---

## 📋 Problem

Nessun test verifica **visual regression** (layout, styling, rendering). Chromatic è configurato per Storybook ma non integrato in E2E tests.

**Gap**:
- ❌ Layout breaks non rilevati
- ❌ CSS regression passa inosservata
- ❌ Cross-browser rendering issues
- ❌ Dark mode visual bugs

---

## 🎯 Impact

- **Quality**: UI bugs raggiungono produzione
- **UX**: Inconsistenza visuale non rilevata
- **Cross-browser**: Rendering differenze tra browsers

---

## ✅ Solution

### Option 1: Playwright Screenshots (Quick)

```typescript
test('chat page visual regression', async ({ page }) => {
  await page.goto('/chat');
  await page.waitForLoadState('networkidle');

  // Take screenshot
  await expect(page).toHaveScreenshot('chat-page.png', {
    maxDiffPixels: 100 // Allow small differences
  });
});
```

### Option 2: Chromatic Integration (Recommended)

```typescript
// Extend existing Chromatic setup to E2E
import { chromatic } from '@chromatic-com/playwright';

test('admin dashboard visual', async ({ page }) => {
  await page.goto('/admin');

  // Send to Chromatic for review
  await chromatic.capture(page, {
    name: 'Admin Dashboard',
    viewports: [1280, 375] // Desktop + Mobile
  });
});
```

### Option 3: Percy Integration

```yaml
# .github/workflows/visual-tests.yml
- name: Percy visual tests
  run: pnpm percy exec -- pnpm test:e2e:visual
  env:
    PERCY_TOKEN: ${{ secrets.PERCY_TOKEN }}
```

---

## 📝 Checklist

- [ ] Choose tool (Chromatic recommended, già configurato)
- [ ] Add visual tests for 10 critical pages
- [ ] Configure baselines
- [ ] Integrate in CI
- [ ] Document workflow

**Critical Pages**: Homepage, Chat, Login, Admin, Upload, Editor, Games, Profile, Settings, Error 404

---

**Target**: 10+ visual regression tests
