/**
 * Mobile golden-path B1 — chat input testid contract (#1816 P2-1).
 *
 * Regression: at viewport <1024px (Tailwind `lg` breakpoint), the mobile chat
 * surface (`ChatMobile`) renders a <textarea> + send button that must carry the
 * same `data-testid` attributes as the desktop `ChatThreadView`/`ChatInputArea`
 * so that E2E specs targeting `getByTestId('message-input')` resolve to the
 * VISIBLE mobile element on the `mobile-chrome` / `mobile-safari` projects.
 *
 * Pre-fix DOM contained:
 *   [0] visible mobile <textarea> (303×42 @ y=726), NO data-testid
 *   [1] hidden desktop <textarea> with `data-testid="message-input"` (display:none)
 *
 * Audit: `docs/for-developers/audits/2026-06-02-mobile-golden-path-audit.md` § P2 testid.
 *
 * Runs only when the active project viewport is < lg (1024px). Auto-skips on
 * desktop-chrome / desktop-firefox / desktop-safari / tablet-chrome.
 */
import { test, expect } from '@playwright/test';

const MOCK_THREAD_ID = 'b1-thread-1816';
const MOCK_USER_ID = 'b1-user-1816';

test.describe('Mobile golden-path B1 — message-input testid', () => {
  test.beforeEach(async ({ page }) => {
    // Auth — FE still calls /auth/me even under PLAYWRIGHT_AUTH_BYPASS to
    // populate the auth context store.
    await page.context().route('**/api/v1/auth/me', async route => {
      await route.fulfill({
        json: {
          id: MOCK_USER_ID,
          email: 'b1@meepleai.app',
          displayName: 'B1 Mobile Test',
          role: 'User',
          tier: 'free',
        },
      });
    });

    // Thread data — minimal stub: no messages, no agent, no game so neither
    // SSE stream nor RAG QA pipeline kicks in.
    await page.context().route(`**/api/v1/chat-threads/${MOCK_THREAD_ID}`, async route => {
      await route.fulfill({
        json: {
          id: MOCK_THREAD_ID,
          title: 'B1 mobile testid contract',
          agentId: null,
          gameId: null,
          createdAt: new Date().toISOString(),
          updatedAt: new Date().toISOString(),
          messages: [],
        },
      });
    });
  });

  test('mobile chat textarea exposes data-testid="message-input"', async ({ page, viewport }) => {
    test.skip(
      (viewport?.width ?? 0) >= 1024,
      'mobile-only contract — desktop renders ChatThreadView with its own testid'
    );

    await page.goto(`/chat/${MOCK_THREAD_ID}`);
    await page.waitForLoadState('domcontentloaded');

    // Both ChatMobile and ChatThreadView are mounted in DOM under responsive
    // `lg:hidden` / `hidden lg:block` wrappers. At <lg only the mobile one is
    // visible. Assert that exactly ONE visible element carries the testid.
    const visibleInputs = page.locator('[data-testid="message-input"]:visible');
    await expect(visibleInputs).toHaveCount(1);

    // The visible element must be a <textarea> (mobile pattern).
    const tagName = await visibleInputs.evaluate(el => el.tagName.toLowerCase());
    expect(tagName).toBe('textarea');
  });

  test('mobile send button exposes data-testid="send-btn"', async ({ page, viewport }) => {
    test.skip(
      (viewport?.width ?? 0) >= 1024,
      'mobile-only contract — desktop ChatInputArea also carries send-btn'
    );

    await page.goto(`/chat/${MOCK_THREAD_ID}`);
    await page.waitForLoadState('domcontentloaded');

    const visibleSendBtn = page.locator('[data-testid="send-btn"]:visible');
    await expect(visibleSendBtn).toHaveCount(1);
  });
});
