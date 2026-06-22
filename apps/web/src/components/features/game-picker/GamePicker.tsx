'use client';

/**
 * GamePicker — Shared canonical game search/picker widget.
 *
 * Issue #2089 — Unifies 3 divergent search widgets:
 *   - SessionCreationWizard "Cerca nella tua libreria" (was: client-filter, no debounce, silent catch)
 *   - SessionCreationWizard "Nome del gioco" manual entry
 *   - SearchGameStep (was: Enter-only trigger, no debounce, silent catch)
 *
 * Features:
 * - Source prop: 'library' | 'catalog' | 'both'
 * - Debounced input (300ms default) — replaces Enter-only / no-debounce patterns
 * - 5 canonical states: loading / error / empty / manual-entry / manual-unknown-warning
 * - BE-side search filter (uses api.library.getLibrary({ search }) / api.sharedGames.search)
 * - Error feedback via sonner toast (replaces silent catch{})
 */

import { useCallback, useEffect, useId, useState, type JSX } from 'react';

import { AlertTriangle, Loader2, Search } from 'lucide-react';
import { toast } from 'sonner';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useDebounce } from '@/hooks/useDebounce';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface GameOption {
  id: string;
  title: string;
  imageUrl?: string;
  /** True when the option is a free-text manual entry (no backing id from library/catalog). */
  manual?: boolean;
}

export type GamePickerSource = 'library' | 'catalog' | 'both';

export interface GamePickerProps {
  /** Search source — 'library' (user's own library) | 'catalog' (shared catalog) | 'both' (library first, catalog fallback). */
  source: GamePickerSource;
  /** Selected game option (null when nothing selected yet). */
  value: GameOption | null;
  /** Callback fired when the user selects a game (or clears the selection with null). */
  onChange: (option: GameOption | null) => void;
  /** When true, surfaces a free-text input that produces a manual-entry GameOption (manual: true). */
  allowManualEntry?: boolean;
  /** Placeholder for the search input (default: "Cerca un gioco…"). */
  placeholder?: string;
  /** Debounce delay in ms (default 300). */
  debounceMs?: number;
  /** Max results to display (default 10). */
  maxResults?: number;
  /** Stable testid prefix for E2E selectors (default: "game-picker"). */
  testId?: string;
  /**
   * Optional slot rendered to the right of each result row (e.g. KB badges, custom indicators).
   * Receives the {@link GameOption}; return null to skip.
   */
  renderResultExtra?: (option: GameOption) => React.ReactNode;
}

// ============================================================================
// Component
// ============================================================================

