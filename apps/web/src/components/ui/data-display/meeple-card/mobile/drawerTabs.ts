/**
 * Entity-specific tab definitions for MobileCardDrawer.
 *
 * Each entity type exposes a set of tabs shown in the drawer when the user
 * taps the focused card in the mobile layout. The tab IDs are stable and can
 * be used by consumers to control active tab via props.
 */

import type { MeepleEntityType } from '../types';

export interface CardDrawerTab {
  id: string;
  label: string;
  icon: string;
}

export const entityDrawerTabs: Record<MeepleEntityType, CardDrawerTab[]> = {
  game: [
    { id: 'overview', label: 'Overview', icon: '📋' },
    { id: 'ai', label: 'AI', icon: '🤖' },
    { id: 'sessions', label: 'Sessioni', icon: '🎲' },
    { id: 'media', label: 'Media', icon: '🖼' },
    { id: 'scoreboard', label: 'Scoreboard', icon: '🏆' },
  ],
  player: [
    { id: 'overview', label: 'Profilo', icon: '👤' },
    { id: 'stats', label: 'Stats', icon: '📊' },
    { id: 'history', label: 'Storico', icon: '📜' },
    { id: 'achievements', label: 'Achievements', icon: '🏅' },
  ],
  session: [
    { id: 'overview', label: 'Overview', icon: '📋' },
    { id: 'players', label: 'Players', icon: '👥' },
    { id: 'scoreboard', label: 'Scoreboard', icon: '🏆' },
    { id: 'notes', label: 'Note', icon: '📝' },
    { id: 'photos', label: 'Foto', icon: '📷' },
  ],
  agent: [
    { id: 'overview', label: 'Overview', icon: '📋' },
    { id: 'config', label: 'Config', icon: '⚙️' },
    { id: 'stats', label: 'Stats', icon: '📊' },
    { id: 'chat', label: 'Chat', icon: '💬' },
    { id: 'sources', label: 'Fonti', icon: '📚' },
  ],
  kb: [
    { id: 'overview', label: 'Overview', icon: '📋' },
    { id: 'chunks', label: 'Chunks', icon: '🧩' },
    { id: 'preview', label: 'Preview', icon: '👁' },
    { id: 'metadata', label: 'Metadata', icon: '🏷' },
  ],
  chat: [
    { id: 'messages', label: 'Messaggi', icon: '💬' },
    { id: 'context', label: 'Contesto', icon: '🧠' },
    { id: 'sources', label: 'Fonti', icon: '📚' },
    { id: 'agent', label: 'Agent', icon: '🤖' },
  ],
  event: [
    { id: 'overview', label: 'Overview', icon: '📋' },
    { id: 'participants', label: 'Partecipanti', icon: '👥' },
    { id: 'games', label: 'Giochi', icon: '🎲' },
    { id: 'location', label: 'Luogo', icon: '📍' },
  ],
  toolkit: [
    { id: 'overview', label: 'Overview', icon: '📋' },
    { id: 'tools', label: 'Strumenti', icon: '🛠' },
    { id: 'decks', label: 'Mazzi', icon: '🃏' },
    { id: 'phases', label: 'Fasi', icon: '📖' },
    { id: 'history', label: 'Storico', icon: '📜' },
  ],
  tool: [
    { id: 'overview', label: 'Overview', icon: '📋' },
    { id: 'use', label: 'Usa', icon: '▶' },
    { id: 'history', label: 'Storico', icon: '📜' },
    { id: 'config', label: 'Config', icon: '⚙️' },
  ],
};

export function getDrawerTabs(entity: MeepleEntityType): CardDrawerTab[] {
  return entityDrawerTabs[entity];
}
