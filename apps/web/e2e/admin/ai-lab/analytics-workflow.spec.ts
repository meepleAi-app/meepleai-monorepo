/**
 * AI Lab — dashboard di analisi degli agenti.
 *
 * #4098 — riscritta, e ridotta da tre test a uno. La versione precedente non era mai stata
 * eseguita (#4092) ed era anonima, quindi le sue asserzioni cadevano sulla pagina di login.
 * Sistemata l'autenticazione, restavano tre problemi distinti:
 *
 *   1. `/admin/agents/catalog` non esiste, e le stringhe asserite (`Total Agents`,
 *      `Total Executions`) hanno **0 occorrenze** in `apps/web/src`. La dashboard reale è
 *      `/admin/agents/analytics`, in italiano: h1 `Analisi Agenti`, tab `Panoramica` /
 *      `Top Agenti` / `Tendenze`. Questo test è stato riscritto contro quella.
 *
 *   2. `/admin/analytics/chat` e `/admin/analytics/pdf` non esistono: `/admin/analytics` è
 *      una pagina unica a tab — `overview · ai-usage · audit · reports · api-keys` — senza
 *      una tab chat né una pdf. I due test sono stati ELIMINATI, non saltati, perché oltre
 *      alla rotta assente **non asserivano nulla**: il corpo era
 *      `await expect(page).toHaveURL(/\/admin\/analytics\/pdf/)` subito dopo aver navigato
 *      a quella stessa URL. Un test che verifica solo la URL che ha appena chiesto non può
 *      fallire per un difetto del prodotto, e infatti falliva per il redirect al login.
 *
 *   3. I loro commenti rimandavano a «will be implemented when #3815 / #3816 merges». Su
 *      GitHub quei numeri sono PR di osservabilità già mergiate, estranee all'AI Lab: sono
 *      riferimenti di uno schema di numerazione precedente. Non ripropagati.
 *
 * @see docs/for-developers/audits/2026-10-06-orphan-e2e-specs-classification.md
 */

import { test, expect } from '@playwright/test';

import {
  hasRealAdminCredentials,
  loginAsRealAdmin,
  MISSING_CREDENTIALS_REASON,
} from '../../_helpers/realAdminAuth';

test.describe('AI Lab - Analytics', () => {
  test.beforeEach(async ({ page }) => {
    test.skip(!hasRealAdminCredentials, MISSING_CREDENTIALS_REASON);
    await loginAsRealAdmin(page);
  });

  test('should load agent analytics dashboard with stats', async ({ page }) => {
    await page.goto('/admin/agents/analytics');

    await expect(page.getByRole('heading', { name: 'Analisi Agenti' })).toBeVisible();
    await expect(page.getByText('Panoramica').first()).toBeVisible();
  });
});
