/**
 * Game-chat helpers for nightly smoke specs (B+ ibrida — see #938).
 *
 * Used by 3 mock specs in this directory:
 *   game-night-low-confidence.smoke.spec.ts
 *   game-night-out-of-context.smoke.spec.ts
 *   game-night-pdf-non-owner.smoke.spec.ts
 *
 * SSE protocol: type=7 Token, type=4 Complete (matches chatClient.ts qaStream parser).
 * Auth model: smoke-user (admin) seeds game inline; no smoke-aaron / no snapshot DB needed.
 *
 * Spec: docs/superpowers/specs/2026-05-10-e2e-smoke-game-night-integration-design.md
 * Plan: docs/superpowers/plans/2026-05-10-e2e-smoke-integration.md
 */
import type { APIRequestContext, Page, Route } from '@playwright/test';

const API_BASE = process.env.SMOKE_API_BASE ?? 'http://localhost:8080';

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
 * Deterministic SharedGame ID seeded via SQL fixture (tests/fixtures/smoke-test-games.sql).
 *
 * Inline runtime seeding via API was attempted (PR #956) but failed because
 * POST /api/v1/games creates a Game (GameManagement BC), while the library
 * route requires a SharedGame (SharedGameCatalog BC) — different aggregates,
 * no public admin endpoint for SharedGame creation. SQL fixture is applied
 * by the workflow before specs run.
 *
 * The `request` and `cookieHeader` parameters are unused but kept for API
 * stability across spec call sites — future migrations may need real seeding.
 *
 * @returns deterministic UUID matching tests/fixtures/smoke-test-games.sql
 */
export function seedGameForChat(
  _request: APIRequestContext,
  _cookieHeader: string
): Promise<string> {
  return Promise.resolve('00000000-0000-4000-8000-000000053e1d');
}

/**
 * Mock SSE V2 stream — formato `{type, data}` come usato da chatClient.ts qaStream.
 *
 * type=7 → Token (string accumulated in buffer da useGameChat)
 * type=4 → Complete (confidence + Citations + chatThreadId)
 *
 * NB: parser qaStream estrae solo le linee `data:` (ignora `event:`).
 * Il payload JSON DEVE contenere il campo `type` numerico.
 * Citations DEVE essere PascalCase per match con backend Contracts.cs.
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
  await page.route(`**/api/v1/knowledge-base/${gameId}/documents`, async (route: Route) => {
    await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
  });
}

/**
 * Mock getGameDocuments with at least one indexed document.
 *
 * Required for game-night specs that need ChatInputBar to mount: GameAiChatTab
 * gates rendering of GameChatTabV2 on `kbDocs.some(d => d.status === 'indexed' || 'processing')`.
 * If kbDocs is empty (real backend with no PDFs uploaded for the seed game),
 * the tab renders the "Carica un PDF" placeholder and message-input never appears,
 * causing waitForSelector to time out (#960 Cat B real root cause).
 *
 * The doc shape mirrors what /api/v1/knowledge-base/{gameId}/documents returns
 * in production. Only `id` and `status` are consumed by GameAiChatTab; other
 * fields are present for type safety against PdfDocumentResponseSchema.
 */
export async function mockHasIndexedDocument(page: Page, gameId: string): Promise<void> {
  const doc = {
    id: '00000000-0000-4000-8000-000000053edd',
    fileName: 'smoke-fixture-rulebook.pdf',
    fileSizeBytes: 1024,
    contentType: 'application/pdf',
    pageCount: 1,
    uploadedAt: new Date().toISOString(),
    uploadedByUserId: '00000000-0000-4000-8000-000000000001',
    processingStatus: 'Ready',
    status: 'indexed',
    chunkCount: 1,
    isPublic: false,
  };
  await page.route(`**/api/v1/knowledge-base/${gameId}/documents`, async (route: Route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([doc]),
    });
  });
}
