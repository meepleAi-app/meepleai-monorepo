'use client';

import { useTranslation } from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';

export interface DraftAutosaveIndicatorProps {
  isPending: boolean;
  lastSavedAt: number | null;
}

function formatTime(ts: number): string {
  const d = new Date(ts);
  const hh = String(d.getHours()).padStart(2, '0');
  const mm = String(d.getMinutes()).padStart(2, '0');
  return `${hh}:${mm}`;
}

/**
 * #2436 PR-A AC-A2 — draft autosave status. Renders nothing until the first
 * save is pending or completed. role="status" + aria-live="polite" announces
 * the state change politely to assistive tech.
 */
export function DraftAutosaveIndicator({ isPending, lastSavedAt }: DraftAutosaveIndicatorProps) {
  const { t } = useTranslation();

  if (!isPending && lastSavedAt === null) return null;

  return (
    <span
      role="status"
      aria-live="polite"
      data-testid="draft-autosave-indicator"
      className="inline-flex items-center gap-1.5 font-mono text-[10px] font-bold uppercase tracking-widest text-muted-foreground"
    >
      <span
        className={cn(
          'h-1.5 w-1.5 rounded-full',
          isPending ? 'bg-entity-session animate-pulse' : 'bg-entity-session/50'
        )}
        aria-hidden="true"
      />
      {isPending
        ? t('playRecords.new.draft.saving')
        : t('playRecords.new.draft.saved').replace('{time}', formatTime(lastSavedAt as number))}
    </span>
  );
}
