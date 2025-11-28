/**
 * E2E Tests - Complete Citation Journey (FAST - Mocked APIs)
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

import { test, expect } from '@playwright/test';
import {
  setupCitationTestEnv,
  mockCitationAPI,
  verifyCitationDisplay,
  verifyNoCitations,
  sendQuestionAndWaitForResponse,
  defaultHarmoniesCitation,
  defaultChessCitations,
  CitationResponse,
} from './helpers/citation-test-utils';

test.describe('E2E Citation Journey - Fast (Mocked)', () => {
  test('E2E-1: User uploads PDF, asks question, and sees citation from uploaded PDF', async ({
    page,
  }) => {
    // Setup environment with HARMONIES game
    await setupCitationTestEnv(page, {
      gameId: 'harmonies-1',
    });

    // Mock citation response for HARMONIES setup question
    const citationResponse: CitationResponse = {
      answer:
        'To start playing HARMONIES, place the habitat tiles face up in the center of the table. Each player takes a set of tokens and places them on the starting positions.',
      snippets: [defaultHarmoniesCitation],
      messageId: 'msg-harmonies-setup-123',
      tokenUsage: {
        promptTokens: 150,
        completionTokens: 85,
        totalTokens: 235,
      },
    };
    await mockCitationAPI(page, citationResponse);

    // Navigate to chat page
    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle'); // Double wait pattern from existing tests

    // Send question about game setup
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

    // Verify feedback buttons are present
    await expect(page.getByRole('button', { name: '👍 Utile' })).toBeVisible();
    await expect(page.getByRole('button', { name: '👎 Non utile' })).toBeVisible();
  });

  test('E2E-2: Citation displays correct PDF name and page number', async ({ page }) => {
    await setupCitationTestEnv(page, {
      gameId: 'harmonies-1',
    });

    const citationResponse: CitationResponse = {
      answer: 'Players score points by creating habitats and completing objectives.',
      snippets: [
        {
          text: 'At the end of the game, players score points for each completed habitat and objective card.',
          source: 'HARMONIES_RULES_EN.pdf',
          page: 7,
          line: null,
          documentId: 'doc-harmonies-scoring',
        },
      ],
      messageId: 'msg-scoring-456',
    };
    await mockCitationAPI(page, citationResponse);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');

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
    await setupCitationTestEnv(page, {
      gameId: 'harmonies-1',
    });

    const citationResponse: CitationResponse = {
      answer: 'HARMONIES has multiple game modes: standard, advanced, and solo.',
      snippets: [
        {
          text: 'Standard mode: Play with 2-4 players using the basic rules.',
          source: 'HARMONIES_RULES_EN.pdf',
          page: 2,
          line: null,
        },
        {
          text: 'Advanced mode: Use advanced objective cards for experienced players.',
          source: 'HARMONIES_RULES_EN.pdf',
          page: 9,
          line: null,
        },
        {
          text: 'Solo mode: Play alone against an AI opponent with special rules.',
          source: 'HARMONIES_RULES_EN.pdf',
          page: 12,
          line: null,
        },
      ],
      messageId: 'msg-modes-789',
    };
    await mockCitationAPI(page, citationResponse);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');

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
    await setupCitationTestEnv(page, {
      gameId: 'chess-1',
    });

    const citationResponse: CitationResponse = {
      answer:
        "En passant is a special pawn capture move in chess. It can only occur when a pawn moves two squares forward from its starting position and lands beside an opponent's pawn.",
      snippets: defaultChessCitations,
      messageId: 'msg-enpassant-101',
    };
    await mockCitationAPI(page, citationResponse);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');

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
    await setupCitationTestEnv(page, {
      gameId: 'harmonies-1',
    });

    // Mock response with "Not specified" and empty snippets
    const citationResponse: CitationResponse = {
      answer: 'Not specified',
      snippets: [],
      messageId: 'msg-notspecified-202',
      tokenUsage: {
        promptTokens: 120,
        completionTokens: 5,
        totalTokens: 125,
      },
    };
    await mockCitationAPI(page, citationResponse);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');

    await sendQuestionAndWaitForResponse(page, 'Quanto costa il gioco?', 'Not specified');

    // Verify NO citations section is shown
    await verifyNoCitations(page);

    // Verify feedback buttons are still present
    await expect(page.getByRole('button', { name: '👍 Utile' })).toBeVisible();
    await expect(page.getByRole('button', { name: '👎 Non utile' })).toBeVisible();
  });

  test('E2E-6: Citation without page number displays correctly', async ({ page }) => {
    await setupCitationTestEnv(page, {
      gameId: 'harmonies-1',
    });

    const citationResponse: CitationResponse = {
      answer: 'Here is information from a text file without page numbers.',
      snippets: [
        {
          text: 'Some text from a source without page numbers.',
          source: 'harmonies-faq.txt',
          page: null, // No page number
          line: null,
        },
      ],
      messageId: 'msg-nopage-303',
    };
    await mockCitationAPI(page, citationResponse);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');

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
    await setupCitationTestEnv(page, {
      gameId: 'harmonies-1',
    });

    const citationResponse: CitationResponse = {
      answer: 'Answer with collapsible citations.',
      snippets: [defaultHarmoniesCitation],
      messageId: 'msg-collapsible-404',
    };
    await mockCitationAPI(page, citationResponse);

    await page.goto('/chat');
    await page.waitForLoadState('networkidle');
    await page.waitForLoadState('networkidle');

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
