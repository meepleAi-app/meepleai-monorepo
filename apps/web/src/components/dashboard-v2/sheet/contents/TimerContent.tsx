'use client';

import { useDashboardMode } from '../../useDashboardMode';
import { CardLinkChip } from '../CardLinkChip';

// ---------------------------------------------------------------------------
// TimerContent
// ---------------------------------------------------------------------------

interface TimerContentProps {
  sessionId: string;
}

export function TimerContent({ sessionId: _sessionId }: TimerContentProps) {
  const { navigateCardLink } = useDashboardMode();

  return (
    <div className="flex flex-col gap-4 p-4">
      <h2 className="text-lg font-semibold tracking-tight">⏱ Timer Sessione</h2>

      <p className="text-sm text-muted-foreground">Timer in arrivo…</p>

      <div className="flex flex-wrap gap-2 pt-2">
        <CardLinkChip label="Di chi è il turno?" target="players" onClick={navigateCardLink} />
      </div>
    </div>
  );
}
