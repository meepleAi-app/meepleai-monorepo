/**
 * E2E Tests - Complete Citation Journey (FAST - Mocked APIs) - MIGRATED TO POM
 *
 * @see apps/web/e2e/pages/
 *
 * Issue #1018: [BGAI-080] End-to-end testing (question → PDF citation)
 *
 * Purpose: Fast CI-friendly tests validating the complete user journey
 * from asking a question to seeing citations from PDFs.
 *
 * Implementation Strategy: OPTION 2 (Hybrid Suite)
 * - This file: MOCKED APIs for fast, reliable CI execution (~30s)
 * - Companion file: Real backend integration (@slow tag, on-demand)
 *
 * KNOWN ISSUE: Tests currently skipped due to incomplete citation UI implementation
 * - Input remains disabled after page load
 * - Auto-selection of game/agent not triggering properly with mocked APIs
 * - Citation display components may not be fully integrated
 *
 * TODO: Un-skip tests once citation UI is fully implemented and integrated
 *
 * Test Coverage:
 * 1. Upload → question → citation display (happy path)
 * 2. Citation shows correct PDF name and page number
 * 3. Multiple citations from same PDF
 * 4. Multiple citations from different PDFs
 * 5. No citations when answer is "Not specified"
 *
 * Dependencies:
 * - citation-test-utils.ts: Shared helper functions
 * - qa-test-utils.ts: Authentication and API mocking
 *
 * Related:
 * - Issue #1009: Q&A interface E2E tests (51 tests)
 * - Issue #978: RAG validation pipeline integration tests
 * - ai04-qa-snippets.spec.ts: Q&A with snippets tests
 * - chat-citations.spec.ts: Citation display tests
 */

import { test, expect } from './fixtures/chromatic';
import { setupAuthRoutes, waitForAutoSelection } from './helpers/qa-test-utils';
import {
  verifyCitationDisplay,
  verifyNoCitations,
  defaultHarmoniesCitation,
  defaultChessCitations,
  mockQAStreamingAPI,
  SSEStreamEvent,
  setupCitationTestEnv,
  sendQuestionAndWaitForResponse,
  CitationResponse,
  mockCitationAPI,
} from './helpers/citation-test-utils';

/**
 * FIXED (Issue #1805): Mobile skip removed - SSE streaming now properly mocked
 *
 * Previous Issue: Tests skipped on mobile due to incomplete SSE mock
 * Solution: Centralized mockQAStreamingAPI helper with full SSE format compliance
 */
