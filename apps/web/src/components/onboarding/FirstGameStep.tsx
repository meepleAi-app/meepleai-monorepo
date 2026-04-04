'use client';

/**
 * FirstGameStep Component
 * Issue #132 - Add first game during onboarding
 *
 * Search bar with debounce for game catalog search.
 * Click a result to add it to the user's library.
 */

import { useCallback, useEffect, useRef, useState } from 'react';

import { Search } from 'lucide-react';
import { toast } from 'sonner';

import { AccessibleButton, AccessibleFormInput } from '@/components/accessible';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';
import { getErrorMessage } from '@/lib/utils/errorHandler';

export interface FirstGameStepProps {
  onComplete: () => void;
  onSkip: () => void;
  onGameAdded: (gameId: string, gameName: string) => void;
}

interface GameResult {
  id: string;
  title: string;
  publisher?: string | null;
  yearPublished?: number | null;
}

export function FirstGameStep({ onComplete, onSkip, onGameAdded }: FirstGameStepProps) {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<GameResult[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [selectedGame, setSelectedGame] = useState<GameResult | null>(null);
  const [isAdding, setIsAdding] = useState(false);
  const [errorMessage, setErrorMessage] = useState('');
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(null);

  // Debounced search
  useEffect(() => {
    if (debounceRef.current) {
      clearTimeout(debounceRef.current);
    }

    if (query.trim().length < 2) {
      setResults([]);
      return;
    }

    debounceRef.current = setTimeout(async () => {
      setIsSearching(true);
      try {
        const response = await api.games.getAll({ search: query.trim() });
        const games = (response.games ?? []).slice(0, 8).map(g => ({
          id: g.id,
          title: g.title,
          publisher: g.publisher,
          yearPublished: g.yearPublished,
        }));
        setResults(games);
      } catch {
        setResults([]);
      } finally {
        setIsSearching(false);
      }
    }, 400);

    return () => {
      if (debounceRef.current) {
        clearTimeout(debounceRef.current);
      }
    };
  }, [query]);

  const handleSelectGame = useCallback((game: GameResult) => {
    setSelectedGame(game);
    setResults([]);
    setQuery(game.title);
  }, []);

  const handleAddGame = async () => {
    if (!selectedGame) return;

    setIsAdding(true);
    setErrorMessage('');
    try {
      await api.library.addGame(selectedGame.id);
      toast.success(`${selectedGame.title} added to your library!`);
      onGameAdded(selectedGame.id, selectedGame.title);
      onComplete();
    } catch (err) {
      setErrorMessage(getErrorMessage(err, 'Failed to add game to library.'));
    } finally {
      setIsAdding(false);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold text-slate-900">Add Your First Game</h2>
        <p className="mt-1 text-sm text-slate-600">
          Search for a board game to add to your library. You can always add more later.
        </p>
      </div>

      {errorMessage && (
        <div
          role="alert"
          aria-live="polite"
          className="rounded-lg border border-red-200 bg-red-50 px-4 py-3 text-sm text-red-700"
        >
          {errorMessage}
        </div>
      )}

      <div className="relative">
        <AccessibleFormInput
          label="Search games"
          type="text"
          value={query}
          onChange={e => {
            setQuery(e.target.value);
            setSelectedGame(null);
          }}
          placeholder="e.g., Catan, Wingspan, Ticket to Ride..."
          autoComplete="off"
        />
        {isSearching && (
          <div className="absolute right-3 top-9">
            <Search className="h-4 w-4 animate-pulse text-slate-400" aria-hidden="true" />
          </div>
        )}

        {/* Search results dropdown */}
        {results.length > 0 && !selectedGame && (
          <ul
            className="absolute z-10 mt-1 w-full rounded-lg border bg-white shadow-lg max-h-60 overflow-y-auto"
            role="listbox"
            aria-label="Game search results"
            data-testid="game-search-results"
          >
            {results.map(game => (
              <li key={game.id} role="option" aria-selected={false}>
                <button
                  type="button"
                  onClick={() => handleSelectGame(game)}
                  className="w-full px-4 py-3 text-left hover:bg-amber-50 transition-colors border-b last:border-b-0"
                >
                  <div className="font-medium text-slate-900">{game.title}</div>
                  {(game.publisher || game.yearPublished) && (
                    <div className="text-xs text-slate-500">
                      {[game.publisher, game.yearPublished].filter(Boolean).join(' \u2022 ')}
                    </div>
                  )}
                </button>
              </li>
            ))}
          </ul>
        )}
      </div>

      {/* Selected game preview */}
      {selectedGame && (
        <div
          className={cn(
            'flex items-center justify-between rounded-lg border-2 border-amber-300 bg-amber-50 p-4'
          )}
          data-testid="selected-game"
        >
          <div>
            <div className="font-medium text-slate-900">{selectedGame.title}</div>
            {selectedGame.publisher && (
              <div className="text-xs text-slate-500">{selectedGame.publisher}</div>
            )}
          </div>
          <button
            type="button"
            onClick={() => {
              setSelectedGame(null);
              setQuery('');
            }}
            className="text-sm text-slate-500 hover:text-slate-700"
            aria-label={`Remove ${selectedGame.title}`}
          >
            Change
          </button>
        </div>
      )}

      <div className="flex items-center gap-3">
        {selectedGame ? (
          <AccessibleButton
            type="button"
            variant="primary"
            className="flex-1"
            onClick={handleAddGame}
            isLoading={isAdding}
            loadingText="Adding..."
          >
            Add to Library
          </AccessibleButton>
        ) : (
          <div className="flex-1" />
        )}
        <button
          type="button"
          onClick={onSkip}
          className="text-sm text-slate-500 hover:text-slate-700"
          data-testid="game-skip"
        >
          Skip for now
        </button>
      </div>
    </div>
  );
}
