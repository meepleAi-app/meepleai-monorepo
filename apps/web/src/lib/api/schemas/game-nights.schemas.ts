/**
 * Game Nights Zod Schemas
 * Issue #33 — P3 Game Night Frontend
 */

import { z } from 'zod';

export const GameNightStatusSchema = z.enum(['Draft', 'Published', 'Cancelled', 'Completed']);
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
  startedAt: z.string().nullable(),
  completedAt: z.string().nullable(),
});
export type GameNightSessionDto = z.infer<typeof GameNightSessionDtoSchema>;

export const GameNightLineupItemDtoSchema = z.object({
  gameId: z.string().uuid(),
  gameTitle: z.string(),
});
export type GameNightLineupItemDto = z.infer<typeof GameNightLineupItemDtoSchema>;

export const GameNightLiveDtoSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  status: GameNightStatusSchema,
  sessions: z.array(GameNightSessionDtoSchema),
  // WS1 DEC-9: gates the organizer-only "Avvia prossimo gioco" CTA on the FE.
  isViewerOrganizer: z.boolean(),
  // WS1 DEC-9: planned games not yet started, in order — the CTA starts the first.
  plannedLineup: z.array(GameNightLineupItemDtoSchema),
});
export type GameNightLiveDto = z.infer<typeof GameNightLiveDtoSchema>;

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
