/**
 * mapNightLive unit tests — #2633 Slice B (LD-1, LD-4..LD-11).
 *
 * The mapper is a PURE function: mapNightLiveToViewModel(dto, now) with no
 * internal clock/random/global read. These tests pin every field→source rule
 * from the spec-panel verdict (2026-07-04-issue-2633-sliceb-spec-panel-verdict).
 */

import { describe, it, expect } from 'vitest';

import type { GameNightLiveDto } from '@/lib/api/schemas/game-nights.schemas';
import { hashToHue } from '@/lib/games/cover-utils';

import { mapNightLiveToViewModel, toPlannedGameStatus } from '../mapNightLive';

const GN_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const G1 = '11111111-1111-4111-8111-111111111111';
const G2 = '22222222-2222-4222-8222-222222222222';
const G3 = '33333333-3333-4333-8333-333333333333';
const S1 = 'aaaa1111-1111-4111-8111-111111111111';
const S2 = 'aaaa2222-2222-4222-8222-222222222222';
const S3 = 'aaaa3333-3333-4333-8333-333333333333';

function session(over: Partial<GameNightLiveDto['sessions'][number]>) {
  return {
    sessionId: S1,
    gameId: G1,
    gameTitle: 'Eldoria',
    playOrder: 1,
    status: 'Pending' as const,
    winnerId: null,
    winnerName: null,
    startedAt: null,
    completedAt: null,
    ...over,
  };
}

function live(over: Partial<GameNightLiveDto> = {}): GameNightLiveDto {
  return {
    id: GN_ID,
    title: 'Serata Eldoria',
    status: 'Published',
    sessions: [],
    isViewerOrganizer: false,
    plannedLineup: [],
    currentSessionRoster: [],
    ...over,
  };
}

const NOW = new Date('2026-07-04T22:35:00Z');

// Happy-path 3-session night: Completed + InProgress + Pending.
const HAPPY = live({
  sessions: [
    session({
      sessionId: S1,
      gameId: G1,
      gameTitle: 'Brass',
      playOrder: 1,
      status: 'Completed',
      winnerId: null,
      startedAt: '2026-07-04T20:00:00Z',
      completedAt: '2026-07-04T21:53:00Z',
    }),
    session({
      sessionId: S2,
      gameId: G2,
      gameTitle: 'Spirit Island',
      playOrder: 2,
      status: 'InProgress',
      startedAt: '2026-07-04T22:00:00Z',
    }),
    session({
      sessionId: S3,
      gameId: G3,
      gameTitle: 'Wingspan',
      playOrder: 3,
      status: 'Pending',
    }),
  ],
});

describe('toPlannedGameStatus', () => {
  it('maps the 5 wire statuses to the 3 planned buckets', () => {
    expect(toPlannedGameStatus('Pending')).toBe('upcoming');
    expect(toPlannedGameStatus('InProgress')).toBe('inprogress');
    expect(toPlannedGameStatus('Completed')).toBe('completed');
    expect(toPlannedGameStatus('Skipped')).toBe('completed');
    expect(toPlannedGameStatus('Corrupted')).toBe('completed');
  });
});

