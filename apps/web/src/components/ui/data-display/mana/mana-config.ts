import {
  GameIcon,
  SessionIcon,
  PlayerIcon,
  EventIcon,
  CollectionIcon,
  GroupIcon,
  LocationIcon,
  ExpansionIcon,
  AgentIcon,
  KnowledgeIcon,
  ChatIcon,
  NoteIcon,
  ToolkitIcon,
  ToolIcon,
  AchievementIcon,
  CustomIcon,
} from '@/components/icons/entities';

import type { MeepleEntityType } from '../meeple-card-styles';
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
  collection: {
    key: 'collection',
    displayName: 'Collection',
    symbol: '📦',
    tier: 'social',
    Icon: CollectionIcon,
  },
  group: { key: 'group', displayName: 'Group', symbol: '👥', tier: 'social', Icon: GroupIcon },
  location: {
    key: 'location',
    displayName: 'Location',
    symbol: '📍',
    tier: 'social',
    Icon: LocationIcon,
  },
  expansion: {
    key: 'expansion',
    displayName: 'Expansion',
    symbol: '🃏',
    tier: 'social',
    Icon: ExpansionIcon,
  },
  agent: { key: 'agent', displayName: 'Agent', symbol: '⚡', tier: 'ai', Icon: AgentIcon },
  kb: { key: 'kb', displayName: 'Knowledge', symbol: '📜', tier: 'ai', Icon: KnowledgeIcon },
  chatSession: {
    key: 'chatSession',
    displayName: 'Chat',
    symbol: '💬',
    tier: 'ai',
    Icon: ChatIcon,
  },
  note: { key: 'note', displayName: 'Note', symbol: '📝', tier: 'ai', Icon: NoteIcon },
  toolkit: {
    key: 'toolkit',
    displayName: 'Toolkit',
    symbol: '⚙',
    tier: 'tools',
    Icon: ToolkitIcon,
  },
  tool: { key: 'tool', displayName: 'Tool', symbol: '🔧', tier: 'tools', Icon: ToolIcon },
  achievement: {
    key: 'achievement',
    displayName: 'Achievement',
    symbol: '🏆',
    tier: 'tools',
    Icon: AchievementIcon,
  },
  custom: { key: 'custom', displayName: 'Custom', symbol: '✧', tier: 'tools', Icon: CustomIcon },
};

export const ENTITY_RELATIONSHIPS: EntityRelationshipMap = {
  game: ['session', 'kb', 'agent', 'expansion', 'collection', 'note'],
  session: ['game', 'player', 'event', 'location', 'note', 'group'],
  player: ['session', 'group', 'achievement', 'collection', 'note'],
  event: ['session', 'game', 'player', 'location', 'group'],
  collection: ['game', 'expansion', 'player'],
  group: ['player', 'event', 'session', 'location'],
  location: ['event', 'session', 'group'],
  expansion: ['game', 'collection'],
  agent: ['game', 'kb', 'chatSession', 'tool'],
  kb: ['game', 'agent', 'note'],
  chatSession: ['agent', 'game', 'player'],
  toolkit: ['tool', 'game'],
  tool: ['toolkit', 'game'],
  achievement: ['player', 'game', 'session'],
  note: ['game', 'session', 'player', 'kb', 'event'],
  custom: [],
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
