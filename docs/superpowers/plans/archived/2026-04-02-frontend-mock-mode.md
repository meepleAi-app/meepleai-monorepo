# Frontend Mock Mode — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Permettere `pnpm dev:mock` per sviluppare il frontend senza backend, intercettando tutte le fetch con MSW ServiceWorker.

**Architecture:** Quattro handler già esistono in `src/mocks/handlers/`. Si aggiungono i 9 restanti domain handler, si crea `handlers/index.ts` che li aggrega, si crea `MockProvider.tsx` (Client Component) che avvia il worker in `useEffect`, si aggiorna `providers.tsx` per renderizzarlo condizionalmente, si aggiunge lo script `dev:mock` e si aggiorna Storybook.

**Tech Stack:** MSW 2.x (`msw/browser` setupWorker), Next.js 16 App Router, TypeScript, pnpm, msw-storybook-addon (già installato).

---

## Stato iniziale

Questi file esistono già e non vanno modificati:
- `src/mocks/browser.ts` ✅ — setupWorker già configurato
- `src/mocks/node.ts` ✅ — setupServer già configurato
- `src/mocks/data/factories.ts` ✅ — factory browser-safe complete
- `src/mocks/handlers/auth.handlers.ts` ✅
- `src/mocks/handlers/games.handlers.ts` ✅
- `src/mocks/handlers/chat.handlers.ts` ✅
- `src/mocks/handlers/documents.handlers.ts` ✅

Questi file mancano e vanno creati in ordine:

```
src/mocks/handlers/
  ├── library.handlers.ts        ← Task 1
  ├── shared-games.handlers.ts   ← Task 2
  ├── catalog.handlers.ts        ← Task 3
  ├── admin.handlers.ts          ← Task 4
  ├── sessions.handlers.ts       ← Task 5
  ├── game-nights.handlers.ts    ← Task 6
  ├── players.handlers.ts        ← Task 7
  ├── notifications.handlers.ts  ← Task 8
  ├── badges.handlers.ts         ← Task 9
  └── index.ts                   ← Task 10
src/app/mock-provider.tsx         ← Task 11
(modifica) src/app/providers.tsx  ← Task 12
(modifica) apps/web/package.json  ← Task 13
public/mockServiceWorker.js       ← Task 14 (generato da MSW CLI)
(modifica) .storybook/preview.tsx ← Task 15
```

---

## Task 1: `src/mocks/handlers/library.handlers.ts`

**File:** Create `apps/web/src/mocks/handlers/library.handlers.ts`

- [ ] **Step 1: Creare il file**

```typescript
/**
 * MSW handlers for library endpoints (browser-safe)
 * Covers: /api/v1/library/*
 */
import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface LibraryItem {
  id: string;
  gameId: string;
  name: string;
  status: 'owned' | 'wishlist' | 'played' | 'want_to_play';
  rating?: number;
  playCount: number;
  notes?: string;
  addedAt: string;
  lastPlayedAt?: string;
}

let libraryItems: LibraryItem[] = [
  { id: 'lib-1', gameId: 'catan-1', name: 'Catan', status: 'owned', rating: 8, playCount: 15, addedAt: '2024-01-15T10:00:00Z', lastPlayedAt: '2024-12-01T18:30:00Z' },
  { id: 'lib-2', gameId: 'ticket-1', name: 'Ticket to Ride', status: 'owned', rating: 9, playCount: 22, addedAt: '2024-02-20T14:00:00Z', lastPlayedAt: '2024-12-15T20:00:00Z' },
  { id: 'lib-3', gameId: 'wingspan-1', name: 'Wingspan', status: 'wishlist', playCount: 0, addedAt: '2024-06-10T09:00:00Z' },
  { id: 'lib-4', gameId: 'scythe-1', name: 'Scythe', status: 'want_to_play', playCount: 2, addedAt: '2024-03-05T11:00:00Z', lastPlayedAt: '2024-08-20T15:00:00Z' },
];

export const libraryHandlers = [
  http.get(`${API_BASE}/api/v1/library/stats`, () => {
    return HttpResponse.json({
      totalGames: libraryItems.length,
      owned: libraryItems.filter(i => i.status === 'owned').length,
      wishlist: libraryItems.filter(i => i.status === 'wishlist').length,
      played: libraryItems.filter(i => i.status === 'played').length,
      wantToPlay: libraryItems.filter(i => i.status === 'want_to_play').length,
      totalPlays: libraryItems.reduce((sum, i) => sum + i.playCount, 0),
      averageRating: libraryItems.filter(i => i.rating).reduce((sum, i) => sum + (i.rating || 0), 0) / (libraryItems.filter(i => i.rating).length || 1),
    });
  }),

  http.get(`${API_BASE}/api/v1/library`, ({ request }) => {
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const page = parseInt(url.searchParams.get('page') || '1');
    const pageSize = parseInt(url.searchParams.get('pageSize') || '20');
    let filtered = status ? libraryItems.filter(i => i.status === status) : [...libraryItems];
    const start = (page - 1) * pageSize;
    return HttpResponse.json({
      items: filtered.slice(start, start + pageSize),
      totalCount: filtered.length,
      page,
      pageSize,
      totalPages: Math.ceil(filtered.length / pageSize),
    });
  }),

  http.get(`${API_BASE}/api/v1/library/:id`, ({ params }) => {
    const item = libraryItems.find(i => i.id === params.id);
    if (!item) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(item);
  }),

  http.post(`${API_BASE}/api/v1/library`, async ({ request }) => {
    const body = await request.json() as { gameId: string; name: string; status: LibraryItem['status'] };
    if (libraryItems.find(i => i.gameId === body.gameId)) {
      return HttpResponse.json({ error: 'Already in library' }, { status: 409 });
    }
    const newItem: LibraryItem = { id: `lib-${Date.now()}`, gameId: body.gameId, name: body.name, status: body.status, playCount: 0, addedAt: new Date().toISOString() };
    libraryItems.push(newItem);
    return HttpResponse.json(newItem, { status: 201 });
  }),

  http.put(`${API_BASE}/api/v1/library/:id`, async ({ params, request }) => {
    const idx = libraryItems.findIndex(i => i.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = await request.json() as Partial<LibraryItem>;
    libraryItems[idx] = { ...libraryItems[idx], ...body };
    return HttpResponse.json(libraryItems[idx]);
  }),

  http.delete(`${API_BASE}/api/v1/library/:id`, ({ params }) => {
    const idx = libraryItems.findIndex(i => i.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    libraryItems.splice(idx, 1);
    return HttpResponse.json({ success: true });
  }),

  http.post(`${API_BASE}/api/v1/library/:id/play`, async ({ params }) => {
    const idx = libraryItems.findIndex(i => i.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    libraryItems[idx] = { ...libraryItems[idx], playCount: libraryItems[idx].playCount + 1, lastPlayedAt: new Date().toISOString() };
    return HttpResponse.json(libraryItems[idx]);
  }),
];
```

