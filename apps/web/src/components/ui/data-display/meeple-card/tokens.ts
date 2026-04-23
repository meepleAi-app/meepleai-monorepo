import type { MeepleEntityType } from './types';

export const entityColors: Record<MeepleEntityType, { h: number; s: string; l: string }> = {
  game: { h: 25, s: '95%', l: '45%' },
  player: { h: 262, s: '83%', l: '58%' },
  session: { h: 240, s: '60%', l: '55%' },
  agent: { h: 38, s: '92%', l: '50%' },
  kb: { h: 210, s: '40%', l: '55%' },
  chat: { h: 220, s: '80%', l: '55%' },
  event: { h: 350, s: '89%', l: '60%' },
  toolkit: { h: 142, s: '70%', l: '45%' },
  tool: { h: 195, s: '80%', l: '50%' },
};

export function entityHsl(entity: MeepleEntityType, alpha?: number): string {
  const c = entityColors[entity];
  if (alpha !== undefined) {
    return `hsla(${c.h}, ${c.s}, ${c.l}, ${alpha})`;
  }
  return `hsl(${c.h}, ${c.s}, ${c.l})`;
}

export const entityLabel: Record<MeepleEntityType, string> = {
  game: 'Game',
  player: 'Player',
  session: 'Session',
  agent: 'Agent',
  kb: 'KB',
  chat: 'Chat',
  event: 'Event',
  toolkit: 'Toolkit',
  tool: 'Tool',
};

export const entityIcon: Record<MeepleEntityType, string> = {
  game: '🎲',
  player: '👤',
  session: '🎯',
  agent: '🤖',
  kb: '📚',
  chat: '💬',
  event: '📅',
  toolkit: '🧰',
  tool: '🔧',
};

/**
 * Named derived colors for an entity.
 *
 * Replaces inline `entityHsl(entity, 0.12)` calls with semantic names.
 * Use these when styling ConnectionChip, badges, popovers, etc.
 */
export interface EntityTokens {
  solid: string; // full color — icons, badge bg
  fill: string; // 0.12 — chip bg default
  border: string; // 0.35 — chip border default
  hover: string; // 0.22 — chip bg on hover
  glow: string; // 0.18 — box-shadow spread
  shadow: string; // 0.25 — box-shadow drop
  muted: string; // 0.06 — empty/disabled bg
  dashed: string; // 0.25 — dashed border empty state
  textOn: string; // text color on solid bg
}

export function entityTokens(entity: MeepleEntityType): EntityTokens {
  return {
    solid: entityHsl(entity),
    fill: entityHsl(entity, 0.12),
    border: entityHsl(entity, 0.35),
    hover: entityHsl(entity, 0.22),
    glow: entityHsl(entity, 0.18),
    shadow: entityHsl(entity, 0.25),
    muted: entityHsl(entity, 0.06),
    dashed: entityHsl(entity, 0.25),
    textOn: '#ffffff',
  };
}

export const statusColors: Record<string, { bg: string; text: string }> = {
  owned: { bg: '#dcfce7', text: '#166534' },
  wishlist: { bg: '#fef3c7', text: '#92400e' },
  active: { bg: '#dcfce7', text: '#166534' },
  idle: { bg: '#f1f5f9', text: '#64748b' },
  archived: { bg: '#f1f5f9', text: '#64748b' },
  processing: { bg: '#dbeafe', text: '#1e40af' },
  indexed: { bg: '#dcfce7', text: '#166534' },
  failed: { bg: '#fee2e2', text: '#991b1b' },
  inprogress: { bg: '#dbeafe', text: '#1e40af' },
  setup: { bg: '#fef3c7', text: '#92400e' },
  completed: { bg: '#f3e8ff', text: '#6b21a8' },
  paused: { bg: '#fef3c7', text: '#92400e' },
};
