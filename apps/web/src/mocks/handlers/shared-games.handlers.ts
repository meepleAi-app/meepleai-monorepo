/**
 * MSW handlers for shared games catalog (browser-safe)
 * Covers: /api/v1/shared-games/*
 */
import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface SharedGame {
  id: string;
  bggId?: number;
  title: string;
  description?: string;
  publisher?: string;
  yearPublished?: number;
  minPlayers?: number;
  maxPlayers?: number;
  playingTimeMinutes?: number;
  complexityRating?: number;
  averageRating?: number;
  imageUrl?: string;
  categories: Array<{ id: string; name: string; slug: string }>;
  mechanics: Array<{ id: string; name: string; slug: string }>;
  designers: Array<{ id: string; name: string }>;
}

const sharedGames: SharedGame[] = [
  {
    id: 'sg-catan',
    bggId: 13,
    title: 'Catan',
    publisher: 'KOSMOS',
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    playingTimeMinutes: 90,
    complexityRating: 2.32,
    averageRating: 7.15,
    imageUrl:
      'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__thumb/img/8a9HeqFydO7UnHoiF_nBoBEVCBU=/fit-in/200x150/filters:strip_icc()/pic2419375.jpg',
    categories: [{ id: 'strategy', name: 'Strategy', slug: 'strategy' }],
    mechanics: [{ id: 'trading', name: 'Trading', slug: 'trading' }],
    designers: [{ id: 'd1', name: 'Klaus Teuber' }],
  },
  {
    id: 'sg-wingspan',
    bggId: 266192,
    title: 'Wingspan',
    publisher: 'Stonemaier Games',
    yearPublished: 2019,
    minPlayers: 1,
    maxPlayers: 5,
    playingTimeMinutes: 70,
    complexityRating: 2.45,
    averageRating: 8.1,
    imageUrl:
      'https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__thumb/img/NP9UNiCAbTSM7nABLhEe5oNiWlI=/fit-in/200x150/filters:strip_icc()/pic4458123.jpg',
    categories: [{ id: 'family', name: 'Family', slug: 'family' }],
    mechanics: [{ id: 'engine-building', name: 'Engine Building', slug: 'engine-building' }],
    designers: [{ id: 'd2', name: 'Elizabeth Hargrave' }],
  },
  {
    id: 'sg-pandemic',
    bggId: 30549,
    title: 'Pandemic',
    publisher: 'Z-Man Games',
    yearPublished: 2008,
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 45,
    complexityRating: 2.41,
    averageRating: 7.65,
    categories: [{ id: 'cooperative', name: 'Cooperative', slug: 'cooperative' }],
    mechanics: [{ id: 'hand-management', name: 'Hand Management', slug: 'hand-management' }],
    designers: [{ id: 'd3', name: 'Matt Leacock' }],
  },
  {
    id: 'sg-terraforming-mars',
    bggId: 167791,
    title: 'Terraforming Mars',
    publisher: 'FryxGames',
    yearPublished: 2016,
    minPlayers: 1,
    maxPlayers: 5,
    playingTimeMinutes: 120,
    complexityRating: 3.24,
    averageRating: 8.4,
    categories: [{ id: 'strategy', name: 'Strategy', slug: 'strategy' }],
    mechanics: [{ id: 'engine-building', name: 'Engine Building', slug: 'engine-building' }],
    designers: [{ id: 'd4', name: 'Jacob Fryxelius' }],
  },
  {
    id: 'sg-spirit-island',
    bggId: 162886,
    title: 'Spirit Island',
    publisher: 'Greater Than Games',
    yearPublished: 2017,
    minPlayers: 1,
    maxPlayers: 4,
    playingTimeMinutes: 120,
    complexityRating: 3.88,
    averageRating: 8.5,
    categories: [{ id: 'cooperative', name: 'Cooperative', slug: 'cooperative' }],
    mechanics: [{ id: 'area-control', name: 'Area Control', slug: 'area-control' }],
    designers: [{ id: 'd5', name: 'R. Eric Reuss' }],
  },
];

export const sharedGamesHandlers = [
  http.get(`${API_BASE}/api/v1/shared-games`, ({ request }) => {
    const url = new URL(request.url);
    const search = url.searchParams.get('search') || '';
    const page = parseInt(url.searchParams.get('page') || '1');
    const pageSize = parseInt(url.searchParams.get('pageSize') || '20');

    const filtered = search
      ? sharedGames.filter(g => g.title.toLowerCase().includes(search.toLowerCase()))
      : [...sharedGames];

    const start = (page - 1) * pageSize;
    return HttpResponse.json({
      items: filtered.slice(start, start + pageSize),
      totalCount: filtered.length,
      page,
      pageSize,
      totalPages: Math.ceil(filtered.length / pageSize),
    });
  }),

  http.get(`${API_BASE}/api/v1/shared-games/:id`, ({ params }) => {
    const game = sharedGames.find(g => g.id === params.id);
    if (!game) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(game);
  }),

  http.post(`${API_BASE}/api/v1/shared-games/:id/import`, ({ params }) => {
    const game = sharedGames.find(g => g.id === params.id);
    if (!game) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json({ success: true, gameId: `imported-${params.id}` }, { status: 201 });
  }),
];
