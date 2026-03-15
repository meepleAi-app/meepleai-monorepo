/**
 * Shared entity color and emoji constants for hand drawer components.
 * Colors match the MeepleCard entity color system (HSL without wrapper).
 */

export const ENTITY_COLORS: Record<string, string> = {
  game: '25 95% 45%',
  player: '262 83% 58%',
  session: '240 60% 55%',
  agent: '38 92% 50%',
  kb: '174 60% 40%',
  chatSession: '220 80% 55%',
  event: '350 89% 60%',
  toolkit: '142 70% 45%',
  tool: '195 80% 50%',
  custom: '220 70% 50%',
};

export const ENTITY_EMOJIS: Record<string, string> = {
  game: '\uD83C\uDFB2',
  player: '\uD83D\uDC64',
  session: '\uD83C\uDFAE',
  agent: '\uD83E\uDD16',
  kb: '\uD83D\uDCC4',
  chatSession: '\uD83D\uDCAC',
  event: '\uD83D\uDCC5',
  toolkit: '\uD83D\uDD27',
  tool: '\uD83D\uDD28',
  custom: '\uD83D\uDCCC',
};
