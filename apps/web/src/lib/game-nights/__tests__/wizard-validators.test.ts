/**
 * Unit tests for the SP7 wizard Zod validators + step-completion predicates.
 * Issue #950 W2 Foundation.
 */

import { describe, expect, it } from 'vitest';

import { initialWizardState } from '../wizard-reducer';
import type { Invitee, WizardState } from '../wizard-types';
import {
  buildSubmitPayload,
  isStepComplete,
  isWizardComplete,
  MAX_COMBINED_INVITEES,
  MAX_EMAIL_LENGTH,
  persistedDraftSchema,
  step1DateSchema,
  step3InviteesSchema,
  step4GamesSchema,
  submitPayloadSchema,
} from '../wizard-validators';

const futureIso = (days = 7): string => {
  const d = new Date();
  d.setDate(d.getDate() + days);
  return d.toISOString();
};

const ALICE: Invitee = {
  kind: 'user',
  id: '11111111-1111-4111-a111-111111111111',
  displayName: 'Alice',
  email: 'alice@example.com',
};

const VALID_GAME_ID = '33333333-3333-4333-a333-333333333333';

const VALID_EMAIL: Invitee = { kind: 'email', address: 'guest@example.com' };

describe('step1DateSchema', () => {
  it('accepts an ISO date > 1h in the future', () => {
    expect(step1DateSchema.safeParse({ iso: futureIso(7) }).success).toBe(true);
  });

  it('rejects an empty ISO', () => {
    expect(step1DateSchema.safeParse({ iso: '' }).success).toBe(false);
  });

  it('rejects a malformed ISO', () => {
    expect(step1DateSchema.safeParse({ iso: 'not-a-date' }).success).toBe(false);
  });

  it('rejects a past ISO', () => {
    const yesterday = new Date();
    yesterday.setDate(yesterday.getDate() - 1);
    expect(step1DateSchema.safeParse({ iso: yesterday.toISOString() }).success).toBe(false);
  });

  it('rejects an ISO 30 minutes in the future (under 1h cap)', () => {
    const soon = new Date(Date.now() + 30 * 60 * 1000).toISOString();
    expect(step1DateSchema.safeParse({ iso: soon }).success).toBe(false);
  });
});

describe('step3InviteesSchema', () => {
  it('accepts an empty list', () => {
    expect(step3InviteesSchema.safeParse([]).success).toBe(true);
  });

  it('accepts a mix of user + email invitees', () => {
    expect(step3InviteesSchema.safeParse([ALICE, VALID_EMAIL]).success).toBe(true);
  });

  it('rejects email > 200 chars', () => {
    const long = `${'a'.repeat(195)}@example.com`;
    const result = step3InviteesSchema.safeParse([{ kind: 'email', address: long }]);
    expect(result.success).toBe(false);
  });

  it('rejects invalid email format', () => {
    expect(step3InviteesSchema.safeParse([{ kind: 'email', address: 'no-at-sign' }]).success).toBe(
      false
    );
  });

  it(`rejects > ${MAX_COMBINED_INVITEES} entries`, () => {
    const fifty: Invitee[] = Array.from({ length: 50 }, (_, i) => ({
      kind: 'email',
      address: `u${i}@example.com`,
    }));
    expect(step3InviteesSchema.safeParse(fifty).success).toBe(false);
  });

  it('rejects case-insensitive email duplicates', () => {
    const dupes: Invitee[] = [
      { kind: 'email', address: 'guest@example.com' },
      { kind: 'email', address: 'GUEST@example.com' },
    ];
    expect(step3InviteesSchema.safeParse(dupes).success).toBe(false);
  });

  it('rejects duplicate user ids', () => {
    const dupes: Invitee[] = [ALICE, ALICE];
    expect(step3InviteesSchema.safeParse(dupes).success).toBe(false);
  });
});

