/**
 * Cover R2-strict E2E (issue #3498) — SCAFFOLD, needs CI validation.
 *
 * Replaces the mocked `cover-l4.spec.ts` fixme (`src.match(/\.webp/)`, a false-green) with a
 * REAL-LOAD assertion against a real backend + MinIO (STORAGE_PROVIDER=s3, presign public host
 * localhost:9000 via #3535). Proves the invariant «the cover is served from R2/MinIO, never the
 * submitted input host» AND that the image actually loads (not just that the URL ends in .webp).
 *
 * Runs ONLY in the dedicated e2e-cover-r2-strict job (real backend + `--profile storage`).
 *
 * Prereq (seeded by the workflow, TODO to finalize in CI):
 *   - a game whose PdfCoverR2Key points at an object put into the MinIO `meepleai-uploads` bucket;
 *   - the browser can reach http://localhost:9000 (published MinIO port, compose.e2e-storage.yml).
 */
import { test, expect } from '@playwright/test';

// TODO(#3498): source these from the seeded fixture the workflow provisions (game id/title + the
// MinIO object key). Kept as constants until the seed step lands.
const SEEDED_GAME_TITLE = process.env.E2E_COVER_GAME_TITLE ?? 'Catan R2 Strict';
const MINIO_PRESIGN_HOST = 'localhost:9000';

test.describe('cover R2-strict (real backend + MinIO)', () => {
  test('the library cover loads from MinIO and never from the input host', async ({ page }) => {
    await page.goto('/library');
    await page.waitForLoadState('networkidle');

    const card = page.getByText(SEEDED_GAME_TITLE).first();
    await expect(card).toBeVisible({ timeout: 10_000 });

    const cardContainer = card.locator('xpath=ancestor::*[self::article or self::div][1]');
    const img = cardContainer.locator('img').first();
    await expect(img).toBeVisible({ timeout: 5_000 });

    // Real-load assertion (NOT a URL match): the browser must have decoded a non-empty image,
    // which is only true if the presigned MinIO URL was actually reachable and served bytes.
    await expect
      .poll(async () => img.evaluate((el: HTMLImageElement) => el.naturalWidth), { timeout: 8_000 })
      .toBeGreaterThan(0);

    // The resolved host is MinIO (the R2/allowlist host), never the submitted/input host.
    const src = await img.getAttribute('src');
    expect(src).toBeTruthy();
    expect(new URL(src!).host).toBe(MINIO_PRESIGN_HOST);
  });
});
