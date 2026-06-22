'use client';

/**
 * LiveSessionPill — runtime backfill of nav-chrome primitive D5 (Issue #2150).
 *
 * Renders an entity-typed chip in the AppTopBar when a live game session is
 * active. Provides:
 *   - quick visual reminder that a session is in progress (entity color +
 *     game name + elapsed timer)
 *   - "Resume" CTA to jump back to `/sessions/[id]/live`
 *
 * Hybrid backfill rationale (#2150 decision):
 *   - The `primitive-nav-topbar.html` mockup documents a LiveSessionPill on
 *     the topbar; the runtime AppTopBar was missing it.
 *   - Rather than regenerating 4 page-mocks to also document the pill
 *     (Option B full backfill), we promote the runtime to feature-complete
 *     and keep the page-mock convention of "no chrome shown" (Option A).
 *   - The `primitive-nav-chat-panel.html` (D7) primitive is concurrently
 *     marked `forward-refactor-obsolete` because there is no demand evidence.
 *
 * Source of truth for live state: `useLiveSessionStore` (Zustand,
 * driven by SignalR `GameStateHub` events).
 */

import Link from 'next/link';

import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { cn } from '@/lib/utils';

interface LiveSessionPillProps {
  className?: string;
}

/**
 * Format `elapsedSeconds` as `mm:ss` (< 1h) or `h:mm` (>= 1h).
 * Negative/null → `0:00`.
 */
function formatElapsed(seconds: number): string {
  if (!Number.isFinite(seconds) || seconds <= 0) return '0:00';
  const total = Math.floor(seconds);
  const h = Math.floor(total / 3600);
  const m = Math.floor((total % 3600) / 60);
  const s = total % 60;
  if (h > 0) return `${h}:${String(m).padStart(2, '0')}`;
  return `${m}:${String(s).padStart(2, '0')}`;
}

export function LiveSessionPill({ className }: LiveSessionPillProps) {
  const sessionId = useLiveSessionStore(s => s.sessionId);
  const gameName = useLiveSessionStore(s => s.gameName);
  const status = useLiveSessionStore(s => s.status);
  const elapsedSeconds = useLiveSessionStore(s => s.elapsedSeconds);

  // No active session → render nothing. The topbar spacer collapses naturally.
  if (!sessionId || status === 'Completed') return null;

  const isPaused = status === 'Paused';
  const elapsedLabel = formatElapsed(elapsedSeconds);
  const displayName = gameName?.trim().length > 0 ? gameName : 'Sessione';

  return (
    <Link
      href={`/sessions/${sessionId}/live`}
      data-testid="live-session-pill"
      data-slot="live-session-pill"
      data-paused={isPaused ? 'true' : undefined}
      aria-label={
        isPaused
          ? `Sessione ${displayName} in pausa, ${elapsedLabel} trascorsi. Riprendi.`
          : `Sessione ${displayName} attiva, ${elapsedLabel} trascorsi. Riprendi.`
      }
      className={cn(
        'group inline-flex shrink-0 items-center gap-2 rounded-full border px-3 py-1.5',
        'text-[12px] font-bold transition-colors',
        'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
        // Two visual states: live (pulse + entity-game) vs paused (muted + amber).
        isPaused
          ? 'border-amber-500/40 bg-amber-500/10 text-amber-700 hover:bg-amber-500/15'
          : 'border-entity-game/40 bg-entity-game/10 text-entity-game-text hover:bg-entity-game/15',
        className
      )}
    >
      <span aria-hidden="true" className="text-base leading-none">
        🎲
      </span>
      <span className="max-w-[140px] truncate" data-slot="live-session-pill-name">
        {displayName}
      </span>
      <span
        aria-hidden="true"
        className={cn(
          'inline-flex items-center gap-0.5 font-mono text-[11px] tabular-nums',
          isPaused ? 'opacity-80' : 'opacity-90'
        )}
        data-slot="live-session-pill-elapsed"
      >
        <span className="text-[10px] leading-none">⏱</span>
        {elapsedLabel}
      </span>
      {isPaused && (
        <span
          className="rounded-full bg-amber-500/20 px-1.5 py-0.5 text-[9px] uppercase tracking-wide"
          data-slot="live-session-pill-status"
        >
          Pausa
        </span>
      )}
    </Link>
  );
}
