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
        <CardLinkChip
          icon="🤖"
          label="Come si punteggia?"
          accentColor="violet"
          onClick={() => navigateCardLink('rules-ai')}
        />
        <CardLinkChip
          icon="👥"
          label="Dettaglio per giocatore"
          accentColor="emerald"
          onClick={() => navigateCardLink('players')}
        />
      </div>
    </div>
  );
}
