/**
 * Document Management Flow - Visual Documentation (Editor Role)
 *
 * Captures visual documentation for document management flows:
 * - Upload PDF rulebook
 * - Monitor processing status
 * - Manage documents
 * - View extraction results
 *
 * @see docs/08-user-flows/editor-role/02-document-management.md
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

// Mock PDF data
const MOCK_PDFS = [
  {
    id: 'pdf-1',
    gameId: 'game-1',
    fileName: 'ticket-to-ride-rulebook.pdf',
    fileSizeBytes: 2500000,
    pageCount: 12,
    processingStatus: 'Completed',
    documentType: 'base',
    uploadedAt: '2026-01-15T10:00:00Z',
    processedAt: '2026-01-15T10:05:00Z',
  },
  {
    id: 'pdf-2',
    gameId: 'game-1',
    fileName: 'expansion-rules.pdf',
    fileSizeBytes: 1500000,
    pageCount: 8,
    processingStatus: 'Processing',
    documentType: 'expansion',
    uploadedAt: '2026-01-19T14:00:00Z',
  },
];

test.describe('Document Management Flow - Visual Documentation (Editor)', () => {
  let helper: ScreenshotHelper;
  let authHelper: AuthHelper;

  test.beforeEach(async ({ page }) => {
    helper = new ScreenshotHelper({
      outputDir: EDITOR_FLOWS.documentManagement.outputDir,
      flow: EDITOR_FLOWS.documentManagement.name,
      role: EDITOR_FLOWS.documentManagement.role,
    });
    authHelper = new AuthHelper(page);
    await disableAnimations(page);

    // Setup editor session
    await authHelper.mockAuthenticatedSession(USER_FIXTURES.editor);

    // Mock game endpoint
    await page.route(`${API_BASE}/api/v1/games/game-1*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          id: 'game-1',
          title: 'Ticket to Ride',
          publisher: 'Days of Wonder',
        }),
      });
    });

    // Mock PDFs endpoints
    await page.route(`${API_BASE}/api/v1/games/game-1/pdfs*`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(MOCK_PDFS),
      });
    });

    // Mock ingest endpoint
    await page.route(`${API_BASE}/api/v1/ingest/pdf`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({ documentId: 'new-pdf-id' }),
      });
    });

    // Mock progress endpoint
    await page.route(`${API_BASE}/api/v1/pdfs/*/progress`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          currentStep: 'Extracting text',
          percentComplete: 65,
          pagesProcessed: 8,
          totalPages: 12,
        }),
      });
    });
  });

  test('upload PDF - document upload flow', async ({ page }) => {
    // Step 1: Navigate to game documents
    await page.goto('/admin/games/game-1/documents');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Game Documents',
      description: 'View and manage game documents',
      annotations: [
        { selector: 'button:has-text("Upload"), [data-testid="upload-pdf"]', label: 'Upload PDF', color: ANNOTATION_COLORS.success },
      ],
      nextAction: 'Upload new document',
    });

    // Step 2: Click upload
    const uploadBtn = page.locator('button:has-text("Upload"), [data-testid="upload-pdf"]').first();
    if (await uploadBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await uploadBtn.click();
      await waitForStableState(page);

      // Step 3: Upload form/modal
      const uploadForm = page.locator('[role="dialog"], .upload-form, form:has(input[type="file"])').first();
      if (await uploadForm.isVisible({ timeout: 2000 }).catch(() => false)) {
        await helper.capture(page, {
          step: 2,
          title: 'Upload Form',
          description: 'Select PDF file to upload',
          annotations: [
            { selector: 'input[type="file"]', label: 'File Input', color: ANNOTATION_COLORS.primary },
            { selector: 'select, [data-testid="document-type"]', label: 'Document Type', color: ANNOTATION_COLORS.info },
          ],
          previousAction: 'Open upload form',
          nextAction: 'Select file',
        });
      }
    }

    // Step 4: Document type selection
    const typeSelect = page.locator('select, [data-testid="document-type"]').first();
    if (await typeSelect.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 3,
        title: 'Document Type',
        description: 'Select document type (base rulebook, expansion, FAQ)',
        annotations: [
          { selector: 'select, [data-testid="document-type"]', label: 'Type Selection', color: ANNOTATION_COLORS.primary },
        ],
        previousAction: 'View types',
        nextAction: 'Select type and upload',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Upload PDF captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('processing status - monitor extraction', async ({ page }) => {
    // Step 1: Navigate to documents with processing document
    await page.goto('/admin/games/game-1/documents');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Documents List',
      description: 'View documents with processing status',
      nextAction: 'View processing document',
    });

    // Step 2: Processing indicator
    const processingItem = page.locator('text=Processing, [data-status="processing"], .processing').first();
    if (await processingItem.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Processing Status',
        description: 'Document being processed - text extraction in progress',
        annotations: [
          { selector: 'text=Processing, [data-status="processing"]', label: 'Processing', color: ANNOTATION_COLORS.warning },
        ],
        previousAction: 'View list',
        nextAction: 'Check progress',
      });
    }

    // Step 3: Progress details
    const progressBar = page.locator('[role="progressbar"], .progress-bar, .progress').first();
    if (await progressBar.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 3,
        title: 'Processing Progress',
        description: 'Detailed progress of PDF processing',
        annotations: [
          { selector: '[role="progressbar"], .progress-bar', label: 'Progress', color: ANNOTATION_COLORS.info },
          { selector: 'text=/\\d+%|\\d+ of \\d+/', label: 'Percentage', color: ANNOTATION_COLORS.primary },
        ],
        previousAction: 'View processing',
        nextAction: 'Wait for completion',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Processing status captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('document list - manage existing documents', async ({ page }) => {
    // Step 1: View documents
    await page.goto('/admin/games/game-1/documents');
    await waitForStableState(page);

    await helper.capture(page, {
      step: 1,
      title: 'Document Library',
      description: 'All documents for this game',
      nextAction: 'Manage documents',
    });

    // Step 2: Document card/row
    const docItem = page.locator('[data-testid="document-item"], .document-card, tr').first();
    if (await docItem.isVisible({ timeout: 3000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 2,
        title: 'Document Item',
        description: 'Individual document with actions',
        annotations: [
          { selector: '[data-testid="document-item"], .document-card', label: 'Document', color: ANNOTATION_COLORS.info },
          { selector: 'button:has-text("View"), [data-testid="view-pdf"]', label: 'View', color: ANNOTATION_COLORS.primary },
          { selector: 'button:has-text("Delete"), [data-testid="delete-pdf"]', label: 'Delete', color: ANNOTATION_COLORS.error },
        ],
        previousAction: 'View documents',
        nextAction: 'Perform action',
      });
    }

    // Step 3: Completed document details
    const completedDoc = page.locator('text=Completed, [data-status="completed"]').first();
    if (await completedDoc.isVisible({ timeout: 2000 }).catch(() => false)) {
      await helper.capture(page, {
        step: 3,
        title: 'Completed Document',
        description: 'Successfully processed document ready for use',
        annotations: [
          { selector: 'text=Completed, [data-status="completed"]', label: 'Completed', color: ANNOTATION_COLORS.success },
          { selector: 'text=/\\d+ pages/', label: 'Page Count', color: ANNOTATION_COLORS.info },
        ],
        previousAction: 'View document',
        nextAction: 'View details',
      });
    }

    helper.setTotalSteps(3);
    console.log(`\n✅ Document list captured: ${helper.getCapturedSteps().length} screenshots`);
  });

  test('view PDF - document viewer', async ({ page }) => {
    // Mock PDF download
    await page.route(`${API_BASE}/api/v1/pdfs/pdf-1/download`, async route => {
      await route.fulfill({
        status: 200,
        contentType: 'application/pdf',
        body: '%PDF-1.4 minimal content',
      });
    });

    // Step 1: Navigate and open viewer
    await page.goto('/admin/games/game-1/documents');
    await waitForStableState(page);

    const viewBtn = page.locator('button:has-text("View"), [data-testid="view-pdf"]').first();
    if (await viewBtn.isVisible({ timeout: 3000 }).catch(() => false)) {
      await viewBtn.click();
      await waitForStableState(page);

      // Step 2: PDF viewer modal
      const viewer = page.locator('[role="dialog"], .pdf-viewer, [data-testid="pdf-viewer"]').first();
      if (await viewer.isVisible({ timeout: 2000 }).catch(() => false)) {
        await helper.capture(page, {
          step: 1,
          title: 'PDF Viewer',
          description: 'View uploaded PDF document',
          annotations: [
            { selector: 'button:has-text("Previous"), [data-testid="prev-page"]', label: 'Previous', color: ANNOTATION_COLORS.neutral },
            { selector: 'button:has-text("Next"), [data-testid="next-page"]', label: 'Next', color: ANNOTATION_COLORS.neutral },
            { selector: 'button:has-text("Close"), [data-testid="close-viewer"]', label: 'Close', color: ANNOTATION_COLORS.primary },
          ],
          nextAction: 'Navigate pages',
        });
      }
    }

    helper.setTotalSteps(1);
    console.log(`\n✅ PDF viewer captured: ${helper.getCapturedSteps().length} screenshots`);
  });
});
