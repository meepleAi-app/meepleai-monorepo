'use client';

import { MeepleCard } from './MeepleCard';
import type { MeepleCardProps, MeepleEntityType } from './types';

function createEntityCard(entity: MeepleEntityType) {
  const EntityCard = function EntityCard(props: Omit<MeepleCardProps, 'entity'>) {
    return <MeepleCard {...props} entity={entity} />;
  };
  EntityCard.displayName = `MeepleCards.${entity.charAt(0).toUpperCase() + entity.slice(1)}`;
  return EntityCard;
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
