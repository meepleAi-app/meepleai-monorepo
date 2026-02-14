/**
 * Library Page Client Component (Issue #2464, #2613, #2618, #2866)
 * Updated: Issue #3104 - Navigation handled by layout
 * Updated: Issue #2866 - Filters and View Mode Toggle
 *
 * Client-side component for the library page with framer-motion animations.
 * Loaded via next/dynamic with ssr: false to avoid DOMMatrix SSR issues.
 */

'use client';

import React, { useMemo, useState } from 'react';

import { AnimatePresence, motion } from 'framer-motion';
import { BookOpen, CheckSquare, Plus, Share2 } from 'lucide-react';
import Link from 'next/link';

import {
  QuotaStatusBar,
  QuotaStickyHeader,
  LibraryFilters,
  EditNotesModal,
  RemoveGameDialog,
  MeepleLibraryGameCard,
  AgentConfigModal,
  PdfUploadModal,
  BulkActionBar,
  ShareLibraryModal,
  ViewModeToggle,
  type ViewMode,
} from '@/components/library';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useLibrary, useLibraryQuota } from '@/hooks/queries/useLibrary';
import type { GameStateType, GetUserLibraryParams } from '@/lib/api/schemas/library.schemas';
import { useBulkSelectionStore } from '@/lib/stores/bulk-selection-store';

