'use client';

import { useDashboardMode } from '../../useDashboardMode';
import { CardLinkChip } from '../CardLinkChip';

// ---------------------------------------------------------------------------
// PlayersContent
// ---------------------------------------------------------------------------

interface PlayersContentProps {
  sessionId: string;
}

export function PlayersContent({ sessionId: _sessionId }: PlayersContentProps) {
  const { navigateCardLink } = useDashboardMode();

  return (
    <div className="flex flex-col gap-4 p-4">
      <h2 className="text-lg font-semibold tracking-tight">👥 Partecipanti</h2>

      <p className="text-sm text-muted-foreground">Elenco giocatori in caricamento…</p>

      <div className="flex flex-wrap gap-2 pt-2">
        <CardLinkChip label="Punteggi giocatore" target="scores" onClick={navigateCardLink} />
      </div>
    </div>
  );
}
