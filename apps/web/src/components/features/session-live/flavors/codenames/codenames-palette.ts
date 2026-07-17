import type { CodenamesKey } from './codenames-state';

/**
 * The 4 Codenames key colours — inline hsl() applied via `style` (like catan-palette).
 * The blue team hue falls near the chat/document entity-token hue by coincidence, so it
 * trips `meepleai/no-inline-hsl-v2`. It is NOT an entity token (a Codenames blue team card
 * is not a chat entity), so migrating it to getEntityToken() would be semantically wrong —
 * it carries a line-level disable with a reason instead.
 */
const KEY_HSL: Record<CodenamesKey, string> = {
  red: 'hsl(0, 65%, 52%)',
  // eslint-disable-next-line meepleai/no-inline-hsl-v2 -- Codenames blue team colour, not the chat/document entity token
  blue: 'hsl(215, 60%, 52%)',
  neutral: 'hsl(38, 30%, 72%)',
  assassin: 'hsl(0, 0%, 18%)',
};

export function codenamesKeyColor(key: CodenamesKey): string {
  return KEY_HSL[key];
}
