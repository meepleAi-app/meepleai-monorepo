'use client';

import { MeepleCard } from './MeepleCard';
import type { MeepleCardProps, MeepleEntityType } from './types';

function createEntityCard(entity: MeepleEntityType) {
  return function EntityCard(props: Omit<MeepleCardProps, 'entity'>) {
    return <MeepleCard {...props} entity={entity} />;
  };
}

export const MeepleCards = {
  Game: createEntityCard('game'),
  Player: createEntityCard('player'),
  Session: createEntityCard('session'),
  Agent: createEntityCard('agent'),
  Kb: createEntityCard('kb'),
  Chat: createEntityCard('chat'),
  Event: createEntityCard('event'),
  Toolkit: createEntityCard('toolkit'),
  Tool: createEntityCard('tool'),
  Base: MeepleCard,
};
