'use client';

import type { ReactElement } from 'react';

import Link from 'next/link';

import type { UserCampaignWithStale } from '@/lib/gamebook/hooks/useUserCampaigns';

export interface ResumeHeroProps {
  campaign: UserCampaignWithStale;
  gameId: string;
  onCreateNew?: () => void;
}

function daysSince(iso: string, now: Date = new Date()): number {
  const last = new Date(iso).getTime();
  if (Number.isNaN(last)) return 0;
  return Math.max(0, Math.floor((now.getTime() - last) / (1000 * 60 * 60 * 24)));
}

/**
 * Stato 02 (mockup G state-02-single-resume) — 1 campagna fresh.
 * Hero card "Riprendi: §N · letto Xgg fa" + CTA primary "Riprendi".
 *
 * FREEZE-compliant: var(--c-game) / token semantici only.
 */
export function ResumeHero({ campaign, gameId, onCreateNew }: ResumeHeroProps): ReactElement {
  const days = daysSince(campaign.lastReadAt);
  const lastLabel = days === 0 ? 'oggi' : days === 1 ? 'ieri' : `${days} giorni fa`;
  const next = (campaign.currentParagraph ?? 0) + 1;

  return (
    <section
      data-testid="gamebook-resume-hero"
      data-state="state-02-single-resume"
      className="mx-auto px-4 py-6 max-w-screen-sm lg:max-w-6xl lg:grid lg:grid-cols-[380px_minmax(0,1fr)] lg:gap-8"
    >
      {/* Sidebar — visible only on desktop (lg+).
          Mockup state-02-desktop-split-view: 380px lista campagne / single-entry preview. */}
      <aside
        data-testid="gamebook-resume-hero-sidebar"
        className="hidden lg:block"
        aria-label="Elenco campagne"
      >
        <p className="mb-3 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
          Le tue campagne
        </p>
        <ul className="space-y-2">
          <li>
            <Link
              href={`/library/${gameId}/play/${campaign.id}`}
              className="block rounded-lg border border-[var(--c-game)]/40 bg-[var(--c-game)]/5 px-4 py-3 text-sm font-medium text-[var(--c-game)] hover:bg-[var(--c-game)]/10"
              aria-current="page"
            >
              <span className="block truncate font-semibold">{campaign.title}</span>
              <span className="mt-0.5 block text-xs text-muted-foreground">
                §{campaign.currentParagraph} · {lastLabel}
              </span>
            </Link>
          </li>
        </ul>
      </aside>

      {/* Main pane — single-resume hero card. Sole content on mobile, right pane on desktop. */}
      <div data-testid="gamebook-resume-hero-main">
        <p className="mb-3 text-xs font-semibold uppercase tracking-wider text-muted-foreground lg:hidden">
          Bentornato
        </p>
        <article className="overflow-hidden rounded-2xl border border-[var(--c-game)]/30 bg-background shadow-sm">
          <div className="h-1 bg-[var(--c-game)]" aria-hidden />
          <div className="px-5 py-5 space-y-4 lg:px-8 lg:py-7">
            <header>
              <h2 className="text-xl font-bold leading-tight lg:text-2xl">{campaign.title}</h2>
              <p className="mt-1 text-xs text-muted-foreground">Ultima sessione {lastLabel}</p>
            </header>

            <div className="flex items-baseline gap-3">
              <span className="text-4xl font-extrabold tabular-nums text-[var(--c-game)] lg:text-5xl">
                §{campaign.currentParagraph}
              </span>
              <span className="text-xs text-muted-foreground">ultimo paragrafo letto</span>
            </div>

            <div className="flex flex-col gap-2 lg:flex-row lg:gap-3">
              <Link
                href={`/library/${gameId}/play/${campaign.id}`}
                data-testid="gamebook-resume-hero-cta"
                className="inline-flex items-center justify-center rounded-md bg-[var(--c-game)] px-5 py-3 text-sm font-semibold text-white shadow-sm hover:opacity-95"
              >
                Riprendi → §{next}
              </Link>
              {onCreateNew && (
                <button
                  type="button"
                  onClick={onCreateNew}
                  className="rounded-md border border-border bg-background px-5 py-2.5 text-sm font-semibold text-foreground hover:bg-muted"
                >
                  + Nuova campagna
                </button>
              )}
            </div>
          </div>
        </article>
      </div>
    </section>
  );
}
