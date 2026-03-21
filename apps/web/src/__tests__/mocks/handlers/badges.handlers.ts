/**
 * MSW handlers for badge/gamification endpoints
 *
 * Covers: /api/v1/badges/* routes
 * - List badges, claim, toggle display
 * - Leaderboard
 */

import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface Badge {
  id: string;
  name: string;
  description: string;
  iconUrl: string;
  isEarned: boolean;
  isClaimed: boolean;
  isDisplayed: boolean;
  earnedAt?: string;
  category: string;
}

let badges: Badge[] = [
  {
    id: 'badge-1',
    name: 'First Game',
    description: 'Play your first game',
    iconUrl: '/badges/first-game.png',
    isEarned: true,
    isClaimed: false,
    isDisplayed: false,
    earnedAt: '2024-01-15T10:00:00Z',
    category: 'Getting Started',
  },
  {
    id: 'badge-2',
    name: 'Collector',
    description: 'Add 10 games to your library',
    iconUrl: '/badges/collector.png',
    isEarned: false,
    isClaimed: false,
    isDisplayed: false,
    category: 'Library',
  },
  {
    id: 'badge-3',
    name: 'Social Butterfly',
    description: 'Organize 5 game nights',
    iconUrl: '/badges/social.png',
    isEarned: true,
    isClaimed: true,
    isDisplayed: true,
    earnedAt: '2024-02-01T10:00:00Z',
    category: 'Social',
  },
];

export const badgesHandlers = [
  // GET /api/v1/badges
  http.get(`${API_BASE}/api/v1/badges`, () => {
    return HttpResponse.json({
      items: badges,
      totalCount: badges.length,
      earnedCount: badges.filter(b => b.isEarned).length,
    });
  }),

  // POST /api/v1/badges/:id/claim
  http.post(`${API_BASE}/api/v1/badges/:id/claim`, ({ params }) => {
    const badge = badges.find(b => b.id === params.id);
    if (!badge) {
      return HttpResponse.json({ error: 'Badge not found' }, { status: 404 });
    }
    if (!badge.isEarned) {
      return HttpResponse.json({ error: 'Badge not yet earned' }, { status: 400 });
    }
    badge.isClaimed = true;
    return HttpResponse.json(badge);
  }),

  // PUT /api/v1/badges/:id/display
  http.put(`${API_BASE}/api/v1/badges/:id/display`, async ({ params, request }) => {
    const body = (await request.json()) as { isDisplayed: boolean };
    const badge = badges.find(b => b.id === params.id);
    if (!badge) {
      return HttpResponse.json({ error: 'Badge not found' }, { status: 404 });
    }
    badge.isDisplayed = body.isDisplayed;
    return HttpResponse.json(badge);
  }),

  // GET /api/v1/badges/leaderboard
  http.get(`${API_BASE}/api/v1/badges/leaderboard`, () => {
    return HttpResponse.json({
      items: [
        { userId: 'user-1', displayName: 'Alice', totalBadges: 12, rank: 1 },
        { userId: 'user-2', displayName: 'Bob', totalBadges: 8, rank: 2 },
        { userId: 'user-3', displayName: 'Charlie', totalBadges: 5, rank: 3 },
      ],
    });
  }),
];

export const resetBadgesState = () => {
  badges = [
    {
      id: 'badge-1',
      name: 'First Game',
      description: 'Play your first game',
      iconUrl: '/badges/first-game.png',
      isEarned: true,
      isClaimed: false,
      isDisplayed: false,
      earnedAt: '2024-01-15T10:00:00Z',
      category: 'Getting Started',
    },
    {
      id: 'badge-2',
      name: 'Collector',
      description: 'Add 10 games to your library',
      iconUrl: '/badges/collector.png',
      isEarned: false,
      isClaimed: false,
      isDisplayed: false,
      category: 'Library',
    },
    {
      id: 'badge-3',
      name: 'Social Butterfly',
      description: 'Organize 5 game nights',
      iconUrl: '/badges/social.png',
      isEarned: true,
      isClaimed: true,
      isDisplayed: true,
      earnedAt: '2024-02-01T10:00:00Z',
      category: 'Social',
    },
  ];
};
