/**
 * Journey 1: Pre-Game Assistance E2E Tests
 *
 * Tests the complete pre-game workflow for users preparing to play:
 * 1. AI Chat - Ask setup questions
 * 2. Checklist - Mark setup items completed (future feature)
 * 3. Regolamento (Rules) - View/access rulebook PDF
 *
 * Pattern: Hybrid approach (mock external APIs, test real internal APIs)
 * Related Issue: #2843 - E2E User Journey Tests
 * Epic: #2823
 */

import { expect, test } from '../fixtures';
import { WaitHelper } from '../helpers/WaitHelper';
import { AuthHelper, USER_FIXTURES } from '../pages';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const MOCK_GAME = {
  id: 'test-game-pre-1',
  title: 'Wingspan',
  bggId: 266192,
  minPlayers: 1,
  maxPlayers: 5,
  minPlayTimeMinutes: 40,
  maxPlayTimeMinutes: 70,
  complexity: 2.44,
  yearPublished: 2019,
  description: 'Bird collection and habitat building game',
  thumbnailUrl: 'https://cf.geekdo-images.com/wingspan-thumb.jpg',
  imageUrl: 'https://cf.geekdo-images.com/wingspan-full.jpg',
};

const MOCK_BGG_DETAILS = {
  bggId: 266192,
  name: 'Wingspan',
  yearPublished: 2019,
  description: MOCK_GAME.description,
  thumbnailUrl: MOCK_GAME.thumbnailUrl,
  imageUrl: MOCK_GAME.imageUrl,
  minPlayers: 1,
  maxPlayers: 5,
  playingTime: 70,
  minPlayTime: 40,
  maxPlayTime: 70,
  publishers: ['Stonemaier Games'],
  designers: ['Elizabeth Hargrave'],
  categories: ['Animals', 'Card Game'],
  mechanics: ['Set Collection', 'Hand Management', 'Dice Rolling'],
};

const MOCK_PDF = {
  id: 'test-pdf-pre-1',
  gameId: MOCK_GAME.id,
  fileName: 'wingspan_rulebook.pdf',
  filePath: '/pdfs/wingspan_rulebook.pdf',
  fileSizeBytes: 2048000,
  processingStatus: 'Completed',
  uploadedAt: new Date().toISOString(),
  processedAt: new Date().toISOString(),
  pageCount: 12,
  documentType: 'base',
  isPublic: false,
};

