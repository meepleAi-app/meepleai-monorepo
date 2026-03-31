import React from 'react';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useActivityFeed } from '@/hooks/useActivityFeed';

function formatRelativeDate(dateStr: string): string {
  const date = new Date(dateStr);
  const now = new Date();
  const diffDays = Math.floor((now.getTime() - date.getTime()) / (1000 * 60 * 60 * 24));
  if (diffDays === 0) return 'Oggi';
  if (diffDays === 1) return 'Ieri';
  if (diffDays < 7) return `${diffDays} giorni fa`;
  return date.toLocaleDateString('it-IT', { day: '2-digit', month: 'short' });
}

export function ActivityFeed(): React.ReactElement {
  const { items, isLoading, error } = useActivityFeed(15);

  if (isLoading) {
    return (
      <div className="space-y-3">
        {[...Array(5)].map((_, i) => (
          <Skeleton key={i} className="h-14 w-full rounded-xl" data-testid="activity-skeleton" />
        ))}
      </div>
    );
  }

  if (error) {
    return (
      <p className="text-sm text-destructive font-nunito">Errore nel caricamento delle attività.</p>
    );
  }

  if (items.length === 0) {
    return (
      <div className="py-12 text-center">
        <p className="text-2xl mb-2">🎲</p>
        <p className="font-medium font-quicksand mb-1">Nessuna attività ancora</p>
        <p className="text-sm text-muted-foreground font-nunito">
          Le tue partite e i badge guadagnati appariranno qui.
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      {items.map(item => (
        <div
          key={item.id}
          className="flex items-center gap-3 p-3 rounded-xl border border-border/40 hover:bg-muted/30 transition-colors"
        >
          <span className="text-xl shrink-0" aria-hidden="true">
            {item.iconEmoji}
          </span>
          <div className="flex-1 min-w-0">
            <p className="font-medium text-sm font-nunito truncate">{item.title}</p>
            {item.subtitle && (
              <p className="text-xs text-muted-foreground font-nunito">{item.subtitle}</p>
            )}
          </div>
          <span className="text-xs text-muted-foreground font-nunito shrink-0">
            {formatRelativeDate(item.timestamp)}
          </span>
        </div>
      ))}
    </div>
  );
}
