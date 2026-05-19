/**
 * Unit tests for the SP7 wizard reducer (issue #950 W2).
 *
 * Goal: exhaustive coverage of action handlers — every `WizardAction`
 * variant exercised at least once, with both side-effect-bearing and
 * identity (no-op) branches verified.
 */

import { describe, expect, it } from 'vitest';

import { dedupInvitees, initialWizardState, wizardReducer } from '../wizard-reducer';
import {
  WIZARD_DRAFT_SCHEMA_VERSION,
  inviteeKey,
  type Invitee,
  type PersistedDraft,
  type WizardState,
} from '../wizard-types';

const ALICE_USER: Invitee = {
  kind: 'user',
  id: '11111111-1111-1111-1111-111111111111',
  displayName: 'Alice',
  email: 'alice@example.com',
};

const BOB_USER: Invitee = {
  kind: 'user',
  id: '22222222-2222-2222-2222-222222222222',
  displayName: 'Bob',
  email: 'bob@example.com',
};

const EMAIL_INVITEE: Invitee = { kind: 'email', address: 'guest@example.com' };

describe('initialWizardState', () => {
  it('starts on step 1', () => {
    expect(initialWizardState.step).toBe(1);
  });

  it('has empty branches', () => {
    expect(initialWizardState.date.iso).toBeNull();
    expect(initialWizardState.invitees).toHaveLength(0);
    expect(initialWizardState.games.selected).toHaveLength(0);
    expect(initialWizardState.games.decideAtGroup).toBe(false);
    expect(initialWizardState.draft.status).toBe('idle');
  });
});

describe('wizardReducer: goToStep', () => {
  it('moves to the requested step', () => {
    const next = wizardReducer(initialWizardState, { type: 'goToStep', step: 3 });
    expect(next.step).toBe(3);
  });

  it('returns the identical state object when already on the target step (perf invariant)', () => {
    const next = wizardReducer(initialWizardState, { type: 'goToStep', step: 1 });
    expect(next).toBe(initialWizardState);
  });

  it('preserves other branches', () => {
    const seeded: WizardState = { ...initialWizardState, invitees: [ALICE_USER] };
    const next = wizardReducer(seeded, { type: 'goToStep', step: 4 });
    expect(next.invitees).toBe(seeded.invitees);
  });
});

describe('wizardReducer: setDate / clearDate / recordConflict', () => {
  it('setDate stores the ISO and invalidates any prior conflict check', () => {
    const seeded: WizardState = {
      ...initialWizardState,
      date: {
        iso: '2026-06-01T20:00:00Z',
        conflictCheckedAt: '2026-05-19T05:00:00Z',
        conflictResult: { hasConflict: false, conflicts: [] },
      },
    };
    const next = wizardReducer(seeded, { type: 'setDate', iso: '2026-06-02T20:00:00Z' });

    expect(next.date.iso).toBe('2026-06-02T20:00:00Z');
    expect(next.date.conflictCheckedAt).toBeNull();
    expect(next.date.conflictResult).toBeNull();
  });

  it('clearDate resets the entire date branch', () => {
    const seeded: WizardState = {
      ...initialWizardState,
      date: {
        iso: '2026-06-01T20:00:00Z',
        conflictCheckedAt: '2026-05-19T05:00:00Z',
        conflictResult: { hasConflict: true, conflicts: [] },
      },
    };
    const next = wizardReducer(seeded, { type: 'clearDate' });

    expect(next.date.iso).toBeNull();
    expect(next.date.conflictCheckedAt).toBeNull();
    expect(next.date.conflictResult).toBeNull();
  });

  it('recordConflict updates the conflict branch without touching iso', () => {
    const seeded: WizardState = {
      ...initialWizardState,
      date: { iso: '2026-06-01T20:00:00Z', conflictCheckedAt: null, conflictResult: null },
    };
    const next = wizardReducer(seeded, {
      type: 'recordConflict',
      checkedAt: '2026-05-19T05:00:00Z',
      result: {
        hasConflict: true,
        conflicts: [
          { id: 'gn1', title: 'Catan', scheduledAt: '2026-06-01T19:00:00Z', role: 'organizer' },
        ],
      },
    });

    expect(next.date.iso).toBe('2026-06-01T20:00:00Z');
    expect(next.date.conflictCheckedAt).toBe('2026-05-19T05:00:00Z');
    expect(next.date.conflictResult?.hasConflict).toBe(true);
    expect(next.date.conflictResult?.conflicts).toHaveLength(1);
  });
});

