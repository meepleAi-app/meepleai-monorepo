'use client';

import { useEffect, useState } from 'react';

import Link from 'next/link';

export interface DashboardHeroProps {
  displayName: string;
}

function formatKicker(now: Date): string {
  const weekday = now.toLocaleDateString('it-IT', { weekday: 'long' });
  const day = now.toLocaleDateString('it-IT', { day: 'numeric', month: 'long' });
  return `BENVENUTO · ${weekday.toUpperCase()} ${day.toUpperCase()}`;
}

export function DashboardHero({ displayName }: DashboardHeroProps) {
  const [kicker, setKicker] = useState('');

  useEffect(() => {
    setKicker(formatKicker(new Date()));
  }, []);

  return (
    <section className="mx-auto w-full max-w-[1200px] px-6 pb-8 pt-16">
      <div
        data-testid="hero-kicker"
        aria-hidden="true"
        className="mb-3 min-h-[1em] font-mono text-xs font-bold uppercase tracking-[0.12em] text-muted-foreground"
      >
        {kicker || ' '}
      </div>
      <h1 className="mb-4 font-quicksand text-[clamp(32px,5vw,48px)] font-bold leading-tight tracking-tight text-foreground">
        Ciao,{' '}
        {/* Two-stop gradient (orange → purple). Dropped the rose midpoint because
            hsl(350 89% 60%) on cream gave 2.95:1 — fails WCAG 1.4.3 large-text
            (≥ 3:1). See docs/for-developers/audits/2026-05-12-dashboard-contrast.md */}
        <span className="hero-mark bg-gradient-to-r from-[hsl(var(--c-game))] to-[hsl(var(--c-player))] bg-clip-text text-transparent">
          {displayName}
        </span>
      </h1>
      <p className="max-w-[680px] text-[17px] leading-snug text-muted-foreground">
        La tua tavola da gioco di oggi — riprendi una sessione, sfoglia la libreria, attiva un agente.
      </p>
      <div className="mt-5 flex gap-2">
        <Link
          href="/sessions/new"
          className="inline-flex items-center gap-1 rounded-full bg-[hsl(var(--c-game))] px-5 py-2 font-quicksand text-xs font-bold text-white shadow-sm transition-transform hover:scale-[1.03]"
        >
          + Nuova sessione
        </Link>
        <Link
          href="/library?action=add"
          className="inline-flex items-center gap-1 rounded-full border border-border bg-card px-5 py-2 font-quicksand text-xs font-bold text-foreground transition-colors hover:bg-muted"
        >
          + Aggiungi gioco
        </Link>
      </div>
    </section>
  );
}
