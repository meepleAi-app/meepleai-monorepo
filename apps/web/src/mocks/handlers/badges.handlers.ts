/**
 * MSW handlers for badge/gamification endpoints (browser-safe)
 * Covers: /api/v1/badges/*
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

const badges: Badge[] = [
  {
    id: 'badge-1',
    name: 'Prima Partita',
    description: 'Gioca la tua prima partita',
    iconUrl: '/badges/first-game.png',
    isEarned: true,
    isClaimed: false,
    isDisplayed: false,
    earnedAt: '2024-01-15T10:00:00Z',
    category: 'Getting Started',
  },
  {
    id: 'badge-2',
    name: 'Collezionista',
    description: 'Aggiungi 10 giochi alla libreria',
    iconUrl: '/badges/collector.png',
    isEarned: false,
    isClaimed: false,
    isDisplayed: false,
    category: 'Library',
  },
  {
    id: 'badge-3',
    name: 'Organizzatore',
    description: 'Organizza 5 game night',
    iconUrl: '/badges/social.png',
    isEarned: true,
    isClaimed: true,
    isDisplayed: true,
    earnedAt: '2024-02-01T10:00:00Z',
    category: 'Social',
  },
];

export const badgesHandlers = [
  http.get(`${API_BASE}/api/v1/badges`, () => HttpResponse.json(badges)),

  // leaderboard MUST be registered before /:id — MSW matches in order
  http.get(`${API_BASE}/api/v1/badges/leaderboard`, () => {
    return HttpResponse.json({
      items: [
        { rank: 1, userId: 'user-3', displayName: 'Carol', badgeCount: 12 },
        { rank: 2, userId: 'user-1', displayName: 'Alice', badgeCount: 8 },
        { rank: 3, userId: 'user-2', displayName: 'Bob', badgeCount: 5 },
      ],
      totalCount: 3,
    });
  }),

  http.get(`${API_BASE}/api/v1/badges/:id`, ({ params }) => {
    const badge = badges.find(b => b.id === params.id);
    if (!badge) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(badge);
  }),

  http.post(`${API_BASE}/api/v1/badges/:id/claim`, ({ params }) => {
    const badge = badges.find(b => b.id === params.id);
    if (!badge) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    if (!badge.isEarned) return HttpResponse.json({ error: 'Badge not earned' }, { status: 400 });
    badge.isClaimed = true;
    return HttpResponse.json(badge);
  }),

  http.put(`${API_BASE}/api/v1/badges/:id/display`, async ({ params, request }) => {
    const badge = badges.find(b => b.id === params.id);
    if (!badge) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = (await request.json()) as { display: boolean };
    badge.isDisplayed = body.display;
    return HttpResponse.json(badge);
  }),
];

// Helper to reset badge state between tests
export const resetBadgesState = () => {
  badges.splice(
    0,
    badges.length,
    {
      id: 'badge-1',
      name: 'Prima Partita',
      description: 'Gioca la tua prima partita',
      iconUrl: '/badges/first-game.png',
      isEarned: true,
      isClaimed: false,
      isDisplayed: false,
      earnedAt: '2024-01-15T10:00:00Z',
      category: 'Getting Started',
    },
    {
      id: 'badge-2',
      name: 'Collezionista',
      description: 'Aggiungi 10 giochi alla libreria',
      iconUrl: '/badges/collector.png',
      isEarned: false,
      isClaimed: false,
      isDisplayed: false,
      category: 'Library',
    },
    {
      id: 'badge-3',
      name: 'Organizzatore',
      description: 'Organizza 5 game night',
      iconUrl: '/badges/social.png',
      isEarned: true,
      isClaimed: true,
      isDisplayed: true,
      earnedAt: '2024-02-01T10:00:00Z',
      category: 'Social',
    }
  );
};