describe('mapNightLiveToViewModel — header + progression', () => {
  it('maps title from dto.Title and total from sessions.length', () => {
    const vm = mapNightLiveToViewModel(HAPPY, NOW);
    expect(vm.night.title).toBe('Serata Eldoria');
    expect(vm.night.shortTitle).toBeUndefined();
    expect(vm.night.nightCode).toBeUndefined();
    expect(vm.total).toBe(3);
  });

  it('carries the raw GameNightStatus as nightStatus (LD-14 terminal routing)', () => {
    expect(mapNightLiveToViewModel(HAPPY, NOW).nightStatus).toBe('Published');
    expect(mapNightLiveToViewModel(live({ status: 'Cancelled' }), NOW).nightStatus).toBe(
      'Cancelled'
    );
    expect(mapNightLiveToViewModel(live({ status: 'Completed' }), NOW).nightStatus).toBe(
      'Completed'
    );
  });

  it('derives nextGame from the first planned-lineup item (WS1 DEC-9)', () => {
    const withLineup = live({
      plannedLineup: [
        { gameId: G3, gameTitle: 'Wingspan' },
        { gameId: G1, gameTitle: 'Brass' },
      ],
    });
    expect(mapNightLiveToViewModel(withLineup, NOW).nextGame).toEqual({
      gameId: G3,
      gameTitle: 'Wingspan',
    });
    expect(mapNightLiveToViewModel(live({ plannedLineup: [] }), NOW).nextGame).toBeNull();
  });

  it('threads isViewerOrganizer from the DTO (WS1 DEC-9)', () => {
    expect(mapNightLiveToViewModel(live({ isViewerOrganizer: true }), NOW).isViewerOrganizer).toBe(
      true
    );
    expect(mapNightLiveToViewModel(live({ isViewerOrganizer: false }), NOW).isViewerOrganizer).toBe(
      false
    );
  });

  it('sets current to the PlayOrder of the single InProgress session', () => {
    expect(mapNightLiveToViewModel(HAPPY, NOW).current).toBe(2);
  });

  it("status is 'live' when a session is InProgress", () => {
    expect(mapNightLiveToViewModel(HAPPY, NOW).status).toBe('live');
  });

  it("status is 'transition' when no session is InProgress", () => {
    const allDone = live({
      sessions: [
        session({
          status: 'Completed',
          startedAt: '2026-07-04T20:00:00Z',
          completedAt: '2026-07-04T21:00:00Z',
        }),
      ],
    });
    expect(mapNightLiveToViewModel(allDone, NOW).status).toBe('transition');
  });

  it('current with no InProgress = count of terminal sessions (Completed/Skipped/Corrupted)', () => {
    const dto = live({
      sessions: [
        session({
          sessionId: S1,
          playOrder: 1,
          status: 'Completed',
          startedAt: '2026-07-04T20:00:00Z',
          completedAt: '2026-07-04T20:30:00Z',
        }),
        session({ sessionId: S2, playOrder: 2, status: 'Skipped' }),
        session({ sessionId: S3, playOrder: 3, status: 'Pending' }),
      ],
    });
    // Completed + Skipped = 2 terminal, no InProgress.
    expect(mapNightLiveToViewModel(dto, NOW).current).toBe(2);
  });

  it('with >1 InProgress (racy read) picks the lowest PlayOrder and does not throw', () => {
    const dto = live({
      sessions: [
        session({
          sessionId: S2,
          playOrder: 3,
          status: 'InProgress',
          startedAt: '2026-07-04T22:00:00Z',
        }),
        session({
          sessionId: S1,
          playOrder: 2,
          status: 'InProgress',
          startedAt: '2026-07-04T22:10:00Z',
        }),
      ],
    });
    expect(() => mapNightLiveToViewModel(dto, NOW)).not.toThrow();
    expect(mapNightLiveToViewModel(dto, NOW).current).toBe(2);
  });
});

describe('mapNightLiveToViewModel — planned games', () => {
  it('maps id from SessionId (not GameId) and preserves PlayOrder', () => {
    const vm = mapNightLiveToViewModel(HAPPY, NOW);
    expect(vm.plannedGames.map(g => g.id)).toEqual([S1, S2, S3]);
    expect(vm.plannedGames.map(g => g.order)).toEqual([1, 2, 3]);
  });

  it('maps status per bucket', () => {
    const vm = mapNightLiveToViewModel(HAPPY, NOW);
    expect(vm.plannedGames.map(g => g.status)).toEqual(['completed', 'inprogress', 'upcoming']);
  });

  it('derives a deterministic cover gradient from GameId', () => {
    const vm = mapNightLiveToViewModel(HAPPY, NOW);
    const cover = vm.plannedGames[0].cover;
    expect(cover).toHaveLength(2);
    expect(cover?.[0]).toContain(`hsl(${hashToHue(G1)}`);
    // deterministic: same input, same cover
    expect(mapNightLiveToViewModel(HAPPY, NOW).plannedGames[0].cover).toEqual(cover);
  });

  it('leaves publisher/emoji/estimated/score/winner undefined (Slice B)', () => {
    const g = mapNightLiveToViewModel(HAPPY, NOW).plannedGames[0];
    expect(g.publisher).toBeUndefined();
    expect(g.emoji).toBeUndefined();
    expect(g.estimated).toBeUndefined();
    expect(g.score).toBeUndefined();
    expect(g.winner).toBeUndefined();
  });

  it('carries winnerId through untouched (winner resolution is Slice C)', () => {
    const dto = live({
      sessions: [
        session({
          status: 'Completed',
          winnerId: G2,
          startedAt: '2026-07-04T20:00:00Z',
          completedAt: '2026-07-04T21:00:00Z',
        }),
      ],
    });
    const g = mapNightLiveToViewModel(dto, NOW).plannedGames[0];
    expect(g.winnerId).toBe(G2);
    expect(g.winner).toBeUndefined();
  });

  it('marks a Skipped session muted and keeps it in the list', () => {
    const dto = live({ sessions: [session({ status: 'Skipped', gameTitle: 'Saltata' })] });
    const g = mapNightLiveToViewModel(dto, NOW).plannedGames[0];
    expect(g.status).toBe('completed');
    expect(g.muted).toBe(true);
  });

  it('renders a Corrupted session with a non-empty title and never throws', () => {
    const dto = live({ sessions: [session({ status: 'Corrupted', gameTitle: '' })] });
    expect(() => mapNightLiveToViewModel(dto, NOW)).not.toThrow();
    const g = mapNightLiveToViewModel(dto, NOW).plannedGames[0];
    expect(g.status).toBe('completed');
    expect(g.title.length).toBeGreaterThan(0);
  });
});

