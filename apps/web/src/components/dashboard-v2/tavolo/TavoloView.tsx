'use client';

import { useDashboardMode } from '../useDashboardMode';
import { EventLog } from './EventLog';
import { QuickActions } from './QuickActions';
import { ScoreboardCompact } from './ScoreboardCompact';
import { TurnIndicator } from './TurnIndicator';

import type { EventLogItem } from './EventLog';
import type { ScoreboardPlayer } from './ScoreboardCompact';

interface TavoloViewProps {
  sessionId: string;
}

// Placeholder data — will be replaced by SSE session stream integration
const EMPTY_PLAYERS: ScoreboardPlayer[] = [];
const EMPTY_EVENTS: EventLogItem[] = [];

export function TavoloView({ sessionId: _sessionId }: TavoloViewProps) {
  // send is used in openSheet; will be wired to OPEN_SHEET event in Task 6
  const { send: _send } = useDashboardMode();

  // openSheet will be wired to the SessionSheet in a later task.

  function openSheet(_sheet: string) {
    // TODO: wire to OPEN_SHEET event once SessionSheet is implemented (Task 6)
  }

  return (
    <div data-testid="tavolo-view" className="flex flex-col gap-4 px-4 py-3">
      {/* Scoreboard */}
      <ScoreboardCompact players={EMPTY_PLAYERS} />

      {/* Turn indicator */}
      <TurnIndicator playerName="—" playerColor="#888888" />

      {/* Quick actions */}
      <QuickActions onAddScore={() => openSheet('add-score')} onAskAi={() => openSheet('ask-ai')} />

      {/* Event log */}
      <EventLog events={EMPTY_EVENTS} />
    </div>
  );
}
