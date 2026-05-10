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
 * Idempotent inline game seeding for chat smoke specs.
 *
 * Creates a game with a run-id-suffixed title (unique per CI run), then adds
 * it to the admin's library so /library/games/{id} route renders. Returns the
 * gameId. The smoke-user is admin (INITIAL_ADMIN_* env in CI workflow), so
 * has authority to create games.
 *
 * Throws on any non-2xx status (except 409 conflict on create, which is
 * tolerated — but we still need the id, so re-fetch via list query).
 */
export async function seedGameForChat(
  request: APIRequestContext,
  cookieHeader: string
): Promise<string> {
  const runId = process.env.GITHUB_RUN_ID ?? 'local';
  const title = `Smoke Chat Game ${runId}`;

  const createRes = await request.post(`${API_BASE}/api/v1/games`, {
    headers: { Cookie: cookieHeader },
    data: {
      title,
      publisher: 'smoke',
      minPlayers: 2,
      maxPlayers: 4,
    },
  });

  let gameId: string;

  if (createRes.status() === 201 || createRes.status() === 200) {
    const body = (await createRes.json()) as { id: string };
    if (!body.id) {
      throw new Error(`seedGameForChat: create returned no id (status=${createRes.status()})`);
    }
    gameId = body.id;
  } else if (createRes.status() === 409) {
    // Conflict — game with this title already exists from a prior run on
    // the same GITHUB_RUN_ID. Re-fetch to recover the id.
    const listRes = await request.get(
      `${API_BASE}/api/v1/games?title=${encodeURIComponent(title)}`,
      { headers: { Cookie: cookieHeader } }
    );
    if (!listRes.ok()) {
      throw new Error(
        `seedGameForChat: create=409 but list re-fetch failed ${listRes.status()}`
      );
    }
    const listBody = (await listRes.json()) as { id: string; title: string }[] | { items: { id: string; title: string }[] };
    const items = Array.isArray(listBody) ? listBody : listBody.items;
    const found = items.find(g => g.title === title);
    if (!found) {
      throw new Error(`seedGameForChat: create=409 but no game with title="${title}" in list`);
    }
    gameId = found.id;
  } else {
    const errBody = await createRes.text();
    throw new Error(
      `seedGameForChat: create failed status=${createRes.status()} body=${errBody}`
    );
  }

  // Add to library so /library/games/{id} renders. Idempotent on backend (or
  // tolerate 409 if already present).
  const libRes = await request.post(
    `${API_BASE}/api/v1/library/games/${gameId}`,
    { headers: { Cookie: cookieHeader } }
  );
  if (!libRes.ok() && libRes.status() !== 409) {
    const errBody = await libRes.text();
    throw new Error(
      `seedGameForChat: library add failed status=${libRes.status()} body=${errBody}`
    );
  }

  return gameId;
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
  await page.route(
    `**/api/v1/knowledge-base/${gameId}/documents`,
    async (route: Route) => {
      await route.fulfill({ status: 200, contentType: 'application/json', body: '[]' });
    }
  );
}
