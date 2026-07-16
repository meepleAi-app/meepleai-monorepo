/**
 * Game Nights Zod Schemas
 * Issue #33 — P3 Game Night Frontend
 */

import { z } from 'zod';

// SI-3 #2634: `InProgress` mirrors the BE `GameNightStatus` enum (a Published night is promoted
// to InProgress when its first session starts, event-driven via SessionStartedHandler). It MUST be
// listed so the live/detail read of a night mid-play parses instead of failing the whole payload.
export const GameNightStatusSchema = z.enum([
  'Draft',
  'Published',
  'InProgress',
  'Cancelled',
  'Completed',
]);
export type GameNightStatus = z.infer<typeof GameNightStatusSchema>;

export const RsvpStatusSchema = z.enum(['Pending', 'Accepted', 'Declined', 'Maybe']);
export type RsvpStatus = z.infer<typeof RsvpStatusSchema>;

export const GameNightRsvpDtoSchema = z.object({
  id: z.string().uuid(),
  userId: z.string().uuid(),
  userName: z.string(),
  status: RsvpStatusSchema,
  respondedAt: z.string().nullable(),
  createdAt: z.string(),
});
export type GameNightRsvpDto = z.infer<typeof GameNightRsvpDtoSchema>;

export const GameNightDtoSchema = z.object({
  id: z.string().uuid(),
  organizerId: z.string().uuid(),
  organizerName: z.string(),
  title: z.string(),
  description: z.string().nullable(),
  scheduledAt: z.string(),
  location: z.string().nullable(),
  maxPlayers: z.number().nullable(),
  gameIds: z.array(z.string().uuid()),
  status: GameNightStatusSchema,
  acceptedCount: z.number(),
  pendingCount: z.number(),
  totalInvited: z.number(),
  createdAt: z.string(),
  updatedAt: z.string().nullable().optional(),
  // #2978 (invariante #17): the viewer's own RSVP status, or null when not an invitee (incl. the
  // organizer). `.nullish()` tolerates legacy payloads/fixtures that omit the field; consumers
  // normalize undefined → null (see toGameNightVM).
  myRsvpStatus: RsvpStatusSchema.nullish(),
});
export type GameNightDto = z.infer<typeof GameNightDtoSchema>;

// ──────────────────────────────────────────────────────────────────────
// #2633 Slice B — night-live read model (GET /game-nights/{id}/live).
// Mirrors the shipped C# GameNightLiveDto / GameNightSessionDto (Slice A).
// LD-4: the status enum MUST list all 5 wire strings so a Skipped/Corrupted
// row never fails the whole array .parse(); an unknown 6th value fails fast.
// ──────────────────────────────────────────────────────────────────────

export const GameNightSessionStatusSchema = z.enum([
  'Pending',
  'InProgress',
  'Completed',
  'Skipped',
  'Corrupted',
]);
export type GameNightSessionStatus = z.infer<typeof GameNightSessionStatusSchema>;

export const GameNightSessionDtoSchema = z.object({
  sessionId: z.string().uuid(),
  gameId: z.string().uuid(),
  gameTitle: z.string(),
  playOrder: z.number().int(),
  status: GameNightSessionStatusSchema,
  winnerId: z.string().uuid().nullable(),
  // #2634 C4: the winner's display name, BE-resolved from winnerId (a Participant.Id), scoped by
  // session and guest-capable. null when there is no winner (or it could not be resolved).
  winnerName: z.string().nullable(),
  startedAt: z.string().nullable(),
  completedAt: z.string().nullable(),
});
export type GameNightSessionDto = z.infer<typeof GameNightSessionDtoSchema>;

export const GameNightLineupItemDtoSchema = z.object({
  gameId: z.string().uuid(),
  gameTitle: z.string(),
});
export type GameNightLineupItemDto = z.infer<typeof GameNightLineupItemDtoSchema>;

// #2634 C4: a candidate winner in the current live session — a tracking Participant.
export const GameNightRosterMemberDtoSchema = z.object({
  participantId: z.string().uuid(),
  displayName: z.string(),
});
export type GameNightRosterMemberDto = z.infer<typeof GameNightRosterMemberDtoSchema>;

// WS1 DEC-10: result of POST /game-nights/{id}/sessions (start next game).
export const StartGameNightSessionResultSchema = z.object({
  sessionId: z.string().uuid(),
  gameNightSessionId: z.string().uuid(),
  sessionCode: z.string(),
  playOrder: z.number().int(),
});
export type StartGameNightSessionResult = z.infer<typeof StartGameNightSessionResultSchema>;

