/**
 * Type definitions for the SP7 game-night-create wizard (issue #950 W2).
 *
 * The wizard state is decomposed by step so reducers can target individual
 * branches without rebuilding the whole tree. Discriminated unions on
 * `invitees` and `WizardAction` keep type narrowing exact.
 *
 * Spec: docs/superpowers/specs/2026-05-18-sp7-game-night-create.md §8.
 */

export type WizardStep = 1 | 2 | 3 | 4;

export type LocationKind = 'home' | 'friend' | 'online' | 'tbd';

export type DraftStatus = 'idle' | 'saving' | 'saved' | 'error';

/**
 * Single conflict entry returned by the conflict-check backend (W1-PR2
 * `GET /api/v1/game-nights/check-conflict`). Mirrors the BE
 * `ConflictEntryDto` shape so the wizard can render warnings inline.
 */
export interface ConflictEntry {
  id: string;
  title: string;
  scheduledAt: string;
  role: 'organizer' | 'invitee';
}

export interface ConflictResult {
  hasConflict: boolean;
  conflicts: readonly ConflictEntry[];
}

/**
 * Registered-user invitee (resolved via `/users/search`).
 */
export interface UserInvitee {
  kind: 'user';
  id: string;
  displayName: string;
  email: string;
}

/**
 * Email-only invitee (resolved server-side via `CreateGameNightInvitationByEmail`).
 */
export interface EmailInvitee {
  kind: 'email';
  address: string;
}

export type Invitee = UserInvitee | EmailInvitee;

/**
 * Stable de-dup key for an invitee. Used by `removeInvitee` action so the
 * reducer can target the right entry without exposing array indices.
 */
export function inviteeKey(invitee: Invitee): string {
  return invitee.kind === 'user'
    ? `user:${invitee.id}`
    : `email:${invitee.address.trim().toLowerCase()}`;
}

export interface DateBranch {
  iso: string | null;
  conflictCheckedAt: string | null;
  conflictResult: ConflictResult | null;
}

export interface LocationBranch {
  kind: LocationKind;
  details: string;
}

export interface GamesBranch {
  decideAtGroup: boolean;
  selected: readonly string[];
}

export interface DraftBranch {
  savedAt: string | null;
  status: DraftStatus;
}

export interface WizardState {
  step: WizardStep;
  date: DateBranch;
  location: LocationBranch;
  invitees: readonly Invitee[];
  games: GamesBranch;
  draft: DraftBranch;
}

/**
 * Schema version of the persisted draft payload. Bumping this number on
 * incompatible reducer-state shape changes lets the autosave restore
 * routine discard stale drafts (§9 guard).
 */
export const WIZARD_DRAFT_SCHEMA_VERSION = 1 as const;

/**
 * Persisted draft snapshot — everything except the transient `draft` branch
 * itself (avoid recursion).
 */
export type PersistedDraft = Omit<WizardState, 'draft'> & {
  schemaVersion: typeof WIZARD_DRAFT_SCHEMA_VERSION;
};

/**
 * Wizard actions. Discriminated by `type` for exhaustive switch handling.
 */
export type WizardAction =
  | { type: 'goToStep'; step: WizardStep }
  | { type: 'setDate'; iso: string }
  | { type: 'clearDate' }
  | { type: 'recordConflict'; result: ConflictResult; checkedAt: string }
  | { type: 'setLocation'; kind: LocationKind; details?: string }
  | { type: 'addInvitee'; invitee: Invitee }
  | { type: 'removeInvitee'; key: string }
  | { type: 'toggleDecideAtGroup' }
  | { type: 'toggleGame'; gameId: string }
  | { type: 'draftSaveStart' }
  | { type: 'draftSaveSuccess'; savedAt: string }
  | { type: 'draftSaveError' }
  | { type: 'restoreFromDraft'; draft: PersistedDraft };
