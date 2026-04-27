# Visual Migrated — V2 Phase 1

> Project Playwright `visual-migrated-{desktop,mobile}` (vedi `apps/web/playwright.config.ts`)

Spec di **visual contract** per le route migrate Phase 1 (Wave A→D, issue umbrella #578).

## Cosa contiene

Una spec per route migrata, esempio `sp3-faq-enhanced.spec.ts` (#583, Wave A.1 FAQ pilot).
Ogni spec naviga la route prod Next.js (`localhost:3000`) con dati stub equivalenti al `data.js` del mockup, e confronta lo screenshot con la PNG mockup baseline (committata in PR #575) referenziata da `apps/web/e2e/visual-mockups/baseline.spec.ts-snapshots/<slug>-{desktop,mobile}.png`.

## Pattern (TDD red→green)

1. **Red** — scrivi la spec referenziando lo snapshot mockup esistente. Test fallisce su `feature/<slug>` perché la route è ancora v1.
2. **Green** — implementa v2 (componenti, i18n, dati stub). Snapshot matcha → CI green.

```ts
import { test, expect } from '@playwright/test';

test('FAQ — default state matches mockup', async ({ page }) => {
  await page.goto('/faq');
  await page.waitForLoadState('networkidle');
  await expect(page).toHaveScreenshot('sp3-faq-enhanced.png', {
    fullPage: true,
    mask: [page.locator('[data-dynamic]')],
  });
});
```

> Lo snapshot path qui (`sp3-faq-enhanced.png`) deve combaciare con il mockup baseline filename (vedi snapshot dir di `baseline.spec.ts`).

## Comandi

```bash
pnpm test:visual:migrated          # desktop + mobile
pnpm test:visual:migrated:desktop
pnpm test:visual:migrated:mobile
```

## Riferimenti

- `docs/frontend/visual-regression.md` — sezione "Phase 1+ — Visual Migrated + V2 States"
- `docs/superpowers/specs/2026-04-27-v2-migration-phase1-execution.md`
- `docs/superpowers/specs/2026-04-27-v2-migration-wave-a-1-faq.md`
