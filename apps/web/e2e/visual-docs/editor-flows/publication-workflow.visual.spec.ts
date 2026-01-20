/**
 * Publication Workflow - Visual Documentation (Editor Role)
 *
 * Captures visual documentation for publication workflow:
 * - Submit for approval
 * - View submission status
 * - Handle rejection feedback
 * - Publication success
 *
 * @see docs/08-user-flows/editor-role/04-publication-workflow.md
 */

import { test } from '../../fixtures/chromatic';
import { AuthHelper, USER_FIXTURES } from '../../pages';
import {
  ScreenshotHelper,
  EDITOR_FLOWS,
  disableAnimations,
  waitForStableState,
  ANNOTATION_COLORS,
} from '../fixtures/screenshot-helpers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock game states
const MOCK_DRAFT_GAME = {
  id: 'game-draft',
  title: 'Wingspan',
  status: 'draft',
  completenessScore: 85,
  missingFields: ['description'],
};

const MOCK_PENDING_GAME = {
  id: 'game-pending',
  title: 'Catan',
  status: 'pending_approval',
  submittedAt: '2026-01-19T10:00:00Z',
};

const MOCK_REJECTED_GAME = {
  id: 'game-rejected',
  title: 'Risk',
  status: 'rejected',
  rejectionReason: 'Missing rulebook PDF and incomplete description',
  rejectedAt: '2026-01-18T14:00:00Z',
};