describe('wizardReducer: setLocation', () => {
  it('records the kind and details for a concrete location', () => {
    const next = wizardReducer(initialWizardState, {
      type: 'setLocation',
      kind: 'friend',
      details: "Sara's apartment",
    });
    expect(next.location.kind).toBe('friend');
    expect(next.location.details).toBe("Sara's apartment");
  });

  it('preserves previous details when omitted', () => {
    const seeded = wizardReducer(initialWizardState, {
      type: 'setLocation',
      kind: 'friend',
      details: "Sara's place",
    });
    const next = wizardReducer(seeded, { type: 'setLocation', kind: 'home' });
    // Switching to 'home' without explicit details keeps the previous string;
    // the FE input element controls what details is persisted.
    expect(next.location.details).toBe("Sara's place");
    expect(next.location.kind).toBe('home');
  });

  it('clears details when switching to "tbd"', () => {
    const seeded = wizardReducer(initialWizardState, {
      type: 'setLocation',
      kind: 'friend',
      details: "Sara's place",
    });
    const next = wizardReducer(seeded, { type: 'setLocation', kind: 'tbd' });
    expect(next.location.kind).toBe('tbd');
    expect(next.location.details).toBe('');
  });
});

describe('wizardReducer: addInvitee / removeInvitee', () => {
  it('adds a user invitee', () => {
    const next = wizardReducer(initialWizardState, { type: 'addInvitee', invitee: ALICE_USER });
    expect(next.invitees).toEqual([ALICE_USER]);
  });

  it('adds an email invitee', () => {
    const next = wizardReducer(initialWizardState, { type: 'addInvitee', invitee: EMAIL_INVITEE });
    expect(next.invitees).toEqual([EMAIL_INVITEE]);
  });

  it('is idempotent on duplicate user adds', () => {
    const once = wizardReducer(initialWizardState, { type: 'addInvitee', invitee: ALICE_USER });
    const twice = wizardReducer(once, { type: 'addInvitee', invitee: ALICE_USER });
    expect(twice).toBe(once);
  });

  it('is idempotent on case-insensitive email duplicates', () => {
    const once = wizardReducer(initialWizardState, {
      type: 'addInvitee',
      invitee: { kind: 'email', address: 'guest@example.com' },
    });
    const twice = wizardReducer(once, {
      type: 'addInvitee',
      invitee: { kind: 'email', address: 'GUEST@EXAMPLE.com' },
    });
    expect(twice).toBe(once);
  });

  it('removes an invitee by key', () => {
    const seeded: WizardState = { ...initialWizardState, invitees: [ALICE_USER, BOB_USER] };
    const next = wizardReducer(seeded, {
      type: 'removeInvitee',
      key: inviteeKey(ALICE_USER),
    });
    expect(next.invitees).toEqual([BOB_USER]);
  });

  it('returns identity when removeInvitee targets an unknown key', () => {
    const seeded: WizardState = { ...initialWizardState, invitees: [ALICE_USER] };
    const next = wizardReducer(seeded, { type: 'removeInvitee', key: 'user:nonexistent' });
    expect(next).toBe(seeded);
  });
});

