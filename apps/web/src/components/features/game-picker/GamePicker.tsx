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
 * - Error feedback on the page, with a retry (replaces silent catch{})
 *
 * Issue #4227 — the data layer:
 * - loaded through TanStack Query, keyed by what is asked: a late answer to an older text
 *   cannot replace the current list, and the previous list stays while the next one loads;
 * - the library is sorted by title; the catalog cover comes from `coverUrl` (#2123);
 * - `showLibraryWhenEmpty` lists the library before any typing;
 * - an error stays on the page with a retry, and with two sources a failing one is
 *   reported as partial results instead of being swallowed.
 *
 * Issue #4227 — keyboard: the input is an ARIA combobox (APG list-autocomplete pattern)
 * over the inline listbox. Arrow keys move the active option, Enter picks it, Escape
 * drops it; the options are not tab stops. While the list belongs to an older text the
 * keyboard does not pick from it.
 */

import { useCallback, useEffect, useId, useState, type JSX, type KeyboardEvent } from 'react';

import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { AlertTriangle, Loader2, Search } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useDebounce } from '@/hooks/useDebounce';
import { api } from '@/lib/api';
import { shouldUsePlaceholder } from '@/lib/games/cover-utils';
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
  /**
   * List the user's library while the field is empty, and search it from the first letter
   * (#4227). It concerns the library only: the catalog is always searched from 2 letters.
   * Default false.
   */
  showLibraryWhenEmpty?: boolean;
  /**
   * Read-only picker (#4250): the field does not search and a selected game is shown
   * without the × that would clear it. Default false.
   */
  disabled?: boolean;
  /**
   * Forwarded to the search input, so the picker can sit inside a form control: a
   * `<label htmlFor>` reaches the field, and the field carries the error state.
   */
  id?: string;
  'aria-describedby'?: string;
  'aria-invalid'?: boolean | 'true' | 'false';
  /**
   * Extra classes for the listbox, e.g. a max height with its own scroll where the list
   * is long (the active option is kept in view).
   */
  listClassName?: string;
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
  showLibraryWhenEmpty = false,
  disabled = false,
  id,
  'aria-describedby': ariaDescribedBy,
  'aria-invalid': ariaInvalid,
  listClassName,
  testId = 'game-picker',
  renderResultExtra,
}: GamePickerProps): JSX.Element {
  const [searchQuery, setSearchQuery] = useState('');
  const debouncedQuery = useDebounce(searchQuery, debounceMs);
  const [manualTitle, setManualTitle] = useState('');
  // The option the keyboard is on, by game id: when the list changes and the game is gone,
  // there is no active option any more.
  const [activeId, setActiveId] = useState<string | null>(null);
  const listboxId = useId();
  const optionDomId = (option: GameOption): string => `${listboxId}-option-${option.id}`;

  // ---------------------------------------------------------------------------
  // Search — a query keyed by what is asked (#4227). A late answer to an older text
  // belongs to another key and cannot replace the current list; the previous list stays
  // on screen while the next one loads.
  // ---------------------------------------------------------------------------
  // An emptied field counts at once: waiting for the debounce would leave the list of the
  // previous text under an empty input (after the × that clears a selection).
  const term = searchQuery.trim() === '' ? '' : debouncedQuery.trim();
  const wantsLibrary = source !== 'catalog' && term.length >= (showLibraryWhenEmpty ? 0 : 2);
  const wantsCatalog = source !== 'library' && term.length >= 2;
  // No request while read-only, nor while a game is selected: the list is not on screen.
  const active = !disabled && !value && (wantsLibrary || wantsCatalog);

  const search = useQuery({
    queryKey: ['game-picker', term, maxResults, wantsLibrary, wantsCatalog],
    queryFn: () => searchGames({ term, maxResults, wantsLibrary, wantsCatalog }),
    enabled: active,
    placeholderData: keepPreviousData,
    staleTime: 30_000,
    // A list the user is choosing from must not reload when the tab regains focus.
    refetchOnWindowFocus: false,
  });

  const searchError = active && search.isError ? search.error : null;
  // A failed refresh keeps the data it had: the list stays, next to the error.
  const outcome = active ? search.data : undefined;
  const results = outcome?.options ?? [];
  const isSearching = active && search.isFetching;
  // False while the list on screen belongs to an older text.
  const resultsMatchQuery = debouncedQuery === searchQuery && !search.isPlaceholderData;

  // Replace silent catch{} pattern — surface failure to user (Bug B2.2/B3.2). The message
  // stays on the page with a retry; a toast on top of it announced the same thing twice.
  const searchErrorMessage = searchError
    ? searchError instanceof Error
      ? searchError.message
      : 'Errore di ricerca'
    : null;

  const handleSelect = useCallback(
    (option: GameOption) => {
      onChange(option);
      setActiveId(null);
      // Don't reset search query: caller may want to render the selected state.
    },
    [onChange]
  );

  const activeIndex = activeId === null ? -1 : results.findIndex(o => o.id === activeId);
  const activeOption = activeIndex >= 0 ? results[activeIndex] : undefined;
  const activeDomId = activeOption ? optionDomId(activeOption) : undefined;

  // Keep the active option in view when the list scrolls.
  useEffect(() => {
    if (!activeDomId) return;
    document.getElementById(activeDomId)?.scrollIntoView?.({ block: 'nearest' });
  }, [activeDomId]);

  /** ARIA combobox keyboard pattern (APG) over the inline listbox. */
  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>): void => {
    if (results.length === 0) return;

    const picks = event.key === 'ArrowDown' || event.key === 'ArrowUp' || event.key === 'Enter';
    // The list on screen belongs to an older text: the keyboard must not pick from it.
    // Enter is swallowed too, so it cannot submit a surrounding form on a stale choice.
    if (picks && !resultsMatchQuery) {
      event.preventDefault();
      return;
    }

    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      const direction = event.key === 'ArrowDown' ? 1 : -1;
      const next =
        activeIndex < 0
          ? direction === 1
            ? 0
            : results.length - 1
          : Math.min(Math.max(activeIndex + direction, 0), results.length - 1);
      setActiveId(results[next].id);
      return;
    }

    if (event.key === 'Enter') {
      // With no active option Enter is left alone (a surrounding form may want it).
      if (!activeOption) return;
      event.preventDefault();
      handleSelect(activeOption);
      return;
    }

    if (event.key === 'Escape' && activeOption) {
      event.preventDefault();
      setActiveId(null);
    }
  };

  const handleClear = useCallback(() => {
    onChange(null);
    setSearchQuery('');
    setManualTitle('');
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
          <PickerCover imageUrl={value.imageUrl} className="h-12 w-12 rounded-lg" />
          <div className="flex-1 min-w-0">
            <p className="font-semibold truncate">{value.title}</p>
            <p className="text-xs text-muted-foreground">
              {value.manual ? 'Inserimento manuale' : 'Dalla tua libreria/catalogo'}
            </p>
          </div>
          {!disabled && (
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
          )}
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
          id={id}
          type="search"
          role="combobox"
          aria-describedby={ariaDescribedBy}
          aria-invalid={ariaInvalid}
          placeholder={placeholder}
          value={searchQuery}
          autoComplete="off"
          disabled={disabled}
          onChange={e => {
            setSearchQuery(e.target.value);
            setActiveId(null);
          }}
          onKeyDown={handleKeyDown}
          className="pl-9"
          aria-label="Cerca un gioco"
          aria-autocomplete="list"
          aria-expanded={results.length > 0}
          aria-controls={listboxId}
          aria-activedescendant={activeDomId}
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
        className={cn('space-y-1', listClassName, results.length === 0 && 'hidden')}
      >
        {results.map(option => (
          <li key={option.id} role="presentation">
            <button
              type="button"
              role="option"
              id={optionDomId(option)}
              aria-selected={option.id === activeOption?.id}
              // The keyboard reaches the options with the arrows, from the input.
              tabIndex={-1}
              onClick={() => handleSelect(option)}
              data-testid={`${testId}-result-${option.id}`}
              className={cn(
                'flex items-center gap-3 w-full p-3 rounded-lg border bg-card text-left transition-colors',
                'hover:border-primary/30 hover:bg-primary/5',
                option.id === activeOption?.id
                  ? 'border-primary/30 bg-primary/5 ring-2 ring-ring'
                  : 'border-border'
              )}
            >
              <PickerCover
                imageUrl={option.imageUrl}
                className="h-10 w-10 rounded-lg flex-shrink-0"
              />
              <span className="font-medium text-sm truncate flex-1">{option.title}</span>
              {renderResultExtra?.(option)}
            </button>
          </li>
        ))}
      </ul>

      {/* More in the library than shown */}
      {outcome && outcome.libraryTotal > outcome.libraryShown && (
        <p data-testid={`${testId}-more`} className="text-xs text-muted-foreground text-center">
          Mostrati {outcome.libraryShown} di {outcome.libraryTotal} giochi della libreria: scrivi
          per restringere.
        </p>
      )}

      {/* One of the two sources failed */}
      {outcome?.partial && (
        <p
          role="status"
          data-testid={`${testId}-partial`}
          className="text-xs text-muted-foreground text-center"
        >
          Risultati parziali: una delle due fonti non risponde.
        </p>
      )}

      {/* Error state */}
      {searchErrorMessage && (
        <div
          role="alert"
          data-testid={`${testId}-error`}
          className="flex items-center justify-between gap-3 rounded-lg border border-destructive/30 bg-destructive/5 p-3 text-sm"
        >
          <span>Impossibile cercare i giochi: {searchErrorMessage}</span>
          <Button
            type="button"
            variant="outline"
            size="sm"
            onClick={() => void search.refetch()}
            data-testid={`${testId}-retry`}
          >
            Riprova
          </Button>
        </div>
      )}

      {/* Empty state */}
      {outcome && !searchError && !isSearching && resultsMatchQuery && results.length === 0 && (
        <p
          role="status"
          data-testid={`${testId}-empty`}
          className="text-sm text-muted-foreground text-center py-4"
        >
          {term.length === 0
            ? 'La tua libreria è vuota.'
            : 'Nessun risultato. Prova con un altro nome.'}
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
              disabled={disabled}
              data-testid={`${testId}-manual-input`}
              aria-label="Nome del gioco"
            />
            <Button
              type="button"
              variant="outline"
              onClick={handleManualConfirm}
              disabled={disabled || manualTitle.trim().length === 0}
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
// Sub-component: cover thumbnail
// ============================================================================

interface PickerCoverProps {
  imageUrl?: string;
  className: string;
}

/**
 * The cover of a row, or a neutral square. Like the MeepleCard cover: a BGG-hosted URL is
 * never requested (#2123), and a cover that fails to load — a CSP-blocked host, a 404 —
 * falls back to the square instead of leaving a broken image.
 */
function PickerCover({ imageUrl, className }: PickerCoverProps): JSX.Element {
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    setFailed(false);
  }, [imageUrl]);

  if (failed || shouldUsePlaceholder(imageUrl)) {
    return <div className={cn(className, 'bg-muted')} aria-hidden="true" />;
  }

  return (
    // eslint-disable-next-line @next/next/no-img-element -- ext. urls vary, no static optim. needed for picker preview
    <img
      src={imageUrl}
      alt=""
      className={cn(className, 'object-cover')}
      onError={() => setFailed(true)}
    />
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

interface SearchRequest {
  /** Trimmed text; empty when the library is browsed without a search. */
  term: string;
  maxResults: number;
  wantsLibrary: boolean;
  wantsCatalog: boolean;
}

interface SearchOutcome {
  options: GameOption[];
  /** True when one of the two sources failed and the other answered. */
  partial: boolean;
  /** Library games matching the text on the server, and how many of them are listed. */
  libraryTotal: number;
  libraryShown: number;
}

/**
 * Asks the sources that are wanted. The library is sorted by title, like every other
 * library picker: the endpoint sorts descending by date unless told otherwise. The catalog
 * endpoint returns published games only. With both sources the library comes first and
 * the catalog fills the rest, deduplicated by id (the two share ids).
 *
 * A failing source is not swallowed: when every asked source fails the error is thrown;
 * when one of two fails the outcome is marked `partial`.
 */
async function searchGames({
  term,
  maxResults,
  wantsLibrary,
  wantsCatalog,
}: SearchRequest): Promise<SearchOutcome> {
  const libraryLimit =
    wantsLibrary && wantsCatalog ? Math.max(1, Math.ceil(maxResults / 2)) : maxResults;

  const [library, catalog] = await Promise.allSettled([
    wantsLibrary
      ? api.library.getLibrary({
          search: term || undefined,
          page: 1,
          pageSize: libraryLimit,
          sortBy: 'title',
          sortDescending: false,
        })
      : null,
    wantsCatalog
      ? api.sharedGames.search({ searchTerm: term, page: 1, pageSize: maxResults })
      : null,
  ]);

  const failures = [library, catalog].filter(
    (result): result is PromiseRejectedResult => result.status === 'rejected'
  );
  const asked = Number(wantsLibrary) + Number(wantsCatalog);
  if (failures.length > 0 && failures.length === asked) {
    throw failures[0].reason;
  }

  const libraryPage = library.status === 'fulfilled' ? library.value : null;
  const libraryOptions: GameOption[] = (libraryPage?.items ?? []).map(g => ({
    id: g.gameId,
    title: g.gameTitle,
    imageUrl: g.coverUrl ?? g.gameImageUrl ?? undefined,
  }));
  // Issue #2123: `coverUrl` is the only cover a browser may request for a catalog game.
  const catalogOptions: GameOption[] = (
    catalog.status === 'fulfilled' ? (catalog.value?.items ?? []) : []
  ).map(g => ({
    id: g.id,
    title: g.title,
    imageUrl: g.coverUrl ?? undefined,
  }));

  const seen = new Set<string>();
  const options: GameOption[] = [];
  for (const option of [...libraryOptions, ...catalogOptions]) {
    if (seen.has(option.id)) continue;
    seen.add(option.id);
    options.push(option);
    if (options.length >= maxResults) break;
  }

  return {
    options,
    partial: failures.length > 0,
    libraryTotal: libraryPage?.totalCount ?? 0,
    libraryShown: libraryOptions.length,
  };
}
