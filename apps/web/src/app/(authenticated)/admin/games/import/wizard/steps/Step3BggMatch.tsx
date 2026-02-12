'use client';

/**
 * Step 3: BGG Match
 * Issue #4164: BoardGameGeek search with match scoring and selection
 *
 * Features:
 * - Search input pre-filled with extracted title
 * - BGG search with debounced autocomplete
 * - Match score calculation (Levenshtein similarity)
 * - Result cards with thumbnails and metadata
 * - Selected highlight state
 * - Manual BGG ID input fallback
 * - Error handling with retry
 */

import { useEffect, useState, useCallback, useMemo } from 'react';
import type { JSX } from 'react';

import { Search, Loader2, ExternalLink, AlertCircle, CheckCircle2, Plus } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/data-display/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Badge } from '@/components/ui/data-display/badge';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Separator } from '@/components/ui/navigation/separator';
import { useSearchBggGames } from '@/hooks/queries/useSearchBggGames';
import { calculateStringSimilarity } from '@/lib/utils/string-similarity';
import { useGameImportWizardStore, type BggGameData } from '@/stores/useGameImportWizardStore';
import type { BggSearchResult } from '@/lib/api/schemas/games.schemas';
import { cn } from '@/lib/utils';
import { api } from '@/lib/api';

export interface Step3BggMatchProps {
  /** Callback when BGG game is selected */
  onComplete?: (bggId: number, data?: BggGameData) => void;
}

interface BggSearchResultWithScore extends BggSearchResult {
  matchScore: number;
}

/**
 * Get match score badge color based on percentage
 */
function getMatchBadgeVariant(score: number): 'default' | 'secondary' | 'destructive' {
  if (score >= 80) return 'default'; // Green
  if (score >= 50) return 'secondary'; // Yellow/amber
  return 'destructive'; // Red
}

