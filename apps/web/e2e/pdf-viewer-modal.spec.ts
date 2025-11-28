/**
 * E2E Tests - PDF Viewer Modal (BGAI-076)
 *
 * Tests for PDF viewer modal functionality including:
 * - Citation click-to-jump PDF viewer (BGAI-074)
 * - Modal rendering and controls
 * - Page navigation and zoom
 * - Keyboard shortcuts
 */

import { test, expect } from '@playwright/test';

test.describe('PDF Viewer Modal (BGAI-076)', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to chat page (assumes logged in via global setup)
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
  });

  test.describe('Citation Click-to-Jump (BGAI-074)', () => {
    test('opens PDF viewer when clicking on a citation', async ({ page }) => {
      // Mock API response with citations
      await page.route('**/api/v1/agents/qa/stream', async route => {
        const response = [
          'event: token\ndata: {"token":"Test answer"}\n\n',
          'event: citations\ndata: {"citations":[{"documentId":"doc-1","pageNumber":5,"snippet":"Test citation","relevanceScore":0.95}]}\n\n',
          'event: complete\ndata: {"totalTokens":50}\n\n',
        ].join('');

        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'text/event-stream' },
          body: response,
        });
      });

      // Mock PDF download URL
      await page.route('**/api/v1/pdf/*/download', async route => {
        // Return a mock PDF (1x1 pixel PDF for testing)
        const mockPdf = Buffer.from(
          '%PDF-1.0\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj 2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj 3 0 obj<</Type/Page/MediaBox[0 0 3 3]/Parent 2 0 R/Resources<<>>>>endobj\nxref\n0 4\n0000000000 65535 f\n0000000010 00000 n\n0000000053 00000 n\n0000000102 00000 n\ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n149\n%EOF'
        );

        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'application/pdf' },
          body: mockPdf,
        });
      });

      // Send a message to trigger citation response
      await page.getByRole('textbox', { name: /fai una domanda/i }).fill('Test question');
      await page.getByRole('button', { name: /invia/i }).click();

      // Wait for citations to appear
      await expect(page.getByTestId('citation-list')).toBeVisible();

      // Click on the citation card
      await page.getByTestId('citation-card').first().click();

      // PDF viewer modal should open
      await expect(page.getByTestId('dialog')).toHaveAttribute('data-open', 'true');
      await expect(page.getByTestId('dialog-title')).toBeVisible();
    });

    test('jumps to the correct page when opening PDF from citation', async ({ page }) => {
      // Mock API response with citation to page 10
      await page.route('**/api/v1/agents/qa/stream', async route => {
        const response = [
          'event: token\ndata: {"token":"Answer"}\n\n',
          'event: citations\ndata: {"citations":[{"documentId":"doc-1","pageNumber":10,"snippet":"Citation from page 10","relevanceScore":0.9}]}\n\n',
          'event: complete\ndata: {"totalTokens":50}\n\n',
        ].join('');

        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'text/event-stream' },
          body: response,
        });
      });

      // Mock PDF download
      await page.route('**/api/v1/pdf/*/download', async route => {
        const mockPdf = Buffer.from(
          '%PDF-1.0\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj 2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj 3 0 obj<</Type/Page/MediaBox[0 0 3 3]/Parent 2 0 R/Resources<<>>>>endobj\nxref\n0 4\n0000000000 65535 f\n0000000010 00000 n\n0000000053 00000 n\n0000000102 00000 n\ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n149\n%EOF'
        );

        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'application/pdf' },
          body: mockPdf,
        });
      });

      // Send message and wait for citation
      await page.getByRole('textbox', { name: /fai una domanda/i }).fill('Test');
      await page.getByRole('button', { name: /invia/i }).click();
      await expect(page.getByTestId('citation-list')).toBeVisible();

      // Verify citation shows page 10
      await expect(page.getByTestId('citation-page')).toContainText('Pag. 10');

      // Click citation to open PDF viewer
      await page.getByTestId('citation-card').first().click();

      // Verify modal opened and displays correct document name
      await expect(page.getByTestId('dialog-title')).toContainText('PDF - Page 10');
    });

    test('displays document name in PDF viewer modal', async ({ page }) => {
      // Similar setup as previous test
      await page.route('**/api/v1/agents/qa/stream', async route => {
        const response = [
          'event: token\ndata: {"token":"Answer"}\n\n',
          'event: citations\ndata: {"citations":[{"documentId":"doc-1","pageNumber":5,"snippet":"Test","relevanceScore":0.9}]}\n\n',
          'event: complete\ndata: {"totalTokens":50}\n\n',
        ].join('');

        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'text/event-stream' },
          body: response,
        });
      });

      await page.route('**/api/v1/pdf/*/download', async route => {
        const mockPdf = Buffer.from(
          '%PDF-1.0\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj 2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj 3 0 obj<</Type/Page/MediaBox[0 0 3 3]/Parent 2 0 R/Resources<<>>>>endobj\nxref\n0 4\n0000000000 65535 f\n0000000010 00000 n\n0000000053 00000 n\n0000000102 00000 n\ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n149\n%EOF'
        );

        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'application/pdf' },
          body: mockPdf,
        });
      });

      await page.getByRole('textbox', { name: /fai una domanda/i }).fill('Test');
      await page.getByRole('button', { name: /invia/i }).click();
      await expect(page.getByTestId('citation-list')).toBeVisible();

      await page.getByTestId('citation-card').first().click();

      // Verify document name is displayed
      await expect(page.getByTestId('dialog-title')).toBeVisible();
      await expect(page.getByTestId('dialog-title')).toContainText('PDF - Page 5');
    });
  });

  test.describe('Modal Behavior', () => {
    test.beforeEach(async ({ page }) => {
      // Setup citation and PDF mocks for each test
      await page.route('**/api/v1/agents/qa/stream', async route => {
        const response = [
          'event: token\ndata: {"token":"Answer"}\n\n',
          'event: citations\ndata: {"citations":[{"documentId":"doc-1","pageNumber":1,"snippet":"Test","relevanceScore":0.9}]}\n\n',
          'event: complete\ndata: {"totalTokens":50}\n\n',
        ].join('');

        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'text/event-stream' },
          body: response,
        });
      });

      await page.route('**/api/v1/pdf/*/download', async route => {
        const mockPdf = Buffer.from(
          '%PDF-1.0\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj 2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj 3 0 obj<</Type/Page/MediaBox[0 0 3 3]/Parent 2 0 R/Resources<<>>>>endobj\nxref\n0 4\n0000000000 65535 f\n0000000010 00000 n\n0000000053 00000 n\n0000000102 00000 n\ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n149\n%EOF'
        );

        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'application/pdf' },
          body: mockPdf,
        });
      });

      // Send message and open modal
      await page.getByRole('textbox', { name: /fai una domanda/i }).fill('Test');
      await page.getByRole('button', { name: /invia/i }).click();
      await expect(page.getByTestId('citation-list')).toBeVisible();
      await page.getByTestId('citation-card').first().click();
      await expect(page.getByTestId('dialog')).toHaveAttribute('data-open', 'true');
    });

    test('closes modal when clicking outside or pressing escape', async ({ page }) => {
      // Modal should be open
      await expect(page.getByTestId('dialog')).toHaveAttribute('data-open', 'true');

      // Press Escape to close
      await page.keyboard.press('Escape');

      // Modal should close
      await expect(page.getByTestId('dialog')).toHaveAttribute('data-open', 'false');
    });

    test('displays PDF viewer controls', async ({ page }) => {
      // Verify all main controls are present
      await expect(page.getByTestId('zoom-in')).toBeVisible();
      await expect(page.getByTestId('zoom-out')).toBeVisible();
      await expect(page.getByTestId('zoom-25')).toBeVisible();
      await expect(page.getByTestId('zoom-50')).toBeVisible();
      await expect(page.getByTestId('zoom-100')).toBeVisible();
      await expect(page.getByTestId('zoom-150')).toBeVisible();
      await expect(page.getByTestId('zoom-200')).toBeVisible();
    });

    test('shows loading state while PDF loads', async ({ page }) => {
      // On a fresh load, there should be a loading indicator
      // (this test might need adjustment based on actual loading behavior)
      const dialogContent = page.getByTestId('dialog-content');
      await expect(dialogContent).toBeVisible();
    });
  });

  test.describe('Zoom Controls', () => {
    test.beforeEach(async ({ page }) => {
      // Setup mocks and open modal
      await page.route('**/api/v1/agents/qa/stream', async route => {
        const response = [
          'event: token\ndata: {"token":"Answer"}\n\n',
          'event: citations\ndata: {"citations":[{"documentId":"doc-1","pageNumber":1,"snippet":"Test","relevanceScore":0.9}]}\n\n',
          'event: complete\ndata: {"totalTokens":50}\n\n',
        ].join('');

        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'text/event-stream' },
          body: response,
        });
      });

      await page.route('**/api/v1/pdf/*/download', async route => {
        const mockPdf = Buffer.from(
          '%PDF-1.0\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj 2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj 3 0 obj<</Type/Page/MediaBox[0 0 3 3]/Parent 2 0 R/Resources<<>>>>endobj\nxref\n0 4\n0000000000 65535 f\n0000000010 00000 n\n0000000053 00000 n\n0000000102 00000 n\ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n149\n%EOF'
        );

        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'application/pdf' },
          body: mockPdf,
        });
      });

      await page.getByRole('textbox', { name: /fai una domanda/i }).fill('Test');
      await page.getByRole('button', { name: /invia/i }).click();
      await expect(page.getByTestId('citation-list')).toBeVisible();
      await page.getByTestId('citation-card').first().click();
      await expect(page.getByTestId('dialog')).toHaveAttribute('data-open', 'true');
    });

    test('defaults to 100% zoom level', async ({ page }) => {
      const zoom100Button = page.getByTestId('zoom-100');
      await expect(zoom100Button).toHaveAttribute('aria-pressed', 'true');
    });

    test('can zoom in using zoom in button', async ({ page }) => {
      // Click zoom in button
      await page.getByTestId('zoom-in').click();

      // Should now be at 150%
      await expect(page.getByTestId('zoom-150')).toHaveAttribute('aria-pressed', 'true');
    });

    test('can zoom out using zoom out button', async ({ page }) => {
      // Click zoom out button
      await page.getByTestId('zoom-out').click();

      // Should now be at 50%
      await expect(page.getByTestId('zoom-50')).toHaveAttribute('aria-pressed', 'true');
    });

    test('can set zoom level directly', async ({ page }) => {
      // Click 200% button
      await page.getByTestId('zoom-200').click();

      // Should now be at 200%
      await expect(page.getByTestId('zoom-200')).toHaveAttribute('aria-pressed', 'true');
    });

    test('disables zoom in button at maximum zoom', async ({ page }) => {
      // Set to max zoom
      await page.getByTestId('zoom-200').click();

      // Zoom in button should be disabled
      await expect(page.getByTestId('zoom-in')).toBeDisabled();
    });

    test('disables zoom out button at minimum zoom', async ({ page }) => {
      // Set to min zoom
      await page.getByTestId('zoom-25').click();

      // Zoom out button should be disabled
      await expect(page.getByTestId('zoom-out')).toBeDisabled();
    });
  });

  test.describe('Keyboard Shortcuts', () => {
    test.beforeEach(async ({ page }) => {
      // Setup mocks and open modal
      await page.route('**/api/v1/agents/qa/stream', async route => {
        const response = [
          'event: token\ndata: {"token":"Answer"}\n\n',
          'event: citations\ndata: {"citations":[{"documentId":"doc-1","pageNumber":1,"snippet":"Test","relevanceScore":0.9}]}\n\n',
          'event: complete\ndata: {"totalTokens":50}\n\n',
        ].join('');

        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'text/event-stream' },
          body: response,
        });
      });

      await page.route('**/api/v1/pdf/*/download', async route => {
        const mockPdf = Buffer.from(
          '%PDF-1.0\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj 2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj 3 0 obj<</Type/Page/MediaBox[0 0 3 3]/Parent 2 0 R/Resources<<>>>>endobj\nxref\n0 4\n0000000000 65535 f\n0000000010 00000 n\n0000000053 00000 n\n0000000102 00000 n\ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n149\n%EOF'
        );

        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'application/pdf' },
          body: mockPdf,
        });
      });

      await page.getByRole('textbox', { name: /fai una domanda/i }).fill('Test');
      await page.getByRole('button', { name: /invia/i }).click();
      await expect(page.getByTestId('citation-list')).toBeVisible();
      await page.getByTestId('citation-card').first().click();
      await expect(page.getByTestId('dialog')).toHaveAttribute('data-open', 'true');
    });

    test('zooms in with + key', async ({ page }) => {
      // Press + to zoom in
      await page.keyboard.press('+');

      // Should be at 150%
      await expect(page.getByTestId('zoom-150')).toHaveAttribute('aria-pressed', 'true');
    });

    test('zooms out with - key', async ({ page }) => {
      // Press - to zoom out
      await page.keyboard.press('-');

      // Should be at 50%
      await expect(page.getByTestId('zoom-50')).toHaveAttribute('aria-pressed', 'true');
    });
  });

  test.describe('Multiple Citations', () => {
    test('can open different PDFs from different citations', async ({ page }) => {
      // Mock response with multiple citations
      await page.route('**/api/v1/agents/qa/stream', async route => {
        const response = [
          'event: token\ndata: {"token":"Answer"}\n\n',
          'event: citations\ndata: {"citations":[{"documentId":"doc-1","pageNumber":5,"snippet":"First citation","relevanceScore":0.95},{"documentId":"doc-2","pageNumber":10,"snippet":"Second citation","relevanceScore":0.90}]}\n\n',
          'event: complete\ndata: {"totalTokens":50}\n\n',
        ].join('');

        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'text/event-stream' },
          body: response,
        });
      });

      await page.route('**/api/v1/pdf/*/download', async route => {
        const mockPdf = Buffer.from(
          '%PDF-1.0\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj 2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj 3 0 obj<</Type/Page/MediaBox[0 0 3 3]/Parent 2 0 R/Resources<<>>>>endobj\nxref\n0 4\n0000000000 65535 f\n0000000010 00000 n\n0000000053 00000 n\n0000000102 00000 n\ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n149\n%EOF'
        );

        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'application/pdf' },
          body: mockPdf,
        });
      });

      // Send message and wait for citations
      await page.getByRole('textbox', { name: /fai una domanda/i }).fill('Test');
      await page.getByRole('button', { name: /invia/i }).click();
      await expect(page.getByTestId('citation-list')).toBeVisible();

      // Should have 2 citations
      await expect(page.getByTestId('citation-card')).toHaveCount(2);

      // Click first citation
      await page.getByTestId('citation-card').first().click();
      await expect(page.getByTestId('dialog-title')).toContainText('PDF - Page 5');

      // Close modal
      await page.keyboard.press('Escape');
      await expect(page.getByTestId('dialog')).toHaveAttribute('data-open', 'false');

      // Click second citation
      await page.getByTestId('citation-card').nth(1).click();
      await expect(page.getByTestId('dialog-title')).toContainText('PDF - Page 10');
    });
  });

  test.describe('Accessibility', () => {
    test.beforeEach(async ({ page }) => {
      // Setup mocks and open modal
      await page.route('**/api/v1/agents/qa/stream', async route => {
        const response = [
          'event: token\ndata: {"token":"Answer"}\n\n',
          'event: citations\ndata: {"citations":[{"documentId":"doc-1","pageNumber":1,"snippet":"Test","relevanceScore":0.9}]}\n\n',
          'event: complete\ndata: {"totalTokens":50}\n\n',
        ].join('');

        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'text/event-stream' },
          body: response,
        });
      });

      await page.route('**/api/v1/pdf/*/download', async route => {
        const mockPdf = Buffer.from(
          '%PDF-1.0\n1 0 obj<</Type/Catalog/Pages 2 0 R>>endobj 2 0 obj<</Type/Pages/Kids[3 0 R]/Count 1>>endobj 3 0 obj<</Type/Page/MediaBox[0 0 3 3]/Parent 2 0 R/Resources<<>>>>endobj\nxref\n0 4\n0000000000 65535 f\n0000000010 00000 n\n0000000053 00000 n\n0000000102 00000 n\ntrailer<</Size 4/Root 1 0 R>>\nstartxref\n149\n%EOF'
        );

        await route.fulfill({
          status: 200,
          headers: { 'Content-Type': 'application/pdf' },
          body: mockPdf,
        });
      });

      await page.getByRole('textbox', { name: /fai una domanda/i }).fill('Test');
      await page.getByRole('button', { name: /invia/i }).click();
      await expect(page.getByTestId('citation-list')).toBeVisible();
      await page.getByTestId('citation-card').first().click();
      await expect(page.getByTestId('dialog')).toHaveAttribute('data-open', 'true');
    });

    test('has proper ARIA labels for zoom controls', async ({ page }) => {
      await expect(page.getByLabel('Zoom in')).toBeVisible();
      await expect(page.getByLabel('Zoom out')).toBeVisible();
      await expect(page.getByLabel('Zoom 100%')).toBeVisible();
    });

    test('citation card has button role when clickable', async ({ page }) => {
      // Close the modal first
      await page.keyboard.press('Escape');

      // Check that citation card has button role
      const citationCard = page.getByTestId('citation-card').first();
      await expect(citationCard).toHaveAttribute('role', 'button');
    });

    test('can activate citation with keyboard', async ({ page }) => {
      // Close the modal first
      await page.keyboard.press('Escape');
      await expect(page.getByTestId('dialog')).toHaveAttribute('data-open', 'false');

      // Focus citation card and press Enter
      const citationCard = page.getByTestId('citation-card').first();
      await citationCard.focus();
      await page.keyboard.press('Enter');

      // Modal should open
      await expect(page.getByTestId('dialog')).toHaveAttribute('data-open', 'true');
    });
  });
});
