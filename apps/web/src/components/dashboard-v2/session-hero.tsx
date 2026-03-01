/**
 * DashboardSessionHero — Issue #5095, Epic #5094
 *
 * Full-width hero card for the dashboard:
 * - ACTIVE state: gradient card with live session info + "Riprendi" CTA
 * - EMPTY state: dashed card with "Nuova sessione" / "Riprendi ultima" CTAs
 */

'use client';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { Play, RotateCcw } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useGame } from '@/hooks/queries/useGames';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';
import type { SessionSummaryDto } from '@/lib/api/dashboard-client';

// ─── Helpers ────────────────────────────────────────────────────────────────

function formatDuration(minutes: number): string {
  if (minutes <= 0) return '';
  const h = Math.floor(minutes / 60);
  const m = minutes % 60;
  if (h > 0) return `${h}h ${m > 0 ? `${m}m` : ''}`.trim();
  return `${m}m`;
}

// ─── Active hero ─────────────────────────────────────────────────────────────

function ActiveSessionHero({ session }: { session: GameSessionDto }) {
  const { data: game } = useGame(session.gameId);

  const gameTitle = game?.title ?? 'Sessione in corso';
  const duration = formatDuration(session.durationMinutes);
  const meta = [
    `${session.playerCount} giocatori`,
    duration ? `Da ${duration}` : null,
  ]
    .filter(Boolean)
    .join(' · ');

  return (
    <div
      className="relative flex items-center gap-4 rounded-2xl px-6 py-5 overflow-hidden"
      style={{
        background:
          'linear-gradient(135deg, hsl(240,60%,55%), hsl(240,60%,38%))',
      }}
    >
      {/* Diagonal stripe overlay */}
      <div
        aria-hidden
        className="pointer-events-none absolute inset-0"
        style={{
          background:
            'repeating-linear-gradient(-45deg, transparent, transparent 12px, rgba(255,255,255,.04) 12px, rgba(255,255,255,.04) 13px)',
        }}
      />

      {/* Live dot */}
      <span
        className="relative z-10 shrink-0 rounded-full"
        style={{
          width: 10,
          height: 10,
          background: '#4ade80',
          boxShadow: '0 0 0 4px rgba(74,222,128,.3)',
        }}
      />

      {/* Content */}
      <div className="relative z-10 flex-1 min-w-0">
        <p className="text-[11px] font-bold font-quicksand uppercase tracking-widest text-white/75">
          🟢 Sessione attiva
        </p>
        <h2 className="font-quicksand text-lg font-bold text-white truncate mt-0.5">
          {gameTitle}
        </h2>
        {meta && (
          <p className="text-xs text-white/70 font-nunito mt-0.5">{meta}</p>
        )}
      </div>

      {/* CTA */}
      <Link
        href={`/sessions/${session.id}`}
        className="relative z-10 shrink-0"
      >
        <Button
          size="sm"
          className="font-quicksand font-bold bg-white text-[hsl(240,60%,55%)] hover:bg-white/90 hover:-translate-y-px transition-transform"
        >
          Riprendi →
        </Button>
      </Link>
    </div>
  );
}

// ─── Empty hero ───────────────────────────────────────────────────────────────

function EmptySessionHero({
  lastSession,
}: {
  lastSession?: SessionSummaryDto;
}) {
  const lastGameName = lastSession?.gameName;
  const lastDate = lastSession?.sessionDate
    ? formatDistanceToNow(new Date(lastSession.sessionDate), {
        addSuffix: true,
        locale: it,
      })
    : null;

  return (
    <div className="flex items-center gap-4 rounded-2xl px-6 py-5 border-2 border-dashed border-border bg-surface">
      {/* Icon */}
      <span className="text-4xl shrink-0 opacity-40" aria-hidden>
        🎲
      </span>

      {/* Content */}
      <div className="flex-1 min-w-0">
        <p className="text-xs font-bold font-quicksand uppercase tracking-widest text-muted-foreground">
          Nessuna sessione in corso
        </p>
        <h2 className="font-quicksand text-base font-bold text-foreground mt-0.5">
          Pronto a giocare?
        </h2>
        {lastGameName && lastDate && (
          <p className="text-xs text-muted-foreground font-nunito mt-0.5">
            Ultima sessione: {lastGameName}, {lastDate}
          </p>
        )}
      </div>

      {/* CTAs */}
      <div className="flex gap-2 shrink-0 flex-wrap justify-end">
        <Button
          size="sm"
          className="font-quicksand font-bold gap-1.5"
          style={{
            background: 'hsl(240,60%,55%)',
            color: '#fff',
          }}
          asChild
        >
          <Link href="/sessions/new">
            <Play className="h-3.5 w-3.5" />
            Nuova sessione
          </Link>
        </Button>
        {lastSession && (
          <Button
            variant="outline"
            size="sm"
            className="font-quicksand font-bold gap-1.5"
            asChild
          >
            <Link href="/sessions">
              <RotateCcw className="h-3.5 w-3.5" />
              Riprendi ultima
            </Link>
          </Button>
        )}
      </div>
    </div>
  );
}

// ─── Loading skeleton ─────────────────────────────────────────────────────────

function SessionHeroSkeleton() {
  return <Skeleton className="h-[82px] w-full rounded-2xl" />;
}

// ─── Public component ─────────────────────────────────────────────────────────

interface DashboardSessionHeroProps {
  /** Last session from store — used for "Riprendi ultima" fallback */
  lastSession?: SessionSummaryDto;
}

export function DashboardSessionHero({ lastSession }: DashboardSessionHeroProps) {
  const { data, isLoading } = useActiveSessions(1);

  if (isLoading) return <SessionHeroSkeleton />;

  const activeSession = data?.sessions?.[0];

  if (activeSession) {
    return <ActiveSessionHero session={activeSession} />;
  }

  return <EmptySessionHero lastSession={lastSession} />;
}
