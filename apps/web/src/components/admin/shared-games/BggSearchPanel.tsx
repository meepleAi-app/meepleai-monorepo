'use client';

/**
 * BggSearchPanel - Reusable BGG Search Component
 * Extracted from Step3BggMatch for reuse in NewGameClient and other contexts.
 *
 * Features:
 * - Search input with debounced autocomplete
 * - Match score calculation (Levenshtein similarity)
 * - Full game details fetch on selection (categories, mechanics, designers, publishers)
 * - Duplicate check with warning UI
 * - Throttle UX (3s timer for slow responses, unavailable alert with retry)
 * - Manual BGG ID input fallback
 * - NO dependency on useGameImportWizardStore (uses onSelect callback)
 */

import { useEffect, useState, useCallback, useMemo, useRef } from 'react';
import type { JSX } from 'react';

import {
  Search,
  Loader2,
  ExternalLink,
  AlertCircle,
  CheckCircle2,
  Plus,
  AlertTriangle,
} from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/data-display/badge';
import { Card } from '@/components/ui/data-display/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Separator } from '@/components/ui/navigation/separator';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { useSearchBggGames } from '@/hooks/queries/useSearchBggGames';
import { api } from '@/lib/api';
import type { BggSearchResult } from '@/lib/api/schemas/games.schemas';
import { cn } from '@/lib/utils';
import { calculateStringSimilarity } from '@/lib/utils/string-similarity';
import type { BggFullGameData } from '@/types/bgg';

export interface BggSearchPanelProps {
  /** Callback when a BGG game is selected with full metadata */
  onSelect: (data: BggFullGameData) => void;
  /** Pre-fill search with this query */
  initialQuery?: string;
  /** Show manual BGG ID input section (default: true) */
  showManualIdInput?: boolean;
}

interface BggSearchResultWithScore extends BggSearchResult {
  matchScore: number;
}

/**
 * Get match score badge color based on percentage
 */
function getMatchBadgeVariant(score: number): 'default' | 'secondary' | 'destructive' {
  if (score >= 80) return 'default';
  if (score >= 50) return 'secondary';
  return 'destructive';
}

