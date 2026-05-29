/**
 * MSW handlers for play-records endpoints.
 *
 * Covers:
 *   GET  /api/v1/play-records          — paginated history
 *   GET  /api/v1/play-records/:id      — full record DTO
 *   GET  /api/v1/play-records/statistics
 *   POST /api/v1/play-records          — create
 *   PUT  /api/v1/play-records/:id      — update (3 editable fields)
 *   DELETE /api/v1/play-records/:id    — delete
 *
 * Issue #1488: Play Records reskin — Task 3 (SessionCreateForm wizard)
 */

import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE ?? 'http://localhost:8080';

// ─── Seed data ────────────────────────────────────────────────────────────────

const isoDate = (daysAgo = 0) => {
  const d = new Date();
  d.setDate(d.getDate() - daysAgo);
  return d.toISOString();
};

type PlayRecordStatus = 'Planned' | 'InProgress' | 'Completed' | 'Archived';
type PlayRecordVisibility = 'Private' | 'Group';
type PlayRecordOutcomeType = 'competitive' | 'none';

interface SeedRecord {
  id: string;
  gameId: string | null;
  gameName: string;
  sessionDate: string;
  duration: string | null;
  status: PlayRecordStatus;
  players: Array<{
    id: string;
    userId: string | null;
    displayName: string;
    scores: Array<{ dimension: string; value: number; unit: string | null }>;
    totalScore: number | null;
  }>;
  scoringConfig: { enabledDimensions: string[]; dimensionUnits: Record<string, string> };
  createdByUserId: string;
  visibility: PlayRecordVisibility;
  startTime: string | null;
  endTime: string | null;
  notes: string | null;
  location: string | null;
  createdAt: string;
  updatedAt: string;
  winnerPlayerIds: string[];
  outcomeType: PlayRecordOutcomeType;
  playerCount: number;
}

let records: SeedRecord[] = [
  {
    id: 'pr-won-1',
    gameId: 'game-1',
    gameName: 'Catan',
    sessionDate: isoDate(2),
    duration: '01:30:00',
    status: 'Completed',
    players: [
      {
        id: 'plr-1',
        userId: 'user-me',
        displayName: 'Marco',
        scores: [{ dimension: 'points', value: 10, unit: null }],
        totalScore: 10,
      },
      {
        id: 'plr-2',
        userId: 'user-2',
        displayName: 'Anna',
        scores: [{ dimension: 'points', value: 7, unit: null }],
        totalScore: 7,
      },
    ],
    scoringConfig: { enabledDimensions: ['points'], dimensionUnits: {} },
    createdByUserId: 'user-me',
    visibility: 'Private',
    startTime: null,
    endTime: null,
    notes: 'Great game',
    location: null,
    createdAt: isoDate(2),
    updatedAt: isoDate(2),
    winnerPlayerIds: ['plr-1'],
    outcomeType: 'competitive',
    playerCount: 2,
  },
  {
    id: 'pr-tied-1',
    gameId: 'game-2',
    gameName: 'Ticket to Ride',
    sessionDate: isoDate(5),
    duration: '02:00:00',
    status: 'Completed',
    players: [
      {
        id: 'plr-3',
        userId: 'user-me',
        displayName: 'Marco',
        scores: [{ dimension: 'points', value: 8, unit: null }],
        totalScore: 8,
      },
      {
        id: 'plr-4',
        userId: 'user-2',
        displayName: 'Anna',
        scores: [{ dimension: 'points', value: 8, unit: null }],
        totalScore: 8,
      },
    ],
    scoringConfig: { enabledDimensions: ['points'], dimensionUnits: {} },
    createdByUserId: 'user-me',
    visibility: 'Private',
    startTime: null,
    endTime: null,
    notes: null,
    location: null,
    createdAt: isoDate(5),
    updatedAt: isoDate(5),
    winnerPlayerIds: ['plr-3', 'plr-4'],
    outcomeType: 'competitive',
    playerCount: 2,
  },
  {
    id: 'pr-coop-1',
    gameId: 'game-3',
    gameName: 'Pandemic',
    sessionDate: isoDate(10),
    duration: '01:45:00',
    status: 'Completed',
    players: [
      { id: 'plr-5', userId: 'user-me', displayName: 'Marco', scores: [], totalScore: null },
      { id: 'plr-6', userId: 'user-2', displayName: 'Anna', scores: [], totalScore: null },
    ],
    scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
    createdByUserId: 'user-me',
    visibility: 'Private',
    startTime: null,
    endTime: null,
    notes: 'We won together!',
    location: null,
    createdAt: isoDate(10),
    updatedAt: isoDate(10),
    winnerPlayerIds: [],
    outcomeType: 'none',
    playerCount: 2,
  },
  {
    id: 'pr-inprogress-1',
    gameId: null,
    gameName: 'My Custom Game',
    sessionDate: isoDate(0),
    duration: null,
    status: 'InProgress',
    players: [
      { id: 'plr-7', userId: 'user-me', displayName: 'Marco', scores: [], totalScore: null },
    ],
    scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
    createdByUserId: 'user-me',
    visibility: 'Private',
    startTime: new Date().toISOString(),
    endTime: null,
    notes: null,
    location: 'Home',
    createdAt: isoDate(0),
    updatedAt: isoDate(0),
    winnerPlayerIds: [],
    outcomeType: 'competitive',
    playerCount: 1,
  },
];

