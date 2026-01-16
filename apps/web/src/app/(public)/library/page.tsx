/**
 * User Library Page (Issue #2445)
 *
 * Displays user's personal game collection with quota status.
 *
 * Route: /library
 * Features:
 * - Quota status bar showing usage and limits
 * - User's game collection grid
 * - Favorite toggle and notes
 * - Empty state with CTA to browse catalog
 *
 * Dependencies:
 * - useLibrary hook (TanStack Query)
 * - useLibraryQuota hook (Issue #2445)
 * - QuotaStatusBar component (Issue #2445)
 */

'use client';

import React from 'react';

import { BookOpen, Plus } from 'lucide-react';
import Link from 'next/link';

import { BottomNav } from '@/components/layout/BottomNav';
import { TopNav } from '@/components/layout/TopNav';
import { FavoriteToggle, QuotaStatusBar } from '@/components/library';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { useLibrary, useLibraryQuota } from '@/hooks/queries/useLibrary';

export default function LibraryPage() {
  // Fetch user's library and quota
  const {
    data: libraryData,
    isLoading: libraryLoading,
    error: libraryError,
  } = useLibrary();

  const {
    data: quota,
    isLoading: quotaLoading,
    error: quotaError,
  } = useLibraryQuota();

  // Loading state
  if (libraryLoading || quotaLoading) {
    return (
      <main className="min-h-screen bg-background pb-24 md:pb-0 md:pt-16">
        <TopNav />
        <div className="container mx-auto px-4 py-8 space-y-6">
          <Skeleton className="h-24 w-full" />
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {Array.from({ length: 6 }).map((_, i) => (
              <Skeleton key={i} className="h-64 w-full" />
            ))}
          </div>
        </div>
        <BottomNav />
      </main>
    );
  }

  // Error state
  if (libraryError || quotaError) {
    const errorMessage =
      libraryError instanceof Error ? libraryError.message : String(libraryError || quotaError);

    return (
      <main className="min-h-screen bg-background pb-24 md:pb-0 md:pt-16">
        <TopNav />
        <div className="container mx-auto px-4 py-8">
          <Alert variant="destructive">
            <AlertDescription>{errorMessage}</AlertDescription>
          </Alert>
        </div>
        <BottomNav />
      </main>
    );
  }

  const games = libraryData?.items ?? [];
  const hasGames = games.length > 0;

  return (
    <main className="min-h-screen bg-background pb-24 md:pb-0 md:pt-16">
      <TopNav />

      <div className="container mx-auto px-4 py-8 space-y-6">
        {/* Page Header */}
        <div className="flex items-center justify-between">
          <h1 className="text-3xl font-bold font-quicksand">La Mia Libreria</h1>
          <Button asChild>
            <Link href="/games">
              <Plus className="mr-2 h-4 w-4" />
              Aggiungi Gioco
            </Link>
          </Button>
        </div>

        {/* Quota Status Bar (Issue #2445) */}
        {quota && (
          <QuotaStatusBar
            currentCount={quota.currentCount}
            maxAllowed={quota.maxAllowed}
            userTier={quota.userTier}
            percentageUsed={quota.percentageUsed}
            remainingSlots={quota.remainingSlots}
          />
        )}

        {/* Library Content */}
        {hasGames ? (
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
            {games.map(game => (
              <Card key={game.id} className="hover:shadow-lg transition-shadow">
                <CardHeader>
                  <div className="flex items-start justify-between">
                    <div className="flex-1">
                      <CardTitle className="text-lg line-clamp-2">{game.gameTitle}</CardTitle>
                      {game.gamePublisher && (
                        <CardDescription className="mt-1">{game.gamePublisher}</CardDescription>
                      )}
                    </div>
                    <FavoriteToggle gameId={game.gameId} isFavorite={game.isFavorite} size="sm" />
                  </div>
                </CardHeader>
                <CardContent>
                  {game.notes && (
                    <p className="text-sm text-muted-foreground line-clamp-3 mb-3">{game.notes}</p>
                  )}
                  <div className="flex gap-2">
                    <Button asChild variant="outline" size="sm" className="flex-1">
                      <Link href={`/games/${game.gameId}`}>
                        <BookOpen className="mr-1 h-3 w-3" />
                        Dettagli
                      </Link>
                    </Button>
                  </div>
                  <p className="text-xs text-muted-foreground mt-2">
                    Aggiunto il {new Date(game.addedAt).toLocaleDateString('it-IT')}
                  </p>
                </CardContent>
              </Card>
            ))}
          </div>
        ) : (
          /* Empty State */
          <Card className="border-dashed">
            <CardContent className="flex flex-col items-center justify-center py-12 text-center">
              <BookOpen className="h-16 w-16 text-muted-foreground mb-4" />
              <h3 className="text-xl font-semibold mb-2">La tua libreria è vuota</h3>
              <p className="text-muted-foreground mb-6 max-w-md">
                Inizia ad aggiungere giochi dal catalogo per costruire la tua collezione personale.
              </p>
              <Button asChild>
                <Link href="/games">Esplora Catalogo Giochi</Link>
              </Button>
            </CardContent>
          </Card>
        )}
      </div>

      <BottomNav />
    </main>
  );
}
