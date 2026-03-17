import { Page } from '@playwright/test';

const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ── Mock Data ──────────────────────────────────────────────

export const MOCK_SESSION_ID = 'a1b2c3d4-e5f6-7890-abcd-ef1234567890';
export const MOCK_GAME_ID = 'g1a2m3e4-i5d6-7890-abcd-ef1234567890';
export const MOCK_SNAPSHOT_ID = 's1n2a3p4-s5h6-7890-abcd-ef1234567890';
export const MOCK_INVITE_CODE = 'ABC123';
export const MOCK_PRIVATE_GAME_ID = 'p1r2i3v4-g5a6-7890-abcd-ef1234567890';

export const MOCK_PLAYERS = [
  {
    id: 'player-1',
    userId: 'user-test-1',
    displayName: 'Marco',
    avatarUrl: null,
    color: 'Red' as const,
    role: 'Host' as const,
    teamId: null,
    totalScore: 45,
    currentRank: 1,
    joinedAt: new Date().toISOString(),
    isActive: true,
  },
  {
    id: 'player-2',
    userId: null,
    displayName: 'Lucia',
    avatarUrl: null,
    color: 'Blue' as const,
    role: 'Player' as const,
    teamId: null,
    totalScore: 38,
    currentRank: 2,
    joinedAt: new Date().toISOString(),
    isActive: true,
  },
  {
    id: 'player-3',
    userId: null,
    displayName: 'Paolo',
    avatarUrl: null,
    color: 'Green' as const,
    role: 'Player' as const,
    teamId: null,
    totalScore: 32,
    currentRank: 3,
    joinedAt: new Date().toISOString(),
    isActive: true,
  },
];

export const MOCK_LIVE_SESSION = {
  id: MOCK_SESSION_ID,
  sessionCode: MOCK_INVITE_CODE,
  gameId: MOCK_GAME_ID,
  gameName: 'Catan',
  createdByUserId: 'user-test-1',
  status: 'InProgress' as const,
  visibility: 'Private' as const,
  groupId: null,
  createdAt: new Date().toISOString(),
  startedAt: new Date().toISOString(),
  pausedAt: null,
  completedAt: null,
  updatedAt: new Date().toISOString(),
  lastSavedAt: null,
  currentTurnIndex: 5,
  currentTurnPlayerId: 'player-1',
  agentMode: 'Active' as const,
  chatSessionId: 'chat-session-1',
  notes: null,
  players: MOCK_PLAYERS,
  teams: [],
  roundScores: [
    {
      playerId: 'player-1',
      round: 1,
      dimension: 'points',
      value: 15,
      unit: null,
      recordedAt: new Date().toISOString(),
    },
    {
      playerId: 'player-2',
      round: 1,
      dimension: 'points',
      value: 12,
      unit: null,
      recordedAt: new Date().toISOString(),
    },
    {
      playerId: 'player-3',
      round: 1,
      dimension: 'points',
      value: 10,
      unit: null,
      recordedAt: new Date().toISOString(),
    },
  ],
  scoringConfig: { dimensions: ['points'], units: {}, sortDirection: 'desc' },
};

export const MOCK_BGG_SEARCH_RESULTS = {
  items: [
    {
      bggId: 13,
      title: 'Catan',
      yearPublished: 1995,
      thumbnailUrl: 'https://example.com/catan.jpg',
    },
    {
      bggId: 14,
      title: 'Catan: Seafarers',
      yearPublished: 1997,
      thumbnailUrl: 'https://example.com/seafarers.jpg',
    },
  ],
  totalCount: 2,
  page: 1,
  pageSize: 10,
};

export const MOCK_IMPORT_BGG_RESPONSE = {
  privateGameId: MOCK_PRIVATE_GAME_ID,
  libraryEntryId: 'lib-entry-1',
  title: 'Catan',
  imageUrl: 'https://example.com/catan.jpg',
};

export const MOCK_START_SESSION_RESPONSE = {
  sessionId: MOCK_SESSION_ID,
  inviteCode: MOCK_INVITE_CODE,
  shareLink: `http://localhost:3000/sessions/join?code=${MOCK_INVITE_CODE}`,
};

export const MOCK_SCORE_PARSE_RECORDED = {
  status: 'recorded' as const,
  playerName: 'Marco',
  playerId: 'player-1',
  dimension: 'points',
  value: 5,
  round: 2,
  confidence: 0.95,
  requiresConfirmation: false,
  message: 'Punteggio registrato: Marco +5 punti',
  ambiguousCandidates: null,
};

