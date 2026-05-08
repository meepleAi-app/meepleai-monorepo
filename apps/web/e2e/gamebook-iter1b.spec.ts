import { test, expect } from '@playwright/test';

test.describe('Iter 1.B — Photo translate flow', () => {
  test('@ci translate page renders viewer with mocked artifact', async ({ page }) => {
    const campaignId = '11111111-1111-4111-a111-111111111111';
    const gameId = '22222222-2222-4222-a222-222222222222';
    const photoId = '33333333-3333-4333-a333-333333333333';

    const fakeArtifact = {
      id: photoId,
      campaignId,
      pageType: 'Storybook',
      status: 'Segmented',
      ocrFullText: '§47 The cave is dark.',
      segments: [{ paragraphNumber: 47, sourceText: 'The cave is dark.', boundingBox: null }],
      failureReason: null,
      createdAt: '2026-05-08T10:00:00Z',
      expiresAt: '2026-05-09T10:00:00Z',
    };

    await page.context().route('**/api/v1/gamebook/campaigns/*/photos', async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify(fakeArtifact),
        });
        return;
      }
      await route.fallback();
    });

    await page.context().route('**/api/v1/gamebook/campaigns/*/photos/*/segment', async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(fakeArtifact),
      });
    });

    await page.goto(`/library/games/${gameId}/play/${campaignId}/translate`);
    await expect(page.getByText(/Traduci pagina libro game/i)).toBeVisible({ timeout: 10000 });
    await expect(page.getByTestId('open-camera-button')).toBeVisible();
    await expect(page.getByTestId('page-type-select')).toBeVisible();
  });

  test.skip('@dogfood Aaron full storybook page translate flow with real OCR + DeepSeek', () => {
    // Manual run by Aaron — NOT in CI. See design doc §3 N3 scenarios.
  });
});
