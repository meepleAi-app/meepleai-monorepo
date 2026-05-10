/**
 * Smoke E2E helpers — login + navigate + mock SSE V2 + ownership mocks.
 *
 * NB: il mock SSE qui usa il formato V2 `data: {"type": 7, "data": ...}`
 * (NON il formato V1 `event: token` di ChatHelper.mockQAStreamWithCitations
 * — quel pattern è incompatibile col parser qaStream V2 in chatClient.ts:399).
 *
 * Spec: docs/superpowers/specs/2026-05-10-e2e-smoke-game-night-design.md
 */
import type { Page, Route } from '@playwright/test';

import { SMOKE_USER } from './_helpers.fixtures';

interface MockCitation {
  documentId: string;
  pageNumber: number;
  snippet: string;
  relevanceScore: number;
  copyrightTier?: 'full' | 'protected';
}

interface MockQaV2Options {
  tokens: string[];
  citations?: MockCitation[];
  confidence?: number;
  chatThreadId?: string;
}

/**
 * Login smoke-aaron via API endpoint diretto (più veloce di UI form).
 * Usa POST /api/v1/auth/login per ottenere session cookie.
 */
export async function loginSmokeAaron(page: Page): Promise<void> {
  const response = await page.request.post('/api/v1/auth/login', {
    data: {
      email: SMOKE_USER.email,
      password: SMOKE_USER.password,
    },
  });
  if (!response.ok()) {
    throw new Error(`Login failed: HTTP ${response.status()} — ${await response.text()}`);
  }
  // Cookie session is now set on page context
}

/**
 * Mock SSE V2 stream — formato `{type, data}` come usato da chatClient.ts qaStream.
 *
 * type=7 → Token (string accumulated in buffer da useGameChat)
 * type=4 → Complete (confidence + Citations + chatThreadId)
 *
 * NB: parser qaStream estrae solo le linee `data:` (ignora `event:`).
 * Il payload JSON DEVE contenere il campo `type` numerico.
 */
export async function mockQaStreamV2(page: Page, options: MockQaV2Options): Promise<void> {
  const { tokens, citations = [], confidence = 0.9, chatThreadId } = options;

  let sseBody = '';

  // Stream token events (type=7, data=string)
  for (const token of tokens) {
    sseBody += `data: ${JSON.stringify({ type: 7, data: token })}\n\n`;
  }

  // Complete event (type=4, data=StreamingComplete with PascalCase Citations)
  const completePayload: Record<string, unknown> = {
    estimatedReadingTimeMinutes: 0,
    promptTokens: 10,
    completionTokens: tokens.length * 5,
    totalTokens: tokens.length * 5 + 10,
    confidence,
    Citations: citations,
  };
  if (chatThreadId) completePayload.chatThreadId = chatThreadId;
  sseBody += `data: ${JSON.stringify({ type: 4, data: completePayload })}\n\n`;

  await page.route('**/api/v1/agents/qa/stream', async (route: Route) => {
    await route.fulfill({
      status: 200,
      headers: {
        'Content-Type': 'text/event-stream',
        'Cache-Control': 'no-cache',
      },
      body: sseBody,
    });
  });
}

/**
 * Mock empty getThreadsByGame (forces no historical messages — fresh chat).
 * Used by edge case specs to isolate single message flow without G2 hydrate noise.
 */
export async function mockNoChatHistory(page: Page, gameId: string): Promise<void> {
  await page.route(`**/api/v1/chat-threads*`, async (route: Route) => {
    const url = route.request().url();
    if (url.includes(`gameId=${gameId}`)) {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    } else {
      await route.continue();
    }
  });
}

/**
 * Mock empty getGameDocuments (forces non-owner state for G4 ownership gate).
 * useCanViewPdf vede documents=[] → canView=false → CitationOwnershipUpsell.
 */
export async function mockNoDocuments(page: Page, gameId: string): Promise<void> {
  await page.route(
    `**/api/v1/knowledge-base/${gameId}/documents`,
    async (route: Route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    }
  );
}
