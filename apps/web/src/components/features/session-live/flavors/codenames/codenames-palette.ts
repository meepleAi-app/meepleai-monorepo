import type { CodenamesKey } from './codenames-state';

// The 4 Codenames key colours — inline hsl() (token-lint safe escape, like catan-palette).
const KEY_HSL: Record<CodenamesKey, string> = {
  red: 'hsl(0, 65%, 52%)',
  blue: 'hsl(215, 60%, 52%)',
  neutral: 'hsl(38, 30%, 72%)',
  assassin: 'hsl(0, 0%, 18%)',
};

export function codenamesKeyColor(key: CodenamesKey): string {
  return KEY_HSL[key];
}
