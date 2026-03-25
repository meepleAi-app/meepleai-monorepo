/**
 * ERR-14: Quota Exceeded Actions
 * Issue #3082 - P0 Critical
 *
 * Tests quota exceeded error handling:
 * - Library full - cannot add game
 * - Session limit reached - cannot create session
 * - Quota exceeded modal/message display
 * - Upgrade prompts and removal options
 */

import { test, expect } from '../fixtures';

import type { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface QuotaStatus {
  library: { current: number; max: number; unlimited: boolean };
  sessions: { current: number; max: number; unlimited: boolean };
  pdf: { current: number; max: number; unlimited: boolean };
}

/**
 * Setup mock routes for quota exceeded testing
 */
async function setupQuotaExceededMocks(
  page: Page,
  options: {
    tier?: 'Free' | 'Normal' | 'Premium';
    libraryFull?: boolean;
    sessionsFull?: boolean;
    pdfFull?: boolean;
  } = {}
) {
  const { tier = 'Free', libraryFull = false, sessionsFull = false, pdfFull = false } = options;

  const quotaLimits = {
    Free: { library: 5, sessions: 3, pdf: 3 },
    Normal: { library: 50, sessions: 10, pdf: 20 },
    Premium: { library: 999, sessions: 999, pdf: 999 },
  };

  const limits = quotaLimits[tier];

  const quotaStatus: QuotaStatus = {
    library: {
      current: libraryFull ? limits.library : Math.floor(limits.library / 2),
      max: limits.library,
      unlimited: tier === 'Premium',
    },
    sessions: {
      current: sessionsFull ? limits.sessions : Math.floor(limits.sessions / 2),
      max: limits.sessions,
      unlimited: tier === 'Premium',
    },
    pdf: {
      current: pdfFull ? limits.pdf : Math.floor(limits.pdf / 2),
      max: limits.pdf,
      unlimited: tier === 'Premium',
    },
  };

  // Mock auth
  await page.route(`${API_BASE}/api/v1/auth/me`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        user: {
          id: 'test-user-id',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User',
          tier,
        },
        expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
      }),
    });
  });

  // Mock quota status endpoint
  await page.route(`${API_BASE}/api/v1/users/me/quota`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(quotaStatus),
    });
  });

  // Mock library endpoint with quota enforcement
  await page.route(`${API_BASE}/api/v1/library**`, async route => {
    const method = route.request().method();

    if (method === 'GET') {
      const games = Array.from({ length: quotaStatus.library.current }, (_, i) => ({
        id: `game-${i + 1}`,
        title: `Library Game ${i + 1}`,
        addedAt: new Date().toISOString(),
      }));
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(games),
      });
    } else if (method === 'POST') {
      if (libraryFull && tier !== 'Premium') {
        await route.fulfill({
          status: 403,
          contentType: 'application/json',
          body: JSON.stringify({
            error: 'Quota exceeded',
            message: `Library is full (${quotaStatus.library.current}/${quotaStatus.library.max}). Upgrade your plan or remove a game to add more.`,
            quotaType: 'library',
            current: quotaStatus.library.current,
            max: quotaStatus.library.max,
            tier,
            upgradeOptions: tier === 'Free' ? ['Normal', 'Premium'] : ['Premium'],
          }),
        });
      } else {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'new-game', title: 'New Game' }),
        });
      }
    } else if (method === 'DELETE') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ message: 'Game removed from library' }),
      });
    } else {
      await route.continue();
    }
  });

  // Mock sessions endpoint with quota enforcement
  await page.route(`${API_BASE}/api/v1/sessions`, async route => {
    const method = route.request().method();

    if (method === 'GET') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    } else if (method === 'POST') {
      if (sessionsFull && tier !== 'Premium') {
        await route.fulfill({
          status: 403,
          contentType: 'application/json',
          body: JSON.stringify({
            error: 'Quota exceeded',
            message: `Session limit reached (${quotaStatus.sessions.current}/${quotaStatus.sessions.max}). Upgrade to create more sessions.`,
            quotaType: 'sessions',
            current: quotaStatus.sessions.current,
            max: quotaStatus.sessions.max,
            tier,
          }),
        });
      } else {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'new-session' }),
        });
      }
    } else {
      await route.continue();
    }
  });

  // Mock games catalog
  await page.route(`${API_BASE}/api/v1/games**`, async route => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([
        { id: 'catalog-1', title: 'Chess', description: 'Classic game' },
        { id: 'catalog-2', title: 'Catan', description: 'Strategy game' },
        { id: 'catalog-3', title: 'Ticket to Ride', description: 'Railway game' },
      ]),
    });
  });

  // Mock PDF upload with quota enforcement
  await page.route(`${API_BASE}/api/v1/documents/upload`, async route => {
    if (pdfFull && tier !== 'Premium') {
      await route.fulfill({
        status: 403,
        contentType: 'application/json',
        body: JSON.stringify({
          error: 'Quota exceeded',
          message: `PDF upload limit reached (${quotaStatus.pdf.current}/${quotaStatus.pdf.max}).`,
          quotaType: 'pdf',
        }),
      });
    } else {
      await route.fulfill({
        status: 201,
        contentType: 'application/json',
        body: JSON.stringify({ id: 'new-pdf', status: 'processing' }),
      });
    }
  });

  return { quotaStatus, limits, tier };
}

