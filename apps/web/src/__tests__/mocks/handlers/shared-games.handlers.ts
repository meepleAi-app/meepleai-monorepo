/**
 * MSW handlers for shared games catalog endpoints
 *
 * Covers: /api/v1/shared-games/* routes
 * - Search community game catalog
 * - Get game details from shared catalog
 * - Import games to user library
 *
 * Issue #2760: MSW Infrastructure Setup
 * Issue #2763: Fixed to match actual API schema (SharedGameSchema, SharedGameDetailSchema)
 */

import { http, HttpResponse, delay } from 'msw';

// API base URL for MSW handlers - must match httpClient's getApiBase() in test environment
// In tests, httpClient uses 'http://localhost:8080' as base URL (see httpClient.ts:49-64)
const API_BASE = 'http://localhost:8080';

// Types matching the actual Zod schemas from shared-games.schemas.ts
interface GameCategory {
  id: string;
  name: string;
  slug: string;
}

interface GameMechanic {
  id: string;
  name: string;
  slug: string;
}

interface GameDesigner {
  id: string;
  name: string;
}

interface GamePublisher {
  id: string;
  name: string;
}

interface GameRules {
  content: string;
  language: string;
}

interface GameFaq {
  id: string;
  question: string;
  answer: string;
  order: number;
  createdAt: string;
}

interface GameErrata {
  id: string;
  description: string;
  pageReference: string;
  publishedDate: string;
  createdAt: string;
}

// SharedGame (list view) - matches SharedGameSchema
interface SharedGame {
  id: string;
  bggId: number | null;
  title: string;
  yearPublished: number;
  description: string;
  minPlayers: number;
  maxPlayers: number;
  playingTimeMinutes: number;
  minAge: number;
  complexityRating: number | null;
  averageRating: number | null;
  imageUrl: string;
  thumbnailUrl: string;
  status: number; // 0=Draft, 1=PendingApproval, 2=Published, 3=Archived
  createdAt: string;
  modifiedAt: string | null;
}

// SharedGameDetail (single game view) - matches SharedGameDetailSchema
interface SharedGameDetail extends SharedGame {
  rules: GameRules | null;
  createdBy: string;
  modifiedBy: string | null;
  faqs: GameFaq[];
  erratas: GameErrata[];
  designers: GameDesigner[];
  publishers: GamePublisher[];
  categories: GameCategory[];
  mechanics: GameMechanic[];
}

// Mock categories
const mockCategories: GameCategory[] = [
  { id: 'cat-strategy', name: 'Strategy', slug: 'strategy' },
  { id: 'cat-economic', name: 'Economic', slug: 'economic' },
  { id: 'cat-family', name: 'Family', slug: 'family' },
  { id: 'cat-trains', name: 'Trains', slug: 'trains' },
  { id: 'cat-animals', name: 'Animals', slug: 'animals' },
  { id: 'cat-card-game', name: 'Card Game', slug: 'card-game' },
  { id: 'cat-wargame', name: 'Wargame', slug: 'wargame' },
  { id: 'cat-abstract', name: 'Abstract', slug: 'abstract' },
  { id: 'cat-puzzle', name: 'Puzzle', slug: 'puzzle' },
];

// Mock mechanics
const mockMechanics: GameMechanic[] = [
  { id: 'mech-dice-rolling', name: 'Dice Rolling', slug: 'dice-rolling' },
  { id: 'mech-trading', name: 'Trading', slug: 'trading' },
  { id: 'mech-modular-board', name: 'Modular Board', slug: 'modular-board' },
  { id: 'mech-set-collection', name: 'Set Collection', slug: 'set-collection' },
  { id: 'mech-route-building', name: 'Route Building', slug: 'route-building' },
  { id: 'mech-engine-building', name: 'Engine Building', slug: 'engine-building' },
  { id: 'mech-hand-management', name: 'Hand Management', slug: 'hand-management' },
  { id: 'mech-area-control', name: 'Area Control', slug: 'area-control' },
  { id: 'mech-variable-player-powers', name: 'Variable Player Powers', slug: 'variable-player-powers' },
  { id: 'mech-pattern-building', name: 'Pattern Building', slug: 'pattern-building' },
  { id: 'mech-tile-placement', name: 'Tile Placement', slug: 'tile-placement' },
  { id: 'mech-drafting', name: 'Drafting', slug: 'drafting' },
];

