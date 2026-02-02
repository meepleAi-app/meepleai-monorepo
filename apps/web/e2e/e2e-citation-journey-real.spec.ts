/**
 * E2E Citation Journey (Real Backend) - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/
 */

/**
 * E2E Tests - Complete Citation Journey (REAL - Integration)
 *
 * Issue #1018: [BGAI-080] End-to-end testing (question → PDF citation)
 *
 * Purpose: Comprehensive integration test validating REAL RAG pipeline
 * with actual backend services (Postgres, Qdrant, Redis, API, LLM).
 *
 * Implementation Strategy: OPTION 2 (Hybrid Suite)
 * - Companion file: Fast mocked tests (CI-friendly)
 * - This file: REAL backend integration (on-demand, @slow tag)
 *
 * Test Coverage:
 * 1. Full journey: Upload PDF → RAG processing → ask question → verify citation
 * 2. Citation traceability: Verify citation links to uploaded PDF (documentId matching)
 *
 * Prerequisites:
 * - Backend API running: cd apps/api/src/Api && dotnet run
 * - Required services: cd infra && docker compose up meepleai-postgres meepleai-qdrant meepleai-redis
 * - Frontend dev server: cd apps/web && pnpm dev
 * - PDF file exists: data/Test-EN-LIBELLUD_HARMONIES_RULES_EN.pdf
 * - Demo user exists in DB: editor@meepleai.dev / Demo123!
 *
 * Usage:
 * - Skip in CI: Test is tagged @slow
 * - Run locally: pnpm test:e2e --grep "@slow"
 * - Run before merge: Validate real RAG behavior
 *
 * Dependencies:
 * - citation-test-utils.ts: Shared helper functions
 * - user-journey-upload-chat.spec.ts: Real backend patterns
 *
 * Related:
 * - Issue #1009: Q&A interface E2E tests
 * - Issue #978: RAG validation pipeline integration tests
 * - BGAI E2E validation: Real PDF processing tests
 *
 * Performance:
 * - Expected duration: 3-5 minutes per test
 * - LLM API calls: Real OpenRouter usage (cost consideration)
 * - Services: Requires full stack running
 */

import path from 'path';

import { test, expect } from './fixtures';
import { authenticateViaAPI } from './fixtures/auth';
import {
  uploadPdfAndWaitForProcessing,
  navigateToChatAndSelectGame,
  sendQuestionAndWaitForResponse,
} from './helpers/citation-test-utils';
import { WaitHelper } from './helpers/WaitHelper';

