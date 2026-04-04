import { z } from 'zod';

export const SeedingGameDtoSchema = z.object({
  id: z.string().uuid(),
  bggId: z.number().int().nullable(),
  title: z.string(),
  gameDataStatus: z.number().int(),
  gameDataStatusName: z.string(),
  gameStatus: z.number().int(),
  gameStatusName: z.string(),
  hasUploadedPdf: z.boolean(),
  isRagReady: z.boolean(),
  errorMessage: z.string().nullable(),
  createdAt: z.string(),
});

export type SeedingGameDto = z.infer<typeof SeedingGameDtoSchema>;

export const SeedingGameListSchema = z.array(SeedingGameDtoSchema);
export type SeedingGameList = z.infer<typeof SeedingGameListSchema>;

export const BggQueueItemSchema = z.object({
  id: z.string().uuid(),
  bggId: z.number().int(),
  gameName: z.string().nullable(),
  status: z.number().int(),
  position: z.number().int(),
  retryCount: z.number().int(),
  createdAt: z.string(),
  errorMessage: z.string().nullable().optional(),
});

export const BggQueueStatusSchema = z.object({
  totalQueued: z.number().int(),
  totalProcessing: z.number().int(),
  items: z.array(BggQueueItemSchema),
});

export type BggQueueStatus = z.infer<typeof BggQueueStatusSchema>;
