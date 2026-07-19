import { z } from 'zod';

export const WINGSPAN_STATE_VERSION = 1;

/** Turns available per round (base game), rounds 1..4. Derived constant, never stored. */
export const WINGSPAN_ROUND_TURN_BUDGET = [8, 7, 6, 5] as const;

/**
 * The 6 canonical Wingspan VP categories. `id` is the scoring dimension name the flavor
 * sums over `roundScores`; `emoji` is language-agnostic display. Labels come from i18n.
 */
export const WINGSPAN_CATEGORIES: ReadonlyArray<{ id: string; emoji: string }> = [
  { id: 'birds', emoji: '🐦' },
  { id: 'bonusCards', emoji: '🎴' },
  { id: 'endOfRoundGoals', emoji: '🎯' },
  { id: 'eggs', emoji: '🥚' },
  { id: 'cachedFood', emoji: '🌰' },
  { id: 'tuckedCards', emoji: '🍃' },
];

export const WingspanRoundGoalSchema = z.object({ label: z.string() });
export type WingspanRoundGoal = z.infer<typeof WingspanRoundGoalSchema>;

export const WingspanGameStateSchema = z.object({
  v: z.literal(WINGSPAN_STATE_VERSION),
  game: z.literal('wingspan'),
  round: z.number().int().min(1).max(4),
  roundGoals: z.array(WingspanRoundGoalSchema).max(4),
});
export type WingspanGameState = z.infer<typeof WingspanGameStateSchema>;

/** Safe-parse the opaque L1 gameState. Returns null (never throws) on wrong game/version/shape. */
export function parseWingspanGameState(raw: unknown): WingspanGameState | null {
  const result = WingspanGameStateSchema.safeParse(raw);
  return result.success ? result.data : null;
}

export function initialWingspanState(): WingspanGameState {
  return { v: WINGSPAN_STATE_VERSION, game: 'wingspan', round: 1, roundGoals: [] };
}
