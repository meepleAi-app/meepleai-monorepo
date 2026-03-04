/**
 * DashboardSessionHero — Issue #5095, Epic #5094
 *
 * Full-width hero card for the dashboard:
 * - ACTIVE state: warm amber gradient with live session info + "Riprendi" CTA
 * - EMPTY state: warm dashed card with "Nuova sessione" / "Riprendi ultima" CTAs
 */

'use client';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { Dices, Play, RotateCcw } from 'lucide-react';
import Link from 'next/link';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useActiveSessions } from '@/hooks/queries/useActiveSessions';
import { useGame } from '@/hooks/queries/useGames';
import type { SessionSummaryDto } from '@/lib/api/dashboard-client';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

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
  const meta = [`${session.playerCount} giocatori`, duration ? `Da ${duration}` : null]
    .filter(Boolean)
    .join(' · ');

  return (
    <div
      className="relative flex items-center gap-4 rounded-2xl px-6 py-5 overflow-hidden shadow-[0_8px_32px_rgba(180,100,30,0.18)]"
      style={{
        background: 'linear-gradient(135deg, hsl(25,90%,48%), hsl(25,70%,28%))',
      }}
    >
      {/* Diagonal stripe overlay */}
      <div
        aria-hidden
        className="pointer-events-none absolute inset-0"
        style={{
          background:
            'repeating-linear-gradient(-45deg, transparent, transparent 12px, rgba(255,255,255,.05) 12px, rgba(255,255,255,.05) 13px)',
        }}
      />

      {/* Subtle noise grain */}
      <div
        aria-hidden
        className="pointer-events-none absolute inset-0 opacity-[0.03]"
        style={{
          backgroundImage: `url("data:image/svg+xml,%3Csvg viewBox='0 0 256 256' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='n'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='4' stitchTiles='stitch'/%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23n)'/%3E%3C/svg%3E")`,
        }}
      />

      {/* Live dot */}
      <span
        className="relative z-10 shrink-0 rounded-full animate-pulse"
        style={{
          width: 10,
          height: 10,
          background: '#4ade80',
          boxShadow: '0 0 0 4px rgba(74,222,128,.3)',
        }}
      />

      {/* Content */}
      <div className="relative z-10 flex-1 min-w-0">
        <p className="text-[11px] font-bold font-quicksand uppercase tracking-widest text-white/80">
          Sessione attiva
        </p>
        <h2 className="font-quicksand text-lg font-bold text-white truncate mt-0.5">{gameTitle}</h2>
        {meta && <p className="text-xs text-white/70 font-nunito mt-0.5">{meta}</p>}
      </div>

      {/* CTA */}
      <Link href={`/sessions/${session.id}`} className="relative z-10 shrink-0">
        <Button
          size="sm"
          className="font-quicksand font-bold bg-white text-[hsl(25,85%,35%)] hover:bg-white/90 shadow-md hover:shadow-lg hover:-translate-y-px transition-all duration-200"
        >
          Riprendi →
        </Button>
      </Link>
    </div>
  );
}

// ─── Empty hero ───────────────────────────────────────────────────────────────

function EmptySessionHero({ lastSession }: { lastSession?: SessionSummaryDto }) {
  const lastGameName = lastSession?.gameName;
  const lastDate = lastSession?.sessionDate
    ? formatDistanceToNow(new Date(lastSession.sessionDate), {
        addSuffix: true,
        locale: it,
      })
    : null;

  return (
    <div className="flex items-center gap-4 rounded-2xl px-6 py-5 border-2 border-dashed border-[hsl(25,40%,80%)] dark:border-[hsl(25,30%,30%)] bg-[rgba(255,245,235,0.5)] dark:bg-[rgba(40,30,20,0.4)]">
      {/* Icon */}
      <span className="shrink-0 opacity-50" aria-hidden>
        <Dices className="h-9 w-9 text-[hsl(25,60%,50%)]" />
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
          className="font-quicksand font-bold gap-1.5 bg-[hsl(25,90%,45%)] hover:bg-[hsl(25,90%,40%)] text-white shadow-sm"
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
            className="font-quicksand font-bold gap-1.5 border-[hsl(25,40%,75%)] text-[hsl(25,70%,35%)] hover:bg-[hsl(25,95%,95%)]"
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
