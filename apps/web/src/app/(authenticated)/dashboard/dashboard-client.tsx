'use client';

/**
 * Dashboard Bento — Layout con 6 widget
 *
 * Desktop: 12-col bento grid con widget variabili
 * - LiveSession (8×2), KPI Stats (4×2)
 * - Library (6×4), Chat (6×4)
 * - Leaderboard (6×3), Trending (6×3)
 *
 * First-run (totalGames=0 && no recent sessions):
 * - WelcomeHero (12×3) al posto di LiveSession+KPI
 * - Library empty CTA, Trending reale, Chat empty CTA, Sessions empty CTA
 */

import { useEffect } from 'react';

import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { StatsRow } from '@/components/dashboard/StatsRow';
import { WelcomeHero } from '@/components/dashboard/WelcomeHero';
import { FloatingActionPill } from '@/components/layout/FloatingActionPill';
import type { SessionSummaryDto, TrendingGameDto, UserGameDto } from '@/lib/api/dashboard-client';
import { useDashboardStore } from '@/lib/stores/dashboard-store';
import { cn } from '@/lib/utils';

// ─── Entity color tokens (CSS custom properties) ──────────────────────────────

const C = {
  game: 'hsl(var(--e-game))',
  player: 'hsl(var(--e-player))',
  session: 'hsl(var(--e-session))',
  chat: 'hsl(var(--e-chat))',
  kb: 'hsl(var(--e-kb))',
  event: 'hsl(var(--e-event))',
  success: 'hsl(var(--e-success))',
} as const;

// ─── Helpers ─────────────────────────────────────────────────────────────────

function parsePlayTimeHours(timeSpan: string): string {
  if (!timeSpan) return '—';
  const parts = timeSpan.split(':');
  const hours = parseInt(parts[0] ?? '0', 10);
  const minutes = parseInt(parts[1] ?? '0', 10);
  if (hours === 0) return `${minutes}m`;
  return `${hours}h`;
}

// ─── Bento Widget ─────────────────────────────────────────────────────────────

// Tailwind col-span classes (explicit strings for Tailwind v4 scanning).
// tablet col-span (tc) is always ≤ 6 via Math.min in BentoWidget, so only 2–6 needed here.
const COL_SPAN: Record<number, string> = {
  2: 'col-span-2',
  3: 'col-span-3',
  4: 'col-span-4',
  6: 'col-span-6',
};
const LG_COL_SPAN: Record<number, string> = {
  2: 'lg:col-span-2',
  3: 'lg:col-span-3',
  4: 'lg:col-span-4',
  6: 'lg:col-span-6',
  8: 'lg:col-span-8',
  12: 'lg:col-span-12',
};
const ROW_SPAN: Record<number, string> = {
  1: 'row-span-1',
  2: 'row-span-2',
  3: 'row-span-3',
  4: 'row-span-4',
  5: 'row-span-5',
  6: 'row-span-6',
};

interface BentoWidgetProps {
  colSpan: number;
  /** col-span on the 6-col tablet grid. Defaults to min(colSpan, 6). */
  tabletColSpan?: number;
  rowSpan: number;
  accentColor?: string;
  accentBg?: string;
  className?: string;
  children: React.ReactNode;
  onClick?: () => void;
}

function BentoWidget({
  colSpan,
  tabletColSpan,
  rowSpan,
  accentColor,
  accentBg,
  className,
  children,
  onClick,
}: BentoWidgetProps) {
  const tc = tabletColSpan ?? Math.min(colSpan, 6);
  return (
    <div
      className={cn(
        // Responsive grid spans
        COL_SPAN[tc] ?? `col-span-${tc}`,
        LG_COL_SPAN[colSpan] ?? `lg:col-span-${colSpan}`,
        ROW_SPAN[rowSpan] ?? `row-span-${rowSpan}`,
        // Base styles
        'rounded-xl border border-border/60 bg-card overflow-hidden p-3',
        'transition-colors duration-150',
        onClick && 'cursor-pointer hover:bg-muted/30 hover:border-border',
        className
      )}
      style={{
        ...(accentColor ? { borderLeft: `3px solid ${accentColor}` } : {}),
        ...(accentBg ? { background: accentBg } : {}),
      }}
      onClick={onClick}
      {...(onClick
        ? {
            role: 'button' as const,
            tabIndex: 0,
            onKeyDown: (e: React.KeyboardEvent) => {
              if (e.key === 'Enter' || e.key === ' ') onClick();
            },
          }
        : {})}
    >
      {children}
    </div>
  );
}

