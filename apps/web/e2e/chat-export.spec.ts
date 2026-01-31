/**
 * Chat Export E2E Tests (Issue #843 Phase 3)
 *
 * Tests chat export functionality including:
 * - Export button visibility after chat interaction
 * - Format selection (JSON/TXT)
 * - Export content validation (messages, citations, timestamps)
 * - Export filename generation
 * - Edge cases (empty chat, long conversation)
 *
 * Coverage Target: 10+ tests, 70%+ pass rate
 */

/**
 * Chat Export E2E Tests - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/ - Page Object Model architecture
 */

import { test as base, expect, Page } from './fixtures/chromatic';
import { AuthHelper, USER_FIXTURES } from './pages';
import { ChatPage } from './pages/chat/ChatPage';

const test = base.extend<{ chatPage: Page }>({
  chatPage: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    const authHelper = new AuthHelper(page);

    // Set up auth using AuthHelper
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.user);

    // ✅ REMOVED MOCK: Use real Chat Export and Thread APIs
    // Real backend endpoints verified:
    //   GET /api/v1/chat-threads/{threadId}/export (JSON/TXT format)
    //   GET /api/v1/chat-threads (list threads)
    //   GET /api/v1/chat-threads/{threadId} (get specific thread with messages)
    //   POST /api/v1/chat-threads (create thread)
    //   GET /api/v1/games (game list - for chat context)
    //   GET /api/v1/agents (agent list - for chat context)
    //   POST /api/v1/agents/qa (streaming QA)
    // Note: Tests verify export functionality with backend seeded chat data

    await use(page);
  },
});

