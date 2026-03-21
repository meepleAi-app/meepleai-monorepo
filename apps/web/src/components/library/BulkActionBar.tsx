/**
 * BulkActionBar Component (Issue #2613, #2868, Mobile UX #18)
 *
 * Floating action bar for bulk operations in library.
 * Shows when items are selected, with actions:
 * - Change game state (Nuovo/InPrestito/Wishlist/Owned)
 * - Mark as favorite
 * - Remove from library
 * - Export selected
 *
 * Responsive: Fixed bottom on mobile (with safe area insets), full width on desktop.
 * Style: Glassmorphism design with design system tokens
 */

'use client';

import { useState } from 'react';

import {
  Download,
  Heart,
  Loader2,
  Trash2,
  X,
  CheckSquare,
  Square,
  RefreshCw,
  FileText,
  FileJson,
  ChevronDown,
} from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import { useUpdateLibraryEntry, useUpdateGameState } from '@/hooks/queries/useLibrary';
import type { UserLibraryEntry, GameStateType } from '@/lib/api/schemas/library.schemas';
import { exportLibrary, type ExportFormat, type ExportScope } from '@/lib/export/libraryExport';

import { BulkRemoveDialog } from './BulkRemoveDialog';

// State labels for display
const stateLabels: Record<GameStateType, string> = {
  Nuovo: 'Nuovo',
  InPrestito: 'In Prestito',
  Wishlist: 'Wishlist',
  Owned: 'Posseduto',
};

// State icons colors
const stateColors: Record<GameStateType, string> = {
  Nuovo: 'text-green-600',
  InPrestito: 'text-red-600',
  Wishlist: 'text-yellow-600',
  Owned: 'text-blue-600',
};

export interface BulkActionBarProps {
  selectedCount: number;
  selectedIds: string[];
  allGameIds: string[];
  games: UserLibraryEntry[];
  onClearSelection: () => void;
  onSelectAll: (ids: string[]) => void;
  onDeselectAll: () => void;
}

