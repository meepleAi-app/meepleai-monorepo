'use client';

import Link from 'next/link';

import { IS_ALPHA_MODE } from '@/lib/alpha-mode';

interface ActiveSessionBannerProps {
  gameName: string;
  elapsed: string;
  sessionId: string;
}

export function ActiveSessionBanner({ gameName, elapsed, sessionId }: ActiveSessionBannerProps) {
  if (IS_ALPHA_MODE) return null;

  return (
    <div className="flex items-center justify-between gap-4 rounded-2xl px-5 py-3 bg-emerald-500/10 border border-emerald-500/25 dark:bg-emerald-900/20 dark:border-emerald-700/30">
      <div className="flex items-center gap-3 min-w-0">
        <span
          className="relative flex h-2.5 w-2.5 shrink-0"
          role="status"
          aria-label="Sessione attiva"
        >
          <span className="animate-ping absolute inline-flex h-full w-full rounded-full bg-emerald-400 opacity-75" />
          <span className="relative inline-flex rounded-full h-2.5 w-2.5 bg-emerald-500" />
        </span>

        <div className="min-w-0">
          <p className="font-nunito text-[10px] font-bold uppercase tracking-widest text-emerald-700 dark:text-emerald-400">
            Sessione in corso
          </p>
          <p className="font-quicksand text-sm font-bold text-foreground truncate">
            {gameName}
            <span className="font-nunito font-normal text-muted-foreground text-xs ml-2">
              {elapsed}
            </span>
          </p>
        </div>
      </div>

      <Link
        href={`/sessions/live/${sessionId}`}
        className="shrink-0 font-quicksand text-sm font-bold text-emerald-700 dark:text-emerald-400 hover:text-emerald-600 dark:hover:text-emerald-300 transition-colors whitespace-nowrap"
        aria-label={`Riprendi sessione di ${gameName}`}
      >
        Riprendi →
      </Link>
    </div>
  );
}
