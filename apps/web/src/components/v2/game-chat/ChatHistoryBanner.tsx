/**
 * ChatHistoryBanner — disclaimer "messaggi storici text-only" mostrato in cima
 * alla lista messaggi quando ci sono SIA storici (da hydrate) SIA nuovi (sessione corrente).
 *
 * Spec: docs/superpowers/specs/2026-05-10-fast-resume-audit-g2-design.md §3.3
 */
import type { ReactElement } from 'react';

import { Info } from 'lucide-react';
import clsx from 'clsx';

export interface ChatHistoryBannerProps {
  readonly className?: string;
}

export function ChatHistoryBanner({ className }: ChatHistoryBannerProps): ReactElement {
  return (
    <div
      role="note"
      data-slot="chat-history-banner"
      className={clsx(
        'flex items-start gap-2 rounded-md border-l-4 px-3 py-2 text-xs',
        'border-l-[hsl(var(--c-info,210_80%_50%)/0.5)] bg-muted/50 text-muted-foreground',
        className
      )}
    >
      <Info className="mt-0.5 h-3.5 w-3.5 flex-shrink-0" aria-hidden="true" />
      <span>
        I messaggi precedenti a questa sessione sono in sola lettura. Citazioni e indicatori di
        confidenza non sono ricostruibili.
      </span>
    </div>
  );
}
