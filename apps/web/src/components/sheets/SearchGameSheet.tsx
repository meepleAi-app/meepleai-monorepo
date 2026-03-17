/**
 * SearchGameSheet - Quick game search sheet for placeholder action card
 *
 * Responsive layout:
 * - Mobile: Bottom sheet
 * - Desktop: Right drawer, 480px max-width
 *
 * Shows search results from user library ("La mia collezione") and shared
 * catalog ("Catalogo shared"). Click on a result navigates to game detail.
 */

'use client';

import { useState, useMemo, useCallback } from 'react';

import { Search, X, Loader2, BookOpen, Users, AlertCircle } from 'lucide-react';
import { useRouter } from 'next/navigation';

import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/navigation/sheet';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { useSharedGames } from '@/hooks/queries/useSharedGames';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import type { SharedGame } from '@/lib/api/schemas/shared-games.schemas';

// --- Types ---

export interface SearchGameSheetProps {
  isOpen: boolean;
  onClose: () => void;
}

type SearchScope = 'library' | 'catalog';

interface GameResultItem {
  id: string;
  title: string;
  publisher: string | null;
  minPlayers: number | null;
  maxPlayers: number | null;
  kbCount: number;
  source: SearchScope;
  href: string;
}

// --- Helpers ---

function fromLibraryEntry(entry: UserLibraryEntry): GameResultItem {
  return {
    id: entry.gameId,
    title: entry.gameTitle,
    publisher: entry.gamePublisher ?? null,
    minPlayers: entry.minPlayers ?? null,
    maxPlayers: entry.maxPlayers ?? null,
    kbCount: entry.kbCardCount ?? 0,
    source: 'library',
    href: `/library/${entry.gameId}`,
  };
}

function fromSharedGame(game: SharedGame): GameResultItem {
  return {
    id: game.id,
    title: game.title,
    publisher: null,
    minPlayers: game.minPlayers,
    maxPlayers: game.maxPlayers,
    kbCount: 0,
    source: 'catalog',
    href: `/games/${game.id}`,
  };
}

// --- Sub-components ---

function ScopeToggle({
  scope,
  onChange,
}: {
  scope: SearchScope;
  onChange: (scope: SearchScope) => void;
}) {
  return (
    <div className="flex gap-1 p-1 bg-muted rounded-lg">
      <button
        type="button"
        className={`flex-1 px-3 py-1.5 rounded-md text-sm font-medium transition-colors ${
          scope === 'library'
            ? 'bg-background shadow text-foreground'
            : 'text-muted-foreground hover:text-foreground'
        }`}
        onClick={() => onChange('library')}
      >
        La mia collezione
      </button>
      <button
        type="button"
        className={`flex-1 px-3 py-1.5 rounded-md text-sm font-medium transition-colors ${
          scope === 'catalog'
            ? 'bg-background shadow text-foreground'
            : 'text-muted-foreground hover:text-foreground'
        }`}
        onClick={() => onChange('catalog')}
      >
        Catalogo shared
      </button>
    </div>
  );
}

function GameResultRow({
  item,
  onClick,
}: {
  item: GameResultItem;
  onClick: (item: GameResultItem) => void;
}) {
  const playerRange =
    item.minPlayers != null && item.maxPlayers != null
      ? item.minPlayers === item.maxPlayers
        ? `${item.minPlayers} giocatori`
        : `${item.minPlayers}–${item.maxPlayers} giocatori`
      : null;

  return (
    <button
      type="button"
      className="w-full flex items-center gap-3 px-4 py-3 hover:bg-muted/50 transition-colors text-left"
      onClick={() => onClick(item)}
    >
      <div className="h-9 w-9 rounded-md bg-muted flex items-center justify-center shrink-0">
        <BookOpen className="h-4 w-4 text-muted-foreground" />
      </div>
      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium truncate">{item.title}</p>
        <div className="flex items-center gap-2 mt-0.5">
          {item.publisher && (
            <span className="text-xs text-muted-foreground truncate">{item.publisher}</span>
          )}
          {playerRange && (
            <span className="flex items-center gap-1 text-xs text-muted-foreground shrink-0">
              <Users className="h-3 w-3" />
              {playerRange}
            </span>
          )}
        </div>
      </div>
      {item.kbCount > 0 && (
        <Badge variant="secondary" className="shrink-0 text-xs">
          📚 {item.kbCount}
        </Badge>
      )}
    </button>
  );
}

