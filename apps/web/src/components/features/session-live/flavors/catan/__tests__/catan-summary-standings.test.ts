import { describe, it, expect, vi } from 'vitest';
import { buildCatanSummaryStandings } from '../catan-summary-standings';
import type { ScorePlayerDto } from '@/lib/api/schemas/games.schemas';

const players: ScorePlayerDto[] = [
  { id: 'p1', displayName: 'Alice', color: 'Red' },
  { id: 'p2', displayName: 'Bob', color: 'Blue' },
  { id: 'p3', displayName: 'Carol', color: 'Orange' },
];

const pointsJson = JSON.stringify({
  scores: [
    { playerId: 'p1', points: 10 },
    { playerId: 'p2', points: 8 },
    { playerId: 'p3', points: 6 },
  ],
});

describe('buildCatanSummaryStandings', () => {
  it('joins by id, orders winner-first then score DESC, zips color', () => {
    const rows = buildCatanSummaryStandings('Points', pointsJson, players);
    expect(rows.map(r => r.playerName)).toEqual(['Alice', 'Bob', 'Carol']);
    expect(rows[0]).toMatchObject({ playerName: 'Alice', score: 10, isWinner: true, color: 'Red' });
    expect(rows[2]).toMatchObject({
      playerName: 'Carol',
      score: 6,
      isWinner: false,
      color: 'Orange',
    });
  });

  it('returns [] for null score / null-or-empty scorePlayers / unknown type', () => {
    expect(buildCatanSummaryStandings(null, null, players)).toEqual([]);
    expect(buildCatanSummaryStandings('Points', pointsJson, null)).toEqual([]);
    expect(buildCatanSummaryStandings('Points', pointsJson, [])).toEqual([]);
    expect(buildCatanSummaryStandings('Nope', pointsJson, players)).toEqual([]);
  });

  it('returns [] and warns on malformed JSON', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    expect(buildCatanSummaryStandings('Points', '{bad', players)).toEqual([]);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });
});
