'use client';

import { useMemo, useState } from 'react';

import { useSessionStore } from '@/stores/session';

import { ActivityFeedEvent } from './ActivityFeedEvent';

const PAGE_SIZE = 50;

export function ActivityFeed() {
  const events = useSessionStore(s => s.events);
  const [visibleCount, setVisibleCount] = useState(PAGE_SIZE);

  const sorted = useMemo(
    () =>
      [...events].sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()),
    [events]
  );

  if (events.length === 0) {
    return (
      <div className="flex items-center justify-center py-12 text-center">
        <p className="text-sm text-muted-foreground">
          Sessione appena iniziata — le attività appariranno qui
        </p>
      </div>
    );
  }

  const visible = sorted.slice(0, visibleCount);
  const hasMore = visibleCount < sorted.length;

  return (
    <div
      className="flex flex-col divide-y divide-border overflow-y-auto"
      data-testid="activity-feed"
    >
      {visible.map(event => (
        <ActivityFeedEvent key={event.id} event={event} />
      ))}
      {hasMore && (
        <button
          type="button"
          onClick={() => setVisibleCount(prev => prev + PAGE_SIZE)}
          className="py-3 text-xs text-muted-foreground hover:text-foreground transition-colors"
        >
          Mostra altri ({sorted.length - visibleCount} rimanenti)
        </button>
      )}
    </div>
  );
}