export function GamePicker({
  source,
  value,
  onChange,
  allowManualEntry = false,
  placeholder = 'Cerca un gioco…',
  debounceMs = 300,
  maxResults = 10,
  testId = 'game-picker',
  renderResultExtra,
}: GamePickerProps): JSX.Element {
  const [searchQuery, setSearchQuery] = useState('');
  const debouncedQuery = useDebounce(searchQuery, debounceMs);
  const [results, setResults] = useState<GameOption[]>([]);
  const [isSearching, setIsSearching] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);
  const [manualTitle, setManualTitle] = useState('');
  const listboxId = useId();

  // ---------------------------------------------------------------------------
  // Search effect — runs on debounced query change
  // ---------------------------------------------------------------------------
  useEffect(() => {
    const trimmed = debouncedQuery.trim();
    if (trimmed.length < 2) {
      setResults([]);
      setHasSearched(false);
      return;
    }

    let cancelled = false;
    setIsSearching(true);
    setHasSearched(true);

    void (async () => {
      try {
        const combined = await searchGames(source, trimmed, maxResults);
        if (!cancelled) {
          setResults(combined);
        }
      } catch (err) {
        if (!cancelled) {
          setResults([]);
          const message = err instanceof Error ? err.message : 'Errore di ricerca';
          // Replace silent catch{} pattern — surface failure to user (Bug B2.2/B3.2).
          toast.error(`Impossibile cercare i giochi: ${message}`);
        }
      } finally {
        if (!cancelled) {
          setIsSearching(false);
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [debouncedQuery, source, maxResults]);

  const handleSelect = useCallback(
    (option: GameOption) => {
      onChange(option);
      // Don't reset search query: caller may want to render the selected state.
    },
    [onChange]
  );

  const handleClear = useCallback(() => {
    onChange(null);
    setSearchQuery('');
    setManualTitle('');
    setResults([]);
    setHasSearched(false);
  }, [onChange]);

  const handleManualConfirm = useCallback(() => {
    const trimmed = manualTitle.trim();
    if (trimmed.length === 0) return;
    onChange({ id: `manual-${trimmed}`, title: trimmed, manual: true });
  }, [manualTitle, onChange]);

  // ---------------------------------------------------------------------------
  // Selected-state render
  // ---------------------------------------------------------------------------
  if (value) {
    return (
      <div data-testid={`${testId}-selected`} className="space-y-2">
        <div className="flex items-center gap-3 rounded-xl border border-primary/20 bg-primary/5 p-3">
          {value.imageUrl ? (
            // eslint-disable-next-line @next/next/no-img-element -- ext. urls vary, no static optim. needed for picker preview
            <img src={value.imageUrl} alt="" className="h-12 w-12 rounded-lg object-cover" />
          ) : (
            <div className="h-12 w-12 rounded-lg bg-muted" aria-hidden="true" />
          )}
          <div className="flex-1 min-w-0">
            <p className="font-semibold truncate">{value.title}</p>
            <p className="text-xs text-muted-foreground">
              {value.manual ? 'Inserimento manuale' : 'Dalla tua libreria/catalogo'}
            </p>
          </div>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={handleClear}
            data-testid={`${testId}-clear`}
            aria-label="Rimuovi gioco selezionato"
          >
            ×
          </Button>
        </div>

        {value.manual && <ManualUnknownWarning testId={testId} />}
      </div>
    );
  }

  // ---------------------------------------------------------------------------
  // Default render: search input + results + optional manual entry
  // ---------------------------------------------------------------------------
  return (
    <div data-testid={testId} className="space-y-3">
      {/* Search input */}
      <div className="relative">
        <Search
          className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground"
          aria-hidden="true"
        />
        <Input
          type="search"
          placeholder={placeholder}
          value={searchQuery}
          onChange={e => setSearchQuery(e.target.value)}
          className="pl-9"
          aria-label="Cerca un gioco"
          aria-controls={listboxId}
          aria-busy={isSearching}
          data-testid={`${testId}-input`}
        />
        {isSearching && (
          <Loader2
            data-testid={`${testId}-loader`}
            className="absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 animate-spin text-muted-foreground"
            aria-hidden="true"
          />
        )}
      </div>

      {/* Results listbox */}
      <ul
        id={listboxId}
        role="listbox"
        aria-label="Risultati ricerca giochi"
        data-testid={`${testId}-results`}
        className={cn('space-y-1', results.length === 0 && 'hidden')}
      >
        {results.map(option => (
          <li key={option.id}>
            <button
              type="button"
              role="option"
              aria-selected={false}
              onClick={() => handleSelect(option)}
              data-testid={`${testId}-result-${option.id}`}
              className="flex items-center gap-3 w-full p-3 rounded-lg border border-border bg-card text-left hover:border-primary/30 hover:bg-primary/5 transition-colors"
            >
              {option.imageUrl ? (
                // eslint-disable-next-line @next/next/no-img-element -- ext. urls vary, no static optim. needed for picker preview
                <img
                  src={option.imageUrl}
                  alt=""
                  className="h-10 w-10 rounded-lg object-cover flex-shrink-0"
                />
              ) : (
                <div className="h-10 w-10 rounded-lg bg-muted flex-shrink-0" aria-hidden="true" />
              )}
              <span className="font-medium text-sm truncate flex-1">{option.title}</span>
              {renderResultExtra?.(option)}
            </button>
          </li>
        ))}
      </ul>

      {/* Empty state */}
      {!isSearching && hasSearched && results.length === 0 && (
        <p
          role="status"
          data-testid={`${testId}-empty`}
          className="text-sm text-muted-foreground text-center py-4"
        >
          Nessun risultato. Prova con un altro nome.
        </p>
      )}

      {/* Optional manual entry */}
      {allowManualEntry && (
        <>
          <div className="relative flex items-center gap-3" aria-hidden="true">
            <div className="flex-1 border-t border-border" />
            <span className="text-xs text-muted-foreground px-2">oppure</span>
            <div className="flex-1 border-t border-border" />
          </div>
          <div className="flex gap-2">
            <Input
              placeholder="Nome del gioco (es. Catan, Ticket to Ride…)"
              value={manualTitle}
              onChange={e => setManualTitle(e.target.value)}
              data-testid={`${testId}-manual-input`}
              aria-label="Nome del gioco"
            />
            <Button
              type="button"
              variant="outline"
              onClick={handleManualConfirm}
              disabled={manualTitle.trim().length === 0}
              data-testid={`${testId}-manual-confirm`}
            >
              Usa
            </Button>
          </div>
        </>
      )}
    </div>
  );
}

// ============================================================================
// Sub-component: Manual-unknown warning
// ============================================================================

interface ManualUnknownWarningProps {
  testId: string;
}

function ManualUnknownWarning({ testId }: ManualUnknownWarningProps): JSX.Element {
  return (
    <div
      role="alert"
      data-testid={`${testId}-manual-warning`}
      className="flex items-start gap-2 rounded-lg border border-amber-200 bg-amber-50 p-3"
    >
      <AlertTriangle className="mt-0.5 h-4 w-4 flex-none text-amber-600" aria-hidden="true" />
      <p className="text-xs text-amber-900">
        <strong>Gioco non riconosciuto</strong> — il sistema non potrà fornire regole AI.
      </p>
    </div>
  );
}

// ============================================================================
// Search dispatcher
// ============================================================================

async function searchGames(
  source: GamePickerSource,
  query: string,
  maxResults: number
): Promise<GameOption[]> {
  if (source === 'library') {
    const response = await api.library.getLibrary({ search: query, pageSize: maxResults });
    return (response.items ?? []).map(g => ({
      id: g.gameId,
      title: g.gameTitle,
      imageUrl: g.gameImageUrl ?? undefined,
    }));
  }

  if (source === 'catalog') {
    const response = await api.sharedGames.search({
      searchTerm: query,
      page: 1,
      pageSize: maxResults,
      status: 1, // Published
    });
    return (response.items ?? []).map(g => ({
      id: g.id,
      title: g.title,
      imageUrl: g.thumbnailUrl ?? undefined,
    }));
  }

  // source === 'both': library first, fill remainder with catalog (deduped by id).
  const half = Math.max(1, Math.ceil(maxResults / 2));
  const [libraryResults, catalogResults] = await Promise.all([
    api.library
      .getLibrary({ search: query, pageSize: half })
      .then(r =>
        (r.items ?? []).map(g => ({
          id: g.gameId,
          title: g.gameTitle,
          imageUrl: g.gameImageUrl ?? undefined,
        }))
      )
      .catch(() => [] as GameOption[]),
    api.sharedGames
      .search({ searchTerm: query, page: 1, pageSize: maxResults, status: 1 })
      .then(r =>
        (r.items ?? []).map(g => ({
          id: g.id,
          title: g.title,
          imageUrl: g.thumbnailUrl ?? undefined,
        }))
      )
      .catch(() => [] as GameOption[]),
  ]);

  const seen = new Set<string>();
  const merged: GameOption[] = [];
  for (const option of [...libraryResults, ...catalogResults]) {
    if (seen.has(option.id)) continue;
    seen.add(option.id);
    merged.push(option);
    if (merged.length >= maxResults) break;
  }
  return merged;
}