export const GameNightLiveDtoSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  status: GameNightStatusSchema,
  sessions: z.array(GameNightSessionDtoSchema),
  // WS1 DEC-9: gates the organizer-only "Avvia prossimo gioco" CTA on the FE.
  isViewerOrganizer: z.boolean(),
  // WS1 DEC-9: planned games not yet started, in order — the CTA starts the first.
  plannedLineup: z.array(GameNightLineupItemDtoSchema),
  // #2634 C4: the in-progress session's participants — the winner-picker candidates. Empty when
  // no game is live.
  currentSessionRoster: z.array(GameNightRosterMemberDtoSchema),
});
export type GameNightLiveDto = z.infer<typeof GameNightLiveDtoSchema>;

// ──────────────────────────────────────────────────────────────────────
// #2633 C2 — night diary read model (GET /game-nights/{id}/diary).
// Mirrors the C# GameNightDiaryDto / GameNightDiaryEntryDto. Per panel D8:
// `eventType` is an OPEN z.string() — mapDiary derives a CLOSED DiaryEventKind
// with a total default→system, so a new/unlisted BE event type renders instead
// of being silently dropped as "malformed".
// ──────────────────────────────────────────────────────────────────────

export const GameNightDiaryEntryDtoSchema = z.object({
  id: z.string().uuid(),
  sessionId: z.string().uuid(),
  eventType: z.string(),
  description: z.string(),
  payload: z.string().nullish(),
  actorId: z.string().uuid().nullish(),
  timestamp: z.string(),
});
export type GameNightDiaryEntryDto = z.infer<typeof GameNightDiaryEntryDtoSchema>;

export const GameNightDiaryDtoSchema = z.object({
  gameNightId: z.string().uuid(),
  entries: z.array(GameNightDiaryEntryDtoSchema),
});
export type GameNightDiaryDto = z.infer<typeof GameNightDiaryDtoSchema>;

/**
 * Panel D8 / must-fix #2: per-row resilient parse. One malformed diary entry must
 * NOT blow up the whole timeline (the array-level `.parse()` throw the shipped SSE
 * hook suffered via unguarded JSON.parse). Skips + warns on bad rows, keeps the
 * valid ones; a malformed envelope degrades to an empty diary rather than throwing.
 */
export function parseGameNightDiaryResilient(raw: unknown): GameNightDiaryDto {
  const envelope = z
    .object({ gameNightId: z.string().uuid(), entries: z.array(z.unknown()) })
    .safeParse(raw);

  if (!envelope.success) {
    if (typeof console !== 'undefined') {
      console.warn('[night-diary] malformed envelope, rendering empty diary', envelope.error);
    }
    const maybeId = (raw as { gameNightId?: unknown } | null)?.gameNightId;
    return { gameNightId: typeof maybeId === 'string' ? maybeId : '', entries: [] };
  }

  const entries: GameNightDiaryEntryDto[] = [];
  for (const row of envelope.data.entries) {
    const parsed = GameNightDiaryEntryDtoSchema.safeParse(row);
    if (parsed.success) {
      entries.push(parsed.data);
    } else if (typeof console !== 'undefined') {
      console.warn('[night-diary] skipping malformed entry', parsed.error);
    }
  }
  return { gameNightId: envelope.data.gameNightId, entries };
}

export const CreateGameNightInputSchema = z.object({
  title: z.string().min(3).max(200),
  description: z.string().max(2000).optional(),
  scheduledAt: z.string(),
  location: z.string().max(500).optional(),
  maxPlayers: z.number().min(2).max(50).optional(),
  gameIds: z.array(z.string().uuid()).max(20).optional(),
  invitedUserIds: z.array(z.string().uuid()).max(49).optional(),
  // Issue #950 W1-PR1: email invitees feeding the token-based
  // GameNightInvitation flow (#607 Wave A.5a).
  invitedEmails: z.array(z.string().email().max(200)).max(49).optional(),
});
export type CreateGameNightInput = z.infer<typeof CreateGameNightInputSchema>;

// ──────────────────────────────────────────────────────────────────────
// Issue #950 W1-PR2 — wizard hooks payloads
// ──────────────────────────────────────────────────────────────────────

export const RegularDtoSchema = z.object({
  id: z.string().uuid(),
  displayName: z.string(),
  email: z.string(),
  eventCount: z.number().int().nonnegative(),
  lastInvitedAt: z.string(),
});
export type RegularDto = z.infer<typeof RegularDtoSchema>;

export const ConflictEntryDtoSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  scheduledAt: z.string(),
  role: z.enum(['organizer', 'invitee']),
});
export type ConflictEntryDto = z.infer<typeof ConflictEntryDtoSchema>;

