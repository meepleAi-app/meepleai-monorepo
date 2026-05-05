/**
 * SharedGames v2 API client — Wave A.3b (Issue #596).
 *
 * Public, unauthenticated endpoints exposed by `SharedGameCatalogPublicEndpoints`:
 *   - GET /api/v1/shared-games          (paged list w/ filter chips, genre, sort)
 *   - GET /api/v1/shared-games/top-contributors?limit=5
 *   - GET /api/v1/shared-games/categories  (used to map UI genre name → categoryId Guid)
 *
 * Why a v2 module rather than reusing `schemas/shared-games.schemas.ts`:
 *  - Wave A.3a extended `SharedGameDto` with 7 new fields (`toolkitsCount`,
 *    `agentsCount`, `kbsCount`, `newThisWeekCount`, `contributorsCount`,
 *    `isTopRated`, `isNew`). The legacy schema (used by /shared-games/[id])
 *    is kept untouched to avoid breaking other call-sites.
 *  - Public surface — no shared `apiClient` indirection, mirrors the
 *    `waitlist.ts` direct-fetch pattern so failures stay local and explicit.
 */

import { z } from 'zod';

import { getApiBase } from './core/httpClient';

// ========== Schemas ==========

/**
 * Extended SharedGameDto (post-Wave A.3a). Mirrors backend
 * `SharedGameDto` record exactly — JSON camelCase serialization.
 */
export const SharedGameV2Schema = z.object({
  id: z.string().uuid(),
  bggId: z.number().int().nullable(),
  title: z.string().min(1),
  yearPublished: z.number().int(),
  description: z.string(),
  minPlayers: z.number().int().nonnegative(),
  maxPlayers: z.number().int().nonnegative(),
  playingTimeMinutes: z.number().int().nonnegative(),
  minAge: z.number().int().nonnegative(),
  complexityRating: z.number().nullable(),
  averageRating: z.number().nullable(),
  imageUrl: z.string().catch(''),
  thumbnailUrl: z.string().catch(''),
  status: z.string(),
  createdAt: z.string(),
  modifiedAt: z.string().nullable(),
  isRagPublic: z.boolean().default(false),
  hasKnowledgeBase: z.boolean().default(false),
  // Wave A.3a additions:
  toolkitsCount: z.number().int().nonnegative().default(0),
  agentsCount: z.number().int().nonnegative().default(0),
  kbsCount: z.number().int().nonnegative().default(0),
  newThisWeekCount: z.number().int().nonnegative().default(0),
  contributorsCount: z.number().int().nonnegative().default(0),
  isTopRated: z.boolean().default(false),
  isNew: z.boolean().default(false),
});

export type SharedGameV2 = z.infer<typeof SharedGameV2Schema>;

export const PagedSharedGamesV2Schema = z.object({
  items: z.array(SharedGameV2Schema),
  total: z.number().int().nonnegative(),
  page: z.number().int().positive(),
  pageSize: z.number().int().positive(),
});

export type PagedSharedGamesV2 = z.infer<typeof PagedSharedGamesV2Schema>;

export const TopContributorSchema = z.object({
  userId: z.string().uuid(),
  displayName: z.string().min(1),
  avatarUrl: z.string().nullable(),
  totalSessions: z.number().int().nonnegative(),
  totalWins: z.number().int().nonnegative(),
  score: z.number().int().nonnegative(),
});

export type TopContributor = z.infer<typeof TopContributorSchema>;

export const GameCategoryV2Schema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1),
  slug: z.string(),
});

export type GameCategoryV2 = z.infer<typeof GameCategoryV2Schema>;

// ========== Detail (Wave A.4 — Issue #603) ==========

/**
 * Mirrors backend `PublishedToolkitPreviewDto` (Issue #603 §3.2).
 * Simplified shape: spec's `version`/`downloadCount` deferred until domain model
 * exposes them — frontend renders sensible fallbacks via i18n.
 */
export const PublishedToolkitPreviewSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1),
  ownerId: z.string().uuid(),
  ownerName: z.string(),
  lastUpdatedAt: z.string(),
});

export type PublishedToolkitPreview = z.infer<typeof PublishedToolkitPreviewSchema>;

/**
 * Mirrors backend `PublishedAgentPreviewDto`. `invocationCount` is the real
 * popularity proxy from runtime telemetry; rating system not implemented yet.
 */
export const PublishedAgentPreviewSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1),
  invocationCount: z.number().int().nonnegative(),
  lastUpdatedAt: z.string(),
});

export type PublishedAgentPreview = z.infer<typeof PublishedAgentPreviewSchema>;

/**
 * Mirrors backend `PublishedKbPreviewDto`. `totalChunks` is a coarse "size"
 * indicator until PdfDocument metadata (filename/title/pages) is wired through
 * to KB queries.
 */
