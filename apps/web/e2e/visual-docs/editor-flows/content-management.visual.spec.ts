/**
 * Content Management Flow - Visual Documentation (Editor Role)
 *
 * Captures visual documentation for content management flows:
 * - FAQ management
 * - Errata management
 * - Quick questions
 *
 * @see docs/08-user-flows/editor-role/03-content-management.md
 */

import { test } from '../../fixtures';
import { AuthHelper, USER_FIXTURES } from '../../pages';
import {
  ScreenshotHelper,
  EDITOR_FLOWS,
  disableAnimations,
  waitForStableState,
  ANNOTATION_COLORS,
} from '../fixtures/screenshot-helpers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Mock FAQ data
const MOCK_FAQS = [
  {
    id: 'faq-1',
    question: 'Can I draw two cards on my turn?',
    answer: 'Yes, drawing two train car cards is one of the three actions you can take on your turn.',
    category: 'Gameplay',
    createdAt: '2026-01-15T10:00:00Z',
  },
  {
    id: 'faq-2',
    question: 'What happens if I cannot complete a route?',
    answer: 'If you cannot complete a destination ticket, you lose the points shown on that ticket at the end of the game.',
    category: 'Scoring',
    createdAt: '2026-01-16T14:00:00Z',
  },
];

// Mock quick questions
const MOCK_QUICK_QUESTIONS = [
  { id: 'qq-1', question: 'How many cards can I hold?', answer: 'There is no hand limit.' },
  { id: 'qq-2', question: 'Can I take back a move?', answer: 'Once a train is placed, it cannot be moved.' },
];

