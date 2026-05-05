# V2 States — Phase 1 State Coverage

> Project Playwright `v2-states-{desktop,mobile}` (vedi `apps/web/playwright.config.ts`)

Spec di **state coverage** per le route migrate Phase 1 (Wave A→D, issue umbrella #578).

## Cosa contiene

Una spec per route migrata, esempio `faq.spec.ts` (#583, Wave A.1).
Ogni spec cattura **4 stati** della route con dati stubbati via `page.route()`:

| Stato | Stub | Snapshot |
|---|---|---|
| `default` | dati realistici (≥1 entity) | `<route>-default-{desktop,mobile}.png` |
| `empty` | API ritorna `[]` o no data | `<route>-empty-{desktop,mobile}.png` |
| `loading` | request `await new Promise(() => {})` (mai-resolve) | `<route>-loading-{desktop,mobile}.png` |
| `error` | API ritorna 500 / network error | `<route>-error-{desktop,mobile}.png` |

## Pattern

```ts
import { test, expect } from '@playwright/test';

test.describe('FAQ — state coverage', () => {
  test('default state', async ({ page }) => {
    await page.goto('/faq');
    await page.waitForLoadState('networkidle');
    await expect(page).toHaveScreenshot('faq-default.png', {
      fullPage: true,
      mask: [page.locator('[data-dynamic]')],
    });
  });

  test('empty state (search no match)', async ({ page }) => {
    await page.goto('/faq#q=zzzzzzz');
    await page.waitForLoadState('networkidle');
    await expect(page).toHaveScreenshot('faq-empty.png', { fullPage: true });
  });

  test('loading state', async ({ page }) => {
    await page.route('**/api/v1/faq', () => new Promise(() => {})); // never resolves
    await page.goto('/faq');
    await expect(page).toHaveScreenshot('faq-loading.png', { fullPage: true });
  });

  test('error state', async ({ page }) => {
    await page.route('**/api/v1/faq', (route) =>
      route.fulfill({ status: 500, body: '{"error":"internal"}' }),
    );
    await page.goto('/faq');
    await expect(page).toHaveScreenshot('faq-error.png', { fullPage: true });
  });
});
```

> Snapshot di prima generazione richiedono `--update-snapshots` una volta. Successivi diff = regression.

## Comandi

```bash
pnpm test:v2-states          # desktop + mobile
pnpm test:v2-states:desktop
pnpm test:v2-states:mobile
```

## Snapshot dir

`apps/web/e2e/v2-states/<route>.spec.ts-snapshots/` (auto-creato da Playwright al primo run con `--update-snapshots`).

## Riferimenti

- `docs/frontend/visual-regression.md` — sezione "Phase 1+ — Visual Migrated + V2 States"
- `docs/superpowers/specs/2026-04-27-v2-migration-wave-a-1-faq.md` — sezione 6 (Test plan)