function WidgetLabel({ children }: { children: React.ReactNode }) {
  return (
    <p className="font-nunito text-[9px] uppercase tracking-widest text-muted-foreground/50 mb-1.5">
      {children}
    </p>
  );
}

// ─── Empty state helper ───────────────────────────────────────────────────────

function WidgetEmptyState({
  icon,
  text,
  ctaLabel,
  ctaColor,
  href,
}: {
  icon: string;
  text: string;
  ctaLabel: string;
  ctaColor: string;
  href: string;
}) {
  const router = useRouter();
  return (
    <div className="flex-1 flex flex-col items-center justify-center gap-2 text-center p-4">
      <span className="text-2xl opacity-40">{icon}</span>
      <p className="font-nunito text-[11px] text-muted-foreground leading-snug">{text}</p>
      <button
        onClick={() => router.push(href)}
        className="font-nunito text-[11px] font-bold px-3 py-1 rounded-full border transition-colors hover:opacity-80"
        style={{ borderColor: `${ctaColor}66`, color: ctaColor }}
      >
        {ctaLabel}
      </button>
    </div>
  );
}

// ─── Live Session Widget (8×2) ────────────────────────────────────────────────

function LiveSessionWidget({
  session,
  isLoading,
}: {
  session: SessionSummaryDto | undefined;
  isLoading: boolean;
}) {
  const router = useRouter();

  if (isLoading) {
    return (
      <BentoWidget colSpan={8} rowSpan={2} accentColor={C.success} className="animate-pulse">
        <div className="h-full" />
      </BentoWidget>
    );
  }

  if (!session) {
    return (
      <BentoWidget colSpan={8} rowSpan={2} className="border-dashed flex items-center gap-4">
        <div className="flex-1 min-w-0">
          <p className="font-quicksand font-bold text-base text-foreground">
            Nessuna sessione attiva
          </p>
          <p className="text-sm text-muted-foreground mt-0.5">
            Avvia una nuova partita per vederla qui
          </p>
        </div>
        <Link
          href="/sessions/new"
          className="shrink-0 px-4 py-2 rounded-lg text-sm font-bold text-white transition-opacity hover:opacity-90"
          style={{ background: C.game }}
          onClick={e => e.stopPropagation()}
        >
          Nuova partita
        </Link>
      </BentoWidget>
    );
  }

  return (
    <BentoWidget
      colSpan={8}
      rowSpan={2}
      accentColor={C.success}
      accentBg="hsl(var(--e-success) / 0.04)"
      className="flex flex-col justify-between"
      onClick={() => router.push(`/sessions/${session.id}`)}
    >
      <div className="flex items-center justify-between mb-2">
        <WidgetLabel>Sessione Recente</WidgetLabel>
      </div>
      <div className="flex items-center gap-3">
        <div
          className="w-11 h-11 rounded-xl flex items-center justify-center text-2xl shrink-0 overflow-hidden"
          style={{ background: 'rgba(255,255,255,0.06)' }}
        >
          {session.gameImageUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img
              src={session.gameImageUrl}
              alt={session.gameName}
              className="w-full h-full object-cover"
            />
          ) : (
            '🎲'
          )}
        </div>
        <div className="flex-1 min-w-0">
          <p className="font-quicksand font-extrabold text-base leading-tight truncate">
            {session.gameName}
          </p>
          <p className="font-nunito text-[11px] text-muted-foreground mt-0.5">
            {session.playerCount} giocatori
            {session.winnerName ? ` · Vincitore: ${session.winnerName}` : ''}
          </p>
        </div>
        <span
          className="shrink-0 px-3.5 py-1.5 rounded-lg text-[11px] font-bold font-nunito text-white"
          style={{ background: C.game }}
          aria-hidden="true"
        >
          Vai →
        </span>
      </div>
    </BentoWidget>
  );
}

// ─── KPI Stats Widget (4×2) ───────────────────────────────────────────────────

