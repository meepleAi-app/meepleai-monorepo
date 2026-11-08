/**
 * Login reale via UI per ciascun ruolo, con salvataggio dello storageState.
 *
 * Non usa `authenticateViaAPI` né i cookie sintetici degli altri spec: il primo
 * elemento che l'audit verifica è che si riesca davvero ad accedere. Se questo
 * passo fallisce, è un finding — non un ostacolo da aggirare.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { mkdirSync } from 'node:fs';

import { expect, test } from '@playwright/test';

import { AUTH_DIR, authFile } from './auth-paths';

const CREDENTIALS = {
  user: { email: process.env.AUDIT_USER_EMAIL, password: process.env.AUDIT_USER_PASSWORD },
  admin: { email: process.env.AUDIT_ADMIN_EMAIL, password: process.env.AUDIT_ADMIN_PASSWORD },
};

for (const [role, creds] of Object.entries(CREDENTIALS)) {
  test(`login reale come ${role}`, async ({ page }) => {
    const varName = `AUDIT_${role.toUpperCase()}`;
    test.skip(
      !creds.email || !creds.password,
      `${varName}_EMAIL e ${varName}_PASSWORD non impostate: ruolo ${role} non verificabile`
    );

    mkdirSync(AUTH_DIR, { recursive: true });

    await page.goto('/login');
    // Selettori per tipo di campo: il form usa un componente con prop `label`,
    // che non garantisce un <label for> associabile con getByLabel.
    await page
      .locator('input[type="email"]')
      .first()
      .fill(creds.email as string);
    await page
      .locator('input[type="password"]')
      .first()
      .fill(creds.password as string);
    await page.locator('button[type="submit"]').first().click();

    // Il login è riuscito solo se lasciamo /login: un errore in-page lascia la
    // URL invariata e un'attesa generica lo scambierebbe per successo.
    await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });

    await page.context().storageState({ path: authFile(role) });
  });
}
