/**
 * Zod schemas + step-completion predicates for the SP7 game-night-create wizard.
 *
 * Issue #950 W2 Foundation. Spec: docs/superpowers/specs/2026-05-18-sp7-game-night-create.md §11.
 *
 * Each step's predicate answers "can the user advance from here?" — the same
 * gate that toggles the "Avanti" / "Crea evento" button in the wizard UI.
 * Submit-time validation runs the full schema (`submitPayloadSchema`) so the
 * BE contract is the single source of truth for the network call.
 */

import { z } from 'zod';

import { WIZARD_DRAFT_SCHEMA_VERSION, type LocationKind, type WizardState } from './wizard-types';

// ────────────────────────────────────────────────────────────────────────────
// Constants — kept in sync with the BE CreateGameNightCommandValidator.
// See apps/api/src/Api/BoundedContexts/GameManagement/Application/Validators/
//   GameNights/CreateGameNightCommandValidator.cs:13-15
// ────────────────────────────────────────────────────────────────────────────

export const MAX_COMBINED_INVITEES = 49;
export const MAX_EMAIL_LENGTH = 200;
export const MAX_TITLE_LENGTH = 200;
export const MAX_DESCRIPTION_LENGTH = 2000;
export const MAX_LOCATION_DETAILS_LENGTH = 500;
export const MAX_GAMES = 20;
export const SCHEDULED_AT_MIN_HOURS_AHEAD = 1;

// Pragmatic RFC 5321 gate. Identical pattern to the BE
// `EmailFormatRegex` so client and server reject the same set of inputs.
const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

// ────────────────────────────────────────────────────────────────────────────
// Step-level Zod schemas
// ────────────────────────────────────────────────────────────────────────────

/**
 * Step 1 (Quando): date is set and at least 1 hour in the future.
 * Conflict detection is non-blocking (Scenario 2 lets the user advance with
 * "Continua comunque"), so we only validate the timestamp itself here.
 */
export const step1DateSchema = z
  .object({
    iso: z
      .string()
      .min(1, 'Seleziona una data')
      .refine(iso => !Number.isNaN(Date.parse(iso)), 'Data non valida')
      .refine(iso => {
        const t = Date.parse(iso);
        return t > Date.now() + SCHEDULED_AT_MIN_HOURS_AHEAD * 60 * 60 * 1000;
      }, `La data deve essere almeno ${SCHEDULED_AT_MIN_HOURS_AHEAD} ora nel futuro`),
  })
  .strip();

const LOCATION_KINDS = [
  'home',
  'friend',
  'online',
  'tbd',
] as const satisfies readonly LocationKind[];

/**
 * Step 2 (Dove): a location kind is always selected (initial state = `home`),
 * so this step is implicitly complete. We still validate `details` length to
 * fail loudly if the user types a 600-char address.
 */
export const step2LocationSchema = z
  .object({
    kind: z.enum(LOCATION_KINDS),
    details: z
      .string()
      .max(MAX_LOCATION_DETAILS_LENGTH, `Massimo ${MAX_LOCATION_DETAILS_LENGTH} caratteri`),
  })
  .strip();

const userInviteeSchema = z.object({
  kind: z.literal('user'),
  id: z.string().uuid('ID utente non valido'),
  displayName: z.string(),
  email: z.string(),
});

const emailInviteeSchema = z.object({
  kind: z.literal('email'),
  address: z
    .string()
    .max(MAX_EMAIL_LENGTH, `Email troppo lunga (max ${MAX_EMAIL_LENGTH} caratteri)`)
    .regex(EMAIL_REGEX, 'Formato email non valido'),
});

const inviteeSchema = z.discriminatedUnion('kind', [userInviteeSchema, emailInviteeSchema]);

/**
 * Step 3 (Chi): zero invitees is allowed (Marco may want a solo session, or
 * the FE may submit then add invitees later). The combined cap mirrors the
 * BE validator so the wizard never sends a request that will be rejected.
 */
export const step3InviteesSchema = z
  .array(inviteeSchema)
  .max(MAX_COMBINED_INVITEES, `Massimo ${MAX_COMBINED_INVITEES} inviti totali`)
  .refine(
    invitees => {
      const seen = new Set<string>();
      for (const inv of invitees) {
        const key =
          inv.kind === 'user' ? `user:${inv.id}` : `email:${inv.address.trim().toLowerCase()}`;
        if (seen.has(key)) return false;
        seen.add(key);
      }
      return true;
    },
    { message: 'Inviti duplicati non sono permessi' }
  );

/**
 * Step 4 (Cosa): EITHER `decideAtGroup` is on OR at least one game is picked.
 */
export const step4GamesSchema = z
  .object({
    decideAtGroup: z.boolean(),
    selected: z
      .array(z.string().uuid('Game ID non valido'))
      .max(MAX_GAMES, `Massimo ${MAX_GAMES} giochi`),
  })
  .refine(games => games.decideAtGroup || games.selected.length > 0, {
    message: 'Seleziona almeno un gioco o attiva "lascia decidere al gruppo"',
  });

// ────────────────────────────────────────────────────────────────────────────
// Submit-time schema — matches the BE CreateGameNightCommand shape.
// ────────────────────────────────────────────────────────────────────────────