export function BggSearchPanel({
  onSelect,
  initialQuery = '',
  showManualIdInput = true,
}: BggSearchPanelProps): JSX.Element {
  // Search state
  const [query, setQuery] = useState(initialQuery);
  const [debouncedQuery, setDebouncedQuery] = useState(initialQuery);

  // Selection state (local, no store dependency)
  const [selectedBggId, setSelectedBggId] = useState<number | null>(null);
  const [isLoadingDetails, setIsLoadingDetails] = useState(false);

  // Duplicate check state
  const [duplicateWarning, setDuplicateWarning] = useState<{
    isDuplicate: boolean;
    existingGameId: string | null;
  } | null>(null);

  // Throttle UX state
  const [isThrottled, setIsThrottled] = useState(false);
  const throttleTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  // Manual BGG ID state
  const [manualId, setManualId] = useState('');
  const [manualLoading, setManualLoading] = useState(false);
  const [manualError, setManualError] = useState<string | null>(null);
  const [manualPreview, setManualPreview] = useState<BggFullGameData | null>(null);

  // BGG Search with React Query
  const {
    data: searchData,
    isLoading: isSearching,
    error: searchError,
  } = useSearchBggGames({
    query: debouncedQuery,
    exact: false,
  });

  // Debounce search query (300ms)
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedQuery(query);
    }, 300);

    return () => clearTimeout(timer);
  }, [query]);

  // Throttle UX: show notice if search takes > 3s
  useEffect(() => {
    if (isSearching) {
      throttleTimerRef.current = setTimeout(() => {
        setIsThrottled(true);
      }, 3000);
    } else {
      setIsThrottled(false);
      if (throttleTimerRef.current) {
        clearTimeout(throttleTimerRef.current);
        throttleTimerRef.current = null;
      }
    }

    return () => {
      if (throttleTimerRef.current) {
        clearTimeout(throttleTimerRef.current);
        throttleTimerRef.current = null;
      }
    };
  }, [isSearching]);

  // Calculate match scores for results
  const resultsWithScores = useMemo<BggSearchResultWithScore[]>(() => {
    if (!searchData?.results) {
      return [];
    }

    const referenceTitle = initialQuery || query;
    if (!referenceTitle) {
      return searchData.results.map(result => ({
        ...result,
        matchScore: 0,
      }));
    }

    return searchData.results
      .map(result => ({
        ...result,
        matchScore: calculateStringSimilarity(result.name, referenceTitle),
      }))
      .sort((a, b) => b.matchScore - a.matchScore);
  }, [searchData, initialQuery, query]);

  /**
   * Map BggGameDetails API response to BggFullGameData
   */
  const mapDetailsToFullData = useCallback(
    (details: Awaited<ReturnType<typeof api.bgg.getGameDetails>>): BggFullGameData => ({
      id: details.bggId,
      name: details.name,
      yearPublished: details.yearPublished ?? undefined,
      minPlayers: details.minPlayers ?? undefined,
      maxPlayers: details.maxPlayers ?? undefined,
      playingTime: details.playingTime ?? undefined,
      minAge: details.minAge ?? undefined,
      description: details.description ?? undefined,
      imageUrl: details.imageUrl ?? undefined,
      thumbnailUrl: details.thumbnailUrl ?? undefined,
      categories: details.categories ?? [],
      mechanics: details.mechanics ?? [],
      designers: details.designers ?? [],
      publishers: details.publishers ?? [],
      complexityRating: details.averageWeight ?? undefined,
      averageRating: details.averageRating ?? undefined,
    }),
    []
  );

  // Handle result selection - fetches full details
  const handleSelectResult = useCallback(
    async (result: BggSearchResultWithScore) => {
      setSelectedBggId(result.bggId);
      setIsLoadingDetails(true);
      setDuplicateWarning(null);

      try {
        // Fetch full details and check duplicate in parallel
        const [details, duplicateCheck] = await Promise.all([
          api.bgg.getGameDetails(result.bggId),
          api.sharedGames
            .checkBggDuplicate(result.bggId)
            .catch(() => ({ isDuplicate: false, existingGameId: null })),
        ]);

        const fullData = mapDetailsToFullData(details);

        // Set duplicate warning if applicable
        if (duplicateCheck.isDuplicate) {
          setDuplicateWarning({
            isDuplicate: true,
            existingGameId: duplicateCheck.existingGameId ?? null,
          });
        }

        onSelect(fullData);
      } catch (_err) {
        // If full details fetch fails, fall back to basic data from search result
        const fallbackData: BggFullGameData = {
          id: result.bggId,
          name: result.name,
          yearPublished: result.yearPublished ?? undefined,
          imageUrl: result.thumbnailUrl ?? undefined,
          thumbnailUrl: result.thumbnailUrl ?? undefined,
          categories: [],
          mechanics: [],
          designers: [],
          publishers: [],
        };
        onSelect(fallbackData);
      } finally {
        setIsLoadingDetails(false);
      }
    },
    [onSelect, mapDetailsToFullData]
  );

  // Handle manual BGG ID fetch
  const handleFetchManualId = useCallback(async () => {
    const id = parseInt(manualId);

    if (isNaN(id) || id <= 0) {
      setManualError('Please enter a valid BGG ID (positive number)');
      return;
    }

    setManualLoading(true);
    setManualError(null);
    setManualPreview(null);

    try {
      const details = await api.bgg.getGameDetails(id);
      const fullData = mapDetailsToFullData(details);
      setManualPreview(fullData);
    } catch (err) {
      setManualError(err instanceof Error ? err.message : `Game with BGG ID ${id} not found`);
    } finally {
      setManualLoading(false);
    }
  }, [manualId, mapDetailsToFullData]);

  // Confirm manual selection
  const handleConfirmManual = useCallback(() => {
    if (!manualPreview) return;

    setSelectedBggId(manualPreview.id);

    // Check for duplicate in background
    api.sharedGames
      .checkBggDuplicate(manualPreview.id)
      .then(result => {
        if (result.isDuplicate) {
          setDuplicateWarning({
            isDuplicate: true,
            existingGameId: result.existingGameId ?? null,
          });
        }
      })
      .catch(() => {
        // Ignore duplicate check errors
      });

    onSelect(manualPreview);
  }, [manualPreview, onSelect]);

  return (
    <div className="space-y-6">
      {/* Search Section */}
      <Card className="p-6">
        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="bgg-search">Search BoardGameGeek</Label>
            <div className="relative">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="bgg-search"
                type="text"
                value={query}
                onChange={e => setQuery(e.target.value)}
                placeholder="Search for game title..."
                className="pl-10"
                data-testid="bgg-search-input"
              />
              {(isSearching || isLoadingDetails) && (
                <Loader2 className="absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 animate-spin text-muted-foreground" />
              )}
            </div>
            {initialQuery && (
              <p className="text-xs text-muted-foreground">
                Pre-filled with extracted title: &quot;{initialQuery}&quot;
              </p>
            )}
          </div>

          {/* Throttle Notice */}
          {isThrottled && (
            <Alert>
              <Loader2 className="h-4 w-4 animate-spin" />
              <AlertTitle>BGG is responding slowly</AlertTitle>
              <AlertDescription className="text-sm">
                The BoardGameGeek API is taking longer than expected. Please wait...
              </AlertDescription>
            </Alert>
          )}

          {/* Search Error */}
          {searchError && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Search Failed</AlertTitle>
              <AlertDescription className="text-sm">
                {searchError instanceof Error
                  ? searchError.message
                  : 'An error occurred during BGG search'}
              </AlertDescription>
            </Alert>
          )}

          {/* Duplicate Warning */}
          {duplicateWarning?.isDuplicate && (
            <Alert>
              <AlertTriangle className="h-4 w-4" />
              <AlertTitle>Duplicate Detected</AlertTitle>
              <AlertDescription className="text-sm">
                This game already exists in the shared catalog.
                {duplicateWarning.existingGameId && (
                  <span className="ml-1 text-xs text-muted-foreground">
                    (ID: {duplicateWarning.existingGameId})
                  </span>
                )}
              </AlertDescription>
            </Alert>
          )}

          {/* Results */}
          {resultsWithScores.length > 0 && (
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <p className="text-sm font-medium">
                  Found {resultsWithScores.length} result
                  {resultsWithScores.length !== 1 ? 's' : ''}
                </p>
                <p className="text-xs text-muted-foreground">Click to select</p>
              </div>

              <div
                className="max-h-96 space-y-2 overflow-y-auto rounded-md border p-2"
                data-testid="bgg-search-results"
              >
                {resultsWithScores.map(result => {
                  const isSelected = result.bggId === selectedBggId;

                  return (
                    <button
                      key={result.bggId}
                      type="button"
                      onClick={() => handleSelectResult(result)}
                      className={cn(
                        'flex w-full items-center gap-3 rounded-md border p-3 text-left transition-all',
                        'hover:border-primary hover:bg-accent',
                        isSelected &&
                          'border-primary bg-primary/10 ring-2 ring-primary ring-offset-2'
                      )}
                      data-testid={`bgg-result-${result.bggId}`}
                    >
                      {/* Thumbnail */}
                      {result.thumbnailUrl ? (
                        // eslint-disable-next-line @next/next/no-img-element
                        <img
                          src={result.thumbnailUrl}
                          alt={result.name}
                          className="h-16 w-16 flex-shrink-0 rounded object-cover"
                        />
                      ) : (
                        <div className="flex h-16 w-16 flex-shrink-0 items-center justify-center rounded bg-muted">
                          <Search className="h-6 w-6 text-muted-foreground" />
                        </div>
                      )}

                      {/* Info */}
                      <div className="min-w-0 flex-1 space-y-1">
                        <div className="flex items-start justify-between gap-2">
                          <h4 className="font-medium leading-tight">{result.name}</h4>
                          {isSelected && (
                            <CheckCircle2 className="h-5 w-5 flex-shrink-0 text-primary" />
                          )}
                        </div>

                        <div className="flex flex-wrap items-center gap-2 text-sm">
                          {result.yearPublished && (
                            <Badge variant="outline" className="text-xs">
                              {result.yearPublished}
                            </Badge>
                          )}

                          <Badge variant="outline" className="text-xs capitalize">
                            {result.type === 'boardgame' ? 'Gioco' : result.type}
                          </Badge>

                          <Badge
                            variant={getMatchBadgeVariant(result.matchScore)}
                            className="text-xs font-semibold"
                          >
                            {result.matchScore}% match
                          </Badge>

                          <span className="text-xs text-muted-foreground">BGG #{result.bggId}</span>
                        </div>
                      </div>

                      {/* BGG Link */}
                      <a
                        href={`https://boardgamegeek.com/boardgame/${result.bggId}`}
                        target="_blank"
                        rel="noopener noreferrer"
                        className="flex-shrink-0 rounded p-1 hover:bg-background"
                        onClick={e => e.stopPropagation()}
                        title="View on BoardGameGeek"
                      >
                        <ExternalLink className="h-4 w-4 text-muted-foreground" />
                      </a>
                    </button>
                  );
                })}
              </div>
            </div>
          )}

          {/* Empty State */}
          {!isSearching &&
            debouncedQuery.length >= 2 &&
            resultsWithScores.length === 0 &&
            !searchError && (
              <div className="rounded-md border-2 border-dashed p-8 text-center">
                <p className="text-sm text-muted-foreground">
                  No games found for &quot;{debouncedQuery}&quot;. Try a different search term
                  {showManualIdInput ? ' or use manual BGG ID below' : ''}.
                </p>
              </div>
            )}
        </div>
      </Card>

      {/* Manual BGG ID Section */}
      {showManualIdInput && (
        <div>
          <div className="mb-3 flex items-center gap-2">
            <Separator className="flex-1" />
            <span className="text-xs font-medium uppercase text-muted-foreground">Or</span>
            <Separator className="flex-1" />
          </div>

          <Card className="p-6">
            <div className="space-y-4">
              <div>
                <h4 className="text-sm font-semibold">Manual BGG ID Input</h4>
                <p className="text-xs text-muted-foreground">
                  If you know the BoardGameGeek ID, enter it directly.
                </p>
              </div>

              <div className="space-y-3">
                <div className="flex gap-2">
                  <div className="flex-1 space-y-2">
                    <Label htmlFor="manual-bgg-id">BGG ID</Label>
                    <Input
                      id="manual-bgg-id"
                      type="number"
                      min="1"
                      value={manualId}
                      onChange={e => {
                        setManualId(e.target.value);
                        setManualError(null);
                        setManualPreview(null);
                      }}
                      placeholder="e.g., 13"
                      data-testid="manual-bgg-id-input"
                    />
                  </div>

                  <div className="flex items-end">
                    <Button
                      onClick={handleFetchManualId}
                      disabled={!manualId || manualLoading}
                      variant="outline"
                      data-testid="fetch-manual-bgg-btn"
                    >
                      {manualLoading ? (
                        <>
                          <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                          Loading...
                        </>
                      ) : (
                        <>
                          <Plus className="mr-2 h-4 w-4" />
                          Fetch
                        </>
                      )}
                    </Button>
                  </div>
                </div>

                {/* Manual Error */}
                {manualError && (
                  <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertDescription className="text-sm">{manualError}</AlertDescription>
                  </Alert>
                )}

                {/* Manual Preview */}
                {manualPreview && (
                  <div className="space-y-3">
                    <div className="flex items-center gap-3 rounded-md border bg-background p-3">
                      {manualPreview.thumbnailUrl ? (
                        // eslint-disable-next-line @next/next/no-img-element
                        <img
                          src={manualPreview.thumbnailUrl}
                          alt={manualPreview.name}
                          className="h-16 w-16 rounded object-cover"
                        />
                      ) : (
                        <div className="flex h-16 w-16 items-center justify-center rounded bg-muted">
                          <Search className="h-6 w-6 text-muted-foreground" />
                        </div>
                      )}

                      <div className="min-w-0 flex-1">
                        <h5 className="font-medium">{manualPreview.name}</h5>
                        <div className="mt-1 flex flex-wrap items-center gap-2 text-sm text-muted-foreground">
                          {manualPreview.yearPublished && (
                            <span>{manualPreview.yearPublished}</span>
                          )}
                          {manualPreview.minPlayers && manualPreview.maxPlayers && (
                            <span>
                              {manualPreview.minPlayers}-{manualPreview.maxPlayers} players
                            </span>
                          )}
                          <span className="text-xs">BGG #{manualPreview.id}</span>
                        </div>
                        {/* Show metadata preview */}
                        {manualPreview.categories.length > 0 && (
                          <div className="mt-1 flex flex-wrap gap-1">
                            {manualPreview.categories.slice(0, 3).map(cat => (
                              <Badge key={cat} variant="outline" className="text-xs">
                                {cat}
                              </Badge>
                            ))}
                            {manualPreview.categories.length > 3 && (
                              <Badge variant="outline" className="text-xs">
                                +{manualPreview.categories.length - 3}
                              </Badge>
                            )}
                          </div>
                        )}
                      </div>
                    </div>

                    <Button
                      onClick={handleConfirmManual}
                      className="w-full"
                      data-testid="confirm-manual-bgg-btn"
                    >
                      <CheckCircle2 className="mr-2 h-4 w-4" />
                      Use This Game
                    </Button>
                  </div>
                )}
              </div>
            </div>
          </Card>
        </div>
      )}
    </div>
  );
}
