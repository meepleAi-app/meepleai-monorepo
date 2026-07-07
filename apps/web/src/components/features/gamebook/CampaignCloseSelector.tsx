/**
 * CampaignCloseSelector — the play-evening-end 3-way close selector (issue #2639,
 * SI-8). Reconciles the mockup's 4 outcome states to the ratified D2 model:
 * three top-level choices, "defeat" being a dock-only branch (out of scope here).
 *
 *   • Completa (terminal)  → close the campaign as Completed.
 *   • Archivia (resumable) → leave the campaign open; the evening's Session is
 *                            finalized separately (SI-3). Fires `onArchive`.
 *   • Abbandona (terminal) → close the campaign as Abandoned.
 *
 * Per D4 (reuse-Session), the play-evening lifecycle (Session.Finalize + the
 * GameNight in-progress→completed transition) lives in SI-3; this component owns
 * only the campaign-level terminal outcome. Completa/Abbandona POST to the SI-8
 * close endpoint via `useCloseGamebookCampaign`; the caller composes the SI-3
 * finalize where a live GameNight session exists.
 *
 * Design tokens only (no hardcoded neutral utilities; entity/semantic colors).
 */

'use client';

import { useCallback, type ReactElement } from 'react';

import { useTranslation } from '@/hooks/useTranslation';
import { GamebookCampaignOutcome, type GamebookCampaign } from '@/lib/api/gamebook-campaigns';
import { useCloseGamebookCampaign } from '@/lib/gamebook/hooks/useCloseGamebookCampaign';

export interface CampaignCloseSelectorProps {
  readonly campaignId: string;
  readonly campaignName: string;
  /** Fired when the user picks "Archivia" (resumable — no campaign close). */
  readonly onArchive: () => void;
  /** Fired after a successful terminal close (Completa/Abbandona). */
  readonly onClosed?: (campaign: GamebookCampaign) => void;
}

export function CampaignCloseSelector({
  campaignId,
  campaignName,
  onArchive,
  onClosed,
}: CampaignCloseSelectorProps): ReactElement {
  const { t } = useTranslation();
  const close = useCloseGamebookCampaign(campaignId);

  const handleComplete = useCallback(() => {
    close.mutate(GamebookCampaignOutcome.Completed, { onSuccess: onClosed });
  }, [close, onClosed]);

  const handleAbandon = useCallback(() => {
    close.mutate(GamebookCampaignOutcome.Abandoned, { onSuccess: onClosed });
  }, [close, onClosed]);

  const busy = close.isPending;

  return (
    <section
      data-slot="campaign-close-selector"
      data-testid="campaign-close-selector"
      aria-label={t('gamebook.campaignClose.heading')}
      className="flex flex-col gap-3 rounded-lg border border-border bg-card p-4"
    >
      <div>
        <h2 className="font-quicksand text-lg font-bold text-foreground">
          {t('gamebook.campaignClose.heading')}
        </h2>
        <p className="mt-0.5 text-sm text-muted-foreground">
          {t('gamebook.campaignClose.prompt', { campaign: campaignName })}
        </p>
      </div>

      <div className="flex flex-col gap-2">
        <CloseChoice
          testId="campaign-close-complete"
          label={t('gamebook.campaignClose.complete')}
          hint={t('gamebook.campaignClose.completeHint')}
          tone="success"
          disabled={busy}
          onClick={handleComplete}
        />
        <CloseChoice
          testId="campaign-close-archive"
          label={t('gamebook.campaignClose.archive')}
          hint={t('gamebook.campaignClose.archiveHint')}
          tone="neutral"
          disabled={busy}
          onClick={onArchive}
        />
        <CloseChoice
          testId="campaign-close-abandon"
          label={t('gamebook.campaignClose.abandon')}
          hint={t('gamebook.campaignClose.abandonHint')}
          tone="danger"
          disabled={busy}
          onClick={handleAbandon}
        />
      </div>

      {busy && (
        <p
          data-testid="campaign-close-pending"
          className="text-sm text-muted-foreground"
          aria-live="polite"
        >
          {t('gamebook.campaignClose.closing')}
        </p>
      )}

      {close.isError && !busy && (
        <div role="alert" data-testid="campaign-close-error" className="flex items-center gap-2">
          <p className="text-sm text-[hsl(var(--c-danger))]">{t('gamebook.campaignClose.error')}</p>
          <button
            type="button"
            onClick={() => close.reset()}
            className="rounded-md border border-border px-2 py-1 text-sm font-medium text-foreground"
          >
            {t('gamebook.campaignClose.retry')}
          </button>
        </div>
      )}
    </section>
  );
}

function CloseChoice({
  testId,
  label,
  hint,
  tone,
  disabled,
  onClick,
}: {
  testId: string;
  label: string;
  hint: string;
  tone: 'success' | 'neutral' | 'danger';
  disabled: boolean;
  onClick: () => void;
}): ReactElement {
  const toneClass =
    tone === 'success'
      ? 'border-[hsl(var(--c-success)/0.4)] hover:bg-[hsl(var(--c-success)/0.08)]'
      : tone === 'danger'
        ? 'border-[hsl(var(--c-danger)/0.4)] hover:bg-[hsl(var(--c-danger)/0.08)]'
        : 'border-border hover:bg-muted';
  return (
    <button
      type="button"
      data-testid={testId}
      disabled={disabled}
      onClick={onClick}
      className={
        'flex flex-col items-start rounded-md border px-4 py-3 text-left transition-colors disabled:opacity-60 ' +
        toneClass
      }
    >
      <span className="font-quicksand font-semibold text-foreground">{label}</span>
      <span className="mt-0.5 text-xs text-muted-foreground">{hint}</span>
    </button>
  );
}
