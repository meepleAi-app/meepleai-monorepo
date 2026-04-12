'use client';
import { useEffect } from 'react';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import { useRecentsStore } from '@/stores/use-recents';

interface DeckTrackerSyncProps {
  entity: MeepleEntityType;
  id: string;
  title: string;
  href: string;
  subtitle?: string;
  imageUrl?: string;
}

export function DeckTrackerSync({ entity, id, title, href }: DeckTrackerSyncProps) {
  useEffect(() => {
    useRecentsStore.getState().push({ id, entity, title, href });
  }, [id]); // eslint-disable-line react-hooks/exhaustive-deps
  return null;
}
