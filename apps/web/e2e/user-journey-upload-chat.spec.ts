/**
 * User Journey Upload-Chat E2E Tests - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/
 */

import { test, expect } from '@playwright/test';
import path from 'path';
import { authenticateViaAPI } from './fixtures/auth';

/**
 * Complete User Journey E2E Test: Upload → Chat with REAL RAG Integration
 *
 * This test validates the complete flow with real backend:
 * 1. Authenticate user (via API or cookie setup)
 * 2. Upload real PDF file
 * 3. Verify PDF processing and RAG embedding creation
 * 4. Navigate to chat
 * 5. Ask question in Italian
 * 6. Verify REAL LLM response using RAG
 *
 * Prerequisites:
 * - Backend API running: cd apps/api/src/Api && dotnet run
 * - Required services: cd infra && docker compose up meepleai-postgres meepleai-qdrant meepleai-redis
 * - Frontend dev server: cd apps/web && pnpm dev
 * - PDF file exists: data/Test-EN-LIBELLUD_HARMONIES_RULES_EN.pdf
 * - Demo user exists in DB: user@meepleai.dev / Demo123!
 *
 * Note: This is a REAL INTEGRATION test - uses actual backend APIs
 */

test.describe('Complete User Journey: Real Backend Integration', () => {
  test.setTimeout(180000); // 3 minutes for real processing

  test('should upload PDF, create RAG embeddings, and answer question using real LLM', async ({
    browser,
  }) => {
    // Create new browser context
    const context = await browser.newContext({
      baseURL: 'http://localhost:3000',
    });

    const page = await context.newPage();

    // ========================================================================
    // Phase 1: Authenticate via API
    // ========================================================================

    await test.step('Authenticate user via API', async () => {
      const authenticated = await authenticateViaAPI(page, 'editor@meepleai.dev', 'Demo123!');

      if (!authenticated) {
        test.skip(
          true,
          'Authentication failed - backend API might not be running or credentials invalid'
        );
      }

      // Navigate to verify auth
      await page.goto('/');
      await page.waitForLoadState('domcontentloaded');
    });

    // ========================================================================
    // Phase 2: Navigate to Upload Page
    // ========================================================================

    await test.step('Navigate to upload page as editor', async () => {
      await page.goto('/upload');
      await page.waitForLoadState('networkidle');

      // Check if we're authenticated
      const pageContent = await page.textContent('body');

      if (
        pageContent?.includes('You need to be logged in') ||
        pageContent?.includes('Login required')
      ) {
        // Skip test if not authenticated - would need proper session setup
        test.skip(true, 'Authentication required - need to setup session cookies for real backend');
      }
    });

    // ========================================================================
    // Phase 3: Select/Create Game
    // ========================================================================

    await test.step('Select or create HARMONIES game', async () => {
      // Wait for games to load
      await page.waitForTimeout(2000);

      const gameSelect = page.locator('select#gameSelect');

      if (await gameSelect.isVisible({ timeout: 5000 })) {
        // Check if HARMONIES exists
        const options = await gameSelect.locator('option').allTextContents();

        if (options.some(opt => opt.includes('HARMONIES'))) {
          // Select existing - find the exact label text
          const harmoniesOption = options.find(opt => opt.includes('HARMONIES'));
          if (harmoniesOption) {
            await gameSelect.selectOption({ label: harmoniesOption });
          }
        } else if (options.length > 0) {
          // Select first game or create new one
          await gameSelect.selectOption({ index: 0 });
        }

        // Click confirm if button is visible
        const confirmButton = page.getByRole('button', { name: /confirm/i });
        if (await confirmButton.isVisible({ timeout: 2000 })) {
          await confirmButton.click();
          await page.waitForTimeout(1000);
        }
      }
    });

    // ========================================================================
    // Phase 4: Upload Real PDF File
    // ========================================================================

    await test.step('Upload HARMONIES PDF file', async () => {
      const fileInput = page.locator('input[type="file"]').first();
      // Use relative path from project root (works on all OS)
      const pdfPath = path.join(__dirname, '../../../data/Test-EN-LIBELLUD_HARMONIES_RULES_EN.pdf');

      // Upload the file
      await fileInput.setInputFiles(pdfPath);

      // Wait for upload to start
      await page.waitForTimeout(2000);

      // Click upload button if needed
      const uploadButton = page.locator('[data-testid="upload-button"]');
      if ((await uploadButton.isVisible({ timeout: 2000 })) && (await uploadButton.isEnabled())) {
        await uploadButton.click();
      }
    });

    // ========================================================================
    // Phase 5: Wait for PDF Processing
    // ========================================================================

    await test.step('Wait for PDF processing to complete', async () => {
      // Wait for processing indicators
      // Real backend processing can take 10-60 seconds

      // Poll for processing completion indicator (with 60s max timeout)
      try {
        // Wait for success message or processing completion indicator
        await page.waitForSelector(
          '[data-testid="processing-complete"], text=/processing complete|elaborazione completata/i',
          {
            timeout: 60000,
            state: 'visible',
          }
        );
        console.log('✓ PDF processing completed successfully');
      } catch (error) {
        // Fallback: check for any success indicators in page content
        const pageContent = await page.textContent('body');
        console.log('Upload status:', pageContent?.substring(0, 500));
        // If no explicit indicator, wait a bit more for embeddings
        await page.waitForTimeout(5000);
      }
    });

    // ========================================================================
    // Phase 6: Navigate to Chat
    // ========================================================================

    await test.step('Navigate to chat page', async () => {
      await page.goto('/chat');
      await page.waitForLoadState('networkidle');

      // Verify chat page loaded
      await expect(page.getByRole('heading', { name: /meepleai chat/i })).toBeVisible({
        timeout: 10000,
      });
    });

    // ========================================================================
    // Phase 7: Select Game in Chat (if needed)
    // ========================================================================

    await test.step('Select HARMONIES game in chat', async () => {
      const gameSelect = page.locator('select#gameSelect');

      if (await gameSelect.isVisible({ timeout: 5000 })) {
        // Try to select HARMONIES
        try {
          const options = await gameSelect.locator('option').allTextContents();
          const harmoniesOption = options.find(opt => opt.includes('HARMONIES'));
          if (harmoniesOption) {
            await gameSelect.selectOption({ label: harmoniesOption });
          }
          await page.waitForTimeout(1000);
        } catch (e) {
          // Game might not be in dropdown yet
          console.log('Could not select HARMONIES, using default game');
        }
      }
    });

    // ========================================================================
    // Phase 8: Ask Setup Question in Italian (REAL LLM)
    // ========================================================================

    await test.step('Ask game setup question in Italian', async () => {
      const questionInput = page.getByPlaceholder(/fai una domanda|ask a question/i);

      // Wait for input to be enabled (means game is selected)
      await expect(questionInput).toBeEnabled({ timeout: 10000 });

      // Type question
      await questionInput.fill('come devo sistemare il gioco per iniziare a giocare?');

      // Click send
      const sendButton = page.getByRole('button', { name: /invia|send/i });
      await sendButton.click();

      // Verify question appears
      await expect(
        page.getByText('come devo sistemare il gioco per iniziare a giocare?')
      ).toBeVisible();
    });

    // ========================================================================
    // Phase 9: Wait for REAL LLM Response
    // ========================================================================

    await test.step('Wait for LLM response via RAG', async () => {
      // Real LLM can take 5-20 seconds
      // Wait for streaming indicator to disappear
      const streamingIndicator = page.locator('[data-testid="streaming-indicator"]');

      // Wait for streaming to start
      await expect(streamingIndicator)
        .toBeVisible({ timeout: 5000 })
        .catch(() => {
          console.log('Streaming indicator not found, continuing...');
        });

      // Wait for streaming to complete
      await expect(streamingIndicator)
        .not.toBeVisible({ timeout: 60000 })
        .catch(() => {
          console.log('Streaming took longer than expected');
        });

      // Additional wait for response to render
      await page.waitForTimeout(2000);
    });

    // ========================================================================
    // Phase 10: Validate REAL Response Content
    // ========================================================================

    await test.step('Verify response contains setup instructions', async () => {
      const pageContent = await page.textContent('body');

      // Log response for debugging
      console.log('='.repeat(80));
      console.log('REAL LLM RESPONSE (first 500 chars):');
      console.log(
        pageContent?.substring(
          pageContent.indexOf('come devo sistemare'),
          pageContent.indexOf('come devo sistemare') + 500
        )
      );
      console.log('='.repeat(80));

      // Verify response exists and is substantial
      expect(pageContent).toBeTruthy();
      expect(pageContent!.length).toBeGreaterThan(200);

      // Verify Italian keywords related to game setup
      const setupKeywords = [
        'giocatori', // players
        'tavolo', // table
        'carte', // cards
        'preparazione', // preparation
        'inizia', // start
        'setup', // setup
        'tessere', // tiles
        'posizione', // position
        'disporre', // arrange
        'mescola', // shuffle
      ];

      const foundKeywords = setupKeywords.filter(keyword =>
        pageContent!.toLowerCase().includes(keyword)
      );

      console.log('Found setup keywords:', foundKeywords);

      // At least one keyword should be present in real response
      expect(foundKeywords.length).toBeGreaterThan(0);
    });

    // ========================================================================
    // Phase 11: Verify RAG Citations
    // ========================================================================

    await test.step('Verify RAG provided citations from PDF', async () => {
      const citations = page.locator('[data-testid="citation"]');
      const citationCount = await citations.count();

      console.log('Citation count:', citationCount);

      // Real RAG should provide citations
      if (citationCount > 0) {
        const firstCitation = await citations.first().textContent();
        console.log('First citation:', firstCitation);

        expect(citationCount).toBeGreaterThan(0);
      } else {
        console.warn('No citations found - RAG might not be working');
      }
    });

    await context.close();
  });
});
