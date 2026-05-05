/**
 * @vitest-environment jsdom
 *
 * Wave A.4 (Issue #603) — shared-games v2 API client tests.
 *
 * Verifies:
 *  - `searchSharedGames`: URL composition with all filter chips, defaults, and
 *    repeated `categoryIds` query params; schema validation
 *  - `getTopContributors`: limit clamping (Math.min(20, Math.max(1, trunc(n))))
 *  - `getCategories`: schema validation
 *  - `getSharedGameDetail`: id encoding, schema validation, includes default empty arrays
 *  - getJson error path: throws on non-OK response and on fetch network error
 */

import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  getCategories,
  getSharedGameDetail,
  getTopContributors,
  searchSharedGames,
} from './shared-games';

const API_BASE = 'http://localhost:8080';

function makeJsonResponse(body: unknown, init?: { ok?: boolean; status?: number }): Response {
  return {
    ok: init?.ok ?? true,
    status: init?.status ?? 200,
    json: async () => body,
  } as unknown as Response;
}

const fetchSpy = vi.fn<typeof fetch>();

beforeEach(() => {
  fetchSpy.mockReset();
  vi.stubGlobal('fetch', fetchSpy);
});

afterEach(() => {
  vi.unstubAllGlobals();
});

// ===== searchSharedGames =====

describe('searchSharedGames', () => {
  const VALID_PAGE = {
    items: [],
    total: 0,
    page: 1,
    pageSize: 20,
  };

  it('builds URL with default pageNumber=1 and pageSize=20', async () => {
    fetchSpy.mockResolvedValueOnce(makeJsonResponse(VALID_PAGE));
    await searchSharedGames({});
    const url = fetchSpy.mock.calls[0]![0] as string;
    expect(url).toContain(`${API_BASE}/api/v1/shared-games?`);
    expect(url).toContain('pageNumber=1');
    expect(url).toContain('pageSize=20');
  });

  it('appends search query when non-empty', async () => {
    fetchSpy.mockResolvedValueOnce(makeJsonResponse(VALID_PAGE));
    await searchSharedGames({ search: 'catan' });
    const url = fetchSpy.mock.calls[0]![0] as string;
    expect(url).toContain('search=catan');
  });

  it('omits search when empty string', async () => {
    fetchSpy.mockResolvedValueOnce(makeJsonResponse(VALID_PAGE));
    await searchSharedGames({ search: '' });
    const url = fetchSpy.mock.calls[0]![0] as string;
    expect(url).not.toContain('search=');
  });

  it('appends repeated categoryIds query params', async () => {
    fetchSpy.mockResolvedValueOnce(makeJsonResponse(VALID_PAGE));
    await searchSharedGames({ categoryIds: ['cat-a', 'cat-b'] });
    const url = fetchSpy.mock.calls[0]![0] as string;
    // URLSearchParams encodes multiple appends
    expect(url).toContain('categoryIds=cat-a');
    expect(url).toContain('categoryIds=cat-b');
  });

  it('omits categoryIds param when array is empty', async () => {
    fetchSpy.mockResolvedValueOnce(makeJsonResponse(VALID_PAGE));
    await searchSharedGames({ categoryIds: [] });
    const url = fetchSpy.mock.calls[0]![0] as string;
    expect(url).not.toContain('categoryIds=');
  });

  it('serializes filter chip booleans', async () => {
    fetchSpy.mockResolvedValueOnce(makeJsonResponse(VALID_PAGE));
    await searchSharedGames({
      hasToolkit: true,
      hasAgent: false,
      hasKb: true,
      isTopRated: false,
      isNew: true,
    });
    const url = fetchSpy.mock.calls[0]![0] as string;
    expect(url).toContain('hasToolkit=true');
    expect(url).toContain('hasAgent=false');
    expect(url).toContain('hasKb=true');
    expect(url).toContain('isTopRated=false');
    expect(url).toContain('isNew=true');
  });

  it('serializes sortBy and sortDescending', async () => {
    fetchSpy.mockResolvedValueOnce(makeJsonResponse(VALID_PAGE));
    await searchSharedGames({ sortBy: 'rating', sortDescending: true });
    const url = fetchSpy.mock.calls[0]![0] as string;
    expect(url).toContain('sortBy=rating');
    expect(url).toContain('sortDescending=true');
  });

  it('uses provided pageNumber and pageSize', async () => {
    fetchSpy.mockResolvedValueOnce(makeJsonResponse(VALID_PAGE));
    await searchSharedGames({ pageNumber: 3, pageSize: 50 });
    const url = fetchSpy.mock.calls[0]![0] as string;
    expect(url).toContain('pageNumber=3');
    expect(url).toContain('pageSize=50');
  });

  it('sends GET with credentials=include and Accept JSON', async () => {
    fetchSpy.mockResolvedValueOnce(makeJsonResponse(VALID_PAGE));
    await searchSharedGames({});
    const init = fetchSpy.mock.calls[0]![1] as RequestInit;
    expect(init.method).toBe('GET');
    expect(init.credentials).toBe('include');
    expect((init.headers as Record<string, string>).Accept).toBe('application/json');
  });
});

// ===== getTopContributors =====