function KpiStatsWidget({
  stats,
  isLoading,
}: {
  stats: {
    totalGames: number;
    monthlyPlays: number;
    weeklyPlayTime: string;
    monthlyFavorites: number;
  } | null;
  isLoading: boolean;
}) {
  return (
    <BentoWidget colSpan={4} tabletColSpan={6} rowSpan={2}>
      <WidgetLabel>Le tue statistiche</WidgetLabel>
      {isLoading ? (
        <div className="grid grid-cols-2 gap-2 mt-1">
          {Array.from({ length: 4 }).map((_, i) => (
            <div key={i} className="h-8 rounded bg-muted/40 animate-pulse" />
          ))}
        </div>
      ) : (
        <div className="grid grid-cols-2 gap-x-3 gap-y-2 mt-1">
          <div>
            <p className="font-quicksand font-extrabold text-2xl" style={{ color: C.game }}>
              {stats?.totalGames ?? '—'}
            </p>
            <p className="font-nunito text-[10px] text-muted-foreground">giochi</p>
          </div>
          <div>
            <p className="font-quicksand font-extrabold text-2xl" style={{ color: C.session }}>
              {stats?.monthlyPlays ?? '—'}
            </p>
            <p className="font-nunito text-[10px] text-muted-foreground">partite/mese</p>
          </div>
          <div>
            <p className="font-quicksand font-extrabold text-2xl" style={{ color: C.chat }}>
              {stats ? parsePlayTimeHours(stats.weeklyPlayTime) : '—'}
            </p>
            <p className="font-nunito text-[10px] text-muted-foreground">ore/sett.</p>
          </div>
          <div>
            <p className="font-quicksand font-extrabold text-2xl" style={{ color: C.success }}>
              {stats?.monthlyFavorites ?? '—'}
            </p>
            <p className="font-nunito text-[10px] text-muted-foreground">preferiti</p>
          </div>
        </div>
      )}
    </BentoWidget>
  );
}

// ─── Library Widget (6×4) ─────────────────────────────────────────────────────

function LibraryWidget({
  games,
  totalCount,
  isLoading,
}: {
  games: UserGameDto[];
  totalCount: number;
  isLoading: boolean;
}) {
  const router = useRouter();

  return (
    <BentoWidget
      colSpan={6}
      rowSpan={4}
      className="flex flex-col gap-0"
      onClick={() => router.push('/library')}
    >
      <WidgetLabel>La Tua Libreria</WidgetLabel>
      <div className="flex-1 flex flex-col overflow-hidden">
        {isLoading ? (
          Array.from({ length: 5 }).map((_, i) => (
            <div
              key={i}
              className="flex items-center gap-2 py-1.5 border-b border-border/40 animate-pulse"
            >
              <div className="w-7 h-7 rounded-md bg-muted/60 shrink-0" />
              <div className="flex-1 h-3 rounded bg-muted/60" />
              <div className="w-8 h-3 rounded bg-muted/40" />
            </div>
          ))
        ) : games.length === 0 ? (
          <WidgetEmptyState
            icon="🎲"
            text="La tua libreria è vuota. Aggiungi i giochi che possiedi."
            ctaLabel="+ Aggiungi gioco"
            ctaColor={C.game}
            href="/library?action=add"
          />
        ) : (
          games.slice(0, 6).map(game => (
            <div
              key={game.id}
              className="flex items-center gap-2 py-1.5 border-b border-border/40 last:border-0 group/row"
              onClick={e => {
                e.stopPropagation();
                router.push(`/library/${game.id}`);
              }}
            >
              {(game.thumbnailUrl ?? game.imageUrl) ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={game.thumbnailUrl ?? game.imageUrl ?? ''}
                  alt={game.title}
                  className="w-7 h-7 rounded-md object-cover shrink-0"
                />
              ) : (
                <div
                  className="w-7 h-7 rounded-md shrink-0 flex items-center justify-center text-sm"
                  style={{ background: 'hsl(var(--e-game) / 0.13)' }}
                >
                  🎲
                </div>
              )}
              <span className="font-quicksand font-semibold text-[11px] flex-1 truncate text-foreground group-hover/row:text-primary transition-colors">
                {game.title}
              </span>
              {game.averageRating !== null && game.averageRating !== undefined && (
                <span
                  className="font-mono text-[9px] font-semibold shrink-0"
                  style={{ color: C.game }}
                >
                  ★ {game.averageRating.toFixed(1)}
                </span>
              )}
            </div>
          ))
        )}
      </div>
      {games.length > 0 && (
        <p className="font-nunito text-[10px] font-bold mt-2 pt-1" style={{ color: C.game }}>
          Vedi tutti {totalCount} →
        </p>
      )}
    </BentoWidget>
  );
}

// ─── Chat Preview Widget (6×4) ────────────────────────────────────────────────

