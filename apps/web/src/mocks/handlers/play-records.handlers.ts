/**
 * MSW handlers for play-records endpoints (browser-safe)
 * Covers: /api/v1/play-records/*
 */
import { http, HttpResponse } from 'msw';

import { mockId, HANDLER_BASE } from '../data/factories';

const API_BASE = HANDLER_BASE;

const playRecords = [
  {
    id: mockId(601),
    gameName: 'Chess',
    gameId: mockId(101),
    sessionDate: '2026-04-08T19:00:00Z',
    duration: '01:30:00',
    status: 'Completed' as const,
    playerCount: 2,
  },
  {
    id: mockId(602),
    gameName: 'Wingspan',
    gameId: mockId(103),
    sessionDate: '2026-04-05T15:00:00Z',
    duration: '02:15:00',
    status: 'Completed' as const,
    playerCount: 3,
  },
  {
    id: mockId(603),
    gameName: 'Catan',
    gameId: mockId(104),
    sessionDate: '2026-03-28T18:00:00Z',
    duration: '03:00:00',
    status: 'Completed' as const,
    playerCount: 4,
  },
];

const playRecordDetail = {
  id: mockId(601),
  gameId: mockId(101),
  gameName: 'Chess',
  sessionDate: '2026-04-08T19:00:00Z',
  duration: '01:30:00',
  status: 'Completed' as const,
  players: [
    {
      id: mockId(701),
      userId: mockId(1),
      displayName: 'Test User',
      scores: [{ dimension: 'points', value: 1, unit: null }],
    },
    {
      id: mockId(702),
      userId: null,
      displayName: 'Guest',
      scores: [{ dimension: 'points', value: 0, unit: null }],
    },
  ],
  scoringConfig: {
    enabledDimensions: ['points'],
    dimensionUnits: { points: '' },
  },
  createdByUserId: mockId(1),
  visibility: 'Private' as const,
  startTime: '2026-04-08T19:00:00Z',
  endTime: '2026-04-08T20:30:00Z',
  notes: null,
  location: null,
  createdAt: '2026-04-08T18:50:00Z',
  updatedAt: '2026-04-08T20:31:00Z',
};

export const playRecordsHandlers = [
  http.get(`${API_BASE}/api/v1/play-records/history`, ({ request }) => {
    const url = new URL(request.url);
    const page = parseInt(url.searchParams.get('Page') ?? '1', 10);
    const pageSize = parseInt(url.searchParams.get('PageSize') ?? '20', 10);
    return HttpResponse.json({
      records: playRecords,
      totalCount: playRecords.length,
      page,
      pageSize,
      totalPages: 1,
    });
  }),

  http.get(`${API_BASE}/api/v1/play-records/statistics`, () => {
    return HttpResponse.json({
      totalSessions: 3,
      totalWins: 1,
      gamePlayCounts: { Chess: 1, Wingspan: 1, Catan: 1 },
      averageScoresByGame: { Chess: 1, Wingspan: 0, Catan: 0 },
    });
  }),

  http.get(`${API_BASE}/api/v1/play-records/:id`, ({ params }) => {
    if (params.id === mockId(601)) return HttpResponse.json(playRecordDetail);
    return HttpResponse.json({ error: 'Not found' }, { status: 404 });
  }),

  http.post(`${API_BASE}/api/v1/play-records`, () => {
    return HttpResponse.json(mockId(604), { status: 201 });
  }),

  http.post(`${API_BASE}/api/v1/play-records/:id/start`, () => {
    return new HttpResponse(null, { status: 204 });
  }),

  http.post(`${API_BASE}/api/v1/play-records/:id/complete`, () => {
    return new HttpResponse(null, { status: 204 });
  }),

  http.post(`${API_BASE}/api/v1/play-records/:id/players`, () => {
    return new HttpResponse(null, { status: 204 });
  }),

  http.post(`${API_BASE}/api/v1/play-records/:id/scores`, () => {
    return new HttpResponse(null, { status: 204 });
  }),

  http.put(`${API_BASE}/api/v1/play-records/:id`, () => {
    return new HttpResponse(null, { status: 204 });
  }),
];
