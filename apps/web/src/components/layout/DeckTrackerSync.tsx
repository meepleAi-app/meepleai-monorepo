'use client';
import { useEffect } from 'react';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card-styles';
import { useCardHand } from '@/stores/use-card-hand';

interface DeckTrackerSyncProps {
  entity: MeepleEntityType;
  id: string;
  title: string;
  href: string;
  subtitle?: string;
  imageUrl?: string;
}

export function DeckTrackerSync({
  entity,
  id,
  title,
  href,
  subtitle,
  imageUrl,
}: DeckTrackerSyncProps) {
  const drawCard = useCardHand(s => s.drawCard);
  useEffect(() => {
    drawCard({ id, entity, title, href, subtitle, imageUrl });
  }, [id]); // eslint-disable-line react-hooks/exhaustive-deps
  return null;
}
