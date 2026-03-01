/**
 * Game Selector - Select game from user library or shared catalog
 * Issue #4774: GameSelector API Integration
 *
 * Features:
 * - Fetch games from user library via React Query
 * - Search/filter within results
 * - Visual indicator for games with PDF rulebooks
 * - Badge for games not yet in collection
 * - Controlled component (value/onChange props)
 * - Loading, empty, and error states
 */

'use client';

import { useState, useMemo } from 'react';
import { BookOpen, Loader2, Search, Library, AlertCircle } from 'lucide-react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { useLibrary } from '@/hooks/queries/useLibrary';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

export interface GameSelectorProps {
  /** Currently selected game ID */
  value?: string;
  /** Callback when game changes */
  onChange: (gameId: string, game: UserLibraryEntry | null) => void;
  /** Whether the selector is disabled */
  disabled?: boolean;
  /** Optional placeholder text */
  placeholder?: string;
  /** Optional className for styling */
  className?: string;
}

export function GameSelector({
  value,
  onChange,
  disabled = false,
  placeholder = 'Choose a game...',
  className,
}: GameSelectorProps) {
  const [search, setSearch] = useState('');

  // Fetch user's library games (large page to get all games for selector)
  const { data: libraryData, isLoading, error } = useLibrary({ page: 1, pageSize: 100 });

  const games = libraryData?.items ?? [];

  // Filter games by search term
  const filteredGames = useMemo(() => {
    if (!search.trim()) return games;
    const term = search.toLowerCase();
    return games.filter(
      (g) =>
        g.gameTitle.toLowerCase().includes(term) ||
        g.gamePublisher?.toLowerCase().includes(term)
    );
  }, [games, search]);

  // Find selected game for display
  const selectedGame = games.find((g) => g.gameId === value);

  const handleValueChange = (gameId: string) => {
    const game = games.find((g) => g.gameId === gameId) ?? null;
    onChange(gameId, game);
  };

  if (error) {
    return (
      <div className="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
        <div className="flex items-center gap-2">
          <AlertCircle className="h-4 w-4 text-red-500" />
          <p className="text-sm text-red-700 dark:text-red-300">
            Failed to load games. Please try again.
          </p>
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-2">
      <label className="block text-sm font-medium">
        Select Game
        <span className="ml-1 text-red-500">*</span>
      </label>

      <Select
        value={value}
        onValueChange={handleValueChange}
        disabled={disabled || isLoading}
      >
        <SelectTrigger className={className} aria-label="Select game">
          {isLoading ? (
            <div className="flex items-center gap-2">
              <Loader2 className="h-4 w-4 animate-spin" />
              <span className="text-muted-foreground">Loading games...</span>
            </div>
          ) : selectedGame ? (
            <div className="flex items-center gap-2">
              <BookOpen className="h-4 w-4 text-orange-500" />
              <span>{selectedGame.gameTitle}</span>
              {selectedGame.hasKb && (
                <span className="text-xs text-green-600 dark:text-green-400">📚</span>
              )}
            </div>
          ) : (
            <SelectValue placeholder={placeholder} />
          )}
        </SelectTrigger>

        <SelectContent>
          {/* Search input */}
          {games.length > 5 && (
            <div className="px-2 pb-2">
              <div className="flex items-center gap-2 px-2 py-1.5 rounded-md bg-muted/50">
                <Search className="h-3.5 w-3.5 text-muted-foreground" />
                <input
                  type="text"
                  value={search}
                  onChange={(e) => setSearch(e.target.value)}
                  placeholder="Search games..."
                  className="flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground"
                  // Prevent Select from closing on input interaction
                  onKeyDown={(e) => e.stopPropagation()}
                />
              </div>
            </div>
          )}

          {filteredGames.length > 0 ? (
            filteredGames.map((game) => (
              <SelectItem
                key={game.gameId}
                value={game.gameId}
                className="cursor-pointer"
              >
                <div className="flex items-center justify-between gap-3 w-full">
                  <div className="flex items-center gap-2 flex-1 min-w-0">
                    <BookOpen className="h-4 w-4 text-orange-500 shrink-0" />
                    <div className="flex flex-col min-w-0">
                      <span className="font-medium truncate">{game.gameTitle}</span>
                      {game.gamePublisher && (
                        <span className="text-xs text-muted-foreground truncate">
                          {game.gamePublisher}
                        </span>
                      )}
                    </div>
                  </div>
                  <div className="flex items-center gap-1.5 shrink-0">
                    {game.hasKb && (
                      <span className="px-1.5 py-0.5 rounded text-[10px] bg-green-100 dark:bg-green-500/20 text-green-700 dark:text-green-300">
                        📚 Rulebook
                      </span>
                    )}
                    {game.isFavorite && (
                      <span className="text-amber-500">★</span>
                    )}
                  </div>
                </div>
              </SelectItem>
            ))
          ) : (
            <div className="p-3 text-sm text-muted-foreground text-center">
              {search.trim() ? (
                <p>No games matching &quot;{search}&quot;</p>
              ) : (
                <div className="flex flex-col items-center gap-2">
                  <Library className="h-8 w-8 text-muted-foreground/50" />
                  <p>No games in your library</p>
                  <p className="text-xs">Add games to your collection first</p>
                </div>
              )}
            </div>
          )}
        </SelectContent>
      </Select>

      {!value && !isLoading && (
        <p className="text-xs text-muted-foreground">
          Select a game from your library to create an agent for it.
        </p>
      )}
    </div>
  );
}