export const PublishedKbPreviewSchema = z.object({
  id: z.string().uuid(),
  language: z.string(),
  totalChunks: z.number().int().nonnegative(),
  indexedAt: z.string(),
});

export type PublishedKbPreview = z.infer<typeof PublishedKbPreviewSchema>;

/**
 * Mirrors backend `SharedGameDetailDto` post-Wave A.4 extension.
 * Nested toolkit/agent/kb arrays are nullable on the wire — schema normalises
 * to empty arrays so consumers don't need to null-check.
 */
export const SharedGameDetailV2Schema = z.object({
  id: z.string().uuid(),
  bggId: z.number().int().nullable(),
  title: z.string().min(1),
  yearPublished: z.number().int(),
  description: z.string(),
  minPlayers: z.number().int().nonnegative(),
  maxPlayers: z.number().int().nonnegative(),
  playingTimeMinutes: z.number().int().nonnegative(),
  minAge: z.number().int().nonnegative(),
  complexityRating: z.number().nullable(),
  averageRating: z.number().nullable(),
  imageUrl: z.string().catch(''),
  thumbnailUrl: z.string().catch(''),
  status: z.string(),
  createdAt: z.string(),
  modifiedAt: z.string().nullable(),
  // Wave A.4 extension fields:
  toolkits: z
    .array(PublishedToolkitPreviewSchema)
    .nullable()
    .default([])
    .transform(v => v ?? []),
  agents: z
    .array(PublishedAgentPreviewSchema)
    .nullable()
    .default([])
    .transform(v => v ?? []),
  kbs: z
    .array(PublishedKbPreviewSchema)
    .nullable()
    .default([])
    .transform(v => v ?? []),
  toolkitsCount: z.number().int().nonnegative().default(0),
  agentsCount: z.number().int().nonnegative().default(0),
  kbsCount: z.number().int().nonnegative().default(0),
  contributorsCount: z.number().int().nonnegative().default(0),
  hasKnowledgeBase: z.boolean().default(false),
  isTopRated: z.boolean().default(false),
  isNew: z.boolean().default(false),
});

export type SharedGameDetailV2 = z.infer<typeof SharedGameDetailV2Schema>;

// ========== Search args ==========

export interface SearchSharedGamesArgs {
  /** Free-text search; maps to `?search=`. */
  readonly search?: string;
  /** Pre-resolved category Guids; maps to repeated `?categoryIds=`. */
  readonly categoryIds?: readonly string[];
  /** Chip flags — translate to `?hasToolkit=`, `?hasAgent=`, etc. */
  readonly hasToolkit?: boolean;
  readonly hasAgent?: boolean;
  readonly hasKb?: boolean;
  readonly isTopRated?: boolean;
  readonly isNew?: boolean;
  /** UI sort key; mapped via `sortKeyToBackendParams` before being sent. */
  readonly sortBy?: string;
  readonly sortDescending?: boolean;
  readonly pageNumber?: number;
  readonly pageSize?: number;
}

// ========== Errors ==========

/**
 * Typed error for SharedGames v2 API failures. Carries enough context for the
 * `useSharedGameDetail` FSM to map an exception to a UI status:
 *   - `kind='http'` + `httpStatus===404` → `'not-found'`
 *   - `kind='http'` + 5xx                → `'error'`
 *   - `kind='network' | 'timeout'`       → `'error'`
 *   - `kind='parse'`                     → `'error'` (server returned malformed payload)
 *
 * Issue #615.
 */
export class SharedGamesApiError extends Error {
  readonly kind: 'http' | 'network' | 'timeout' | 'parse';
  readonly httpStatus?: number;

  constructor(
    message: string,
    kind: SharedGamesApiError['kind'],
    options?: ErrorOptions & { httpStatus?: number }
  ) {
    super(message, options);
    this.name = 'SharedGamesApiError';
    this.kind = kind;
    this.httpStatus = options?.httpStatus;
  }
}

// ========== Fetch helpers ==========

/**
 * Extended init type for SharedGames v2 fetches. `timeoutMs` triggers an
 * AbortController abort after the specified delay — used by the SSR path
 * (page.tsx sets `timeoutMs: 2000`) to keep the public detail page from
 * hanging the Next.js render. Client-side callers omit it; abort is owned
 * by TanStack Query via `useSharedGameDetail`.
 *
 * Issue #615.
 */
export type SharedGamesRequestInit = RequestInit & { timeoutMs?: number };

