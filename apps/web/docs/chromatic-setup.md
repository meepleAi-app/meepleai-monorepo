# Chromatic Setup - Dual Configuration

MeepleAI uses **two separate Chromatic setups**:

## 1️⃣ Chromatic Storybook (Visual Regression - Components)

**Purpose**: Visual regression testing for UI components

**Configuration**: `chromatic.config.json`

**Usage**:
```bash
# Local development
pnpm storybook              # Start Storybook on localhost:6006
pnpm build-storybook        # Build static Storybook

# Visual regression
pnpm test:visual            # Run Chromatic with exit-zero-on-changes
pnpm test:visual:ci         # CI mode with upload
```

**What it tests**:
- Component visual appearance
- All Storybook stories (*.stories.tsx)
- Style changes, color, layout
- Dark theme variants

**Status**: ✅ **Configured and working**

---

## 2️⃣ Chromatic Playwright (E2E Visual Testing)

**Purpose**: Visual regression for full-page E2E tests

**Configuration**: `playwright.config.ts` (test fixture, NOT reporter)

**Setup**:
1. ✅ Install: `pnpm add -D @chromatic-com/playwright`
2. ✅ Import ChromaticConfig in playwright.config.ts
3. ✅ Set env var in `.env.local`: `CHROMATIC_PROJECT_TOKEN=chpt_293b6e01ef6f39d`
4. ✅ Add Chromatic fixture options to `use` config

**Configuration** (`playwright.config.ts`):
```typescript
import { defineConfig } from '@playwright/test';
import { ChromaticConfig } from '@chromatic-com/playwright';

export default defineConfig<ChromaticConfig>({
  reporter: ['html'], // Standard reporters only
  use: {
    disableAutoSnapshot: false, // Chromatic auto-capture
    cropToViewport: false,
    // ... other options
  }
});
```

**Usage**:
```bash
# Step 1: Run E2E tests (generates archives)
npx playwright test

# Step 2: Upload to Chromatic
npx chromatic --playwright --project-token=$CHROMATIC_PROJECT_TOKEN
# Or use script:
pnpm test:visual:e2e
```

**Important**: Chromatic Playwright is a **test fixture**, not a reporter!
❌ Don't add to `reporter` array
✅ Import in config and use fixture options

**What it tests**:
- Full-page screenshots during E2E tests
- User flows and interactions
- Responsive layouts across devices
- Real browser rendering

**Status**: ✅ **Configured with Playwright 1.57.0**

---

## Current Issue

**Problem**: `npx chromatic --playwright` fails with:
```
Chromatic archives directory cannot be found: test-results/chromatic-archives
```

**Cause**: E2E tests must run successfully first to generate archives.

**Solution**:

### Option A: Fix E2E Tests First (Recommended)
```bash
# Fix failing auth tests in e2e/chat.spec.ts
# Then run:
npx playwright test
npx chromatic --playwright
```

### Option B: Use Storybook Chromatic Only
```bash
# This already works perfectly:
pnpm test:visual
```

---

## Recommendation

**Per MeepleAvatar**: Usa **Chromatic Storybook** (già funzionante)

```bash
cd apps/web
pnpm build-storybook
pnpm test:visual
```

Questo cattura visual regression delle 12 stories di MeepleAvatar senza bisogno di E2E tests.

**Chromatic Playwright** è opzionale e serve per visual testing di pagine complete, non necessario per componenti UI isolati.

---

## Files Modified

- ✅ `playwright.config.ts`: Added @chromatic-com/playwright reporter
- ✅ `.env.local`: Added CHROMATIC_PROJECT_TOKEN
- ✅ `package.json`: Added @chromatic-com/playwright dependency

---

**Next Steps**:
1. Usa `pnpm test:visual` per Storybook (già pronto)
2. Opzionale: Risolvi E2E test failures se vuoi usare Chromatic Playwright
