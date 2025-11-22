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

import { test as base, expect, Page, Route } from '@playwright/test';
import { loginAsUser } from './fixtures/auth';
import { ChatPage } from './pages/chat/ChatPage';

const test = base.extend<{ chatPage: Page }>({
  chatPage: async ({ page }: { page: Page }, use: (page: Page) => Promise<void>) => {
    // Set up auth mocks first
    await loginAsUser(page, true);

    // Mock chat API endpoints
    await page.route('**/api/v1/chats*', async (route: Route) => {
      const url = route.request().url();
      const method = route.request().method();

      // Export endpoint
      if (url.includes('/export') && method === 'POST') {
        const requestBody = JSON.parse(route.request().postData() || '{}');
        const format = requestBody.format || 'json';
        const chatId = url.match(/chats\/([^/]+)\/export/)?.[1] || 'test-chat-id';

        let content: string;
        let contentType: string;

        if (format === 'json') {
          content = JSON.stringify({
            chatId,
            gameName: 'Chess',
            agentName: 'Q&A Agent',
            exportedAt: new Date().toISOString(),
            messages: [
              {
                id: 'msg-1',
                level: 'user',
                message: 'How do I castle in chess?',
                timestamp: '2025-11-10T10:00:00Z'
              },
              {
                id: 'msg-2',
                level: 'assistant',
                message: 'Castling is a special move involving the king and rook...',
                metadata: {
                  citations: [
                    { title: 'Chess Rulebook', page: 12 },
                    { title: 'Advanced Chess Tactics', page: 5 }
                  ],
                  confidence: 0.92
                },
                timestamp: '2025-11-10T10:00:05Z'
              }
            ],
            messageCount: 2
          }, null, 2);
          contentType = 'application/json';
        } else {
          content = `Chess - Q&A Agent
Exported: ${new Date().toISOString()}

---

[User - 2025-11-10T10:00:00Z]
How do I castle in chess?

[Assistant - 2025-11-10T10:00:05Z]
Castling is a special move involving the king and rook...

Citations:
- Chess Rulebook (Page 12)
- Advanced Chess Tactics (Page 5)

Confidence: 92%
`;
          contentType = 'text/plain';
        }

        const timestamp = new Date().toISOString().split('T')[0];
        const filename = `chat-chess-${timestamp}.${format}`;

        await route.fulfill({
          status: 200,
          contentType,
          headers: {
            'Content-Disposition': `attachment; filename="${filename}"`
          },
          body: content
        });
      }
      // List chats endpoint
      else if (method === 'GET' && !url.includes('/chats/')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify([
            {
              id: 'chat-123',
              gameId: 'chess',
              gameName: 'Chess',
              agentId: 'qa-agent',
              agentName: 'Q&A Agent',
              startedAt: new Date().toISOString(),
              lastMessageAt: new Date().toISOString()
            }
          ])
        });
      }
      // Get specific chat
      else if (method === 'GET' && url.includes('/chats/')) {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'chat-123',
            gameId: 'chess',
            gameName: 'Chess',
            agentId: 'qa-agent',
            agentName: 'Q&A Agent',
            startedAt: new Date().toISOString(),
            lastMessageAt: new Date().toISOString(),
            messages: [
              {
                id: 'msg-1',
                level: 'user',
                message: 'How do I castle in chess?',
                metadataJson: null,
                createdAt: new Date().toISOString()
              },
              {
                id: 'msg-2',
                level: 'assistant',
                message: 'Castling is a special move...',
                metadataJson: JSON.stringify({
                  citations: [{ title: 'Chess Rulebook', page: 12 }]
                }),
                createdAt: new Date().toISOString()
              }
            ]
          })
        });
      }
      // Create chat
      else if (method === 'POST') {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'chat-123',
            gameId: 'chess',
            gameName: 'Chess',
            agentId: 'qa-agent',
            agentName: 'Q&A Agent',
            startedAt: new Date().toISOString(),
            lastMessageAt: new Date().toISOString()
          })
        });
      } else {
        await route.continue();
      }
    });

    // Mock games endpoint
    await page.route('**/api/v1/games', async (route: Route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'chess', name: 'Chess', description: 'Chess game rules' }
        ])
      });
    });

    // Mock agents endpoint
    await page.route('**/api/v1/agents', async (route: Route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'qa-agent', name: 'Q&A Agent', description: 'Answer questions about game rules' }
        ])
      });
    });

    // Mock streaming QA endpoint
    await page.route('**/api/v1/qa/ask*', async (route: Route) => {
      await route.fulfill({
        status: 200,
        contentType: 'text/plain',
        body: 'Castling is a special move in chess involving the king and rook.'
      });
    });

    await use(page);
  }
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

    // Validate structure
    expect(exportData).toHaveProperty('chatId');
    expect(exportData).toHaveProperty('gameName', 'Chess');
    expect(exportData).toHaveProperty('messages');
    expect(exportData.messages).toHaveLength(2);
    expect(exportData.messages[0]).toHaveProperty('level', 'user');
    expect(exportData.messages[1]).toHaveProperty('level', 'assistant');
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

    // Check citations in assistant message
    const assistantMessage = exportData.messages.find((m: any) => m.level === 'assistant');
    expect(assistantMessage).toBeDefined();
    expect(assistantMessage.metadata).toHaveProperty('citations');
    expect(assistantMessage.metadata.citations).toHaveLength(2);
    expect(assistantMessage.metadata.citations[0]).toHaveProperty('title', 'Chess Rulebook');
    expect(assistantMessage.metadata.citations[0]).toHaveProperty('page', 12);
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

    // Validate TXT format
    expect(content).toContain('Chess - Q&A Agent');
    expect(content).toContain('[User');
    expect(content).toContain('[Assistant');
    expect(content).toContain('How do I castle in chess?');
    expect(content).toContain('Citations:');
    expect(content).toContain('Chess Rulebook (Page 12)');
    expect(content).toContain('Confidence: 92%');
  });

  test('should include game name and timestamp in filename', async ({ chatPage: page }) => {
    const chat = new ChatPage(page);
    await chat.goto();
    await page.waitForLoadState('networkidle');

    const downloadPromise = page.waitForEvent('download');
    await chat.exportConversation('json');
    const download = await downloadPromise;

    // Filename should include game and timestamp
    const filename = download.suggestedFilename();
    expect(filename).toMatch(/chat-chess-\d{4}-\d{2}-\d{2}\.json$/);
  });

  test('should handle empty chat export (edge case)', async ({ chatPage: page }) => {
    // Override mock to return empty chat
    await page.route('**/api/v1/chats/**/export', async (route: Route) => {
      const requestBody = JSON.parse(route.request().postData() || '{}');
      const format = requestBody.format || 'json';

      let content: string;
      let contentType: string;

      if (format === 'json') {
        content = JSON.stringify({
          chatId: 'empty-chat',
          gameName: 'Chess',
          agentName: 'Q&A Agent',
          exportedAt: new Date().toISOString(),
          messages: [],
          messageCount: 0
        });
        contentType = 'application/json';
      } else {
        content = 'Chess - Q&A Agent\nExported: ' + new Date().toISOString() + '\n\n---\n\nNo messages in this chat.\n';
        contentType = 'text/plain';
      }

      await route.fulfill({
        status: 200,
        contentType,
        headers: {
          'Content-Disposition': `attachment; filename="chat-chess-empty.${format}"`
        },
        body: content
      });
    });

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

    expect(exportData.messages).toHaveLength(0);
    expect(exportData.messageCount).toBe(0);
  });

  test('should handle long conversation export (>50 messages)', async ({ chatPage: page }) => {
    // Override mock to return large chat
    await page.route('**/api/v1/chats/**/export', async (route: Route) => {
      const requestBody = JSON.parse(route.request().postData() || '{}');
      const format = requestBody.format || 'json';

      const messages = [];
      for (let i = 0; i < 60; i++) {
        messages.push({
          id: `msg-${i}`,
          level: i % 2 === 0 ? 'user' : 'assistant',
          message: `Message ${i}`,
          timestamp: new Date(Date.now() + i * 1000).toISOString()
        });
      }

      let content: string;
      if (format === 'json') {
        content = JSON.stringify({
          chatId: 'long-chat',
          gameName: 'Chess',
          agentName: 'Q&A Agent',
          exportedAt: new Date().toISOString(),
          messages,
          messageCount: messages.length
        });
      } else {
        content = messages.map(m => `[${m.level}] ${m.message}`).join('\n\n');
      }

      await route.fulfill({
        status: 200,
        contentType: format === 'json' ? 'application/json' : 'text/plain',
        headers: {
          'Content-Disposition': `attachment; filename="chat-chess-long.${format}"`
        },
        body: content
      });
    });

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

    expect(exportData.messages).toHaveLength(60);
    expect(exportData.messageCount).toBe(60);
  });

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
    await page.waitForTimeout(500);

    // Second export (should work independently)
    const downloadPromise2 = page.waitForEvent('download');
    await chat.exportConversation('txt');
    const download2 = await downloadPromise2;
    expect(download2.suggestedFilename()).toMatch(/\.txt$/);
  });
});