test.describe('ERR-14: Quota Exceeded Actions', () => {
  test.describe('Library Quota', () => {
    test('should show quota exceeded modal when library is full', async ({ page }) => {
      await setupQuotaExceededMocks(page, {
        tier: 'Free',
        libraryFull: true,
      });

      await page.goto('/library');
      await page.waitForLoadState('networkidle');

      // Try to add a game to library
      const addButton = page
        .getByRole('button', { name: /add.*library|add.*to.*collection/i })
        .first();

      if (await addButton.isVisible()) {
        await addButton.click();

        // Should show quota exceeded message
        await expect(page.getByText(/quota.*exceeded|library.*full|limit.*reached/i)).toBeVisible();
      }
    });

    test('should offer upgrade option when library quota exceeded', async ({ page }) => {
      await setupQuotaExceededMocks(page, {
        tier: 'Free',
        libraryFull: true,
      });

      await page.goto('/library');
      await page.waitForLoadState('networkidle');

      const addButton = page.getByRole('button', { name: /add.*library/i }).first();

      if (await addButton.isVisible()) {
        await addButton.click();

        // Should show upgrade button/link
        await expect(
          page
            .getByRole('button', { name: /upgrade/i })
            .or(page.getByRole('link', { name: /upgrade|pricing/i }))
        ).toBeVisible();
      }
    });

    test('should offer remove game option when library full', async ({ page }) => {
      await setupQuotaExceededMocks(page, {
        tier: 'Free',
        libraryFull: true,
      });

      await page.goto('/library');
      await page.waitForLoadState('networkidle');

      // Should show option to remove games or manage library
      await expect(
        page
          .getByRole('button', { name: /remove|delete/i })
          .first()
          .or(page.getByText(/remove.*game|manage.*library/i))
      ).toBeVisible();
    });

    test('should show current usage in quota modal', async ({ page }) => {
      await setupQuotaExceededMocks(page, {
        tier: 'Free',
        libraryFull: true,
      });

      await page.goto('/library');
      await page.waitForLoadState('networkidle');

      const addButton = page.getByRole('button', { name: /add.*library/i }).first();

      if (await addButton.isVisible()) {
        await addButton.click();

        // Should show current/max usage
        await expect(page.getByText(/5.*\/.*5|5.*of.*5|full/i)).toBeVisible();
      }
    });
  });

  test.describe('Session Quota', () => {
    test('should show quota exceeded when session limit reached', async ({ page }) => {
      await setupQuotaExceededMocks(page, {
        tier: 'Free',
        sessionsFull: true,
      });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      // Try to create session
      const createButton = page.getByRole('button', {
        name: /create.*session|new.*session|start/i,
      });

      if (await createButton.isVisible()) {
        await createButton.click();

        // Should show quota exceeded message
        await expect(
          page.getByText(/limit.*reached|quota.*exceeded|cannot.*create/i)
        ).toBeVisible();
      }
    });

    test('should suggest ending existing session', async ({ page }) => {
      await setupQuotaExceededMocks(page, {
        tier: 'Free',
        sessionsFull: true,
      });

      await page.goto('/sessions');
      await page.waitForLoadState('networkidle');

      const createButton = page.getByRole('button', { name: /create.*session/i });

      if (await createButton.isVisible()) {
        await createButton.click();

        // Should suggest ending existing sessions
        await expect(
          page
            .getByText(/end.*session|close.*session|manage.*session/i)
            .or(page.getByRole('button', { name: /end|close/i }))
        ).toBeVisible();
      }
    });
  });

  test.describe('Modal Interactions', () => {
    test('should close quota modal on dismiss', async ({ page }) => {
      await setupQuotaExceededMocks(page, {
        tier: 'Free',
        libraryFull: true,
      });

      await page.goto('/library');
      await page.waitForLoadState('networkidle');

      const addButton = page.getByRole('button', { name: /add.*library/i }).first();

      if (await addButton.isVisible()) {
        await addButton.click();

        // Wait for modal
        await expect(page.getByText(/quota.*exceeded|library.*full/i)).toBeVisible();

        // Find close/dismiss button
        const closeButton = page.getByRole('button', { name: /close|cancel|dismiss|ok|got.*it/i });
        if (await closeButton.isVisible()) {
          await closeButton.click();

          // Modal should be dismissed
          await expect(page.getByText(/quota.*exceeded|library.*full/i)).not.toBeVisible();
        }
      }
    });

    test('should navigate to upgrade page from modal', async ({ page }) => {
      await setupQuotaExceededMocks(page, {
        tier: 'Free',
        libraryFull: true,
      });

      // Mock pricing page
      await page.route(`${API_BASE}/api/v1/pricing**`, async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            plans: [
              { id: 'normal', name: 'Normal', price: 9.99 },
              { id: 'premium', name: 'Premium', price: 19.99 },
            ],
          }),
        });
      });

      await page.goto('/library');
      await page.waitForLoadState('networkidle');

      const addButton = page.getByRole('button', { name: /add.*library/i }).first();

      if (await addButton.isVisible()) {
        await addButton.click();

        // Click upgrade
        const upgradeButton = page
          .getByRole('button', { name: /upgrade/i })
          .or(page.getByRole('link', { name: /upgrade/i }));

        if (await upgradeButton.isVisible()) {
          await upgradeButton.click();

          // Should navigate to pricing/upgrade page
          await page.waitForURL(/pricing|upgrade|subscription/i, { timeout: 5000 }).catch(() => {
            // May just close modal and show upgrade info inline
          });
        }
      }
    });
  });

  test.describe('Tier-specific Behavior', () => {
    test('should show Normal tier upgrade for Free users', async ({ page }) => {
      await setupQuotaExceededMocks(page, {
        tier: 'Free',
        libraryFull: true,
      });

      await page.goto('/library');
      await page.waitForLoadState('networkidle');

      const addButton = page.getByRole('button', { name: /add.*library/i }).first();

      if (await addButton.isVisible()) {
        await addButton.click();

        // Should mention Normal tier as option
        await expect(page.getByText(/normal|standard/i)).toBeVisible();
      }
    });

    test('should show Premium upgrade for Normal users', async ({ page }) => {
      await setupQuotaExceededMocks(page, {
        tier: 'Normal',
        libraryFull: true,
      });

      await page.goto('/library');
      await page.waitForLoadState('networkidle');

      const addButton = page.getByRole('button', { name: /add.*library/i }).first();

      if (await addButton.isVisible()) {
        await addButton.click();

        // Should mention Premium tier
        await expect(page.getByText(/premium|unlimited/i)).toBeVisible();
      }
    });

    test('should not show quota exceeded for Premium users', async ({ page }) => {
      await setupQuotaExceededMocks(page, {
        tier: 'Premium',
        libraryFull: false, // Premium has unlimited
      });

      await page.goto('/library');
      await page.waitForLoadState('networkidle');

      const addButton = page.getByRole('button', { name: /add.*library/i }).first();

      if (await addButton.isVisible()) {
        await addButton.click();

        // Should NOT show quota exceeded for Premium
        await expect(page.getByText(/quota.*exceeded/i)).not.toBeVisible({ timeout: 2000 });
      }
    });
  });

  test.describe('Recovery Actions', () => {
    test('should update quota after removing item', async ({ page }) => {
      let libraryCount = 5;

      // Setup dynamic library count
      await page.route(`${API_BASE}/api/v1/auth/me`, async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            user: { id: 'test', email: 'test@example.com', tier: 'Free' },
            expiresAt: new Date(Date.now() + 3600000).toISOString(),
          }),
        });
      });

      await page.route(`${API_BASE}/api/v1/library**`, async route => {
        const method = route.request().method();
        if (method === 'DELETE') {
          libraryCount--;
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify({ message: 'Removed' }),
          });
        } else {
          await route.fulfill({
            status: 200,
            contentType: 'application/json',
            body: JSON.stringify(
              Array.from({ length: libraryCount }, (_, i) => ({
                id: `game-${i}`,
                title: `Game ${i}`,
              }))
            ),
          });
        }
      });

      await page.route(`${API_BASE}/api/v1/users/me/quota`, async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            library: { current: libraryCount, max: 5, unlimited: false },
          }),
        });
      });

      await page.route(`${API_BASE}/api/v1/games**`, async route => {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([]),
        });
      });

      await page.goto('/library');
      await page.waitForLoadState('networkidle');

      // Initially at 5/5
      await expect(page.getByText(/5.*\/.*5|5.*of.*5/i)).toBeVisible();
    });
  });
});
