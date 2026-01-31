/**
 * E2E Test: Agent RAG Flow - Complete User Journeys (AGT-017)
 *
 * Test scenarios:
 * - E2E-AGT-001: First-time agent launch with configuration
 * - E2E-AGT-002: Returning user (skip config modal)
 * - E2E-AGT-003: Token quota enforcement (auto-fallback)
 * - E2E-AGT-004: Admin approval workflow (new typology)
 *
 * Real APIs Used:
 * - POST /api/v1/game-sessions/{sessionId}/agent/launch
 * - POST /api/v1/game-sessions/{sessionId}/agent/chat (SSE streaming)
 * - PUT /api/v1/game-sessions/{sessionId}/agent/state
 * - DELETE /api/v1/game-sessions/{sessionId}/agent
 *
 * @see Issue #3191 - AGT-017 Agent E2E Test Flows
 */

import { test, expect, Page } from '@playwright/test';
import { WaitHelper } from '../helpers/WaitHelper';
import { AuthHelper, USER_FIXTURES, E2E_CREDENTIALS } from '../pages/helpers/AuthHelper';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ========================================
// Test Helpers
// ========================================

/**
 * Login helper for authenticated agent tests
 */
async function loginAsUser(page: Page, userType: 'user' | 'editor' | 'admin' = 'user') {
  const authHelper = new AuthHelper(page);

  // Use real login for E2E flow validation
  await page.goto('/');

  // Click Get Started / Sign In
  const signInButton = page.getByRole('button', { name: /get started|sign in/i });
  await signInButton.click();

  // Wait for auth modal
  await page.waitForSelector('[data-testid="auth-modal"]', { timeout: 5000 });

  // Fill credentials
  const credentials = E2E_CREDENTIALS[userType];
  await page.fill('[data-testid="email-input"]', credentials.email);
  await page.fill('[data-testid="password-input"]', credentials.password);

  // Submit
  await page.click('[data-testid="login-submit"]');

  // Wait for dashboard/library navigation
  await page.waitForURL(url => url.pathname.includes('/library') || url.pathname.includes('/dashboard'), {
    timeout: 10000
  });
}

/**
 * Launch agent helper - configures and launches agent from game card
 */
async function launchAgent(
  page: Page,
  options: {
    typology?: string;
    model?: string;
    skipConfig?: boolean;
  } = {}
) {
  const { typology = 'Rules Expert', model = 'GPT-4o-mini', skipConfig = false } = options;

  // Click Ask Agent button (first game card)
  await page.locator('[data-testid="ask-agent-button"]').first().click();

  if (!skipConfig) {
    // Wait for config modal
    await page.waitForSelector('[data-testid="agent-config-modal"]', { timeout: 5000 });

    // Configure agent
    await page.locator('[data-testid="typology-select"]').selectOption(typology);
    await page.locator('[data-testid="model-select"]').selectOption(model);

    // Launch
    await page.locator('[data-testid="launch-agent-button"]').click();
  }

  // Wait for chat interface to appear
  await page.waitForSelector('[data-testid="agent-chat-sidebar"]', { timeout: 10000 });
}

/**
 * Send message and wait for agent response
 */
async function sendMessage(page: Page, message: string, waitHelper: WaitHelper) {
  // Fill message
  await page.locator('[data-testid="chat-input"]').fill(message);

  // Set up SSE listener BEFORE clicking send
  const ssePromise = page.waitForResponse(
    response => response.url().includes('/agent/chat') &&
                response.headers()['content-type']?.includes('text/event-stream') === true,
    { timeout: 15000 }
  );

  // Send message
  await page.locator('[data-testid="send-button"]').click();

  // Wait for SSE stream to start
  await ssePromise;

  // Wait for streaming to complete (typing indicator disappears)
  await waitHelper.waitForStreamingComplete({
    endpoint: '/agent/chat',
    timeout: 15000
  });

  // Wait for message to appear in chat
  await expect(page.locator('[data-testid="agent-message"]').last()).toBeVisible({
    timeout: 5000
  });
}

/**
 * Wait for agent response with confidence validation
 */
