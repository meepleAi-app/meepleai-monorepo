/**
 * Admin Wikidata Cover Enrichment dead-letter API client.
 * Issue #1823 Wave 3 M13.
 *
 * BE source:
 *   apps/api/src/Api/Routing/Admin/AdminWikidataCoverEnrichmentEndpoints.cs
 *   apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetWikidataDeadLetterAttemptsQuery.cs
 *
 * Endpoints used in this client:
 *   GET  /api/v1/admin/wikidata/enrichment/dead-letters  — paginated list
 *   POST /api/v1/admin/wikidata/enrichment/{gameId}      — retry trigger (M12)
 */

export interface WikidataDeadLetterAttemptDto {
  id: string;
  sharedGameId: string;
  gameTitle: string;
  attemptedAt: string;
  deadLetteredAt: string;
  reason: string;
  details: string | null;
  retryCount: number;
  // Phase F F5 — acknowledgement (Issue #2254)
  acknowledgedAt: string | null;
  acknowledgedBy: string | null;
  acknowledgedByFullName: string | null;
  // Phase F F6 — attempt-source attribution (Issue #2255)
  triggeredByAdminUserId: string | null;
  triggeredByAdminFullName: string | null;
}

export interface WikidataDeadLetterAttemptsResult {
  items: WikidataDeadLetterAttemptDto[];
  totalCount: number;
  skip: number;
  take: number;
}

export interface AdminEnrichWikidataCoverResult {
  outcome: 'success' | 'skipped' | 'failed';
  reason: string | null;
  details: string | null;
  r2Key: string | null;
  license: string | null;
  attribution: string | null;
  sourceUrl: string | null;
}

const ENDPOINT_BASE = '/api/v1/admin/wikidata/enrichment';

async function rejectAs(response: Response, message: string): Promise<never> {
  let body: string;
  try {
    body = await response.text();
  } catch {
    body = '';
  }
  throw new Error(
    `${message}: ${response.status} ${response.statusText}${body ? ` — ${body}` : ''}`
  );
}

export async function listDeadLetters(
  options: {
    skip?: number;
    take?: number;
    reason?: string;
    includeAcknowledged?: boolean;
  } = {}
): Promise<WikidataDeadLetterAttemptsResult> {
  const params = new URLSearchParams();
  if (options.skip !== undefined) params.set('skip', String(options.skip));
  if (options.take !== undefined) params.set('take', String(options.take));
  if (options.reason) params.set('reason', options.reason);
  if (options.includeAcknowledged) params.set('includeAcknowledged', 'true');

  const url = `${ENDPOINT_BASE}/dead-letters${params.size > 0 ? `?${params.toString()}` : ''}`;
  const response = await fetch(url, { credentials: 'include' });
  if (!response.ok) {
    await rejectAs(response, 'Failed to list dead-letters');
  }
  return (await response.json()) as WikidataDeadLetterAttemptsResult;
}

/**
 * Re-trigger enrichment for a dead-letter row. Passes forceRefresh=true so the
 * M8 freshness window is bypassed.
 */
export async function retryDeadLetter(gameId: string): Promise<AdminEnrichWikidataCoverResult> {
  const response = await fetch(`${ENDPOINT_BASE}/${encodeURIComponent(gameId)}`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ forceRefresh: true }),
  });
  if (!response.ok) {
    await rejectAs(response, 'Failed to retry enrichment');
  }
  return (await response.json()) as AdminEnrichWikidataCoverResult;
}

// ─────────────────────────────────────────────────────────────────────────────
// Phase E F2 — bulk retry
// ─────────────────────────────────────────────────────────────────────────────

/**
 * BE-side cap (mirrors AdminBulkRetryWikidataCoverCommandValidator.MaxBatchSize).
 * Larger client-side selections MUST be chunked before invoking the endpoint.
 */
export const BULK_RETRY_MAX_BATCH = 50;

export type BulkRetryRowOutcome = 'triggered' | 'not-found' | 'failed';

export interface BulkRetryRow {
  attemptId: string;
  gameId: string | null;
  outcome: BulkRetryRowOutcome;
  reason: string | null;
}

