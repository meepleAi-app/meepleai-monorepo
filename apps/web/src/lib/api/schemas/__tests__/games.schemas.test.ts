import { describe, it, expect } from 'vitest';
import { GameSessionDtoSchema, PaginatedGamesResponseSchema } from '../games.schemas';

const base = {
  id: '00000000-0000-4000-8000-000000000001',
  gameId: '00000000-0000-4000-8000-0000000000aa',
  status: 'Completed',
  startedAt: '2026-01-01T00:00:00Z',
  completedAt: '2026-01-01T01:00:00Z',
  playerCount: 1,
  players: [{ playerName: 'Alice', playerOrder: 1, color: 'Red' }],
  winnerName: 'Alice',
  notes: null,
  durationMinutes: 47,
};

describe('GameSessionDtoSchema #3022', () => {
  it('parses gameSlug/gameName/scorePlayers when present', () => {
    const parsed = GameSessionDtoSchema.parse({
      ...base,
      gameSlug: 'catan',
      gameName: 'Catan',
      scorePlayers: [{ id: 'x', displayName: 'Alice', color: 'Red' }],
    });
    expect(parsed.gameSlug).toBe('catan');
    expect(parsed.gameName).toBe('Catan');
    expect(parsed.scorePlayers?.[0]).toEqual({ id: 'x', displayName: 'Alice', color: 'Red' });
  });

  it('accepts absent and null new fields (back-compat)', () => {
    expect(GameSessionDtoSchema.parse(base).scorePlayers).toBeUndefined();
    expect(
      GameSessionDtoSchema.parse({ ...base, scorePlayers: null, gameSlug: null }).scorePlayers
    ).toBeNull();
  });
});

// #4158: il catalogo condiviso salva giocatori e durata come int non nullable (SharedGameEntity), e
// usa 0 per «sconosciuto». Lo schema esigeva valori positivi: un solo gioco così faceva scartare
// l'intera pagina. Si è visto quando getAll ha cominciato a rispettare pageSize: Chess, 31° gioco
// del catalogo locale, ha durata 0 e mandava in errore il selettore del pannello chat (50 giochi).
describe('PaginatedGamesResponseSchema #4158', () => {
  const chess = {
    id: '00000000-0000-4000-8000-000000000031',
    title: 'Chess',
    publisher: null,
    yearPublished: 1475,
    minPlayers: 2,
    maxPlayers: 2,
    minPlayTimeMinutes: 0,
    maxPlayTimeMinutes: 0,
    bggId: 171,
    createdAt: '2026-01-01T00:00:00Z',
    imageUrl: null,
  };
  const page = (games: unknown[]) => ({ games, total: 165, page: 1, pageSize: 50, totalPages: 4 });

  it('accepts a game whose play time is unknown (0)', () => {
    expect(PaginatedGamesResponseSchema.safeParse(page([chess])).success).toBe(true);
  });

  it('accepts a game whose player counts are unknown (0)', () => {
    const unknownPlayers = { ...chess, minPlayers: 0, maxPlayers: 0 };
    expect(PaginatedGamesResponseSchema.safeParse(page([unknownPlayers])).success).toBe(true);
  });

  it('still rejects a negative play time', () => {
    const negative = { ...chess, minPlayTimeMinutes: -1 };
    expect(PaginatedGamesResponseSchema.safeParse(page([negative])).success).toBe(false);
  });
});
