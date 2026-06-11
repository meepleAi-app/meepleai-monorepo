'use client';

import { cn } from '@/lib/utils';

export interface CommunityStats {
  totalGames: number;
  totalPlayers: number;
  totalSessions: number;
  totalCommunityContent: number;
}

interface CommunityStatsRowProps {
  stats: CommunityStats;
  className?: string;
}

/**
 * Community stats banner — 4-column grid (Games / Players / Sessions / Content).
 *
 * #2208 DS-17-10 sub-issue: NEW primitive for sp3-library-public route.
 * Mockup parity ref: `admin-mockups/design_files/sp3-library-public.jsx` line 140.
 * Each cell: big number (font-display) + label (uppercase small).
 */
export function CommunityStatsRow({ stats, className }: CommunityStatsRowProps) {
  const items: Array<{ key: keyof CommunityStats; label: string }> = [
    { key: 'totalGames', label: 'Giochi' },
    { key: 'totalPlayers', label: 'Giocatori' },
    { key: 'totalSessions', label: 'Partite' },
    { key: 'totalCommunityContent', label: 'Contenuti community' },
  ];

  return (
    <div
      className={cn(
        'grid grid-cols-2 gap-6 rounded-2xl border border-border/50 bg-card/90 p-6 backdrop-blur-md sm:grid-cols-4',
        className
      )}
      role="region"
      aria-label="Statistiche community MeepleAI"
    >
      {items.map(item => (
        <div key={item.key} className="flex flex-col items-start gap-1">
          <span className="font-quicksand text-3xl font-bold tabular-nums text-foreground sm:text-4xl">
            {stats[item.key].toLocaleString('it-IT')}
          </span>
          <span className="text-[10px] font-bold uppercase tracking-wide text-muted-foreground">
            {item.label}
          </span>
        </div>
      ))}
    </div>
  );
}
