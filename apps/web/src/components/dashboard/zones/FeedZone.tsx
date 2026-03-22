'use client';

import Link from 'next/link';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';

// Placeholder: in production, this would be driven by an activity feed hook
interface FeedItem {
  id: string;
  entity: 'game' | 'session' | 'player';
  title: string;
  subtitle: string;
}

export function FeedZone() {
  // TODO: Replace with real activity feed hook when available
  const feedItems: FeedItem[] = [];

  if (feedItems.length === 0) {
    return (
      <section data-testid="feed-zone" className="flex-1">
        <h3 className="text-sm font-medium text-foreground/80 mb-2">Attivita recente</h3>
        <div className="rounded-xl border border-dashed border-border/50 py-6 text-center">
          <p className="text-sm text-muted-foreground">Nessuna attivita recente</p>
        </div>
      </section>
    );
  }

  return (
    <section data-testid="feed-zone" className="flex-1">
      <h3 className="text-sm font-medium text-foreground/80 mb-2">Attivita recente</h3>
      <div className="space-y-2">
        {feedItems.map(item => (
          <MeepleCard
            key={item.id}
            entity={item.entity}
            variant="list"
            title={item.title}
            subtitle={item.subtitle}
            data-testid={`feed-${item.id}`}
          />
        ))}
      </div>
      <Link
        href="/activity"
        className="block mt-2 text-xs text-primary hover:underline text-center"
      >
        Vedi tutto
      </Link>
    </section>
  );
}