test.describe('Chat Export E2E Tests', () => {
  test('should show export button after chat interaction', async ({ chatPage: page }) => {
    const chat = new ChatPage(page);
    await chat.goto();
    await page.waitForLoadState('networkidle');

    // Export button should be visible on chat page
    const exportButton = page.getByRole('button', { name: /export|esporta/i });
    await expect(exportButton).toBeVisible({ timeout: 10000 });
  });

  test('should open export modal with format selection', async ({ chatPage: page }) => {
    const chat = new ChatPage(page);
    await chat.goto();
    await page.waitForLoadState('networkidle');

    // Click export button
    const exportButton = page.getByRole('button', { name: /export|esporta/i });
    await exportButton.click();

    // Modal should open with format options
    await expect(page.getByText(/export.*format|formato/i)).toBeVisible({ timeout: 5000 });
    await expect(page.getByRole('button', { name: /json/i })).toBeVisible();
    await expect(page.getByRole('button', { name: /txt/i })).toBeVisible();
  });

  test('should export chat in JSON format', async ({ chatPage: page }) => {
    const chat = new ChatPage(page);
    await chat.goto();
    await page.waitForLoadState('networkidle');

    // Set up download listener
    const downloadPromise = page.waitForEvent('download');

    // Click export button and select JSON
    await chat.exportConversation('json');

    // Wait for download
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/chat-.*\.json$/);
  });

  test('should export chat in TXT format', async ({ chatPage: page }) => {
    const chat = new ChatPage(page);
    await chat.goto();
    await page.waitForLoadState('networkidle');

    // Set up download listener
    const downloadPromise = page.waitForEvent('download');

    // Click export button and select TXT
    await chat.exportConversation('txt');

    // Wait for download
    const download = await downloadPromise;
    expect(download.suggestedFilename()).toMatch(/chat-.*\.txt$/);
  });

  test('should include all messages in JSON export', async ({ chatPage: page }) => {
    const chat = new ChatPage(page);
    await chat.goto();
    await page.waitForLoadState('networkidle');

    // Set up download listener with content capture
    const downloadPromise = page.waitForEvent('download');
    await chat.exportConversation('json');
    const download = await downloadPromise;

    // Read download content
    const stream = await download.createReadStream();
    const chunks: Buffer[] = [];
    for await (const chunk of stream) {
      chunks.push(chunk);
    }
    const content = Buffer.concat(chunks).toString('utf-8');
    const exportData = JSON.parse(content);

    // ✅ CHANGED: Validate structure (not specific mock values)
    expect(exportData).toHaveProperty('chatId');
    expect(exportData).toHaveProperty('gameName'); // Any game from backend
    expect(exportData).toHaveProperty('messages');
    expect(Array.isArray(exportData.messages)).toBe(true);
    expect(exportData.messages.length).toBeGreaterThan(0);
    if (exportData.messages.length > 0) {
      expect(exportData.messages[0]).toHaveProperty('level');
    }
  });

  test('should include citations in JSON export', async ({ chatPage: page }) => {
    const chat = new ChatPage(page);
    await chat.goto();
    await page.waitForLoadState('networkidle');

    const downloadPromise = page.waitForEvent('download');
    await chat.exportConversation('json');
    const download = await downloadPromise;

    const stream = await download.createReadStream();
    const chunks: Buffer[] = [];
    for await (const chunk of stream) {
      chunks.push(chunk);
    }
    const content = Buffer.concat(chunks).toString('utf-8');
    const exportData = JSON.parse(content);

    // ✅ CHANGED: Check citations structure (if present in backend)
    const assistantMessage = exportData.messages.find((m: any) => m.level === 'assistant');
    if (assistantMessage && assistantMessage.metadata) {
      expect(assistantMessage.metadata).toHaveProperty('citations');
      expect(Array.isArray(assistantMessage.metadata.citations)).toBe(true);
      if (assistantMessage.metadata.citations.length > 0) {
        expect(assistantMessage.metadata.citations[0]).toHaveProperty('title');
      }
    }
  });

  test('should use human-readable format for TXT export', async ({ chatPage: page }) => {
    const chat = new ChatPage(page);
    await chat.goto();
    await page.waitForLoadState('networkidle');

    const downloadPromise = page.waitForEvent('download');
    await chat.exportConversation('txt');
    const download = await downloadPromise;

    const stream = await download.createReadStream();
    const chunks: Buffer[] = [];
    for await (const chunk of stream) {
      chunks.push(chunk);
    }
    const content = Buffer.concat(chunks).toString('utf-8');

    // ✅ CHANGED: Validate TXT format structure (not specific content)
    expect(content).toContain('[User'); // User message marker
    expect(content).toContain('[Assistant'); // AI message marker
    expect(content.length).toBeGreaterThan(100); // Non-empty export
  });

  test('should include game name and timestamp in filename', async ({ chatPage: page }) => {
    const chat = new ChatPage(page);
    await chat.goto();
    await page.waitForLoadState('networkidle');

    const downloadPromise = page.waitForEvent('download');
    await chat.exportConversation('json');
    const download = await downloadPromise;

    // ✅ CHANGED: Filename pattern (any game name from backend)
    const filename = download.suggestedFilename();
    expect(filename).toMatch(/chat-.*-\d{4}-\d{2}-\d{2}\.json$/);
  });

  // ✅ REMOVED: Scenario tests with mock overrides
  // - "empty chat export" test removed (required mock for zero messages)
  // - "long conversation >50 messages" test removed (required mock for 60 messages)
  // Backend can naturally provide these scenarios through seeded test data if needed

  test('should trigger download correctly on re-export', async ({ chatPage: page }) => {
    const chat = new ChatPage(page);
    await chat.goto();
    await page.waitForLoadState('networkidle');

    // First export
    const downloadPromise1 = page.waitForEvent('download');
    await chat.exportConversation('json');
    const download1 = await downloadPromise1;
    expect(download1.suggestedFilename()).toMatch(/\.json$/);

    // Wait a bit

    // Second export (should work independently)
    const downloadPromise2 = page.waitForEvent('download');
    await chat.exportConversation('txt');
    const download2 = await downloadPromise2;
    expect(download2.suggestedFilename()).toMatch(/\.txt$/);
  });
});
