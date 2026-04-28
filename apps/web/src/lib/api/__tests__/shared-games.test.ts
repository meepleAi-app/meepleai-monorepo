import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  GameCategoryV2Schema,
  PagedSharedGamesV2Schema,
  SharedGameV2Schema,
  TopContributorSchema,
  getCategories,
  getTopContributors,
  searchSharedGames,
} from '../shared-games';

describe('SharedGameV2Schema', () => {
  it('accepts a minimal payload with all Wave A.3a defaults', () => {
    const minimal = {
      id: 'f47ac10b-58cc-4372-a567-0e02b2c3d479',
      bggId: null,
      title: 'Catan',
      yearPublished: 1995,
      description: '',
      minPlayers: 3,
      maxPlayers: 4,
      playingTimeMinutes: 60,
      minAge: 10,
      complexityRating: null,
      averageRating: null,
      imageUrl: '',
      thumbnailUrl: '',
      status: 'Published',
      createdAt: '2026-04-01T00:00:00Z',
      modifiedAt: null,
    };
    const parsed = SharedGameV2Schema.parse(minimal);
    // All A.3a defaults applied:
    expect(parsed.toolkitsCount).toBe(0);
    expect(parsed.agentsCount).toBe(0);
    expect(parsed.kbsCount).toBe(0);
    expect(parsed.newThisWeekCount).toBe(0);
    expect(parsed.contributorsCount).toBe(0);
    expect(parsed.isTopRated).toBe(false);
    expect(parsed.isNew).toBe(false);
    expect(parsed.hasKnowledgeBase).toBe(false);
    expect(parsed.isRagPublic).toBe(false);
  });

  it('accepts a full payload with all Wave A.3a fields populated', () => {
    const full = {
      id: 'f47ac10b-58cc-4372-a567-0e02b2c3d479',
      bggId: 13,
      title: 'Catan',
      yearPublished: 1995,
      description: 'Trade and build',
      minPlayers: 3,
      maxPlayers: 4,
      playingTimeMinutes: 60,
      minAge: 10,
      complexityRating: 2.3,
      averageRating: 4.1,
      imageUrl: 'https://cdn.example/c.jpg',
      thumbnailUrl: 'https://cdn.example/c-thumb.jpg',
      status: 'Published',
      createdAt: '2026-04-01T00:00:00Z',
      modifiedAt: '2026-04-15T00:00:00Z',
      isRagPublic: true,
      hasKnowledgeBase: true,
      toolkitsCount: 5,
      agentsCount: 3,
      kbsCount: 2,
      newThisWeekCount: 1,
      contributorsCount: 42,
      isTopRated: true,
      isNew: false,
    };
    const parsed = SharedGameV2Schema.parse(full);
    expect(parsed.toolkitsCount).toBe(5);
    expect(parsed.agentsCount).toBe(3);
    expect(parsed.kbsCount).toBe(2);
    expect(parsed.contributorsCount).toBe(42);
    expect(parsed.isTopRated).toBe(true);
  });
});

describe('PagedSharedGamesV2Schema', () => {
  it('accepts an empty page', () => {
    const parsed = PagedSharedGamesV2Schema.parse({
      items: [],
      total: 0,
      page: 1,
      pageSize: 20,
    });
    expect(parsed.items).toHaveLength(0);
    expect(parsed.total).toBe(0);
  });
});

describe('TopContributorSchema', () => {
  it('parses a minimal contributor', () => {
    const parsed = TopContributorSchema.parse({
      userId: 'c0a80101-7777-4123-8abc-def012345678',
      displayName: 'alice',
      avatarUrl: null,
      totalSessions: 10,
      totalWins: 4,
      score: 18,
    });
    expect(parsed.score).toBe(18);
    expect(parsed.avatarUrl).toBeNull();
  });
});

describe('GameCategoryV2Schema', () => {
  it('parses a category', () => {
    const parsed = GameCategoryV2Schema.parse({
      id: '12345678-90ab-4cde-9abc-fedcba987654',
      name: 'Strategy',
      slug: 'strategy',
    });
    expect(parsed.name).toBe('Strategy');
  });
});

// ========== Function-level integration with mocked fetch ==========

