// catan-flavor-l2-l3.spec.ts
import { test, expect } from '@playwright/test';

// The Catan flavor tab is gated on liveSessionDto (sessionQuery.data), which is
// null in ?fixture=host (the LiveSessionFixture is minimal, no gameSlug). So the
// positive path can only be exercised against a real Catan session — deferred
// (same known limitation as the SSE smoke test, #3033 spec § Testing).
test.describe('Catan rich flavor (#3033)', () => {
  test('regression guard: a non-Catan fixture session shows no flavor tab', async ({ page }) => {
    await page.goto('/sessions/demo/live?fixture=host');
    await expect(page.getByRole('tab', { name: /catan|flavor/i })).toHaveCount(0);
  });

  test.fixme('host can generate the board and record a dice roll (needs a real Catan session)', async () => {
    // Requires a seeded Catan live session over HTTP; not reachable in fixture mode.
  });
});