export interface AdminBulkRetryWikidataCoverResult {
  triggeredCount: number;
  notFoundCount: number;
  failedCount: number;
  rows: BulkRetryRow[];
}

export async function bulkRetryDeadLetters(
  attemptIds: string[]
): Promise<AdminBulkRetryWikidataCoverResult> {
  const response = await fetch(`${ENDPOINT_BASE}/bulk-retry`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ attemptIds }),
  });
  if (!response.ok) {
    await rejectAs(response, 'Failed to bulk-retry enrichments');
  }
  return (await response.json()) as AdminBulkRetryWikidataCoverResult;
}

// ─────────────────────────────────────────────────────────────────────────────
// Phase E F3 — per-row attempt timeline
// ─────────────────────────────────────────────────────────────────────────────

/**
 * Outcome surfaced by the BE on each timeline node — mirrors the
 * <c>WikidataCoverEnrichmentOutcome</c> enum on the aggregate.
 */
export type AttemptTimelineOutcome = 'Success' | 'Skipped' | 'Failed' | 'DeadLetter';

export interface WikidataAttemptTimelineNode {
  id: string;
  attemptedAt: string;
  outcome: AttemptTimelineOutcome;
  reason: string | null;
  details: string | null;
  retryCount: number;
  nextRetryAt: string | null;
  deadLetteredAt: string | null;
  // Phase F F6 — attempt-source attribution (Issue #2255)
  triggeredByAdminUserId: string | null;
  triggeredByAdminFullName: string | null;
}

export interface WikidataAttemptTimelineResult {
  gameId: string;
  items: WikidataAttemptTimelineNode[];
}

export async function getAttemptTimeline(
  gameId: string,
  limit = 50
): Promise<WikidataAttemptTimelineResult> {
  const params = new URLSearchParams({ limit: String(limit) });
  const response = await fetch(
    `${ENDPOINT_BASE}/games/${encodeURIComponent(gameId)}/attempts?${params.toString()}`,
    { credentials: 'include' }
  );
  if (!response.ok) {
    await rejectAs(response, 'Failed to load attempt timeline');
  }
  return (await response.json()) as WikidataAttemptTimelineResult;
}

// ─────────────────────────────────────────────────────────────────────────────
// Phase F F5 — bulk acknowledge (Issue #2254)
// ─────────────────────────────────────────────────────────────────────────────

/**
 * BE-side cap (mirrors AdminBulkAcknowledgeWikidataCoverCommandValidator.MaxBatchSize).
 * Larger client-side selections MUST be chunked before invoking the endpoint.
 */
export const BULK_ACKNOWLEDGE_MAX_BATCH = 50;

/**
 * BE-side note maxLength (mirrors AdminBulkAcknowledgeWikidataCoverCommandValidator.MaxNoteLength).
 */
export const BULK_ACKNOWLEDGE_NOTE_MAX_LENGTH = 500;

export type BulkAcknowledgeRowOutcome = 'acked' | 'already-acked' | 'not-found' | 'wrong-state';

export interface BulkAcknowledgeRow {
  attemptId: string;
  gameId: string | null;
  outcome: BulkAcknowledgeRowOutcome;
  reason: string | null;
}

export interface AdminBulkAcknowledgeResult {
  ackedCount: number;
  idempotentNoOpCount: number;
  notFoundCount: number;
  rows: BulkAcknowledgeRow[];
}

/**
 * Bulk-acknowledge dead-letter attempts. Optional note is log-only on the BE
 * (DEC-F-4); not persisted on the attempt row.
 */
export async function bulkAcknowledgeDeadLetters(
  attemptIds: string[],
  note: string | null
): Promise<AdminBulkAcknowledgeResult> {
  const response = await fetch(`${ENDPOINT_BASE}/bulk-acknowledge`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ attemptIds, note }),
  });
  if (!response.ok) {
    await rejectAs(response, 'Failed to bulk-acknowledge enrichments');
  }
  return (await response.json()) as AdminBulkAcknowledgeResult;
}
