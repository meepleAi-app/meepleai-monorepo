'use client';

import Link from 'next/link';

export interface GreetingStripStats {
  games: number;
  sessions: number;
  agents: number;
}

interface GreetingStripProps {
  displayName: string;
  stats: GreetingStripStats;
}

export function GreetingStrip({ displayName, stats }: GreetingStripProps) {
  const initial = displayName.charAt(0).toUpperCase();
  const statsSummary = [
    stats.games > 0 ? `${stats.games} giochi` : null,
    stats.sessions > 0 ? `${stats.sessions} sessioni` : null,
    stats.agents > 0 ? `${stats.agents} agenti` : null,
  ]
    .filter(Boolean)
    .join(' · ');

  return (
    <div className="flex items-center gap-5 rounded-2xl border border-[hsl(25,40%,85%)] bg-gradient-to-r from-[hsl(25,40%,94%)] to-[hsl(30,35%,91%)] px-7 py-6 dark:border-[hsl(25,30%,25%)] dark:from-[hsl(25,20%,14%)] dark:to-[hsl(30,15%,12%)]">
      {/* Avatar */}
      <div
        data-testid="greeting-avatar"
        className="grid h-[52px] w-[52px] shrink-0 place-items-center rounded-xl bg-gradient-to-br from-[hsl(25,95%,45%)] to-[hsl(25,95%,55%)] text-xl font-bold text-white"
      >
        {initial}
      </div>

      {/* Text */}
      <div className="min-w-0">
        <h1 className="font-quicksand text-[22px] font-bold text-foreground">
          Ciao, {displayName} 👋
        </h1>
        <p className="mt-0.5 text-[13px] text-muted-foreground">
          La tua tavola da gioco{statsSummary ? ` · ${statsSummary}` : ''}
        </p>
      </div>

      {/* Quick Actions - desktop only */}
      <div className="ml-auto flex shrink-0 gap-2 max-md:hidden">
        <Link
          href="/sessions/new"
          className="inline-flex items-center gap-1.5 rounded-full border border-[var(--mc-border)] bg-card/70 px-4 py-2 text-xs font-semibold text-foreground transition-all hover:bg-card hover:shadow-sm"
        >
          📋 Nuova sessione
        </Link>
        <Link
          href="/library?action=add"
          className="inline-flex items-center gap-1.5 rounded-full bg-[hsl(25,95%,45%)] px-4 py-2 text-xs font-semibold text-white shadow-[0_2px_12px_rgba(210,105,30,0.25)] transition-all hover:bg-[hsl(25,95%,38%)]"
        >
          + Aggiungi gioco
        </Link>
      </div>
    </div>
  );
}