function buildSearchUrl(args: SearchSharedGamesArgs): string {
  const params = new URLSearchParams();
  if (args.search && args.search.length > 0) params.set('search', args.search);
  if (args.categoryIds && args.categoryIds.length > 0) {
    for (const id of args.categoryIds) params.append('categoryIds', id);
  }
  if (args.hasToolkit !== undefined) params.set('hasToolkit', String(args.hasToolkit));
  if (args.hasAgent !== undefined) params.set('hasAgent', String(args.hasAgent));
  if (args.hasKb !== undefined) params.set('hasKb', String(args.hasKb));
  if (args.isTopRated !== undefined) params.set('isTopRated', String(args.isTopRated));
  if (args.isNew !== undefined) params.set('isNew', String(args.isNew));
  if (args.sortBy) params.set('sortBy', args.sortBy);
  if (args.sortDescending !== undefined) params.set('sortDescending', String(args.sortDescending));
  params.set('pageNumber', String(args.pageNumber ?? 1));
  params.set('pageSize', String(args.pageSize ?? 20));
  return `${getApiBase()}/api/v1/shared-games?${params.toString()}`;
}

async function getJson<T>(
  url: string,
  schema: z.ZodType<T>,
  init?: SharedGamesRequestInit
): Promise<T> {
  // Timeout wiring: SSR callers pass `timeoutMs` so a slow backend can't
  // block the public detail page render. Client callers omit it; the
  // hook owns cancellation via TanStack Query's per-query AbortController.
  // If the caller already passed an `init.signal`, we honour it as-is and
  // skip our own controller — composing two signals would require
  // AbortSignal.any (Node 20+/widely-shipped browsers) and isn't needed
  // for current call sites.
  const useOurController = init?.timeoutMs !== undefined && !init?.signal;
  const controller = useOurController ? new AbortController() : null;
  const timeoutId =
    controller && init?.timeoutMs !== undefined
      ? setTimeout(() => controller.abort(), init.timeoutMs)
      : null;

  let response: Response;
  try {
    response = await fetch(url, {
      method: 'GET',
      credentials: 'include',
      headers: { Accept: 'application/json' },
      ...init,
      signal: controller?.signal ?? init?.signal,
    });
  } catch (cause) {
    // AbortError: distinguish caller-cancelled vs our-timeout-fired.
    const isAbort =
      cause instanceof Error && (cause.name === 'AbortError' || cause.name === 'TimeoutError');
    if (isAbort && controller?.signal.aborted) {
      throw new SharedGamesApiError(
        `Request to ${url} timed out after ${init?.timeoutMs}ms`,
        'timeout',
        { cause }
      );
    }
    if (isAbort) {
      // Caller-owned abort — re-throw the original so consumers can detect
      // cancellation via `cause.name === 'AbortError'` if they care.
      throw cause;
    }
    throw new SharedGamesApiError(
      `Request to ${url} failed before reaching the server`,
      'network',
      { cause }
    );
  } finally {
    if (timeoutId !== null) {
      clearTimeout(timeoutId);
    }
  }

  if (!response.ok) {
    throw new SharedGamesApiError(
      `Request to ${url} failed with status ${response.status}`,
      'http',
      { httpStatus: response.status }
    );
  }

  const body = (await response.json()) as unknown;
  try {
    return schema.parse(body);
  } catch (cause) {
    throw new SharedGamesApiError(`Response from ${url} did not match expected schema`, 'parse', {
      cause,
    });
  }
}

// ========== Public functions ==========

export async function searchSharedGames(
  args: SearchSharedGamesArgs,
  init?: SharedGamesRequestInit
): Promise<PagedSharedGamesV2> {
  const url = buildSearchUrl(args);
  return getJson(url, PagedSharedGamesV2Schema, init);
}

export async function getTopContributors(
  limit = 5,
  init?: SharedGamesRequestInit
): Promise<readonly TopContributor[]> {
  const bounded = Math.min(20, Math.max(1, Math.trunc(limit)));
  const url = `${getApiBase()}/api/v1/shared-games/top-contributors?limit=${bounded}`;
  return getJson(url, z.array(TopContributorSchema), init);
}

export async function getCategories(
  init?: SharedGamesRequestInit
): Promise<readonly GameCategoryV2[]> {
  const url = `${getApiBase()}/api/v1/shared-games/categories`;
  return getJson(url, z.array(GameCategoryV2Schema), init);
}

/**
 * Fetch detail of a single shared game.
 *
 * Wave A.4 (Issue #603). Public, unauthenticated endpoint. Backend
 * exposes nested previews (toolkits/agents/kbs) eagerly to avoid
 * N+1 client calls on the detail page.
 *
 * Throws on non-OK response — caller must handle 404 / 500 explicitly
 * (typically via TanStack Query's `error` channel).
 */
export async function getSharedGameDetail(
  id: string,
  init?: SharedGamesRequestInit
): Promise<SharedGameDetailV2> {
  const url = `${getApiBase()}/api/v1/shared-games/${encodeURIComponent(id)}`;
  return getJson(url, SharedGameDetailV2Schema, init);
}
