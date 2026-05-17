export type EntityType =
  | 'game'
  | 'player'
  | 'session'
  | 'agent'
  | 'kb'
  | 'chat'
  | 'event'
  | 'toolkit'
  | 'tool';

export interface EntityToken {
  readonly bg: string;
  readonly bgSoft: string;
  readonly text: string;
  readonly border: string;
  readonly emoji: string;
  readonly label: string;
}

// Map "kb" -> tailwind class "document" (pre-existing naming)
const TAILWIND_KEY: Record<EntityType, string> = {
  game: 'game',
  player: 'player',
  session: 'session',
  agent: 'agent',
  kb: 'document',
  chat: 'chat',
  event: 'event',
  toolkit: 'toolkit',
  tool: 'tool',
};

const EMOJI: Record<EntityType, string> = {
  game: '🎲',
  player: '👤',
  session: '🎯',
  agent: '🤖',
  kb: '📚',
  chat: '💬',
  event: '🗓️',
  toolkit: '🧰',
  tool: '🔧',
};

const LABEL: Record<EntityType, string> = {
  game: 'Gioco',
  player: 'Giocatore',
  session: 'Sessione',
  agent: 'Agente',
  kb: 'Base di conoscenza',
  chat: 'Chat',
  event: 'Evento',
  toolkit: 'Toolkit',
  tool: 'Tool',
};

export function getEntityToken(type: EntityType): EntityToken {
  const k = TAILWIND_KEY[type];
  return {
    bg: `bg-entity-${k}`,
    bgSoft: `bg-entity-${k}/10`,
    text: `text-entity-${k}`,
    border: `border-entity-${k}`,
    emoji: EMOJI[type],
    label: LABEL[type],
  };
}

export const ENTITY_TOKENS: readonly EntityType[] = [
  'game',
  'player',
  'session',
  'agent',
  'kb',
  'chat',
  'event',
  'toolkit',
  'tool',
] as const;
