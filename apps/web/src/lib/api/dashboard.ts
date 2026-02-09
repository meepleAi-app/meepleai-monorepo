/**
 * Dashboard API Client - Issue #3975
 *
 * Client for `/api/v1/dashboard` aggregated endpoint
 *
 * @see Epic #3901 - Dashboard Hub Core (MVP)
 * @see Backend Issue #3972 - Dashboard Aggregated API Endpoint
 */

import type { DashboardData } from '@/types/dashboard';

// ============================================================================
// Mock Data (Temporary - until Backend #3972 is implemented)
// ============================================================================

const MOCK_DASHBOARD_DATA: DashboardData = {
  user: {
    id: '123e4567-e89b-12d3-a456-426614174000',
    username: 'Marco',
    email: 'marco@example.com',
    avatarUrl: undefined,
  },
  stats: {
    libraryCount: 127,
    playedLast30Days: 23,
    chatCount: 12,
    wishlistCount: 15,
    currentStreak: 7,
  },
  activeSessions: [
    {
      id: 'session-1',
      gameName: 'Catan',
      gameId: 'game-catan',
      coverUrl: 'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__imagepage/img/M_3Vg1j2HlNgkv7PL2xl0jq30JY=/fit-in/900x600/filters:no_upscale():strip_icc()/pic2419375.jpg',
      players: { current: 3, total: 4 },
      progress: { turn: 12, duration: '45min' },
      lastActivity: new Date('2026-01-20T14:30:00Z'),
    },
    {
      id: 'session-2',
      gameName: 'Ticket to Ride',
      gameId: 'game-ttr',
      coverUrl: 'https://cf.geekdo-images.com/ZWJg0dCdrWHxVnc0eFXK8w__imagepage/img/4GwvqjsH3GiL2V0KuY6l9b0Klqc=/fit-in/900x600/filters:no_upscale():strip_icc()/pic66668.jpg',
      players: { current: 2, total: 5 },
      progress: { turn: 8, duration: '30min' },
      lastActivity: new Date('2026-01-19T18:00:00Z'),
    },
  ],
  librarySnapshot: {
    quota: { used: 127, total: 200 },
    topGames: [
      {
        id: 'game-1',
        title: 'Catan',
        coverUrl: 'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__imagepage/img/M_3Vg1j2HlNgkv7PL2xl0jq30JY=/fit-in/900x600/filters:no_upscale():strip_icc()/pic2419375.jpg',
        rating: 5,
        playCount: 45,
      },
      {
        id: 'game-2',
        title: 'Ticket to Ride',
        coverUrl: 'https://cf.geekdo-images.com/ZWJg0dCdrWHxVnc0eFXK8w__imagepage/img/4GwvqjsH3GiL2V0KuY6l9b0Klqc=/fit-in/900x600/filters:no_upscale():strip_icc()/pic66668.jpg',
        rating: 4,
        playCount: 32,
      },
      {
        id: 'game-3',
        title: 'Azul',
        coverUrl: 'https://cf.geekdo-images.com/aPSHJO0d0XOpQR5X-wJonw__imagepage/img/q4uWd2nXGeEkKDR8Cc2JOrHLiIo=/fit-in/900x600/filters:no_upscale():strip_icc()/pic6973671.png',
        rating: 4,
        playCount: 28,
      },
    ],
  },
  recentActivity: [
    {
      id: 'activity-1',
      type: 'game_added',
      gameId: 'game-wingspan',
      gameName: 'Wingspan',
      timestamp: new Date('2026-02-09T15:00:00Z'),
    },
    {
      id: 'activity-2',
      type: 'session_completed',
      sessionId: 'session-1',
      gameName: 'Catan',
      timestamp: new Date('2026-02-09T14:30:00Z'),
    },
    {
      id: 'activity-3',
      type: 'chat_saved',
      chatId: 'chat-1',
      topic: 'Regole Wingspan',
      timestamp: new Date('2026-02-08T20:15:00Z'),
    },
    {
      id: 'activity-4',
      type: 'wishlist_added',
      gameId: 'game-terraforming',
      gameName: 'Terraforming Mars',
      timestamp: new Date('2026-01-19T18:00:00Z'),
    },
    {
      id: 'activity-5',
      type: 'session_completed',
      sessionId: 'session-azul',
      gameName: 'Azul',
      timestamp: new Date('2026-01-18T21:00:00Z'),
    },
  ],
  chatHistory: [
    {
      id: 'chat-1',
      topic: 'Regole Wingspan - Setup iniziale',
      messageCount: 8,
      timestamp: new Date('2026-02-09T14:30:00Z'),
    },
    {
      id: 'chat-2',
      topic: 'Strategie Catan - Espansione Marinai',
      messageCount: 12,
      timestamp: new Date('2026-02-08T20:00:00Z'),
    },
    {
      id: 'chat-3',
      topic: 'FAQ Ticket to Ride - Carte duplicate',
      messageCount: 5,
      timestamp: new Date('2026-01-18T19:30:00Z'),
    },
    {
      id: 'chat-4',
      topic: 'Setup Azul - Modalità 2 giocatori',
      messageCount: 6,
      timestamp: new Date('2026-01-17T21:00:00Z'),
    },
  ],
};

// ============================================================================
// API Client
// ============================================================================

const API_BASE_URL = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';

/**
 * Mock data toggle for development
 *
 * Backend Issue #3972 (Dashboard Aggregated API) is not yet complete.
 * To switch to real API: add `NEXT_PUBLIC_USE_MOCK_DASHBOARD=false` to `.env.local`
 * Any other value (or omitting the var) keeps mock data active.
 *
 * @default true - mock data is ON until backend #3972 is deployed
 */
const USE_MOCK_DATA = process.env.NEXT_PUBLIC_USE_MOCK_DASHBOARD !== 'false';

/**
 * Fetch aggregated dashboard data
 *
 * @returns Promise<DashboardData>
 * @throws Error if API request fails
 */
export async function fetchDashboardData(): Promise<DashboardData> {
  // Use mock data during development (until Backend #3972 is complete)
  if (USE_MOCK_DATA) {
    // Simulate network delay
    await new Promise((resolve) => setTimeout(resolve, 300));
    return Promise.resolve(MOCK_DASHBOARD_DATA);
  }

  // Real API call (when backend is ready)
  const response = await fetch(`${API_BASE_URL}/api/v1/dashboard`, {
    method: 'GET',
    headers: {
      'Content-Type': 'application/json',
      // Add auth token when ready
      // Authorization: `Bearer ${getAuthToken()}`,
    },
    credentials: 'include',
  });

  if (!response.ok) {
    throw new Error(`Dashboard API error: ${response.status} ${response.statusText}`);
  }

  const data = await response.json();
  return data;
}

/**
 * Invalidate dashboard cache (for cache invalidation strategy)
 *
 * @see Backend Issue #3974 - Cache Invalidation Strategy
 */
export async function invalidateDashboardCache(): Promise<void> {
  if (USE_MOCK_DATA) {
    return Promise.resolve();
  }

  await fetch(`${API_BASE_URL}/api/v1/dashboard/invalidate`, {
    method: 'POST',
    credentials: 'include',
  });
}