- [ ] **Step 2: Verificare che compili senza errori TypeScript**

```bash
cd apps/web
npx tsc --noEmit --skipLibCheck 2>&1 | grep "library.handlers" || echo "OK - nessun errore nel file"
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/mocks/handlers/library.handlers.ts
git commit -m "feat(mock): add library handlers to src/mocks"
```

---

## Task 2: `src/mocks/handlers/shared-games.handlers.ts`

**File:** Create `apps/web/src/mocks/handlers/shared-games.handlers.ts`

- [ ] **Step 1: Creare il file**

```typescript
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

let sharedGames: SharedGame[] = [
  {
    id: 'sg-catan', bggId: 13, title: 'Catan', publisher: 'KOSMOS', yearPublished: 1995,
    minPlayers: 3, maxPlayers: 4, playingTimeMinutes: 90, complexityRating: 2.32, averageRating: 7.15,
    imageUrl: 'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__thumb/img/8a9HeqFydO7UnHoiF_nBoBEVCBU=/fit-in/200x150/filters:strip_icc()/pic2419375.jpg',
    categories: [{ id: 'strategy', name: 'Strategy', slug: 'strategy' }],
    mechanics: [{ id: 'trading', name: 'Trading', slug: 'trading' }],
    designers: [{ id: 'd1', name: 'Klaus Teuber' }],
  },
  {
    id: 'sg-wingspan', bggId: 266192, title: 'Wingspan', publisher: 'Stonemaier Games', yearPublished: 2019,
    minPlayers: 1, maxPlayers: 5, playingTimeMinutes: 70, complexityRating: 2.45, averageRating: 8.1,
    imageUrl: 'https://cf.geekdo-images.com/yLZJCVLlIx4c7eJEWUNJ7w__thumb/img/NP9UNiCAbTSM7nABLhEe5oNiWlI=/fit-in/200x150/filters:strip_icc()/pic4458123.jpg',
    categories: [{ id: 'family', name: 'Family', slug: 'family' }],
    mechanics: [{ id: 'engine-building', name: 'Engine Building', slug: 'engine-building' }],
    designers: [{ id: 'd2', name: 'Elizabeth Hargrave' }],
  },
  {
    id: 'sg-pandemic', bggId: 30549, title: 'Pandemic', publisher: 'Z-Man Games', yearPublished: 2008,
    minPlayers: 2, maxPlayers: 4, playingTimeMinutes: 45, complexityRating: 2.41, averageRating: 7.65,
    categories: [{ id: 'cooperative', name: 'Cooperative', slug: 'cooperative' }],
    mechanics: [{ id: 'hand-management', name: 'Hand Management', slug: 'hand-management' }],
    designers: [{ id: 'd3', name: 'Matt Leacock' }],
  },
  {
    id: 'sg-terraforming-mars', bggId: 167791, title: 'Terraforming Mars', publisher: 'FryxGames', yearPublished: 2016,
    minPlayers: 1, maxPlayers: 5, playingTimeMinutes: 120, complexityRating: 3.24, averageRating: 8.4,
    categories: [{ id: 'strategy', name: 'Strategy', slug: 'strategy' }],
    mechanics: [{ id: 'engine-building', name: 'Engine Building', slug: 'engine-building' }],
    designers: [{ id: 'd4', name: 'Jacob Fryxelius' }],
  },
  {
    id: 'sg-spirit-island', bggId: 162886, title: 'Spirit Island', publisher: 'Greater Than Games', yearPublished: 2017,
    minPlayers: 1, maxPlayers: 4, playingTimeMinutes: 120, complexityRating: 3.88, averageRating: 8.5,
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

    let filtered = search
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
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/mocks/handlers/shared-games.handlers.ts
git commit -m "feat(mock): add shared-games handlers to src/mocks"
```

---

## Task 3: `src/mocks/handlers/catalog.handlers.ts`

**File:** Create `apps/web/src/mocks/handlers/catalog.handlers.ts`

- [ ] **Step 1: Creare il file**

