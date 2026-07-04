import { describe, it, expect } from 'vitest';

import type { GameNightDiaryDto } from '@/lib/api/schemas/game-nights.schemas';
import { mapDiary, toKind, toTimeLabel } from '@/lib/game-nights/mapDiary';
import type { NightSessionRef } from '@/lib/game-nights/mapNightLive';

const S1 = '11111111-1111-4111-8111-111111111111';
const S2 = '22222222-2222-4222-8222-222222222222';
const G1 = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const G2 = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';

const SESSIONS: readonly NightSessionRef[] = [
  { sessionId: S1, gameId: G1, gameTitle: 'Brass' },
  { sessionId: S2, gameId: G2, gameTitle: 'Spirit Island' },
];

function entry(
  over: Partial<GameNightDiaryDto['entries'][number]> = {}
): GameNightDiaryDto['entries'][number] {
  return {
    id: crypto.randomUUID(),
    sessionId: S1,
    eventType: 'game_started',
    description: '🎲 Partita iniziata',
    payload: null,
    actorId: null,
    timestamp: '2026-07-04T20:00:00',
    ...over,
  };
}

function dto(entries: GameNightDiaryDto['entries']): GameNightDiaryDto {
  return { gameNightId: 'cccccccc-cccc-4ccc-8ccc-cccccccccccc', entries };
}

describe('toKind — emitter vocabulary (panel D6)', () => {
  it('maps the write-side emitter strings to their kinds', () => {
    expect(toKind('score_updated')).toBe('score');
    expect(toKind('turn_advanced')).toBe('turn');
    expect(toKind('dice_rolled')).toBe('custom');
    expect(toKind('session_paused')).toBe('system');
    expect(toKind('game_completed')).toBe('end');
  });

  it('also maps the legacy display-switch aliases', () => {
    expect(toKind('score_update')).toBe('score');
    expect(toKind('dice_roll')).toBe('custom');
    expect(toKind('pause_resume')).toBe('system');
  });

  it('falls back to system for unknown/new event types (D8 — never dropped)', () => {
    expect(toKind('some_future_event')).toBe('system');
    expect(toKind('')).toBe('system');
  });
});

describe('toTimeLabel — UTC pinning (panel timezone must-fix)', () => {
  it('reads a bare BE timestamp as UTC (append Z), not browser-local', () => {
    // 20:00 UTC must render 20:00 regardless of the runner's timezone.
    expect(toTimeLabel('2026-07-04T20:00:00')).toBe('20:00');
    expect(toTimeLabel('2026-07-04T09:05:00')).toBe('09:05');
  });

  it('respects an explicit offset when present', () => {
    expect(toTimeLabel('2026-07-04T20:00:00Z')).toBe('20:00');
    expect(toTimeLabel('2026-07-04T22:00:00+02:00')).toBe('20:00');
  });

  it('returns empty for an unparseable timestamp', () => {
    expect(toTimeLabel('not-a-date')).toBe('');
  });
});

describe('mapDiary', () => {
  it('groups events under the game resolved from the live sessionId lookup (D4)', () => {
    const result = mapDiary(
      dto([
        entry({ sessionId: S1, eventType: 'turn_advanced' }),
        entry({ sessionId: S2, eventType: 'score_updated' }),
      ]),
      SESSIONS
    );

    expect(result.diaryEvents.map(e => e.gameId)).toEqual([G1, G2]);
    expect(result.diaryEvents.map(e => e.kind)).toEqual(['turn', 'score']);
    // one DiaryGameRef per distinct game, titles from the live lookup
    expect(result.diaryGames).toEqual([
      { id: G1, title: 'Brass', emoji: expect.any(String) },
      { id: G2, title: 'Spirit Island', emoji: expect.any(String) },
    ]);
  });

  it('maps an event whose session is not in the live lookup to gameId=null (AC3, no crash)', () => {
    const orphanSession = '99999999-9999-4999-8999-999999999999';
    const result = mapDiary(dto([entry({ sessionId: orphanSession })]), SESSIONS);

    expect(result.diaryEvents).toHaveLength(1);
    expect(result.diaryEvents[0].gameId).toBeNull();
    // a null-game (night-level) event contributes no DiaryGameRef
    expect(result.diaryGames).toEqual([]);
  });

  it('keeps the server Description as text and derives the icon from the kind, not Description[0] (D7)', () => {
    const result = mapDiary(
      dto([entry({ eventType: 'score_updated', description: '📊 Punteggio aggiornato' })]),
      SESSIONS
    );
    expect(result.diaryEvents[0].text).toBe('📊 Punteggio aggiornato');
    expect(result.diaryEvents[0].icon).toBe('📊'); // ICON_BY_KIND['score'], not sliced from the text
  });

  it('deduplicates DiaryGameRef when a game has multiple events', () => {
    const result = mapDiary(
      dto([entry({ sessionId: S1 }), entry({ sessionId: S1, eventType: 'turn_advanced' })]),
      SESSIONS
    );
    expect(result.diaryGames).toHaveLength(1);
    expect(result.diaryGames[0].id).toBe(G1);
  });

  it('is minimal (D5): no actor avatars, no diary players', () => {
    const result = mapDiary(dto([entry(), entry({ eventType: 'score_updated' })]), SESSIONS);
    expect(result.diaryEvents.every(e => e.actors.length === 0)).toBe(true);
    expect(result.diaryPlayers).toEqual([]);
  });

  it('handles an empty diary', () => {
    const result = mapDiary(dto([]), SESSIONS);
    expect(result.diaryEvents).toEqual([]);
    expect(result.diaryGames).toEqual([]);
    expect(result.diaryPlayers).toEqual([]);
  });

  it('is deterministic (pure): same input → identical output', () => {
    const input = dto([
      entry({ sessionId: S1 }),
      entry({ sessionId: S2, eventType: 'score_updated' }),
    ]);
    expect(mapDiary(input, SESSIONS)).toEqual(mapDiary(input, SESSIONS));
  });
});
