'use client';

/**
 * Dashboard Bento — Layout B (Bento Command)
 *
 * Desktop dashboard with:
 * - 12-column bento grid with variable-span widgets
 * - Live session, KPI stats, library, quick actions, leaderboard, trending
 */

import { useEffect } from 'react';

import Link from 'next/link';
import { useRouter } from 'next/navigation';

import type { SessionSummaryDto, TrendingGameDto, UserGameDto } from '@/lib/api/dashboard-client';
import { useDashboardStore } from '@/lib/stores/dashboard-store';
import { cn } from '@/lib/utils';

// ─── Entity colors (matches meeple-card-styles.ts) ───────────────────────────

const C = {
  game: 'hsl(25,95%,45%)',
  player: 'hsl(262,83%,58%)',
  session: 'hsl(240,60%,55%)',
  chat: 'hsl(220,80%,55%)',
  kb: 'hsl(174,60%,40%)',
  event: 'hsl(350,89%,60%)',
  agent: 'hsl(38,92%,50%)',
  success: 'hsl(142,70%,45%)',
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
    <p className="font-mono text-[9px] uppercase tracking-widest text-muted-foreground/50 mb-1.5">
      {children}
    </p>
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
      accentBg="rgba(16,185,129,0.04)"
      className="flex flex-col justify-between"
      onClick={() => router.push(`/sessions/${session.id}`)}
    >
      <div className="flex items-center justify-between mb-2">
        <WidgetLabel>Sessione Live</WidgetLabel>
        <span
          className="inline-flex items-center gap-1.5 text-[10px] font-bold rounded-full px-2.5 py-0.5"
          style={{
            background: 'rgba(16,185,129,0.12)',
            color: C.success,
            border: '1px solid rgba(16,185,129,0.2)',
          }}
        >
          <span
            className="w-1.5 h-1.5 rounded-full animate-pulse"
            style={{ background: C.success }}
          />
          IN CORSO
        </span>
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
          <p className="text-[11px] text-muted-foreground mt-0.5">
            {session.playerCount} giocatori
            {session.winnerName ? ` · Vincitore: ${session.winnerName}` : ''}
          </p>
        </div>
        <span
          className="shrink-0 px-3.5 py-1.5 rounded-lg text-[11px] font-bold text-white"
          style={{ background: C.game }}
          aria-hidden="true"
        >
          Entra →
        </span>
      </div>
    </BentoWidget>
  );
}

// ─── KPI Widget (variable span) ───────────────────────────────────────────────

interface KpiWidgetProps {
  label: string;
  value: string | number;
  badge?: string;
  badgePositive?: boolean;
  sub?: string;
  accentColor: string;
  colSpan: number;
  tabletColSpan?: number;
  rowSpan: number;
  href?: string;
}