```typescript
/**
 * MSW handlers for catalog filter endpoints (browser-safe)
 * Covers: /api/v1/games/categories, /api/v1/games/mechanics, etc.
 */
import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const categories = [
  { id: 'strategy', name: 'Strategy', count: 150 },
  { id: 'family', name: 'Family', count: 120 },
  { id: 'party', name: 'Party Games', count: 80 },
  { id: 'thematic', name: 'Thematic', count: 95 },
  { id: 'cooperative', name: 'Cooperative', count: 70 },
  { id: 'abstract', name: 'Abstract', count: 60 },
  { id: 'card-game', name: 'Card Game', count: 110 },
];

const mechanics = [
  { id: 'worker-placement', name: 'Worker Placement', count: 85 },
  { id: 'deck-building', name: 'Deck Building', count: 65 },
  { id: 'area-control', name: 'Area Control', count: 90 },
  { id: 'dice-rolling', name: 'Dice Rolling', count: 120 },
  { id: 'hand-management', name: 'Hand Management', count: 150 },
  { id: 'engine-building', name: 'Engine Building', count: 60 },
  { id: 'drafting', name: 'Drafting', count: 55 },
];

const complexityRanges = [
  { id: 'light', label: 'Light (1.0–2.0)', min: 1.0, max: 2.0, count: 85 },
  { id: 'medium-light', label: 'Medium Light (2.0–2.5)', min: 2.0, max: 2.5, count: 120 },
  { id: 'medium', label: 'Medium (2.5–3.0)', min: 2.5, max: 3.0, count: 95 },
  { id: 'medium-heavy', label: 'Medium Heavy (3.0–3.5)', min: 3.0, max: 3.5, count: 60 },
  { id: 'heavy', label: 'Heavy (3.5–5.0)', min: 3.5, max: 5.0, count: 40 },
];

const playerCounts = [
  { id: '1', label: '1 Player (Solo)', value: 1, count: 45 },
  { id: '2', label: '2 Players', value: 2, count: 180 },
  { id: '3', label: '3 Players', value: 3, count: 150 },
  { id: '4', label: '4 Players', value: 4, count: 160 },
  { id: '6+', label: '6+ Players', value: 6, count: 55 },
];

const playingTimeRanges = [
  { id: 'quick', label: 'Quick (< 30 min)', min: 0, max: 30, count: 65 },
  { id: 'short', label: 'Short (30–60 min)', min: 30, max: 60, count: 120 },
  { id: 'medium', label: 'Medium (1–2 hours)', min: 60, max: 120, count: 110 },
  { id: 'long', label: 'Long (2–3 hours)', min: 120, max: 180, count: 55 },
  { id: 'epic', label: 'Epic (3+ hours)', min: 180, max: 999, count: 25 },
];

export const catalogHandlers = [
  http.get(`${API_BASE}/api/v1/games/categories`, () => HttpResponse.json(categories)),
  http.get(`${API_BASE}/api/v1/games/mechanics`, () => HttpResponse.json(mechanics)),
  http.get(`${API_BASE}/api/v1/games/complexity-ranges`, () => HttpResponse.json(complexityRanges)),
  http.get(`${API_BASE}/api/v1/games/player-counts`, () => HttpResponse.json(playerCounts)),
  http.get(`${API_BASE}/api/v1/games/playing-time-ranges`, () => HttpResponse.json(playingTimeRanges)),
];
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/mocks/handlers/catalog.handlers.ts
git commit -m "feat(mock): add catalog handlers to src/mocks"
```

---

## Task 4: `src/mocks/handlers/admin.handlers.ts`

**File:** Create `apps/web/src/mocks/handlers/admin.handlers.ts`

Nota: la versione in `__tests__/` importa tipi da `@/lib/api`. Qui li definiamo inline per mantenere il file browser-safe.

- [ ] **Step 1: Creare il file**

```typescript
/**
 * MSW handlers for admin dashboard endpoints (browser-safe)
 * Covers: /api/v1/admin/*
 * Data is inlined (no @/lib/api type imports) to stay browser-safe.
 */
import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

const dashboardStats = {
  metrics: {
    totalUsers: 2847, activeSessions: 156, apiRequestsToday: 8492,
    totalPdfDocuments: 567, totalChatMessages: 10000, averageConfidenceScore: 0.85,
    totalRagRequests: 5000, totalTokensUsed: 2500000, totalGames: 1234,
    apiRequests7d: 45000, apiRequests30d: 150000, averageLatency24h: 120,
    averageLatency7d: 115, errorRate24h: 0.02, activeAlerts: 2, resolvedAlerts: 50,
    tokenBalanceEur: 450.75, tokenLimitEur: 1000.0, dbStorageGb: 2.5,
    dbStorageLimitGb: 10.0, dbGrowthMbPerDay: 15.3, cacheHitRatePercent: 87.5,
    cacheHitRateTrendPercent: 2.1,
  },
  trends: Array.from({ length: 7 }, (_, i) => {
    const d = new Date('2026-04-02T00:00:00Z');
    d.setDate(d.getDate() - (6 - i));
    return { date: d.toISOString(), count: Math.floor(100 + i * 20), averageValue: 50 + i * 5 };
  }),
};

const recentActivity = {
  events: [
    { id: 'ev-1', type: 'user_registered', description: 'New user registered', timestamp: new Date(Date.now() - 60000).toISOString(), severity: 'info' },
    { id: 'ev-2', type: 'pdf_processed', description: 'PDF processed successfully', timestamp: new Date(Date.now() - 120000).toISOString(), severity: 'success' },
    { id: 'ev-3', type: 'api_error', description: 'API rate limit hit', timestamp: new Date(Date.now() - 300000).toISOString(), severity: 'warning' },
  ],
  totalCount: 3,
};

const infrastructureDetails = {
  services: [
    { name: 'API', status: 'healthy', uptime: 99.9, latencyMs: 120 },
    { name: 'Database', status: 'healthy', uptime: 99.95, latencyMs: 5 },
    { name: 'Redis', status: 'healthy', uptime: 100, latencyMs: 1 },
    { name: 'Embedding Service', status: 'healthy', uptime: 98.5, latencyMs: 250 },
  ],
};

const adminStats = {
  totalUsers: 2847, totalGames: 1234, totalDocuments: 567,
  totalSessions: 890, totalChatMessages: 10000,
};

export const adminHandlers = [
  http.get(`${API_BASE}/api/v1/admin/stats`, () => HttpResponse.json(adminStats)),
  http.get(`${API_BASE}/api/v1/admin/analytics`, () => HttpResponse.json(dashboardStats)),
  http.get(`${API_BASE}/api/v1/admin/activity`, () => HttpResponse.json(recentActivity)),
  http.get(`${API_BASE}/api/v1/admin/infrastructure`, () => HttpResponse.json(infrastructureDetails)),

  http.get(`${API_BASE}/api/v1/admin/users`, ({ request }) => {
    const url = new URL(request.url);
    const page = parseInt(url.searchParams.get('page') || '1');
    const pageSize = parseInt(url.searchParams.get('pageSize') || '20');
    const users = Array.from({ length: 5 }, (_, i) => ({
      id: `user-${i + 1}`, email: `user${i + 1}@meepleai.dev`,
      displayName: `User ${i + 1}`, role: i === 0 ? 'Admin' : 'User',
      createdAt: new Date(Date.now() - i * 86400000).toISOString(), isActive: true,
    }));
    return HttpResponse.json({ items: users, totalCount: users.length, page, pageSize, totalPages: 1 });
  }),

  http.get(`${API_BASE}/api/v1/admin/content`, () => {
    return HttpResponse.json({ totalDocuments: 567, pendingDocuments: 12, failedDocuments: 3 });
  }),
];
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/mocks/handlers/admin.handlers.ts
git commit -m "feat(mock): add admin handlers to src/mocks (browser-safe inline data)"
```

