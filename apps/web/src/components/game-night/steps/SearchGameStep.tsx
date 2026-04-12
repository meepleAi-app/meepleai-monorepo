'use client';

/**
 * SearchGameStep — Step 1 of GameNightWizard.
 *
 * Searches shared game catalog.
 * Returns game ID and title for subsequent steps.
 *
 * Issue #123 — Game Night Quick Start Wizard
 * Game Night MVP hardening F1 — PDF-aware soft filter:
 *   each result shows a GameKbBadge (AI pronto / Solo manuale) and, when a
 *   non-indexed game is selected, a GameKbWarning is surfaced below the list.
 */

import { useCallback, useState } from 'react';

import { Loader2, Search } from 'lucide-react';
import Image from 'next/image';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import { useGameKbStatus } from '@/lib/domain-hooks/useGameKbStatus';
import { cn } from '@/lib/utils';

import { GameKbBadge, GameKbWarning } from '../GameKbBadge';

// ============================================================================
// Types
// ============================================================================

interface SearchGameStepProps {
  onGameFound: (data: { gameId?: string; privateGameId?: string; gameTitle: string }) => void;
}

interface GameResult {
  id: string;
  title: string;
  thumbnailUrl?: string;
  source: 'catalog';
  yearPublished?: number;
}

// ============================================================================
// GameOption sub-component (hook must live at top level — Rules of Hooks)
// ============================================================================

interface GameOptionProps {
  result: GameResult;
  isSelected: boolean;
  onSelect: (result: GameResult) => void;
}

function GameOption({ result, isSelected, onSelect }: GameOptionProps) {
  const kb = useGameKbStatus(result.id);

  return (
    <li>
      <button
        type="button"
        data-testid="game-option"
        data-game-id={result.id}
        aria-pressed={isSelected}
        onClick={() => onSelect(result)}
        className={cn(
          'w-full flex items-center gap-3 p-3 rounded-lg border',
          'hover:border-amber-500/50 hover:bg-amber-50/50 dark:hover:bg-amber-900/10',
          'transition-colors text-left',
          isSelected && 'border-amber-500 bg-amber-50/70 dark:bg-amber-900/20'
        )}
      >
        {result.thumbnailUrl ? (
          <Image
            src={result.thumbnailUrl}
            alt=""
            width={48}
            height={48}
            className="rounded object-cover flex-shrink-0"
          />
        ) : (
          <div className="h-12 w-12 rounded bg-slate-200 dark:bg-slate-700 flex-shrink-0" />
        )}
        <div className="min-w-0 flex-1">
          <p className="font-medium text-sm truncate">{result.title}</p>
          <p className="text-xs text-muted-foreground">
            {result.yearPublished && `${result.yearPublished} · `}
            Catalogo MeepleAI
          </p>
        </div>
        <GameKbBadge isIndexed={kb.isIndexed} isLoading={kb.isLoading} />
      </button>
    </li>
  );
}

// ============================================================================
// Component
// ============================================================================

export function SearchGameStep({ onGameFound }: SearchGameStepProps) {
  const [query, setQuery] = useState('');
  const [results, setResults] = useState<GameResult[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);
  const [selected, setSelected] = useState<GameResult | null>(null);

  const handleSearch = useCallback(async () => {
    if (!query.trim()) return;
    setIsSearching(true);
    setHasSearched(true);
    setSelected(null);

    try {
      // Search catalog first
      const catalogResponse = await api.sharedGames.search({
        searchTerm: query.trim(),
        page: 1,
        pageSize: 5,
        status: 1, // Published
      });

      const catalogResults: GameResult[] = (catalogResponse.items ?? []).map(g => ({
        id: g.id,
        title: g.title,
        thumbnailUrl: g.thumbnailUrl ?? undefined,
        source: 'catalog' as const,
        yearPublished: g.yearPublished ?? undefined,
      }));

      setResults(catalogResults);
    } catch {
      setResults([]);
    } finally {
      setIsSearching(false);
    }
  }, [query]);

  const handleSelect = useCallback((result: GameResult) => {
    setSelected(result);
  }, []);

  const handleConfirm = useCallback(() => {
    if (!selected) return;
    onGameFound({ gameId: selected.id, gameTitle: selected.title });
  }, [onGameFound, selected]);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter') {
        e.preventDefault();
        handleSearch();
      }
    },
    [handleSearch]
  );

  return (
    <div className="space-y-4" data-testid="search-game-step">
      <div>
        <h3 className="font-quicksand font-bold text-lg text-slate-900 dark:text-slate-100">
          Trova il gioco
        </h3>
        <p className="text-sm text-muted-foreground mt-1">Cerca nel catalogo MeepleAI.</p>
      </div>

      <div className="flex gap-2">
        <Input
          placeholder="Nome del gioco..."
          value={query}
          onChange={e => setQuery(e.target.value)}
          onKeyDown={handleKeyDown}
          data-testid="game-search-input"
        />
        <Button
          onClick={handleSearch}
          disabled={isSearching || !query.trim()}
          aria-label={isSearching ? 'Ricerca in corso' : 'Cerca'}
        >
          {isSearching ? (
            <Loader2 className="h-4 w-4 animate-spin" aria-hidden="true" />
          ) : (
            <Search className="h-4 w-4" aria-hidden="true" />
          )}
        </Button>
      </div>

      {/* Results */}
      {isSearching && (
        <div className="flex items-center justify-center py-6">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      )}

      {!isSearching && results.length > 0 && (
        <ul className="space-y-2" data-testid="game-search-results">
          {results.map(result => (
            <GameOption
              key={`${result.source}-${result.id}`}
              result={result}
              isSelected={selected?.id === result.id}
              onSelect={handleSelect}
            />
          ))}
        </ul>
      )}

      {selected && <GameKbWarning gameId={selected.id} />}

      {selected && (
        <div className="flex justify-end">
          <Button type="button" onClick={handleConfirm} data-testid="confirm-game-button">
            Continua con {selected.title}
          </Button>
        </div>
      )}

      {!isSearching && hasSearched && results.length === 0 && (
        <p className="text-sm text-muted-foreground text-center py-4">
          Nessun risultato. Prova con un altro nome.
        </p>
      )}
    </div>
  );
}