// --- Main Component ---

export function SearchGameSheet({ isOpen, onClose }: SearchGameSheetProps) {
  const router = useRouter();
  const [query, setQuery] = useState('');
  const [scope, setScope] = useState<SearchScope>('library');

  // Library query — always enabled when sheet is open
  const {
    data: libraryData,
    isLoading: isLibraryLoading,
    error: libraryError,
  } = useLibrary({ page: 1, pageSize: 100 }, isOpen && scope === 'library');

  // Catalog query — debounce-free: fires when scope === catalog and query length >= 2
  const catalogSearchEnabled = isOpen && scope === 'catalog' && query.trim().length >= 2;
  const {
    data: catalogData,
    isLoading: isCatalogLoading,
    error: catalogError,
  } = useSharedGames(
    catalogSearchEnabled ? { searchTerm: query.trim(), page: 1, pageSize: 20 } : undefined,
    catalogSearchEnabled
  );

  // Derive results
  const results = useMemo<GameResultItem[]>(() => {
    if (scope === 'library') {
      const items = (libraryData?.items ?? []).map(fromLibraryEntry);
      if (!query.trim()) return items;
      const term = query.toLowerCase();
      return items.filter(
        i =>
          i.title.toLowerCase().includes(term) ||
          (i.publisher?.toLowerCase().includes(term) ?? false)
      );
    }

    // catalog scope
    return (catalogData?.items ?? []).map(fromSharedGame);
  }, [scope, libraryData, catalogData, query]);

  const isLoading = scope === 'library' ? isLibraryLoading : isCatalogLoading;
  const error = scope === 'library' ? libraryError : catalogError;

  const handleResultClick = useCallback(
    (item: GameResultItem) => {
      onClose();
      router.push(item.href);
    },
    [onClose, router]
  );

  const handleClose = useCallback(() => {
    setQuery('');
    setScope('library');
    onClose();
  }, [onClose]);

  // --- Render ---

  return (
    <Sheet open={isOpen} onOpenChange={open => !open && handleClose()}>
      <SheetContent side="right" className="w-full sm:max-w-[480px] flex flex-col p-0">
        {/* Header */}
        <SheetHeader className="border-b px-6 py-4">
          <div className="flex items-center justify-between">
            <SheetTitle className="text-lg font-semibold">Cerca Gioco</SheetTitle>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8"
              onClick={handleClose}
              aria-label="Chiudi"
            >
              <X className="h-4 w-4" />
            </Button>
          </div>
        </SheetHeader>

        {/* Search controls */}
        <div className="px-4 py-3 space-y-3 border-b">
          {/* Search input */}
          <div className="relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <Input
              type="search"
              value={query}
              onChange={e => setQuery(e.target.value)}
              placeholder={
                scope === 'library' ? 'Cerca nella tua collezione...' : 'Cerca nel catalogo...'
              }
              className="pl-9"
              autoFocus
            />
          </div>

          {/* Scope toggles */}
          <ScopeToggle scope={scope} onChange={setScope} />
        </div>

        {/* Results */}
        <div className="flex-1 overflow-y-auto">
          {isLoading && (
            <div className="flex items-center justify-center py-12">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          )}

          {error && !isLoading && (
            <div className="flex items-center gap-2 mx-4 mt-4 p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
              <AlertCircle className="h-4 w-4 text-red-500 shrink-0" />
              <p className="text-sm text-red-700 dark:text-red-300">
                Impossibile caricare i giochi. Riprova.
              </p>
            </div>
          )}

          {!isLoading && !error && results.length === 0 && (
            <div className="flex flex-col items-center justify-center py-12 text-center px-4">
              <BookOpen className="h-10 w-10 text-muted-foreground/40 mb-3" />
              {scope === 'catalog' && query.trim().length < 2 ? (
                <p className="text-sm text-muted-foreground">
                  Digita almeno 2 caratteri per cercare nel catalogo.
                </p>
              ) : (
                <p className="text-sm text-muted-foreground">
                  Nessun gioco trovato
                  {query.trim() ? ` per "${query}"` : ''}.
                </p>
              )}
            </div>
          )}

          {!isLoading && !error && results.length > 0 && (
            <div className="divide-y">
              {results.map(item => (
                <GameResultRow key={item.id} item={item} onClick={handleResultClick} />
              ))}
            </div>
          )}
        </div>
      </SheetContent>
    </Sheet>
  );
}
