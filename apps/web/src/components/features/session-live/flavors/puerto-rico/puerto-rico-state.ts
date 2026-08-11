import { z } from 'zod';

export const PUERTO_RICO_STATE_VERSION = 1;
export const PUERTO_RICO_GOODS = ['corn', 'indigo', 'sugar', 'tobacco', 'coffee'] as const;

export const PuertoRicoGoodSchema = z.enum(PUERTO_RICO_GOODS);
export type PuertoRicoGood = z.infer<typeof PuertoRicoGoodSchema>;

const nn = () => z.number().int().min(0);

export const PuertoRicoStorehouseSchema = z.object({
  corn: nn(),
  indigo: nn(),
  sugar: nn(),
  tobacco: nn(),
  coffee: nn(),
});

export const PuertoRicoPlayerStateSchema = z.object({
  doubloons: nn(),
  colonists: nn(),
  storehouse: PuertoRicoStorehouseSchema,
  plantations: nn(),
  quarries: nn(),
  buildings: nn(),
});
export type PuertoRicoPlayerState = z.infer<typeof PuertoRicoPlayerStateSchema>;

export const PuertoRicoGalleonSchema = z.object({
  good: PuertoRicoGoodSchema.nullable(),
  loaded: nn(),
  cap: nn(),
});
export type PuertoRicoGalleon = z.infer<typeof PuertoRicoGalleonSchema>;

export const PuertoRicoGameStateSchema = z.object({
  v: z.literal(PUERTO_RICO_STATE_VERSION),
  game: z.literal('puerto-rico'),
  players: z.record(z.string(), PuertoRicoPlayerStateSchema),
  galleons: z.array(PuertoRicoGalleonSchema),
  tradingHouse: z.object({ slots: z.array(PuertoRicoGoodSchema.nullable()).length(4) }),
  colonistShip: z.object({ onShip: nn(), supply: nn() }),
});
export type PuertoRicoGameState = z.infer<typeof PuertoRicoGameStateSchema>;

export function parsePuertoRicoGameState(raw: unknown): PuertoRicoGameState | null {
  const result = PuertoRicoGameStateSchema.safeParse(raw);
  return result.success ? result.data : null;
}

export function emptyPuertoRicoPlayerState(): PuertoRicoPlayerState {
  return {
    doubloons: 0,
    colonists: 0,
    storehouse: { corn: 0, indigo: 0, sugar: 0, tobacco: 0, coffee: 0 },
    plantations: 0,
    quarries: 0,
    buildings: 0,
  };
}

export function initialPuertoRicoState(playerIds: readonly string[]): PuertoRicoGameState {
  const n = playerIds.length;
  const players: Record<string, PuertoRicoPlayerState> = {};
  for (const id of playerIds) players[id] = emptyPuertoRicoPlayerState();
  return {
    v: PUERTO_RICO_STATE_VERSION,
    game: 'puerto-rico',
    players,
    galleons: [
      { good: null, loaded: 0, cap: n + 1 },
      { good: null, loaded: 0, cap: n + 2 },
      { good: null, loaded: 0, cap: n + 3 },
    ],
    tradingHouse: { slots: [null, null, null, null] },
    colonistShip: { onShip: 0, supply: 0 },
  };
}
