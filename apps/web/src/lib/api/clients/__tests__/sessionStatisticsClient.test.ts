/**
 * #3835 — le due chiamate di questo client omettevano il prefisso `/api/v1`.
 *
 * Senza prefisso la URL resta relativa all'origin del frontend: la richiesta finiva su
 * `http://localhost:3000/game-sessions/session-statistics` e tornava 404, mentre l'endpoint
 * dell'API rispondeva regolarmente. `/toolkit/stats` mostrava una pagina vuota e un 404 in console,
 * senza che nulla indicasse dove fosse il problema.
 *
 * Il test asserisce la URL, non il risultato: e' l'unica parte che era sbagliata, ed e' quella che
 * nessun controllo di tipo puo' proteggere — una stringa vale l'altra per TypeScript.
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createSessionStatisticsClient } from '../sessionStatisticsClient';
import type { HttpClient } from '../../core/httpClient';

const mockHttpClient: HttpClient = {
  get: vi.fn(),
  post: vi.fn(),
  put: vi.fn(),
  delete: vi.fn(),
  patch: vi.fn(),
} as unknown as HttpClient;

const statistiche = {
  totalSessions: 3,
  totalGamesPlayed: 5,
  averageSessionDuration: '01:30:00',
  mostPlayedGames: [],
  recentScoreTrends: [],
  monthlyActivity: [],
};

const statisticheGioco = {
  gameId: '11111111-1111-1111-1111-111111111111',
  gameName: 'Azul',
  totalPlays: 2,
  wins: 1,
  winRate: 0.5,
  averageScore: 42,
  highScore: 60,
  averageSessionDuration: '01:30:00',
  scoreHistory: [],
};

describe('sessionStatisticsClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('chiama le statistiche di sessione sotto /api/v1', async () => {
    vi.mocked(mockHttpClient.get).mockResolvedValue(statistiche);
    const client = createSessionStatisticsClient({ httpClient: mockHttpClient });

    await client.getStatistics();

    expect(mockHttpClient.get).toHaveBeenCalledWith('/api/v1/game-sessions/session-statistics');
  });

  it('conserva il prefisso anche con il parametro monthsBack', async () => {
    vi.mocked(mockHttpClient.get).mockResolvedValue(statistiche);
    const client = createSessionStatisticsClient({ httpClient: mockHttpClient });

    await client.getStatistics(6);

    expect(mockHttpClient.get).toHaveBeenCalledWith(
      '/api/v1/game-sessions/session-statistics?monthsBack=6'
    );
  });

  it('chiama le statistiche per gioco sotto /api/v1', async () => {
    vi.mocked(mockHttpClient.get).mockResolvedValue(statisticheGioco);
    const client = createSessionStatisticsClient({ httpClient: mockHttpClient });

    await client.getGameStatistics('11111111-1111-1111-1111-111111111111');

    expect(mockHttpClient.get).toHaveBeenCalledWith(
      '/api/v1/game-sessions/session-statistics/game/11111111-1111-1111-1111-111111111111'
    );
  });
});
