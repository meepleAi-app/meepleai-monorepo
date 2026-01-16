/**
 * User Library Page (Issue #2464)
 *
 * Enhanced user library management with search, filtering, and actions.
 *
 * Route: /library
 * Features:
 * - Search by game title
 * - Filter by favorites
 * - Sort options (date, title, favorite)
 * - Edit notes modal
 * - Remove confirmation dialog
 * - Quota status bar
 * - Empty state with CTA
 */

'use client';

import React, { useState } from 'react';

import { BookOpen, Plus, Edit2, Trash2 } from 'lucide-react';
import Link from 'next/link';

import { BottomNav } from '@/components/layout/BottomNav';
import { TopNav } from '@/components/layout/TopNav';
import {
  FavoriteToggle,
  QuotaStatusBar,
  LibraryFilters,
  EditNotesModal,
  RemoveGameDialog,
} from '@/components/library';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Skeleton } from '@/components/ui/skeleton';
import { useLibrary, useLibraryQuota } from '@/hooks/queries/useLibrary';
import type { GetUserLibraryParams } from '@/lib/api/schemas/library.schemas';

export default function LibraryPage() {
  // Filter state
  const [filters, setFilters] = useState<GetUserLibraryParams>({
    page: 1,
    pageSize: 20,
    favoritesOnly: false,
    sortBy: 'addedAt',
    sortDescending: true,
  });
  const [searchQuery, setSearchQuery] = useState('');

  // Modal state
  const [editNotesModal, setEditNotesModal] = useState<{
    isOpen: boolean;
    gameId: string;
    gameTitle: string;
    currentNotes?: string | null;
  }>({
    isOpen: false,
    gameId: '',
    gameTitle: '',
    currentNotes: null,
  });

  const [removeDialog, setRemoveDialog] = useState<{
    isOpen: boolean;
    gameId: string;
    gameTitle: string;
  }>({
    isOpen: false,
    gameId: '',
    gameTitle: '',
  });

  // Fetch user's library and quota
  const {
    data: libraryData,
    isLoading: libraryLoading,
    error: libraryError,
  } = useLibrary(filters);

  const {
    data: quota,
    isLoading: quotaLoading,
    error: quotaError,
  } = useLibraryQuota();

  const handleSearchChange = (query: string) => {
    setSearchQuery(query);
    // TODO: Integrate with backend search when API supports it
  };

  const handleFavoritesChange = (enabled: boolean) => {
    setFilters(prev => ({ ...prev, favoritesOnly: enabled, page: 1 }));
  };

  const handleSortChange = (
    sortBy: 'addedAt' | 'title' | 'favorite',
    descending: boolean
  ) => {
    setFilters(prev => ({
      ...prev,
      sortBy,
      sortDescending: descending,
      page: 1,
    }));
  };

  const handleClearFilters = () => {
    setSearchQuery('');
    setFilters({
      page: 1,
      pageSize: 20,
      favoritesOnly: false,
      sortBy: 'addedAt',
      sortDescending: true,
    });
  };

  const handleEditNotes = (gameId: string, gameTitle: string, currentNotes?: string | null) => {
    setEditNotesModal({
      isOpen: true,
      gameId,
      gameTitle,
      currentNotes,
    });
  };

  const handleRemoveGame = (gameId: string, gameTitle: string) => {
    setRemoveDialog({
      isOpen: true,
      gameId,
      gameTitle,
    });
  };

  // Loading state
  if (libraryLoading || quotaLoading) {
    return (
      <main className="min-h-screen bg-background pb-24 md:pb-0 md:pt-16">
        <TopNav />
        <div className="container mx-auto px-4 py-8 space-y-6">
          <Skeleton className="h-24 w-full" />
          <Skeleton className="h-16 w-full" />
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

  // Client-side search filtering (until backend supports it)
  const filteredGames = searchQuery
    ? games.filter(game => game.gameTitle.toLowerCase().includes(searchQuery.toLowerCase()))
    : games;

  return (
    <main className="min-h-screen bg-background pb-24 md:pb-0 md:pt-16">
      <TopNav />

      <div className="container mx-auto px-4 py-8 space-y-6">
        {/* Page Header */}
        <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
          <h1 className="text-3xl font-bold font-quicksand">La Mia Libreria</h1>
          <Button asChild>
            <Link href="/games">
              <Plus className="mr-2 h-4 w-4" />
              Aggiungi Gioco
            </Link>
          </Button>
        </div>

        {/* Quota Status Bar */}
        {quota && (
          <QuotaStatusBar
            currentCount={quota.currentCount}
            maxAllowed={quota.maxAllowed}
            userTier={quota.userTier}
            remainingSlots={quota.remainingSlots}
            percentageUsed={quota.percentageUsed}
          />
        )}

        {/* Filters */}
        {hasGames && (
          <LibraryFilters
            searchQuery={searchQuery}
            onSearchChange={handleSearchChange}
            favoritesOnly={filters.favoritesOnly}
            onFavoritesChange={handleFavoritesChange}
            sortBy={filters.sortBy}
            sortDescending={filters.sortDescending}
            onSortChange={handleSortChange}
            onClearFilters={handleClearFilters}
          />
        )}

        {/* Library Content */}
        {hasGames ? (
          <>
            {filteredGames.length > 0 ? (
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                {filteredGames.map(game => (
                  <Card
                    key={game.id}
                    className="hover:shadow-lg transition-shadow"
                    data-testid="game-card"
                  >
                    <CardHeader>
                      <div className="flex items-start justify-between">
                        <div className="flex-1">
                          <CardTitle className="text-lg line-clamp-2">{game.gameTitle}</CardTitle>
                          {game.gamePublisher && (
                            <CardDescription className="mt-1">{game.gamePublisher}</CardDescription>
                          )}
                        </div>
                        <FavoriteToggle
                          gameId={game.gameId}
                          isFavorite={game.isFavorite}
                          gameTitle={game.gameTitle}
                          size="sm"
                        />
                      </div>
                    </CardHeader>
                    <CardContent>
                      {game.notes && (
                        <p className="text-sm text-muted-foreground line-clamp-3 mb-3">
                          {game.notes}
                        </p>
                      )}
                      <div className="flex gap-2 mb-3">
                        <Button asChild variant="outline" size="sm" className="flex-1">
                          <Link href={`/games/${game.gameId}`}>
                            <BookOpen className="mr-1 h-3 w-3" />
                            Dettagli
                          </Link>
                        </Button>
                      </div>
                      <div className="flex gap-2">
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleEditNotes(game.gameId, game.gameTitle, game.notes)}
                          className="flex-1"
                        >
                          <Edit2 className="mr-1 h-3 w-3" />
                          Modifica Note
                        </Button>
                        <Button
                          variant="ghost"
                          size="sm"
                          onClick={() => handleRemoveGame(game.gameId, game.gameTitle)}
                          className="text-destructive hover:text-destructive"
                        >
                          <Trash2 className="h-3 w-3" />
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
              /* No Results from Search */
              <Card className="border-dashed">
                <CardContent className="flex flex-col items-center justify-center py-12 text-center">
                  <BookOpen className="h-16 w-16 text-muted-foreground mb-4" />
                  <h3 className="text-xl font-semibold mb-2">Nessun gioco trovato</h3>
                  <p className="text-muted-foreground mb-6 max-w-md">
                    Prova a modificare i filtri o la ricerca per trovare i tuoi giochi.
                  </p>
                  <Button variant="outline" onClick={handleClearFilters}>
                    Pulisci Filtri
                  </Button>
                </CardContent>
              </Card>
            )}
          </>
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

      {/* Edit Notes Modal */}
      <EditNotesModal
        isOpen={editNotesModal.isOpen}
        onClose={() => setEditNotesModal(prev => ({ ...prev, isOpen: false }))}
        gameId={editNotesModal.gameId}
        gameTitle={editNotesModal.gameTitle}
        currentNotes={editNotesModal.currentNotes}
      />

      {/* Remove Game Dialog */}
      <RemoveGameDialog
        isOpen={removeDialog.isOpen}
        onClose={() => setRemoveDialog(prev => ({ ...prev, isOpen: false }))}
        gameId={removeDialog.gameId}
        gameTitle={removeDialog.gameTitle}
      />
    </main>
  );
}