const seedStats = {
  totalSessions: 4,
  totalWins: 1,
  gamePlayCounts: { 'game-1': 1, 'game-2': 1, 'game-3': 1 },
  averageScoresByGame: { 'game-1': 8.5, 'game-2': 8 },
  totalDurationMinutes: 285,
  winByGame: [
    { gameId: 'game-1', gameName: 'Catan', played: 1, won: 1 },
    { gameId: 'game-2', gameName: 'Ticket to Ride', played: 1, won: 0 },
    { gameId: 'game-3', gameName: 'Pandemic', played: 1, won: 0 },
  ],
  mostPlayedGames: [
    { gameId: 'game-1', gameName: 'Catan', plays: 1 },
    { gameId: 'game-2', gameName: 'Ticket to Ride', plays: 1 },
    { gameId: 'game-3', gameName: 'Pandemic', plays: 1 },
  ],
};

// ─── Handlers ─────────────────────────────────────────────────────────────────

export const playRecordsHandlers = [
  // GET /api/v1/play-records/statistics — must come BEFORE /:id
  http.get(`${API_BASE}/api/v1/play-records/statistics`, () => {
    return HttpResponse.json(seedStats);
  }),

  // GET /api/v1/play-records — paginated list
  http.get(`${API_BASE}/api/v1/play-records`, ({ request }) => {
    const url = new URL(request.url);
    const page = parseInt(url.searchParams.get('page') ?? '1');
    const pageSize = parseInt(url.searchParams.get('pageSize') ?? '20');
    const status = url.searchParams.get('status');

    let filtered = [...records];
    if (status && status !== 'all') {
      filtered = filtered.filter(r => r.status === status);
    }

    const start = (page - 1) * pageSize;
    const items = filtered.slice(start, start + pageSize);

    return HttpResponse.json({
      records: items.map(r => ({
        id: r.id,
        gameName: r.gameName,
        gameId: r.gameId,
        sessionDate: r.sessionDate,
        duration: r.duration,
        status: r.status,
        playerCount: r.playerCount,
        winnerPlayerIds: r.winnerPlayerIds,
        outcomeType: r.outcomeType,
      })),
      totalCount: filtered.length,
      page,
      pageSize,
      totalPages: Math.ceil(filtered.length / pageSize),
    });
  }),

  // GET /api/v1/play-records/:id — full record
  http.get(`${API_BASE}/api/v1/play-records/:id`, ({ params }) => {
    const { id } = params;
    const record = records.find(r => r.id === id);
    if (!record) {
      return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    }
    return HttpResponse.json(record);
  }),

  // POST /api/v1/play-records — create
  http.post(`${API_BASE}/api/v1/play-records`, async ({ request }) => {
    const body = (await request.json()) as {
      gameId?: string;
      gameName: string;
      sessionDate: string;
      visibility: PlayRecordVisibility;
      groupId?: string;
      scoringDimensions?: string[];
      dimensionUnits?: Record<string, string>;
    };

    const newRecord: SeedRecord = {
      id: `pr-new-${Date.now()}`,
      gameId: body.gameId ?? null,
      gameName: body.gameName,
      sessionDate: body.sessionDate,
      duration: null,
      status: 'Completed',
      players: [],
      scoringConfig: {
        enabledDimensions: body.scoringDimensions ?? [],
        dimensionUnits: body.dimensionUnits ?? {},
      },
      createdByUserId: 'user-me',
      visibility: body.visibility,
      startTime: null,
      endTime: null,
      notes: null,
      location: null,
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
      winnerPlayerIds: [],
      outcomeType: 'competitive',
      playerCount: 0,
    };

    records.push(newRecord);
    return HttpResponse.json({ id: newRecord.id }, { status: 201 });
  }),

  // PUT /api/v1/play-records/:id — update (K5: only sessionDate/notes/location)
  http.put(`${API_BASE}/api/v1/play-records/:id`, async ({ params, request }) => {
    const { id } = params;
    const idx = records.findIndex(r => r.id === id);
    if (idx === -1) {
      return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    }

    const body = (await request.json()) as {
      sessionDate?: string;
      notes?: string;
      location?: string;
    };

    records[idx] = {
      ...records[idx],
      ...(body.sessionDate !== undefined && { sessionDate: body.sessionDate }),
      ...(body.notes !== undefined && { notes: body.notes }),
      ...(body.location !== undefined && { location: body.location }),
      updatedAt: new Date().toISOString(),
    };

    return HttpResponse.json(records[idx]);
  }),

  // DELETE /api/v1/play-records/:id
  http.delete(`${API_BASE}/api/v1/play-records/:id`, ({ params }) => {
    const { id } = params;
    const idx = records.findIndex(r => r.id === id);
    if (idx === -1) {
      return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    }
    records.splice(idx, 1);
    return new HttpResponse(null, { status: 204 });
  }),
];

