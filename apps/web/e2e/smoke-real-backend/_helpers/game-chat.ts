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
 * Mock indexed getGameDocuments (forces State-3 KB-ready chat in GameAiChatTab).
 *
 * Issue #992: senza questo mock, GameAiChatTab.tsx:60 vede indexedCount=0 e
 * renderizza lo State-2 "Carica un PDF delle regole per abilitare la chat AI"
 * invece della chat UI → message-input mai presente → timeout 30s. La SQL fixture
 * smoke-test-games.sql crea una SharedGame senza PDF reale (per design — niente
 * KB / vector index nel nightly stack), quindi il test DEVE mockare l'endpoint.
 *
 * Pre-PR #988 il fallimento si manifestava come React #418 hydration mismatch
 * minified in produzione (SSR vs client state divergence sul KB list); locale
 * dev build mostra solo l'empty State-2 silent. Root cause identica.
 */
export async function mockReadyKbDocuments(page: Page, gameId: string): Promise<void> {
  // Shape matches GameDocumentSchema (apps/web/src/lib/api/schemas/game-documents.schemas.ts).
  // Zod strict parse: any extra/missing field → null → State-2 empty regression.
  const docs = [
    {
      id: '00000000-0000-4000-8000-000000000001',
      title: 'smoke-rules.pdf',
      status: 'indexed',
      pageCount: 10,
      createdAt: new Date().toISOString(),
      category: 'Rulebook',
      versionLabel: null,
    },
  ];
  await page.route(`**/api/v1/knowledge-base/${gameId}/documents`, async (route: Route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(docs),
    });
  });
}