---

## Task 5: `src/mocks/handlers/sessions.handlers.ts`

**File:** Create `apps/web/src/mocks/handlers/sessions.handlers.ts`

Nota: usa `createMockSession` da `../data/factories` (già presente, browser-safe).

- [ ] **Step 1: Creare il file**

```typescript
/**
 * MSW handlers for session endpoints (browser-safe)
 * Covers: /api/v1/sessions/*
 */
import { http, HttpResponse } from 'msw';
import { createMockSession } from '../data/factories';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface SessionData {
  id: string;
  sessionCode: string;
  gameId: string;
  gameName?: string;
  status: 'Active' | 'Paused' | 'Finalized';
  participants: Array<{ id: string; displayName: string; isOwner: boolean; totalScore: number }>;
  notes: string[];
  createdAt: string;
  completedAt?: string;
}

let sessions: SessionData[] = [
  {
    id: 'session-1', sessionCode: 'ABC123', gameId: 'demo-chess', gameName: 'Chess',
    status: 'Active',
    participants: [
      { id: 'p1', displayName: 'Alice', isOwner: true, totalScore: 0 },
      { id: 'p2', displayName: 'Bob', isOwner: false, totalScore: 0 },
    ],
    notes: [],
    createdAt: '2024-01-15T10:00:00Z',
  },
];

export const sessionsHandlers = [
  http.get(`${API_BASE}/api/v1/sessions`, ({ request }) => {
    const url = new URL(request.url);
    const status = url.searchParams.get('status');
    const filtered = status ? sessions.filter(s => s.status === status) : [...sessions];
    return HttpResponse.json({ items: filtered, totalCount: filtered.length, page: 1, pageSize: 20, totalPages: 1 });
  }),

  http.get(`${API_BASE}/api/v1/sessions/:id`, ({ params }) => {
    const session = sessions.find(s => s.id === params.id);
    if (!session) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(session);
  }),

  http.post(`${API_BASE}/api/v1/sessions`, async ({ request }) => {
    const body = await request.json() as { gameId: string; gameName?: string };
    const newSession: SessionData = {
      id: `session-${Date.now()}`,
      sessionCode: Math.random().toString(36).substring(2, 8).toUpperCase(),
      gameId: body.gameId,
      gameName: body.gameName,
      status: 'Active',
      participants: [],
      notes: [],
      createdAt: new Date().toISOString(),
    };
    sessions.push(newSession);
    return HttpResponse.json(newSession, { status: 201 });
  }),

  http.put(`${API_BASE}/api/v1/sessions/:id/pause`, ({ params }) => {
    const idx = sessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    sessions[idx] = { ...sessions[idx], status: 'Paused' };
    return HttpResponse.json(sessions[idx]);
  }),

  http.put(`${API_BASE}/api/v1/sessions/:id/resume`, ({ params }) => {
    const idx = sessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    sessions[idx] = { ...sessions[idx], status: 'Active' };
    return HttpResponse.json(sessions[idx]);
  }),

  http.put(`${API_BASE}/api/v1/sessions/:id/complete`, ({ params }) => {
    const idx = sessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    sessions[idx] = { ...sessions[idx], status: 'Finalized', completedAt: new Date().toISOString() };
    return HttpResponse.json(sessions[idx]);
  }),

  http.post(`${API_BASE}/api/v1/sessions/:id/participants`, async ({ params, request }) => {
    const idx = sessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = await request.json() as { displayName: string };
    const participant = { id: `p-${Date.now()}`, displayName: body.displayName, isOwner: false, totalScore: 0 };
    sessions[idx].participants.push(participant);
    return HttpResponse.json(participant, { status: 201 });
  }),

  http.delete(`${API_BASE}/api/v1/sessions/:sessionId/participants/:participantId`, ({ params }) => {
    const idx = sessions.findIndex(s => s.id === params.sessionId);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    sessions[idx].participants = sessions[idx].participants.filter(p => p.id !== params.participantId);
    return HttpResponse.json({ success: true });
  }),

  http.post(`${API_BASE}/api/v1/sessions/:id/notes`, async ({ params, request }) => {
    const idx = sessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = await request.json() as { content: string };
    sessions[idx].notes.push(body.content);
    return HttpResponse.json({ success: true });
  }),

  http.delete(`${API_BASE}/api/v1/sessions/:id`, ({ params }) => {
    const idx = sessions.findIndex(s => s.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    sessions.splice(idx, 1);
    return HttpResponse.json({ success: true });
  }),
];
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/mocks/handlers/sessions.handlers.ts
git commit -m "feat(mock): add sessions handlers to src/mocks"
```

---

## Task 6: `src/mocks/handlers/game-nights.handlers.ts`

**File:** Create `apps/web/src/mocks/handlers/game-nights.handlers.ts`

- [ ] **Step 1: Creare il file**

