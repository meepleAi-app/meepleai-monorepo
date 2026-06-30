'use client';

/**
 * AgentDisputeTabContent — #2588 A4 dispute-tab backport.
 *
 * Renders the "Arbitro" tab content for the canonical session-live view.
 * Hosts ArbitroModal (open-dispute CTA + form) and DisputeHistory (past verdicts).
 *
 * Dispute hydration: `useSignalRSession` is mounted at the SessionLiveView
 * orchestrator level (NOT here) so the SignalR connection persists across tab
 * changes and populates `useLiveSessionStore.disputes` in real-time.
 *
 * Known limitations:
 *   - No REST hydration on mount: disputes are SignalR-only (lost on reload).
 *   - Dual transport: disputes come via SignalR; rest of live state via SSE.
 *
 * @see ArbitroModal  — self-contained, POSTs to legacy endpoint, owns verdict UX.
 * @see DisputeHistory — reads disputes from useLiveSessionStore.
 * @see useSignalRSession — wired in SessionLiveView, populates store on 'DisputeResolved'.
 */

import type { ReactElement } from 'react';

import { ArbitroModal } from '@/components/session/live/ArbitroModal';
import { DisputeHistory } from '@/components/session/live/DisputeHistory';

// ─── Props ────────────────────────────────────────────────────────────────────

export interface AgentDisputeTabContentProps {
  /** Canonical live-session id (LiveGameSession.id). */
  readonly sessionId: string;
  /** Players for the ArbitroModal "raised-by" selector. */
  readonly players: ReadonlyArray<{ id?: string; name: string }>;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function AgentDisputeTabContent({
  sessionId,
  players,
}: AgentDisputeTabContentProps): ReactElement {
  return (
    <div data-slot="agent-dispute-tab-content" className="flex flex-col gap-4 p-3">
      {/* Open-dispute CTA — ArbitroModal self-manages open/close state.
          Spread converts ReadonlyArray → mutable Array as required by ArbitroModal prop. */}
      <ArbitroModal sessionId={sessionId} players={[...players]} />

      {/* Past verdicts — reads from useLiveSessionStore.disputes */}
      <DisputeHistory sessionId={sessionId} />
    </div>
  );
}
