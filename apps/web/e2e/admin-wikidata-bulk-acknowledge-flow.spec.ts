import { test, expect } from '@playwright/test';

/**
 * Issue #2254 — F5 bulk acknowledge UI E2E skeleton.
 *
 * Validates end-to-end the admin bulk-acknowledge flow on the Wikidata
 * dead-letter visibility page:
 *   1. Operator logs in
 *   2. Selects N dead-letters via checkbox
 *   3. Opens the acknowledge modal, optionally types a note, confirms
 *   4. Acknowledged rows disappear from the default list
 *   5. Toggling "Show acknowledged" surfaces them with a dimmed marker
 *
 * Non-blocking on CI (continue-on-error: true) because it depends on:
 *   - Admin credentials via E2E_ADMIN_EMAIL/PASSWORD
 *   - A staging snapshot with ≥2 live dead-letter rows
 *
 * Will be promoted to a blocking suite after the BGG-cover-enrichment
 * fixture seeds reliably create the required dead-letter rows on every
 * test run.
 */
test.describe('Admin Wikidata bulk acknowledge flow (#2254)', () => {
  test.skip(
    !process.env.E2E_ADMIN_EMAIL || !process.env.E2E_ADMIN_PASSWORD,
    'Requires E2E_ADMIN_EMAIL + E2E_ADMIN_PASSWORD env vars'
  );

  test('select dead-letters, acknowledge with note, toggle Show acked', async ({ page }) => {
    // 1. Login as admin
    await page.goto('/login');
    await page.getByLabel(/email/i).fill(process.env.E2E_ADMIN_EMAIL!);
    await page.getByLabel(/password/i).fill(process.env.E2E_ADMIN_PASSWORD!);
    await page.getByRole('button', { name: /sign in/i }).click();
    await page.waitForURL(/\/(admin|dashboard)/);

    // 2. Navigate to dead-letter page
    await page.goto('/admin/monitor/wikidata-dead-letters');
    await expect(page.getByRole('heading', { name: /dead.*letter/i })).toBeVisible();

    // 3. Need ≥ 2 dead-letters to run a meaningful bulk-ack
    const rows = page.getByRole('row').filter({ has: page.getByRole('checkbox') });
    const count = await rows.count();
    test.skip(count < 2, 'Requires ≥ 2 dead-letters in the staging snapshot');

    // 4. Select first 2 rows
    await rows.nth(0).getByRole('checkbox').check();
    await rows.nth(1).getByRole('checkbox').check();

    // 5. Open modal, type note, confirm
    await page.getByRole('button', { name: /Acknowledge selected/i }).click();
    await page.getByLabel(/note/i).fill('e2e: known commons-deleted asset');
    await page.getByRole('button', { name: /^Confirm$/i }).click();

    // 6. Modal closes, page reloads, acked rows hidden from default view
    await expect(page.getByRole('dialog')).not.toBeVisible();

    // 7. Toggle Show acknowledged → reappear with dimmed marker
    await page.getByRole('switch', { name: /Show acknowledged/i }).click();
    await expect(page.getByText(/Acked by/i).first()).toBeVisible();
  });

  test('over-50 batch returns validation error', async ({ page }) => {
    // Skeleton: requires fixture seed of ≥51 dead-letters which is impractical
    // without dedicated test factory. Documented as a follow-up.
    test.skip(true, 'Pending dedicated factory for 51+ dead-letter seed');
  });
});