describe('step4GamesSchema', () => {
  it('accepts decideAtGroup=true with empty selection', () => {
    expect(step4GamesSchema.safeParse({ decideAtGroup: true, selected: [] }).success).toBe(true);
  });

  it('accepts at least one game with decideAtGroup=false', () => {
    const result = step4GamesSchema.safeParse({
      decideAtGroup: false,
      selected: [VALID_GAME_ID],
    });
    expect(result.success).toBe(true);
  });

  it('rejects decideAtGroup=false with empty selection', () => {
    expect(step4GamesSchema.safeParse({ decideAtGroup: false, selected: [] }).success).toBe(false);
  });

  it('rejects > 20 games', () => {
    const tooMany = Array.from({ length: 21 }, () => VALID_GAME_ID);
    expect(step4GamesSchema.safeParse({ decideAtGroup: false, selected: tooMany }).success).toBe(
      false
    );
  });
});

describe('isStepComplete', () => {
  it('step 1 incomplete when date not set', () => {
    expect(isStepComplete(initialWizardState, 1)).toBe(false);
  });

  it('step 1 complete when date is in the future', () => {
    const state: WizardState = {
      ...initialWizardState,
      date: { iso: futureIso(7), conflictCheckedAt: null, conflictResult: null },
    };
    expect(isStepComplete(state, 1)).toBe(true);
  });

  it('step 2 always complete on initial state (kind defaults to "home")', () => {
    expect(isStepComplete(initialWizardState, 2)).toBe(true);
  });

  it('step 3 complete with empty invitees', () => {
    expect(isStepComplete(initialWizardState, 3)).toBe(true);
  });

  it('step 4 incomplete with no games and no decideAtGroup', () => {
    expect(isStepComplete(initialWizardState, 4)).toBe(false);
  });

  it('step 4 complete with decideAtGroup=true', () => {
    const state: WizardState = {
      ...initialWizardState,
      games: { decideAtGroup: true, selected: [] },
    };
    expect(isStepComplete(state, 4)).toBe(true);
  });
});

describe('isWizardComplete', () => {
  it('false on initial state (step 1 + 4 incomplete)', () => {
    expect(isWizardComplete(initialWizardState)).toBe(false);
  });

  it('true on fully populated state', () => {
    const state: WizardState = {
      ...initialWizardState,
      date: { iso: futureIso(7), conflictCheckedAt: null, conflictResult: null },
      games: { decideAtGroup: true, selected: [] },
    };
    expect(isWizardComplete(state)).toBe(true);
  });
});

describe('buildSubmitPayload', () => {
  it('returns ok with a valid payload', () => {
    const state: WizardState = {
      ...initialWizardState,
      date: { iso: futureIso(7), conflictCheckedAt: null, conflictResult: null },
      location: { kind: 'friend', details: "Sara's place" },
      invitees: [ALICE, VALID_EMAIL],
      games: { decideAtGroup: false, selected: [VALID_GAME_ID] },
    };

    const result = buildSubmitPayload(state, { title: 'Friday Catan' });
    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.payload.title).toBe('Friday Catan');
      expect(result.payload.invitedUserIds).toEqual([ALICE.id]);
      expect(result.payload.invitedEmails).toEqual(['guest@example.com']);
      expect(result.payload.gameIds).toEqual([VALID_GAME_ID]);
      expect(result.payload.location).toBe("Sara's place");
    }
  });

  it('normalizes emails to lowercase', () => {
    const state: WizardState = {
      ...initialWizardState,
      date: { iso: futureIso(7), conflictCheckedAt: null, conflictResult: null },
      invitees: [{ kind: 'email', address: '  GUEST@Example.COM  ' }],
      games: { decideAtGroup: true, selected: [] },
    };

    const result = buildSubmitPayload(state, { title: 'Friday Catan' });
    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.payload.invitedEmails).toEqual(['guest@example.com']);
    }
  });

  it('sends gameIds: [] when decideAtGroup is on (BE Scenario 6)', () => {
    const state: WizardState = {
      ...initialWizardState,
      date: { iso: futureIso(7), conflictCheckedAt: null, conflictResult: null },
      games: { decideAtGroup: true, selected: [] },
    };

    const result = buildSubmitPayload(state, { title: 'Friday Catan' });
    expect(result.ok).toBe(true);
    if (result.ok) {
      expect(result.payload.gameIds).toEqual([]);
    }
  });

  it('returns ok:false with issues on invalid state', () => {
    const state: WizardState = {
      ...initialWizardState,
      date: { iso: '', conflictCheckedAt: null, conflictResult: null },
      games: { decideAtGroup: true, selected: [] },
    };

    const result = buildSubmitPayload(state, { title: 'X' }); // title too short + bad ISO
    expect(result.ok).toBe(false);
    if (!result.ok) {
      expect(result.issues.length).toBeGreaterThan(0);
    }
  });

  it('returns ok:false when combined invitees exceed 49', () => {
    const fifty: Invitee[] = Array.from({ length: 50 }, (_, i) => ({
      kind: 'email',
      address: `u${i}@example.com`,
    }));
    const state: WizardState = {
      ...initialWizardState,
      date: { iso: futureIso(7), conflictCheckedAt: null, conflictResult: null },
      invitees: fifty,
      games: { decideAtGroup: true, selected: [] },
    };

    const result = buildSubmitPayload(state, { title: 'Friday Catan' });
    expect(result.ok).toBe(false);
  });
});