// ─── Named fixtures for Task 2 detail tests ──────────────────────────────────

/** PR fixture: completed competitive, user wins (won perspective). */
export const FIXTURE_WON = records[0];

/** PR fixture: completed competitive, tied result (tied perspective). */
export const FIXTURE_TIED = records[1];

/** PR fixture: completed cooperative game (outcomeType=none). */
export const FIXTURE_COOP = records[2];

/** PR fixture: in-progress game (pending perspective, no duration). */
export const FIXTURE_INPROGRESS = records[3];

/** PR fixture: planned future game (pending perspective + future date). */
export const FIXTURE_PLANNED: (typeof records)[0] = {
  id: 'pr-planned-5',
  gameId: 'game-5',
  gameName: 'Scythe',
  sessionDate: new Date(Date.now() + 7 * 24 * 60 * 60 * 1000).toISOString(), // +7 days
  duration: null,
  status: 'Planned',
  players: [{ id: 'plr-8', userId: 'user-me', displayName: 'Marco', scores: [], totalScore: null }],
  scoringConfig: { enabledDimensions: ['points'], dimensionUnits: {} },
  createdByUserId: 'user-me',
  visibility: 'Private',
  startTime: null,
  endTime: null,
  notes: null,
  location: 'Casa Luca',
  createdAt: isoDate(1),
  updatedAt: isoDate(1),
  winnerPlayerIds: [],
  outcomeType: 'competitive',
  playerCount: 1,
};