// Helper to create ISO date strings
const isoDate = (daysAgo = 0) => {
  const date = new Date();
  date.setDate(date.getDate() - daysAgo);
  return date.toISOString();
};

// Shared games catalog mock data (matches SharedGameDetailSchema)
const sharedGamesCatalog: SharedGameDetail[] = [
  {
    id: 'game-1',
    bggId: 13,
    title: 'Catan',
    yearPublished: 1995,
    description: 'A game of trading and building settlements on the island of Catan.',
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 90,
    minAge: 10,
    complexityRating: 2.32,
    averageRating: 7.2,
    imageUrl: 'https://example.com/catan.jpg',
    thumbnailUrl: 'https://example.com/catan-thumb.jpg',
    status: 2, // Published
    createdAt: isoDate(365),
    modifiedAt: isoDate(30),
    createdBy: 'user-admin',
    modifiedBy: 'user-editor',
    rules: {
      content: '<h2>Setup</h2><p>Place the board in the center of the table.</p>',
      language: 'en',
    },
    faqs: [
      {
        id: 'faq-1',
        question: 'Can I trade on my first turn?',
        answer: 'Yes, you can trade resources on any turn.',
        order: 1,
        createdAt: isoDate(100),
      },
      {
        id: 'faq-2',
        question: 'How many roads can I build?',
        answer: 'You can build up to 15 roads.',
        order: 2,
        createdAt: isoDate(90),
      },
    ],
    erratas: [
      {
        id: 'errata-1',
        description: 'The rulebook incorrectly states the maximum hand size.',
        pageReference: '12',
        publishedDate: isoDate(200),
        createdAt: isoDate(200),
      },
    ],
    designers: [{ id: 'd1', name: 'Klaus Teuber' }],
    publishers: [{ id: 'p1', name: 'Kosmos' }],
    categories: [mockCategories[0], mockCategories[1]], // Strategy, Economic
    mechanics: [mockMechanics[0], mockMechanics[1], mockMechanics[2]], // Dice Rolling, Trading, Modular Board
  },
  {
    id: 'game-2',
    bggId: 9209,
    title: 'Ticket to Ride',
    yearPublished: 2004,
    description: 'Build train routes across North America.',
    minPlayers: 2,
    maxPlayers: 5,
    playingTimeMinutes: 60,
    minAge: 8,
    complexityRating: 1.85,
    averageRating: 7.5,
    imageUrl: 'https://example.com/ticket.jpg',
    thumbnailUrl: 'https://example.com/ticket-thumb.jpg',
    status: 2,
    createdAt: isoDate(300),
    modifiedAt: null,
    createdBy: 'user-admin',
    modifiedBy: null,
    rules: null,
    faqs: [],
    erratas: [],
    designers: [{ id: 'd2', name: 'Alan R. Moon' }],
    publishers: [{ id: 'p2', name: 'Days of Wonder' }],
    categories: [mockCategories[2], mockCategories[3]], // Family, Trains
    mechanics: [mockMechanics[3], mockMechanics[4]], // Set Collection, Route Building
  },
  {
    id: 'game-3',
    bggId: 266192,
    title: 'Wingspan',
    yearPublished: 2019,
    description: 'A competitive bird-collection engine-building game.',
    minPlayers: 1,
    maxPlayers: 5,
    playingTimeMinutes: 70,
    minAge: 10,
    complexityRating: 2.44,
    averageRating: 8.1,
    imageUrl: 'https://example.com/wingspan.jpg',
    thumbnailUrl: 'https://example.com/wingspan-thumb.jpg',
    status: 2,
    createdAt: isoDate(200),
    modifiedAt: isoDate(10),
    createdBy: 'user-admin',
    modifiedBy: 'user-editor',
    rules: {
      content: '<h2>Objective</h2><p>Attract birds to your wildlife preserve.</p>',
      language: 'en',
    },
    faqs: [],
    erratas: [],
    designers: [{ id: 'd3', name: 'Elizabeth Hargrave' }],
    publishers: [{ id: 'p3', name: 'Stonemaier Games' }],
    categories: [mockCategories[4], mockCategories[5]], // Animals, Card Game
    mechanics: [mockMechanics[5], mockMechanics[0], mockMechanics[6]], // Engine Building, Dice Rolling, Hand Management
  },
  {
    id: 'game-4',
    bggId: 169786,
    title: 'Scythe',
    yearPublished: 2016,
    description: 'Control mechs in an alternate-history 1920s Europe.',
    minPlayers: 1,
    maxPlayers: 5,
    playingTimeMinutes: 115,
    minAge: 14,
    complexityRating: 3.42,
    averageRating: 8.2,
    imageUrl: 'https://example.com/scythe.jpg',
    thumbnailUrl: 'https://example.com/scythe-thumb.jpg',
    status: 2,
    createdAt: isoDate(250),
    modifiedAt: null,
    createdBy: 'user-admin',
    modifiedBy: null,
    rules: null,
    faqs: [],
    erratas: [],
    designers: [{ id: 'd4', name: 'Jamey Stegmaier' }],
    publishers: [{ id: 'p3', name: 'Stonemaier Games' }],
    categories: [mockCategories[0], mockCategories[6]], // Strategy, Wargame
    mechanics: [mockMechanics[7], mockMechanics[5], mockMechanics[8]], // Area Control, Engine Building, Variable Player Powers
  },
  {
    id: 'game-5',
    bggId: 230802,
    title: 'Azul',
    yearPublished: 2017,
    description: 'Draft tiles to decorate the Royal Palace of Evora.',
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 45,
    minAge: 8,
    complexityRating: 1.78,
    averageRating: 7.8,
    imageUrl: 'https://example.com/azul.jpg',
    thumbnailUrl: 'https://example.com/azul-thumb.jpg',
    status: 2,
    createdAt: isoDate(180),
    modifiedAt: null,
    createdBy: 'user-admin',
    modifiedBy: null,
    rules: null,
    faqs: [],
    erratas: [],
    designers: [{ id: 'd5', name: 'Michael Kiesling' }],
    publishers: [{ id: 'p4', name: 'Plan B Games' }],
    categories: [mockCategories[7], mockCategories[8]], // Abstract, Puzzle
    mechanics: [mockMechanics[9], mockMechanics[10], mockMechanics[11]], // Pattern Building, Tile Placement, Drafting
  },
];