test.describe('Content Management Flow - Visual Documentation (Editor)', () => {
  let helper: ScreenshotHelper;
  let authHelper: AuthHelper;

  test.beforeEach(async ({ page }) => {
    helper = new ScreenshotHelper({
      outputDir: EDITOR_FLOWS.contentManagement.outputDir,
      flow: EDITOR_FLOWS.contentManagement.name,
      role: EDITOR_FLOWS.contentManagement.role,
    });
    authHelper = new AuthHelper(page);
    await disableAnimations(page);

    // Setup editor session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.editor);

    // Mock game
    await page.route(`${API_BASE}/api/v1/games/game-1*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ id: 'game-1', title: 'Ticket to Ride' }),
      });
    });

    // Mock FAQ endpoints
    await page.route(`${API_BASE}/api/v1/admin/shared-games/game-1/faq*`, async route => {
      if (route.request().method() === 'POST') {
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'new-faq', question: 'New Question', answer: 'New Answer' }),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify(MOCK_FAQS),
        });
      }
    });

    // Mock quick questions
    await page.route(`${API_BASE}/api/v1/admin/shared-games/game-1/quick-questions*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_QUICK_QUESTIONS),
      });
    });
  });

  test('FAQ management - view and create', async ({ page }) => {
    // Step 1: Navigate to FAQ
    await page.goto('/admin/games/game-1/faq');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'FAQ Management',
      description: 'Manage frequently asked questions for the game',
      annotations: [
        { selector: 'button:has-text("Add FAQ"), button:has-text("New"), [data-testid="add-faq"]', label: 'Add FAQ', color: ANNOTATION_COLORS.success },
      ],
      nextAction: 'Add new FAQ',
    });

    // Step 2: FAQ list
    const faqList = page.locator('[data-testid="faq-list"], .faq-list, ul, table').first();
    if (await faqList.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'FAQ List',
        description: 'Existing FAQs with questions and answers',
        annotations: [
          { selector: '[data-testid="faq-item"], .faq-item, li', label: 'FAQ Entry', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View FAQs',
        nextAction: 'Edit or add FAQ',
      });
    }

    // Step 3: Add FAQ form
    const addBtn = page.locator('button:has-text("Add"), button:has-text("New"), [data-testid="add-faq"]').first();
    if (await addBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await addBtn.click();
      await waitForStableState(page);

      const faqForm = page.locator('[role="dialog"], form, .faq-form').first();
      if (await faqForm.isVisible({ timeout: 2000 }).catch(() => false)) {
        await helper.capture(page, {
          step: 3,
          title: 'Add FAQ Form',
          description: 'Create new FAQ entry',
          annotations: [
            { selector: 'input[name="question"], textarea[name="question"]', label: 'Question', color: ANNOTATION_COLORS.primary },
            { selector: 'textarea[name="answer"]', label: 'Answer', color: ANNOTATION_COLORS.primary },
          ],
          previousAction: 'Open form',
          nextAction: 'Enter FAQ content',
        });
      }
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ FAQ management captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('edit FAQ - update existing entry', async ({ page }) => {
    // Step 1: Navigate to FAQ
    await page.goto('/admin/games/game-1/faq');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'FAQ List for Edit',
      description: 'Select FAQ to edit',
      nextAction: 'Click edit',
    });

    // Step 2: Click edit on FAQ
    const editBtn = page.locator('button:has-text("Edit"), [data-testid="edit-faq"]').first();
    if (await editBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await editBtn.click();
      await waitForStableState(page);

      const editForm = page.locator('[role="dialog"], form, .edit-faq-form').first();
      if (await editForm.isVisible({ timeout: 2000 }).catch(() => false)) {
        await helper.capture(page, {
          step: 2,
          title: 'Edit FAQ',
          description: 'Modify existing FAQ entry',
          annotations: [
            { selector: 'input[name="question"], textarea[name="question"]', label: 'Question', color: ANNOTATION_COLORS.warning },
            { selector: 'textarea[name="answer"]', label: 'Answer', color: ANNOTATION_COLORS.warning },
            { selector: 'button[type="submit"], button:has-text("Save")', label: 'Save', color: ANNOTATION_COLORS.success },
          ],
          previousAction: 'Open edit form',
          nextAction: 'Save changes',
        });
      }
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Edit FAQ captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('quick questions - rapid Q&A management', async ({ page }) => {
    // Step 1: Navigate to quick questions
    await page.goto('/admin/games/game-1/quick-questions');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Quick Questions',
      description: 'Manage quick Q&A pairs for common rules questions',
      annotations: [
        { selector: 'button:has-text("Add"), [data-testid="add-quick-question"]', label: 'Add Question', color: ANNOTATION_COLORS.success },
      ],
      nextAction: 'Add quick question',
    });

    // Step 2: Quick questions list
    const qqList = page.locator('[data-testid="quick-questions"], .quick-questions-list').first();
    if (await qqList.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Quick Questions List',
        description: 'Short-form Q&A for instant rules lookup',
        annotations: [
          { selector: '[data-testid="quick-question-item"], .qq-item', label: 'Q&A Pair', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View questions',
        nextAction: 'Edit or add',
      });
    }

    // Step 3: Add inline
    const addBtn = page.locator('button:has-text("Add"), [data-testid="add-quick-question"]').first();
    if (await addBtn.isVisible({ timeout: 2000 }).catch(() => false)) {
      await addBtn.click();
      await waitForStableState(page);

      await helper.capture(page, {
        step: 3,
        title: 'Add Quick Question',
        description: 'Inline form for adding quick Q&A',
        annotations: [
          { selector: 'input[placeholder*="Question"], [data-testid="qq-question"]', label: 'Question', color: ANNOTATION_COLORS.primary },
          { selector: 'input[placeholder*="Answer"], [data-testid="qq-answer"]', label: 'Answer', color: ANNOTATION_COLORS.primary },
        ],
        previousAction: 'Click add',
        nextAction: 'Enter Q&A',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Quick questions captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('errata management - rule corrections', async ({ page }) => {
    // Mock errata endpoint
    await page.route(`${API_BASE}/api/v1/admin/shared-games/game-1/errata*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify([
          {
            id: 'errata-1',
            pageNumber: 5,
            originalText: 'Draw 2 cards',
            correctedText: 'Draw up to 2 cards',
            severity: 'minor',
            createdAt: '2026-01-17T10:00:00Z',
          },
        ]),
      });
    });

    // Step 1: Navigate to errata
    await page.goto('/admin/games/game-1/errata');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Errata Management',
      description: 'Manage rule corrections and updates',
      annotations: [
        { selector: 'button:has-text("Add Errata"), [data-testid="add-errata"]', label: 'Add Errata', color: ANNOTATION_COLORS.warning },
      ],
      nextAction: 'Add correction',
    });

    // Step 2: Errata list
    const errataItem = page.locator('[data-testid="errata-item"], .errata-item').first();
    if (await errataItem.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Errata Entry',
        description: 'Rule correction with original and corrected text',
        annotations: [
          { selector: '[data-testid="errata-item"], .errata-item', label: 'Correction', color: ANNOTATION_COLORS.warning },
          { selector: 'text=/Page \\d+|p\\.\\d+/', label: 'Page Reference', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View errata',
        nextAction: 'Edit or add',
      });
    }

    helper.setTotalSteps(2);
    console.log(`\n✅ Errata management captured: ${helper.getCapturedSteps().length} screenshots`);
  });
});
