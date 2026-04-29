/**
 * deriveState — pure FSM helper tests (Wave A.5b, Issue #611).
 *
 * Covers the 7-state derivation matrix from spec §3.2 and the resolution
 * priority documented in `derive-state.ts` (override > banner > mutation
 * outcome > server snapshot > pending).
 *
 * Why this matters: the FSM is the single source of truth for which surface
 * to render in `page-client.tsx`. A regression here flips users into the
 * wrong banner/shell on idempotent retries, conflict resolutions, or after
 * SSR fetch failures.
 */

import { describe, expect, it } from 'vitest';

import type { PublicGameNightInvitation } from '@/lib/api/game-night-invitations';

import { deriveState, parseStateOverride, VALID_STATE_OVERRIDES } from '../derive-state';

import type { DeriveStateArgs } from '../derive-state';

const PENDING_INVITATION: PublicGameNightInvitation = {
  token: 'tok-pending',
  status: 'Pending',
  expiresAt: '2026-05-30T00:00:00.000Z',
  respondedAt: null,
  hostUserId: '00000000-0000-4000-8000-000000000001',
  hostDisplayName: 'Alex Host',
  hostAvatarUrl: null,
  hostWelcomeMessage: null,
  gameNightId: '00000000-0000-4000-8000-000000000002',
  title: 'Friday Game Night',
  scheduledAt: '2026-05-15T20:00:00.000Z',
  location: 'Living Room',
  durationMinutes: 180,
  expectedPlayers: 4,
  acceptedSoFar: 1,
  primaryGameId: '00000000-0000-4000-8000-000000000003',
  primaryGameName: 'Wingspan',
  primaryGameImageUrl: null,
  alreadyRespondedAs: null,
};

function baseArgs(overrides: Partial<DeriveStateArgs> = {}): DeriveStateArgs {
  return {
    data: PENDING_INVITATION,
    hasSession: false,
    mutationKind: null,
    mutationAction: null,
    initialBannerState: undefined,
    stateOverride: undefined,
    ...overrides,
  };
}

