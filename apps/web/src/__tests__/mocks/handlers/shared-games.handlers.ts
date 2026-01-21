/**
 * MSW handlers for shared games catalog endpoints
 *
 * Covers: /api/v1/shared-games/* routes
 * - Search community game catalog
 * - Get game details from shared catalog
 * - Import games to user library
 *
 * Issue #2760: MSW Infrastructure Setup
 */

import { http, HttpResponse, delay } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Shared games catalog mock data
interface SharedGame {
  id: string;
  bggId?: number;
  name: string;
  yearPublished?: number;
  description?: string;
  minPlayers: number;
  maxPlayers: number;
  playingTime: number;
  minPlayTime?: number;
  maxPlayTime?: number;
  complexity: number;
  rating: number;
  numRatings: number;
  thumbnailUrl?: string;
  imageUrl?: string;
  categories: string[];
  mechanics: string[];
  designers: string[];
  publishers: string[];
  isExpansion: boolean;
}

const sharedGamesCatalog: SharedGame[] = [
  {
    id: 'shared-catan',
    bggId: 13,
    name: 'Catan',
    yearPublished: 1995,
    description: 'Collect and trade resources to build settlements and roads.',
    minPlayers: 3,
    maxPlayers: 4,
    playingTime: 90,
    complexity: 2.32,
    rating: 7.2,
    numRatings: 125000,
    categories: ['strategy', 'economic'],
    mechanics: ['dice-rolling', 'trading', 'modular-board'],
    designers: ['Klaus Teuber'],
    publishers: ['Kosmos'],
    isExpansion: false,
  },
  {
    id: 'shared-ticket',
    bggId: 9209,
    name: 'Ticket to Ride',
    yearPublished: 2004,
    description: 'Build train routes across North America.',
    minPlayers: 2,
    maxPlayers: 5,
    playingTime: 60,
    complexity: 1.85,
    rating: 7.5,
    numRatings: 98000,
    categories: ['family', 'trains'],
    mechanics: ['set-collection', 'route-building'],
    designers: ['Alan R. Moon'],
    publishers: ['Days of Wonder'],
    isExpansion: false,
  },
  {
    id: 'shared-wingspan',
    bggId: 266192,
    name: 'Wingspan',
    yearPublished: 2019,
    description: 'A competitive bird-collection engine-building game.',
    minPlayers: 1,
    maxPlayers: 5,
    playingTime: 70,
    complexity: 2.44,
    rating: 8.1,
    numRatings: 75000,
    categories: ['animals', 'card-game'],
    mechanics: ['engine-building', 'dice-rolling', 'hand-management'],
    designers: ['Elizabeth Hargrave'],
    publishers: ['Stonemaier Games'],
    isExpansion: false,
  },
  {
    id: 'shared-scythe',
    bggId: 169786,
    name: 'Scythe',
    yearPublished: 2016,
    description: 'Control mechs in an alternate-history 1920s Europe.',
    minPlayers: 1,
    maxPlayers: 5,
    playingTime: 115,
    complexity: 3.42,
    rating: 8.2,
    numRatings: 85000,
    categories: ['strategy', 'wargame'],
    mechanics: ['area-control', 'engine-building', 'variable-player-powers'],
    designers: ['Jamey Stegmaier'],
    publishers: ['Stonemaier Games'],
    isExpansion: false,
  },
  {
    id: 'shared-azul',
    bggId: 230802,
    name: 'Azul',
    yearPublished: 2017,
    description: 'Draft tiles to decorate the Royal Palace of Evora.',
    minPlayers: 2,
    maxPlayers: 4,
    playingTime: 45,
    complexity: 1.78,
    rating: 7.8,
    numRatings: 65000,
    categories: ['abstract', 'puzzle'],
    mechanics: ['pattern-building', 'tile-placement', 'drafting'],
    designers: ['Michael Kiesling'],
    publishers: ['Plan B Games'],
    isExpansion: false,
  },
];

