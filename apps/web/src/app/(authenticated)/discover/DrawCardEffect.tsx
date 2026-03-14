'use client';

import { useEffect } from 'react';

import { useCardHand } from '@/stores/use-card-hand';

/**
 * Draws a section card into the card hand when the Discover page mounts.
 * Renders nothing.
 */
export function DiscoverDrawCardEffect() {
  const { drawCard } = useCardHand();

  useEffect(() => {
    drawCard({
      id: 'section-discover',
      entity: 'game',
      title: 'Discover',
      href: '/discover',
    });
  }, [drawCard]);

  return null;
}
