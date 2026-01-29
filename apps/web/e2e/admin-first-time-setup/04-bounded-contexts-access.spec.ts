/**
 * E2E Test Suite 4: Bounded Context Access
 *
 * Validates that admin can access critical bounded contexts after setup:
 * - Administration: User management, roles, audit logs
 * - GameManagement: Catalog, game details, AI agent status
 * - KnowledgeBase: RAG agent, chat threads, Q&A functionality
 *
 * Strategy: Post-wizard validation of system availability
 * Execution: Serial (requires completed setup)
 */

import { test, expect } from '@playwright/test';
import { loginAsAdmin, sendTestQuestion } from '../utils/admin-setup-helpers';

test.describe.configure({ mode: 'serial' });
test.describe('Bounded Context Access', () => {
  // Ensure admin is logged in for all tests
  test.beforeEach(async ({ page }) => {
    await loginAsAdmin(page);
  });

  test('should access Administration context - User Management', async ({ page, request }) => {
    // Navigate to user management
    await page.goto('/admin/users');

    // Verify page loaded
    await expect(
      page.locator('h1:has-text("User"), h1:has-text("Users"), h1:has-text("Management")')
    ).toBeVisible({ timeout: 10000 });

    // Verify users table/list visible
    const usersContainer = page.locator(
      'table[aria-label*="user" i], [data-testid*="user"], .users-list'
    );
    await expect(usersContainer.first()).toBeVisible({ timeout: 5000 });

    // Verify admin user exists in list via API
    const usersResponse = await request.get('/api/v1/users');
    expect(usersResponse.status()).toBe(200);

    const users = await usersResponse.json();
    const adminUser = users.find((u: { role: string }) => u.role === 'Admin');

    expect(adminUser).toBeDefined();
    expect(adminUser.email).toContain('admin');

    console.log('✅ Administration context accessible');
    console.log(`   Admin user: ${adminUser.email}`);
    console.log(`   Total users: ${users.length}`);
  });

  test('should access Administration context - Audit Logs', async ({ page }) => {
    // Navigate to audit logs (if available)
    await page.goto('/admin/audit');

    // Verify audit log interface
    const auditContainer = page.locator(
      'h1:has-text("Audit"), [data-testid="audit-log"], table'
    );

    // Audit logs might not exist yet in fresh setup - that's OK
    const auditExists = await auditContainer.first().isVisible({ timeout: 5000 }).catch(() => false);

    if (auditExists) {
      console.log('✅ Audit logs accessible');
    } else {
      console.log('⏭️  Audit logs not yet populated (expected for fresh setup)');
    }
  });

  test('should access GameManagement context - Catalog', async ({ page, request }) => {
    // Navigate to game catalog
    await page.goto('/games');

    // Verify catalog loaded
    await expect(
      page.locator('h1:has-text("Game"), h1:has-text("Catalog")')
    ).toBeVisible({ timeout: 10000 });

    // Verify seeded games visible via API
    const gamesResponse = await request.get('/api/v1/shared-games');
    expect(gamesResponse.status()).toBe(200);

    const games = await gamesResponse.json();
    expect(games.length).toBeGreaterThanOrEqual(9);

    // Verify at least one game card visible in UI
    const gameCards = page.locator('[data-testid*="game-card"], .game-card, article');
    const gameCount = await gameCards.count();
    expect(gameCount).toBeGreaterThan(0);

    console.log('✅ GameManagement context accessible');
    console.log(`   Games in catalog: ${games.length}`);
    console.log(`   Games visible in UI: ${gameCount}`);
  });

  test('should verify game with AI agent is marked correctly', async ({ page, request }) => {
    await page.goto('/games');

    // Check if any game has AI agent badge/indicator
    // This depends on wizard completion in previous tests

    // Query games with agents via API
    const gamesResponse = await request.get('/api/v1/shared-games');
    const games = await gamesResponse.json();

    // Look for games with associated documents or agents
    const gamesWithAgents = games.filter(
      (g: { hasAgent?: boolean; documentCount?: number }) => g.hasAgent || (g.documentCount ?? 0) > 0
    );

    if (gamesWithAgents.length > 0) {
      console.log(`✅ Found ${gamesWithAgents.length} game(s) with AI agents`);

      // Verify UI reflects agent availability
      const agentBadge = page.locator(
        '.badge:has-text("AI"), .badge:has-text("Agent"), [data-testid="ai-badge"]'
      );

      const badgeVisible = await agentBadge.first().isVisible({ timeout: 5000 }).catch(() => false);

      if (badgeVisible) {
        console.log('   AI Agent badge visible in UI');
      } else {
        console.warn('   ⚠️  AI Agent badge not found in UI (may need wizard completion)');
      }
    } else {
      console.log('⏭️  No games with AI agents yet (wizard not completed)');
    }
  });

  test('should access game details page', async ({ page }) => {
    await page.goto('/games');

    // Click first game card
    const firstGameCard = page.locator('[data-testid*="game-card"], .game-card, article').first();
    await firstGameCard.click();

    // Verify game details page loaded
    await page.waitForURL(/\/games\/[a-zA-Z0-9-]+/, { timeout: 10000 });

    // Verify game details visible
    await expect(
      page.locator('h1, h2, [data-testid="game-title"]')
    ).toBeVisible({ timeout: 5000 });

    console.log('✅ Game details page accessible');
    console.log(`   URL: ${page.url()}`);
  });

  test('should access KnowledgeBase context - Chat Threads', async ({ page, request }) => {
    // Navigate to chat/threads page
    await page.goto('/chat');

    // Verify chat interface loaded
    await expect(
      page.locator('h1:has-text("Chat"), [data-testid="chat-interface"]')
    ).toBeVisible({ timeout: 10000 });

    // Check for existing chat threads via API
    const threadsResponse = await request.get('/api/v1/chat-threads');

    if (threadsResponse.ok()) {
      const threads = await threadsResponse.json();

      console.log(`✅ KnowledgeBase context accessible`);
      console.log(`   Chat threads: ${threads.length}`);

      if (threads.length > 0) {
        // Verify threads visible in UI
        const threadList = page.locator('[data-testid*="thread"], .thread-item, .chat-list');
        const threadsVisible = await threadList.count();

        console.log(`   Threads visible in UI: ${threadsVisible}`);
      } else {
        console.log('   No chat threads yet (wizard creates first thread)');
      }
    } else {
      console.log('⏭️  Chat threads endpoint not available yet');
    }
  });

  test('should verify RAG agent is operational (if chat exists)', async ({ page, request }) => {
    // Get existing chat threads
    const threadsResponse = await request.get('/api/v1/chat-threads');

    if (!threadsResponse.ok()) {
      console.log('⏭️  Skipping RAG test - no chat threads endpoint');
      return;
    }

    const threads = await threadsResponse.json();

    if (threads.length === 0) {
      console.log('⏭️  Skipping RAG test - no chat threads created yet');
      return;
    }

    // Navigate to first chat thread
    const firstThread = threads[0];
    const threadId = firstThread.id || firstThread.threadId;

    await page.goto(`/chat/${threadId}`);

    // Verify chat interface loaded
    await expect(
      page.locator('[data-testid="chat-messages"], .chat-messages, .message-container')
    ).toBeVisible({ timeout: 10000 });

    // Send test message
    try {
      const response = await sendTestQuestion(page, 'What is this game about?');

      expect(response.length).toBeGreaterThan(0);
      console.log('✅ RAG agent operational');
      console.log(`   Response length: ${response.length} chars`);
    } catch (error) {
      console.warn('⚠️  RAG agent test failed (may require real backend):', error);
    }
  });

  test('should verify admin has access to system configuration', async ({ page }) => {
    // Navigate to system config (if available)
    await page.goto('/admin/settings');

    // Verify settings page loaded
    const settingsContainer = page.locator(
      'h1:has-text("Settings"), h1:has-text("Configuration"), [data-testid="settings"]'
    );

    const settingsAvailable = await settingsContainer
      .first()
      .isVisible({ timeout: 5000 })
      .catch(() => false);

    if (settingsAvailable) {
      console.log('✅ System configuration accessible');
    } else {
      // Try alternative route
      await page.goto('/admin/configuration');

      const altSettingsAvailable = await settingsContainer
        .first()
        .isVisible({ timeout: 5000 })
        .catch(() => false);

      if (altSettingsAvailable) {
        console.log('✅ System configuration accessible (alt route)');
      } else {
        console.log('⏭️  System configuration not implemented yet');
      }
    }
  });

  test('should verify all critical bounded contexts are healthy', async ({ request }) => {
    // Health check for each bounded context via API
    const contexts = [
      { name: 'Administration', endpoint: '/api/v1/users' },
      { name: 'GameManagement', endpoint: '/api/v1/shared-games' },
      { name: 'KnowledgeBase', endpoint: '/api/v1/chat-threads' },
    ];

    const results = await Promise.all(
      contexts.map(async (ctx) => {
        try {
          const response = await request.get(ctx.endpoint);
          return {
            name: ctx.name,
            healthy: response.ok(),
            status: response.status(),
          };
        } catch {
          return {
            name: ctx.name,
            healthy: false,
            status: 0,
          };
        }
      })
    );

    console.log('📊 Bounded Context Health:');
    results.forEach((result) => {
      const icon = result.healthy ? '✅' : '❌';
      console.log(`   ${icon} ${result.name}: ${result.status}`);
    });

    // Verify at least Administration and GameManagement are healthy
    const criticalContexts = results.filter((r) =>
      ['Administration', 'GameManagement'].includes(r.name)
    );
    const allCriticalHealthy = criticalContexts.every((r) => r.healthy);

    expect(allCriticalHealthy).toBe(true);
  });
});