describe('getTopContributors', () => {
  it('clamps limit to upper bound 20', async () => {
    fetchSpy.mockResolvedValueOnce(makeJsonResponse([]));
    await getTopContributors(999);
    const url = fetchSpy.mock.calls[0]![0] as string;
    expect(url).toContain('limit=20');
  });

  it('clamps limit to lower bound 1', async () => {
    fetchSpy.mockResolvedValueOnce(makeJsonResponse([]));
    await getTopContributors(0);
    const url = fetchSpy.mock.calls[0]![0] as string;
    expect(url).toContain('limit=1');
  });

  it('truncates fractional limit', async () => {
    fetchSpy.mockResolvedValueOnce(makeJsonResponse([]));
    await getTopContributors(7.9);
    const url = fetchSpy.mock.calls[0]![0] as string;
    expect(url).toContain('limit=7');
  });

  it('uses default limit=5 when omitted', async () => {
    fetchSpy.mockResolvedValueOnce(makeJsonResponse([]));
    await getTopContributors();
    const url = fetchSpy.mock.calls[0]![0] as string;
    expect(url).toContain('limit=5');
  });

  it('parses contributor list through schema', async () => {
    fetchSpy.mockResolvedValueOnce(
      makeJsonResponse([
        {
          userId: '11111111-1111-4111-8111-111111111111',
          displayName: 'Alice',
          avatarUrl: null,
          totalSessions: 5,
          totalWins: 2,
          score: 12,
        },
      ])
    );
    const result = await getTopContributors(5);
    expect(result).toHaveLength(1);
    expect(result[0]!.displayName).toBe('Alice');
  });
});

// ===== getCategories =====

describe('getCategories', () => {
  it('hits the categories endpoint and parses response', async () => {
    fetchSpy.mockResolvedValueOnce(
      makeJsonResponse([
        {
          id: '22222222-2222-4222-8222-222222222222',
          name: 'Strategy',
          slug: 'strategy',
        },
      ])
    );
    const result = await getCategories();
    const url = fetchSpy.mock.calls[0]![0] as string;
    expect(url).toBe(`${API_BASE}/api/v1/shared-games/categories`);
    expect(result).toHaveLength(1);
    expect(result[0]!.name).toBe('Strategy');
  });
});

// ===== getSharedGameDetail =====

describe('getSharedGameDetail', () => {
  const SAMPLE_DETAIL = {
    id: '33333333-3333-4333-8333-333333333333',
    bggId: null,
    title: 'Catan',
    yearPublished: 1995,
    description: 'Settlers',
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 75,
    minAge: 10,
    complexityRating: 2.3,
    averageRating: 7.4,
    imageUrl: '',
    thumbnailUrl: '',
    status: 'Published',
    createdAt: '2026-04-15T00:00:00Z',
    modifiedAt: null,
    toolkits: null,
    agents: null,
    kbs: null,
    toolkitsCount: 0,
    agentsCount: 0,
    kbsCount: 0,
    contributorsCount: 0,
    hasKnowledgeBase: false,
    isTopRated: false,
    isNew: false,
  };

  it('encodes id in URL path', async () => {
    fetchSpy.mockResolvedValueOnce(makeJsonResponse(SAMPLE_DETAIL));
    await getSharedGameDetail('with spaces/and?special');
    const url = fetchSpy.mock.calls[0]![0] as string;
    expect(url).toContain(encodeURIComponent('with spaces/and?special'));
  });

  it('normalises null nested arrays to empty arrays via schema default', async () => {
    fetchSpy.mockResolvedValueOnce(makeJsonResponse(SAMPLE_DETAIL));
    const result = await getSharedGameDetail(SAMPLE_DETAIL.id);
    expect(result.toolkits).toEqual([]);
    expect(result.agents).toEqual([]);
    expect(result.kbs).toEqual([]);
  });

  it('parses populated nested arrays through schema', async () => {
    const populated = {
      ...SAMPLE_DETAIL,
      toolkits: [
        {
          id: '44444444-4444-4444-8444-444444444444',
          name: 'StarterKit',
          ownerId: '55555555-5555-4555-8555-555555555555',
          ownerName: 'Alice',
          lastUpdatedAt: '2026-04-15T00:00:00Z',
        },
      ],
      agents: [
        {
          id: '66666666-6666-4666-8666-666666666666',
          name: 'RuleBot',
          invocationCount: 42,
          lastUpdatedAt: '2026-04-15T00:00:00Z',
        },
      ],
      kbs: [
        {
          id: '77777777-7777-4777-8777-777777777777',
          language: 'en',
          totalChunks: 100,
          indexedAt: '2026-04-15T00:00:00Z',
        },
      ],
      toolkitsCount: 1,
      agentsCount: 1,
      kbsCount: 1,
    };
    fetchSpy.mockResolvedValueOnce(makeJsonResponse(populated));
    const result = await getSharedGameDetail(populated.id);
    expect(result.toolkits).toHaveLength(1);
    expect(result.toolkits[0]!.name).toBe('StarterKit');
    expect(result.agents[0]!.invocationCount).toBe(42);
    expect(result.kbs[0]!.totalChunks).toBe(100);
  });
});

// ===== Error paths (getJson) =====

describe('getJson error handling', () => {
  it('throws when response is not ok', async () => {
    fetchSpy.mockResolvedValueOnce(makeJsonResponse({}, { ok: false, status: 500 }));
    await expect(getCategories()).rejects.toThrow(/failed with status 500/);
  });

  it('wraps fetch network error with cause', async () => {
    const networkErr = new TypeError('NetworkError');
    fetchSpy.mockRejectedValueOnce(networkErr);
    await expect(getCategories()).rejects.toThrow(/failed before reaching the server/);
  });
});