export function Step3BggMatch({ onComplete }: Step3BggMatchProps): JSX.Element {
  const { extractedMetadata, selectedBggId, bggGameData, setSelectedBggId } = useGameImportWizardStore();

  // Search state
  const [query, setQuery] = useState(extractedMetadata?.title || '');
  const [debouncedQuery, setDebouncedQuery] = useState(extractedMetadata?.title || '');

  // Manual BGG ID state
  const [manualId, setManualId] = useState('');
  const [manualLoading, setManualLoading] = useState(false);
  const [manualError, setManualError] = useState<string | null>(null);
  const [manualPreview, setManualPreview] = useState<BggGameData | null>(null);

  // BGG Search with React Query
  const { data: searchData, isLoading: isSearching, error: searchError } = useSearchBggGames({
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

  // Calculate match scores for results
  const resultsWithScores = useMemo<BggSearchResultWithScore[]>(() => {
    if (!searchData?.results || !extractedMetadata?.title) {
      return [];
    }

    return searchData.results
      .map(result => ({
        ...result,
        matchScore: calculateStringSimilarity(result.name, extractedMetadata.title!),
      }))
      .sort((a, b) => b.matchScore - a.matchScore); // Sort by score descending
  }, [searchData, extractedMetadata]);

  // Handle result selection
  const handleSelectResult = useCallback(
    (result: BggSearchResultWithScore) => {
      const gameData: BggGameData = {
        id: result.bggId,
        name: result.name,
        yearPublished: result.yearPublished ?? undefined,
        imageUrl: result.thumbnailUrl ?? undefined,
        thumbnailUrl: result.thumbnailUrl ?? undefined,
      };

      setSelectedBggId(result.bggId, gameData);
      onComplete?.(result.bggId, gameData);
    },
    [setSelectedBggId, onComplete]
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

      const gameData: BggGameData = {
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
      };

      setManualPreview(gameData);
    } catch (err) {
      setManualError(
        err instanceof Error ? err.message : `Game with BGG ID ${id} not found`
      );
    } finally {
      setManualLoading(false);
    }
  }, [manualId]);

  // Confirm manual selection
  const handleConfirmManual = useCallback(() => {
    if (!manualPreview) return;

    setSelectedBggId(manualPreview.id, manualPreview);
    onComplete?.(manualPreview.id, manualPreview);
  }, [manualPreview, setSelectedBggId, onComplete]);

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h3 className="text-lg font-semibold">Select BoardGameGeek Game</h3>
        <p className="text-sm text-muted-foreground">
          Search for the matching game on BoardGameGeek, or enter a BGG ID manually.
        </p>
      </div>

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
              {isSearching && (
                <Loader2 className="absolute right-3 top-1/2 h-4 w-4 -translate-y-1/2 animate-spin text-muted-foreground" />
              )}
            </div>
            {extractedMetadata?.title && (
              <p className="text-xs text-muted-foreground">
                Pre-filled with extracted title: &quot;{extractedMetadata.title}&quot;
              </p>
            )}
          </div>

          {/* Search Error */}
          {searchError && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Search Failed</AlertTitle>
              <AlertDescription className="text-sm">
                {searchError instanceof Error ? searchError.message : 'An error occurred during BGG search'}
              </AlertDescription>
            </Alert>
          )}

          {/* Results */}
          {resultsWithScores.length > 0 && (
            <div className="space-y-3">
              <div className="flex items-center justify-between">
                <p className="text-sm font-medium">
                  Found {resultsWithScores.length} result{resultsWithScores.length !== 1 ? 's' : ''}
                </p>
                <p className="text-xs text-muted-foreground">Click to select</p>
              </div>

              <div className="max-h-96 space-y-2 overflow-y-auto rounded-md border p-2" data-testid="bgg-search-results">
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
                        isSelected && 'border-primary bg-primary/10 ring-2 ring-primary ring-offset-2'
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
                          {isSelected && <CheckCircle2 className="h-5 w-5 flex-shrink-0 text-primary" />}
                        </div>

                        <div className="flex flex-wrap items-center gap-2 text-sm">
                          {/* Year Badge */}
                          {result.yearPublished && (
                            <Badge variant="outline" className="text-xs">
                              {result.yearPublished}
                            </Badge>
                          )}

                          {/* Type Badge */}
                          <Badge variant="outline" className="text-xs capitalize">
                            {result.type === 'boardgame' ? 'Gioco' : result.type}
                          </Badge>

                          {/* Match Score Badge */}
                          <Badge variant={getMatchBadgeVariant(result.matchScore)} className="text-xs font-semibold">
                            {result.matchScore}% match
                          </Badge>

                          {/* BGG ID */}
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
          {!isSearching && debouncedQuery.length >= 2 && resultsWithScores.length === 0 && !searchError && (
            <div className="rounded-md border-2 border-dashed p-8 text-center">
              <p className="text-sm text-muted-foreground">
                No games found for &quot;{debouncedQuery}&quot;. Try a different search term or use manual BGG ID below.
              </p>
            </div>
          )}
        </div>
      </Card>

      {/* Manual BGG ID Section */}
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
                        {manualPreview.yearPublished && <span>{manualPreview.yearPublished}</span>}
                        {manualPreview.minPlayers && manualPreview.maxPlayers && (
                          <span>
                            {manualPreview.minPlayers}-{manualPreview.maxPlayers} players
                          </span>
                        )}
                        <span className="text-xs">BGG #{manualPreview.id}</span>
                      </div>
                    </div>
                  </div>

                  <Button onClick={handleConfirmManual} className="w-full" data-testid="confirm-manual-bgg-btn">
                    <CheckCircle2 className="mr-2 h-4 w-4" />
                    Use This Game
                  </Button>
                </div>
              )}
            </div>
          </div>
        </Card>
      </div>

      {/* Selected Game Summary */}
      {selectedBggId && bggGameData && (
        <Alert>
          <CheckCircle2 className="h-4 w-4 text-green-500" />
          <AlertTitle>Game Selected</AlertTitle>
          <AlertDescription className="text-sm">
            Selected: <strong>{bggGameData.name}</strong> (BGG #{selectedBggId}). Click &quot;Next&quot; to
            continue.
          </AlertDescription>
        </Alert>
      )}
    </div>
  );
}
