/**
 * Pure reducer for the SP7 game-night-create wizard (issue #950 W2).
 *
 * Spec: docs/superpowers/specs/2026-05-18-sp7-game-night-create.md §8.
 *
 * The reducer is exhaustive over `WizardAction` and never mutates the
 * incoming state. Every action handler is unit-tested in
 * `wizard-reducer.test.ts` (target ≥ 100% line/branch coverage).
 */

import { inviteeKey, type Invitee, type WizardAction, type WizardState } from './wizard-types';

/**
 * Initial state for a fresh wizard session.
 *
 * Step 1 by default; all branches in their "empty" form. Consumers that
 * need to hydrate from a persisted draft should dispatch
 * `restoreFromDraft` after mounting with this initial state.
 */
export const initialWizardState: WizardState = {
  step: 1,
  date: { iso: null, conflictCheckedAt: null, conflictResult: null },
  location: { kind: 'home', details: '' },
  invitees: [],
  games: { decideAtGroup: false, selected: [] },
  draft: { savedAt: null, status: 'idle' },
};

export function wizardReducer(state: WizardState, action: WizardAction): WizardState {
  switch (action.type) {
    case 'goToStep':
      // Identity short-circuit: avoids unnecessary re-renders downstream.
      return state.step === action.step ? state : { ...state, step: action.step };

    case 'setDate':
      return {
        ...state,
        date: {
          iso: action.iso,
          // Changing the date invalidates the previous conflict check; the
          // wizard re-runs the check effect on every iso change.
          conflictCheckedAt: null,
          conflictResult: null,
        },
      };

    case 'clearDate':
      return {
        ...state,
        date: { iso: null, conflictCheckedAt: null, conflictResult: null },
      };

    case 'recordConflict':
      return {
        ...state,
        date: {
          ...state.date,
          conflictCheckedAt: action.checkedAt,
          conflictResult: action.result,
        },
      };

    case 'setLocation':
      return {
        ...state,
        location: {
          kind: action.kind,
          // `tbd` collapses to empty details — the FE shouldn't expose a free
          // text input for "Da definire", and the BE shouldn't persist stale
          // details when the user reverts from a concrete kind to `tbd`.
          details: action.kind === 'tbd' ? '' : (action.details ?? state.location.details),
        },
      };

    case 'addInvitee': {
      const key = inviteeKey(action.invitee);
      // Idempotent add — the dedup invariant matches the BE validator
      // (`HasNoCaseInsensitiveDuplicates`) for emails.
      if (state.invitees.some(i => inviteeKey(i) === key)) {
        return state;
      }
      return { ...state, invitees: [...state.invitees, action.invitee] };
    }

    case 'removeInvitee': {
      const filtered = state.invitees.filter(i => inviteeKey(i) !== action.key);
      return filtered.length === state.invitees.length ? state : { ...state, invitees: filtered };
    }

    case 'toggleDecideAtGroup': {
      const nextDecide = !state.games.decideAtGroup;
      return {
        ...state,
        games: {
          decideAtGroup: nextDecide,
          // Spec §12 BE-Scenario 6: when "lascia decidere al gruppo" is on,
          // the submit payload has `gameIds: []`. We clear the selection in
          // state so toggling back off doesn't surface stale picks.
          selected: nextDecide ? [] : state.games.selected,
        },
      };
    }

    case 'toggleGame': {
      // Picking a game implicitly turns off "decide at group" so the UI and
      // submit payload stay consistent.
      const currentlySelected = state.games.selected.includes(action.gameId);
      const nextSelected = currentlySelected
        ? state.games.selected.filter(id => id !== action.gameId)
        : [...state.games.selected, action.gameId];

      return {
        ...state,
        games: { decideAtGroup: false, selected: nextSelected },
      };
    }

    case 'draftSaveStart':
      return { ...state, draft: { ...state.draft, status: 'saving' } };

    case 'draftSaveSuccess':
      return {
        ...state,
        draft: { savedAt: action.savedAt, status: 'saved' },
      };

    case 'draftSaveError':
      return { ...state, draft: { ...state.draft, status: 'error' } };

    case 'restoreFromDraft':
      return {
        step: action.draft.step,
        date: action.draft.date,
        location: action.draft.location,
        invitees: action.draft.invitees,
        games: action.draft.games,
        // The restored draft never resets to "saving" — restoration is a
        // read, not a write. Status returns to idle until the next mutation
        // triggers an autosave cycle.
        draft: { savedAt: null, status: 'idle' },
      };

    default: {
      // Exhaustiveness check — TypeScript will error here if a new action
      // is added without a handler.
      const _exhaustive: never = action;
      void _exhaustive;
      return state;
    }
  }
}

/**
 * Helper to dedup an array of invitees while preserving order. Useful for
 * tests and FE components that need to clean up legacy state (e.g. from a
 * draft persisted before dedup was enforced).
 */
export function dedupInvitees(invitees: readonly Invitee[]): readonly Invitee[] {
  const seen = new Set<string>();
  const result: Invitee[] = [];
  for (const invitee of invitees) {
    const key = inviteeKey(invitee);
    if (!seen.has(key)) {
      seen.add(key);
      result.push(invitee);
    }
  }
  return result;
}