async function waitForAgentResponse(page: Page, options: {
  minConfidence?: number;
  timeout?: number;
} = {}) {
  const { minConfidence = 0.7, timeout = 15000 } = options;

  // Wait for agent message to appear
  const agentMessage = page.locator('[data-testid="agent-message"]').last();
  await agentMessage.waitFor({ state: 'visible', timeout });

  // Check confidence badge if required
  if (minConfidence > 0) {
    const confidenceBadge = agentMessage.locator('[data-testid="confidence-badge"]');
    const confidenceText = await confidenceBadge.textContent();
    const confidence = parseFloat(confidenceText?.replace(/[^0-9.]/g, '') || '0') / 100;

    expect(confidence).toBeGreaterThanOrEqual(minConfidence);
  }

  return agentMessage;
}

// ========================================
// Test Suite
// ========================================

test.describe('Agent RAG Flow - E2E Tests (AGT-017)', () => {

  test.describe('E2E-AGT-001: First-Time Agent Launch', () => {
    test('User launches agent, configures, asks question, verifies response and citations', async ({ page }) => {
      const waitHelper = new WaitHelper(page);

      // Setup console error listener before test actions
      const consoleErrors: string[] = [];
      page.on('console', msg => {
        if (msg.type() === 'error') {
          consoleErrors.push(msg.text());
        }
      });

      // Step 1: Login as user
      await loginAsUser(page, 'user');

      // Step 2: Navigate to library
      await page.goto('/library');
      await page.waitForLoadState('networkidle');

      // Step 3: Launch agent with configuration
      await launchAgent(page, {
        typology: 'Rules Expert',
        model: 'GPT-4o-mini'
      });

      // Step 4: Send question
      const question = 'How do I setup the game?';
      await sendMessage(page, question, waitHelper);

      // Step 5: Verify response contains setup instructions
      const response = await waitForAgentResponse(page, { minConfidence: 0.7 });
      await expect(response).toContainText(/setup|set up|setting up/i);

      // Step 6: Verify citations are present
      const citationBadge = page.locator('[data-testid="citation-badge"]');
      await expect(citationBadge).toBeVisible({ timeout: 5000 });

      // Step 7: Verify no console errors
      expect(consoleErrors).toHaveLength(0);
    });
  });

  test.describe('E2E-AGT-002: Returning User (Skip Config)', () => {
    test('User with existing config clicks Ask Agent, chat opens directly without modal', async ({ page }) => {
      const waitHelper = new WaitHelper(page);

      // Step 1: Login and launch agent once (creates config)
      await loginAsUser(page, 'user');
      await page.goto('/library');
      await launchAgent(page, {
        typology: 'Rules Expert',
        model: 'GPT-4o-mini'
      });

      // Step 2: Close chat sidebar
      await page.locator('[data-testid="close-chat-button"]').click();
      await expect(page.locator('[data-testid="agent-chat-sidebar"]')).not.toBeVisible();

      // Step 3: Click Ask Agent again (should skip config modal)
      await launchAgent(page, { skipConfig: true });

      // Step 4: Verify chat opens directly without modal
      await expect(page.locator('[data-testid="agent-config-modal"]')).not.toBeVisible();
      await expect(page.locator('[data-testid="agent-chat-sidebar"]')).toBeVisible();

      // Step 5: Send message to verify session works
      await sendMessage(page, 'What is the objective?', waitHelper);
      const response = await waitForAgentResponse(page);
      await expect(response).toBeVisible();
    });
  });

  test.describe('E2E-AGT-003: Token Quota Enforcement', () => {
    test('User at 95% quota sees agent fallback to free model with warning', async ({ page }) => {
      const waitHelper = new WaitHelper(page);

      // Step 1: Login as user
      await loginAsUser(page, 'user');

      // Step 2: Mock quota API to return 95% usage
      await page.route('**/api/v1/users/me/quota', route => {
        route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            tokenUsage: 95000,
            tokenLimit: 100000,
            percentageUsed: 95
          })
        });
      });

      // Step 3: Navigate to library and launch agent
      await page.goto('/library');
      await launchAgent(page, {
        typology: 'Rules Expert',
        model: 'GPT-4o-mini' // Attempt premium model
      });

      // Step 4: Verify warning message appears
      const warningBanner = page.locator('[data-testid="quota-warning"]');
      await expect(warningBanner).toBeVisible({ timeout: 5000 });
      await expect(warningBanner).toContainText(/quota|fallback|free model/i);

      // Step 5: Verify model auto-switched to free tier
      const modelIndicator = page.locator('[data-testid="active-model-badge"]');
      await expect(modelIndicator).toContainText(/gpt-3.5-turbo|free/i);

      // Step 6: Send message to verify fallback model works
      await sendMessage(page, 'Test message', waitHelper);
      const response = await waitForAgentResponse(page);
      await expect(response).toBeVisible();
    });
  });

  test.describe('E2E-AGT-004: Admin Approval Workflow', () => {
    test('Editor proposes typology, admin approves, user sees new typology in dropdown', async ({ page, context }) => {
      // Step 1: Login as Editor
      await loginAsUser(page, 'editor');

      // Step 2: Navigate to admin panel
      await page.goto('/admin/agent-typologies');
      await page.waitForLoadState('networkidle');

      // Step 3: Propose new typology
      await page.locator('[data-testid="propose-typology-button"]').click();
      await page.fill('[data-testid="typology-name-input"]', 'Strategy Expert');
      await page.fill('[data-testid="typology-description-input"]', 'Expert in game strategy and tactics');
      await page.locator('[data-testid="submit-proposal-button"]').click();

      // Step 4: Verify proposal submitted
      await expect(page.locator('[data-testid="proposal-success"]')).toBeVisible({ timeout: 5000 });

      // Step 5: Switch to Admin user (new page/context)
      const adminPage = await context.newPage();
      await loginAsUser(adminPage, 'admin');

      // Step 6: Navigate to pending approvals
      await adminPage.goto('/admin/agent-typologies/pending');
      await adminPage.waitForLoadState('networkidle');

      // Step 7: Approve the proposal
      const proposalRow = adminPage.locator('[data-typology="Strategy Expert"]');
      await proposalRow.locator('[data-testid="approve-button"]').click();

      // Step 8: Verify approval success
      await expect(adminPage.locator('[data-testid="approval-success"]')).toBeVisible({ timeout: 5000 });

      // Step 9: Switch back to regular user
      const userPage = await context.newPage();
      await loginAsUser(userPage, 'user');

      // Step 10: Navigate to library and open agent config
      await userPage.goto('/library');
      await userPage.locator('[data-testid="ask-agent-button"]').first().click();

      // Step 11: Verify new typology appears in dropdown
      const typologySelect = userPage.locator('[data-testid="typology-select"]');
      await expect(typologySelect.locator('option:has-text("Strategy Expert")')).toBeVisible({
        timeout: 5000
      });

      // Cleanup
      await adminPage.close();
      await userPage.close();
    });
  });

  test.describe('SSE Streaming Validation', () => {
    test('SSE streaming remains stable for 5-minute session', async ({ page }) => {
      const waitHelper = new WaitHelper(page);

      // Step 1: Login and launch agent
      await loginAsUser(page, 'user');
      await page.goto('/library');
      await launchAgent(page);

      // Step 2: Send messages over 5 minutes (6 messages, 1 per minute)
      const startTime = Date.now();
      const messages = [
        'How do I setup the game?',
        'What are the victory conditions?',
        'How many players can play?',
        'What is the recommended age?',
        'How long does a typical game last?',
        'Are there any expansions?'
      ];

      for (let i = 0; i < messages.length; i++) {
        await sendMessage(page, messages[i], waitHelper);

        // Verify response
        const response = await waitForAgentResponse(page, { minConfidence: 0.7 });
        await expect(response).toBeVisible();

        // Wait ~1 minute before next message (except last)
        if (i < messages.length - 1) {
          await page.waitForTimeout(60000);
        }
      }

      const endTime = Date.now();
      const duration = (endTime - startTime) / 1000 / 60; // minutes

      // Step 3: Verify session lasted ~5 minutes
      expect(duration).toBeGreaterThanOrEqual(4.5);
      expect(duration).toBeLessThanOrEqual(6);

      // Step 4: Verify all messages are present
      const messageCount = await page.locator('[data-testid="agent-message"]').count();
      expect(messageCount).toBe(messages.length);
    });

    test('SSE reconnection works after temporary disconnect', async ({ page }) => {
      const waitHelper = new WaitHelper(page);

      // Step 1: Login and launch agent
      await loginAsUser(page, 'user');
      await page.goto('/library');
      await launchAgent(page);

      // Step 2: Send first message
      await sendMessage(page, 'First message', waitHelper);
      await waitForAgentResponse(page);

      // Step 3: Simulate network interruption (block SSE endpoint)
      await page.route('**/agent/chat*', route => route.abort());

      // Step 4: Try to send message (should show error/reconnecting)
      await page.locator('[data-testid="chat-input"]').fill('Second message');
      await page.locator('[data-testid="send-button"]').click();

      const errorIndicator = page.locator('[data-testid="connection-error"]');
      await expect(errorIndicator).toBeVisible({ timeout: 5000 });

      // Step 5: Restore network
      await page.unroute('**/agent/chat*');

      // Step 6: Verify reconnection indicator
      const reconnectingIndicator = page.locator('[data-testid="reconnecting"]');
      await expect(reconnectingIndicator).toBeVisible({ timeout: 3000 });

      // Step 7: Wait for reconnection success
      await expect(reconnectingIndicator).not.toBeVisible({ timeout: 10000 });

      // Step 8: Send message again (should work)
      await sendMessage(page, 'Third message after reconnect', waitHelper);
      const response = await waitForAgentResponse(page);
      await expect(response).toBeVisible();
    });
  });

  test.describe('Mobile Viewport Tests', () => {
    test.use({
      viewport: { width: 375, height: 667 } // iPhone SE
    });

    test('Agent chat opens as bottom sheet on mobile', async ({ page }) => {
      const waitHelper = new WaitHelper(page);

      // Step 1: Login as user
      await loginAsUser(page, 'user');

      // Step 2: Navigate to library
      await page.goto('/library');
      await page.waitForLoadState('networkidle');

      // Step 3: Launch agent
      await launchAgent(page);

      // Step 4: Verify bottom sheet UI (not sidebar)
      const chatContainer = page.locator('[data-testid="agent-chat-sidebar"]');
      await expect(chatContainer).toHaveClass(/bottom-sheet|mobile-chat/);

      // Step 5: Verify bottom sheet takes full width
      const boundingBox = await chatContainer.boundingBox();
      expect(boundingBox?.width).toBeGreaterThanOrEqual(370); // Near full viewport width

      // Step 6: Send message
      await sendMessage(page, 'Mobile test message', waitHelper);
      const response = await waitForAgentResponse(page);
      await expect(response).toBeVisible();

      // Step 7: Verify bottom sheet can be minimized
      const minimizeButton = page.locator('[data-testid="minimize-chat-button"]');
      await minimizeButton.click();

      // Chat should collapse to a small indicator
      const minimizedIndicator = page.locator('[data-testid="chat-minimized"]');
      await expect(minimizedIndicator).toBeVisible({ timeout: 3000 });
    });

    test('Mobile bottom sheet swipe-to-dismiss gesture', async ({ page }) => {
      const waitHelper = new WaitHelper(page);

      // Step 1: Login and launch agent
      await loginAsUser(page, 'user');
      await page.goto('/library');
      await launchAgent(page);

      // Step 2: Simulate swipe-down gesture
      const chatContainer = page.locator('[data-testid="agent-chat-sidebar"]');
      const box = await chatContainer.boundingBox();
      if (!box) throw new Error('Chat container not found');

      // Swipe from top to bottom
      await page.mouse.move(box.x + box.width / 2, box.y + 20);
      await page.mouse.down();
      await page.mouse.move(box.x + box.width / 2, box.y + box.height - 20, { steps: 10 });
      await page.mouse.up();

      // Step 3: Verify bottom sheet dismissed
      await expect(chatContainer).not.toBeVisible({ timeout: 3000 });
    });
  });
});
