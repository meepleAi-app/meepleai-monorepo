import { test, expect, ensureAdminAuth } from '../fixtures/onboarding-flow.fixture';
import { type OnboardingFlowState } from '../fixtures/onboarding-flow.fixture';
import { extractInvitation } from '../helpers/email-strategy';
import { cleanupOnboardingTest } from '../helpers/onboarding-cleanup';
import { env } from '../helpers/onboarding-environment';
import { AdminUsersPage } from '../pages/admin/AdminUsersPage';
import { AuditLogPage } from '../pages/admin/AuditLogPage';
import { AgentChatPage } from '../pages/agent/AgentChatPage';
import { AgentCreationPage } from '../pages/agent/AgentCreationPage';
import { AcceptInvitePage } from '../pages/auth/AcceptInvitePage';
import { LoginPage } from '../pages/auth/LoginPage';
import { LibraryPage } from '../pages/library/LibraryPage';

const timestamp = Date.now();
const testUserEmail =
  env.email.strategy === 'mailosaur'
    ? `e2e-onboarding-${timestamp}@${env.email.mailosaurServerId}.mailosaur.net`
    : `e2e-onboarding-${timestamp}@test.local`;
const testUserPassword = `E2eTest!${timestamp}`;

test.describe.configure({ mode: 'serial' });