describe('submitPayloadSchema combined limit', () => {
  it(`rejects 25 userIds + 25 emails (total ${MAX_COMBINED_INVITEES + 1})`, () => {
    const userIds = Array.from(
      { length: 25 },
      (_, i) => '00000000-0000-4000-a000-' + i.toString().padStart(12, '0')
    );
    const emails = Array.from({ length: 25 }, (_, i) => `u${i}@example.com`);
    const result = submitPayloadSchema.safeParse({
      title: 'Friday Catan',
      scheduledAt: futureIso(7),
      invitedUserIds: userIds,
      invitedEmails: emails,
    });
    expect(result.success).toBe(false);
  });

  it(`accepts 25 userIds + 24 emails (total ${MAX_COMBINED_INVITEES})`, () => {
    const userIds = Array.from(
      { length: 25 },
      (_, i) => '00000000-0000-4000-a000-' + i.toString().padStart(12, '0')
    );
    const emails = Array.from({ length: 24 }, (_, i) => `u${i}@example.com`);
    const result = submitPayloadSchema.safeParse({
      title: 'Friday Catan',
      scheduledAt: futureIso(7),
      invitedUserIds: userIds,
      invitedEmails: emails,
    });
    expect(result.success).toBe(true);
  });
});

describe('persistedDraftSchema', () => {
  it('accepts a valid draft', () => {
    const draft = {
      schemaVersion: 1,
      step: 2,
      date: { iso: futureIso(7), conflictCheckedAt: null, conflictResult: null },
      location: { kind: 'home', details: '' },
      invitees: [],
      games: { decideAtGroup: false, selected: [] },
    };
    expect(persistedDraftSchema.safeParse(draft).success).toBe(true);
  });

  it('rejects a draft with a non-matching schemaVersion', () => {
    const draft = {
      schemaVersion: 999,
      step: 2,
      date: { iso: futureIso(7), conflictCheckedAt: null, conflictResult: null },
      location: { kind: 'home', details: '' },
      invitees: [],
      games: { decideAtGroup: false, selected: [] },
    };
    expect(persistedDraftSchema.safeParse(draft).success).toBe(false);
  });
});

describe('email length cap', () => {
  it(`accepts emails up to ${MAX_EMAIL_LENGTH} chars`, () => {
    const localLen = MAX_EMAIL_LENGTH - '@example.com'.length;
    const atLimit = `${'a'.repeat(localLen)}@example.com`;
    expect(atLimit.length).toBe(MAX_EMAIL_LENGTH);
    const result = step3InviteesSchema.safeParse([{ kind: 'email', address: atLimit }]);
    expect(result.success).toBe(true);
  });
});
