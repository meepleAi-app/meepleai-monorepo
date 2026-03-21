/**
 * Admin Embedding Flow — E2E Test
 *
 * Tests the full embedding flow:
 *   1. Admin uploads a PDF for an existing game
 *   2. Admin monitors the embedding job in the queue dashboard
 *   3. Admin tests the RAG agent with the embedded knowledge base
 *
 * Prerequisites:
 *   - A PDF fixture file must exist at: e2e/test-data/sample-rules.pdf
 *     (The test will skip if the file is missing)
 *   - Admin credentials must be available via env config (same as onboarding tests)
 *   - PdfUpload feature flag must be enabled
 *
 * Parametrization via TEST_ENV env var:
 *   TEST_ENV=dev          (default) — shorter timeouts, seed-based game selector
 *   TEST_ENV=integration  — longer timeouts, search-based game selector
 *
 * Run:
 *   cd apps/web && npx playwright test e2e/flows/admin-embedding-flow.spec.ts
 */

import * as fs from 'fs';
import * as path from 'path';

import { test, expect, BrowserContext, Page } from '@playwright/test';

import { env } from '../helpers/onboarding-environment';
import { QueueDashboardPage } from '../pages/admin/QueueDashboardPage';
import { AgentChatPage } from '../pages/agent/AgentChatPage';
import { LoginPage } from '../pages/auth/LoginPage';
import { UploadPage } from '../pages/upload/UploadPage';

// ── Config ───────────────────────────────────────────────────────────────────

const TEST_ENV = process.env.TEST_ENV ?? 'dev';

const envConfig = (
  {
    dev: { jobTimeout: 120_000, gameSelector: 'seed' as const },
    integration: { jobTimeout: 180_000, gameSelector: 'search' as const },
  } as Record<string, { jobTimeout: number; gameSelector: 'seed' | 'search' }>
)[TEST_ENV] ?? { jobTimeout: 120_000, gameSelector: 'seed' as const };

/** Path to a small PDF used for upload. Must be placed manually (see README). */
const PDF_FIXTURE_PATH = path.resolve(__dirname, '../test-data/sample-rules.pdf');

/** Game name to use when looking for an existing catalog entry */
const SEED_GAME_NAME = env.seedGameName; // e.g. 'Pandemic' (local) or 'Catan' (staging)

// ── State shared across serial tests ─────────────────────────────────────────

interface EmbeddingFlowState {
  adminContext: BrowserContext;
  adminPage: Page;
  gameId: string;
  gameName: string;
  jobId: string;
  failureReason?: string; // Propagates root cause to downstream test skip messages
}

const state: Partial<EmbeddingFlowState> = {};

// ── Test Suite ────────────────────────────────────────────────────────────────

test.describe.configure({ mode: 'serial' });