```typescript
/**
 * MSW handlers for game night endpoints (browser-safe)
 * Covers: /api/v1/game-nights/*
 */
import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface GameNight {
  id: string;
  title: string;
  date: string;
  location?: string;
  status: 'Planned' | 'InProgress' | 'Completed' | 'Cancelled';
  organizerId: string;
  participants: Array<{ id: string; userId: string; displayName: string; rsvpStatus: 'Pending' | 'Accepted' | 'Declined' }>;
  playlist: Array<{ id: string; gameId: string; gameName: string; order: number }>;
  createdAt: string;
}

let gameNights: GameNight[] = [
  {
    id: 'gn-1', title: 'Friday Game Night', date: '2026-04-11T19:00:00Z',
    location: 'Casa di Alice', status: 'Planned', organizerId: 'user-1',
    participants: [
      { id: 'gnp-1', userId: 'user-1', displayName: 'Alice', rsvpStatus: 'Accepted' },
      { id: 'gnp-2', userId: 'user-2', displayName: 'Bob', rsvpStatus: 'Pending' },
    ],
    playlist: [
      { id: 'pl-1', gameId: 'catan-1', gameName: 'Catan', order: 1 },
      { id: 'pl-2', gameId: 'wingspan-1', gameName: 'Wingspan', order: 2 },
    ],
    createdAt: '2026-04-01T10:00:00Z',
  },
];

export const gameNightsHandlers = [
  http.get(`${API_BASE}/api/v1/game-nights`, () => {
    return HttpResponse.json({ items: gameNights, totalCount: gameNights.length, page: 1, pageSize: 20, totalPages: 1 });
  }),

  http.get(`${API_BASE}/api/v1/game-nights/:id`, ({ params }) => {
    const night = gameNights.find(n => n.id === params.id);
    if (!night) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(night);
  }),

  http.post(`${API_BASE}/api/v1/game-nights`, async ({ request }) => {
    const body = await request.json() as { title: string; date: string; location?: string };
    const newNight: GameNight = {
      id: `gn-${Date.now()}`, title: body.title, date: body.date, location: body.location,
      status: 'Planned', organizerId: 'user-1', participants: [], playlist: [],
      createdAt: new Date().toISOString(),
    };
    gameNights.push(newNight);
    return HttpResponse.json(newNight, { status: 201 });
  }),

  http.put(`${API_BASE}/api/v1/game-nights/:id`, async ({ params, request }) => {
    const idx = gameNights.findIndex(n => n.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = await request.json() as Partial<GameNight>;
    gameNights[idx] = { ...gameNights[idx], ...body };
    return HttpResponse.json(gameNights[idx]);
  }),

  http.delete(`${API_BASE}/api/v1/game-nights/:id`, ({ params }) => {
    const idx = gameNights.findIndex(n => n.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    gameNights.splice(idx, 1);
    return HttpResponse.json({ success: true });
  }),

  http.post(`${API_BASE}/api/v1/game-nights/:id/invite`, async ({ params, request }) => {
    const idx = gameNights.findIndex(n => n.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = await request.json() as { userId: string; displayName: string };
    const participant = { id: `gnp-${Date.now()}`, userId: body.userId, displayName: body.displayName, rsvpStatus: 'Pending' as const };
    gameNights[idx].participants.push(participant);
    return HttpResponse.json(participant, { status: 201 });
  }),

  http.put(`${API_BASE}/api/v1/game-nights/:id/rsvp`, async ({ params, request }) => {
    const idx = gameNights.findIndex(n => n.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = await request.json() as { status: 'Accepted' | 'Declined' };
    const pIdx = gameNights[idx].participants.findIndex(p => p.userId === 'user-1');
    if (pIdx !== -1) gameNights[idx].participants[pIdx].rsvpStatus = body.status;
    return HttpResponse.json(gameNights[idx]);
  }),

  http.post(`${API_BASE}/api/v1/game-nights/:id/playlist`, async ({ params, request }) => {
    const idx = gameNights.findIndex(n => n.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = await request.json() as { gameId: string; gameName: string };
    const item = { id: `pl-${Date.now()}`, gameId: body.gameId, gameName: body.gameName, order: gameNights[idx].playlist.length + 1 };
    gameNights[idx].playlist.push(item);
    return HttpResponse.json(item, { status: 201 });
  }),
];
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/mocks/handlers/game-nights.handlers.ts
git commit -m "feat(mock): add game-nights handlers to src/mocks"
```

---

## Task 7: `src/mocks/handlers/players.handlers.ts`

**File:** Create `apps/web/src/mocks/handlers/players.handlers.ts`

- [ ] **Step 1: Creare il file**

```typescript
/**
 * MSW handlers for player endpoints (browser-safe)
 * Covers: /api/v1/players/*
 */
import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface Player {
  id: string;
  displayName: string;
  isActive: boolean;
  gamesPlayed: number;
  totalWins: number;
  createdAt: string;
}

let players: Player[] = [
  { id: 'player-1', displayName: 'Alice', isActive: true, gamesPlayed: 15, totalWins: 8, createdAt: '2024-01-10T10:00:00Z' },
  { id: 'player-2', displayName: 'Bob', isActive: true, gamesPlayed: 12, totalWins: 5, createdAt: '2024-01-15T10:00:00Z' },
  { id: 'player-3', displayName: 'Carol', isActive: true, gamesPlayed: 20, totalWins: 11, createdAt: '2024-02-01T10:00:00Z' },
];

export const playersHandlers = [
  http.get(`${API_BASE}/api/v1/players`, () => {
    const active = players.filter(p => p.isActive);
    return HttpResponse.json({ items: active, totalCount: active.length, page: 1, pageSize: 20, totalPages: 1 });
  }),

  http.get(`${API_BASE}/api/v1/players/:id`, ({ params }) => {
    const player = players.find(p => p.id === params.id);
    if (!player) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(player);
  }),

  http.post(`${API_BASE}/api/v1/players`, async ({ request }) => {
    const body = await request.json() as { displayName: string };
    const newPlayer: Player = {
      id: `player-${Date.now()}`, displayName: body.displayName,
      isActive: true, gamesPlayed: 0, totalWins: 0, createdAt: new Date().toISOString(),
    };
    players.push(newPlayer);
    return HttpResponse.json(newPlayer, { status: 201 });
  }),

  http.put(`${API_BASE}/api/v1/players/:id`, async ({ params, request }) => {
    const idx = players.findIndex(p => p.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    const body = await request.json() as Partial<Player>;
    players[idx] = { ...players[idx], ...body };
    return HttpResponse.json(players[idx]);
  }),

  http.delete(`${API_BASE}/api/v1/players/:id`, ({ params }) => {
    const idx = players.findIndex(p => p.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    players[idx] = { ...players[idx], isActive: false };
    return HttpResponse.json({ success: true });
  }),
];
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/mocks/handlers/players.handlers.ts
git commit -m "feat(mock): add players handlers to src/mocks"
```