function KpiWidget({
  label,
  value,
  badge,
  badgePositive,
  sub,
  accentColor,
  colSpan,
  tabletColSpan,
  rowSpan,
  href,
}: KpiWidgetProps) {
  const router = useRouter();
  return (
    <BentoWidget
      colSpan={colSpan}
      tabletColSpan={tabletColSpan}
      rowSpan={rowSpan}
      accentColor={accentColor}
      onClick={href ? () => router.push(href) : undefined}
    >
      <WidgetLabel>{label}</WidgetLabel>
      <p
        className="font-quicksand text-[26px] font-extrabold leading-none tracking-tight"
        style={{ color: accentColor }}
      >
        {value}
      </p>
      {badge && (
        <span
          className={cn(
            'inline-block mt-1.5 text-[10px] font-bold px-2 py-0.5 rounded-full',
            badgePositive
              ? 'bg-emerald-500/15 text-emerald-400'
              : 'bg-muted/60 text-muted-foreground'
          )}
        >
          {badge}
        </span>
      )}
      {sub && !badge && <p className="text-[10px] text-muted-foreground mt-1">{sub}</p>}
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
          <p className="text-[11px] text-muted-foreground mt-2">Nessun gioco in libreria ancora</p>
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
                  style={{ background: `${C.game}22` }}
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
      <p className="text-[10px] font-bold mt-2 pt-1" style={{ color: C.game }}>
        Vedi tutti {totalCount} →
      </p>
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
          className="text-[9px] font-bold rounded-full px-2 py-0.5"
          style={{ background: `${C.chat}20`, color: C.chat }}
        >
          Regole & Domande
        </span>
      </div>
      <div className="flex-1 flex flex-col gap-1.5 overflow-hidden justify-end">
        <div
          className="self-end max-w-[80%] rounded-lg px-2.5 py-1.5 text-[11px]"
          style={{ background: `${C.game}18`, border: `1px solid ${C.game}22` }}
        >
          Quante strade posso costruire?
        </div>
        <div
          className="self-start max-w-[90%] rounded-lg px-2.5 py-1.5 text-[11px] text-muted-foreground"
          style={{
            background: 'rgba(255,255,255,0.04)',
            border: '1px solid rgba(255,255,255,0.07)',
          }}
        >
          Puoi costruire tutte le strade che vuoi, purché tu abbia le risorse.{' '}
          <span
            className="font-mono text-[8px] rounded px-1 py-0.5 cursor-pointer"
            style={{ background: `${C.chat}18`, color: C.chat }}
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
          <p className="text-[11px] text-muted-foreground mt-2">
            Gioca partite con amici per vedere la classifica
          </p>
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

// ─── Trending Widget (6×2) ───────────────────────────────────────────────────

function TrendingWidget({ games, isLoading }: { games: TrendingGameDto[]; isLoading: boolean }) {
  const router = useRouter();
  return (
    <BentoWidget
      colSpan={6}
      rowSpan={2}
      accentColor={C.kb}
      className="flex flex-col"
      onClick={() => router.push('/games')}
    >
      <WidgetLabel>Popolari questa settimana</WidgetLabel>
      <div className="flex gap-3 mt-1 overflow-hidden">
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
  const {
    stats,
    isLoadingStats,
    fetchStats,
    recentSessions,
    isLoadingSessions,
    fetchRecentSessions,
    updateFilters,
    games,
    isLoadingGames,
    totalGamesCount,
    trendingGames,
    isLoadingTrending,
    fetchTrendingGames,
  } = useDashboardStore();

  useEffect(() => {
    fetchStats();
    fetchRecentSessions(8);
    fetchTrendingGames(6);
    updateFilters({ sort: 'alphabetical', pageSize: 8, page: 1 });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- store actions are stable Zustand references
  }, []);

  const latestSession = recentSessions[0];

  const monthlyPlays = stats?.monthlyPlays ?? 0;
  const playsChange = stats?.monthlyPlaysChange;
  const weeklyHours = stats ? parsePlayTimeHours(stats.weeklyPlayTime) : '—';
  const totalGames = stats?.totalGames ?? 0;

  return (
    <div className="flex-1 overflow-y-auto p-3.5">
      <div
        className="grid grid-cols-6 lg:grid-cols-12"
        style={{ gridAutoRows: '60px', gap: '8px' }}
      >
        {/* Row 1-2: Live Session (8×2) + Partite (4×2) */}
        <LiveSessionWidget session={latestSession} isLoading={isLoadingSessions} />
        <KpiWidget
          label="Partite (mese)"
          value={isLoadingStats ? '…' : monthlyPlays}
          badge={
            playsChange !== null && playsChange !== undefined && playsChange !== 0
              ? `${playsChange > 0 ? '+' : ''}${playsChange}%`
              : undefined
          }
          badgePositive={(playsChange ?? 0) > 0}
          accentColor={C.game}
          colSpan={4}
          tabletColSpan={6}
          rowSpan={2}
          href="/sessions"
        />

        {/* Row 3-6: Library (6×4) + Ore (3×2) + Giochi (3×2) */}
        <LibraryWidget
          games={games}
          totalCount={totalGamesCount || totalGames}
          isLoading={isLoadingGames}
        />
        <KpiWidget
          label="Ore sett."
          value={isLoadingStats ? '…' : weeklyHours}
          sub="questa settimana"
          accentColor={C.session}
          colSpan={3}
          rowSpan={2}
        />
        <KpiWidget
          label="Giochi in lib."
          value={isLoadingStats ? '…' : totalGames}
          accentColor={C.event}
          colSpan={3}
          rowSpan={2}
          href="/library"
        />

        {/* Row 5-8: Chat AI (6×4) */}
        <ChatPreviewWidget />

        {/* Row 7-9: Leaderboard (6×3) */}
        <LeaderboardWidget sessions={recentSessions} />

        {/* Row 9-10: Trending (6×2) */}
        <TrendingWidget games={trendingGames} isLoading={isLoadingTrending} />
      </div>
    </div>
  );
}