function ChatPreviewWidget() {
  const router = useRouter();
  return (
    <BentoWidget
      colSpan={6}
      rowSpan={4}
      accentColor={C.chat}
      className="flex flex-col"
      onClick={() => router.push('/chat')}
    >
      <div className="flex items-center justify-between mb-2">
        <WidgetLabel>Chat AI</WidgetLabel>
        <span
          className="text-[9px] font-bold font-nunito rounded-full px-2 py-0.5"
          style={{ background: 'hsl(var(--e-chat) / 0.13)', color: C.chat }}
        >
          Regole & Domande
        </span>
      </div>
      <div className="flex-1 flex flex-col gap-1.5 overflow-hidden justify-end">
        <div
          className="self-end max-w-[80%] rounded-lg px-2.5 py-1.5 text-[11px] font-nunito"
          style={{
            background: 'hsl(var(--e-game) / 0.09)',
            border: '1px solid hsl(var(--e-game) / 0.13)',
          }}
        >
          Quante strade posso costruire?
        </div>
        <div
          className="self-start max-w-[90%] rounded-lg px-2.5 py-1.5 text-[11px] font-nunito text-muted-foreground"
          style={{
            background: 'rgba(255,255,255,0.04)',
            border: '1px solid rgba(255,255,255,0.07)',
          }}
        >
          Puoi costruire tutte le strade che vuoi, purché tu abbia le risorse.{' '}
          <span
            className="font-mono text-[8px] rounded px-1 py-0.5 cursor-pointer"
            style={{ background: 'hsl(var(--e-chat) / 0.09)', color: C.chat }}
          >
            p.8
          </span>
        </div>
      </div>
      <div
        className="mt-auto pt-2 flex gap-1.5"
        onClick={e => {
          e.stopPropagation();
          router.push('/chat');
        }}
      >
        <div
          className="flex-1 h-7 rounded-lg flex items-center px-2.5 text-[11px] text-muted-foreground/50"
          style={{
            background: 'rgba(255,255,255,0.03)',
            border: '1px solid rgba(255,255,255,0.07)',
          }}
        >
          Fai una domanda…
        </div>
        <button
          className="w-7 h-7 rounded-lg flex items-center justify-center text-white text-sm"
          style={{ background: C.chat }}
          aria-label="Vai alla chat"
        >
          ↑
        </button>
      </div>
    </BentoWidget>
  );
}

// ─── Leaderboard Widget (6×3) ─────────────────────────────────────────────────

function LeaderboardWidget({ sessions }: { sessions: SessionSummaryDto[] }) {
  const winners = sessions
    .filter(s => s.winnerName)
    .reduce<Record<string, number>>((acc, s) => {
      const key = s.winnerName ?? '';
      acc[key] = (acc[key] ?? 0) + 1;
      return acc;
    }, {});

  const sorted = Object.entries(winners)
    .sort(([, a], [, b]) => b - a)
    .slice(0, 4);

  const medals = ['🥇', '🥈', '🥉', '4️⃣'];
  const avatarColors = [C.game, C.player, C.event, C.session];

  return (
    <BentoWidget colSpan={6} rowSpan={3} accentColor={C.event} className="flex flex-col">
      <WidgetLabel>Classifica Gruppo</WidgetLabel>
      <div className="flex-1 flex flex-col overflow-hidden">
        {sorted.length === 0 ? (
          <WidgetEmptyState
            icon="🏆"
            text="Gioca partite con amici per vedere la classifica."
            ctaLabel="▶ Nuova sessione"
            ctaColor={C.session}
            href="/sessions/new"
          />
        ) : (
          sorted.map(([name, wins], i) => (
            <div
              key={name}
              className="flex items-center gap-2 py-1.5 border-b border-border/40 last:border-0"
            >
              <span className="text-sm w-5 text-center shrink-0">{medals[i]}</span>
              <div
                className="w-5 h-5 rounded-full flex items-center justify-center text-[9px] font-bold text-white shrink-0"
                style={{ background: avatarColors[i] }}
              >
                {name[0]?.toUpperCase()}
              </div>
              <span className="flex-1 font-quicksand font-semibold text-[11px] truncate">
                {name}
              </span>
              <span className="font-mono text-[9px] text-muted-foreground shrink-0">
                {wins} vitt.
              </span>
            </div>
          ))
        )}
      </div>
    </BentoWidget>
  );
}

// ─── Trending Widget (6×3) ───────────────────────────────────────────────────

