/**
 * Tests for RSVP state machine (Issue #951 AC-H2 revision).
 *
 * Coverage maps directly to AC-H1 GWT scenarios:
 *   - Scenario "Capacity exceeded" exercises the `allowed` branch (server returns 409 on capacity)
 *   - Scenario "Double-RSVP" → `no-op`
 *   - Scenario "Game-night cancelled" → 410 reject
 *   - Scenario "Concurrent edit" → server 409 path, mapped to i18n keys by the consumer
 *     (`GameNightDetailView.handleRsvp` translates status codes to
 *     `gameNightDetail.rsvp.errors.{cancelledGone,directConflict}`)
 */

import { describe, expect, it } from 'vitest';

import { evaluateRsvpTransition } from '../rsvp-state-machine';

describe('evaluateRsvpTransition', () => {
  describe('no-op (same response replay)', () => {
    it.each(['Accepted', 'Declined', 'Maybe'] as const)(
      'classifies %s → %s as no-op (AC-H1 double-RSVP)',
      response => {
        const outcome = evaluateRsvpTransition({ current: response, desired: response });
        expect(outcome).toEqual({ kind: 'no-op', reason: 'same-response' });
      }
    );
  });

  describe('terminal state rejection (410 Gone)', () => {
    it.each(['Expired', 'Cancelled'] as const)(
      'rejects RSVP attempt when invitation is %s with 410 (AC-H1 cancelled mid-session)',
      terminal => {
        const outcome = evaluateRsvpTransition({ current: terminal, desired: 'Accepted' });
        expect(outcome.kind).toBe('rejected');
        if (outcome.kind === 'rejected') {
          expect(outcome.status).toBe(410);
          expect(outcome.reason).toContain(terminal.toLowerCase());
        }
      }
    );
  });

  describe('direct Accepted ⇄ Declined conflict (409)', () => {
    it('rejects Accepted → Declined with 409', () => {
      const outcome = evaluateRsvpTransition({ current: 'Accepted', desired: 'Declined' });
      expect(outcome).toEqual({
        kind: 'rejected',
        status: 409,
        reason: 'Cannot switch directly between Accepted and Declined.',
      });
    });

    it('rejects Declined → Accepted with 409', () => {
      const outcome = evaluateRsvpTransition({ current: 'Declined', desired: 'Accepted' });
      expect(outcome.kind).toBe('rejected');
      if (outcome.kind === 'rejected') {
        expect(outcome.status).toBe(409);
      }
    });
  });

  describe('allowed transitions', () => {
    it.each([
      ['Pending', 'Accepted'],
      ['Pending', 'Declined'],
      ['Pending', 'Maybe'],
      ['Maybe', 'Accepted'],
      ['Maybe', 'Declined'],
      ['Accepted', 'Maybe'],
      ['Declined', 'Maybe'],
    ] as const)('allows %s → %s', (current, desired) => {
      const outcome = evaluateRsvpTransition({ current, desired });
      expect(outcome).toEqual({ kind: 'allowed', from: current, to: desired });
    });
  });
});