/** PR fixture: spectator (current user NOT in players). */
export const FIXTURE_SPECTATOR: (typeof records)[0] = {
  id: 'pr-spectator-6',
  gameId: 'game-6',
  gameName: 'Terraforming Mars',
  sessionDate: isoDate(7),
  duration: '03:00:00',
  status: 'Completed',
  players: [
    {
      id: 'plr-other1',
      userId: 'u-other1',
      displayName: 'Giocatore A',
      scores: [{ dimension: 'points', value: 95, unit: null }],
      totalScore: 95,
    },
    {
      id: 'plr-other2',
      userId: 'u-other2',
      displayName: 'Giocatore B',
      scores: [{ dimension: 'points', value: 88, unit: null }],
      totalScore: 88,
    },
  ],
  scoringConfig: { enabledDimensions: ['points'], dimensionUnits: {} },
  createdByUserId: 'u-other1',
  visibility: 'Group',
  startTime: isoDate(7),
  endTime: isoDate(7),
  notes: null,
  location: null,
  createdAt: isoDate(7),
  updatedAt: isoDate(7),
  winnerPlayerIds: ['plr-other1'],
  outcomeType: 'competitive',
  playerCount: 2,
};

/** PR fixture: freeform game (gameId=null, all guests). */
export const FIXTURE_FREEFORM: (typeof records)[0] = {
  id: 'pr-freeform-7',
  gameId: null,
  gameName: 'Una partita di carte casuale',
  sessionDate: isoDate(14),
  duration: '00:45:00',
  status: 'Completed',
  players: [
    {
      id: 'plr-guest1',
      userId: null,
      displayName: 'Guest 1',
      scores: [{ dimension: 'points', value: 12, unit: null }],
      totalScore: 12,
    },
    {
      id: 'plr-guest2',
      userId: null,
      displayName: 'Guest 2',
      scores: [{ dimension: 'points', value: 8, unit: null }],
      totalScore: 8,
    },
  ],
  scoringConfig: { enabledDimensions: ['points'], dimensionUnits: {} },
  createdByUserId: 'user-me',
  visibility: 'Private',
  startTime: null,
  endTime: null,
  notes: null,
  location: null,
  createdAt: isoDate(14),
  updatedAt: isoDate(14),
  winnerPlayerIds: ['plr-guest1'],
  outcomeType: 'competitive',
  playerCount: 2,
};

/** PR fixture: multi-dimensional scoring (EC-10). */
export const FIXTURE_MULTIDIM: (typeof records)[0] = {
  id: 'pr-multidim-8',
  gameId: 'game-wingspan',
  gameName: 'Wingspan',
  sessionDate: isoDate(3),
  duration: '02:30:00',
  status: 'Completed',
  players: [
    {
      id: 'plr-m1',
      userId: 'user-me',
      displayName: 'Marco',
      scores: [
        { dimension: 'points', value: 85, unit: null },
        { dimension: 'bonus', value: 12, unit: null },
        { dimension: 'eggs', value: 8, unit: null },
      ],
      totalScore: 85,
    },
    {
      id: 'plr-m2',
      userId: 'user-2',
      displayName: 'Anna',
      scores: [
        { dimension: 'points', value: 72, unit: null },
        { dimension: 'bonus', value: 9, unit: null },
        { dimension: 'eggs', value: 6, unit: null },
      ],
      totalScore: 72,
    },
  ],
  scoringConfig: { enabledDimensions: ['points', 'bonus', 'eggs'], dimensionUnits: {} },
  createdByUserId: 'user-me',
  visibility: 'Private',
  startTime: null,
  endTime: null,
  notes: null,
  location: null,
  createdAt: isoDate(3),
  updatedAt: isoDate(3),
  winnerPlayerIds: ['plr-m1'],
  outcomeType: 'competitive',
  playerCount: 2,
};

// Add these to the MSW handlers' in-memory store for test lookups
[FIXTURE_PLANNED, FIXTURE_SPECTATOR, FIXTURE_FREEFORM, FIXTURE_MULTIDIM].forEach(f => {
  if (!records.find(r => r.id === f.id)) {
    records.push(f);
  }
});

