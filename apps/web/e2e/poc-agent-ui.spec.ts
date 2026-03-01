import { test, expect } from '@playwright/test';

/**
 * E2E Test: POC Agent UI Integration
 * Verifies MeepleAssistant POC agent is visible and functional in the UI
 */

const BASE_URL = 'http://localhost:3000';
const API_URL = 'http://localhost:8080';

test.describe('POC Agent UI Integration', () => {
  test.beforeEach(async ({ page }) => {
    // Login
    await page.goto(`${BASE_URL}/login`);
    await page.fill('input[type="email"]', 'admin@meepleai.dev');
    await page.fill('input[type="password"]', 'pVKOMQNK0tFNgGlX');
    await page.click('button[type="submit"]');
    await page.waitForURL(`${BASE_URL}/dashboard`);
  });

  test('should display POC agent in agents list', async ({ page }) => {
    // Navigate to agents page
    await page.goto(`${BASE_URL}/agents`);

    // Wait for agents to load
    await page.waitForSelector('text=MeepleAssistant POC', { timeout: 10000 });

    // Verify agent card is visible
    const agentCard = page.locator('text=MeepleAssistant POC').first();
    await expect(agentCard).toBeVisible();

    // Verify agent details
    await expect(page.locator('text=RAG')).toBeVisible(); // Agent type
    await expect(page.locator('text=Active').or(page.locator('[data-status="active"]'))).toBeVisible();
  });

  test('should chat with POC agent and receive RAG response', async ({ page }) => {
    // Navigate to agents
    await page.goto(`${BASE_URL}/agents`);

    // Find and click POC agent
    await page.waitForSelector('text=MeepleAssistant POC');
    await page.click('text=MeepleAssistant POC');

    // Wait for chat interface
    await page.waitForSelector('textarea, input[placeholder*="message" i], input[placeholder*="question" i]', { timeout: 5000 });

    // Send test query
    const messageInput = page.locator('textarea, input[placeholder*="message" i], input[placeholder*="question" i]').first();
    await messageInput.fill('How do you score points in Azul?');

    // Submit (look for send button)
    const sendButton = page.locator('button:has-text("Send"), button[aria-label*="send" i], button[type="submit"]').first();
    await sendButton.click();

    // Wait for response (SSE streaming)
    await page.waitForSelector('text=/score|points|tile|wall/i', { timeout: 30000 });

    // Verify response contains Azul-specific content
    const responseText = await page.textContent('body');

    // Check for RAG indicators
    expect(responseText).toMatch(/page \d|based on.*context|vertical line|horizontal|tiles?|wall/i);

    // Verify professional tone (not "I don't know" or generic response)
    expect(responseText).not.toMatch(/unfortunately.*do not have/i);
  });

  test('should show agent metadata correctly', async ({ page }) => {
    await page.goto(`${BASE_URL}/agents`);
    await page.waitForSelector('text=MeepleAssistant POC');

    // Verify metadata displays
    const pocSection = page.locator('text=MeepleAssistant POC').locator('..');

    // Check for strategy, model, or status indicators
    const hasMetadata = await pocSection.locator('text=/SingleModel|Haiku|Chat|Active/i').count();
    expect(hasMetadata).toBeGreaterThan(0);
  });

  test('should handle no-context queries gracefully', async ({ page }) => {
    await page.goto(`${BASE_URL}/agents`);
    await page.click('text=MeepleAssistant POC');

    // Wait for chat
    const messageInput = page.locator('textarea, input[placeholder*="message" i]').first();
    await messageInput.waitFor();

    // Ask about game not in KB
    await messageInput.fill('What are the rules of Twilight Imperium?');
    await page.click('button:has-text("Send"), button[type="submit"]').first();

    // Wait for response
    await page.waitForSelector('text=/context|information|documentation/i', { timeout: 30000 });

    // Should acknowledge limitation professionally
    const responseText = await page.textContent('body');
    expect(responseText).toMatch(/do not have.*context|no.*information|cannot provide/i);
  });
});