// Convert SharedGameDetail to SharedGame (list view)
const toSharedGame = (detail: SharedGameDetail): SharedGame => ({
  id: detail.id,
  bggId: detail.bggId,
  title: detail.title,
  yearPublished: detail.yearPublished,
  description: detail.description,
  minPlayers: detail.minPlayers,
  maxPlayers: detail.maxPlayers,
  playingTimeMinutes: detail.playingTimeMinutes,
  minAge: detail.minAge,
  complexityRating: detail.complexityRating,
  averageRating: detail.averageRating,
  imageUrl: detail.imageUrl,
  thumbnailUrl: detail.thumbnailUrl,
  status: detail.status,
  createdAt: detail.createdAt,
  modifiedAt: detail.modifiedAt,
});

export const sharedGamesHandlers = [
  // GET /api/v1/shared-games/categories - List all categories (MUST be before :id route)
  http.get(`${API_BASE}/api/v1/shared-games/categories`, () => {
    return HttpResponse.json(mockCategories);
  }),

  // GET /api/v1/shared-games/mechanics - List all mechanics (MUST be before :id route)
  http.get(`${API_BASE}/api/v1/shared-games/mechanics`, () => {
    return HttpResponse.json(mockMechanics);
  }),

  // GET /api/v1/shared-games - Search shared catalog (matches SearchSharedGamesParams)
  http.get(`${API_BASE}/api/v1/shared-games`, async ({ request }) => {
    const url = new URL(request.url);
    const searchTerm = url.searchParams.get('searchTerm') || '';
    const page = parseInt(url.searchParams.get('page') || '1');
    const pageSize = parseInt(url.searchParams.get('pageSize') || '20');
    const minPlayers = url.searchParams.get('minPlayers');
    const maxPlayers = url.searchParams.get('maxPlayers');
    const maxPlayingTime = url.searchParams.get('maxPlayingTime');
    const categoryIds = url.searchParams.get('categoryIds')?.split(',').filter(Boolean);
    const mechanicIds = url.searchParams.get('mechanicIds')?.split(',').filter(Boolean);
    const sortBy = url.searchParams.get('sortBy') || 'title';
    const sortDescending = url.searchParams.get('sortDescending') === 'true';

    // Simulate network delay for realistic testing
    await delay(50);

    let filtered = [...sharedGamesCatalog];

    // Text search on title and description
    if (searchTerm) {
      const term = searchTerm.toLowerCase();
      filtered = filtered.filter(
        (g) =>
          g.title.toLowerCase().includes(term) ||
          g.description.toLowerCase().includes(term)
      );
    }

    // Player count filter
    if (minPlayers) {
      filtered = filtered.filter((g) => g.maxPlayers >= parseInt(minPlayers));
    }
    if (maxPlayers) {
      filtered = filtered.filter((g) => g.minPlayers <= parseInt(maxPlayers));
    }

    // Playing time filter
    if (maxPlayingTime) {
      filtered = filtered.filter((g) => g.playingTimeMinutes <= parseInt(maxPlayingTime));
    }

    // Category filter (by ID)
    if (categoryIds && categoryIds.length > 0) {
      filtered = filtered.filter((g) =>
        g.categories.some((c) => categoryIds.includes(c.id))
      );
    }

    // Mechanic filter (by ID)
    if (mechanicIds && mechanicIds.length > 0) {
      filtered = filtered.filter((g) =>
        g.mechanics.some((m) => mechanicIds.includes(m.id))
      );
    }

    // Sort
    filtered.sort((a, b) => {
      let comparison = 0;
      switch (sortBy) {
        case 'title':
          comparison = a.title.localeCompare(b.title);
          break;
        case 'complexity':
          comparison = (a.complexityRating || 0) - (b.complexityRating || 0);
          break;
        case 'rating':
          comparison = (a.averageRating || 0) - (b.averageRating || 0);
          break;
        case 'yearPublished':
          comparison = a.yearPublished - b.yearPublished;
          break;
        default:
          comparison = a.title.localeCompare(b.title);
      }
      return sortDescending ? -comparison : comparison;
    });

    // Paginate
    const start = (page - 1) * pageSize;
    const paginatedItems = filtered.slice(start, start + pageSize);

    // Return PagedSharedGames format
    return HttpResponse.json({
      items: paginatedItems.map(toSharedGame),
      total: filtered.length,
      page,
      pageSize,
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

    // Return full SharedGameDetail
    return HttpResponse.json(game);
  }),

  // POST /api/v1/shared-games/:id/import - Import game to user library
  http.post(`${API_BASE}/api/v1/shared-games/:id/import`, async ({ params }) => {
    const { id } = params;

    const game = sharedGamesCatalog.find((g) => g.id === id);

    if (!game) {
      return HttpResponse.json({ error: 'Game not found' }, { status: 404 });
    }

    // Return CreatedResponse format
    return HttpResponse.json(
      { id: `lib-${Date.now()}` },
      { status: 201 }
    );
  }),
];

// Helper to reset shared games state
export const resetSharedGamesState = () => {
  // Currently stateless, but available for future use
};

// Helper to get mock data for tests
export const getMockSharedGame = (id: string): SharedGameDetail | undefined => {
  return sharedGamesCatalog.find((g) => g.id === id);
};

export const getMockCategories = (): GameCategory[] => mockCategories;
export const getMockMechanics = (): GameMechanic[] => mockMechanics;

// Helper to add games for testing
export const addSharedGame = (game: Partial<SharedGameDetail>): SharedGameDetail => {
  const newGame: SharedGameDetail = {
    id: game.id || `game-${Date.now()}`,
    bggId: game.bggId ?? null,
    title: game.title || 'Test Game',
    yearPublished: game.yearPublished || 2020,
    description: game.description || 'Test description',
    minPlayers: game.minPlayers || 2,
    maxPlayers: game.maxPlayers || 4,
    playingTimeMinutes: game.playingTimeMinutes || 60,
    minAge: game.minAge || 10,
    complexityRating: game.complexityRating ?? 2.5,
    averageRating: game.averageRating ?? 7.0,
    imageUrl: game.imageUrl || 'https://example.com/test.jpg',
    thumbnailUrl: game.thumbnailUrl || 'https://example.com/test-thumb.jpg',
    status: game.status ?? 2,
    createdAt: game.createdAt || isoDate(0),
    modifiedAt: game.modifiedAt ?? null,
    createdBy: game.createdBy || 'user-test',
    modifiedBy: game.modifiedBy ?? null,
    rules: game.rules ?? null,
    faqs: game.faqs || [],
    erratas: game.erratas || [],
    designers: game.designers || [],
    publishers: game.publishers || [],
    categories: game.categories || [],
    mechanics: game.mechanics || [],
  };
  sharedGamesCatalog.push(newGame);
  return newGame;
};
