/**
 * BoardGameGeek API Client - Issue #4141
 *
 * Client for searching and fetching game details from BoardGameGeek.
 * Currently using mock data - will be replaced with real API calls in future iteration.
 */

import type { BggSearchResult, BggGameDetailsDto } from '@/types/bgg';

// Mock data for development
const MOCK_GAMES: BggGameDetailsDto[] = [
  {
    id: 13,
    name: 'Catan',
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    playingTime: 90,
    minAge: 10,
    rating: 7.2,
    thumbnail: 'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__thumb/img/M_3Vv1FU2UlNAVRFbKW7zxZAb5Y=/fit-in/200x150/filters:strip_icc()/pic2419375.jpg',
    description: 'Trade, build, and settle the island of Catan',
    categories: ['Economic', 'Negotiation'],
    mechanics: ['Trading', 'Dice Rolling', 'Route Building'],
  },
  {
    id: 822,
    name: 'Carcassonne',
    yearPublished: 2000,
    minPlayers: 2,
    maxPlayers: 5,
    playingTime: 45,
    minAge: 7,
    rating: 7.4,
    thumbnail: 'https://cf.geekdo-images.com/Z3upN53-fsVPUDimN9SpOA__thumb/img/sH0AM2Y14bxZdeLu6s4uI1f_-ZA=/fit-in/200x150/filters:strip_icc()/pic2337577.jpg',
    description: 'Shape the medieval landscape of France, tile by tile',
    categories: ['Medieval', 'City Building'],
    mechanics: ['Tile Placement', 'Area Control'],
  },
  {
    id: 9209,
    name: 'Ticket to Ride',
    yearPublished: 2004,
    minPlayers: 2,
    maxPlayers: 5,
    playingTime: 60,
    minAge: 8,
    rating: 7.4,
    thumbnail: 'https://cf.geekdo-images.com/ZWJg0dCdrWHxVnc0eFXK8w__thumb/img/t0xtS567TlN7L_mvLlYFF83blu4=/fit-in/200x150/filters:strip_icc()/pic66668.jpg',
    description: 'Build train routes across North America',
    categories: ['Trains', 'Transportation'],
    mechanics: ['Set Collection', 'Route Building', 'Hand Management'],
  },
  {
    id: 30549,
    name: 'Pandemic',
    yearPublished: 2008,
    minPlayers: 2,
    maxPlayers: 4,
    playingTime: 45,
    minAge: 8,
    rating: 7.6,
    thumbnail: 'https://cf.geekdo-images.com/S3ybV1LAp-8SnHIXLLjVqA__thumb/img/kIBu-K93wNrVI8l8JRgn8YRVHCE=/fit-in/200x150/filters:strip_icc()/pic1534148.jpg',
    description: 'Work together to save humanity from deadly diseases',
    categories: ['Medical', 'Cooperative'],
    mechanics: ['Cooperative Play', 'Action Points', 'Trading'],
  },
  {
    id: 68448,
    name: '7 Wonders',
    yearPublished: 2010,
    minPlayers: 2,
    maxPlayers: 7,
    playingTime: 30,
    minAge: 10,
    rating: 7.7,
    thumbnail: 'https://cf.geekdo-images.com/35h9Za_JvMMMtx_92kT0Jg__thumb/img/lVCdErCTHP8_T51BXo04nSGR85A=/fit-in/200x150/filters:strip_icc()/pic7149798.jpg',
    description: 'Draft cards to develop your ancient civilization',
    categories: ['Ancient', 'Card Game', 'Civilization'],
    mechanics: ['Card Drafting', 'Set Collection', 'Simultaneous Action'],
  },
];

/**
 * Search for games by title on BoardGameGeek
 *
 * @param query - Search query string
 * @returns Promise resolving to array of search results (max 10)
 */
export async function searchBggGames(
  query: string
): Promise<BggSearchResult[]> {
  // Simulate API delay
  await delay(500);

  // Mock search: filter games by name
  const lowerQuery = query.toLowerCase();
  const results = MOCK_GAMES.filter((game) =>
    game.name.toLowerCase().includes(lowerQuery)
  )
    .slice(0, 10)
    .map((game) => ({
      id: game.id,
      name: game.name,
      yearPublished: game.yearPublished,
      thumbnail: game.thumbnail,
    }));

  return results;
}

/**
 * Fetch detailed game information by BGG ID
 *
 * @param bggId - BoardGameGeek game ID
 * @returns Promise resolving to detailed game information
 * @throws Error if game not found
 */
export async function fetchBggGameById(
  bggId: number
): Promise<BggGameDetailsDto> {
  // Simulate API delay
  await delay(300);

  // Mock fetch: find game by ID
  const game = MOCK_GAMES.find((g) => g.id === bggId);

  if (!game) {
    throw new Error(`BGG game not found: ID ${bggId}`);
  }

  return game;
}

/**
 * Delay utility for simulating API latency
 */
function delay(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

// TODO: Replace with real BGG XML API calls
// API endpoints:
// - Search: https://boardgamegeek.com/xmlapi2/search?query={query}&type=boardgame
// - Details: https://boardgamegeek.com/xmlapi2/thing?id={id}&stats=1
// - Parse XML responses and transform to TypeScript types
