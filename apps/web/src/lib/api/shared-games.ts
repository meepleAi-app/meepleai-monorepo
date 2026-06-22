/**
 * SharedGames public catalog API client.
 *
 * Public, unauthenticated endpoints exposed by `SharedGameCatalogPublicEndpoints`:
 *   - GET /api/v1/shared-games          (paged list w/ filter chips, genre, sort)
 *   - GET /api/v1/shared-games/top-contributors?limit=5
 *   - GET /api/v1/shared-games/categories  (used to map UI genre name → categoryId Guid)
 *   - GET /api/v1/shared-games/{id}     (detail with nested toolkits/agents/kbs)
 *
 * Schemas live in `@/lib/api/schemas/shared-games.schemas` (canonical). This
 * module provides the public-surface client (no shared `apiClient` indirection,
 * mirrors the `waitlist.ts` direct-fetch pattern so failures stay local and
 * explicit), plus a typed `SharedGamesApiError` for the `useSharedGameDetail`
 * FSM to map exceptions to UI status.
 */

import { z } from 'zod';

import { getApiBase } from './core/httpClient';
import {
  GameCategorySchema,
  PagedSharedGamesSchema,
  PublishedAgentPreviewSchema,
  PublishedKbPreviewSchema,
  PublishedToolkitPreviewSchema,
  SharedGameDetailSchema,
  SharedGameSchema,
  TopContributorSchema,
  type GameCategory,
  type PagedSharedGames,
  type PublishedAgentPreview,
  type PublishedKbPreview,
  type PublishedToolkitPreview,
  type SharedGame,
  type SharedGameDetail,
  type TopContributor,
} from './schemas/shared-games.schemas';

// Re-export so consumers can keep importing from `@/lib/api/shared-games`.
export {
  GameCategorySchema,
  PagedSharedGamesSchema,
  PublishedAgentPreviewSchema,
  PublishedKbPreviewSchema,
  PublishedToolkitPreviewSchema,
  SharedGameDetailSchema,
  SharedGameSchema,
  TopContributorSchema,
};
export type {
  GameCategory,
  PagedSharedGames,
  PublishedAgentPreview,
  PublishedKbPreview,
  PublishedToolkitPreview,
  SharedGame,
  SharedGameDetail,
  TopContributor,
};

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
 * Typed error for SharedGames API failures. Carries enough context for the
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
 * Extended init type for SharedGames fetches. `timeoutMs` triggers an
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
): Promise<PagedSharedGames> {
  const url = buildSearchUrl(args);
  return getJson(url, PagedSharedGamesSchema, init);
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
): Promise<readonly GameCategory[]> {
  const url = `${getApiBase()}/api/v1/shared-games/categories`;
  return getJson(url, z.array(GameCategorySchema), init);
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
): Promise<SharedGameDetail> {
  const url = `${getApiBase()}/api/v1/shared-games/${encodeURIComponent(id)}`;
  return getJson(url, SharedGameDetailSchema, init);
}