function TrendingWidget({ games, isLoading }: { games: TrendingGameDto[]; isLoading: boolean }) {
  const router = useRouter();
  return (
    <BentoWidget
      colSpan={6}
      rowSpan={3}
      accentColor={C.kb}
      className="flex flex-col"
      onClick={() => router.push('/library?tab=catalogo')}
    >
      <WidgetLabel>Popolari questa settimana</WidgetLabel>
      <div className="flex gap-3 mt-1 overflow-hidden flex-1">
        {isLoading
          ? Array.from({ length: 5 }).map((_, i) => (
              <div key={i} className="flex flex-col items-center gap-1 shrink-0">
                <div className="w-9 h-12 rounded-md bg-muted/60 animate-pulse" />
                <div className="w-9 h-2 rounded bg-muted/40 animate-pulse" />
              </div>
            ))
          : games.slice(0, 6).map(game => (
              <div
                key={game.gameId}
                className="flex flex-col items-center gap-1 cursor-pointer shrink-0 group/card"
                onClick={e => {
                  e.stopPropagation();
                  router.push(`/games/${game.gameId}`);
                }}
              >
                {game.thumbnailUrl ? (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img
                    src={game.thumbnailUrl}
                    alt={game.title}
                    className="w-9 h-12 rounded-md object-cover group-hover/card:ring-1 group-hover/card:ring-primary transition-all"
                  />
                ) : (
                  <div
                    className="w-9 h-12 rounded-md flex items-center justify-center text-lg"
                    style={{ background: 'rgba(255,255,255,0.06)' }}
                  >
                    🎲
                  </div>
                )}
                <span className="font-quicksand text-[8px] font-bold text-center w-9 truncate">
                  {game.title}
                </span>
              </div>
            ))}
      </div>
    </BentoWidget>
  );
}

// ─── Dashboard Client ─────────────────────────────────────────────────────────

export function DashboardClient() {
  const { user } = useAuth();
  const {
    fetchStats,
    fetchGames,
    fetchRecentSessions,
    fetchTrendingGames,
    updateFilters,
    stats,
    games,
    totalGamesCount,
    recentSessions,
    trendingGames,
    isLoadingStats,
    isLoadingGames,
    isLoadingTrending,
    isLoadingSessions,
  } = useDashboardStore();

  useEffect(() => {
    fetchStats();
    fetchRecentSessions(10);
    fetchTrendingGames(6);
    updateFilters({ sort: 'alphabetical', pageSize: 6, page: 1 });
    fetchGames();
    // eslint-disable-next-line react-hooks/exhaustive-deps -- store actions are stable Zustand references
  }, []);

  const firstName = user?.displayName?.split(' ')[0] ?? user?.displayName ?? 'Giocatore';

  // New user: stats loaded, no games, no sessions
  const isNewUser =
    !isLoadingStats && stats !== null && stats.totalGames === 0 && recentSessions.length === 0;

  return (
    <div className="max-w-[1400px] mx-auto px-4 py-6 space-y-4 pb-24">
      {/* Stats row — sempre visibile */}
      <StatsRow />

      {/* Bento grid */}
      <div className="grid grid-cols-6 lg:grid-cols-12 auto-rows-[48px] gap-3">
        {isNewUser ? (
          <>
            {/* Welcome Hero — full width */}
            <WelcomeHero firstName={firstName} />

            {/* Library — empty CTA */}
            <LibraryWidget games={[]} totalCount={0} isLoading={false} />

            {/* Trending — always has real data */}
            <TrendingWidget games={trendingGames} isLoading={isLoadingTrending} />

            {/* Chat — empty CTA */}
            <ChatPreviewWidget />

            {/* Leaderboard — empty CTA */}
            <LeaderboardWidget sessions={[]} />
          </>
        ) : (
          <>
            {/* LiveSession / Sessione recente */}
            <LiveSessionWidget session={recentSessions[0]} isLoading={isLoadingSessions} />

            {/* KPI Stats */}
            <KpiStatsWidget stats={stats} isLoading={isLoadingStats} />

            {/* Library */}
            <LibraryWidget games={games} totalCount={totalGamesCount} isLoading={isLoadingGames} />

            {/* Chat */}
            <ChatPreviewWidget />

            {/* Leaderboard */}
            <LeaderboardWidget sessions={recentSessions} />

            {/* Trending */}
            <TrendingWidget games={trendingGames} isLoading={isLoadingTrending} />
          </>
        )}
      </div>

      {/* FAB contestuale */}
      <FloatingActionPill page="dashboard" />
    </div>
  );
}
