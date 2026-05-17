/**
 * Tests for actor classification (Issue #951 AC-H3).
 */

import { describe, expect, it } from 'vitest';

import type { GameNightDto, GameNightRsvpDto } from '@/lib/api/schemas/game-nights.schemas';

import { can, classifyGameNightActor } from '../actor-classification';

const HOST_ID = '11111111-1111-1111-1111-111111111111';
const GUEST_ID = '22222222-2222-2222-2222-222222222222';
const BYSTANDER_ID = '33333333-3333-3333-3333-333333333333';

function buildEvent(overrides: Partial<GameNightDto> = {}): GameNightDto {
  return {
    id: '00000000-0000-0000-0000-0000000000aa',
    organizerId: HOST_ID,
    organizerName: 'Marco R.',
    title: 'Serata Test',
    description: null,
    scheduledAt: '2026-06-01T20:00:00Z',
    location: null,
    maxPlayers: 8,
    gameIds: [],
    status: 'Published',
    acceptedCount: 1,
    pendingCount: 0,
    totalInvited: 1,
    createdAt: '2026-05-15T10:00:00Z',
    updatedAt: null,
    ...overrides,
  };
}

function buildRsvp(overrides: Partial<GameNightRsvpDto> = {}): GameNightRsvpDto {
  return {
    id: '00000000-0000-0000-0000-0000000000bb',
    userId: GUEST_ID,
    userName: 'Davide C.',
    status: 'Accepted',
    respondedAt: '2026-05-15T11:00:00Z',
    createdAt: '2026-05-15T10:30:00Z',
    ...overrides,
  };
}

describe('classifyGameNightActor', () => {
  it('returns host when viewer is the organizer', () => {
    const ctx = classifyGameNightActor({
      viewerId: HOST_ID,
      event: buildEvent(),
      rsvps: [],
    });

    expect(ctx.actor).toBe('host');
    expect(ctx.rsvp).toBeUndefined();
  });

  it('returns guest with attached rsvp when viewer has an RSVP entry', () => {
    const rsvp = buildRsvp({ userId: GUEST_ID, status: 'Maybe' });
    const ctx = classifyGameNightActor({
      viewerId: GUEST_ID,
      event: buildEvent(),
      rsvps: [rsvp],
    });

    expect(ctx.actor).toBe('guest');
    expect(ctx.rsvp).toBe(rsvp);
  });

  it('returns bystander with private default when viewer is unknown to the event', () => {
    const ctx = classifyGameNightActor({
      viewerId: BYSTANDER_ID,
      event: buildEvent(),
      rsvps: [buildRsvp({ userId: GUEST_ID })],
    });

    expect(ctx.actor).toBe('bystander');
    expect(ctx.isPublicEvent).toBe(false);
  });

  it('handles undefined rsvps list (in-flight query) as bystander when not the host', () => {
    const ctx = classifyGameNightActor({
      viewerId: BYSTANDER_ID,
      event: buildEvent(),
      rsvps: undefined,
    });

    expect(ctx.actor).toBe('bystander');
  });

  it('prefers host classification over rsvp entry edge case', () => {
    // Defensive: even if backend somehow returns an RSVP for the host (self-RSVP edge),
    // classification must surface host privileges, not guest.
    const ctx = classifyGameNightActor({
      viewerId: HOST_ID,
      event: buildEvent(),
      rsvps: [buildRsvp({ userId: HOST_ID, status: 'Accepted' })],
    });

    expect(ctx.actor).toBe('host');
  });
});

describe('can permission predicates', () => {
  const hostCtx = { actor: 'host' as const };
  const guestCtx = { actor: 'guest' as const, rsvp: buildRsvp() };
  const bystanderPrivate = { actor: 'bystander' as const, isPublicEvent: false };
  const bystanderPublic = { actor: 'bystander' as const, isPublicEvent: true };

  it.each([
    ['edit', hostCtx, true],
    ['edit', guestCtx, false],
    ['edit', bystanderPrivate, false],
    ['cancel', hostCtx, true],
    ['cancel', guestCtx, false],
    ['kickGuest', hostCtx, true],
    ['kickGuest', bystanderPrivate, false],
    ['rsvp', guestCtx, true],
    ['rsvp', hostCtx, false],
    ['rsvp', bystanderPrivate, false],
    ['requestInvite', bystanderPrivate, true],
    ['requestInvite', bystanderPublic, false],
    ['requestInvite', hostCtx, false],
    ['viewPrivateRoster', hostCtx, true],
    ['viewPrivateRoster', guestCtx, true],
    ['viewPrivateRoster', bystanderPrivate, false],
  ] as const)('can.%s for %j → %s', (action, ctx, expected) => {
    expect(can[action](ctx)).toBe(expected);
  });
});
