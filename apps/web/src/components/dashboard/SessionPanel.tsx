'use client';

import Link from 'next/link';

import { useCascadeNavigationStore } from '@/lib/stores/cascadeNavigationStore';
import { useSessionStore } from '@/lib/stores/sessionStore';

import { useSessionSlot } from './useSessionSlot';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const MANA_PIPS = [
  { entityType: 'kb' as const, label: 'KB', color: 'hsl(174 60% 40%)' },
  { entityType: 'agent' as const, label: 'Agent', color: 'hsl(38 92% 50%)' },
  { entityType: 'player' as const, label: 'Players', color: 'hsl(262 83% 58%)' },
] as const;

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

function LiveDot() {
  return (
    <span
      className="live-dot inline-block h-2 w-2 rounded-full bg-green-500"
      aria-label="Live session"
    />
  );
}

function MiniScoreboard({ players }: { players: Array<{ name: string; score: number }> }) {
  if (players.length === 0) return null;

  return (
    <div className="flex flex-col gap-1 text-sm" data-testid="mini-scoreboard">
      {players.map((p, i) => (
        <div key={p.name} className="flex items-center justify-between gap-2">
          <span className="flex items-center gap-1 min-w-0">
            <span className="shrink-0 text-xs">{i === 0 ? '🥇' : i === 1 ? '🥈' : '🥉'}</span>
            <span className="truncate text-foreground">{p.name}</span>
          </span>
          <span className="font-mono text-xs text-muted-foreground">{p.score}</span>
        </div>
      ))}
    </div>
  );
}

function ManaPipRow({ sessionId }: { sessionId: string }) {
  const openDeckStack = useCascadeNavigationStore(s => s.openDeckStack);

  return (
    <div className="flex items-center gap-2" data-testid="mana-pip-row">
      {MANA_PIPS.map(({ entityType, label, color }) => (
        <button
          key={entityType}
          type="button"
          aria-label={`Open ${label}`}
          className="h-6 w-6 rounded-full border-2 border-white/20 transition-transform hover:scale-110 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
          style={{ backgroundColor: color }}
          data-testid={`mana-pip-${entityType}`}
          onClick={() => openDeckStack(entityType, sessionId)}
        />
      ))}
    </div>
  );
}

// ---------------------------------------------------------------------------
// SessionPanel (expanded — 180px CardStack slot)
// ---------------------------------------------------------------------------

/**
 * Full expanded session panel shown inside the CardStack when expanded (180px).
 *
 * Contains: game header with live dot, mini-scoreboard (top 3),
 * mana pip row, quick actions (pause/resume + chat), and scoreboard link.
 */
export function SessionPanel() {
  const slot = useSessionSlot();
  const pauseSession = useSessionStore(s => s.pauseSession);
  const resumeSession = useSessionStore(s => s.resumeSession);

  if (!slot.isVisible) return null;

  const isPaused = slot.sessionStatus === 'paused';

  return (
    <section
      data-testid="session-panel"
      className="session-panel-enter w-full rounded-2xl bg-background/90 backdrop-blur-xl border border-indigo-500/30 shadow-lg p-4 flex flex-col gap-3"
    >
      {/* Header: game name + live dot + duration */}
      <div className="flex items-center gap-2">
        <span className="font-quicksand font-semibold text-foreground truncate max-w-[180px]">
          {slot.gameName}
        </span>
        <LiveDot />
        <span className="ml-auto text-xs text-muted-foreground font-mono">{slot.duration}min</span>
      </div>

      {/* Mini scoreboard */}
      <MiniScoreboard players={slot.players} />

      {/* Mana pip row */}
      <ManaPipRow sessionId={slot.sessionId} />

      {/* Quick actions */}
      <div className="flex items-center gap-2">
        <button
          type="button"
          data-testid="session-pause-resume"
          className="flex-1 rounded-lg border border-white/10 bg-white/5 px-3 py-1.5 text-xs font-medium text-foreground hover:bg-white/10 transition-colors"
          onClick={() => (isPaused ? resumeSession() : pauseSession())}
        >
          {isPaused ? 'Resume' : 'Pause'}
        </button>
        <Link
          href={`/sessions/${slot.sessionId}/chat`}
          className="flex-1 rounded-lg border border-white/10 bg-white/5 px-3 py-1.5 text-xs font-medium text-foreground hover:bg-white/10 transition-colors text-center"
          data-testid="session-chat-agent"
        >
          Chat Agent
        </Link>
      </div>

      {/* Footer link */}
      <Link
        href={`/sessions/${slot.sessionId}`}
        className="text-xs text-indigo-400 hover:text-indigo-300 transition-colors text-center"
        data-testid="session-scoreboard-link"
      >
        Vai allo scoreboard →
      </Link>
    </section>
  );
}
