'use client';

import type { ReactElement } from 'react';

import Link from 'next/link';

import type { UserCampaignWithStale } from '@/lib/gamebook/hooks/useUserCampaigns';

export interface StaleWarningCardProps {
  campaign: UserCampaignWithStale;
  gameId: string;
  onArchive?: () => void;
  onCreateNew?: () => void;
}

function daysSince(iso: string, now: Date = new Date()): number {
  const last = new Date(iso).getTime();
  if (Number.isNaN(last)) return 0;
  return Math.max(0, Math.floor((now.getTime() - last) / (1000 * 60 * 60 * 24)));
}

/**
 * Stato 04 (mockup G state-04-stale-warning) — 1 campagna stale > 90gg.
 * Banner amber rispettoso + CTA "Riprendi (potrebbe essere disorientante)" +
 * CTA secondario "Archivia e ricomincia".
 *
 * Coverage: scenario @N4.5 (stale > 90 days edge).
 * FREEZE-compliant: usa var(--c-game) per accent + Tailwind warning palette.
 */
export function StaleWarningCard({
  campaign,
  gameId,
  onArchive,
  onCreateNew,
}: StaleWarningCardProps): ReactElement {
  const days = daysSince(campaign.lastReadAt);
  const next = (campaign.currentParagraph ?? 0) + 1;

  return (
    <section
      data-testid="gamebook-resume-stale-warning"
      data-state="state-04-stale-warning"
      className="max-w-screen-sm lg:max-w-2xl mx-auto px-4 py-6"
    >
      <p className="mb-3 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
        Bentornato
      </p>
      <article className="overflow-hidden rounded-2xl border border-amber-500/40 bg-background shadow-sm">
        <div className="h-1 bg-[var(--c-game)]" aria-hidden />

        <div
          role="status"
          className="flex items-start gap-3 border-b border-amber-500/30 bg-amber-500/10 px-4 py-3"
        >
          <span aria-hidden className="text-lg leading-tight">
            ⚠️
          </span>
          <div className="min-w-0">
            <p className="text-sm font-semibold text-amber-700 dark:text-amber-300">
              Ultima sessione {days} giorni fa
            </p>
            <p className="mt-0.5 text-xs text-muted-foreground leading-relaxed">
              Potrebbe volerci qualche minuto per ricordare la trama. Nessuna fretta — il glossario
              e il party sono ancora qui.
            </p>
          </div>
        </div>

        <div className="px-5 py-5 space-y-4">
          <header>
            <h2 className="text-xl font-bold leading-tight">{campaign.title}</h2>
          </header>

          <div className="flex items-baseline gap-3">
            <span className="text-4xl font-extrabold tabular-nums text-[var(--c-game)]">
              §{campaign.currentParagraph}
            </span>
            <span className="text-xs text-muted-foreground">ultimo paragrafo letto</span>
          </div>

          <div className="flex flex-col gap-2">
            <Link
              href={`/library/${gameId}/play/${campaign.id}`}
              data-testid="gamebook-resume-stale-resume-cta"
              className="inline-flex items-center justify-center gap-2 rounded-md bg-[var(--c-game)] px-5 py-3 text-sm font-semibold text-white shadow-sm hover:opacity-95"
            >
              <span>Riprendi → §{next}</span>
              <span className="text-xs font-medium opacity-85">
                (potrebbe essere disorientante)
              </span>
            </Link>
            {onArchive && (
              <button
                type="button"
                onClick={onArchive}
                data-testid="gamebook-resume-stale-archive-cta"
                className="rounded-md border border-amber-500/40 bg-transparent px-5 py-2.5 text-sm font-semibold text-amber-700 dark:text-amber-300 hover:bg-amber-500/10"
              >
                Archivia e ricomincia
              </button>
            )}
            {onCreateNew && (
              <button
                type="button"
                onClick={onCreateNew}
                className="rounded-md border border-border bg-background px-5 py-2.5 text-sm font-semibold text-foreground hover:bg-muted"
              >
                + Nuova campagna
              </button>
            )}
            <p className="text-center text-xs text-muted-foreground leading-relaxed">
              Archiviare non cancella — la campagna resta consultabile in cronologia.
            </p>
          </div>
        </div>
      </article>
    </section>
  );
}