describe('mapNightLiveToViewModel — currentGame (Slice C1)', () => {
  it('derives currentGame from the InProgress session', () => {
    const cg = mapNightLiveToViewModel(HAPPY, NOW).currentGame;
    expect(cg?.sessionId).toBe(S2);
    expect(cg?.id).toBe(G2); // NightLiveHubCurrentGame.id = GameId
    expect(cg?.title).toBe('Spirit Island');
    expect(cg?.cover).toHaveLength(2);
    expect(cg?.cover?.[0]).toContain(`hsl(${hashToHue(G2)}`);
  });

  it('leaves currentGame emoji + score undefined (fixture-only, D-SCORE/TIME)', () => {
    const cg = mapNightLiveToViewModel(HAPPY, NOW).currentGame;
    expect(cg?.emoji).toBeUndefined();
    expect(cg?.score).toBeUndefined();
  });

  it('currentGame is null when no session is InProgress', () => {
    const allDone = live({
      sessions: [
        session({
          status: 'Completed',
          startedAt: '2026-07-04T20:00:00Z',
          completedAt: '2026-07-04T21:00:00Z',
        }),
      ],
    });
    expect(mapNightLiveToViewModel(allDone, NOW).currentGame).toBeNull();
  });

  it('picks the lowest-playOrder InProgress session when >1 (racy read)', () => {
    const dto = live({
      sessions: [
        session({
          sessionId: S2,
          gameId: G2,
          gameTitle: 'Later',
          playOrder: 3,
          status: 'InProgress',
          startedAt: '2026-07-04T22:00:00Z',
        }),
        session({
          sessionId: S1,
          gameId: G1,
          gameTitle: 'Earlier',
          playOrder: 2,
          status: 'InProgress',
          startedAt: '2026-07-04T22:10:00Z',
        }),
      ],
    });
    expect(mapNightLiveToViewModel(dto, NOW).currentGame?.sessionId).toBe(S1);
  });
});

describe('mapNightLiveToViewModel — time (LD-7)', () => {
  it("elapsed is 'Hh Mm' from the earliest StartedAt to now", () => {
    expect(mapNightLiveToViewModel(HAPPY, NOW).elapsed).toBe('2h 35m');
  });

  it("actual is 'Nm' for a Completed session (CompletedAt - StartedAt)", () => {
    expect(mapNightLiveToViewModel(HAPPY, NOW).plannedGames[0].actual).toBe('113m');
  });

  it("actual is 'Nm' for an InProgress session (now - StartedAt)", () => {
    expect(mapNightLiveToViewModel(HAPPY, NOW).plannedGames[1].actual).toBe('35m');
  });

  it('actual is undefined for a Pending session', () => {
    expect(mapNightLiveToViewModel(HAPPY, NOW).plannedGames[2].actual).toBeUndefined();
  });
});

describe('mapNightLiveToViewModel — determinism (LD-1)', () => {
  it('is deeply-equal across two calls with the same now', () => {
    expect(mapNightLiveToViewModel(HAPPY, NOW)).toEqual(mapNightLiveToViewModel(HAPPY, NOW));
  });

  it('a different now changes ONLY the InProgress actual + elapsed, not the Completed actual', () => {
    const later = new Date('2026-07-04T23:00:00Z');
    const a = mapNightLiveToViewModel(HAPPY, NOW);
    const b = mapNightLiveToViewModel(HAPPY, later);
    expect(b.plannedGames[0].actual).toBe(a.plannedGames[0].actual); // Completed row stable
    expect(b.plannedGames[1].actual).not.toBe(a.plannedGames[1].actual); // InProgress row moves
    expect(b.elapsed).not.toBe(a.elapsed);
  });
});