describe('searchSharedGames / getTopContributors / getCategories — fetch contract', () => {
  const originalFetch = globalThis.fetch;
  const mockFetch = vi.fn();

  beforeEach(() => {
    globalThis.fetch = mockFetch as typeof fetch;
    mockFetch.mockReset();
  });

  afterEach(() => {
    globalThis.fetch = originalFetch;
  });

  it('searchSharedGames builds a URL with paged defaults and parses the response', async () => {
    mockFetch.mockResolvedValueOnce(
      new Response(JSON.stringify({ items: [], total: 0, page: 1, pageSize: 20 }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    );

    const result = await searchSharedGames({});
    expect(mockFetch).toHaveBeenCalledTimes(1);
    const calledUrl = (mockFetch.mock.calls[0]?.[0] as string) ?? '';
    expect(calledUrl).toContain('/api/v1/shared-games?');
    expect(calledUrl).toContain('pageNumber=1');
    expect(calledUrl).toContain('pageSize=20');
    expect(result.total).toBe(0);
  });

  it('searchSharedGames serializes filter chips and search', async () => {
    mockFetch.mockResolvedValueOnce(
      new Response(JSON.stringify({ items: [], total: 0, page: 1, pageSize: 20 }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    );

    await searchSharedGames({
      search: 'catan',
      hasToolkit: true,
      hasAgent: false,
      isTopRated: true,
      sortBy: 'AverageRating',
      sortDescending: true,
    });
    const calledUrl = mockFetch.mock.calls[0]?.[0] as string;
    expect(calledUrl).toContain('search=catan');
    expect(calledUrl).toContain('hasToolkit=true');
    expect(calledUrl).toContain('hasAgent=false');
    expect(calledUrl).toContain('isTopRated=true');
    expect(calledUrl).toContain('sortBy=AverageRating');
    expect(calledUrl).toContain('sortDescending=true');
  });

  it('searchSharedGames repeats categoryIds query param for multi-Guid filters', async () => {
    mockFetch.mockResolvedValueOnce(
      new Response(JSON.stringify({ items: [], total: 0, page: 1, pageSize: 20 }), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    );

    await searchSharedGames({
      categoryIds: ['aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee', 'bbbbbbbb-cccc-4ddd-9eee-ffffffffffff'],
    });
    const calledUrl = mockFetch.mock.calls[0]?.[0] as string;
    expect((calledUrl.match(/categoryIds=/g) ?? []).length).toBe(2);
    expect(calledUrl).toContain('categoryIds=aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee');
    expect(calledUrl).toContain('categoryIds=bbbbbbbb-cccc-4ddd-9eee-ffffffffffff');
  });

  it('searchSharedGames throws when the server returns 5xx', async () => {
    mockFetch.mockResolvedValueOnce(new Response('boom', { status: 500 }));
    await expect(searchSharedGames({})).rejects.toThrow(/status 500/);
  });

  it('searchSharedGames throws when fetch itself fails (network error)', async () => {
    mockFetch.mockRejectedValueOnce(new TypeError('fetch failed'));
    await expect(searchSharedGames({})).rejects.toThrow(/before reaching the server/);
  });

  it('getTopContributors clamps limit to [1, 20] and parses arrays', async () => {
    mockFetch.mockResolvedValueOnce(
      new Response(JSON.stringify([]), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    );
    await getTopContributors(50);
    const calledUrl = mockFetch.mock.calls[0]?.[0] as string;
    expect(calledUrl).toContain('limit=20');

    mockFetch.mockResolvedValueOnce(
      new Response(JSON.stringify([]), {
        status: 200,
        headers: { 'Content-Type': 'application/json' },
      })
    );
    await getTopContributors(0);
    const calledUrl2 = mockFetch.mock.calls[1]?.[0] as string;
    expect(calledUrl2).toContain('limit=1');
  });

  it('getCategories hits /shared-games/categories and parses the array', async () => {
    mockFetch.mockResolvedValueOnce(
      new Response(
        JSON.stringify([
          { id: '12345678-90ab-4cde-9abc-fedcba987654', name: 'Strategy', slug: 'strategy' },
        ]),
        { status: 200, headers: { 'Content-Type': 'application/json' } }
      )
    );
    const cats = await getCategories();
    expect(cats).toHaveLength(1);
    expect(cats[0]?.name).toBe('Strategy');
    const calledUrl = mockFetch.mock.calls[0]?.[0] as string;
    expect(calledUrl).toContain('/api/v1/shared-games/categories');
  });
});