---

## Task 8: `src/mocks/handlers/notifications.handlers.ts`

**File:** Create `apps/web/src/mocks/handlers/notifications.handlers.ts`

- [ ] **Step 1: Creare il file**

```typescript
/**
 * MSW handlers for notification endpoints (browser-safe)
 * Covers: /api/v1/notifications/*
 */
import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface Notification {
  id: string;
  type: 'info' | 'warning' | 'success' | 'error';
  title: string;
  message: string;
  isRead: boolean;
  createdAt: string;
  targetUrl?: string;
}

let notifications: Notification[] = [
  { id: 'notif-1', type: 'info', title: 'Benvenuto', message: 'Benvenuto su MeepleAI!', isRead: false, createdAt: '2024-01-15T10:00:00Z', targetUrl: '/dashboard' },
  { id: 'notif-2', type: 'success', title: 'Badge Ottenuto', message: 'Hai ottenuto il badge Prima Partita!', isRead: false, createdAt: '2024-01-16T10:00:00Z', targetUrl: '/badges' },
  { id: 'notif-3', type: 'info', title: 'Invito Ricevuto', message: "Alice ti ha invitato a una Game Night", isRead: true, createdAt: '2024-01-17T10:00:00Z', targetUrl: '/game-nights' },
];

export const notificationsHandlers = [
  http.get(`${API_BASE}/api/v1/notifications`, () => {
    return HttpResponse.json({
      items: notifications,
      totalCount: notifications.length,
      unreadCount: notifications.filter(n => !n.isRead).length,
    });
  }),

  http.put(`${API_BASE}/api/v1/notifications/:id/read`, ({ params }) => {
    const notif = notifications.find(n => n.id === params.id);
    if (!notif) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    notif.isRead = true;
    return HttpResponse.json(notif);
  }),

  http.put(`${API_BASE}/api/v1/notifications/read-all`, () => {
    notifications.forEach(n => { n.isRead = true; });
    return HttpResponse.json({ success: true });
  }),

  http.delete(`${API_BASE}/api/v1/notifications/:id`, ({ params }) => {
    const idx = notifications.findIndex(n => n.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    notifications.splice(idx, 1);
    return HttpResponse.json({ success: true });
  }),

  http.get(`${API_BASE}/api/v1/notifications/preferences`, () => {
    return HttpResponse.json({ email: true, push: false, gameNightInvites: true, badgeEarned: true });
  }),

  http.put(`${API_BASE}/api/v1/notifications/preferences`, async ({ request }) => {
    const body = await request.json();
    return HttpResponse.json(body);
  }),
];
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/mocks/handlers/notifications.handlers.ts
git commit -m "feat(mock): add notifications handlers to src/mocks"
```

---

## Task 9: `src/mocks/handlers/badges.handlers.ts`

**File:** Create `apps/web/src/mocks/handlers/badges.handlers.ts`

- [ ] **Step 1: Creare il file**

```typescript
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

let badges: Badge[] = [
  { id: 'badge-1', name: 'Prima Partita', description: 'Gioca la tua prima partita', iconUrl: '/badges/first-game.png', isEarned: true, isClaimed: false, isDisplayed: false, earnedAt: '2024-01-15T10:00:00Z', category: 'Getting Started' },
  { id: 'badge-2', name: 'Collezionista', description: 'Aggiungi 10 giochi alla libreria', iconUrl: '/badges/collector.png', isEarned: false, isClaimed: false, isDisplayed: false, category: 'Library' },
  { id: 'badge-3', name: 'Organizzatore', description: 'Organizza 5 game night', iconUrl: '/badges/social.png', isEarned: true, isClaimed: true, isDisplayed: true, earnedAt: '2024-02-01T10:00:00Z', category: 'Social' },
];

export const badgesHandlers = [
  http.get(`${API_BASE}/api/v1/badges`, () => HttpResponse.json(badges)),

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
    const body = await request.json() as { display: boolean };
    badge.isDisplayed = body.display;
    return HttpResponse.json(badge);
  }),

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
];
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/mocks/handlers/badges.handlers.ts
git commit -m "feat(mock): add badges handlers to src/mocks"
```

---

## Task 10: `src/mocks/handlers/index.ts`

**File:** Create `apps/web/src/mocks/handlers/index.ts`

Questo file è il prerequisito critico: `browser.ts` e `node.ts` importano `{ handlers } from './handlers'` che risolve su questo file.

- [ ] **Step 1: Creare il file**