test.describe('Publication Workflow - Visual Documentation (Editor)', () => {
  let helper: ScreenshotHelper;
  let authHelper: AuthHelper;

  test.beforeEach(async ({ page }) => {
    helper = new ScreenshotHelper({
      outputDir: EDITOR_FLOWS.publicationWorkflow.outputDir,
      flow: EDITOR_FLOWS.publicationWorkflow.name,
      role: EDITOR_FLOWS.publicationWorkflow.role,
    });
    authHelper = new AuthHelper(page);
    await disableAnimations(page);

    // Setup editor session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.editor);
  });

  test('submit for approval - ready game', async ({ page }) => {
    // Mock draft game
    await page.route(`${API_BASE}/api/v1/admin/shared-games/game-draft*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ ...MOCK_DRAFT_GAME, completenessScore: 100, missingFields: [] }),
      });
    });

    await page.route(`${API_BASE}/api/v1/admin/shared-games/game-draft/submit-for-approval`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ status: 'pending_approval' }),
      });
    });

    // Step 1: Navigate to game
    await page.goto('/admin/games/game-draft');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Draft Game Ready',
      description: 'Game with all required content ready for submission',
      annotations: [
        { selector: 'text=draft, [data-status="draft"]', label: 'Draft Status', color: ANNOTATION_COLORS.warning },
        { selector: 'button:has-text("Submit"), [data-testid="submit-approval"]', label: 'Submit', color: ANNOTATION_COLORS.success },
      ],
      nextAction: 'Submit for approval',
    });

    // Step 2: Completeness check
    const completenessScore = page.locator('text=/\\d+%|complete/i, [data-testid="completeness"]').first();
    if (await completenessScore.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Completeness Check',
        description: 'Verify all required fields are filled',
        annotations: [
          { selector: 'text=/\\d+%|complete/i', label: 'Score', color: ANNOTATION_COLORS.success },
        ],
        previousAction: 'View status',
        nextAction: 'Click submit',
      });
    }

    // Step 3: Submit confirmation
    const submitBtn = page.locator('button:has-text("Submit"), [data-testid="submit-approval"]').first();
    if (await submitBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await submitBtn.click();
      await waitForStableState(page);

      const confirmDialog = page.locator('[role="dialog"], [role="alertdialog"]').first();
      if (await confirmDialog.isVisible({ timeout: 2000 }).catch(() => false)) {
        await helper.capture(page, {
          step: 3,
          title: 'Confirm Submission',
          description: 'Confirm submission for admin review',
          annotations: [
            { selector: 'button:has-text("Confirm"), button:has-text("Submit")', label: 'Confirm', color: ANNOTATION_COLORS.success },
          ],
          previousAction: 'Click submit',
          nextAction: 'Confirm',
        });
      }
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Submit for approval captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('incomplete game - cannot submit', async ({ page }) => {
    // Mock incomplete game
    await page.route(`${API_BASE}/api/v1/admin/shared-games/game-draft*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_DRAFT_GAME),
      });
    });

    // Step 1: View incomplete game
    await page.goto('/admin/games/game-draft');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Incomplete Game',
      description: 'Game missing required content cannot be submitted',
      annotations: [
        { selector: 'text=/\\d+%|incomplete/i', label: 'Incomplete', color: ANNOTATION_COLORS.warning },
      ],
      nextAction: 'View missing fields',
    });

    // Step 2: Missing fields indicator
    const missingFields = page.locator('text=/missing|required/i, [data-testid="missing-fields"]').first();
    if (await missingFields.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Missing Fields',
        description: 'List of required fields that need to be completed',
        annotations: [
          { selector: 'text=/missing|required/i', label: 'Missing', color: ANNOTATION_COLORS.error },
        ],
        previousAction: 'View status',
        nextAction: 'Complete fields',
      });
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Incomplete game captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('pending approval - waiting for review', async ({ page }) => {
    // Mock pending game
    await page.route(`${API_BASE}/api/v1/admin/shared-games/game-pending*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_PENDING_GAME),
      });
    });

    // Step 1: View pending game
    await page.goto('/admin/games/game-pending');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Pending Approval',
      description: 'Game submitted and waiting for admin review',
      annotations: [
        { selector: 'text=/pending|waiting|review/i, [data-status="pending"]', label: 'Pending', color: ANNOTATION_COLORS.warning },
      ],
      nextAction: 'Wait for review',
    });

    // Step 2: Submission timestamp
    const submittedInfo = page.locator('text=/submitted|sent/i').first();
    if (await submittedInfo.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Submission Info',
        description: 'When the game was submitted for review',
        annotations: [
          { selector: 'text=/submitted|sent/i', label: 'Submitted', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View pending',
        nextAction: 'Await decision',
      });
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Pending approval captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('rejected submission - feedback handling', async ({ page }) => {
    // Mock rejected game
    await page.route(`${API_BASE}/api/v1/admin/shared-games/game-rejected*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_REJECTED_GAME),
      });
    });

    // Step 1: View rejected game
    await page.goto('/admin/games/game-rejected');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Rejected Submission',
      description: 'Game rejected with feedback from admin',
      annotations: [
        { selector: 'text=/rejected/i, [data-status="rejected"]', label: 'Rejected', color: ANNOTATION_COLORS.error },
      ],
      nextAction: 'View feedback',
    });

    // Step 2: Rejection reason
    const rejectionReason = page.locator('text=/reason|feedback|comment/i, [data-testid="rejection-reason"]').first();
    if (await rejectionReason.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Rejection Feedback',
        description: 'Admin feedback explaining why submission was rejected',
        annotations: [
          { selector: 'text=/reason|feedback/i', label: 'Feedback', color: ANNOTATION_COLORS.warning },
        ],
        previousAction: 'View status',
        nextAction: 'Address issues',
      });
    }

    // Step 3: Resubmit option
    const resubmitBtn = page.locator('button:has-text("Resubmit"), button:has-text("Submit Again"), [data-testid="resubmit"]').first();
    if (await resubmitBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 3,
        title: 'Resubmit Option',
        description: 'After fixing issues, resubmit for review',
        annotations: [
          { selector: 'button:has-text("Resubmit"), [data-testid="resubmit"]', label: 'Resubmit', color: ANNOTATION_COLORS.success },
        ],
        previousAction: 'Fix issues',
        nextAction: 'Resubmit',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Rejected submission captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('published game - success state', async ({ page }) => {
    // Mock published game
    await page.route(`${API_BASE}/api/v1/admin/shared-games/game-published*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'game-published',
          title: 'Ticket to Ride',
          status: 'published',
          publishedAt: '2026-01-19T15:00:00Z',
        }),
      });
    });

    // Step 1: View published game
    await page.goto('/admin/games/game-published');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Published Game',
      description: 'Game successfully published and visible to users',
      annotations: [
        { selector: 'text=/published/i, [data-status="published"]', label: 'Published', color: ANNOTATION_COLORS.success },
      ],
      nextAction: 'View in catalog',
    });

    // Step 2: Publication details
    const publishedInfo = page.locator('text=/published at|live since/i').first();
    if (await publishedInfo.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Publication Details',
        description: 'When the game was published to the catalog',
        annotations: [
          { selector: 'text=/published at|live/i', label: 'Published Date', color: ANNOTATION_COLORS.success },
        ],
        previousAction: 'View status',
        nextAction: 'Manage published game',
      });
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Published game captured: ${helper.getCapturedSteps().length} screenshots`);
  });
});