describe('deriveState (Wave A.5b)', () => {
  describe('priority 1 — visual-test override', () => {
    it('returns the override regardless of any other input', () => {
      expect(
        deriveState(
          baseArgs({
            stateOverride: 'token-invalid',
            data: PENDING_INVITATION,
            hasSession: true,
            mutationKind: 'success',
            mutationAction: 'Accepted',
            initialBannerState: 'token-invalid',
          })
        )
      ).toBe('token-invalid');
    });

    it.each([
      'default',
      'logged-in',
      'accepted-success',
      'declined',
      'token-expired',
      'token-invalid',
      'already-accepted',
    ] as const)('passes through %s override', state => {
      expect(deriveState(baseArgs({ stateOverride: state }))).toBe(state);
    });
  });

  describe('priority 2 — SSR initialBannerState', () => {
    it('returns token-invalid when SSR signaled the 404', () => {
      expect(deriveState(baseArgs({ data: undefined, initialBannerState: 'token-invalid' }))).toBe(
        'token-invalid'
      );
    });

    it('outranks GET data and mutation outcome', () => {
      expect(
        deriveState(
          baseArgs({
            initialBannerState: 'token-invalid',
            data: PENDING_INVITATION,
            mutationKind: 'success',
            mutationAction: 'Accepted',
          })
        )
      ).toBe('token-invalid');
    });
  });

  describe('priority 3 — mutation outcome', () => {
    it('maps success + Accepted → accepted-success', () => {
      expect(deriveState(baseArgs({ mutationKind: 'success', mutationAction: 'Accepted' }))).toBe(
        'accepted-success'
      );
    });

    it('maps success + Declined → declined', () => {
      expect(deriveState(baseArgs({ mutationKind: 'success', mutationAction: 'Declined' }))).toBe(
        'declined'
      );
    });

    it('maps gone → token-expired regardless of action', () => {
      expect(deriveState(baseArgs({ mutationKind: 'gone', mutationAction: 'Accepted' }))).toBe(
        'token-expired'
      );
      expect(deriveState(baseArgs({ mutationKind: 'gone', mutationAction: 'Declined' }))).toBe(
        'token-expired'
      );
    });

    it('does NOT transition on conflict-state-switch (banner overlay only)', () => {
      // Conflict surfaces as banner over existing surface; FSM stays on the
      // server-snapshot-derived state.
      expect(
        deriveState(
          baseArgs({
            mutationKind: 'conflict-state-switch',
            mutationAction: 'Accepted',
            data: { ...PENDING_INVITATION, alreadyRespondedAs: 'Declined' },
          })
        )
      ).toBe('already-accepted');
    });

    it('outranks server-side snapshot when mutation succeeded', () => {
      // User just accepted — even if cached `alreadyRespondedAs` were stale,
      // we trust the mutation result.
      expect(
        deriveState(
          baseArgs({
            data: { ...PENDING_INVITATION, alreadyRespondedAs: 'Declined' },
            mutationKind: 'success',
            mutationAction: 'Accepted',
          })
        )
      ).toBe('accepted-success');
    });
  });

  describe('priority 4 — server snapshot', () => {
    it('maps Expired status → token-expired', () => {
      expect(deriveState(baseArgs({ data: { ...PENDING_INVITATION, status: 'Expired' } }))).toBe(
        'token-expired'
      );
    });

    it('maps Cancelled status → token-expired', () => {
      expect(deriveState(baseArgs({ data: { ...PENDING_INVITATION, status: 'Cancelled' } }))).toBe(
        'token-expired'
      );
    });

    it('maps alreadyRespondedAs=Accepted → already-accepted', () => {
      expect(
        deriveState(baseArgs({ data: { ...PENDING_INVITATION, alreadyRespondedAs: 'Accepted' } }))
      ).toBe('already-accepted');
    });

    it('maps alreadyRespondedAs=Declined → already-accepted', () => {
      expect(
        deriveState(baseArgs({ data: { ...PENDING_INVITATION, alreadyRespondedAs: 'Declined' } }))
      ).toBe('already-accepted');
    });
  });

  describe('priority 5 — pending fallback', () => {
    it('returns default for anonymous user with pending data', () => {
      expect(deriveState(baseArgs({ hasSession: false }))).toBe('default');
    });

    it('returns logged-in for authenticated user with pending data', () => {
      expect(deriveState(baseArgs({ hasSession: true }))).toBe('logged-in');
    });

    it('returns default for anonymous user with no data + no banner', () => {
      // Transient client-fetch error: render default shell, hook surfaces
      // its own error banner.
      expect(deriveState(baseArgs({ data: undefined, hasSession: false }))).toBe('default');
    });

    it('returns logged-in for authenticated user with no data + no banner', () => {
      expect(deriveState(baseArgs({ data: undefined, hasSession: true }))).toBe('logged-in');
    });
  });
});

describe('parseStateOverride (Wave A.5b)', () => {
  it('returns undefined when disabled regardless of input', () => {
    expect(parseStateOverride('default', { enabled: false })).toBeUndefined();
    expect(parseStateOverride('token-invalid', { enabled: false })).toBeUndefined();
  });

  it('returns undefined when raw is null or empty', () => {
    expect(parseStateOverride(null, { enabled: true })).toBeUndefined();
    expect(parseStateOverride('', { enabled: true })).toBeUndefined();
  });

  it('returns undefined for unknown values', () => {
    expect(parseStateOverride('not-a-state', { enabled: true })).toBeUndefined();
    expect(parseStateOverride('SUCCESS', { enabled: true })).toBeUndefined(); // case-sensitive
  });

  it.each(Array.from(VALID_STATE_OVERRIDES))('passes through valid state %s', state => {
    expect(parseStateOverride(state, { enabled: true })).toBe(state);
  });
});
