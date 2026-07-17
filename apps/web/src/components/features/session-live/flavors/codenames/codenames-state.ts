import { z } from 'zod';

export const CODENAMES_STATE_VERSION = 1;
export const CODENAMES_BOARD_SIZE = 25;
export const CODENAMES_KEY_COUNTS = { starting: 9, other: 8, neutral: 7, assassin: 1 } as const;

export const CodenamesTeamSchema = z.enum(['red', 'blue']);
export type CodenamesTeam = z.infer<typeof CodenamesTeamSchema>;

export const CodenamesKeySchema = z.enum(['red', 'blue', 'neutral', 'assassin']);
export type CodenamesKey = z.infer<typeof CodenamesKeySchema>;

export const CodenamesCellSchema = z.object({
  word: z.string(),
  key: CodenamesKeySchema,
  revealed: z.boolean(),
});
export type CodenamesCell = z.infer<typeof CodenamesCellSchema>;

export const CodenamesClueSchema = z.object({ word: z.string(), number: z.number().int().min(0) });
export type CodenamesClue = z.infer<typeof CodenamesClueSchema>;

export const CodenamesGameStateSchema = z.object({
  v: z.literal(CODENAMES_STATE_VERSION),
  game: z.literal('codenames'),
  board: z.array(CodenamesCellSchema).length(CODENAMES_BOARD_SIZE),
  currentTeam: CodenamesTeamSchema,
  clue: CodenamesClueSchema.nullable(),
});
export type CodenamesGameState = z.infer<typeof CodenamesGameStateSchema>;

export function parseCodenamesGameState(raw: unknown): CodenamesGameState | null {
  const result = CodenamesGameStateSchema.safeParse(raw);
  return result.success ? result.data : null;
}

export function oppositeTeam(team: CodenamesTeam): CodenamesTeam {
  return team === 'red' ? 'blue' : 'red';
}

export function isAssassinRevealed(board: CodenamesCell[]): boolean {
  return board.some(c => c.key === 'assassin' && c.revealed);
}

export function teamCounts(
  board: CodenamesCell[],
  team: CodenamesTeam
): { total: number; found: number } {
  let total = 0;
  let found = 0;
  for (const c of board) {
    if (c.key !== team) continue;
    total++;
    if (c.revealed) found++;
  }
  return { total, found };
}

/** assassin revealed → the on-turn team loses (other wins); all of a team revealed → that team; else null. */
export function codenamesWinner(s: CodenamesGameState): CodenamesTeam | null {
  if (isAssassinRevealed(s.board)) return oppositeTeam(s.currentTeam);
  for (const team of ['red', 'blue'] as const) {
    const { total, found } = teamCounts(s.board, team);
    if (total > 0 && found === total) return team;
  }
  return null;
}
