/**
 * MSW handlers for gamebook glossary endpoints (issue #952).
 *
 * Covers backend routes shipped by BC SessionTracking:
 *   GET  /api/v1/gamebook/campaigns/{campaignId}/glossary
 *   POST /api/v1/gamebook/campaigns/{campaignId}/glossary/bootstrap
 *   PUT  /api/v1/gamebook/campaigns/{campaignId}/glossary/{entryId}
 *
 * Mutable state via `setUpsertResponder` lets individual tests script
 * success / 500 / network-failure outcomes. Request-capture helpers
 * surface the PUT payload for AC-3 / AC-4 retry assertions.
 */

import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ---------------------------------------------------------------------------
// Request capture
// ---------------------------------------------------------------------------

interface CapturedUpsert {
  readonly campaignId: string;
  readonly entryId: string;
  readonly body: { termEn: string; termIt: string };
}

const capturedUpserts: CapturedUpsert[] = [];

export function getCapturedGlossaryUpserts(): readonly CapturedUpsert[] {
  return capturedUpserts;
}

export function resetCapturedGlossaryUpserts(): void {
  capturedUpserts.length = 0;
}

// ---------------------------------------------------------------------------
// Configurable upsert responder (default: echo the request body)
// ---------------------------------------------------------------------------

type UpsertResponder = (params: {
  campaignId: string;
  entryId: string;
  body: { termEn: string; termIt: string };
}) => HttpResponse | Promise<HttpResponse>;

const echoResponder: UpsertResponder = ({ entryId, body }) =>
  HttpResponse.json({
    id: entryId,
    termEn: body.termEn,
    termIt: body.termIt,
    source: 'Manual',
    updatedAt: new Date('2026-05-19T10:00:00Z').toISOString(),
  });

let activeUpsertResponder: UpsertResponder = echoResponder;

/**
 * Override the PUT /glossary/{entryId} responder for a single test.
 * Call `resetGlossaryResponder()` in `afterEach` (or rely on
 * `server.resetHandlers()` from the global vitest setup).
 */
export function setUpsertResponder(responder: UpsertResponder): void {
  activeUpsertResponder = responder;
}

export function resetGlossaryResponder(): void {
  activeUpsertResponder = echoResponder;
}

// ---------------------------------------------------------------------------
// Handlers
// ---------------------------------------------------------------------------

export const gamebookGlossaryHandlers = [
  http.get(
    `${API_BASE}/api/v1/gamebook/campaigns/:campaignId/glossary`,
    () => HttpResponse.json([])
  ),

  http.post(
    `${API_BASE}/api/v1/gamebook/campaigns/:campaignId/glossary/bootstrap`,
    () => HttpResponse.json([])
  ),

  http.put(
    `${API_BASE}/api/v1/gamebook/campaigns/:campaignId/glossary/:entryId`,
    async ({ request, params }) => {
      const body = (await request.json()) as { termEn: string; termIt: string };
      const campaignId = String(params.campaignId);
      const entryId = String(params.entryId);
      capturedUpserts.push({ campaignId, entryId, body });
      return activeUpsertResponder({ campaignId, entryId, body });
    }
  ),
];
