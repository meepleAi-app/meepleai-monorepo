import { test, expect, Page } from '@playwright/test';

const apiBase = 'http://localhost:8080';

async function setupAuthRoutes(page: Page) {
  let authenticated = false;
  const userResponse = {
    user: {
      id: 'user-1',
      email: 'user@meepleai.dev',
      displayName: 'Test User',
      role: 'User'
    },
    expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString()
  };

  await page.route(`${apiBase}/api/v1/auth/me`, async (route) => {
    if (authenticated) {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(userResponse)
      });
    } else {
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({ error: 'Unauthorized' })
      });
    }
  });

  await page.route(`${apiBase}/api/v1/auth/login`, async (route) => {
    authenticated = true;
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(userResponse)
    });
  });

  return {
    authenticate() {
      authenticated = true;
    }
  };
}

test.describe('AI-04: Q&A with Snippets and Not Specified Fallback', () => {
  test('E2E: User asks question and receives answer with snippets', async ({ page }) => {
    const auth = await setupAuthRoutes(page);
    auth.authenticate();

    // Mock games API
    await page.route(`${apiBase}/api/v1/games`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'chess-1', name: 'Chess', createdAt: '2025-01-01T00:00:00Z' },
          { id: 'tictactoe-1', name: 'Tic-Tac-Toe', createdAt: '2025-01-01T00:00:00Z' }
        ])
      });
    });

    // Mock agents API for chess
    await page.route(`${apiBase}/api/v1/games/chess-1/agents`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'agent-qa-1', gameId: 'chess-1', name: 'Chess Q&A Agent', kind: 'qa', createdAt: '2025-01-01T00:00:00Z' }
        ])
      });
    });

    // Mock chats API
    await page.route(`${apiBase}/api/v1/chats?gameId=chess-1`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });

    // Mock chat creation
    await page.route(`${apiBase}/api/v1/chats`, async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'chat-123',
            gameId: 'chess-1',
            gameName: 'Chess',
            agentId: 'agent-qa-1',
            agentName: 'Chess Q&A Agent',
            startedAt: new Date().toISOString(),
            lastMessageAt: null
          })
        });
      }
    });

    // Mock Q&A API with snippets (AI-04 happy path)
    await page.route(`${apiBase}/api/v1/agents/qa`, async (route) => {
      const requestBody = route.request().postDataJSON() as { query: string; gameId: string; chatId: string };

      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          answer: 'En passant is a special pawn capture move in chess. It can only occur when a pawn moves two squares forward from its starting position and lands beside an opponent\'s pawn.',
          snippets: [
            {
              text: 'A pawn attacking a square crossed by an opponent\'s pawn which has advanced two squares in one move from its original square may capture this opponent\'s pawn as though the latter had been moved only one square. This capture is only legal on the move following this advance and is called an "en passant" capture.',
              source: 'chess-rules.pdf',
              page: 12,
              line: null
            },
            {
              text: 'The en passant capture must be made immediately after the opponent\'s pawn makes the two-square advance; otherwise, the right to do so is lost.',
              source: 'chess-advanced-tactics.pdf',
              page: 45,
              line: null
            }
          ],
          messageId: 'msg-123',
          tokenUsage: {
            promptTokens: 150,
            completionTokens: 85,
            totalTokens: 235
          }
        })
      });
    });

    // Navigate to chat page
    await page.goto('/chat');

    // Verify page loaded
    await expect(page.getByRole('heading', { name: 'MeepleAI Chat' })).toBeVisible();

    // Select Chess game (should be auto-selected)
    await expect(page.locator('#gameSelect')).toHaveValue('chess-1');

    // Verify agent is auto-selected
    await expect(page.locator('#agentSelect')).toHaveValue('agent-qa-1');

    // Enter question about en passant
    const input = page.getByPlaceholder('Fai una domanda sul gioco...');
    await input.fill('What is en passant in chess?');

    // Send message
    await page.getByRole('button', { name: 'Invia' }).click();

    // Wait for assistant response
    await expect(page.getByText('En passant is a special pawn capture move in chess')).toBeVisible({ timeout: 10000 });

    // Verify snippets section is visible
    await expect(page.getByText('Fonti:')).toBeVisible();

    // Verify first snippet with source and page number
    await expect(page.getByText('chess-rules.pdf (Pagina 12)')).toBeVisible();
    await expect(page.getByText(/A pawn attacking a square crossed by an opponent's pawn/)).toBeVisible();

    // Verify second snippet with source and page number
    await expect(page.getByText('chess-advanced-tactics.pdf (Pagina 45)')).toBeVisible();
    await expect(page.getByText(/The en passant capture must be made immediately/)).toBeVisible();

    // Verify feedback buttons are present
    await expect(page.getByRole('button', { name: '👍 Utile' })).toBeVisible();
    await expect(page.getByRole('button', { name: '👎 Non utile' })).toBeVisible();
  });

  test('E2E: User asks question with no relevant context and receives "Not specified"', async ({ page }) => {
    const auth = await setupAuthRoutes(page);
    auth.authenticate();

    // Mock games API
    await page.route(`${apiBase}/api/v1/games`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'chess-1', name: 'Chess', createdAt: '2025-01-01T00:00:00Z' }
        ])
      });
    });

    // Mock agents API
    await page.route(`${apiBase}/api/v1/games/chess-1/agents`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'agent-qa-1', gameId: 'chess-1', name: 'Chess Q&A Agent', kind: 'qa', createdAt: '2025-01-01T00:00:00Z' }
        ])
      });
    });

    // Mock chats API
    await page.route(`${apiBase}/api/v1/chats?gameId=chess-1`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });

    // Mock chat creation
    await page.route(`${apiBase}/api/v1/chats`, async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'chat-456',
            gameId: 'chess-1',
            gameName: 'Chess',
            agentId: 'agent-qa-1',
            agentName: 'Chess Q&A Agent',
            startedAt: new Date().toISOString(),
            lastMessageAt: null
          })
        });
      }
    });

    // Mock Q&A API returning "Not specified" with empty snippets (AI-04 fallback)
    await page.route(`${apiBase}/api/v1/agents/qa`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          answer: 'Not specified',
          snippets: [],
          messageId: 'msg-456',
          tokenUsage: {
            promptTokens: 120,
            completionTokens: 5,
            totalTokens: 125
          }
        })
      });
    });

    // Navigate to chat page
    await page.goto('/chat');

    // Enter question about something not in the rulebook
    const input = page.getByPlaceholder('Fai una domanda sul gioco...');
    await input.fill('What is the price of this chess set?');

    // Send message
    await page.getByRole('button', { name: 'Invia' }).click();

    // Wait for assistant response showing "Not specified"
    await expect(page.getByText('Not specified')).toBeVisible({ timeout: 10000 });

    // Verify NO snippets section is shown (empty snippets array)
    await expect(page.getByText('Fonti:')).not.toBeVisible();

    // Verify feedback buttons are still present
    await expect(page.getByRole('button', { name: '👍 Utile' })).toBeVisible();
    await expect(page.getByRole('button', { name: '👎 Non utile' })).toBeVisible();
  });

  test('E2E: User receives answer with snippets from multiple PDF sources', async ({ page }) => {
    const auth = await setupAuthRoutes(page);
    auth.authenticate();

    // Mock games API
    await page.route(`${apiBase}/api/v1/games`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'tictactoe-1', name: 'Tic-Tac-Toe', createdAt: '2025-01-01T00:00:00Z' }
        ])
      });
    });

    // Mock agents API
    await page.route(`${apiBase}/api/v1/games/tictactoe-1/agents`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'agent-qa-2', gameId: 'tictactoe-1', name: 'Tic-Tac-Toe Q&A Agent', kind: 'qa', createdAt: '2025-01-01T00:00:00Z' }
        ])
      });
    });

    // Mock chats API
    await page.route(`${apiBase}/api/v1/chats?gameId=tictactoe-1`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });

    // Mock chat creation
    await page.route(`${apiBase}/api/v1/chats`, async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'chat-789',
            gameId: 'tictactoe-1',
            gameName: 'Tic-Tac-Toe',
            agentId: 'agent-qa-2',
            agentName: 'Tic-Tac-Toe Q&A Agent',
            startedAt: new Date().toISOString(),
            lastMessageAt: null
          })
        });
      }
    });

    // Mock Q&A API with snippets from multiple sources
    await page.route(`${apiBase}/api/v1/agents/qa`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          answer: 'To win Tic-Tac-Toe, you need to get three of your marks in a row, either horizontally, vertically, or diagonally.',
          snippets: [
            {
              text: 'The first player to get three of their marks in a row (horizontally, vertically, or diagonally) wins the game.',
              source: 'tictactoe-basic-rules.pdf',
              page: 1,
              line: null
            },
            {
              text: 'Victory conditions: Three X\'s or O\'s in any row, column, or diagonal.',
              source: 'tictactoe-manual.pdf',
              page: 3,
              line: null
            },
            {
              text: 'Strategic tip: The center square provides the most winning opportunities as it participates in four possible winning lines.',
              source: 'tictactoe-strategy-guide.pdf',
              page: 7,
              line: null
            }
          ],
          messageId: 'msg-789'
        })
      });
    });

    // Navigate to chat page
    await page.goto('/chat');

    // Enter question
    const input = page.getByPlaceholder('Fai una domanda sul gioco...');
    await input.fill('How do you win Tic-Tac-Toe?');

    // Send message
    await page.getByRole('button', { name: 'Invia' }).click();

    // Wait for response
    await expect(page.getByText(/To win Tic-Tac-Toe/)).toBeVisible({ timeout: 10000 });

    // Verify all three snippets are displayed with correct sources
    await expect(page.getByText('tictactoe-basic-rules.pdf (Pagina 1)')).toBeVisible();
    await expect(page.getByText('tictactoe-manual.pdf (Pagina 3)')).toBeVisible();
    await expect(page.getByText('tictactoe-strategy-guide.pdf (Pagina 7)')).toBeVisible();

    // Verify snippet content is visible
    await expect(page.getByText(/The first player to get three of their marks/)).toBeVisible();
    await expect(page.getByText(/Victory conditions: Three X's or O's/)).toBeVisible();
    await expect(page.getByText(/Strategic tip: The center square provides/)).toBeVisible();
  });

  test('E2E: Feedback submission works for answers with snippets', async ({ page }) => {
    const auth = await setupAuthRoutes(page);
    auth.authenticate();

    // Mock games and agents
    await page.route(`${apiBase}/api/v1/games`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'chess-1', name: 'Chess', createdAt: '2025-01-01T00:00:00Z' }
        ])
      });
    });

    await page.route(`${apiBase}/api/v1/games/chess-1/agents`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'agent-qa-1', gameId: 'chess-1', name: 'Chess Q&A Agent', kind: 'qa', createdAt: '2025-01-01T00:00:00Z' }
        ])
      });
    });

    await page.route(`${apiBase}/api/v1/chats?gameId=chess-1`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });

    await page.route(`${apiBase}/api/v1/chats`, async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'chat-feedback',
            gameId: 'chess-1',
            gameName: 'Chess',
            agentId: 'agent-qa-1',
            agentName: 'Chess Q&A Agent',
            startedAt: new Date().toISOString(),
            lastMessageAt: null
          })
        });
      }
    });

    await page.route(`${apiBase}/api/v1/agents/qa`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          answer: 'Castling is a special move involving the king and a rook.',
          snippets: [
            {
              text: 'Castling consists of moving the king two squares towards a rook, then placing the rook on the other side of the king, adjacent to it.',
              source: 'chess-rules.pdf',
              page: 8,
              line: null
            }
          ],
          messageId: 'msg-feedback-123'
        })
      });
    });

    // Mock feedback API
    let feedbackReceived: any = null;
    await page.route(`${apiBase}/api/v1/agents/feedback`, async (route) => {
      feedbackReceived = route.request().postDataJSON();
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ success: true })
      });
    });

    await page.goto('/chat');

    // Send question
    const input = page.getByPlaceholder('Fai una domanda sul gioco...');
    await input.fill('What is castling?');
    await page.getByRole('button', { name: 'Invia' }).click();

    // Wait for response
    await expect(page.getByText(/Castling is a special move/)).toBeVisible({ timeout: 10000 });

    // Click helpful button
    const helpfulButton = page.getByRole('button', { name: '👍 Utile' });
    await helpfulButton.click();

    // Wait a moment for API call
    await page.waitForTimeout(500);

    // Verify button shows active state (green background)
    await expect(helpfulButton).toHaveCSS('background-color', 'rgb(52, 168, 83)');

    // Verify feedback was sent to backend
    expect(feedbackReceived).toBeTruthy();
    expect(feedbackReceived.messageId).toBe('msg-feedback-123');
    expect(feedbackReceived.outcome).toBe('helpful');
    expect(feedbackReceived.endpoint).toBe('qa');
    expect(feedbackReceived.gameId).toBe('chess-1');
  });

  test('E2E: Snippet without page number displays correctly', async ({ page }) => {
    const auth = await setupAuthRoutes(page);
    auth.authenticate();

    await page.route(`${apiBase}/api/v1/games`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'game-1', name: 'Test Game', createdAt: '2025-01-01T00:00:00Z' }
        ])
      });
    });

    await page.route(`${apiBase}/api/v1/games/game-1/agents`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          { id: 'agent-1', gameId: 'game-1', name: 'Test Agent', kind: 'qa', createdAt: '2025-01-01T00:00:00Z' }
        ])
      });
    });

    await page.route(`${apiBase}/api/v1/chats?gameId=game-1`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([])
      });
    });

    await page.route(`${apiBase}/api/v1/chats`, async (route) => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            id: 'chat-no-page',
            gameId: 'game-1',
            gameName: 'Test Game',
            agentId: 'agent-1',
            agentName: 'Test Agent',
            startedAt: new Date().toISOString(),
            lastMessageAt: null
          })
        });
      }
    });

    await page.route(`${apiBase}/api/v1/agents/qa`, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          answer: 'Here is the answer.',
          snippets: [
            {
              text: 'Some text without page number.',
              source: 'source-without-page.txt',
              page: null,
              line: null
            }
          ],
          messageId: 'msg-no-page'
        })
      });
    });

    await page.goto('/chat');

    const input = page.getByPlaceholder('Fai una domanda sul gioco...');
    await input.fill('Test question');
    await page.getByRole('button', { name: 'Invia' }).click();

    await expect(page.getByText('Here is the answer.')).toBeVisible({ timeout: 10000 });

    // Verify snippet displays source without page number (no "Pagina X")
    await expect(page.getByText('source-without-page.txt')).toBeVisible();
    await expect(page.getByText(/source-without-page\.txt \(Pagina/)).not.toBeVisible();
  });
});
