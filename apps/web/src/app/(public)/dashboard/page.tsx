/**
 * Dashboard Page (Post-Login) - Issue #1836 [PAGE-002]
 *
 * Main user dashboard after login with personalized greeting, recent games,
 * and chat history. Protected route with TanStack Query integration.
 *
 * Design: Playful Boardroom - wireframes page 2 "Dashboard"
 *
 * Features:
 * - Personalized greeting (Ciao, {user}!)
 * - Recent games grid (6 games, 2x3)
 * - Chat history (5 most recent threads)
 * - QuickActions (Add Game, New Chat)
 * - BottomNav integration (active: /dashboard)
 *
 * Tech Stack:
 * - TanStack Query v5 (useCurrentUser, useGames, useChats)
 * - Shadcn Skeleton (pulse animation)
 * - Responsive design (mobile-first)
 *
 * Dependencies:
 * - UI-002: BottomNav ✅
 * - UI-003: GameCard ✅
 * - UI-007: QuickActions ✅
 * - FE-IMP-003: TanStack Query ✅
 * - FE-IMP-004: Auth Middleware ✅
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md
 */

'use client';

// Skip static prerendering - page uses client-side data fetching with React Query
// and imports components that require DOMMatrix (framer-motion)
export const dynamic = 'force-dynamic';

import React from 'react';

import { AlertCircle } from 'lucide-react';

import { QuickActions } from '@/components/dashboard/QuickActions';
import { BottomNav } from '@/components/layout/BottomNav';
import { TopNav } from '@/components/layout/TopNav';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { useGames } from '@/hooks/queries/useGames';

import { ActiveSessionsSection } from './components/ActiveSessionsSection';
import { ChatHistorySection } from './components/ChatHistorySection';
import { GreetingSection } from './components/GreetingSection';
import { LibraryQuotaSection } from './components/LibraryQuotaSection';
import { RecentGamesSection } from './components/RecentGamesSection';
import { RecentlyAddedSection } from './components/RecentlyAddedSection';
import { SessionQuotaSection } from './components/SessionQuotaSection';

export default function DashboardPage() {
  // TanStack Query hooks (automatic caching, refetching, error handling)
  const { data: user, isLoading: userLoading, error: userError } = useCurrentUser();
  const {
    data: gamesData,
    isLoading: gamesLoading,
    error: gamesError,
  } = useGames(
    undefined, // no filters
    undefined, // no sorting
    1, // page 1
    6 // 6 games (2x3 grid for recent games)
  );

  // Loading state: Show skeleton UI
  if (userLoading) {
    return (
      <main className="min-h-screen bg-background pb-24 md:pb-0 md:pt-16">
        {/* Top Navigation (desktop) */}
        <TopNav />
        <div className="container mx-auto px-4 py-8 space-y-8">
          {/* Greeting Skeleton */}
          <div className="space-y-2">
            <Skeleton className="h-8 w-48" />
            <Skeleton className="h-4 w-64" />
          </div>

          {/* Recent Games Skeleton */}
          <div className="space-y-4">
            <Skeleton className="h-6 w-32" />
            <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
              {Array.from({ length: 6 }).map((_, i) => (
                <Skeleton key={i} className="h-64 w-full" />
              ))}
            </div>
          </div>

          {/* QuickActions Skeleton */}
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <Skeleton className="h-24 w-full" />
            <Skeleton className="h-24 w-full" />
          </div>
        </div>

        <BottomNav />
      </main>
    );
  }

  // Error state: Show error alert
  if (userError) {
    const errorMessage = userError instanceof Error ? userError.message : String(userError);

    return (
      <main className="min-h-screen bg-background pb-24 md:pb-0 md:pt-16">
        {/* Top Navigation (desktop) */}
        <TopNav />
        <div className="container mx-auto px-4 py-8">
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>Errore di Caricamento</AlertTitle>
            <AlertDescription>
              Impossibile caricare i dati del dashboard. Riprova più tardi.
              <span className="block mt-2 text-xs opacity-75">{errorMessage}</span>
            </AlertDescription>
          </Alert>
        </div>

        <BottomNav />
      </main>
    );
  }

  // No user data (should not happen with middleware, but defensive)
  if (!user) {
    return (
      <main className="min-h-screen bg-background pb-24 md:pb-0 md:pt-16">
        {/* Top Navigation (desktop) */}
        <TopNav />
        <div className="container mx-auto px-4 py-8">
          <Alert data-testid="dashboard-no-user-error">
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>Nessun Utente</AlertTitle>
            <AlertDescription>
              Non è stato possibile recuperare i dati utente. Effettua nuovamente il login.
            </AlertDescription>
          </Alert>
        </div>

        <BottomNav />
      </main>
    );
  }

  // Main dashboard content
  return (
    <main className="min-h-screen bg-background pb-24 md:pb-0 md:pt-16">
      {/* Top Navigation (desktop) */}
      <TopNav />
      <div className="container mx-auto px-4 py-8 space-y-8">
        {/* Greeting Section */}
        <GreetingSection user={user} />

        {/* Quota Widgets Grid (Library + Sessions) */}
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
          {/* Library Quota Widget (Issue #2445) */}
          <LibraryQuotaSection />

          {/* Session Quota Widget (Issue #3075) */}
          <SessionQuotaSection />
        </div>

        {/* Active Sessions Widget (Issue #2617) */}
        <ActiveSessionsSection />

        {/* Recently Added Games from Library (Issue #2612) */}
        <RecentlyAddedSection />

        {/* Recent Games Section */}
        <RecentGamesSection games={gamesData?.games} isLoading={gamesLoading} error={gamesError} />

        {/* Chat History Section */}
        <ChatHistorySection userId={user.id} />

        {/* Quick Actions */}
        <section aria-label="Quick actions">
          <h2 className="text-xl font-quicksand font-semibold mb-4">Azioni Rapide</h2>
          <QuickActions />
        </section>
      </div>

      {/* Bottom Navigation (mobile) */}
      <BottomNav />
    </main>
  );
}