test.describe('E2E Citation Journey - Fast (Mocked)', () => {
  test('E2E-1: User uploads PDF, asks question, and sees citation from uploaded PDF', async ({
    page,
  }) => {
    // Setup test environment with centralized helper
    await setupCitationTestEnv(page, { gameId: 'harmonies-1' });

    // Mock SSE streaming with proper format (Issue #1805 fix)
    const sseEvents: SSEStreamEvent[] = [
      { type: 'stateUpdate', data: { state: 'Searching...' } },
      { type: 'token', data: { token: 'To ' } },
      { type: 'token', data: { token: 'start ' } },
      { type: 'token', data: { token: 'playing ' } },
      { type: 'token', data: { token: 'HARMONIES' } },
      { type: 'token', data: { token: ', place the habitat tiles' } },
      {
        type: 'citation',
        data: {
          text: defaultHarmoniesCitation.text,
          source: defaultHarmoniesCitation.source,
          page: defaultHarmoniesCitation.page,
          line: defaultHarmoniesCitation.line,
          documentId: defaultHarmoniesCitation.documentId,
        },
      },
      { type: 'complete', data: { confidence: 0.85 } },
    ];
    await mockQAStreamingAPI(page, sseEvents);

    // Navigate and send question
    await page.goto('/chat');
    await sendQuestionAndWaitForResponse(
      page,
      'Come devo sistemare il gioco per iniziare a giocare?',
      'To start playing HARMONIES'
    );

    // Verify citation is displayed with correct information
    await verifyCitationDisplay(page, [
      {
        source: 'HARMONIES_RULES_EN.pdf',
        page: 3,
        text: 'Place the habitat tiles face up in the center of the table',
      },
    ]);
  });

  test('E2E-2: Citation displays correct PDF name and page number', async ({ page }) => {
    await setupCitationTestEnv(page, { gameId: 'harmonies-1' });

    // Mock SSE with single citation
    const sseEvents: SSEStreamEvent[] = [
      { type: 'stateUpdate', data: { state: 'Searching...' } },
      {
        type: 'token',
        data: { token: 'Players score points by creating habitats and completing objectives.' },
      },
      {
        type: 'citation',
        data: {
          text: 'At the end of the game, players score points for each completed habitat and objective card.',
          source: 'HARMONIES_RULES_EN.pdf',
          page: 7,
          line: null,
          documentId: 'doc-harmonies-scoring',
        },
      },
      { type: 'complete', data: { confidence: 0.85 } },
    ];
    await mockQAStreamingAPI(page, sseEvents);

    await page.goto('/chat');
    await sendQuestionAndWaitForResponse(
      page,
      'Come si calcolano i punti?',
      'Players score points by creating habitats'
    );

    // Verify citation header shows "Fonti: (1)"
    await expect(page.getByText('📚 Fonti (1)')).toBeVisible();

    // Verify citation card shows source with page number
    await expect(page.getByText('HARMONIES_RULES_EN.pdf (Pagina 7)')).toBeVisible();

    // Verify citation text is displayed
    await expect(
      page.getByText(/At the end of the game, players score points for each completed/)
    ).toBeVisible();
  });

  test('E2E-3: Multiple citations from same PDF are displayed correctly', async ({ page }) => {
    await setupCitationTestEnv(page, { gameId: 'harmonies-1' });

    // Mock SSE with multiple citations from same PDF
    const sseEvents: SSEStreamEvent[] = [
      { type: 'stateUpdate', data: { state: 'Searching...' } },
      {
        type: 'token',
        data: { token: 'HARMONIES has multiple game modes: standard, advanced, and solo.' },
      },
      {
        type: 'citation',
        data: {
          text: 'Standard mode: Play with 2-4 players using the basic rules.',
          source: 'HARMONIES_RULES_EN.pdf',
          page: 2,
          line: null,
        },
      },
      {
        type: 'citation',
        data: {
          text: 'Advanced mode: Use advanced objective cards for experienced players.',
          source: 'HARMONIES_RULES_EN.pdf',
          page: 9,
          line: null,
        },
      },
      {
        type: 'citation',
        data: {
          text: 'Solo mode: Play alone against an AI opponent with special rules.',
          source: 'HARMONIES_RULES_EN.pdf',
          page: 12,
          line: null,
        },
      },
      { type: 'complete', data: { confidence: 0.85 } },
    ];
    await mockQAStreamingAPI(page, sseEvents);

    await page.goto('/chat');
    await sendQuestionAndWaitForResponse(
      page,
      'Quali sono le modalità di gioco?',
      'HARMONIES has multiple game modes'
    );

    // Verify citation count shows 3
    await expect(page.getByText('📚 Fonti (3)')).toBeVisible();

    // Verify all three citations from same PDF with different pages
    await verifyCitationDisplay(page, [
      {
        source: 'HARMONIES_RULES_EN.pdf',
        page: 2,
        text: 'Standard mode: Play with 2-4 players',
      },
      {
        source: 'HARMONIES_RULES_EN.pdf',
        page: 9,
        text: 'Advanced mode: Use advanced objective cards',
      },
      {
        source: 'HARMONIES_RULES_EN.pdf',
        page: 12,
        text: 'Solo mode: Play alone against an AI opponent',
      },
    ]);
  });

  test('E2E-4: Multiple citations from different PDFs are displayed correctly', async ({
    page,
  }) => {
    await setupCitationTestEnv(page, { gameId: 'chess-1' });

    // Mock SSE with citations from different PDFs
    const sseEvents: SSEStreamEvent[] = [
      { type: 'stateUpdate', data: { state: 'Searching...' } },
      {
        type: 'token',
        data: {
          token:
            "En passant is a special pawn capture move in chess. It can only occur when a pawn moves two squares forward from its starting position and lands beside an opponent's pawn.",
        },
      },
      {
        type: 'citation',
        data: {
          text: defaultChessCitations[0].text,
          source: defaultChessCitations[0].source,
          page: defaultChessCitations[0].page,
          line: defaultChessCitations[0].line,
          documentId: defaultChessCitations[0].documentId,
        },
      },
      {
        type: 'citation',
        data: {
          text: defaultChessCitations[1].text,
          source: defaultChessCitations[1].source,
          page: defaultChessCitations[1].page,
          line: defaultChessCitations[1].line,
          documentId: defaultChessCitations[1].documentId,
        },
      },
      { type: 'complete', data: { confidence: 0.85 } },
    ];
    await mockQAStreamingAPI(page, sseEvents);

    await page.goto('/chat');
    await sendQuestionAndWaitForResponse(
      page,
      'What is en passant in chess?',
      'En passant is a special pawn capture move'
    );

    // Verify citation count shows 2
    await expect(page.getByText('📚 Fonti (2)')).toBeVisible();

    // Verify citations from two different PDFs
    await verifyCitationDisplay(page, [
      {
        source: 'chess-rules.pdf',
        page: 12,
        text: "A pawn attacking a square crossed by an opponent's pawn",
      },
      {
        source: 'chess-advanced-tactics.pdf',
        page: 45,
        text: 'The en passant capture must be made immediately',
      },
    ]);

    // Verify both PDF names are visible
    await expect(page.getByText('chess-rules.pdf (Pagina 12)')).toBeVisible();
    await expect(page.getByText('chess-advanced-tactics.pdf (Pagina 45)')).toBeVisible();
  });

  test('E2E-5: No citations shown when answer is "Not specified"', async ({ page }) => {
    await setupCitationTestEnv(page, { gameId: 'harmonies-1' });

    // Mock SSE with "Not specified" (no citations)
    const sseEvents: SSEStreamEvent[] = [
      { type: 'stateUpdate', data: { state: 'Searching...' } },
      { type: 'token', data: { token: 'Not specified' } },
      { type: 'complete', data: { confidence: 0.5 } }, // Low confidence, no citations
    ];
    await mockQAStreamingAPI(page, sseEvents);

    await page.goto('/chat');
    await sendQuestionAndWaitForResponse(page, 'Quanto costa il gioco?', 'Not specified');

    // Verify NO citations section is shown
    await verifyNoCitations(page);
  });

  test('E2E-6: Citation without page number displays correctly', async ({ page }) => {
    await setupCitationTestEnv(page, { gameId: 'harmonies-1' });

    // Mock SSE with citation without page number
    const sseEvents: SSEStreamEvent[] = [
      { type: 'stateUpdate', data: { state: 'Searching...' } },
      {
        type: 'token',
        data: { token: 'Here is information from a text file without page numbers.' },
      },
      {
        type: 'citation',
        data: {
          text: 'Some text from a source without page numbers.',
          source: 'harmonies-faq.txt',
          page: null, // No page number
          line: null,
        },
      },
      { type: 'complete', data: { confidence: 0.85 } },
    ];
    await mockQAStreamingAPI(page, sseEvents);

    await page.goto('/chat');
    await sendQuestionAndWaitForResponse(
      page,
      'Test question',
      'Here is information from a text file'
    );

    // Verify citation displays source without page number
    await expect(page.getByText('harmonies-faq.txt')).toBeVisible();

    // Verify no "Pagina X" is shown
    await expect(page.getByText(/harmonies-faq\.txt \(Pagina/)).not.toBeVisible();

    // Verify citation text is shown
    await expect(page.getByText('Some text from a source without page numbers.')).toBeVisible();
  });

  test('E2E-7: Citation section is collapsible', async ({ page }) => {
    await setupCitationTestEnv(page, { gameId: 'harmonies-1' });

    // Mock SSE with citation (for collapsible test)
    const sseEvents: SSEStreamEvent[] = [
      { type: 'stateUpdate', data: { state: 'Searching...' } },
      { type: 'token', data: { token: 'Answer with collapsible citations.' } },
      {
        type: 'citation',
        data: {
          text: defaultHarmoniesCitation.text,
          source: defaultHarmoniesCitation.source,
          page: defaultHarmoniesCitation.page,
          line: defaultHarmoniesCitation.line,
          documentId: defaultHarmoniesCitation.documentId,
        },
      },
      { type: 'complete', data: { confidence: 0.85 } },
    ];
    await mockQAStreamingAPI(page, sseEvents);

    await page.goto('/chat');
    await sendQuestionAndWaitForResponse(
      page,
      'Test collapsible',
      'Answer with collapsible citations'
    );

    // Verify citations are initially visible
    const citationsContent = page.getByTestId('citations-content');
    await expect(citationsContent).toBeVisible({ timeout: 10000 });

    // Click header to collapse
    const citationsHeader = page.getByTestId('citations-header');
    await citationsHeader.click();

    // Verify citations are hidden
    await expect(citationsContent).not.toBeVisible();

    // Click again to expand
    await citationsHeader.click();

    // Verify citations are visible again
    await expect(citationsContent).toBeVisible();
  });
});
