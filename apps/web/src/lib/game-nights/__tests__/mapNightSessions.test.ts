/**
 * mapNightSessions — #4752 (the night → live game bridge).
 *
 * The night DETAIL page lists the sessions of the night-live read. These tests pin the badge rules
 * (canonical liveness over the raw link status; Skipped and Corrupted both "not played to the end"),
 * the order, and that the live game of each sitting reaches the list.
 */

import { describe, it, expect } from 'vitest';

import type { GameNightLiveDto } from '@/lib/api/schemas/game-nights.schemas';

import { mapNightSessions } from '../mapNightSessions';

const GN_ID = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const G1 = '11111111-1111-4111-8111-111111111111';
const G2 = '22222222-2222-4222-8222-222222222222';
const S1 = 'aaaa1111-1111-4111-8111-111111111111';
const S2 = 'aaaa2222-2222-4222-8222-222222222222';
const S3 = 'aaaa3333-3333-4333-8333-333333333333';
const LIVE1 = 'bbbb1111-1111-4111-8111-111111111111';

type SessionDto = GameNightLiveDto['sessions'][number];

function session(over: Partial<SessionDto> = {}): SessionDto {
  return {
    sessionId: S1,
    gameId: G1,
    gameTitle: 'Catan',
    playOrder: 1,
    status: 'Pending',
    winnerId: null,
    winnerName: null,
    startedAt: null,
    completedAt: null,
    ...over,
  };
}

function live(sessions: SessionDto[]): GameNightLiveDto {
  return {
    id: GN_ID,
    title: 'Serata',
    status: 'InProgress',
    sessions,
    isViewerOrganizer: true,
    plannedLineup: [],
    currentSessionRoster: [],
  };
}

describe('mapNightSessions', () => {
  it('projects each sitting onto the shape the detail page lists', () => {
    const [mapped] = mapNightSessions(
      live([
        session({
          sessionId: S1,
          gameId: G1,
          gameTitle: 'Catan',
          playOrder: 1,
          status: 'Completed',
          winnerId: 'cccc1111-1111-4111-8111-111111111111',
          startedAt: '2026-07-04T20:00:00Z',
          completedAt: '2026-07-04T21:00:00Z',
        }),
      ])
    );

    expect(mapped).toEqual({
      id: S1,
      gameNightSessionId: S1,
      gameId: G1,
      gameTitle: 'Catan',
      playOrder: 1,
      status: 'completed',
      winnerId: 'cccc1111-1111-4111-8111-111111111111',
      startedAt: '2026-07-04T20:00:00Z',
      completedAt: '2026-07-04T21:00:00Z',
      liveSessionId: undefined,
    });
  });

  it('carries the live game of each sitting, and none for a sitting without one', () => {
    const [withLive, withoutLive, nullLive] = mapNightSessions(
      live([
        session({ sessionId: S1, playOrder: 1, liveSessionId: LIVE1 }),
        session({ sessionId: S2, playOrder: 2 }),
        session({ sessionId: S3, playOrder: 3, liveSessionId: null }),
      ])
    );

    expect(withLive.liveSessionId).toBe(LIVE1);
    expect(withoutLive.liveSessionId).toBeUndefined();
    expect(nullLive.liveSessionId).toBeUndefined();
  });

  it('orders the sittings by play order, whatever order the read returned them in', () => {
    const mapped = mapNightSessions(
      live([
        session({ sessionId: S2, gameId: G2, gameTitle: 'Dixit', playOrder: 2 }),
        session({ sessionId: S1, gameId: G1, gameTitle: 'Catan', playOrder: 1 }),
      ])
    );

    expect(mapped.map(s => s.gameTitle)).toEqual(['Catan', 'Dixit']);
  });

  it.each([
    ['Pending', 'pending'],
    ['InProgress', 'in_progress'],
    ['Completed', 'completed'],
    ['Skipped', 'skipped'],
    ['Corrupted', 'skipped'],
  ] as const)('maps the link status %s to the badge %s', (status, expected) => {
    expect(mapNightSessions(live([session({ status })]))[0].status).toBe(expected);
  });

  it('lets the canonical liveness win: a link not InProgress whose tracking session is live is in progress', () => {
    const [mapped] = mapNightSessions(live([session({ status: 'Pending', isLive: true })]));

    expect(mapped.status).toBe('in_progress');
  });

  it('keeps a link InProgress as in progress even when the canonical flag says not live (blocks finalizing)', () => {
    // The night cannot be finalized while a link is InProgress (the backend refuses): the list must not
    // offer «Concludi serata» on a split-brain row it would then refuse.
    const [mapped] = mapNightSessions(live([session({ status: 'InProgress', isLive: false })]));

    expect(mapped.status).toBe('in_progress');
  });

  it('is empty for a night with no sittings', () => {
    expect(mapNightSessions(live([]))).toEqual([]);
  });
});
