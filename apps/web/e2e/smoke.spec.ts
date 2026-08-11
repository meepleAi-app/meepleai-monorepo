/**
 * Smoke Test — post-deploy health check contro lo staging live.
 * Eseguito da deploy-staging.yml (job "Staging E2E Smoke Tests") su meepleai.app.
 *
 * ⚠️ Cosa questo smoke PUÒ e NON PUÒ verificare (#2802):
 * Lo staging fa auth SERVER-SIDE nel middleware `src/proxy.ts` (redirect delle
 * PROTECTED_ROUTES → /login) e `PLAYWRIGHT_AUTH_BYPASS` NON è attivo su staging.
 * I `page.route` mock di Playwright intercettano solo le richieste del browser,
 * quindi NON possono autenticare contro il middleware: qualunque navigazione a
 * una route protetta senza una vera sessione `meepleai_session` viene rediretta
 * a /login prima del render. Il vecchio `mockAdminAuth` era perciò un no-op sullo
 * staging e gli step "carica dopo auth mock" testavano in realtà /login (redirect)
 * asserendo solo `body` → falsa confidenza.
 *
 * Questo smoke quindi verifica ciò che è realmente osservabile SENZA credenziali:
 *   1. la homepage pubblica carica,
 *   2. il login form è server-rendered (regressione SSR = fail, cfr. #2770/#2650),
 *   3. le route protette rediregono a /login se non autenticati (auth gate integro).
 * Il coverage del CONTENUTO autenticato (dashboard/admin/chat reali) richiede un
 * E2E con login applicativo reale (admin seedato via secret) — follow-up separato.
 */

import { test, expect } from '@playwright/test';

// Login page email field — server-rendered nell'HTML iniziale (#2650/#2770).
const EMAIL_INPUT = 'input[type="email"], input[name="email"]';

// Route protette (sottoinsieme rappresentativo di PROTECTED_ROUTES in src/proxy.ts):
// una user route, due admin route, una chat route. Ognuna DEVE redirigere a /login
// per un visitatore anonimo.
const PROTECTED_ROUTES = [
  '/dashboard',
  '/library',
  '/admin/overview',
  '/admin/agents/pipeline',
  '/admin/knowledge-base/documents',
  '/chat/new',
] as const;

test.describe('Smoke Test — deploy health', () => {
  test('1. Homepage pubblica carica', async ({ page }) => {
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    // Almeno un heading/CTA presente = la homepage ha renderizzato (non un errore).
    await expect(page.locator('h1, h2, [data-testid="hero"]').first()).toBeVisible({
      timeout: 8000,
    });
  });

  test('2. Login form server-rendered', async ({ page }) => {
    await page.goto('/login', { waitUntil: 'domcontentloaded' });
    // #2770/#2650: l'email input è nell'HTML SSR iniziale (nessun gate di hydration).
    // Timeout 15s: ampio per latenza VPS/CF ma stretto abbastanza da FALLIRE se l'SSR
    // regredisse (invece di mascherare via hydration client). NON mockare /auth/me:
    // simulerebbe un utente loggato → redirect da /login.
    await expect(page.locator(EMAIL_INPUT).first()).toBeVisible({ timeout: 15000 });
  });

  // Auth gate: ogni route protetta redirige un anonimo a /login (server-side proxy.ts).
  // Cattura regressioni reali: middleware auth giù, route rimossa da PROTECTED_ROUTES,
  // o /login rotto. Vedi header per perché non testiamo il contenuto autenticato.
  for (const [i, route] of PROTECTED_ROUTES.entries()) {
    test(`${i + 3}. Route protetta ${route} redirige a /login se non autenticato`, async ({
      page,
    }) => {
      await page.goto(route, { waitUntil: 'domcontentloaded' });
      await expect(page).toHaveURL(/\/login(\?|\/|$)/, { timeout: 15000 });
      // Il redirect deve atterrare sul login funzionante (form renderizzato).
      await expect(page.locator(EMAIL_INPUT).first()).toBeVisible({ timeout: 15000 });
    });
  }
});
