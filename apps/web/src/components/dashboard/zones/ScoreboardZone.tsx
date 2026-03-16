/**
 * ScoreboardZone — Dashboard zone for game mode.
 *
 * Wraps the existing Scoreboard component with:
 * - Zone-specific layout (padding, max-width, responsive)
 * - Suspense boundary with skeleton fallback
 * - Session data from useSessionStore()
 *
 * Shows an empty state when no active session is present.
 */

'use client';

import { Suspense } from 'react';

import { Trophy } from 'lucide-react';

import { toScoreboardData } from '@/components/session/adapters';
import { Scoreboard } from '@/components/session/Scoreboard';
import { useSessionStore } from '@/lib/stores/sessionStore';

// ---------------------------------------------------------------------------
// Skeleton fallback for Suspense boundary
// ---------------------------------------------------------------------------

function ScoreboardSkeleton() {
  return (
    <div className="space-y-4" aria-label="Loading scoreboard">
      {/* Header skeleton */}
      <div className="flex items-center gap-3">
        <div className="h-5 w-5 animate-pulse rounded bg-muted" />
        <div className="h-5 w-32 animate-pulse rounded bg-muted" />
      </div>
      {/* Player card skeletons */}
      {[1, 2, 3].map(i => (
        <div
          key={i}
          className="flex h-16 items-center gap-4 rounded-xl border border-border bg-card p-4"
        >
          <div className="h-10 w-10 animate-pulse rounded-lg bg-muted" />
          <div className="flex-1 space-y-2">
            <div className="h-4 w-28 animate-pulse rounded bg-muted" />
            <div className="h-3 w-16 animate-pulse rounded bg-muted" />
          </div>
          <div className="h-6 w-12 animate-pulse rounded bg-muted" />
        </div>
      ))}
    </div>
  );
}

// ---------------------------------------------------------------------------
// Empty state when no active session
// ---------------------------------------------------------------------------

function NoSessionState() {
  return (
    <div
      data-testid="scoreboard-zone-empty"
      className="flex flex-col items-center gap-3 py-12 text-center text-muted-foreground"
    >
      <Trophy className="h-10 w-10 opacity-30" />
      <p className="text-sm font-medium">No active session</p>
      <p className="text-xs">Start or join a game session to see the scoreboard.</p>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Scoreboard content — reads from session store
// ---------------------------------------------------------------------------

function ScoreboardContent() {
  const activeSession = useSessionStore(s => s.activeSession);
  const scores = useSessionStore(s => s.scores);

  if (!activeSession) {
    return <NoSessionState />;
  }

  const scoreboardData = toScoreboardData(activeSession, scores, null);

  return <Scoreboard data={scoreboardData} isRealTime variant="full" />;
}

// ---------------------------------------------------------------------------
// ScoreboardZone (public)
// ---------------------------------------------------------------------------

export function ScoreboardZone() {
  return (
    <div
      data-testid="scoreboard-zone"
      className="mx-auto w-full max-w-2xl px-4 py-6 sm:px-6 lg:px-8"
    >
      <Suspense fallback={<ScoreboardSkeleton />}>
        <ScoreboardContent />
      </Suspense>
    </div>
  );
}