export const sharedGamesHandlers = [
  // GET /api/v1/shared-games/search - Search shared catalog
  http.get(`${API_BASE}/api/v1/shared-games/search`, async ({ request }) => {
    const url = new URL(request.url);
    const query = url.searchParams.get('q') || '';
    const page = parseInt(url.searchParams.get('page') || '1');
    const pageSize = parseInt(url.searchParams.get('pageSize') || '20');
    const minPlayers = url.searchParams.get('minPlayers');
    const maxPlayers = url.searchParams.get('maxPlayers');
    const minComplexity = url.searchParams.get('minComplexity');
    const maxComplexity = url.searchParams.get('maxComplexity');
    const categories = url.searchParams.get('categories')?.split(',').filter(Boolean);
    const mechanics = url.searchParams.get('mechanics')?.split(',').filter(Boolean);

    // Simulate network delay for realistic testing
    await delay(50);

    let filtered = [...sharedGamesCatalog];

    // Text search
    if (query) {
      const q = query.toLowerCase();
      filtered = filtered.filter(
        (g) =>
          g.name.toLowerCase().includes(q) ||
          g.description?.toLowerCase().includes(q) ||
          g.designers.some((d) => d.toLowerCase().includes(q))
      );
    }

    // Player count filter
    if (minPlayers) {
      filtered = filtered.filter((g) => g.maxPlayers >= parseInt(minPlayers));
    }
    if (maxPlayers) {
      filtered = filtered.filter((g) => g.minPlayers <= parseInt(maxPlayers));
    }

    // Complexity filter
    if (minComplexity) {
      filtered = filtered.filter((g) => g.complexity >= parseFloat(minComplexity));
    }
    if (maxComplexity) {
      filtered = filtered.filter((g) => g.complexity <= parseFloat(maxComplexity));
    }

    // Category filter
    if (categories && categories.length > 0) {
      filtered = filtered.filter((g) => categories.some((c) => g.categories.includes(c)));
    }

    // Mechanics filter
    if (mechanics && mechanics.length > 0) {
      filtered = filtered.filter((g) => mechanics.some((m) => g.mechanics.includes(m)));
    }

    // Paginate
    const start = (page - 1) * pageSize;
    const paginatedItems = filtered.slice(start, start + pageSize);

    return HttpResponse.json({
      items: paginatedItems,
      totalCount: filtered.length,
      page,
      pageSize,
      totalPages: Math.ceil(filtered.length / pageSize),
    });
  }),

  // GET /api/v1/shared-games/:id - Get game details
  http.get(`${API_BASE}/api/v1/shared-games/:id`, async ({ params }) => {
    const { id } = params;

    await delay(30);

    const game = sharedGamesCatalog.find((g) => g.id === id);

    if (!game) {
      return HttpResponse.json({ error: 'Game not found' }, { status: 404 });
    }

    return HttpResponse.json(game);
  }),

  // POST /api/v1/shared-games/:id/import - Import game to user library
  http.post(`${API_BASE}/api/v1/shared-games/:id/import`, async ({ params, request }) => {
    const { id } = params;
    const body = (await request.json()) as { status: string };

    const game = sharedGamesCatalog.find((g) => g.id === id);

    if (!game) {
      return HttpResponse.json({ error: 'Game not found' }, { status: 404 });
    }

    // Return the imported library item
    return HttpResponse.json(
      {
        id: `lib-${Date.now()}`,
        gameId: game.id,
        name: game.name,
        status: body.status || 'owned',
        playCount: 0,
        addedAt: new Date().toISOString(),
        sourceGame: game,
      },
      { status: 201 }
    );
  }),

  // GET /api/v1/shared-games/categories - List all categories
  http.get(`${API_BASE}/api/v1/shared-games/categories`, () => {
    const categoryCounts: Record<string, number> = {};

    sharedGamesCatalog.forEach((game) => {
      game.categories.forEach((cat) => {
        categoryCounts[cat] = (categoryCounts[cat] || 0) + 1;
      });
    });

    const categories = Object.entries(categoryCounts).map(([id, count]) => ({
      id,
      name: id.charAt(0).toUpperCase() + id.slice(1).replace('-', ' '),
      count,
    }));

    return HttpResponse.json(categories);
  }),

  // GET /api/v1/shared-games/mechanics - List all mechanics
  http.get(`${API_BASE}/api/v1/shared-games/mechanics`, () => {
    const mechanicCounts: Record<string, number> = {};

    sharedGamesCatalog.forEach((game) => {
      game.mechanics.forEach((mech) => {
        mechanicCounts[mech] = (mechanicCounts[mech] || 0) + 1;
      });
    });

    const mechanics = Object.entries(mechanicCounts).map(([id, count]) => ({
      id,
      name: id
        .split('-')
        .map((w) => w.charAt(0).toUpperCase() + w.slice(1))
        .join(' '),
      count,
    }));

    return HttpResponse.json(mechanics);
  }),
];

// Helper to reset shared games state
export const resetSharedGamesState = () => {
  // Currently stateless, but available for future use
};

// Helper to add games for testing
export const addSharedGame = (game: Partial<SharedGame>) => {
  const newGame: SharedGame = {
    id: `shared-${Date.now()}`,
    name: game.name || 'Test Game',
    minPlayers: game.minPlayers || 2,
    maxPlayers: game.maxPlayers || 4,
    playingTime: game.playingTime || 60,
    complexity: game.complexity || 2.5,
    rating: game.rating || 7.0,
    numRatings: game.numRatings || 1000,
    categories: game.categories || ['strategy'],
    mechanics: game.mechanics || ['hand-management'],
    designers: game.designers || ['Unknown'],
    publishers: game.publishers || ['Unknown'],
    isExpansion: game.isExpansion || false,
    ...game,
  };
  sharedGamesCatalog.push(newGame);
  return newGame;
};