describe('wizardReducer: toggleDecideAtGroup / toggleGame', () => {
  it('toggles decideAtGroup and clears selected games when turning on', () => {
    const seeded: WizardState = {
      ...initialWizardState,
      games: { decideAtGroup: false, selected: ['g1', 'g2'] },
    };
    const next = wizardReducer(seeded, { type: 'toggleDecideAtGroup' });
    expect(next.games.decideAtGroup).toBe(true);
    expect(next.games.selected).toEqual([]);
  });

  it('toggles decideAtGroup back off without restoring selected games (FE re-picks)', () => {
    const seeded: WizardState = {
      ...initialWizardState,
      games: { decideAtGroup: true, selected: [] },
    };
    const next = wizardReducer(seeded, { type: 'toggleDecideAtGroup' });
    expect(next.games.decideAtGroup).toBe(false);
    expect(next.games.selected).toEqual([]);
  });

  it('toggleGame adds a new game to selection and clears decideAtGroup', () => {
    const seeded: WizardState = {
      ...initialWizardState,
      games: { decideAtGroup: true, selected: [] },
    };
    const next = wizardReducer(seeded, { type: 'toggleGame', gameId: 'catan' });
    expect(next.games.selected).toEqual(['catan']);
    expect(next.games.decideAtGroup).toBe(false);
  });

  it('toggleGame removes an already-selected game', () => {
    const seeded: WizardState = {
      ...initialWizardState,
      games: { decideAtGroup: false, selected: ['catan', 'azul'] },
    };
    const next = wizardReducer(seeded, { type: 'toggleGame', gameId: 'catan' });
    expect(next.games.selected).toEqual(['azul']);
  });
});

describe('wizardReducer: draft lifecycle', () => {
  it('draftSaveStart sets status to "saving"', () => {
    const next = wizardReducer(initialWizardState, { type: 'draftSaveStart' });
    expect(next.draft.status).toBe('saving');
    expect(next.draft.savedAt).toBeNull();
  });

  it('draftSaveSuccess records timestamp and clears status', () => {
    const next = wizardReducer(initialWizardState, {
      type: 'draftSaveSuccess',
      savedAt: '2026-05-19T05:00:00Z',
    });
    expect(next.draft.status).toBe('saved');
    expect(next.draft.savedAt).toBe('2026-05-19T05:00:00Z');
  });

  it('draftSaveError sets status to "error" without changing savedAt', () => {
    const seeded: WizardState = {
      ...initialWizardState,
      draft: { savedAt: '2026-05-18T20:00:00Z', status: 'saved' },
    };
    const next = wizardReducer(seeded, { type: 'draftSaveError' });
    expect(next.draft.status).toBe('error');
    expect(next.draft.savedAt).toBe('2026-05-18T20:00:00Z');
  });
});

describe('wizardReducer: restoreFromDraft', () => {
  it('hydrates state from a persisted snapshot and resets draft branch to idle', () => {
    const draft: PersistedDraft = {
      schemaVersion: WIZARD_DRAFT_SCHEMA_VERSION,
      step: 3,
      date: { iso: '2026-06-01T20:00:00Z', conflictCheckedAt: null, conflictResult: null },
      location: { kind: 'friend', details: "Sara's place" },
      invitees: [ALICE_USER],
      games: { decideAtGroup: false, selected: ['catan'] },
    };

    const next = wizardReducer(initialWizardState, { type: 'restoreFromDraft', draft });

    expect(next.step).toBe(3);
    expect(next.invitees).toHaveLength(1);
    expect(next.games.selected).toEqual(['catan']);
    expect(next.draft.status).toBe('idle');
    expect(next.draft.savedAt).toBeNull();
  });
});

describe('dedupInvitees', () => {
  it('removes case-insensitive email duplicates while preserving order', () => {
    const result = dedupInvitees([
      ALICE_USER,
      { kind: 'email', address: 'guest@example.com' },
      ALICE_USER,
      { kind: 'email', address: 'GUEST@example.com' },
    ]);
    expect(result).toHaveLength(2);
    expect(result[0]).toEqual(ALICE_USER);
    expect((result[1] as { kind: 'email'; address: string }).address).toBe('guest@example.com');
  });

  it('returns an empty array for empty input', () => {
    expect(dedupInvitees([])).toEqual([]);
  });
});