```typescript
/**
 * MSW handlers barrel export — browser-safe
 *
 * Aggregates all 13 domain handlers for use in:
 * - src/mocks/browser.ts (MSW ServiceWorker — pnpm dev:mock)
 * - src/mocks/node.ts (MSW Node server — alternativo a __tests__/mocks/server.ts)
 * - .storybook/preview.tsx (MSW Storybook addon)
 *
 * The existing __tests__/mocks/handlers/index.ts is NOT affected.
 */

import { authHandlers } from './auth.handlers';
import { gamesHandlers } from './games.handlers';
import { chatHandlers } from './chat.handlers';
import { documentsHandlers } from './documents.handlers';
import { libraryHandlers } from './library.handlers';
import { sharedGamesHandlers } from './shared-games.handlers';
import { catalogHandlers } from './catalog.handlers';
import { adminHandlers } from './admin.handlers';
import { sessionsHandlers } from './sessions.handlers';
import { gameNightsHandlers } from './game-nights.handlers';
import { playersHandlers } from './players.handlers';
import { notificationsHandlers } from './notifications.handlers';
import { badgesHandlers } from './badges.handlers';

export const handlers = [
  ...authHandlers,
  ...gamesHandlers,
  ...chatHandlers,
  ...documentsHandlers,
  ...libraryHandlers,
  ...sharedGamesHandlers,
  ...catalogHandlers,
  ...adminHandlers,
  ...sessionsHandlers,
  ...gameNightsHandlers,
  ...playersHandlers,
  ...notificationsHandlers,
  ...badgesHandlers,
];

// Re-export individual handler groups for per-story overrides in Storybook
export { authHandlers } from './auth.handlers';
export { gamesHandlers } from './games.handlers';
export { chatHandlers } from './chat.handlers';
export { documentsHandlers } from './documents.handlers';
export { libraryHandlers } from './library.handlers';
export { sharedGamesHandlers } from './shared-games.handlers';
export { catalogHandlers } from './catalog.handlers';
export { adminHandlers } from './admin.handlers';
export { sessionsHandlers } from './sessions.handlers';
export { gameNightsHandlers } from './game-nights.handlers';
export { playersHandlers } from './players.handlers';
export { notificationsHandlers } from './notifications.handlers';
export { badgesHandlers } from './badges.handlers';
```

- [ ] **Step 2: Verificare che TypeScript risolva correttamente**

```bash
cd apps/web
npx tsc --noEmit --skipLibCheck 2>&1 | grep "mocks/handlers" | head -20 || echo "OK"
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/mocks/handlers/index.ts
git commit -m "feat(mock): add handlers/index.ts — aggregate all 13 domain handlers"
```

---

## Task 11: `src/app/mock-provider.tsx`

**File:** Create `apps/web/src/app/mock-provider.tsx`

- [ ] **Step 1: Creare il file**

```typescript
'use client';

/**
 * MockProvider — avvia MSW ServiceWorker in browser dev mode
 *
 * Attivato solo quando NEXT_PUBLIC_MOCK_MODE=true.
 * Blocca il render fino a worker.start() completato per evitare
 * race condition con le prime fetch dell'app.
 *
 * Prerequisito: public/mockServiceWorker.js deve esistere.
 * Generarlo con: cd apps/web && npx msw init public/ --save
 */

import { useEffect, useState } from 'react';

interface MockProviderProps {
  children: React.ReactNode;
}

async function initMocks() {
  const { worker } = await import('@/mocks/browser');
  return worker.start({
    onUnhandledRequest: 'bypass', // Non bloccare richieste non gestite (es. font, next internals)
  });
}

export function MockProvider({ children }: MockProviderProps) {
  const [ready, setReady] = useState(false);

  useEffect(() => {
    initMocks().then(() => setReady(true));
  }, []);

  if (!ready) {
    // Blocca il render fino all'avvio del worker per evitare race condition
    return null;
  }

  return <>{children}</>;
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/app/mock-provider.tsx
git commit -m "feat(mock): add MockProvider client component"
```

---

## Task 12: Aggiornare `src/app/providers.tsx`

**File:** Modify `apps/web/src/app/providers.tsx`

Aggiungere `MockProvider` wrappato condizionalmente attorno a `AppProviders`.

- [ ] **Step 1: Aggiungere l'import di MockProvider all'inizio del file, dopo gli import esistenti**

Aprire `apps/web/src/app/providers.tsx` e aggiungere, dopo l'ultimo import esistente (`import { logger } from '@/lib/logger';`):

```typescript
// Dynamic import per evitare che MockProvider sia incluso nel bundle di produzione
const MockProvider = process.env.NEXT_PUBLIC_MOCK_MODE === 'true'
  ? require('./mock-provider').MockProvider as React.ComponentType<{ children: React.ReactNode }>
  : null;
```

- [ ] **Step 2: Modificare `AppProviders` per wrappare con MockProvider**

Modificare la funzione `AppProviders` alla fine del file:

```typescript
export function AppProviders({ children }: AppProvidersProps) {
  const providers = (
    <IntlProvider>
      <ThemeProvider>
        <QueryProvider>
          <AuthProvider>
            <LayoutProvider>
              <ErrorBoundary
                componentName="App"
                showDetails={process.env.NODE_ENV === 'development'}
              >
                <RouteErrorBoundary routeName="AppContent">
                  <CardBrowserProvider>
                    <AddGameWizardProvider>
                      <AppContent>{children}</AppContent>
                    </AddGameWizardProvider>
                  </CardBrowserProvider>
                </RouteErrorBoundary>
              </ErrorBoundary>
            </LayoutProvider>
          </AuthProvider>
        </QueryProvider>
      </ThemeProvider>
    </IntlProvider>
  );

  if (MockProvider) {
    return <MockProvider>{providers}</MockProvider>;
  }

  return providers;
}
```

- [ ] **Step 3: Verificare che la build non sia rotta**

```bash
cd apps/web
npx tsc --noEmit --skipLibCheck 2>&1 | grep "providers.tsx" | head -10 || echo "OK"
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/providers.tsx
git commit -m "feat(mock): integrate MockProvider into AppProviders when NEXT_PUBLIC_MOCK_MODE=true"
```

---

## Task 13: Aggiungere script `dev:mock` in `package.json`

**File:** Modify `apps/web/package.json`

- [ ] **Step 1: Aggiungere lo script `dev:mock` subito dopo `"dev"` nello scripts object**

Aprire `apps/web/package.json` e aggiungere dopo la riga `"dev": "node ./node_modules/next/dist/bin/next dev -p 3000",`:

