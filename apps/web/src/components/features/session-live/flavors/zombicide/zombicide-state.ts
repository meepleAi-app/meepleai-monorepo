import { z } from 'zod';

export const ZOMBICIDE_STATE_VERSION = 1;
export const ZOMBIE_TYPES = [
  'walker',
  'runner',
  'fatty',
  'berserker',
  'abomination',
  'necromancer',
] as const;
export const ZOMBICIDE_WOUND_LEVELS = [0, 1, 2] as const;

export const ZombieTypeSchema = z.enum(ZOMBIE_TYPES);
export type ZombieType = z.infer<typeof ZombieTypeSchema>;
export const WoundLevelSchema = z.union([z.literal(0), z.literal(1), z.literal(2)]);
export type WoundLevel = z.infer<typeof WoundLevelSchema>;

const nn = () => z.number().int().min(0);

export const ZombieCountsSchema = z.object({
  walker: nn(),
  runner: nn(),
  fatty: nn(),
  berserker: nn(),
  abomination: nn(),
  necromancer: nn(),
});
export type ZombieCounts = z.infer<typeof ZombieCountsSchema>;

export const ZombicideGameStateSchema = z.object({
  v: z.literal(ZOMBICIDE_STATE_VERSION),
  game: z.literal('zombicide'),
  zombies: ZombieCountsSchema,
  survivors: z.record(z.string(), WoundLevelSchema),
});
export type ZombicideGameState = z.infer<typeof ZombicideGameStateSchema>;

export function parseZombicideGameState(raw: unknown): ZombicideGameState | null {
  const result = ZombicideGameStateSchema.safeParse(raw);
  return result.success ? result.data : null;
}

export function emptyZombieCounts(): ZombieCounts {
  return { walker: 0, runner: 0, fatty: 0, berserker: 0, abomination: 0, necromancer: 0 };
}

export function initialZombicideState(playerIds: readonly string[]): ZombicideGameState {
  const survivors: Record<string, WoundLevel> = {};
  for (const id of playerIds) survivors[id] = 0;
  return {
    v: ZOMBICIDE_STATE_VERSION,
    game: 'zombicide',
    zombies: emptyZombieCounts(),
    survivors,
  };
}

export function nextWoundLevel(level: WoundLevel): WoundLevel {
  return level === 0 ? 1 : level === 1 ? 2 : 0;
}
