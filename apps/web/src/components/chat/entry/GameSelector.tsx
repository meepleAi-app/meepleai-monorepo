/**
 * GameSelector — Two-tab game picker (private games / shared library)
 *
 * Loads private games on mount, lazy-loads shared library on first tab switch.
 * Provides search filtering and a "skip game" option for generic chat.
 */

'use client';

import React, { useEffect, useState, useMemo, useCallback } from 'react';

import { Gamepad2, Search } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { api } from '@/lib/api';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';
import type { PrivateGameDto } from '@/lib/api/schemas/private-games.schemas';
import { cn } from '@/lib/utils';
import type { Game } from '@/types';

// ── Type Adapters ───────────────────────────────────────────────────────────

function privateGameToGame(pg: PrivateGameDto): Game {
  return { id: pg.id, title: pg.title, createdAt: pg.createdAt };
}

function libraryEntryToGame(entry: UserLibraryEntry): Game {
  return { id: entry.gameId, title: entry.gameTitle, createdAt: entry.addedAt };
}

// ── Sub-components ──────────────────────────────────────────────────────────

function GameGrid({
  games,
  selectedGameId,
  onSelect,
  isLoading,
}: {
  games: Game[];
  selectedGameId: string | null;
  onSelect: (gameId: string) => void;
  isLoading: boolean;
}) {
  const [search, setSearch] = useState('');

  const filtered = useMemo(() => {
    if (!search.trim()) return games;
    const lower = search.toLowerCase();
    return games.filter(g => g.title.toLowerCase().includes(lower));
  }, [games, search]);

  if (isLoading) {
    return (
      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
        {Array.from({ length: 8 }).map((_, i) => (
          <div key={i} className="h-24 rounded-xl bg-muted/50 animate-pulse" />
        ))}
      </div>
    );
  }

  return (
    <div>
      {/* Search */}
      <div className="relative mb-3">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <input
          type="text"
          placeholder="Cerca gioco..."
          value={search}
          onChange={e => setSearch(e.target.value)}
          className="w-full pl-9 pr-3 py-2 rounded-lg border border-border/50 bg-white/70 dark:bg-card/70 backdrop-blur-md text-sm font-nunito placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-amber-500/40"
          data-testid="game-search-input"
        />
      </div>

      {/* Grid */}
      <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3 max-h-64 overflow-y-auto pr-1">
        {filtered.length === 0 ? (
          <p className="col-span-full text-sm text-muted-foreground text-center py-4">
            Nessun gioco trovato
          </p>
        ) : (
          filtered.map(game => (
            <button
              key={game.id}
              onClick={() => onSelect(game.id)}
              className={cn(
                'text-left rounded-xl transition-all duration-200 focus:outline-none focus:ring-2 focus:ring-amber-500/40',
                selectedGameId === game.id
                  ? 'ring-2 ring-amber-500 scale-[1.02]'
                  : 'hover:scale-[1.01]'
              )}
              aria-pressed={selectedGameId === game.id}
              data-testid={`game-card-${game.id}`}
            >
              <MeepleCard
                entity="game"
                variant="compact"
                title={game.title}
                className={cn(selectedGameId === game.id && 'border-amber-500')}
              />
            </button>
          ))
        )}
      </div>

      {/* Skip game selection */}
      <button
        onClick={() => onSelect('')}
        className={cn(
          'mt-3 w-full py-2 px-3 rounded-lg text-xs text-muted-foreground border border-dashed border-border/50 transition-colors',
          'hover:bg-muted/50 hover:text-foreground',
          selectedGameId === '' && 'bg-muted/50 text-foreground border-solid'
        )}
        data-testid="skip-game-btn"
      >
        Continua senza gioco (chat generica)
      </button>
    </div>
  );
}

// ── Main Component ──────────────────────────────────────────────────────────

export interface GameSelectorProps {
  onSelect: (gameId: string) => void;
  selectedGameId: string | null;
  /** If true, shared-tab only shows games with KB ready */
  showOnlyWithKb?: boolean;
  className?: string;
  /** Expose loaded games for thread-title resolution */
  onGamesLoaded?: (games: Game[]) => void;
}

