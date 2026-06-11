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
  options: { skip?: number; take?: number; reason?: string } = {}
): Promise<WikidataDeadLetterAttemptsResult> {
  const params = new URLSearchParams();
  if (options.skip !== undefined) params.set('skip', String(options.skip));
  if (options.take !== undefined) params.set('take', String(options.take));
  if (options.reason) params.set('reason', options.reason);

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
