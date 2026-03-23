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
  createdAt: z.string(),
});

export type SeedingGameDto = z.infer<typeof SeedingGameDtoSchema>;

export const SeedingGameListSchema = z.array(SeedingGameDtoSchema);
export type SeedingGameList = z.infer<typeof SeedingGameListSchema>;