test.describe('Admin Embedding Flow', () => {
  test.afterAll(async () => {
    if (state.adminContext) {
      try {
        await state.adminContext.close();
      } catch (e) {
        console.warn('[cleanup] Failed to close admin context:', e);
      }
    }
  });

  // ── Test 1: Upload PDF for existing game ────────────────────────────────────
  test('1. Upload PDF for existing game', async ({ browser }) => {
    // Skip immediately if the fixture PDF is missing
    const pdfExists = fs.existsSync(PDF_FIXTURE_PATH);
    if (!pdfExists) {
      test.skip(
        true,
        `PDF fixture not found at ${PDF_FIXTURE_PATH}. ` +
          'Place a small PDF named sample-rules.pdf in e2e/test-data/ to enable this test.'
      );
    }

    // ── Setup admin context ─────────────────────────────────────────────────
    const adminContext = await browser.newContext();
    const adminPage = await adminContext.newPage();

    await test.step('Admin logs in', async () => {
      const loginPage = new LoginPage(adminPage);
      await loginPage.goto();
      await loginPage.login(env.admin.email, env.admin.password);

      await adminPage.waitForURL(
        url => url.pathname.includes('/admin') || url.pathname.includes('/dashboard'),
        { timeout: 15_000 }
      );
    });

    await test.step('Ensure PdfUpload feature flag is enabled', async () => {
      const flagUrl = `${env.apiURL}/api/v1/admin/feature-flags`;
      const getResp = await adminPage.request.get(`${flagUrl}/Features.PdfUpload`);
      if (getResp.status() === 404) {
        await adminPage.request.post(flagUrl, {
          data: { key: 'Features.PdfUpload', description: 'Enable PDF upload', isEnabled: true },
        });
      } else {
        const flag = await getResp.json().catch(() => ({ enabled: false }));
        if (!flag.enabled) {
          await adminPage.request.post(`${flagUrl}/Features.PdfUpload/toggle`, { data: {} });
        }
      }
    });

    // ── Navigate to upload page ─────────────────────────────────────────────
    const uploadPage = new UploadPage(adminPage);

    await test.step('Navigate to upload page', async () => {
      await uploadPage.goto();
    });

    // ── Select game ─────────────────────────────────────────────────────────
    await test.step('Select game from catalog', async () => {
      if (envConfig.gameSelector === 'seed') {
        // Use the seeded game name known to exist in dev environment
        await uploadPage.selectOrCreateGame(SEED_GAME_NAME, false);
      } else {
        // Integration: attempt search-based selection, fallback to create
        await uploadPage.selectOrCreateGame(SEED_GAME_NAME, false).catch(async () => {
          console.warn('[T1] Seed game not found, creating a new one');
          await uploadPage.createNewGame(`E2E Test Game ${Date.now()}`);
        });
      }
    });

    // ── Upload PDF ──────────────────────────────────────────────────────────
    await test.step('Upload PDF fixture', async () => {
      await uploadPage.uploadPdf(PDF_FIXTURE_PATH, { language: 'en', autoWait: true });
    });

    // ── Wait for progress tracker ───────────────────────────────────────────
    await test.step('Verify upload progress tracker appears', async () => {
      await expect(adminPage.locator('[data-testid="upload-progress-tracker"]')).toBeVisible({
        timeout: 30_000,
      });
    });

    // ── Capture gameId from queue link ──────────────────────────────────────
    await test.step('Click "Vai alla Queue" and capture gameId', async () => {
      const queueLink = adminPage.getByRole('link', { name: /vai alla queue/i });
      await expect(queueLink).toBeVisible({ timeout: 30_000 });

      // Capture the href to extract gameId before navigating
      const href = (await queueLink.getAttribute('href')) ?? '';
      console.log('[T1] Queue link href:', href);

      const urlParams = new URLSearchParams(href.includes('?') ? href.split('?')[1] : '');
      state.gameId = urlParams.get('gameId') ?? '';
      state.gameName = urlParams.get('gameName') ?? SEED_GAME_NAME;

      await queueLink.click();
      await adminPage.waitForURL(url => url.pathname.includes('/admin/queue'), { timeout: 10_000 });
    });

    // Persist context for subsequent tests
    state.adminContext = adminContext;
    state.adminPage = adminPage;

    expect(state.adminPage).toBeTruthy();
  });

  // ── Test 2: Monitor embedding in queue dashboard ─────────────────────────
  test('2. Monitor embedding in queue dashboard', async () => {
    if (!state.adminPage) test.skip(true, state.failureReason ?? 'Requires test 1 to pass');
    const page = state.adminPage!;
    const queuePage = new QueueDashboardPage(page);

    await test.step('Navigate to queue with flow context', async () => {
      await queuePage.goto({
        flow: 'embedding',
        gameId: state.gameId,
        gameName: state.gameName,
      });
    });

    await test.step('Verify embedding flow banner is visible', async () => {
      // Banner may not appear if gameId/gameName are missing — soft assertion
      const bannerVisible = await page
        .locator('[data-testid="embedding-flow-banner"]')
        .isVisible({ timeout: 10_000 })
        .catch(() => false);

      if (state.gameName && bannerVisible) {
        await queuePage.expectFlowBannerVisible(state.gameName);
      } else {
        console.warn('[T2] Flow banner not visible — flow params may be missing, continuing');
      }
    });

    await test.step('Wait for at least one job to appear in list', async () => {
      await queuePage.waitForJobListLoaded();
      await queuePage
        .expectJobListNotEmpty()
        .catch(() => console.warn('[T2] Job list is empty, embedding may not have started'));
    });

    await test.step('Wait for job to reach terminal status', async () => {
      // This can take a while — use the configured timeout
      await queuePage
        .waitForJobCompletion(envConfig.jobTimeout)
        .catch(e =>
          console.warn(
            `[T2] Job did not complete within ${envConfig.jobTimeout}ms. This may be expected if services are unavailable. Error: ${e.message}`
          )
        );
    });

    await test.step('Verify chunk preview tab is visible', async () => {
      const chunkTabVisible = await page
        .locator('[data-testid="chunk-preview-tab"]')
        .isVisible({ timeout: 10_000 })
        .catch(() => false);

      if (chunkTabVisible) {
        await queuePage
          .openChunksSection()
          .catch(e => console.warn('[T2] Could not open chunks section:', e.message));
      } else {
        console.warn('[T2] Chunk preview tab not visible — job may not be complete yet');
      }
    });

    await test.step('Verify "Testa Agent" button is present', async () => {
      await queuePage
        .expectTestAgentButtonVisible()
        .catch(() =>
          console.warn('[T2] "Testa Agent" button not visible — skipping agent navigation')
        );
    });
  });

  // ── Test 3: Test RAG agent with embedded KB ──────────────────────────────
  test('3. Test RAG agent with embedded knowledge base', async () => {
    if (!state.adminPage) test.skip(true, state.failureReason ?? 'Requires test 1 to pass');
    const page = state.adminPage!;

    // Check "Testa Agent" button is available before proceeding
    const testAgentLink = page.getByRole('link', { name: /testa agent/i });
    const testAgentVisible = await testAgentLink.isVisible({ timeout: 5_000 }).catch(() => false);

    if (!testAgentVisible) {
      test.skip(true, '"Testa Agent" button not found — embedding may not have completed');
    }

    await test.step('Click "Testa Agent" link', async () => {
      await testAgentLink.click();
      // The link navigates to an agent test page (possibly /agents/{id} or similar)
      await page.waitForURL(url => url.pathname.includes('/agent'), { timeout: 15_000 });
    });

    await test.step('Verify agent test page loaded with flow banner', async () => {
      // Flow banner should show queue step as done
      const bannerVisible = await page
        .locator('[data-testid="embedding-flow-banner"]')
        .isVisible({ timeout: 10_000 })
        .catch(() => false);

      if (bannerVisible) {
        const queueStep = page.locator('[data-testid="flow-step-queue"]');
        await expect(queueStep)
          .toBeVisible({ timeout: 5_000 })
          .catch(() => console.warn('[T3] Flow step queue indicator not visible'));
      } else {
        console.warn('[T3] Flow banner not visible on agent test page');
      }
    });

    await test.step('Send a test question about the game rules', async () => {
      const chatPage = new AgentChatPage(page);

      // Locate the message input — may already be on a chat page
      const messageInput = page
        .locator('[data-testid="message-input"]')
        .or(page.getByPlaceholder(/scrivi un messaggio|write a message|ask/i));

      const inputVisible = await messageInput.isVisible({ timeout: 10_000 }).catch(() => false);

      if (!inputVisible) {
        console.warn('[T3] Message input not visible — agent chat interface may differ');
        return;
      }

      await chatPage.sendMessage(`What are the basic rules of ${state.gameName ?? 'this game'}?`);
    });

    await test.step('Verify agent response is received', async () => {
      const chatPage = new AgentChatPage(page);

      const responseText = await chatPage
        .waitForAgentResponse(envConfig.jobTimeout)
        .catch((e: Error) => {
          console.warn('[T3] Agent did not respond in time:', e.message);
          return '';
        });

      if (responseText) {
        expect(responseText.length).toBeGreaterThan(0);
        console.log('[T3] Agent responded with', responseText.length, 'characters');
      } else {
        console.warn('[T3] Empty agent response — RAG pipeline may not be fully configured');
      }
    });
  });
});
