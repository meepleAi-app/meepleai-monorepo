'use client';

import { useDashboardMode } from '../useDashboardMode';
import { EventLog } from './EventLog';
import { QuickActions } from './QuickActions';
import { ScoreboardCompact } from './ScoreboardCompact';
import { TurnIndicator } from './TurnIndicator';

import type { EventLogItem } from './EventLog';
import type { ScoreboardPlayer } from './ScoreboardCompact';

interface TavoloViewProps {
  sessionId: string | null;
}

// Placeholder data — will be replaced by SSE session stream integration
const EMPTY_PLAYERS: ScoreboardPlayer[] = [];
const EMPTY_EVENTS: EventLogItem[] = [];

export function TavoloView({ sessionId: _sessionId }: TavoloViewProps) {
  const { openSheet } = useDashboardMode();

  return (
    <div data-testid="tavolo-view" className="flex flex-col gap-4 px-4 py-3">
      {/* Scoreboard */}
      <ScoreboardCompact players={EMPTY_PLAYERS} />

      {/* Turn indicator */}
      <TurnIndicator playerName="—" playerColor="#888888" />

      {/* Quick actions */}
      <QuickActions onAddScore={() => openSheet('scores')} onAskAi={() => openSheet('rules-ai')} />

      {/* Event log */}
      <EventLog events={EMPTY_EVENTS} />
    </div>
  );
}