export const submitPayloadSchema = z
  .object({
    title: z
      .string()
      .min(3, 'Il titolo deve contenere almeno 3 caratteri')
      .max(MAX_TITLE_LENGTH, `Titolo troppo lungo (max ${MAX_TITLE_LENGTH})`),
    scheduledAt: step1DateSchema.shape.iso,
    description: z.string().max(MAX_DESCRIPTION_LENGTH).nullable().optional(),
    location: z.string().max(MAX_LOCATION_DETAILS_LENGTH).nullable().optional(),
    maxPlayers: z.number().int().min(2).max(50).nullable().optional(),
    gameIds: z.array(z.string().uuid()).max(MAX_GAMES).nullable().optional(),
    invitedUserIds: z.array(z.string().uuid()).nullable().optional(),
    invitedEmails: z
      .array(z.string().regex(EMAIL_REGEX).max(MAX_EMAIL_LENGTH))
      .nullable()
      .optional(),
  })
  .refine(
    payload => {
      const userCount = payload.invitedUserIds?.length ?? 0;
      const emailCount = payload.invitedEmails?.length ?? 0;
      return userCount + emailCount <= MAX_COMBINED_INVITEES;
    },
    {
      message: `Massimo ${MAX_COMBINED_INVITEES} inviti combinati (utenti + email)`,
      path: ['invitedEmails'],
    }
  );

export type SubmitPayload = z.infer<typeof submitPayloadSchema>;

// ────────────────────────────────────────────────────────────────────────────
// Persisted draft schema — guards against stale localStorage payloads when
// `WIZARD_DRAFT_SCHEMA_VERSION` bumps.
// ────────────────────────────────────────────────────────────────────────────

export const persistedDraftSchema = z.object({
  schemaVersion: z.literal(WIZARD_DRAFT_SCHEMA_VERSION),
  step: z.union([z.literal(1), z.literal(2), z.literal(3), z.literal(4)]),
  date: z.object({
    iso: z.string().nullable(),
    conflictCheckedAt: z.string().nullable(),
    conflictResult: z
      .object({
        hasConflict: z.boolean(),
        conflicts: z.array(
          z.object({
            id: z.string(),
            title: z.string(),
            scheduledAt: z.string(),
            role: z.enum(['organizer', 'invitee']),
          })
        ),
      })
      .nullable(),
  }),
  location: step2LocationSchema,
  invitees: z.array(inviteeSchema),
  games: z.object({
    decideAtGroup: z.boolean(),
    selected: z.array(z.string()),
  }),
});

// ────────────────────────────────────────────────────────────────────────────
// Step-completion predicates (the "can advance from here" gate)
// ────────────────────────────────────────────────────────────────────────────

/**
 * Returns true iff the user can advance from `step` given the current state.
 * Used by the wizard's "Avanti" button to enable/disable transitions.
 */
export function isStepComplete(state: WizardState, step: 1 | 2 | 3 | 4): boolean {
  switch (step) {
    case 1:
      return step1DateSchema.safeParse({ iso: state.date.iso ?? '' }).success;
    case 2:
      // Location kind is always set (initial = 'home'); 'tbd' is also valid.
      return step2LocationSchema.safeParse(state.location).success;
    case 3:
      // Invitees ARE optional — Marco can ship a placeholder event. The schema
      // only blocks invalid emails and dedup-violations.
      return step3InviteesSchema.safeParse(state.invitees).success;
    case 4:
      return step4GamesSchema.safeParse(state.games).success;
    default: {
      const _exhaustive: never = step;
      void _exhaustive;
      return false;
    }
  }
}

/**
 * Returns true iff every step is complete — gates the "Crea evento" submit
 * button on the final step.
 */
export function isWizardComplete(state: WizardState): boolean {
  return (
    isStepComplete(state, 1) &&
    isStepComplete(state, 2) &&
    isStepComplete(state, 3) &&
    isStepComplete(state, 4)
  );
}

/**
 * Builds the BE submit payload from wizard state. Title is provided by the
 * caller because it lives outside the reducer (controlled by a sibling
 * input). Returns the validated payload on success or a Zod issue list on
 * failure for FE error mapping.
 */
export function buildSubmitPayload(
  state: WizardState,
  args: { title: string; description?: string | null; maxPlayers?: number | null }
): { ok: true; payload: SubmitPayload } | { ok: false; issues: z.ZodIssue[] } {
  const invitedUserIds = state.invitees
    .filter((i): i is Extract<typeof i, { kind: 'user' }> => i.kind === 'user')
    .map(i => i.id);
  const invitedEmails = state.invitees
    .filter((i): i is Extract<typeof i, { kind: 'email' }> => i.kind === 'email')
    .map(i => i.address.trim().toLowerCase());

  const candidate = {
    title: args.title.trim(),
    scheduledAt: state.date.iso ?? '',
    description: args.description ?? null,
    location: state.location.kind === 'tbd' ? null : state.location.details || null,
    maxPlayers: args.maxPlayers ?? null,
    gameIds: state.games.decideAtGroup ? [] : [...state.games.selected],
    invitedUserIds: invitedUserIds.length > 0 ? invitedUserIds : null,
    invitedEmails: invitedEmails.length > 0 ? invitedEmails : null,
  };

  const result = submitPayloadSchema.safeParse(candidate);
  return result.success
    ? { ok: true, payload: result.data }
    : { ok: false, issues: result.error.issues };
}
