/**
 * Approval Workflow - Visual Documentation (Admin Role)
 *
 * Captures visual documentation for approval workflow:
 * - View pending approvals queue
 * - Review submission details
 * - Approve publication
 * - Reject with feedback
 *
 * @see docs/08-user-flows/admin-role/01-approval-workflow.md
 */

import { test } from '../../fixtures';
import { AuthHelper, USER_FIXTURES } from '../../pages';
import {
  ScreenshotHelper,
  ADMIN_FLOWS,
  disableAnimations,
  waitForStableState,
  ANNOTATION_COLORS,
} from '../fixtures/screenshot-helpers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock pending approvals
const MOCK_PENDING_APPROVALS = [
  {
    id: 'pending-1',
    title: 'Wingspan',
    submittedBy: 'editor@example.com',
    submittedAt: '2026-01-19T10:00:00Z',
    status: 'pending_approval',
    documentCount: 2,
    faqCount: 5,
  },
  {
    id: 'pending-2',
    title: 'Terraforming Mars',
    submittedBy: 'editor2@example.com',
    submittedAt: '2026-01-18T14:00:00Z',
    status: 'pending_approval',
    documentCount: 1,
    faqCount: 3,
  },
];

test.describe('Approval Workflow - Visual Documentation (Admin)', () => {
  let helper: ScreenshotHelper;
  let authHelper: AuthHelper;

  test.beforeEach(async ({ page }) => {
    helper = new ScreenshotHelper({
      outputDir: ADMIN_FLOWS.approvalWorkflow.outputDir,
      flow: ADMIN_FLOWS.approvalWorkflow.name,
      role: ADMIN_FLOWS.approvalWorkflow.role,
    });
    authHelper = new AuthHelper(page);
    await disableAnimations(page);

    // Setup admin session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.admin);

    // Mock pending approvals
    await page.route(`${API_BASE}/api/v1/admin/shared-games/pending-approvals*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ items: MOCK_PENDING_APPROVALS, total: 2 }),
      });
    });

    // Mock single game details
    await page.route(`${API_BASE}/api/v1/admin/shared-games/pending-1*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          ...MOCK_PENDING_APPROVALS[0],
          description: 'A competitive bird-collection engine-building game.',
          publisher: 'Stonemaier Games',
          yearPublished: 2019,
        }),
      });
    });
  });

  test('approval queue - pending submissions', async ({ page }) => {
    // Step 1: Navigate to approvals
    await page.goto('/admin/approvals');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Approval Queue',
      description: 'View all games pending admin approval',
      annotations: [
        { selector: 'h1, [data-testid="approvals-heading"]', label: 'Approvals', color: ANNOTATION_COLORS.primary },
      ],
      nextAction: 'Review submissions',
    });

    // Step 2: Queue list
    const queueList = page.locator('[data-testid="approval-queue"], .approval-list, table, ul').first();
    if (await queueList.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Pending Items',
        description: 'List of games awaiting review',
        annotations: [
          { selector: '[data-testid="approval-item"], .approval-item, tr', label: 'Submission', color: ANNOTATION_COLORS.warning },
        ],
        previousAction: 'View queue',
        nextAction: 'Select to review',
      });
    }

    // Step 3: Queue metrics
    const metrics = page.locator('text=/\\d+ pending|\\d+ items/i, [data-testid="queue-count"]').first();
    if (await metrics.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 3,
        title: 'Queue Metrics',
        description: 'Number of items requiring review',
        annotations: [
          { selector: 'text=/\\d+ pending/i', label: 'Count', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View count',
        nextAction: 'Start review',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Approval queue captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('review submission - detailed view', async ({ page }) => {
    // Step 1: Navigate to submission detail
    await page.goto('/admin/approvals/pending-1');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Review Submission',
      description: 'Detailed view of submitted game for review',
      annotations: [
        { selector: 'h1, [data-testid="game-title"]', label: 'Game', color: ANNOTATION_COLORS.primary },
        { selector: 'text=/submitted by/i', label: 'Submitter', color: ANNOTATION_COLORS.info },
      ],
      nextAction: 'Review content',
    });

    // Step 2: Content sections
    const contentSection = page.locator('[data-testid="content-review"], .review-section').first();
    if (await contentSection.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Content Review',
        description: 'Review game details, documents, and FAQ',
        annotations: [
          { selector: 'text=/\\d+ document/i', label: 'Documents', color: ANNOTATION_COLORS.info },
          { selector: 'text=/\\d+ FAQ/i', label: 'FAQs', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View details',
        nextAction: 'Make decision',
      });
    }

    // Step 3: Action buttons
    const actions = page.locator('[data-testid="approval-actions"], .action-buttons').first();
    if (await actions.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 3,
        title: 'Review Actions',
        description: 'Approve or reject the submission',
        annotations: [
          { selector: 'button:has-text("Approve"), [data-testid="approve"]', label: 'Approve', color: ANNOTATION_COLORS.success },
          { selector: 'button:has-text("Reject"), [data-testid="reject"]', label: 'Reject', color: ANNOTATION_COLORS.error },
        ],
        previousAction: 'Review complete',
        nextAction: 'Approve or reject',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Review submission captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('approve publication - success flow', async ({ page }) => {
    // Mock approve endpoint
    await page.route(`${API_BASE}/api/v1/admin/shared-games/pending-1/approve-publication`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ status: 'published' }),
      });
    });

    // Step 1: Navigate to submission
    await page.goto('/admin/approvals/pending-1');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Ready to Approve',
      description: 'Submission reviewed and ready for approval',
      nextAction: 'Click approve',
    });

    // Step 2: Click approve
    const approveBtn = page.locator('button:has-text("Approve"), [data-testid="approve"]').first();
    if (await approveBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await approveBtn.click();
      await waitForStableState(page);

      // Step 3: Confirmation dialog
      const confirmDialog = page.locator('[role="dialog"], [role="alertdialog"]').first();
      if (await confirmDialog.isVisible({ timeout: 2000 }).catch(() => false)) {
        await helper.capture(page, {
          step: 2,
          title: 'Confirm Approval',
          description: 'Confirm game publication to catalog',
          annotations: [
            { selector: 'button:has-text("Confirm"), button:has-text("Publish")', label: 'Confirm', color: ANNOTATION_COLORS.success },
          ],
          previousAction: 'Click approve',
          nextAction: 'Confirm publication',
        });
      }
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Approve flow captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('reject submission - feedback flow', async ({ page }) => {
    // Mock reject endpoint
    await page.route(`${API_BASE}/api/v1/admin/shared-games/pending-1/reject`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ status: 'rejected' }),
      });
    });

    // Step 1: Navigate to submission
    await page.goto('/admin/approvals/pending-1');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Submission to Reject',
      description: 'Submission requiring rejection with feedback',
      nextAction: 'Click reject',
    });

    // Step 2: Click reject
    const rejectBtn = page.locator('button:has-text("Reject"), [data-testid="reject"]').first();
    if (await rejectBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await rejectBtn.click();
      await waitForStableState(page);

      // Step 3: Rejection dialog with reason
      const rejectDialog = page.locator('[role="dialog"]').first();
      if (await rejectDialog.isVisible({ timeout: 2000 }).catch(() => false)) {
        await helper.capture(page, {
          step: 2,
          title: 'Rejection Feedback',
          description: 'Provide reason for rejection to help editor improve',
          annotations: [
            { selector: 'textarea, [data-testid="rejection-reason"]', label: 'Reason', color: ANNOTATION_COLORS.warning },
          ],
          previousAction: 'Click reject',
          nextAction: 'Enter reason',
        });

        // Fill reason
        const reasonInput = page.locator('textarea, [data-testid="rejection-reason"]').first();
        if (await reasonInput.isVisible({ timeout: 1000 }).catch(() => false)) {
          await reasonInput.fill('Missing rulebook PDF. Please upload the official rulebook.');
          await waitForStableState(page);

          await helper.capture(page, {
            step: 3,
            title: 'Reason Entered',
            description: 'Rejection feedback ready to send',
            annotations: [
              { selector: 'textarea', label: 'Feedback', color: ANNOTATION_COLORS.error },
              { selector: 'button:has-text("Confirm"), button:has-text("Reject")', label: 'Send', color: ANNOTATION_COLORS.error },
            ],
            previousAction: 'Enter reason',
            nextAction: 'Send rejection',
          });
        }
      }
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Reject flow captured: ${helper.getCapturedSteps().length} screenshots`);
  });
});
