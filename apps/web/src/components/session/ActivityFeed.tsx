'use client';

import { useSessionStore } from '@/store/session';

import { ActivityFeedEvent } from './ActivityFeedEvent';

export function ActivityFeed() {
  const events = useSessionStore(s => s.events);

  if (events.length === 0) {
    return (
      <div className="flex items-center justify-center py-12 text-center">
        <p className="text-sm text-muted-foreground">
          Sessione appena iniziata — le attività appariranno qui
        </p>
      </div>
    );
  }

  const sorted = [...events].sort(
    (a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()
  );

  return (
    <div
      className="flex flex-col divide-y divide-border overflow-y-auto"
      data-testid="activity-feed"
    >
      {sorted.map(event => (
        <ActivityFeedEvent key={event.id} event={event} />
      ))}
    </div>
  );
}
