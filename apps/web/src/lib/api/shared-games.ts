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

// ========== Fetch helpers ==========

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

async function getJson<T>(url: string, schema: z.ZodType<T>, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(url, {
      method: 'GET',
      credentials: 'include',
      headers: { Accept: 'application/json' },
      ...init,
    });
  } catch (cause) {
    throw new Error(`Request to ${url} failed before reaching the server`, { cause });
  }
  if (!response.ok) {
    throw new Error(`Request to ${url} failed with status ${response.status}`);
  }
  const body = (await response.json()) as unknown;
  return schema.parse(body);
}

// ========== Public functions ==========

export async function searchSharedGames(
  args: SearchSharedGamesArgs,
  init?: RequestInit
): Promise<PagedSharedGamesV2> {
  const url = buildSearchUrl(args);
  return getJson(url, PagedSharedGamesV2Schema, init);
}

export async function getTopContributors(
  limit = 5,
  init?: RequestInit
): Promise<readonly TopContributor[]> {
  const bounded = Math.min(20, Math.max(1, Math.trunc(limit)));
  const url = `${getApiBase()}/api/v1/shared-games/top-contributors?limit=${bounded}`;
  return getJson(url, z.array(TopContributorSchema), init);
}

export async function getCategories(init?: RequestInit): Promise<readonly GameCategoryV2[]> {
  const url = `${getApiBase()}/api/v1/shared-games/categories`;
  return getJson(url, z.array(GameCategoryV2Schema), init);
}
