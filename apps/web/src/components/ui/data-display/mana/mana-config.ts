import type { MeepleEntityType } from '../meeple-card-styles';
import type { ManaDisplayConfig, EntityRelationshipMap } from './mana-types';

export const MANA_DISPLAY: Record<MeepleEntityType, ManaDisplayConfig> = {
  game: { key: 'game', displayName: 'Game', symbol: '🎲', tier: 'core' },
  session: { key: 'session', displayName: 'Session', symbol: '⏳', tier: 'core' },
  player: { key: 'player', displayName: 'Player', symbol: '♟', tier: 'core' },
  event: { key: 'event', displayName: 'Event', symbol: '✦', tier: 'core' },
  collection: { key: 'collection', displayName: 'Collection', symbol: '📦', tier: 'social' },
  group: { key: 'group', displayName: 'Group', symbol: '👥', tier: 'social' },
  location: { key: 'location', displayName: 'Location', symbol: '📍', tier: 'social' },
  expansion: { key: 'expansion', displayName: 'Expansion', symbol: '🃏', tier: 'social' },
  agent: { key: 'agent', displayName: 'Agent', symbol: '⚡', tier: 'ai' },
  kb: { key: 'kb', displayName: 'Knowledge', symbol: '📜', tier: 'ai' },
  chatSession: { key: 'chatSession', displayName: 'Chat', symbol: '💬', tier: 'ai' },
  note: { key: 'note', displayName: 'Note', symbol: '📝', tier: 'ai' },
  toolkit: { key: 'toolkit', displayName: 'Toolkit', symbol: '⚙', tier: 'tools' },
  tool: { key: 'tool', displayName: 'Tool', symbol: '🔧', tier: 'tools' },
  achievement: { key: 'achievement', displayName: 'Achievement', symbol: '🏆', tier: 'tools' },
  custom: { key: 'custom', displayName: 'Custom', symbol: '✧', tier: 'tools' },
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