export const MOCK_SCORE_PARSE_NEEDS_CONFIRM = {
  status: 'parsed' as const,
  playerName: 'Marco',
  playerId: 'player-1',
  dimension: 'points',
  value: 5,
  round: 2,
  confidence: 0.65,
  requiresConfirmation: true,
  message: 'Conferma il punteggio: Marco +5 punti',
  ambiguousCandidates: null,
};

export const MOCK_SCORE_PARSE_AMBIGUOUS = {
  status: 'ambiguous' as const,
  playerName: null,
  playerId: null,
  dimension: 'points',
  value: 5,
  round: 2,
  confidence: 0.4,
  requiresConfirmation: true,
  message: 'Giocatore ambiguo. Seleziona il giocatore corretto.',
  ambiguousCandidates: ['Marco', 'Maria'],
};

export const MOCK_SCORE_PARSE_UNRECOGNIZED = {
  status: 'unrecognized' as const,
  playerName: null,
  playerId: null,
  dimension: null,
  value: null,
  round: null,
  confidence: 0.1,
  requiresConfirmation: false,
  message: 'Non ho capito. Prova con "Marco ha 5 punti".',
  ambiguousCandidates: null,
};

export const MOCK_DISPUTE_RESPONSE = {
  id: 'dispute-1',
  verdict:
    'Marco ha ragione. Secondo il regolamento (pagina 12), quando un giocatore costruisce una strada su un incrocio occupato, deve pagare 1 risorsa aggiuntiva.',
  ruleReferences: ['Pagina 12: Costruzione Strade', 'Pagina 15: Risorse e Costi'],
  note: null,
};

export const MOCK_PAUSE_SNAPSHOT_RESPONSE = {
  snapshotId: MOCK_SNAPSHOT_ID,
};

export const MOCK_RESUME_CONTEXT = {
  sessionId: MOCK_SESSION_ID,
  gameTitle: 'Catan',
  lastSnapshotIndex: 2,
  currentTurn: 5,
  currentPhase: 'Cambio turno',
  pausedAt: new Date(Date.now() - 3600000).toISOString(),
  recap:
    'Quando avete messo in pausa, Marco era in testa con 45 punti. Lucia seguiva con 38. La partita era al turno 5, fase Cambio turno. Marco aveva appena costruito un insediamento.',
  playerScores: [
    { playerId: 'player-1', name: 'Marco', totalScore: 45, rank: 1 },
    { playerId: 'player-2', name: 'Lucia', totalScore: 38, rank: 2 },
    { playerId: 'player-3', name: 'Paolo', totalScore: 32, rank: 3 },
  ],
  photos: [
    {
      attachmentId: 'photo-1',
      thumbnailUrl: 'https://example.com/photo1-thumb.jpg',
      caption: 'Tavolo di gioco',
      attachmentType: 'Photo',
    },
    {
      attachmentId: 'photo-2',
      thumbnailUrl: 'https://example.com/photo2-thumb.jpg',
      caption: 'Punteggi',
      attachmentType: 'Photo',
    },
  ],
};

export const MOCK_RESUME_SESSION_RESPONSE = {
  sessionId: MOCK_SESSION_ID,
  inviteCode: 'XYZ789',
  shareLink: `http://localhost:3000/sessions/join?code=XYZ789`,
  agentRecap: MOCK_RESUME_CONTEXT.recap,
};

export const MOCK_NOTIFICATION_AGENT_READY = {
  id: 'notif-1',
  userId: 'user-test-1',
  type: 'processing_job_completed',
  severity: 'success',
  title: 'Agente pronto',
  message: 'Agente per Catan pronto!',
  link: `/library/private/${MOCK_PRIVATE_GAME_ID}/toolkit`,
  metadata: null,
  isRead: false,
  createdAt: new Date().toISOString(),
  readAt: null,
};

// ── Helper Class ───────────────────────────────────────────

export class SessionHelper {
  constructor(private readonly page: Page) {}

  async mockLiveSession(session = MOCK_LIVE_SESSION) {
    await this.page
      .context()
      .route(`${API_BASE}/api/v1/live-sessions/${session.id}`, async route => {
        if (route.request().method() === 'GET') {
          return route.fulfill({ status: 200, json: session });
        }
        return route.continue();
      });
  }

  async mockBggSearch(results = MOCK_BGG_SEARCH_RESULTS) {
    await this.page
      .context()
      .route(`${API_BASE}/api/v1/game-night/bgg/search**`, async route =>
        route.fulfill({ status: 200, json: results })
      );
  }

