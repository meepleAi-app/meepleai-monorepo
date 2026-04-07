import {
  GameIcon,
  SessionIcon,
  PlayerIcon,
  EventIcon,
  AgentIcon,
  KnowledgeIcon,
  ChatIcon,
  ToolkitIcon,
  ToolIcon,
} from '@/components/icons/entities';

import type { MeepleEntityType } from '../meeple-card';
import type { ManaDisplayConfig, EntityRelationshipMap } from './mana-types';

export const MANA_DISPLAY: Record<MeepleEntityType, ManaDisplayConfig> = {
  game: { key: 'game', displayName: 'Game', symbol: '🎲', tier: 'core', Icon: GameIcon },
  session: {
    key: 'session',
    displayName: 'Session',
    symbol: '⏳',
    tier: 'core',
    Icon: SessionIcon,
  },
  player: { key: 'player', displayName: 'Player', symbol: '♟', tier: 'core', Icon: PlayerIcon },
  event: { key: 'event', displayName: 'Event', symbol: '✦', tier: 'core', Icon: EventIcon },
  agent: { key: 'agent', displayName: 'Agent', symbol: '⚡', tier: 'ai', Icon: AgentIcon },
  kb: { key: 'kb', displayName: 'Knowledge', symbol: '📜', tier: 'ai', Icon: KnowledgeIcon },
  chat: {
    key: 'chat',
    displayName: 'Chat',
    symbol: '💬',
    tier: 'ai',
    Icon: ChatIcon,
  },
  toolkit: {
    key: 'toolkit',
    displayName: 'Toolkit',
    symbol: '⚙',
    tier: 'tools',
    Icon: ToolkitIcon,
  },
  tool: { key: 'tool', displayName: 'Tool', symbol: '🔧', tier: 'tools', Icon: ToolIcon },
};

export const ENTITY_RELATIONSHIPS: EntityRelationshipMap = {
  game: ['session', 'kb', 'agent'],
  session: ['game', 'player', 'event'],
  player: ['session', 'event'],
  event: ['session', 'game', 'player'],
  agent: ['game', 'kb', 'chat', 'tool'],
  kb: ['game', 'agent'],
  chat: ['agent', 'game', 'player'],
  toolkit: ['tool', 'game'],
  tool: ['toolkit', 'game'],
};

export function getManaDisplayName(entityType: MeepleEntityType): string {
  return MANA_DISPLAY[entityType].displayName;
}

export function getManaSymbol(entityType: MeepleEntityType): string {
  return MANA_DISPLAY[entityType].symbol;
}

export function getRelatedEntityTypes(entityType: MeepleEntityType): MeepleEntityType[] {
  return ENTITY_RELATIONSHIPS[entityType];
}