export function BulkActionBar({
  selectedCount,
  selectedIds,
  allGameIds,
  games,
  onClearSelection,
  onSelectAll,
  onDeselectAll,
}: BulkActionBarProps) {
  const [isFavoriting, setIsFavoriting] = useState(false);
  const [isChangingState, setIsChangingState] = useState(false);
  const [isExporting, setIsExporting] = useState(false);
  const [showRemoveDialog, setShowRemoveDialog] = useState(false);

  const updateEntry = useUpdateLibraryEntry();
  const updateGameState = useUpdateGameState();

  // Get selected games data
  const selectedGames = games.filter(g => selectedIds.includes(g.gameId));
  const selectedTitles = selectedGames.map(g => g.gameTitle);
  const allSelected = selectedCount === allGameIds.length && allGameIds.length > 0;

  const handleBulkFavorite = async () => {
    if (isFavoriting || selectedIds.length === 0) return;

    setIsFavoriting(true);
    try {
      const results = await Promise.allSettled(
        selectedIds.map(gameId =>
          updateEntry.mutateAsync({ gameId, request: { isFavorite: true } })
        )
      );

      const successCount = results.filter(r => r.status === 'fulfilled').length;
      const failCount = results.filter(r => r.status === 'rejected').length;

      if (failCount === 0) {
        toast.success(`${successCount} giochi segnati come preferiti`);
      } else if (successCount > 0) {
        toast.warning(`${successCount} aggiornati, ${failCount} errori`);
      } else {
        toast.error('Impossibile aggiornare i preferiti');
      }

      onClearSelection();
    } catch (_error) {
      toast.error("Errore durante l'aggiornamento");
    } finally {
      setIsFavoriting(false);
    }
  };

  const handleBulkChangeState = async (newState: GameStateType) => {
    if (isChangingState || selectedIds.length === 0) return;

    setIsChangingState(true);
    try {
      const results = await Promise.allSettled(
        selectedIds.map(gameId => updateGameState.mutateAsync({ gameId, request: { newState } }))
      );

      const successCount = results.filter(r => r.status === 'fulfilled').length;
      const failCount = results.filter(r => r.status === 'rejected').length;

      if (failCount === 0) {
        toast.success(`${successCount} giochi aggiornati a "${stateLabels[newState]}"`);
      } else if (successCount > 0) {
        toast.warning(`${successCount} aggiornati, ${failCount} errori`);
      } else {
        toast.error('Impossibile cambiare lo stato');
      }

      onClearSelection();
    } catch (_error) {
      toast.error('Errore durante il cambio di stato');
    } finally {
      setIsChangingState(false);
    }
  };

  const handleExport = async (format: ExportFormat, scope: ExportScope) => {
    if (isExporting || selectedGames.length === 0) return;

    setIsExporting(true);
    try {
      exportLibrary(selectedGames, {
        format,
        scope,
        filename: `libreria-selezionati-${new Date().toISOString().split('T')[0]}.${format}`,
      });
      const formatLabel = format === 'csv' ? 'CSV' : 'JSON';
      toast.success(`${selectedGames.length} giochi esportati (${formatLabel})`);
    } catch (_error) {
      toast.error("Errore durante l'esportazione");
    } finally {
      setIsExporting(false);
    }
  };

  const handleRemoveSuccess = () => {
    onClearSelection();
  };

  const handleToggleSelectAll = () => {
    if (allSelected) {
      onDeselectAll();
    } else {
      onSelectAll(allGameIds);
    }
  };

  if (selectedCount === 0) return null;

  return (
    <>
      {/* Floating Action Bar - Glassmorphism design (Mobile UX #18) */}
      <div
        data-testid="bulk-action-bar"
        className="fixed left-0 right-0 z-50 px-4 bottom-[calc(1.5rem+env(safe-area-inset-bottom))] md:bottom-6 animate-in fade-in-0 slide-in-from-bottom-4 duration-300"
      >
        <div className="mx-auto max-w-4xl">
          <div className="bg-card/85 backdrop-blur-md backdrop-saturate-150 border border-border/60 rounded-2xl shadow-lg shadow-black/10 p-3 flex items-center justify-between gap-3">
            {/* Left: Selection info */}
            <div className="flex items-center gap-3">
              <Button
                variant="ghost"
                size="sm"
                onClick={onClearSelection}
                className="h-8 w-8 p-0 text-foreground/70 hover:bg-muted"
                aria-label="Esci dalla selezione"
              >
                <X className="h-4 w-4" />
              </Button>
              <span className="text-sm font-medium text-foreground font-nunito">
                {selectedCount} selezionati
              </span>
              <Button
                variant="ghost"
                size="sm"
                onClick={handleToggleSelectAll}
                className="hidden sm:flex text-foreground/70 hover:bg-muted"
              >
                {allSelected ? (
                  <>
                    <Square className="mr-1 h-4 w-4" />
                    Deseleziona tutti
                  </>
                ) : (
                  <>
                    <CheckSquare className="mr-1 h-4 w-4" />
                    Seleziona tutti
                  </>
                )}
              </Button>
            </div>

            {/* Right: Actions */}
            <div className="flex items-center gap-2">
              {/* Change State Dropdown (Issue #2868) */}
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button
                    variant="secondary"
                    size="sm"
                    disabled={isChangingState}
                    className="hidden sm:flex bg-muted text-foreground hover:bg-muted/80"
                  >
                    {isChangingState ? (
                      <Loader2 className="mr-1 h-4 w-4 animate-spin" />
                    ) : (
                      <RefreshCw className="mr-1 h-4 w-4" />
                    )}
                    Cambia Stato
                    <ChevronDown className="ml-1 h-3 w-3" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem onClick={() => handleBulkChangeState('Nuovo')}>
                    <RefreshCw className={`mr-2 h-4 w-4 ${stateColors.Nuovo}`} />
                    {stateLabels.Nuovo}
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => handleBulkChangeState('InPrestito')}>
                    <RefreshCw className={`mr-2 h-4 w-4 ${stateColors.InPrestito}`} />
                    {stateLabels.InPrestito}
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => handleBulkChangeState('Wishlist')}>
                    <RefreshCw className={`mr-2 h-4 w-4 ${stateColors.Wishlist}`} />
                    {stateLabels.Wishlist}
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => handleBulkChangeState('Owned')}>
                    <RefreshCw className={`mr-2 h-4 w-4 ${stateColors.Owned}`} />
                    {stateLabels.Owned}
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>

              {/* Mobile: Change State Icon-only */}
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button
                    variant="secondary"
                    size="icon"
                    disabled={isChangingState}
                    className="sm:hidden h-8 w-8 min-w-[44px] min-h-[44px] bg-muted text-foreground hover:bg-muted/80"
                    aria-label="Cambia Stato"
                  >
                    {isChangingState ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <RefreshCw className="h-4 w-4" />
                    )}
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem onClick={() => handleBulkChangeState('Nuovo')}>
                    <RefreshCw className={`mr-2 h-4 w-4 ${stateColors.Nuovo}`} />
                    {stateLabels.Nuovo}
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => handleBulkChangeState('InPrestito')}>
                    <RefreshCw className={`mr-2 h-4 w-4 ${stateColors.InPrestito}`} />
                    {stateLabels.InPrestito}
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => handleBulkChangeState('Wishlist')}>
                    <RefreshCw className={`mr-2 h-4 w-4 ${stateColors.Wishlist}`} />
                    {stateLabels.Wishlist}
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => handleBulkChangeState('Owned')}>
                    <RefreshCw className={`mr-2 h-4 w-4 ${stateColors.Owned}`} />
                    {stateLabels.Owned}
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>

              {/* Favorite Button */}
              <Button
                variant="secondary"
                size="sm"
                onClick={handleBulkFavorite}
                disabled={isFavoriting}
                className="hidden sm:flex bg-muted text-foreground hover:bg-muted/80"
              >
                {isFavoriting ? (
                  <Loader2 className="mr-1 h-4 w-4 animate-spin" />
                ) : (
                  <Heart className="mr-1 h-4 w-4" />
                )}
                Preferiti
              </Button>

              {/* Mobile: Icon-only buttons */}
              <Button
                variant="secondary"
                size="icon"
                onClick={handleBulkFavorite}
                disabled={isFavoriting}
                className="sm:hidden h-8 w-8 min-w-[44px] min-h-[44px] bg-muted text-foreground hover:bg-muted/80"
                aria-label="Segna come preferiti"
              >
                {isFavoriting ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Heart className="h-4 w-4" />
                )}
              </Button>

              {/* Export Dropdown */}
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button
                    variant="secondary"
                    size="sm"
                    disabled={isExporting}
                    className="hidden sm:flex bg-muted text-foreground hover:bg-muted/80"
                  >
                    {isExporting ? (
                      <Loader2 className="mr-1 h-4 w-4 animate-spin" />
                    ) : (
                      <Download className="mr-1 h-4 w-4" />
                    )}
                    Esporta
                    <ChevronDown className="ml-1 h-3 w-3" />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem onClick={() => handleExport('csv', 'minimal')}>
                    <FileText className="mr-2 h-4 w-4" />
                    CSV - Base
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => handleExport('csv', 'full')}>
                    <FileText className="mr-2 h-4 w-4" />
                    CSV - Completo
                  </DropdownMenuItem>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem onClick={() => handleExport('json', 'minimal')}>
                    <FileJson className="mr-2 h-4 w-4" />
                    JSON - Base
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => handleExport('json', 'full')}>
                    <FileJson className="mr-2 h-4 w-4" />
                    JSON - Completo
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>

              {/* Mobile Export */}
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button
                    variant="secondary"
                    size="icon"
                    disabled={isExporting}
                    className="sm:hidden h-8 w-8 min-w-[44px] min-h-[44px] bg-muted text-foreground hover:bg-muted/80"
                    aria-label="Esporta"
                  >
                    {isExporting ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <Download className="h-4 w-4" />
                    )}
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem onClick={() => handleExport('csv', 'minimal')}>
                    CSV - Base
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => handleExport('csv', 'full')}>
                    CSV - Completo
                  </DropdownMenuItem>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem onClick={() => handleExport('json', 'minimal')}>
                    JSON - Base
                  </DropdownMenuItem>
                  <DropdownMenuItem onClick={() => handleExport('json', 'full')}>
                    JSON - Completo
                  </DropdownMenuItem>
                </DropdownMenuContent>
              </DropdownMenu>

              {/* Remove Button */}
              <Button
                variant="secondary"
                size="sm"
                onClick={() => setShowRemoveDialog(true)}
                className="hidden sm:flex bg-destructive/10 text-destructive hover:bg-destructive/20"
              >
                <Trash2 className="mr-1 h-4 w-4" />
                Rimuovi
              </Button>

              <Button
                variant="secondary"
                size="icon"
                onClick={() => setShowRemoveDialog(true)}
                className="sm:hidden h-8 w-8 min-w-[44px] min-h-[44px] bg-destructive/10 text-destructive hover:bg-destructive/20"
                aria-label="Rimuovi"
              >
                <Trash2 className="h-4 w-4" />
              </Button>
            </div>
          </div>
        </div>
      </div>

      {/* Remove Confirmation Dialog */}
      <BulkRemoveDialog
        isOpen={showRemoveDialog}
        onClose={() => setShowRemoveDialog(false)}
        gameIds={selectedIds}
        gameTitles={selectedTitles}
        onSuccess={handleRemoveSuccess}
      />
    </>
  );
}
