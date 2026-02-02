/**
 * ActiveSessionsWidget - Dashboard Widget for Active Game Sessions
 * Issue #3309 - Implement ActiveSessionsWidget
 *
 * Features:
 * - Shows up to 2 most recent active sessions
 * - Displays game name, date, players, turn, duration
 * - "Continue" button navigates to session
 * - "View All" link to sessions history
 * - Empty state with "Start New Game" CTA
 * - Loading skeleton state
 *
 * @example
 * ```tsx
 * <ActiveSessionsWidget />
 * ```
 */

'use client';

import { useMemo } from 'react';
import { motion } from 'framer-motion';
import {
  Dices,
  PlayCircle,
  Users,
  Clock,
  ChevronRight,
  Plus,
} from 'lucide-react';
import Link from 'next/link';

import { cn } from '@/lib/utils';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';

// ============================================================================
// Types
// ============================================================================

export interface ActiveSession {
  id: string;
  gameName: string;
  gameId: string;
  startDate: string;
  players: {
    current: number;
    max: number;
  };
  turn: number;
  duration: number; // minutes
}

export interface ActiveSessionsWidgetProps {
  /** Active sessions data */
  sessions?: ActiveSession[];
  /** Total count of active sessions */
  totalCount?: number;
  /** Loading state */
  isLoading?: boolean;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Mock Data (for development)
// ============================================================================

const MOCK_SESSIONS: ActiveSession[] = [
  {
    id: 'session-1',
    gameName: 'Catan',
    gameId: 'game-1',
    startDate: new Date(Date.now() - 1000 * 60 * 60 * 24).toISOString(),
    players: { current: 3, max: 4 },
    turn: 12,
    duration: 45,
  },
  {
    id: 'session-2',
    gameName: 'Ticket to Ride',
    gameId: 'game-2',
    startDate: new Date(Date.now() - 1000 * 60 * 60 * 24 * 2).toISOString(),
    players: { current: 2, max: 5 },
    turn: 8,
    duration: 30,
  },
];

// ============================================================================
// Helper Functions
// ============================================================================

function formatSessionDate(isoDate: string): string {
  const date = new Date(isoDate);
  return date.toLocaleDateString('it-IT', {
    day: '2-digit',
    month: '2-digit',
  });
}

// ============================================================================
// Skeleton Component
// ============================================================================

function ActiveSessionsWidgetSkeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
        className
      )}
      data-testid="active-sessions-widget-skeleton"
    >
      {/* Header Skeleton */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <Skeleton className="h-5 w-5 rounded" />
          <Skeleton className="h-5 w-32" />
        </div>
        <Skeleton className="h-4 w-20" />
      </div>

      {/* Session Cards Skeleton */}
      <div className="space-y-3">
        {Array.from({ length: 2 }).map((_, i) => (
          <div
            key={i}
            className="flex items-center justify-between p-3 rounded-xl border border-border/40 bg-muted/30"
          >
            <div className="flex-1 space-y-2">
              <Skeleton className="h-4 w-40" />
              <Skeleton className="h-3 w-32" />
            </div>
            <Skeleton className="h-8 w-24 rounded-full" />
          </div>
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// Session Card Component
// ============================================================================

function SessionCard({ session }: { session: ActiveSession }) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      className="flex items-center justify-between p-3 rounded-xl border border-border/40 bg-gradient-to-r from-emerald-500/5 to-emerald-600/5 hover:from-emerald-500/10 hover:to-emerald-600/10 transition-colors"
      data-testid={`session-card-${session.id}`}
    >
      <div className="flex-1 min-w-0">
        {/* Game Name & Date */}
        <div className="flex items-center gap-2">
          <Dices className="h-4 w-4 text-emerald-600 dark:text-emerald-400 shrink-0" />
          <p
            className="font-medium text-sm truncate"
            data-testid={`session-name-${session.id}`}
          >
            {session.gameName}
          </p>
          <span className="text-xs text-muted-foreground shrink-0">
            - Partita del {formatSessionDate(session.startDate)}
          </span>
        </div>

        {/* Session Details */}
        <div className="flex items-center gap-3 mt-1 text-xs text-muted-foreground">
          <span className="flex items-center gap-1" data-testid={`session-players-${session.id}`}>
            <Users className="h-3 w-3" />
            {session.players.current}/{session.players.max} giocatori
          </span>
          <span data-testid={`session-turn-${session.id}`}>
            Turno {session.turn}
          </span>
          <span className="flex items-center gap-1" data-testid={`session-duration-${session.id}`}>
            <Clock className="h-3 w-3" />
            {session.duration}min
          </span>
        </div>
      </div>

      {/* Continue Button */}
      <Link href={`/toolkit/${session.id}`}>
        <Button
          size="sm"
          variant="default"
          className="shrink-0 rounded-full bg-emerald-600 hover:bg-emerald-700 text-white"
          data-testid={`session-continue-${session.id}`}
        >
          <PlayCircle className="h-4 w-4 mr-1" />
          Continua
        </Button>
      </Link>
    </motion.div>
  );
}

// ============================================================================
// Empty State Component
// ============================================================================

function EmptyState() {
  return (
    <div
      className="flex flex-col items-center justify-center py-6 text-center"
      data-testid="active-sessions-empty"
    >
      <div className="h-12 w-12 rounded-full bg-muted/50 flex items-center justify-center mb-3">
        <Dices className="h-6 w-6 text-muted-foreground/50" />
      </div>
      <p className="text-sm text-muted-foreground mb-3">
        Nessuna sessione attiva
      </p>
      <Link href="/toolkit/new">
        <Button
          size="sm"
          variant="outline"
          className="rounded-full"
          data-testid="start-new-session-cta"
        >
          <Plus className="h-4 w-4 mr-1" />
          Inizia Nuova Partita
        </Button>
      </Link>
    </div>
  );
}

// ============================================================================
// ActiveSessionsWidget Component
// ============================================================================

export function ActiveSessionsWidget({
  sessions = MOCK_SESSIONS,
  totalCount,
  isLoading = false,
  className,
}: ActiveSessionsWidgetProps) {
  const displaySessions = useMemo(() => sessions.slice(0, 2), [sessions]);
  const total = totalCount ?? sessions.length;
  const hasMore = total > 2;

  if (isLoading) {
    return <ActiveSessionsWidgetSkeleton className={className} />;
  }

  return (
    <section
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
        className
      )}
      data-testid="active-sessions-widget"
    >
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <div className="h-8 w-8 rounded-lg bg-emerald-500/20 flex items-center justify-center">
            <PlayCircle className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
          </div>
          <h3
            className="font-semibold text-sm"
            data-testid="active-sessions-title"
          >
            {total > 0 ? `${total} Session${total > 1 ? 'i' : 'e'} Attiv${total > 1 ? 'e' : 'a'}` : 'Sessioni Attive'}
          </h3>
        </div>

        {hasMore && (
          <Link
            href="/toolkit/history"
            className="text-xs text-muted-foreground hover:text-foreground flex items-center gap-1 transition-colors"
            data-testid="view-all-sessions"
          >
            Vedi Tutte
            <ChevronRight className="h-3 w-3" />
          </Link>
        )}
      </div>

      {/* Content */}
      {displaySessions.length === 0 ? (
        <EmptyState />
      ) : (
        <div className="space-y-3">
          {displaySessions.map((session, index) => (
            <motion.div
              key={session.id}
              initial={{ opacity: 0, y: 10 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{ delay: index * 0.1 }}
            >
              <SessionCard session={session} />
            </motion.div>
          ))}
        </div>
      )}
    </section>
  );
}

export default ActiveSessionsWidget;
