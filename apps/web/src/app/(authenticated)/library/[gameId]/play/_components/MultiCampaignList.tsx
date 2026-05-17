/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 will introduce primitives encoding bg via className. */
'use client';

import { useState, type ReactElement } from 'react';

import Link from 'next/link';

import type { UserCampaignWithStale } from '@/lib/gamebook/hooks/useUserCampaigns';

export interface MultiCampaignListProps {
  campaigns: UserCampaignWithStale[];
  gameId: string;
  onCreateNew?: () => void;
  /** Inline rename — handler resolves with new title or rejects on cancel/error. */
  onRename?: (campaignId: string, newTitle: string) => Promise<void> | void;
  /** Soft-delete with explicit user confirm (caller is responsible for the confirmation UI cue). */
  onDelete?: (campaignId: string) => Promise<void> | void;
}

function daysSince(iso: string, now: Date = new Date()): number {
  const last = new Date(iso).getTime();
  if (Number.isNaN(last)) return 0;
  return Math.max(0, Math.floor((now.getTime() - last) / (1000 * 60 * 60 * 24)));
}

/**
 * Stato 03 (mockup G state-03-multi-campaign) — 2+ campagne in parallelo.
 * Header "Le tue campagne attive (N)" + lista card + CTA "Nuova campagna".
 *
 * Coverage: scenario @N4.4 (multi-campaign edge).
 * FREEZE-compliant.
 */
export function MultiCampaignList({
  campaigns,
  gameId,
  onCreateNew,
  onRename,
  onDelete,
}: MultiCampaignListProps): ReactElement {
  const [busyId, setBusyId] = useState<string | null>(null);

  const handleRename = async (campaignId: string, currentTitle: string) => {
    if (!onRename) return;
    const next = window.prompt('Nuovo titolo della campagna:', currentTitle);
    const trimmed = next?.trim();
    if (!trimmed || trimmed === currentTitle) return;
    try {
      setBusyId(campaignId);
      await onRename(campaignId, trimmed);
    } finally {
      setBusyId(null);
    }
  };

  const handleDelete = async (campaignId: string, currentTitle: string) => {
    if (!onDelete) return;
    if (
      !window.confirm(
        `Eliminare la campagna "${currentTitle}"? Questa azione è reversibile solo lato server (soft-delete).`
      )
    )
      return;
    try {
      setBusyId(campaignId);
      await onDelete(campaignId);
    } finally {
      setBusyId(null);
    }
  };

  return (
    <section
      data-testid="gamebook-resume-multi-list"
      data-state="state-03-multi-campaign"
      className="max-w-screen-sm mx-auto px-4 py-6 space-y-4"
    >
      <header>
        <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
          Bentornato
        </p>
        <h2 className="mt-1 text-2xl font-bold tracking-tight">
          Le tue campagne attive ({campaigns.length})
        </h2>
        <p className="mt-1.5 text-sm text-muted-foreground">
          Ogni campagna mantiene il suo glossario e il suo party. Niente si mescola.
        </p>
      </header>

      <ul className="flex flex-col gap-3">
        {campaigns.map((campaign, idx) => {
          const accent = idx === 0 ? 'var(--c-game)' : 'var(--c-agent)';
          const days = daysSince(campaign.lastReadAt);
          const lastLabel = days === 0 ? 'oggi' : days === 1 ? 'ieri' : `${days}gg fa`;
          const next = (campaign.currentParagraph ?? 0) + 1;
          return (
            <li
              key={campaign.id}
              className="overflow-hidden rounded-xl border bg-background shadow-sm"
              style={{ borderColor: `color-mix(in srgb, ${accent} 32%, transparent)` }}
              data-testid={`gamebook-resume-multi-item-${campaign.id}`}
            >
              <div className="h-1" style={{ background: accent }} aria-hidden />
              <div className="px-4 py-4 space-y-3">
                <div className="flex items-start justify-between gap-3">
                  <h3 className="text-base font-semibold leading-tight">{campaign.title}</h3>
                  <span
                    className="flex-none rounded-full px-2 py-0.5 text-xs font-bold tabular-nums"
                    style={{
                      background: `color-mix(in srgb, ${accent} 12%, transparent)`,
                      color: accent,
                    }}
                  >
                    §{campaign.currentParagraph}
                  </span>
                </div>
                <p className="text-xs text-muted-foreground tabular-nums">
                  ultima lettura {lastLabel}
                </p>
                <Link
                  href={`/library/${gameId}/play/${campaign.id}`}
                  className="inline-flex w-full items-center justify-center rounded-md px-4 py-2 text-sm font-semibold text-white shadow-sm hover:opacity-95"
                  style={{ background: accent }}
                >
                  Riprendi → §{next}
                </Link>
                {(onRename || onDelete) && (
                  <div className="flex items-center justify-end gap-1 pt-1">
                    {onRename && (
                      <button
                        type="button"
                        onClick={() => handleRename(campaign.id, campaign.title)}
                        disabled={busyId === campaign.id}
                        data-testid={`gamebook-resume-multi-rename-${campaign.id}`}
                        className="rounded px-2 py-1 text-xs font-medium text-muted-foreground hover:bg-muted disabled:opacity-50"
                      >
                        Rinomina
                      </button>
                    )}
                    {onDelete && (
                      <button
                        type="button"
                        onClick={() => handleDelete(campaign.id, campaign.title)}
                        disabled={busyId === campaign.id}
                        data-testid={`gamebook-resume-multi-delete-${campaign.id}`}
                        className="rounded px-2 py-1 text-xs font-medium text-destructive hover:bg-destructive/10 disabled:opacity-50"
                      >
                        Elimina
                      </button>
                    )}
                  </div>
                )}
              </div>
            </li>
          );
        })}
      </ul>

      {onCreateNew && (
        <button
          type="button"
          onClick={onCreateNew}
          data-testid="gamebook-resume-multi-cta"
          className="w-full rounded-md border-2 border-dashed border-border bg-transparent px-4 py-3 text-sm font-semibold text-muted-foreground hover:bg-muted"
        >
          + Nuova campagna
        </button>
      )}
    </section>
  );
}
