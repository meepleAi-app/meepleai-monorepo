import { z } from 'zod';

export const PALEO_STATE_VERSION = 1;
export const PALEO_RESOURCES = ['wood', 'stone', 'food', 'knowledge'] as const;
export const PALEO_STATUSES = ['alive', 'wounded', 'dead'] as const;

export const PaleoResourceSchema = z.enum(PALEO_RESOURCES);
export type PaleoResource = z.infer<typeof PaleoResourceSchema>;
export const PaleoStatusSchema = z.enum(PALEO_STATUSES);
export type PaleoStatus = z.infer<typeof PaleoStatusSchema>;

const nn = () => z.number().int().min(0);

export const PaleoResourcesSchema = z.object({
  wood: nn(),
  stone: nn(),
  food: nn(),
  knowledge: nn(),
});
export type PaleoResources = z.infer<typeof PaleoResourcesSchema>;

export const PaleoGameStateSchema = z.object({
  v: z.literal(PALEO_STATE_VERSION),
  game: z.literal('paleo'),
  resources: PaleoResourcesSchema,
  survivors: z.record(z.string(), PaleoStatusSchema),
});
export type PaleoGameState = z.infer<typeof PaleoGameStateSchema>;

export function parsePaleoGameState(raw: unknown): PaleoGameState | null {
  const result = PaleoGameStateSchema.safeParse(raw);
  return result.success ? result.data : null;
}

export function emptyPaleoResources(): PaleoResources {
  return { wood: 0, stone: 0, food: 0, knowledge: 0 };
}

export function initialPaleoState(playerIds: readonly string[]): PaleoGameState {
  const survivors: Record<string, PaleoStatus> = {};
  for (const id of playerIds) survivors[id] = 'alive';
  return {
    v: PALEO_STATE_VERSION,
    game: 'paleo',
    resources: emptyPaleoResources(),
    survivors,
  };
}

export function nextPaleoStatus(status: PaleoStatus): PaleoStatus {
  const i = PALEO_STATUSES.indexOf(status);
  return PALEO_STATUSES[(i + 1) % PALEO_STATUSES.length];
}