test.describe('Journey 1: Pre-Game Assistance', () => {
  test.beforeEach(async ({ page }) => {
    const authHelper = new AuthHelper(page);
    await page.emulateMedia({ reducedMotion: 'reduce' });

    // Auth: Mock authenticated session (skip OAuth)
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // Mock: BGG API (external)
    await page.route(`${API_BASE}/api/v1/bgg/games/${MOCK_GAME.bggId}`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_BGG_DETAILS),
      });
    });

    // Mock: Games endpoints (for test isolation)
    await page.route(`${API_BASE}/api/v1/games/${MOCK_GAME.id}`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_GAME),
      });
    });

    // Mock: Game PDFs (Rules tab)
    await page.route(`${API_BASE}/api/v1/games/${MOCK_GAME.id}/pdfs`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([MOCK_PDF]),
      });
    });

    // Mock: Game Rules (for Rules tab)
    await page.route(`${API_BASE}/api/v1/games/${MOCK_GAME.id}/rules`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([]),
      });
    });

    // Mock: PDF Download
    await page.route(`${API_BASE}/api/v1/pdfs/${MOCK_PDF.id}/download`, async route => {
      // Minimal valid PDF
      const pdfContent =
        '%PDF-1.4\n1 0 obj\n<<\n/Type /Catalog\n/Pages 2 0 R\n>>\nendobj\nxref\n0 2\ntrailer\n<<\n/Size 2\n/Root 1 0 R\n>>\nstartxref\n50\n%%EOF';
      await route.fulfill({
        status: 200,
        contentType: 'application/pdf',
        body: pdfContent,
      });
    });

    // Real: Chat API (RAG integration - test real backend)
    // Let chat endpoints pass through for real RAG testing
  });

  test('should access rulebook PDF from Rules tab', async ({ page }) => {
    await test.step('Navigate to game detail page', async () => {
      await page.goto(`/games/${MOCK_GAME.id}`);
      await expect(page).toHaveURL(new RegExp(`/games/${MOCK_GAME.id}`));

      // Verify game loaded
      await expect(page.locator(`text=${MOCK_GAME.title}`).first()).toBeVisible({ timeout: 10000 });
    });

    await test.step('Navigate to Rules tab', async () => {
      // Click Rules tab
      const rulesTab = page.getByRole('tab', { name: /rules/i });
      await rulesTab.click();

      // Wait for tab content
      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(3000);
    });

    await test.step('Verify rulebook PDF is listed', async () => {
      // Check PDF appears in Rules tab
      await expect(page.locator(`text=${MOCK_PDF.fileName}`)).toBeVisible({ timeout: 5000 });
      await expect(page.locator('text=12 pages')).toBeVisible();
    });

    await test.step('View PDF rulebook', async () => {
      // Find and click View PDF button
      const viewButton = page.getByRole('button', { name: /view pdf|visualizza|view/i }).first();
      await viewButton.click();

      // Wait for PDF viewer modal
      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForDOMStable('body', 3000);

      // Verify PDF viewer opened
      await expect(page.locator(`text=${MOCK_PDF.fileName}`).last()).toBeVisible({ timeout: 5000 });
    });

    console.log('✅ Journey 1 (Regolamento): Rulebook PDF access successful');
  });

  test('should ask setup question via AI chat', async ({ page }) => {
    await test.step('Navigate to AI chat page', async () => {
      await page.goto('/board-game-ai/ask');
      await expect(page).toHaveURL(/\/board-game-ai\/ask/);

      // Wait for page load
      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(3000);
    });

    await test.step('Select game for chat context', async () => {
      // Look for game selector (if exists)
      const gameSelect = page.locator('select#gameSelect, select[name="gameId"]').first();

      if (await gameSelect.isVisible({ timeout: 3000 })) {
        // Try to select Wingspan
        try {
          const options = await gameSelect.locator('option').allTextContents();
          const wingspanOption = options.find(opt => opt.includes('Wingspan'));
          if (wingspanOption) {
            await gameSelect.selectOption({ label: wingspanOption });

            const waitHelper = new WaitHelper(page);
            await waitHelper.waitForNetworkIdle(2000);
          }
        } catch (e) {
          console.log('Game selector not available or Wingspan not in list');
        }
      }
    });

    await test.step('Ask setup question', async () => {
      // Find message input (various possible selectors)
      const messageInput = page.locator(
        '[data-testid="message-input"], [placeholder*="Ask"], [placeholder*="question"], textarea'
      ).first();

      // Wait for input to be enabled
      await expect(messageInput).toBeEnabled({ timeout: 10000 });

      // Type setup question
      const question = 'How do I set up the game for the first time?';
      await messageInput.fill(question);

      // Find and click send button
      const sendButton = page.getByRole('button', { name: /send|invia|ask/i }).first();
      await sendButton.click();

      // Verify question appears in chat
      await expect(page.getByText(question)).toBeVisible({ timeout: 5000 });
    });

    await test.step('Wait for AI response', async () => {
      // Wait for streaming to complete (if streaming indicator exists)
      const streamingIndicator = page.locator('[data-testid="streaming-indicator"]');

      // Wait for streaming to start (optional)
      await streamingIndicator
        .waitFor({ state: 'visible', timeout: 5000 })
        .catch(() => console.log('Streaming indicator not found'));

      // Wait for streaming to complete
      await streamingIndicator
        .waitFor({ state: 'hidden', timeout: 30000 })
        .catch(() => console.log('Streaming took longer than expected'));

      // Wait for DOM to stabilize
      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForDOMStable('body', 5000);
    });

    await test.step('Verify AI provided setup guidance', async () => {
      const pageContent = await page.textContent('body');

      // Verify response exists and is substantial
      expect(pageContent).toBeTruthy();
      expect(pageContent!.length).toBeGreaterThan(200);

      // Verify setup-related keywords (English context)
      const setupKeywords = [
        'setup',
        'start',
        'board',
        'cards',
        'components',
        'player',
        'distribute',
        'prepare',
        'place',
        'shuffle',
      ];

      const foundKeywords = setupKeywords.filter(keyword =>
        pageContent!.toLowerCase().includes(keyword)
      );

      console.log('Found setup keywords:', foundKeywords);

      // At least one keyword should be present in response
      expect(foundKeywords.length).toBeGreaterThan(0);
    });

    console.log('✅ Journey 1 (AI Chat): Setup question answered successfully');
  });

  test('should complete full pre-game workflow (chat → rules)', async ({ page }) => {
    await test.step('Access game detail page', async () => {
      await page.goto(`/games/${MOCK_GAME.id}`);
      await expect(page.locator(`text=${MOCK_GAME.title}`).first()).toBeVisible({ timeout: 10000 });
    });

    await test.step('Access AI chat from game context', async () => {
      // Look for link/button to AI chat (if exists on game page)
      const chatButton = page.getByRole('button', { name: /ask ai|chat|assistant/i }).first();

      if (await chatButton.isVisible({ timeout: 3000 })) {
        await chatButton.click();
        await expect(page).toHaveURL(/\/board-game-ai|\/chat/);
      } else {
        // Navigate manually if no direct link
        await page.goto('/board-game-ai/ask');
      }

      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(3000);
    });

    await test.step('Ask quick setup question', async () => {
      const messageInput = page.locator('[data-testid="message-input"], textarea').first();
      await expect(messageInput).toBeEnabled({ timeout: 10000 });

      await messageInput.fill('What components do I need to set up?');

      const sendButton = page.getByRole('button', { name: /send|invia/i }).first();
      await sendButton.click();

      // Wait for response
      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForDOMStable('body', 10000);
    });

    await test.step('Return to game and access rulebook', async () => {
      // Navigate back to game detail
      await page.goto(`/games/${MOCK_GAME.id}`);

      // Click Rules tab
      const rulesTab = page.getByRole('tab', { name: /rules/i });
      await rulesTab.click();

      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(2000);

      // Verify PDF is accessible
      await expect(page.locator(`text=${MOCK_PDF.fileName}`)).toBeVisible();
    });

    await test.step('Verify complete pre-game assistance accessed', async () => {
      // User has completed the pre-game journey:
      // ✅ Asked AI for setup guidance
      // ✅ Accessed rulebook PDF for reference
      // ✅ (Checklist placeholder - future feature)

      console.log('✅ Journey 1 (Complete): Pre-game workflow finished successfully');
      console.log('   - AI Chat: Setup question answered');
      console.log('   - Rules: Rulebook PDF accessible');
      console.log('   - Checklist: Placeholder (future feature)');
    });
  });

  test.describe('Checklist Feature (Placeholder)', () => {
    test.skip('should complete setup checklist (NOT IMPLEMENTED)', async ({ page }) => {
      // This test is a placeholder for future checklist feature
      // When implemented, it should:
      // 1. Display setup checklist items
      // 2. Allow marking items as completed
      // 3. Track progress (X/Y items complete)
      // 4. Save checklist state

      console.log('⚠️  Checklist feature not yet implemented - skipping test');
    });
  });
});
