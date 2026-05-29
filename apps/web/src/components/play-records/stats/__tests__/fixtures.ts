import type { PlayerStatistics } from '@/lib/api/schemas/play-records.schemas';

export const mockPlayerStatisticsEmpty: PlayerStatistics = {
  totalSessions: 0,
  totalWins: 0,
  gamePlayCounts: {},
  averageScoresByGame: {},
  totalDurationMinutes: 0,
  winByGame: [],
  mostPlayedGames: [],
};

export const mockPlayerStatisticsFull: PlayerStatistics = {
  totalSessions: 42,
  totalWins: 18,
  gamePlayCounts: {
    Catan: 15,
    Carcassonne: 12,
    'Ticket to Ride': 8,
    Splendor: 5,
    'Puerto Rico': 2,
  },
  averageScoresByGame: {
    Catan: 65.5,
    Carcassonne: 120.3,
    'Ticket to Ride': 85.0,
    Splendor: 42.2,
    'Puerto Rico': 78.0,
  },
  totalDurationMinutes: 3180, // 53h
  winByGame: [
    { gameId: '550e8400-e29b-41d4-a716-446655440001', gameName: 'Catan', played: 15, won: 8 },
    { gameId: '550e8400-e29b-41d4-a716-446655440002', gameName: 'Carcassonne', played: 12, won: 6 },
    {
      gameId: '550e8400-e29b-41d4-a716-446655440003',
      gameName: 'Ticket to Ride',
      played: 8,
      won: 3,
    },
    { gameId: '550e8400-e29b-41d4-a716-446655440004', gameName: 'Splendor', played: 5, won: 1 },
  ],
  mostPlayedGames: [
    { gameId: '550e8400-e29b-41d4-a716-446655440001', gameName: 'Catan', plays: 15 },
    { gameId: '550e8400-e29b-41d4-a716-446655440002', gameName: 'Carcassonne', plays: 12 },
    { gameId: '550e8400-e29b-41d4-a716-446655440003', gameName: 'Ticket to Ride', plays: 8 },
    { gameId: '550e8400-e29b-41d4-a716-446655440004', gameName: 'Splendor', plays: 5 },
    { gameId: '550e8400-e29b-41d4-a716-446655440005', gameName: 'Puerto Rico', plays: 2 },
  ],
};

export const mockSharedGamesMap = new Map([
  [
    '550e8400-e29b-41d4-a716-446655440001',
    {
      id: '550e8400-e29b-41d4-a716-446655440001',
      title: 'Catan',
      coverEmoji: '🌾',
      coverUrl: 'https://example.com/catan.jpg',
    },
  ],
  [
    '550e8400-e29b-41d4-a716-446655440002',
    {
      id: '550e8400-e29b-41d4-a716-446655440002',
      title: 'Carcassonne',
      coverEmoji: '🏰',
      coverUrl: 'https://example.com/carcassonne.jpg',
    },
  ],
  [
    '550e8400-e29b-41d4-a716-446655440003',
    {
      id: '550e8400-e29b-41d4-a716-446655440003',
      title: 'Ticket to Ride',
      coverEmoji: '🚂',
      coverUrl: 'https://example.com/ttr.jpg',
    },
  ],
  [
    '550e8400-e29b-41d4-a716-446655440004',
    {
      id: '550e8400-e29b-41d4-a716-446655440004',
      title: 'Splendor',
      coverEmoji: '💎',
      coverUrl: 'https://example.com/splendor.jpg',
    },
  ],
  [
    '550e8400-e29b-41d4-a716-446655440005',
    {
      id: '550e8400-e29b-41d4-a716-446655440005',
      title: 'Puerto Rico',
      coverEmoji: '🏝️',
      coverUrl: 'https://example.com/puertorico.jpg',
    },
  ],
]);