```json
"dev:mock": "cross-env NEXT_PUBLIC_MOCK_MODE=true node ./node_modules/next/dist/bin/next dev -p 3000",
```

Lo scripts block deve diventare:

```json
"scripts": {
  "dev": "node ./node_modules/next/dist/bin/next dev -p 3000",
  "dev:mock": "cross-env NEXT_PUBLIC_MOCK_MODE=true node ./node_modules/next/dist/bin/next dev -p 3000",
  "dev:visual-docs": ...
```

Nota: `cross-env` è già una dipendenza (usata da altri script come `build`).

- [ ] **Step 2: Commit**

```bash
git add apps/web/package.json
git commit -m "feat(mock): add dev:mock script to package.json"
```

---

## Task 14: Generare `public/mockServiceWorker.js`

**File:** Generate `apps/web/public/mockServiceWorker.js`

MSW richiede un service worker file servito dalla root del sito. Va generato con la CLI di MSW e committato nel repository (o aggiunto a `.gitignore` se si preferisce rigenerarlo in CI).

- [ ] **Step 1: Generare il service worker**

```bash
cd apps/web
npx msw init public/ --save
```

Output atteso:
```
Service Worker for Mock Service Worker saved at:
  apps/web/public/mockServiceWorker.js
```

- [ ] **Step 2: Verificare che il file esista**

```bash
ls apps/web/public/mockServiceWorker.js
```

Expected: il file esiste (circa 3-5KB).

- [ ] **Step 3: Verificare che `package.json` sia stato aggiornato con `"msw": { "workerDirectory": "public" }`**

```bash
cat apps/web/package.json | grep -A 3 '"msw"'
```

Expected output:
```json
"msw": {
  "workerDirectory": ["public"]
}
```

Se non presente, aggiungerlo manualmente a `package.json` come campo top-level:
```json
"msw": {
  "workerDirectory": ["public"]
}
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/public/mockServiceWorker.js apps/web/package.json
git commit -m "feat(mock): generate MSW service worker to public/"
```

---

## Task 15: Aggiornare `.storybook/preview.tsx`

**File:** Modify `apps/web/.storybook/preview.tsx`

Aggiungere i global handlers in modo che tutte le stories abbiano MSW attivo di default.

- [ ] **Step 1: Aggiungere import handlers**

Aprire `apps/web/.storybook/preview.tsx`.

Dopo `import { initialize, mswLoader } from 'msw-storybook-addon';` aggiungere:

```typescript
import { handlers } from '../src/mocks/handlers';
```

- [ ] **Step 2: Aggiungere handlers ai parameters**

Nel blocco `parameters: { ... }` aggiungere `msw` come chiave:

```typescript
parameters: {
  msw: {
    handlers,
  },
  controls: { ... },
  // ... resto invariato
},
```

Il risultato finale del blocco `parameters` sarà:

```typescript
parameters: {
  msw: {
    handlers,
  },
  controls: {
    matchers: {
      color: /(background|color)$/i,
      date: /Date$/i,
    },
    expanded: true,
  },
  backgrounds: {
    disable: true,
  },
  layout: 'centered',
  a11y: {
    config: {
      rules: [
        {
          id: 'color-contrast',
          enabled: false,
        },
      ],
    },
  },
},
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/.storybook/preview.tsx
git commit -m "feat(mock): register global MSW handlers in Storybook preview"
```

---

## Task 16: Smoke test manuale

Verifica che tutto funzioni end-to-end.

- [ ] **Step 1: Avviare in mock mode**

```bash
cd apps/web
pnpm dev:mock
```

Output atteso nella console del browser (DevTools → Console):
```
[MSW] Mocking enabled.
[MSW] Worker is now ready.
```

- [ ] **Step 2: Verificare le richieste intercettate**

Aprire DevTools → Network. Navigare su http://localhost:3000.

Verificare che le richieste verso `localhost:8080/api/v1/*` mostrino le risposte mock (status 200, dati JSON presenti).

- [ ] **Step 3: Verificare che `pnpm dev` normale funzioni ancora (no regressioni)**

```bash
# Fermarlo con Ctrl+C, poi:
pnpm dev
```

Il server dovrebbe avviarsi normalmente senza mock. Verificare nei DevTools che le richieste vadano verso il backend reale (o falliscano con network error se il backend non è attivo — il che è corretto).

- [ ] **Step 4: Verificare TypeScript compila tutto**

```bash
cd apps/web
npx tsc --noEmit --skipLibCheck 2>&1 | head -30
```

Expected: nessun errore.

- [ ] **Step 5: Verificare i test esistenti non sono rotti**

```bash
cd apps/web
pnpm test 2>&1 | tail -20
```

Expected: stesso numero di test passati di prima (i test usano `__tests__/mocks/` che è invariato).

---

## Note implementative

### Ordine di priorità degli handler

In `handlers/index.ts` l'ordine degli spread array è importante: MSW usa il primo handler che fa match. L'ordine attuale è sicuro perché ogni handler usa URL distinti. Se si aggiungono handler con URL sovrapposti (es. `/api/v1/games/categories` vs `/api/v1/games/:id`), mettere le route più specifiche prima.

### NEXT_PUBLIC_MOCK_MODE è una variabile build-time

Come `NEXT_PUBLIC_ALPHA_MODE`, questa variabile viene iniettata a build-time. Cambiarla richiede riavvio del dev server (non solo hot reload). `pnpm dev:mock` fa già questo automaticamente.

### Il service worker persiste tra sessioni

Una volta registrato, `mockServiceWorker.js` persiste nel browser. Per tornare alla modalità reale, fare un hard refresh (Ctrl+Shift+R) o aprire una finestra di navigazione in incognito.

### Contract validation (opzionale, futuro)

Lo spec menziona uno script `scripts/validate-mock-contracts.ts` per validare le factory contro i Zod schema. Questo è il "Passo 3" della spec ed è separato da questo piano. Può essere implementato in seguito indipendentemente.
