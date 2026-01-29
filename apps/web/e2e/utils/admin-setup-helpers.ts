/**
 * E2E Test Helpers - Admin First-Time Setup
 *
 * Reusable utilities for admin setup test scenarios
 */

import type { Page, APIRequestContext } from '@playwright/test';

/**
 * Logs in as admin user using credentials from environment
 */
export async function loginAsAdmin(page: Page): Promise<void> {
  await page.goto('/login');

  // Fill login form
  await page.fill('input[name="email"]', process.env.ADMIN_EMAIL || 'admin@meepleai.dev');
  await page.fill('input[name="password"]', process.env.ADMIN_PASSWORD || 'pVKOMQNK0tFNgGlX');

  // Submit and wait for navigation (admin redirects to /admin, not /dashboard)
  await page.click('button[type="submit"]');
  await page.waitForURL(/\/(dashboard|admin)/, { timeout: 10000 });
}

/**
 * Navigates to admin wizard and waits for page load
 */
export async function navigateToWizard(page: Page): Promise<void> {
  await page.goto('/admin/wizard');
  await page.waitForLoadState('networkidle');
}

/**
 * Verifies service health via comprehensive health endpoint
 * Uses /api/v1/health (public) instead of individual endpoints (require auth)
 */
export async function verifyServiceHealth(
  request: APIRequestContext,
  service: 'db' | 'redis' | 'qdrant'
): Promise<boolean> {
  try {
    const response = await request.get('/api/v1/health');

    if (!response.ok()) {
      return false;
    }

    const health = await response.json();

    // Map service names to backend health check names
    const serviceMap: Record<string, string> = {
      db: 'postgres',
      redis: 'redis',
      qdrant: 'qdrant',
    };

    const serviceName = serviceMap[service];
    const serviceCheck = health.checks?.find(
      (c: { serviceName: string; status: string }) => c.serviceName === serviceName
    );

    return serviceCheck?.status === 'Healthy';
  } catch {
    return false;
  }
}

/**
 * Polls PDF processing status until completed or timeout
 */
export async function waitForPdfProcessing(
  page: Page,
  pdfId: string,
  maxWaitMs: number = 60000
): Promise<void> {
  const startTime = Date.now();
  const pollInterval = 2000; // 2 seconds

  while (Date.now() - startTime < maxWaitMs) {
    try {
      const response = await page.request.get(`/api/v1/documents/${pdfId}/status`);

      if (response.ok()) {
        const data = await response.json();

        if (data.status === 'Completed') {
          return;
        }

        if (data.status === 'Failed') {
          throw new Error(`PDF processing failed: ${data.error || 'Unknown error'}`);
        }
      }
    } catch (error) {
      // Continue polling on errors (service might be starting up)
      console.warn(`Polling error: ${error}`);
    }

    await page.waitForTimeout(pollInterval);
  }

  throw new Error(`PDF processing timeout after ${maxWaitMs}ms`);
}

/**
 * Waits for chat thread creation after PDF processing
 */
export async function waitForChatThreadCreation(
  page: Page,
  gameTitle: string,
  maxWaitMs: number = 30000
): Promise<string> {
  const startTime = Date.now();
  const pollInterval = 2000;

  while (Date.now() - startTime < maxWaitMs) {
    try {
      const response = await page.request.get('/api/v1/chat-threads');

      if (response.ok()) {
        const threads = await response.json();
        const matchingThread = threads.find((t: { title: string }) =>
          t.title.toLowerCase().includes(gameTitle.toLowerCase())
        );

        if (matchingThread) {
          return matchingThread.id || matchingThread.threadId;
        }
      }
    } catch (error) {
      console.warn(`Chat thread polling error: ${error}`);
    }

    await page.waitForTimeout(pollInterval);
  }

  throw new Error(`Chat thread creation timeout after ${maxWaitMs}ms`);
}

/**
 * Verifies that shared games were seeded correctly
 */
export async function verifySharedGamesSeeded(
  request: APIRequestContext,
  expectedGames: string[]
): Promise<boolean> {
  try {
    const response = await request.get('/api/v1/shared-games');

    if (!response.ok()) {
      return false;
    }

    const data = await response.json();
    // API returns paginated response: { items: [...], total, page, pageSize }
    const games = data.items || data;
    const gameTitles = games.map((g: { title: string }) => g.title.toLowerCase());

    return expectedGames.every((expectedGame) =>
      gameTitles.some((title) => title.includes(expectedGame.toLowerCase()))
    );
  } catch {
    return false;
  }
}

/**
 * Uploads a PDF file via wizard interface
 */
export async function uploadPdfInWizard(
  page: Page,
  gameName: string,
  pdfPath: string
): Promise<string> {
  // Select game from dropdown
  await page.selectOption('select[name="gameSelect"]', { label: gameName });

  // Upload PDF file
  const fileInput = page.locator('input[type="file"]');
  await fileInput.setInputFiles(pdfPath);

  // Wait for upload response
  const uploadPromise = page.waitForResponse(
    (response) => response.url().includes('/api/v1/documents') && response.status() === 201
  );

  await page.click('button:has-text("Upload")');

  const uploadResponse = await uploadPromise;
  const data = await uploadResponse.json();

  return data.documentId || data.id;
}

/**
 * Completes the wizard game selection step
 */
export async function selectGameInWizard(page: Page, gameName: string): Promise<void> {
  await page.selectOption('select[name="gameId"]', { label: gameName });
  await page.click('button:has-text("Next")');
}

/**
 * Sends a test question in the Q&A step and waits for response
 */
export async function sendTestQuestion(
  page: Page,
  question: string,
  expectedPattern?: RegExp
): Promise<string> {
  await page.fill('textarea[placeholder*="question"], input[placeholder*="question"]', question);

  // Wait for message response
  const messagePromise = page.waitForResponse(
    (response) => response.url().includes('/api/v1/chat/messages') && response.status() === 200,
    { timeout: 30000 }
  );

  await page.click('button:has-text("Send"), button[type="submit"]');
  await messagePromise;

  // Wait for agent response in UI
  await page.waitForSelector('.message.agent, [data-role="agent"]', { timeout: 30000 });

  const agentMessage = await page
    .locator('.message.agent, [data-role="agent"]')
    .last()
    .textContent();

  if (expectedPattern && agentMessage && !expectedPattern.test(agentMessage)) {
    throw new Error(
      `Agent response doesn't match expected pattern. Got: ${agentMessage.substring(0, 100)}...`
    );
  }

  return agentMessage || '';
}

/**
 * Cleans up test data after test completion
 */
export async function cleanupTestData(
  request: APIRequestContext,
  resourceType: 'chat-threads' | 'documents' | 'games',
  resourceId: string
): Promise<void> {
  try {
    await request.delete(`/api/v1/${resourceType}/${resourceId}`);
  } catch (error) {
    console.warn(`Cleanup failed for ${resourceType}/${resourceId}:`, error);
  }
}
