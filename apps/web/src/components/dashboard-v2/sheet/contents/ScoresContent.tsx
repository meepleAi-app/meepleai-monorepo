'use client';

import { useDashboardMode } from '../../useDashboardMode';
import { CardLinkChip } from '../CardLinkChip';

// ---------------------------------------------------------------------------
// ScoresContent
// ---------------------------------------------------------------------------

interface ScoresContentProps {
  sessionId: string;
}

export function ScoresContent({ sessionId: _sessionId }: ScoresContentProps) {
  const { navigateCardLink } = useDashboardMode();

  return (
    <div className="flex flex-col gap-4 p-4">
      <h2 className="text-lg font-semibold tracking-tight">🏆 Classifica Dettagliata</h2>

      <p className="text-sm text-muted-foreground">Caricamento punteggi in corso…</p>

      <div className="flex flex-wrap gap-2 pt-2">
        <CardLinkChip label="Come si punteggia?" target="rules-ai" onClick={navigateCardLink} />
        <CardLinkChip label="Dettaglio per giocatore" target="players" onClick={navigateCardLink} />
      </div>
    </div>
  );
}
