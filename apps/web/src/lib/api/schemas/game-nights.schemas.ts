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
  eventId: z.string().uuid(),
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
  createdAt: z.string(),
});
export type GameNightDto = z.infer<typeof GameNightDtoSchema>;

export const CreateGameNightInputSchema = z.object({
  title: z.string().min(3).max(200),
  description: z.string().max(2000).optional(),
  scheduledAt: z.string(),
  location: z.string().max(500).optional(),
  maxPlayers: z.number().min(2).max(50).optional(),
  gameIds: z.array(z.string().uuid()).max(20).optional(),
  invitedUserIds: z.array(z.string().uuid()).max(49).optional(),
});
export type CreateGameNightInput = z.infer<typeof CreateGameNightInputSchema>;

export const UpdateGameNightInputSchema = z.object({
  title: z.string().min(3).max(200),
  description: z.string().max(2000).optional(),
  scheduledAt: z.string(),
  location: z.string().max(500).optional(),
  maxPlayers: z.number().min(2).max(50).optional(),
  gameIds: z.array(z.string().uuid()).max(20).optional(),
});
export type UpdateGameNightInput = z.infer<typeof UpdateGameNightInputSchema>;