export function GameSelector({
  onSelect,
  selectedGameId,
  showOnlyWithKb = true,
  className,
  onGamesLoaded,
}: GameSelectorProps) {
  const [activeTab, setActiveTab] = useState<'private' | 'shared'>('private');
  const [privateGames, setPrivateGames] = useState<Game[]>([]);
  const [sharedGames, setSharedGames] = useState<Game[]>([]);
  const [isLoadingPrivate, setIsLoadingPrivate] = useState(true);
  const [isLoadingShared, setIsLoadingShared] = useState(false);
  const [sharedLoaded, setSharedLoaded] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Load private games on mount
  useEffect(() => {
    let cancelled = false;
    setIsLoadingPrivate(true);

    api.library
      .getPrivateGames({ pageSize: 100 })
      .then(res => {
        if (!cancelled) {
          const games = (res.items ?? []).map(privateGameToGame);
          setPrivateGames(games);
          onGamesLoaded?.(games);
        }
      })
      .catch(() => {
        if (!cancelled) setError('Errore nel caricamento dei dati');
      })
      .finally(() => {
        if (!cancelled) setIsLoadingPrivate(false);
      });

    return () => {
      cancelled = true;
    };
  }, [onGamesLoaded]);

  // Lazy-load shared library on first tab switch
  useEffect(() => {
    if (activeTab !== 'shared' || sharedLoaded) return;
    let cancelled = false;
    setIsLoadingShared(true);

    api.library
      .getLibrary({ pageSize: 100 })
      .then(res => {
        if (!cancelled) {
          const items = res.items ?? [];
          const filtered = showOnlyWithKb ? items.filter(e => e.hasKb) : items;
          const games = filtered.map(libraryEntryToGame);
          setSharedGames(games);
          onGamesLoaded?.(games);
        }
      })
      .catch(() => {
        if (!cancelled) setError('Errore nel caricamento della libreria');
      })
      .finally(() => {
        if (!cancelled) {
          setIsLoadingShared(false);
          setSharedLoaded(true);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [activeTab, sharedLoaded, showOnlyWithKb, onGamesLoaded]);

  const activeGames = activeTab === 'private' ? privateGames : sharedGames;
  const isLoading = activeTab === 'private' ? isLoadingPrivate : isLoadingShared;

  const handleSelect = useCallback(
    (gameId: string) => {
      onSelect(gameId);
    },
    [onSelect]
  );

  return (
    <section
      className={cn(
        'p-6 rounded-2xl bg-white/70 dark:bg-card/70 backdrop-blur-md border border-border/50',
        className
      )}
      data-testid="game-selection-section"
    >
      <div className="flex items-center gap-2 mb-4">
        <Gamepad2 className="h-5 w-5 text-amber-600 dark:text-amber-400" />
        <h2 className="text-lg font-semibold font-quicksand text-foreground">Seleziona un gioco</h2>
      </div>

      {error && (
        <div
          role="alert"
          className="mb-4 p-3 bg-red-50 dark:bg-red-500/10 text-red-700 dark:text-red-400 rounded-lg text-sm border border-red-200 dark:border-red-500/20"
        >
          {error}
        </div>
      )}

      {/* Tabs */}
      <div
        className="flex gap-1 mb-4 p-1 rounded-lg bg-muted/50"
        data-testid="game-source-tabs"
        role="tablist"
      >
        <button
          role="tab"
          aria-selected={activeTab === 'private'}
          onClick={() => setActiveTab('private')}
          className={cn(
            'flex-1 py-1.5 px-3 rounded-md text-sm font-nunito font-medium transition-all duration-200',
            activeTab === 'private'
              ? 'bg-white dark:bg-card shadow-sm text-foreground'
              : 'text-muted-foreground hover:text-foreground'
          )}
          data-testid="tab-private-games"
        >
          I miei giochi
        </button>
        <button
          role="tab"
          aria-selected={activeTab === 'shared'}
          onClick={() => setActiveTab('shared')}
          className={cn(
            'flex-1 py-1.5 px-3 rounded-md text-sm font-nunito font-medium transition-all duration-200',
            activeTab === 'shared'
              ? 'bg-white dark:bg-card shadow-sm text-foreground'
              : 'text-muted-foreground hover:text-foreground'
          )}
          data-testid="tab-shared-games"
        >
          Libreria condivisa
        </button>
      </div>

      <GameGrid
        games={activeGames}
        selectedGameId={selectedGameId}
        onSelect={handleSelect}
        isLoading={isLoading}
      />
    </section>
  );
}
