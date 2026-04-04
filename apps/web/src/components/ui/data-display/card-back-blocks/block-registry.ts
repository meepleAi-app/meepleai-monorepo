import type { MeepleEntityType } from '../meeple-card-styles';
import type { BlockConfig } from './block-types';

const ENTITY_BLOCK_MAP: Record<MeepleEntityType, BlockConfig[]> = {
  game: [
    { type: 'stats', title: 'Statistiche' },
    { type: 'actions', title: 'Azioni' },
    { type: 'kbPreview', title: 'Knowledge Base' },
  ],
  session: [
    { type: 'ranking', title: 'Classifica' },
    { type: 'timeline', title: 'Timeline' },
    { type: 'actions', title: 'Azioni' },
  ],
  player: [
    { type: 'stats', title: 'Statistiche' },
    { type: 'actions', title: 'Azioni' },
    { type: 'progress', title: 'Progresso' },
  ],
  event: [
    { type: 'members', title: 'Partecipanti' },
    { type: 'timeline', title: 'Programma' },
    { type: 'actions', title: 'Azioni' },
  ],
  agent: [
    { type: 'stats', title: 'Statistiche' },
    { type: 'history', title: 'Query Recenti' },
    { type: 'actions', title: 'Azioni' },
  ],
  kb: [
    { type: 'stats', title: 'Statistiche' },
    { type: 'contents', title: 'Documenti' },
    { type: 'actions', title: 'Azioni' },
  ],
  chatSession: [
    { type: 'history', title: 'Messaggi Recenti' },
    { type: 'actions', title: 'Azioni' },
  ],
  collection: [
    { type: 'contents', title: 'Giochi nella Collezione' },
    { type: 'stats', title: 'Statistiche' },
    { type: 'actions', title: 'Azioni' },
  ],
  group: [
    { type: 'members', title: 'Membri' },
    { type: 'stats', title: 'Statistiche' },
    { type: 'actions', title: 'Azioni' },
  ],
  location: [
    { type: 'stats', title: 'Statistiche' },
    { type: 'actions', title: 'Azioni' },
  ],
  expansion: [
    { type: 'stats', title: 'Statistiche' },
    { type: 'actions', title: 'Azioni' },
  ],
  toolkit: [
    { type: 'contents', title: 'Strumenti' },
    { type: 'actions', title: 'Azioni' },
  ],
  tool: [
    { type: 'stats', title: 'Statistiche' },
    { type: 'actions', title: 'Azioni' },
  ],
  achievement: [
    { type: 'progress', title: 'Progresso' },
    { type: 'stats', title: 'Statistiche' },
    { type: 'actions', title: 'Azioni' },
  ],
  note: [
    { type: 'notes', title: 'Contenuto' },
    { type: 'actions', title: 'Azioni' },
  ],
  custom: [
    { type: 'stats', title: 'Info' },
    { type: 'actions', title: 'Azioni' },
  ],
};

export function getBlocksForEntity(entityType: MeepleEntityType): BlockConfig[] {
  return ENTITY_BLOCK_MAP[entityType];
}