// ─── State helpers ─────────────────────────────────────────────────────────────

export const resetPlayRecordsState = () => {
  records = [
    {
      id: 'pr-won-1',
      gameId: 'game-1',
      gameName: 'Catan',
      sessionDate: isoDate(2),
      duration: '01:30:00',
      status: 'Completed',
      players: [
        {
          id: 'plr-1',
          userId: 'user-me',
          displayName: 'Marco',
          scores: [{ dimension: 'points', value: 10, unit: null }],
          totalScore: 10,
        },
        {
          id: 'plr-2',
          userId: 'user-2',
          displayName: 'Anna',
          scores: [{ dimension: 'points', value: 7, unit: null }],
          totalScore: 7,
        },
      ],
      scoringConfig: { enabledDimensions: ['points'], dimensionUnits: {} },
      createdByUserId: 'user-me',
      visibility: 'Private',
      startTime: null,
      endTime: null,
      notes: 'Great game',
      location: null,
      createdAt: isoDate(2),
      updatedAt: isoDate(2),
      winnerPlayerIds: ['plr-1'],
      outcomeType: 'competitive',
      playerCount: 2,
    },
    {
      id: 'pr-tied-1',
      gameId: 'game-2',
      gameName: 'Ticket to Ride',
      sessionDate: isoDate(5),
      duration: '02:00:00',
      status: 'Completed',
      players: [
        {
          id: 'plr-3',
          userId: 'user-me',
          displayName: 'Marco',
          scores: [{ dimension: 'points', value: 8, unit: null }],
          totalScore: 8,
        },
        {
          id: 'plr-4',
          userId: 'user-2',
          displayName: 'Anna',
          scores: [{ dimension: 'points', value: 8, unit: null }],
          totalScore: 8,
        },
      ],
      scoringConfig: { enabledDimensions: ['points'], dimensionUnits: {} },
      createdByUserId: 'user-me',
      visibility: 'Private',
      startTime: null,
      endTime: null,
      notes: null,
      location: null,
      createdAt: isoDate(5),
      updatedAt: isoDate(5),
      winnerPlayerIds: ['plr-3', 'plr-4'],
      outcomeType: 'competitive',
      playerCount: 2,
    },
    {
      id: 'pr-coop-1',
      gameId: 'game-3',
      gameName: 'Pandemic',
      sessionDate: isoDate(10),
      duration: '01:45:00',
      status: 'Completed',
      players: [
        { id: 'plr-5', userId: 'user-me', displayName: 'Marco', scores: [], totalScore: null },
        { id: 'plr-6', userId: 'user-2', displayName: 'Anna', scores: [], totalScore: null },
      ],
      scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
      createdByUserId: 'user-me',
      visibility: 'Private',
      startTime: null,
      endTime: null,
      notes: 'We won together!',
      location: null,
      createdAt: isoDate(10),
      updatedAt: isoDate(10),
      winnerPlayerIds: [],
      outcomeType: 'none',
      playerCount: 2,
    },
    {
      id: 'pr-inprogress-1',
      gameId: null,
      gameName: 'My Custom Game',
      sessionDate: isoDate(0),
      duration: null,
      status: 'InProgress',
      players: [
        { id: 'plr-7', userId: 'user-me', displayName: 'Marco', scores: [], totalScore: null },
      ],
      scoringConfig: { enabledDimensions: [], dimensionUnits: {} },
      createdByUserId: 'user-me',
      visibility: 'Private',
      startTime: new Date().toISOString(),
      endTime: null,
      notes: null,
      location: 'Home',
      createdAt: isoDate(0),
      updatedAt: isoDate(0),
      winnerPlayerIds: [],
      outcomeType: 'competitive',
      playerCount: 1,
    },
    // Task 2 detail fixtures
    FIXTURE_PLANNED,
    FIXTURE_SPECTATOR,
    FIXTURE_FREEFORM,
    FIXTURE_MULTIDIM,
  ];
};

export const getPlayRecordsState = () => [...records];