  async mockBggImport(response = MOCK_IMPORT_BGG_RESPONSE) {
    await this.page
      .context()
      .route(`${API_BASE}/api/v1/game-night/import-bgg`, async route =>
        route.fulfill({ status: 201, json: response })
      );
  }

  async mockStartSession(response = MOCK_START_SESSION_RESPONSE) {
    await this.page
      .context()
      .route(`${API_BASE}/api/v1/game-night/start-session`, async route =>
        route.fulfill({ status: 201, json: response })
      );
  }

  async mockScoreParse(response = MOCK_SCORE_PARSE_RECORDED) {
    await this.page
      .context()
      .route(`${API_BASE}/api/v1/live-sessions/*/scores/parse`, async route =>
        route.fulfill({ status: 200, json: response })
      );
  }

  async mockScoreConfirm() {
    await this.page
      .context()
      .route(`${API_BASE}/api/v1/live-sessions/*/scores/confirm`, async route =>
        route.fulfill({ status: 200, json: { success: true } })
      );
  }

  async mockDispute(response = MOCK_DISPUTE_RESPONSE) {
    await this.page
      .context()
      .route(`${API_BASE}/api/v1/game-night/sessions/*/disputes`, async route =>
        route.fulfill({ status: 200, json: response })
      );
  }

  async mockPauseSnapshot(response = MOCK_PAUSE_SNAPSHOT_RESPONSE) {
    await this.page
      .context()
      .route(`${API_BASE}/api/v1/game-night/sessions/*/save`, async route =>
        route.fulfill({ status: 200, json: response })
      );
    await this.page.context().route(`${API_BASE}/api/v1/live-sessions/*/pause`, async route =>
      route.fulfill({
        status: 200,
        json: { ...MOCK_LIVE_SESSION, status: 'Paused', pausedAt: new Date().toISOString() },
      })
    );
  }

  async mockResumeContext(context = MOCK_RESUME_CONTEXT) {
    await this.page
      .context()
      .route(`${API_BASE}/api/v1/live-sessions/*/resume-context`, async route =>
        route.fulfill({ status: 200, json: context })
      );
  }

  async mockResumeSession(response = MOCK_RESUME_SESSION_RESPONSE) {
    await this.page.context().route(`${API_BASE}/api/v1/live-sessions/*/resume`, async route => {
      if (route.request().method() === 'POST') {
        return route.fulfill({ status: 200, json: response });
      }
      return route.continue();
    });
  }

  async mockNotifications(notifications = [MOCK_NOTIFICATION_AGENT_READY]) {
    await this.page.context().route(`${API_BASE}/api/v1/notifications**`, async route => {
      const url = route.request().url();
      if (url.includes('/unread-count')) {
        return route.fulfill({
          status: 200,
          json: { count: notifications.filter(n => !n.isRead).length },
        });
      }
      if (url.includes('/stream')) {
        let body = '';
        for (const notif of notifications) {
          body += `data: ${JSON.stringify(notif)}\n\n`;
        }
        return route.fulfill({
          status: 200,
          headers: {
            'Content-Type': 'text/event-stream',
            'Cache-Control': 'no-cache',
            Connection: 'keep-alive',
          },
          body,
        });
      }
      return route.fulfill({ status: 200, json: notifications });
    });
  }

  async mockScoresList() {
    await this.page
      .context()
      .route(`${API_BASE}/api/v1/live-sessions/*/scores`, async route =>
        route.fulfill({ status: 200, json: MOCK_LIVE_SESSION.roundScores })
      );
  }

  async mockPlayersList() {
    await this.page
      .context()
      .route(`${API_BASE}/api/v1/live-sessions/*/players`, async route =>
        route.fulfill({ status: 200, json: MOCK_PLAYERS })
      );
  }

  async mockActiveSessions(sessions = [MOCK_LIVE_SESSION]) {
    await this.page
      .context()
      .route(`${API_BASE}/api/v1/live-sessions/active`, async route =>
        route.fulfill({ status: 200, json: sessions })
      );
  }

  async mockSessionCatchAll() {
    await this.page.context().route(`${API_BASE}/api/v1/live-sessions/**`, async route => {
      console.log(
        `[E2E] Unhandled session API: ${route.request().method()} ${route.request().url()}`
      );
      return route.fulfill({ status: 200, json: {} });
    });
  }
}