export const ConflictCheckDtoSchema = z.object({
  hasConflict: z.boolean(),
  conflicts: z.array(ConflictEntryDtoSchema),
});
export type ConflictCheckDto = z.infer<typeof ConflictCheckDtoSchema>;

export const UpdateGameNightInputSchema = z.object({
  title: z.string().min(3).max(200),
  description: z.string().max(2000).optional(),
  scheduledAt: z.string(),
  location: z.string().max(500).optional(),
  maxPlayers: z.number().min(2).max(50).optional(),
  gameIds: z.array(z.string().uuid()).max(20).optional(),
});
export type UpdateGameNightInput = z.infer<typeof UpdateGameNightInputSchema>;

// ──────────────────────────────────────────────────────────────────────
// Issue #2700 — candidate voting (approval model)
// ──────────────────────────────────────────────────────────────────────

export const GameNightCandidateTallyDtoSchema = z.object({
  gameId: z.string().uuid(),
  voteCount: z.number().int().nonnegative(),
  votedByMe: z.boolean(),
});
export type GameNightCandidateTallyDto = z.infer<typeof GameNightCandidateTallyDtoSchema>;

export const GameNightVoteTallyDtoSchema = z.object({
  isVotingClosed: z.boolean(),
  isTie: z.boolean(),
  winnerGameId: z.string().uuid().nullable(),
  leadingCandidateGameIds: z.array(z.string().uuid()),
  candidates: z.array(GameNightCandidateTallyDtoSchema),
});
export type GameNightVoteTallyDto = z.infer<typeof GameNightVoteTallyDtoSchema>;

// ──────────────────────────────────────────────────────────────────────
// Issue #2702 — summary (MVP/KPI) + share-token + archive
// ──────────────────────────────────────────────────────────────────────

export const GameNightMvpDtoSchema = z.object({
  playerName: z.string(),
  wins: z.number().int().nonnegative(),
});
export type GameNightMvpDto = z.infer<typeof GameNightMvpDtoSchema>;

export const GameNightSummaryKpisDtoSchema = z.object({
  totalGames: z.number().int().nonnegative(),
  completedGames: z.number().int().nonnegative(),
  totalDurationMinutes: z.number().int().nonnegative(),
  winnersCount: z.number().int().nonnegative(),
});
export type GameNightSummaryKpisDto = z.infer<typeof GameNightSummaryKpisDtoSchema>;

export const GameNightGameRecapDtoSchema = z.object({
  sessionId: z.string().uuid(),
  gameId: z.string().uuid(),
  gameTitle: z.string(),
  playOrder: z.number().int(),
  status: z.string(),
  winnerId: z.string().uuid().nullable(),
  winnerName: z.string().nullable(),
  durationMinutes: z.number().int().nullable(),
  eventsCount: z.number().int().nonnegative(),
  topPlayers: z.array(z.object({ playerName: z.string(), rank: z.number().int().nullable() })),
});
export type GameNightGameRecapDto = z.infer<typeof GameNightGameRecapDtoSchema>;

export const GameNightSummaryDtoSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  status: z.string(),
  scheduledAt: z.string(),
  location: z.string().nullable(),
  isViewerOrganizer: z.boolean(),
  isArchived: z.boolean(),
  isShared: z.boolean(),
  shareToken: z.string().nullable(),
  mvp: GameNightMvpDtoSchema.nullable(),
  kpis: GameNightSummaryKpisDtoSchema,
  games: z.array(GameNightGameRecapDtoSchema),
});
export type GameNightSummaryDto = z.infer<typeof GameNightSummaryDtoSchema>;

export const GameNightShareLinkDtoSchema = z.object({ shareToken: z.string() });
export type GameNightShareLinkDto = z.infer<typeof GameNightShareLinkDtoSchema>;

// Issue #2724 — recap photo gallery
export const GameNightPhotoDtoSchema = z.object({
  id: z.string().uuid(),
  photoUrl: z.string(),
  thumbnailUrl: z.string().nullable(),
  caption: z.string().nullable(),
  uploadedByUserId: z.string().uuid(),
  uploadedAt: z.string(),
});
export type GameNightPhotoDto = z.infer<typeof GameNightPhotoDtoSchema>;

export const GameNightPhotoUploadResultSchema = z.object({
  photoId: z.string().uuid(),
  photoUrl: z.string(),
  thumbnailUrl: z.string().nullable(),
  ocrText: z.string().nullable(),
  wasDeduplicated: z.boolean(),
});
export type GameNightPhotoUploadResult = z.infer<typeof GameNightPhotoUploadResultSchema>;