describe('mapNightLiveToViewModel — empty night (LD-11) + Slice-C seam (LD-3)', () => {
  it('a Published night with 0 sessions is a zeroed happy-path viewmodel', () => {
    const vm = mapNightLiveToViewModel(live({ sessions: [] }), NOW);
    expect(vm.plannedGames).toEqual([]);
    expect(vm.current).toBe(0);
    expect(vm.total).toBe(0);
    expect(vm.elapsed).toBe('0h 0m');
    expect(vm.status).toBe('transition');
    expect(vm.night.title).toBe('Serata Eldoria');
  });

  it('populates the resolved winner on a completed session (C4)', () => {
    const winnerId = 'bbbb1111-1111-4111-8111-111111111111';
    const vm = mapNightLiveToViewModel(
      live({
        sessions: [
          session({
            status: 'Completed',
            winnerId,
            winnerName: 'Guest Gina',
            startedAt: '2026-07-04T20:00:00Z',
            completedAt: '2026-07-04T21:00:00Z',
          }),
        ],
      }),
      NOW
    );

    const winner = vm.plannedGames[0].winner;
    expect(winner).toBeDefined();
    expect(winner?.name).toBe('Guest Gina');
    expect(winner?.initials).toBe('GG');
    expect(typeof winner?.color).toBe('number'); // hue for the chip's hsl(color,60%,55%)
  });

  it('leaves winner undefined when the BE resolved no name (D7 — no synthesized winner)', () => {
    const vm = mapNightLiveToViewModel(
      live({ sessions: [session({ status: 'Completed', winnerId: null, winnerName: null })] }),
      NOW
    );
    expect(vm.plannedGames[0].winner).toBeUndefined();
  });

  it('exposes the sessionId→game lookup mapDiary joins against (Slice C2)', () => {
    const vm = mapNightLiveToViewModel(HAPPY, NOW);
    // The VM is live-only now — no diary fields (panel D2).
    expect(vm).not.toHaveProperty('diaryEvents');
    expect(vm.sessions).toEqual([
      { sessionId: S1, gameId: G1, gameTitle: 'Brass' },
      { sessionId: S2, gameId: G2, gameTitle: 'Spirit Island' },
      { sessionId: S3, gameId: G3, gameTitle: 'Wingspan' },
    ]);
  });

  it('leaves confirmedPlayers/totalPlayers undefined (no RSVP fetch in Slice B)', () => {
    const vm = mapNightLiveToViewModel(HAPPY, NOW);
    expect(vm.confirmedPlayers).toBeUndefined();
    expect(vm.totalPlayers).toBeUndefined();
  });
});

describe('mapNightLiveToViewModel — draft night, all Pending (#3188 Slice 1)', () => {
  // Deploy-first forward-compat guard: a night whose sessions are ALL Pending (no InProgress
  // sibling) is a valid *draft* night — it must render as 'transition', every planned game
  // 'upcoming', no current game. Pins the tolerance before later slices flip direct-create to
  // be born Pending, so the draft path can never silently regress into a crash/live misrender.
  it("all-Pending sessions map to a 'transition' draft with no current game", () => {
    const draft = live({
      sessions: [
        session({ sessionId: S1, gameId: G1, gameTitle: 'Brass', playOrder: 1, status: 'Pending' }),
        session({
          sessionId: S2,
          gameId: G2,
          gameTitle: 'Spirit Island',
          playOrder: 2,
          status: 'Pending',
        }),
        session({
          sessionId: S3,
          gameId: G3,
          gameTitle: 'Wingspan',
          playOrder: 3,
          status: 'Pending',
        }),
      ],
    });
    const vm = mapNightLiveToViewModel(draft, NOW);
    expect(vm.status).toBe('transition');
    expect(vm.plannedGames).toHaveLength(3);
    expect(vm.plannedGames.every(g => g.status === 'upcoming')).toBe(true);
    expect(vm.currentGame).toBeNull();
    expect(vm.current).toBe(0);
  });
});
