import { z } from 'zod';

export const CATAN_STATE_VERSION = 1;

export const CATAN_PIECE_TOTALS = { settlements: 5, cities: 4, roads: 15 } as const;
export type CatanPiece = keyof typeof CATAN_PIECE_TOTALS;

export const CatanTerrainSchema = z.enum(['wood', 'brick', 'sheep', 'wheat', 'ore', 'desert']);
export type CatanTerrain = z.infer<typeof CatanTerrainSchema>;

export const CatanHexSchema = z.object({
  id: z.string(),
  col: z.number().int(),
  row: z.number().int(),
  terrain: CatanTerrainSchema,
  number: z.number().int().nullable(),
});
export type CatanHex = z.infer<typeof CatanHexSchema>;

export const CatanPortSchema = z.object({
  hexId: z.string(),
  edge: z.number().int(),
  type: z.union([z.literal('generic'), CatanTerrainSchema]),
  ratio: z.enum(['3:1', '2:1']),
});
export type CatanPort = z.infer<typeof CatanPortSchema>;

export const CatanPlayerStateSchema = z.object({
  handSize: z.number().int(),
  built: z.object({
    settlements: z.number().int(),
    cities: z.number().int(),
    roads: z.number().int(),
  }),
  devCount: z.number().int(),
  badges: z.object({ longestRoad: z.boolean(), largestArmy: z.boolean() }),
});
export type CatanPlayerState = z.infer<typeof CatanPlayerStateSchema>;

export const CatanGameStateSchema = z.object({
  v: z.literal(CATAN_STATE_VERSION),
  game: z.literal('catan'),
  board: z.object({
    hexes: z.array(CatanHexSchema),
    robberHexId: z.string(),
    ports: z.array(CatanPortSchema).optional(),
  }),
  dice: z.object({ last: z.number().int().nullable(), history: z.array(z.number().int()) }),
  players: z.record(z.string(), CatanPlayerStateSchema),
});
export type CatanGameState = z.infer<typeof CatanGameStateSchema>;

/** Safe-parse the opaque L1 gameState. Returns null (never throws) on wrong game/version/shape. */
export function parseCatanGameState(raw: unknown): CatanGameState | null {
  const result = CatanGameStateSchema.safeParse(raw);
  return result.success ? result.data : null;
}

export function emptyCatanPlayerState(): CatanPlayerState {
  return {
    handSize: 0,
    built: { settlements: 0, cities: 0, roads: 0 },
    devCount: 0,
    badges: { longestRoad: false, largestArmy: false },
  };
}
