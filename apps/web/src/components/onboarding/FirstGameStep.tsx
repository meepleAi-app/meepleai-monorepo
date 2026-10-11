'use client';

/**
 * FirstGameStep Component
 * Issue #132 - Add first game during onboarding
 *
 * Search bar with debounce for game catalog search.
 * Click a result to add it to the user's library.
 */

import { useCallback, useEffect, useId, useRef, useState } from 'react';

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
  // #4155: «I own a copy». Off by default: declaring ownership stays an explicit choice.
  const [ownsCopy, setOwnsCopy] = useState(false);
  const ownsCopyHintId = useId();
  const [errorMessage, setErrorMessage] = useState('');
  const debounceRef = useRef<ReturnType<typeof setTimeout>>(null);
  // #4158: the search runs on the server, one request per settled keystroke, and responses can
  // arrive out of order. Only the response to the latest search may touch the state.
  const latestSearchRef = useRef(0);

  // Debounced search
  useEffect(() => {
    if (debounceRef.current) {
      clearTimeout(debounceRef.current);
    }

    const searchId = ++latestSearchRef.current;

    if (query.trim().length < 2) {
      setResults([]);
      // A search still in flight is now stale and will not clear the flag itself.
      setIsSearching(false);
      return;
    }

    debounceRef.current = setTimeout(async () => {
      setIsSearching(true);
      try {
        const response = await api.games.getAll({ search: query.trim() });
        if (searchId !== latestSearchRef.current) return;
        const games = (response.games ?? []).slice(0, 8).map(g => ({
          id: g.id,
          title: g.title,
          publisher: g.publisher,
          yearPublished: g.yearPublished,
        }));
        setResults(games);
      } catch {
        if (searchId !== latestSearchRef.current) return;
        setResults([]);
      } finally {
        if (searchId === latestSearchRef.current) {
          setIsSearching(false);
        }
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
      // #4155: the declaration needs the library entry, so it comes after the add. If it fails the
      // game stays added and onboarding goes on: the chat offers the declaration again.
      if (ownsCopy) {
        try {
          await api.library.declareOwnership(selectedGame.id);
        } catch {
          toast.error(
            `${selectedGame.title} is in your library, but the ownership declaration failed: you can declare it from the chat.`
          );
        }
      }
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
        <h2 className="font-quicksand text-lg font-semibold text-foreground">
          Add Your First Game
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
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
          <div className="absolute right-3 top-9" data-testid="game-search-pending">
            <Search className="h-4 w-4 animate-pulse text-muted-foreground" aria-hidden="true" />
          </div>
        )}

        {/* Search results dropdown */}
        {results.length > 0 && !selectedGame && (
          <ul
            className="absolute z-10 mt-1 w-full rounded-lg border bg-card shadow-lg max-h-60 overflow-y-auto"
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
                  <div className="font-medium text-foreground">{game.title}</div>
                  {(game.publisher || game.yearPublished) && (
                    <div className="text-xs text-muted-foreground">
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
            <div className="font-medium text-foreground">{selectedGame.title}</div>
            {selectedGame.publisher && (
              <div className="text-xs text-muted-foreground">{selectedGame.publisher}</div>
            )}
          </div>
          <button
            type="button"
            onClick={() => {
              setSelectedGame(null);
              setQuery('');
            }}
            className="text-sm text-muted-foreground hover:text-foreground"
            aria-label={`Remove ${selectedGame.title}`}
          >
            Change
          </button>
        </div>
      )}

      {/* #4155: ownership declaration at add time, off by default */}
      {selectedGame && (
        <div className="rounded-lg border border-border bg-muted/30 px-4 py-3 text-sm">
          <label className="flex items-center gap-2 text-foreground">
            <input
              type="checkbox"
              checked={ownsCopy}
              onChange={e => setOwnsCopy(e.target.checked)}
              aria-describedby={ownsCopyHintId}
              className="h-4 w-4"
              data-testid="first-game-owns-copy"
            />
            I own a copy of this game
          </label>
          <p id={ownsCopyHintId} className="mt-1 pl-6 text-xs text-muted-foreground">
            It lets the assistant read the rulebook: rulebooks are copyrighted, and we only read
            them for players who own the game.
          </p>
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
          className="text-sm text-muted-foreground hover:text-foreground"
          data-testid="game-skip"
        >
          Skip for now
        </button>
      </div>
    </div>
  );
}
