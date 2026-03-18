'use client';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useGame } from '@/hooks/queries/useGames';
import { useCascadeNavigationStore } from '@/lib/stores/cascadeNavigationStore';
import { useSessionStore } from '@/lib/stores/sessionStore';

// ---------------------------------------------------------------------------
// Constants
// ---------------------------------------------------------------------------

const MANA_PIPS = [
  { entityType: 'kb' as const, label: 'KB', color: 'hsl(174 60% 40%)' },
  { entityType: 'agent' as const, label: 'Agent', color: 'hsl(38 92% 50%)' },
  { entityType: 'player' as const, label: 'Players', color: 'hsl(262 83% 58%)' },
] as const;

// ---------------------------------------------------------------------------
// Skeleton
// ---------------------------------------------------------------------------

function SessionBarSkeleton() {
  return (
    <div
      data-testid="session-bar-skeleton"
      className="dashboard-hero w-full rounded-2xl bg-background/85 backdrop-blur-xl border border-white/10 shadow-lg p-4 animate-pulse"
    >
      <div className="flex items-center gap-3">
        <Skeleton className="h-8 w-8 rounded-lg" />
        <Skeleton className="h-5 w-32" />
        <Skeleton className="h-3 w-3 rounded-full ml-2" />
        <div className="ml-auto flex gap-2">
          <Skeleton className="h-6 w-6 rounded-full" />
          <Skeleton className="h-6 w-6 rounded-full" />
          <Skeleton className="h-6 w-6 rounded-full" />
        </div>
      </div>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

function LiveDot() {
  return (
    <span
      className="live-dot inline-block h-2.5 w-2.5 rounded-full bg-green-500"
      aria-label="Live session"
    />
  );
}

function LeaderScores({
  players,
}: {
  players: Array<{ displayName: string; totalScore: number; currentRank: number }>;
}) {
  const top3 = [...players].sort((a, b) => a.currentRank - b.currentRank).slice(0, 3);

  if (top3.length === 0) return null;

  return (
    <div
      className="hidden md:flex items-center gap-3 text-sm text-muted-foreground"
      data-testid="leader-scores"
    >
      {top3.map((p, i) => (
        <span key={p.displayName} className="flex items-center gap-1">
          <span className="font-medium text-foreground">
            {i === 0 ? '🥇' : i === 1 ? '🥈' : '🥉'}
          </span>
          <span className="truncate max-w-[80px]">{p.displayName}</span>
          <span className="font-mono text-xs">{p.totalScore}</span>
        </span>
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
// SessionBar
// ---------------------------------------------------------------------------

export function SessionBar() {
  const activeSession = useSessionStore(s => s.activeSession);
  const isLoading = useSessionStore(s => s.isLoading);

  // Fetch game details when session has a gameId
  const gameId = activeSession?.gameId ?? undefined;
  const { data: gameData } = useGame(gameId ?? '', !!gameId);

  if (isLoading && !activeSession) {
    return <SessionBarSkeleton />;
  }

  if (!activeSession) {
    return null;
  }

  const gameName = gameData?.title ?? activeSession.gameName;

  return (
    <section
      data-testid="session-bar"
      className="dashboard-hero w-full rounded-2xl bg-background/85 backdrop-blur-xl border border-white/10 shadow-lg p-4"
    >
      <div className="flex items-center gap-3">
        {/* Game icon + name */}
        <div className="flex items-center gap-2 min-w-0">
          <div
            className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg text-lg"
            style={{ backgroundColor: 'hsl(240 60% 55% / 0.15)' }}
          >
            🎲
          </div>
          <span className="font-quicksand font-semibold text-foreground truncate max-w-[160px] md:max-w-[240px]">
            {gameName}
          </span>
        </div>

        {/* Live dot */}
        <LiveDot />

        {/* Leader scores (desktop only) */}
        <LeaderScores players={activeSession.players} />

        {/* Spacer */}
        <div className="flex-1" />

        {/* Mana pip row */}
        <ManaPipRow sessionId={activeSession.id} />
      </div>
    </section>
  );
}
