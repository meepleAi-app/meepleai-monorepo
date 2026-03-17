'use client';

import { useMemo } from 'react';

import { useDashboardData } from '@/hooks/useDashboardData';

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function getTimeGreeting(): string {
  const hour = new Date().getHours();
  if (hour < 12) return 'Buongiorno';
  if (hour < 18) return 'Buon pomeriggio';
  return 'Buonasera';
}

// ---------------------------------------------------------------------------
// Skeleton
// ---------------------------------------------------------------------------

function HeroZoneSkeleton() {
  return (
    <div
      data-testid="hero-zone-skeleton"
      className="dashboard-hero w-full rounded-2xl bg-gradient-to-br from-amber-50 to-orange-50 dark:from-amber-950/30 dark:to-orange-950/30 p-6 md:p-8 animate-pulse"
    >
      <div className="h-8 w-48 rounded-lg bg-amber-200/50 dark:bg-amber-800/30 mb-3" />
      <div className="h-5 w-72 rounded-md bg-amber-200/30 dark:bg-amber-800/20" />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

function GameNightBanner({
  gameNight,
}: {
  gameNight: { id: string; title: string; scheduledAt: string };
}) {
  const scheduledDate = new Date(gameNight.scheduledAt);
  const hoursUntil = Math.max(0, Math.round((scheduledDate.getTime() - Date.now()) / 3_600_000));

  return (
    <div className="mt-4 flex items-center gap-3 rounded-xl bg-white/60 dark:bg-white/10 px-4 py-3 backdrop-blur-sm">
      <span className="text-2xl" role="img" aria-label="calendar">
        📅
      </span>
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium text-amber-900 dark:text-amber-100 truncate">
          {gameNight.title}
        </p>
        <p className="text-xs text-amber-700/70 dark:text-amber-300/70">
          {hoursUntil <= 1 ? 'Tra poco!' : `Tra ${hoursUntil} ore`}
        </p>
      </div>
    </div>
  );
}

function WelcomePrompt() {
  return (
    <div className="mt-4 flex items-center gap-3 rounded-xl bg-white/60 dark:bg-white/10 px-4 py-3 backdrop-blur-sm">
      <span className="text-2xl" role="img" aria-label="wave">
        👋
      </span>
      <p className="text-sm text-amber-900 dark:text-amber-100">
        Aggiungi il tuo primo gioco alla libreria per iniziare!
      </p>
    </div>
  );
}

// ---------------------------------------------------------------------------
// HeroZone
// ---------------------------------------------------------------------------

export function HeroZone() {
  const { data, isLoading } = useDashboardData();

  const greeting = useMemo(() => getTimeGreeting(), []);

  if (isLoading) {
    return <HeroZoneSkeleton />;
  }

  const user = data?.user;
  const stats = data?.stats;
  const isNewUser = !stats || stats.libraryCount === 0;

  // Check for upcoming game night within 24h (from activeSessions mock context)
  // In a real scenario, this would come from the dashboard data
  const upcomingGameNight: { id: string; title: string; scheduledAt: string } | null = null;

  return (
    <section
      data-testid="hero-zone"
      className="dashboard-hero w-full rounded-2xl bg-gradient-to-br from-amber-50 to-orange-50 dark:from-amber-950/30 dark:to-orange-950/30 p-6 md:p-8"
    >
      <h2 className="font-quicksand text-2xl font-bold text-amber-900 dark:text-amber-100 md:text-3xl">
        {greeting}
        {user?.username ? `, ${user.username}` : ''}!
      </h2>

      {stats && !isNewUser && (
        <p className="mt-1 text-sm text-amber-700/80 dark:text-amber-300/70">
          {stats.libraryCount} giochi in libreria &middot; {stats.playedLast30Days} partite questo
          mese
        </p>
      )}

      {upcomingGameNight && <GameNightBanner gameNight={upcomingGameNight} />}

      {isNewUser && <WelcomePrompt />}
    </section>
  );
}
