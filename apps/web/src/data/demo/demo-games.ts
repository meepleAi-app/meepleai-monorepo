/**
 * Demo Game Fixtures
 * Issue #4781: Mock data for 4 demo games
 *
 * Each game matches the SharedGame schema from shared-games.schemas.ts
 * with realistic BGG data for demo/testing purposes.
 */

import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';

// ============================================================================
// Demo Game IDs (stable UUIDs for cross-reference)
// ============================================================================

export const DEMO_GAME_IDS = {
  catan: 'a1b2c3d4-0001-4000-8000-000000000001',
  descent: 'a1b2c3d4-0002-4000-8000-000000000002',
  ticketToRide: 'a1b2c3d4-0003-4000-8000-000000000003',
  pandemic: 'a1b2c3d4-0004-4000-8000-000000000004',
} as const;

// ============================================================================
// Demo Games
// ============================================================================

export const demoCatan: SharedGame = {
  id: DEMO_GAME_IDS.catan,
  bggId: 13,
  title: 'Catan',
  yearPublished: 1995,
  description:
    'In Catan, i giocatori cercano di essere la forza dominante sull\'isola di Catan costruendo insediamenti, città e strade. In ogni turno si lanciano i dadi per determinare quali risorse produce l\'isola. I giocatori raccolgono queste risorse (carte) per costruire le proprie civiltà e guadagnare punti vittoria.',
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 90,
  minAge: 10,
  complexityRating: 2.32,
  averageRating: 7.1,
  imageUrl: 'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__original/img/o-J27MVjJIeyPDzNjv-o-poXfWc=/0x0/filters:format(jpeg)/pic2419375.jpg',
  thumbnailUrl: 'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__thumb/img/IxiJIW_YMhbsMJCqfGBu74vjn7Q=/fit-in/200x150/filters:strip_icc()/pic2419375.jpg',
  status: 'Published',
  createdAt: '2024-01-15T10:00:00.000Z',
  modifiedAt: null,
};

export const demoDescent: SharedGame = {
  id: DEMO_GAME_IDS.descent,
  bggId: 104162,
  title: 'Descent: Leggende delle Tenebre',
  yearPublished: 2021,
  description:
    'Descent: Leggende delle Tenebre è un gioco cooperativo dungeon-crawler per 1-4 giocatori. Esplora dungeon oscuri, combatti mostri e scopri storie epiche in un\'avventura guidata dall\'app companion. Ogni eroe ha abilità uniche e il gioco evolve attraverso una campagna narrativa.',
  minPlayers: 1,
  maxPlayers: 4,
  playingTimeMinutes: 120,
  minAge: 14,
  complexityRating: 3.15,
  averageRating: 7.8,
  imageUrl: 'https://cf.geekdo-images.com/wtrBhGMRci-NpGMTtMz5gA__original/img/asPMz6aeR-G3VT2A-4mTr-YG_Jo=/0x0/filters:format(jpeg)/pic5875738.jpg',
  thumbnailUrl: 'https://cf.geekdo-images.com/wtrBhGMRci-NpGMTtMz5gA__thumb/img/6xqIxnwg5x6k3VKVvF-ohFElGKw=/fit-in/200x150/filters:strip_icc()/pic5875738.jpg',
  status: 'Published',
  createdAt: '2024-01-15T10:00:00.000Z',
  modifiedAt: null,
};

export const demoTicketToRide: SharedGame = {
  id: DEMO_GAME_IDS.ticketToRide,
  bggId: 9209,
  title: 'Ticket to Ride',
  yearPublished: 2004,
  description:
    'Ticket to Ride è un\'avventura ferroviaria cross-country in cui i giocatori raccolgono carte di vari tipi di vagoni per reclamare tratte ferroviarie che collegano le città. Più lunghe sono le tratte, più punti si guadagnano. Punti bonus per chi completa le carte destinazione.',
  minPlayers: 2,
  maxPlayers: 5,
  playingTimeMinutes: 60,
  minAge: 8,
  complexityRating: 1.83,
  averageRating: 7.4,
  imageUrl: 'https://cf.geekdo-images.com/ZWJg0dCdrWHxVnc0eFXK8w__original/img/pWR_bQKIT7g4xSfuCyDYnGh4Q3c=/0x0/filters:format(jpeg)/pic38668.jpg',
  thumbnailUrl: 'https://cf.geekdo-images.com/ZWJg0dCdrWHxVnc0eFXK8w__thumb/img/nXtfFiS9pnNqe8p4UaCgWS-HYQQ=/fit-in/200x150/filters:strip_icc()/pic38668.jpg',
  status: 'Published',
  createdAt: '2024-01-15T10:00:00.000Z',
  modifiedAt: null,
};

export const demoPandemic: SharedGame = {
  id: DEMO_GAME_IDS.pandemic,
  bggId: 30549,
  title: 'Pandemic',
  yearPublished: 2008,
  description:
    'In Pandemic, diversi virus letali si diffondono nel mondo e il tuo team di specialisti deve trovare le cure prima che sia troppo tardi. I giocatori devono cooperare, utilizzando i punti di forza unici di ogni ruolo per prevenire focolai e epidemie mentre sviluppano le cure.',
  minPlayers: 2,
  maxPlayers: 4,
  playingTimeMinutes: 45,
  minAge: 8,
  complexityRating: 2.41,
  averageRating: 7.6,
  imageUrl: 'https://cf.geekdo-images.com/S3ybV1LAp-8SnHIXLLn0PA__original/img/S_0yEGpAGCdFBnHPsA7jxVCqras=/0x0/filters:format(jpeg)/pic1534148.jpg',
  thumbnailUrl: 'https://cf.geekdo-images.com/S3ybV1LAp-8SnHIXLLn0PA__thumb/img/P4VNpS7JMh-p01SQNPTkE_XJeOQ=/fit-in/200x150/filters:strip_icc()/pic1534148.jpg',
  status: 'Published',
  createdAt: '2024-01-15T10:00:00.000Z',
  modifiedAt: null,
};

// ============================================================================
// Exports
// ============================================================================

/** All 4 demo games as an array */
export const DEMO_GAMES: SharedGame[] = [
  demoCatan,
  demoDescent,
  demoTicketToRide,
  demoPandemic,
];

/** Lookup by ID */
export const DEMO_GAMES_BY_ID: Record<string, SharedGame> = Object.fromEntries(
  DEMO_GAMES.map(g => [g.id, g])
);
