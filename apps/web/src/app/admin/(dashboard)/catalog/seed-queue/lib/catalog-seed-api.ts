/**
 * Issue #1903 M8.1 — Fetch wrapper for the admin catalog seed pipeline endpoints.
 *
 * All endpoints under `/api/v1/admin/catalog/seeds`. Admin session is enforced at
 * the group level (`RequireAdminSessionFilter`), so non-admin callers receive 401/403;
 * when the runtime kill-switch `admin.catalog-seed.enabled` is `false`, all routes
 * return 503 (handled by an endpoint filter, see `AdminCatalogSeedEndpoints.cs`).
 *
 * The fetch wrappers below mirror the BE contract documented in
 * `apps/api/src/Api/Routing/Admin/AdminCatalogSeedEndpoints.cs`.
 */

import type {
  ApproveCatalogSeedResult,
  BulkEnqueueRequest,
  BulkEnqueueResult,
  CatalogSeedDraftDto,
  EnqueueCatalogSeedRequest,
  EnqueueCatalogSeedResult,
  ListCatalogSeedsResult,
  WikidataSearchResult,
} from './catalog-seed-types';

const BASE = '/api/v1/admin/catalog/seeds';

/**
 * Domain-typed error used by every fetch call so consumers can distinguish
 * 503 (feature flag off) from 401/403 (auth) and 4xx validation failures.
 */
export class CatalogSeedApiError extends Error {
  constructor(
    public readonly status: number,
    message: string
  ) {
    super(message);
    this.name = 'CatalogSeedApiError';
  }
}

async function parseJsonOrEmpty(res: Response): Promise<unknown> {
  try {
    return await res.json();
  } catch {
    return {};
  }
}

async function throwOnError(res: Response, defaultMessage: string): Promise<never> {
  const body = (await parseJsonOrEmpty(res)) as { detail?: string; title?: string; error?: string };
  const message = body.detail ?? body.title ?? body.error ?? `${defaultMessage}: ${res.statusText}`;
  throw new CatalogSeedApiError(res.status, message);
}

// ─── Mutations ───────────────────────────────────────────────────────────────

export async function enqueueSeed(
  body: EnqueueCatalogSeedRequest
): Promise<EnqueueCatalogSeedResult> {
  const res = await fetch(BASE, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) await throwOnError(res, 'Enqueue failed');
  return res.json();
}

export async function bulkEnqueueSeeds(body: BulkEnqueueRequest): Promise<BulkEnqueueResult> {
  const res = await fetch(`${BASE}/bulk`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });
  if (!res.ok) await throwOnError(res, 'Bulk enqueue failed');
  return res.json();
}

export async function approveSeed(id: string): Promise<ApproveCatalogSeedResult> {
  const res = await fetch(`${BASE}/${id}/approve`, {
    method: 'POST',
    credentials: 'include',
  });
  if (!res.ok) await throwOnError(res, 'Approve failed');
  return res.json();
}

export async function rejectSeed(id: string, reason: string): Promise<void> {
  const res = await fetch(`${BASE}/${id}/reject`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ reason }),
  });
  if (!res.ok) await throwOnError(res, 'Reject failed');
}

export async function wikidataSearch(searchTerm: string): Promise<WikidataSearchResult> {
  const res = await fetch(`${BASE}/wikidata-search`, {
    method: 'POST',
    credentials: 'include',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ searchTerm }),
  });
  if (!res.ok) await throwOnError(res, 'Wikidata search failed');
  return res.json();
}

// ─── Queries ─────────────────────────────────────────────────────────────────

export interface ListSeedsParams {
  status?: string;
  skip?: number;
  take?: number;
}

export async function listSeeds(params: ListSeedsParams = {}): Promise<ListCatalogSeedsResult> {
  const search = new URLSearchParams();
  if (params.status) search.set('status', params.status);
  if (typeof params.skip === 'number') search.set('skip', String(params.skip));
  if (typeof params.take === 'number') search.set('take', String(params.take));
  const qs = search.toString();
  const url = qs ? `${BASE}?${qs}` : BASE;
  const res = await fetch(url, { method: 'GET', credentials: 'include' });
  if (!res.ok) await throwOnError(res, 'List failed');
  return res.json();
}

/**
 * Returns `null` on 404 so the UI can render an empty-state without throwing.
 */
export async function getSeedById(id: string): Promise<CatalogSeedDraftDto | null> {
  const res = await fetch(`${BASE}/${id}`, { method: 'GET', credentials: 'include' });
  if (res.status === 404) return null;
  if (!res.ok) await throwOnError(res, 'Get seed failed');
  return res.json();
}