export default function LibraryPageClient() {
  // Filter state (Issue #2866)
  const [filters, setFilters] = useState<GetUserLibraryParams>({
    page: 1,
    pageSize: 20,
    favoritesOnly: false,
    stateFilter: [],
    sortBy: 'addedAt',
    sortDescending: true,
  });
  const [searchQuery, setSearchQuery] = useState('');

  // View mode state (Issue #2866)
  const [viewMode, setViewMode] = useState<ViewMode>('grid');

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

  const [agentConfigModal, setAgentConfigModal] = useState<{
    isOpen: boolean;
    gameId: string;
    gameTitle: string;
  }>({
    isOpen: false,
    gameId: '',
    gameTitle: '',
  });

  const [pdfUploadModal, setPdfUploadModal] = useState<{
    isOpen: boolean;
    gameId: string;
    gameTitle: string;
  }>({
    isOpen: false,
    gameId: '',
    gameTitle: '',
  });

  // Ask Agent modal state (Issue #3185)
  const [_askAgentModal, _setAskAgentModal] = useState<{
    isOpen: boolean;
    gameId: string;
  }>({
    isOpen: false,
    gameId: '',
  });

  // Share library modal state (Issue #2614)
  const [shareModalOpen, setShareModalOpen] = useState(false);

  // Bulk selection state (Issue #2613)
  const {
    selectionMode,
    toggleSelectionMode,
    toggleSelection,
    selectAll,
    deselectAll,
    clearSelection,
    selectRange,
    isSelected,
    getSelectedCount,
    getSelectedIds,
  } = useBulkSelectionStore();

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
    setFilters(prev => ({
      ...prev,
      favoritesOnly: enabled,
      stateFilter: enabled ? [] : prev.stateFilter, // Clear state filter when selecting favorites
      page: 1,
    }));
  };

  // State filter handler (Issue #2866)
  const handleStateFilterChange = (states: GameStateType[]) => {
    setFilters(prev => ({
      ...prev,
      stateFilter: states,
      favoritesOnly: states.length > 0 ? false : prev.favoritesOnly, // Clear favorites when selecting states
      page: 1,
    }));
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
      stateFilter: [],
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

  const handleConfigureAgent = (gameId: string, gameTitle: string) => {
    setAgentConfigModal({
      isOpen: true,
      gameId,
      gameTitle,
    });
  };

  const handleUploadPdf = (gameId: string, gameTitle: string) => {
    setPdfUploadModal({
      isOpen: true,
      gameId,
      gameTitle,
    });
  };

  // Handle Ask Agent (Issue #3185)
  // TODO: Replace placeholder with AgentConfigModal or dedicated AskAgentModal when AGT-012 available
  const handleAskAgent = (gameId: string) => {
    // Temporary: Open agent config modal until AGT-012 modal is ready
    setAgentConfigModal({
      isOpen: true,
      gameId,
      gameTitle: '', // Title not critical for config modal
    });
  };

  // Derive data - memoized to ensure stable reference across renders
  // This prevents useMemo dependency warnings for downstream computations
  const games = useMemo(() => libraryData?.items ?? [], [libraryData?.items]);
  const hasGames = games.length > 0;

  // Client-side search filtering (until backend supports it)
  const filteredGames = useMemo(
    () =>
      searchQuery
        ? games.filter(game => game.gameTitle.toLowerCase().includes(searchQuery.toLowerCase()))
        : games,
    [games, searchQuery]
  );

  // Calculate state counts for filter badges (Issue #2866)
  // Must be called before any early returns to follow React hooks rules
  const stateCounts = useMemo(() => {
    return {
      total: games.length,
      favorites: games.filter(g => g.isFavorite).length,
      nuovo: games.filter(g => g.currentState === 'Nuovo').length,
      inPrestito: games.filter(g => g.currentState === 'InPrestito').length,
      wishlist: games.filter(g => g.currentState === 'Wishlist').length,
      owned: games.filter(g => g.currentState === 'Owned').length,
    };
  }, [games]);

  // All game IDs for selection operations (Issue #2613)
  const allGameIds = filteredGames.map(g => g.gameId);

  // Handle game selection (Issue #2613)
  const handleGameSelect = (gameId: string, shiftKey: boolean) => {
    if (shiftKey && allGameIds.length > 0) {
      // Shift+Click: select range
      selectRange(allGameIds, gameId);
    } else {
      // Normal click: toggle single selection
      toggleSelection(gameId);
    }
  };

  // Loading state with staggered skeleton animations (Issue #2618)
  if (libraryLoading || quotaLoading) {
    return (
      <div className="container mx-auto px-4 py-8 space-y-6">
        {/* Quota skeleton */}
        <motion.div
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.3 }}
        >
          <Skeleton className="h-24 w-full" />
        </motion.div>

        {/* Filters skeleton */}
        <motion.div
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.3, delay: 0.1 }}
        >
          <Skeleton className="h-16 w-full" />
        </motion.div>

        {/* Cards grid skeleton with staggered animation */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {Array.from({ length: 6 }).map((_, i) => (
            <motion.div
              key={i}
              initial={{ opacity: 0, y: 20 }}
              animate={{ opacity: 1, y: 0 }}
              transition={{
                duration: 0.3,
                delay: 0.2 + i * 0.08,
                ease: 'easeOut',
              }}
            >
              <Skeleton className="h-64 w-full rounded-lg" />
            </motion.div>
          ))}
        </div>
      </div>
    );
  }

  // Error state
  if (libraryError || quotaError) {
    const errorMessage =
      libraryError instanceof Error ? libraryError.message : String(libraryError || quotaError);

    return (
      <div className="container mx-auto px-4 py-8">
        <Alert variant="destructive">
          <AlertDescription>{errorMessage}</AlertDescription>
        </Alert>
      </div>
    );
  }

  return (
    <>
      {/* Quota Sticky Header (Issue #2869) */}
      {quota && (
        <QuotaStickyHeader
          currentCount={quota.currentCount}
          maxAllowed={quota.maxAllowed}
          percentageUsed={quota.percentageUsed}
          userTier={quota.userTier}
        />
      )}

      <div className="container mx-auto px-4 py-8 space-y-6">
        {/* Page Header */}
      <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
        <h1 className="text-3xl font-bold font-quicksand">La Mia Libreria</h1>
        <div className="flex items-center gap-2">
          {/* View Mode Toggle (Issue #2866) */}
          {hasGames && (
            <ViewModeToggle viewMode={viewMode} onViewModeChange={setViewMode} />
          )}
          {/* Share Library Button (Issue #2614) */}
          {hasGames && (
            <Button variant="outline" onClick={() => setShareModalOpen(true)}>
              <Share2 className="mr-2 h-4 w-4" />
              Condividi
            </Button>
          )}
          {/* Selection Mode Toggle (Issue #2613) */}
          {hasGames && (
            <Button
              variant={selectionMode ? 'secondary' : 'outline'}
              onClick={toggleSelectionMode}
            >
              <CheckSquare className="mr-2 h-4 w-4" />
              {selectionMode ? 'Annulla Selezione' : 'Seleziona'}
            </Button>
          )}
          <Button asChild>
            <Link href="/games/catalog">
              <Plus className="mr-2 h-4 w-4" />
              Aggiungi Gioco
            </Link>
          </Button>
        </div>
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

      {/* Filters (Issue #2866) */}
      {hasGames && (
        <LibraryFilters
          searchQuery={searchQuery}
          onSearchChange={handleSearchChange}
          favoritesOnly={filters.favoritesOnly}
          onFavoritesChange={handleFavoritesChange}
          stateFilter={filters.stateFilter}
          onStateFilterChange={handleStateFilterChange}
          sortBy={filters.sortBy}
          sortDescending={filters.sortDescending}
          onSortChange={handleSortChange}
          onClearFilters={handleClearFilters}
          stateCounts={stateCounts}
        />
      )}

      {/* Library Content (Issue #2866 - Grid/List view support) */}
      {hasGames ? (
        <>
          {filteredGames.length > 0 ? (
            <motion.div
              className={
                viewMode === 'grid'
                  ? 'grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4'
                  : 'flex flex-col gap-3'
              }
              layout
            >
              <AnimatePresence mode="popLayout">
                {filteredGames.map((game) => (
                  <MeepleLibraryGameCard
                    key={game.id}
                    game={game}
                    variant={viewMode === 'grid' ? 'grid' : 'list'}
                    onConfigureAgent={handleConfigureAgent}
                    onUploadPdf={handleUploadPdf}
                    onEditNotes={handleEditNotes}
                    onRemove={handleRemoveGame}
                    onAskAgent={handleAskAgent}
                    selectionMode={selectionMode}
                    isSelected={isSelected(game.gameId)}
                    onSelect={handleGameSelect}
                  />
                ))}
              </AnimatePresence>
            </motion.div>
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
              <Link href="/games/catalog">Esplora Catalogo Giochi</Link>
            </Button>
          </CardContent>
        </Card>
      )}

      {/* Bulk Action Bar (Issue #2613) */}
      {selectionMode && (
        <BulkActionBar
          selectedCount={getSelectedCount()}
          selectedIds={getSelectedIds()}
          allGameIds={allGameIds}
          games={filteredGames}
          onClearSelection={clearSelection}
          onSelectAll={selectAll}
          onDeselectAll={deselectAll}
        />
      )}

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

      {/* Agent Configuration Modal */}
      <AgentConfigModal
        isOpen={agentConfigModal.isOpen}
        onClose={() => setAgentConfigModal(prev => ({ ...prev, isOpen: false }))}
        gameId={agentConfigModal.gameId}
        gameTitle={agentConfigModal.gameTitle}
      />

      {/* PDF Upload Modal */}
      <PdfUploadModal
        isOpen={pdfUploadModal.isOpen}
        onClose={() => setPdfUploadModal(prev => ({ ...prev, isOpen: false }))}
        gameId={pdfUploadModal.gameId}
        gameTitle={pdfUploadModal.gameTitle}
      />

      {/* Share Library Modal (Issue #2614) */}
      <ShareLibraryModal
        isOpen={shareModalOpen}
        onClose={() => setShareModalOpen(false)}
      />
      </div>
    </>
  );
}
