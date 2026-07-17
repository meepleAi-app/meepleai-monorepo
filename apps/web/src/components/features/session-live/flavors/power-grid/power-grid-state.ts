import { z } from 'zod';

export const POWER_GRID_STATE_VERSION = 1;
export const POWER_GRID_RESOURCES = ['coal', 'oil', 'garbage', 'uranium'] as const;
export const POWER_GRID_PLANT_BANKS = ['current', 'future'] as const;

export const PowerGridResourceSchema = z.enum(POWER_GRID_RESOURCES);
export type PowerGridResource = z.infer<typeof PowerGridResourceSchema>;
export type PowerGridPlantBank = (typeof POWER_GRID_PLANT_BANKS)[number];

const nn = () => z.number().int().min(0);
const bank = () => z.array(z.number().int().min(0).nullable()).length(4);

export const PowerGridResourcesSchema = z.object({
  coal: nn(),
  oil: nn(),
  garbage: nn(),
  uranium: nn(),
});
export type PowerGridResources = z.infer<typeof PowerGridResourcesSchema>;

export const PowerGridGameStateSchema = z.object({
  v: z.literal(POWER_GRID_STATE_VERSION),
  game: z.literal('power-grid'),
  plants: z.object({ current: bank(), future: bank() }),
  resources: PowerGridResourcesSchema,
});
export type PowerGridGameState = z.infer<typeof PowerGridGameStateSchema>;

export function parsePowerGridGameState(raw: unknown): PowerGridGameState | null {
  const result = PowerGridGameStateSchema.safeParse(raw);
  return result.success ? result.data : null;
}

export function emptyPowerGridResources(): PowerGridResources {
  return { coal: 0, oil: 0, garbage: 0, uranium: 0 };
}

export function initialPowerGridState(): PowerGridGameState {
  return {
    v: POWER_GRID_STATE_VERSION,
    game: 'power-grid',
    plants: { current: [null, null, null, null], future: [null, null, null, null] },
    resources: emptyPowerGridResources(),
  };
}
