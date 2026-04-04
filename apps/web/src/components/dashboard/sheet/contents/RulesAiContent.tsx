'use client';

import { useDashboardMode } from '../../useDashboardMode';
import { CardLinkChip } from '../CardLinkChip';

// ---------------------------------------------------------------------------
// RulesAiContent
// ---------------------------------------------------------------------------

interface RulesAiContentProps {
  sessionId: string;
}

export function RulesAiContent({ sessionId: _sessionId }: RulesAiContentProps) {
  const { navigateCardLink } = useDashboardMode();

  return (
    <div className="flex flex-col gap-4 p-4">
      <h2 className="text-lg font-semibold tracking-tight">🤖 Assistente Regole</h2>

      <div className="min-h-[120px] rounded-lg border border-border bg-muted/30 p-3">
        <p className="text-sm text-muted-foreground">Area chat — disponibile a breve…</p>
      </div>

      <div className="flex flex-wrap gap-2 pt-2">
        <CardLinkChip
          icon="🏆"
          label="Vai ai punteggi"
          accentColor="cyan"
          onClick={() => navigateCardLink('scores')}
        />
      </div>
    </div>
  );
}