test.describe('Admin-User Onboarding Flow', () => {
  const state: Partial<OnboardingFlowState> = {};

  test.afterAll(async () => {
    if (state.adminPage) {
      try {
        await cleanupOnboardingTest(state.adminPage.request, {
          testUserId: state.testUserId,
          agentId: state.agentId,
        });
      } catch (e) {
        console.warn('Cleanup failed (orphaned test data may remain):', e);
      }
    }

    if (state.userContext) await state.userContext.close();
    if (state.adminContext) await state.adminContext.close();
  });

  // ── Test 1: Admin Login ──────────────────────────────────────
  test('1. Admin logs in', async ({ browser }) => {
    const adminContext = await browser.newContext();
    const adminPage = await adminContext.newPage();

    await test.step('Navigate to login', async () => {
      const loginPage = new LoginPage(adminPage);
      await loginPage.goto();
      await loginPage.login(env.admin.email, env.admin.password);
    });

    await test.step('Verify admin dashboard', async () => {
      await adminPage.waitForURL(
        url => url.pathname.includes('/admin') || url.pathname.includes('/dashboard'),
        { timeout: 15_000 }
      );

      // Verify admin UI is loaded (admin toggle or any admin-specific element)
      const adminIndicator = adminPage
        .locator('[data-testid="user-display-name"], [data-testid="navbar-user"]')
        .or(adminPage.getByText(/admin/i).first());
      await expect(adminIndicator).toBeVisible({ timeout: 5_000 });

      const errorToast = adminPage.locator('[data-testid="toast-error"], .toast-error');
      await expect(errorToast).not.toBeVisible();
    });

    state.adminPage = adminPage;
    state.adminContext = adminContext;
    state.adminCredentials = env.admin;
  });

  // ── Test 2: Admin Invites User ───────────────────────────────
  test('2. Admin invites user via email', async () => {
    if (!state.adminPage) test.skip(true, 'Requires test 1 to pass');
    const page = state.adminPage!;
    const adminUsersPage = new AdminUsersPage(page);

    await test.step('Open invite dialog', async () => {
      await adminUsersPage.goto();
      await adminUsersPage.clickInviteButton();
    });

    await test.step('Fill and send invitation', async () => {
      await adminUsersPage.fillInvitationForm(testUserEmail, 'user');
      await adminUsersPage.submitInvitation();
      await adminUsersPage.waitForNetworkIdle();
    });

    await test.step('Extract invitation token/URL', async () => {
      const result = await extractInvitation(page, testUserEmail);
      state.invitationToken = result.invitationToken;
      state.invitationUrl = result.invitationUrl;
      state.testUserEmail = testUserEmail;
    });

    expect(state.invitationToken).toBeTruthy();
  });

  // ── Test 3: User Accepts Invitation ──────────────────────────
  test('3. User accepts invitation and sets password', async ({ browser }) => {
    if (!state.invitationToken) test.skip(true, 'Requires test 2 to pass');
    const userContext = await browser.newContext();
    const userPage = await userContext.newPage();
    const acceptPage = new AcceptInvitePage(userPage);

    await test.step('Navigate to accept-invite page', async () => {
      if (env.email.strategy === 'mailosaur') {
        await acceptPage.gotoUrl(state.invitationUrl!);
      } else {
        await acceptPage.gotoWithToken(state.invitationToken!);
      }
    });

    await test.step('Set password', async () => {
      await acceptPage.setPassword(testUserPassword);
      await acceptPage.verifyStrengthIndicator('Strong');
    });

    await test.step('Submit and wait for redirect', async () => {
      await acceptPage.submit();
      await acceptPage.waitForRedirectAfterAccept();
    });

    state.userPassword = testUserPassword;
    state.userPage = userPage;
    state.userContext = userContext;
  });

  // ── Test 4: User Logs In ─────────────────────────────────────
  test('4. User logs in with new password', async () => {
    if (!state.userPage) test.skip(true, 'Requires test 3 to pass');
    const page = state.userPage!;
    const loginPage = new LoginPage(page);

    await test.step('Navigate to login and authenticate', async () => {
      await loginPage.goto();
      await loginPage.login(testUserEmail, testUserPassword);
    });

    await test.step('Verify dashboard redirect', async () => {
      await page.waitForURL(
        url =>
          url.pathname.includes('/dashboard') ||
          url.pathname.includes('/library') ||
          url.pathname.includes('/'),
        { timeout: 15_000 }
      );

      const errorToast = page.locator('[data-testid="toast-error"], .toast-error');
      await expect(errorToast).not.toBeVisible();
    });

    await test.step('Capture user ID', async () => {
      try {
        const meResponse = await page.request.get(`${env.apiURL}/api/v1/auth/me`);
        if (meResponse.ok()) {
          const meData = await meResponse.json();
          state.testUserId = meData.id ?? meData.userId ?? '';
        }
      } catch {
        console.warn('Could not capture testUserId from /auth/me');
      }
    });
  });

  // ── Test 5: User Adds Game to Collection ─────────────────────
  test('5. User adds game to collection', async () => {
    if (!state.userPage) test.skip(true, 'Requires test 4 to pass');
    const page = state.userPage!;
    const libraryPage = new LibraryPage(page);

    await test.step('Navigate to library', async () => {
      await page.goto('/library');
      await page.waitForLoadState('domcontentloaded');
    });

    await test.step('Search and add game', async () => {
      await libraryPage.clickAddGame();
      await libraryPage.selectFromCatalog();
      await libraryPage.searchGame(env.seedGameName);

      const { gameId, gameTitle } = await libraryPage.selectFirstSearchResult();

      if (!gameId && env.name === 'local') {
        throw new Error(
          `Seed game "${env.seedGameName}" not found. Ensure DB is seeded via: dotnet ef database update`
        );
      }

      state.gameId = gameId;
      state.gameTitle = gameTitle || env.seedGameName;
    });

    await test.step('Navigate through wizard and save', async () => {
      await libraryPage.clickNext();
      // Skip KB step if present
      const nextBtn2 = page.getByRole('button', { name: /avanti|next/i });
      if (await nextBtn2.isVisible({ timeout: 2_000 }).catch(() => false)) {
        await nextBtn2.click();
      }
      await libraryPage.confirmAddToCollection();
      await libraryPage.verifyGameInCollection(state.gameTitle!);
    });
  });

  // ── Test 6: User Creates Agent ───────────────────────────────
  test('6. User creates agent for the game', async () => {
    if (!state.userPage || !state.gameTitle) test.skip(true, 'Requires test 5 to pass');
    const page = state.userPage!;
    const agentPage = new AgentCreationPage(page);

    await test.step('Open agent creation', async () => {
      await agentPage.goto();
      await agentPage.openCreationSheet();
    });

    await test.step('Configure agent', async () => {
      await agentPage.selectGame(state.gameTitle!);
      await agentPage.selectStrategy('Tutor');
      await agentPage.selectFreeTier();
    });

    await test.step('Submit and capture IDs', async () => {
      const result = await agentPage.submitCreation();
      state.agentId = result.agentId;
      state.gameSessionId = result.gameSessionId;

      expect(state.agentId).toBeTruthy();
    });

    await test.step('Wait for agent ready', async () => {
      await agentPage.waitForAgentReady(env.timeouts.agentReady);
    });
  });

  // ── Test 7: User Chats with Agent ────────────────────────────
  test('7. User asks agent about game scope and turn', async () => {
    if (!state.userPage || !state.agentId) test.skip(true, 'Requires test 6 to pass');
    const page = state.userPage!;
    const chatPage = new AgentChatPage(page);

    await test.step('Open chat with agent', async () => {
      await chatPage.navigateToChat(state.agentId!);
    });

    await test.step('Send question and wait for response', async () => {
      const gameTitle = state.gameTitle!;
      await chatPage.sendMessage(
        `Qual è lo scopo del gioco ${gameTitle}? Descrivimi un turno di gioco.`
      );

      const responseText = await chatPage.waitForAgentResponse(env.timeouts.chatResponse);

      await chatPage.verifyResponseIsValid(responseText);
    });
  });

  // ── Test 8: Admin Changes Role & Checks Audit Log ────────────
  test('8. Admin changes user role and verifies audit log', async () => {
    if (!state.adminPage || !state.testUserEmail) test.skip(true, 'Requires tests 1-2 to pass');
    const page = state.adminPage!;

    await ensureAdminAuth(page, state.adminCredentials!);

    const adminUsersPage = new AdminUsersPage(page);
    const auditLogPage = new AuditLogPage(page);

    await test.step('Change user role to editor', async () => {
      await adminUsersPage.goto();
      await adminUsersPage.changeUserRole(testUserEmail, 'editor');
      await adminUsersPage.waitForNetworkIdle();
    });

    await test.step('Verify role changed in UI', async () => {
      await adminUsersPage.verifyUserRole(testUserEmail, 'editor');
    });

    await test.step('Verify audit log entry', async () => {
      await auditLogPage.goto();
      await auditLogPage.verifyRoleChangeEntry(testUserEmail, {
        newRole: 'editor',
      });
    });
  });
});