test.describe.serial('E2E Citation Journey - Real Backend Integration (@slow)', () => {
  test.setTimeout(300000); // 5 minutes for real processing

  // Skip in CI environment
  test.skip(process.env.CI === 'true', 'Skipping slow tests in CI');

  const uploadedDocumentId: string | null = null;
  let uploadedPdfName: string | null = null;

  test('@slow REAL-1: Upload HARMONIES PDF, process with RAG, ask question, verify citation from uploaded PDF', async ({
    browser,
  }) => {
    // Create new browser context for isolation
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

      await page.goto('/');
      await page.waitForLoadState('domcontentloaded');
    });

    // ========================================================================
    // Phase 2: Upload HARMONIES PDF
    // ========================================================================

    await test.step('Upload HARMONIES PDF and wait for processing', async () => {
      const pdfPath = path.join(__dirname, '../../../data/Test-EN-LIBELLUD_HARMONIES_RULES_EN.pdf');
      uploadedPdfName = 'Test-EN-LIBELLUD_HARMONIES_RULES_EN.pdf';

      await uploadPdfAndWaitForProcessing(page, pdfPath, 'HARMONIES');

      // Extract document ID from upload response (if available)
      // This would require inspecting network responses or DOM elements
      // For now, we'll verify via citation later
      console.log('✓ PDF uploaded and processed');
    });

    // ========================================================================
    // Phase 3: Navigate to Chat and Select HARMONIES
    // ========================================================================

    await test.step('Navigate to chat and select HARMONIES game', async () => {
      await navigateToChatAndSelectGame(page, 'HARMONIES');
    });

    // ========================================================================
    // Phase 4: Ask Setup Question in Italian
    // ========================================================================

    await test.step('Ask game setup question in Italian', async () => {
      await sendQuestionAndWaitForResponse(
        page,
        'come devo sistemare il gioco per iniziare a giocare?',
        'habitat' // Expected keyword in REAL LLM response
      );
    });

    // ========================================================================
    // Phase 5: Wait for REAL LLM Response with Citations
    // ========================================================================

    await test.step('Wait for LLM response via RAG', async () => {
      // Wait for streaming to complete
      const streamingIndicator = page.locator('[data-testid="streaming-indicator"]');

      // Wait for streaming to start (if visible)
      await streamingIndicator.waitFor({ state: 'visible', timeout: 5000 }).catch(() => {
        console.log('Streaming indicator not found, continuing...');
      });

      // Wait for streaming to complete
      await streamingIndicator.waitFor({ state: 'hidden', timeout: 60000 }).catch(() => {
        console.log('Streaming took longer than expected');
      });

      // Additional wait for response to fully render
      const waitHelper = new WaitHelper(page);
      await waitHelper.waitForNetworkIdle(5000);
    });

    // ========================================================================
    // Phase 6: Verify Citations from Uploaded PDF
    // ========================================================================

    await test.step('Verify citations appear and link to uploaded PDF', async () => {
      // Verify citations section is visible
      await expect(page.getByText('Fonti:')).toBeVisible({ timeout: 10000 });

      // Get citation count
      const citations = page.locator('[data-testid="citation-card"]');
      const citationCount = await citations.count();

      console.log(`Found ${citationCount} citations`);

      if (citationCount === 0) {
        test.fail(true, 'No citations found - RAG might not be working properly');
      }

      // Verify at least one citation
      expect(citationCount).toBeGreaterThan(0);

      // Verify first citation shows uploaded PDF name
      const firstCitation = citations.first();
      const citationText = await firstCitation.textContent();

      console.log('First citation:', citationText);

      // Verify citation contains PDF name (case-insensitive, flexible matching)
      const pdfNamePattern = /harmonies|rules/i;
      expect(citationText).toMatch(pdfNamePattern);

      // Verify citation has page number
      expect(citationText).toMatch(/pagina|page|pag/i);
    });

    // ========================================================================
    // Phase 7: Verify REAL Response Content (Italian)
    // ========================================================================

    await test.step('Verify response contains setup instructions in Italian', async () => {
      const pageContent = await page.textContent('body');

      // Log response snippet for debugging
      const questionIndex = pageContent!.indexOf('come devo sistemare');
      if (questionIndex !== -1) {
        console.log('='.repeat(80));
        console.log('REAL LLM RESPONSE (first 500 chars after question):');
        console.log(pageContent!.substring(questionIndex, questionIndex + 500));
        console.log('='.repeat(80));
      }

      // Verify response is substantial
      expect(pageContent).toBeTruthy();
      expect(pageContent!.length).toBeGreaterThan(200);

      // Verify Italian or board game setup keywords
      const setupKeywords = [
        'giocatori', // players
        'tavolo', // table
        'carte', // cards
        'habitat', // habitat (game-specific)
        'tessere', // tiles
        'posizione', // position
        'inizia', // start
        'setup', // setup (English acceptable)
        'preparation', // preparation
        'place', // place (English acceptable)
      ];

      const foundKeywords = setupKeywords.filter(keyword =>
        pageContent!.toLowerCase().includes(keyword)
      );

      console.log('Found setup keywords:', foundKeywords);

      // At least 2 keywords should be present in real response
      expect(foundKeywords.length).toBeGreaterThanOrEqual(2);
    });

    await context.close();
  });

  test('@slow REAL-2: Verify citation traceability (documentId matches uploaded PDF)', async ({
    browser,
  }) => {
    // This test would require backend API integration to:
    // 1. Query uploaded PDF documentId
    // 2. Compare with citation documentId from RAG response
    //
    // For MVP/Alpha, we validate via UI (REAL-1 test)
    // Future enhancement: Add backend API verification

    test.skip(true, 'Citation traceability via backend API - deferred to future sprint');

    // Placeholder for future implementation:
    // const context = await browser.newContext({ baseURL: 'http://localhost:3000' });
    // const page = await context.newPage();
    //
    // // 1. Authenticate
    // await authenticateViaAPI(page, 'editor@meepleai.dev', 'Demo123!');
    //
    // // 2. Query backend for uploaded PDF documentId
    // const response = await page.request.get('/api/v1/documents?game=HARMONIES');
    // const documents = await response.json();
    // const uploadedDoc = documents.find(d => d.filename.includes('HARMONIES'));
    // const documentId = uploadedDoc?.id;
    //
    // // 3. Ask question and capture citation response
    // await navigateToChatAndSelectGame(page, 'HARMONIES');
    // // ... intercept /api/v1/agents/qa response
    //
    // // 4. Verify citation.documentId === uploadedDoc.id
    // expect(citationDocumentId).toBe(documentId);
    //
    // await context.close();
  });
});
