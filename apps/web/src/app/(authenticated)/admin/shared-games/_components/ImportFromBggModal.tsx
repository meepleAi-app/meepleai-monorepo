/**
 * ImportFromBggModal Component - Issue #3535
 *
 * Modal dialog for importing games from BoardGameGeek:
 * - BGG ID or URL input with validation
 * - Live preview of fetched game metadata
 * - Duplicate detection with update option
 * - Rate limit handling
 * - Auto-submit for approval option
 */

'use client';

import { useCallback, useState, useEffect, useMemo } from 'react';

import { useMutation, useQueryClient } from '@tanstack/react-query';
import {
  AlertCircle,
  AlertTriangle,
  Clock,
  ExternalLink,
  Loader2,
  Search,
  Upload,
  Users,
} from 'lucide-react';
import Image from 'next/image';

import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { api, type BggGameDetails, type BggDuplicateCheckResult } from '@/lib/api';

export interface ImportFromBggModalProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onSuccess?: (gameId: string) => void;
}

// Regex patterns for BGG ID/URL parsing
const BGG_ID_PATTERN = /^\d+$/;
const BGG_URL_PATTERNS = [
  /boardgamegeek\.com\/boardgame\/(\d+)/,
  /boardgamegeek\.com\/boardgameexpansion\/(\d+)/,
  /bgg\.cc\/boardgame\/(\d+)/,
];

/**
 * Extract BGG ID from input (accepts ID or URL)
 */
function extractBggId(input: string): number | null {
  const trimmed = input.trim();

  // Direct ID
  if (BGG_ID_PATTERN.test(trimmed)) {
    return parseInt(trimmed, 10);
  }

  // URL pattern
  for (const pattern of BGG_URL_PATTERNS) {
    const match = trimmed.match(pattern);
    if (match) {
      return parseInt(match[1], 10);
    }
  }

  return null;
}

/**
 * Validate BGG input
 */
function validateBggInput(input: string): { valid: boolean; error?: string; bggId?: number } {
  const trimmed = input.trim();

  if (!trimmed) {
    return { valid: false, error: 'Please enter a BGG ID or URL' };
  }

  const bggId = extractBggId(trimmed);

  if (!bggId) {
    return {
      valid: false,
      error: 'Invalid BGG ID or URL. Please enter a numeric ID or a valid BGG URL.',
    };
  }

  if (bggId < 1 || bggId > 10000000) {
    return { valid: false, error: 'BGG ID must be a positive number' };
  }

  return { valid: true, bggId };
}

/**
 * Game preview component
 */
function GamePreview({
  game,
  isLoading,
}: {
  game: BggGameDetails | null;
  isLoading: boolean;
}) {
  if (isLoading) {
    return (
      <div className="flex gap-4 rounded-lg border p-4">
        <Skeleton className="h-24 w-24 shrink-0 rounded" />
        <div className="flex-1 space-y-2">
          <Skeleton className="h-6 w-3/4" />
          <Skeleton className="h-4 w-1/4" />
          <Skeleton className="h-4 w-1/2" />
        </div>
      </div>
    );
  }

  if (!game) {
    return null;
  }

  return (
    <div className="flex gap-4 rounded-lg border p-4">
      <div className="relative h-24 w-24 shrink-0 overflow-hidden rounded bg-muted">
        {game.thumbnailUrl ? (
          <Image
            src={game.thumbnailUrl}
            alt={game.name}
            fill
            className="object-cover"
            sizes="96px"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center">
            <Upload className="h-8 w-8 text-muted-foreground/50" />
          </div>
        )}
      </div>
      <div className="flex-1 min-w-0">
        <h3 className="font-semibold text-lg truncate">{game.name}</h3>
        <p className="text-sm text-muted-foreground">
          {game.yearPublished || 'Unknown Year'}
        </p>
        <div className="flex flex-wrap gap-4 mt-2 text-sm text-muted-foreground">
          {game.minPlayers && game.maxPlayers && (
            <div className="flex items-center gap-1">
              <Users className="h-4 w-4" />
              <span>
                {game.minPlayers === game.maxPlayers
                  ? game.minPlayers
                  : `${game.minPlayers}-${game.maxPlayers}`}
              </span>
            </div>
          )}
          {game.playingTime && (
            <div className="flex items-center gap-1">
              <Clock className="h-4 w-4" />
              <span>{game.playingTime} min</span>
            </div>
          )}
          {game.averageRating && (
            <div className="flex items-center gap-1">
              <span className="text-amber-500">★</span>
              <span>{game.averageRating.toFixed(1)}</span>
            </div>
          )}
        </div>
      </div>
      <a
        href={`https://boardgamegeek.com/boardgame/${game.bggId}`}
        target="_blank"
        rel="noopener noreferrer"
        className="shrink-0 text-muted-foreground hover:text-foreground"
      >
        <ExternalLink className="h-5 w-5" />
      </a>
    </div>
  );
}

export function ImportFromBggModal({
  open,
  onOpenChange,
  onSuccess,
}: ImportFromBggModalProps) {
  const queryClient = useQueryClient();

  // Form state
  const [bggInput, setBggInput] = useState('');
  const [autoSubmit, setAutoSubmit] = useState(false);
  const [inputError, setInputError] = useState<string | null>(null);

  // Preview state
  const [previewData, setPreviewData] = useState<BggDuplicateCheckResult | null>(null);
  const [isPreviewLoading, setIsPreviewLoading] = useState(false);
  const [previewError, setPreviewError] = useState<string | null>(null);

  // Debounce timer
  const [debounceTimer, setDebounceTimer] = useState<NodeJS.Timeout | null>(null);

  // Parsed BGG ID
  const parsedBggId = useMemo(() => extractBggId(bggInput), [bggInput]);

  // Reset state when modal closes
  useEffect(() => {
    if (!open) {
      setBggInput('');
      setAutoSubmit(false);
      setInputError(null);
      setPreviewData(null);
      setPreviewError(null);
    }
  }, [open]);

  // Fetch preview on input change (debounced)
  const fetchPreview = useCallback(async (bggId: number) => {
    setIsPreviewLoading(true);
    setPreviewError(null);

    try {
      const result = await api.sharedGames.checkBggDuplicate(bggId);
      setPreviewData(result);
    } catch (err) {
      console.error('Failed to fetch BGG data:', err);
      if (err instanceof Error) {
        if (err.message.includes('429') || err.message.includes('rate limit')) {
          setPreviewError('BGG API rate limit reached. Please wait a moment and try again.');
        } else if (err.message.includes('404') || err.message.includes('not found')) {
          setPreviewError('Game not found on BoardGameGeek. Please check the ID.');
        } else {
          setPreviewError('Failed to fetch game data. Please try again.');
        }
      } else {
        setPreviewError('Failed to fetch game data. Please try again.');
      }
    } finally {
      setIsPreviewLoading(false);
    }
  }, []);

  // Handle input change with debounce
  const handleInputChange = useCallback(
    (value: string) => {
      setBggInput(value);
      setInputError(null);

      // Clear previous timer
      if (debounceTimer) {
        clearTimeout(debounceTimer);
      }

      // Validate input
      const validation = validateBggInput(value);

      if (!validation.valid) {
        setInputError(validation.error ?? null);
        setPreviewData(null);
        return;
      }

      // Debounce API call
      const timer = setTimeout(() => {
        if (validation.bggId) {
          fetchPreview(validation.bggId);
        }
      }, 500);

      setDebounceTimer(timer);
    },
    [debounceTimer, fetchPreview]
  );

  // Cleanup timer on unmount
  useEffect(() => {
    return () => {
      if (debounceTimer) {
        clearTimeout(debounceTimer);
      }
    };
  }, [debounceTimer]);

  // Import mutation
  const importMutation = useMutation({
    mutationFn: async (bggId: number) => {
      const gameId = await api.sharedGames.importFromBgg(bggId);

      // If auto-submit is enabled, submit for approval
      if (autoSubmit) {
        await api.sharedGames.submitForApproval(gameId);
      }

      return gameId;
    },
    onSuccess: (gameId) => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'shared-games'] });
      onOpenChange(false);
      onSuccess?.(gameId);
    },
  });

  // Handle form submission
  const handleSubmit = useCallback(() => {
    const validation = validateBggInput(bggInput);

    if (!validation.valid) {
      setInputError(validation.error ?? null);
      return;
    }

    if (previewData?.isDuplicate) {
      setInputError('This game already exists in the catalog. Use the update option instead.');
      return;
    }

    if (validation.bggId) {
      importMutation.mutate(validation.bggId);
    }
  }, [bggInput, previewData, importMutation]);

  // Check if form is valid
  const isFormValid =
    parsedBggId &&
    !inputError &&
    !previewError &&
    previewData?.bggData &&
    !previewData?.isDuplicate;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="sm:max-w-lg">
        <DialogHeader>
          <DialogTitle>Import from BoardGameGeek</DialogTitle>
          <DialogDescription>
            Enter a BGG ID or URL to import game data into the catalog.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-6 py-4">
          {/* Rate limit warning banner */}
          <Alert variant="default" className="border-amber-200 bg-amber-50 dark:bg-amber-950/20">
            <AlertTriangle className="h-4 w-4 text-amber-600" />
            <AlertDescription className="text-amber-800 dark:text-amber-200">
              BGG API has rate limits. Avoid rapid successive requests to prevent being blocked.
            </AlertDescription>
          </Alert>

          {/* Input field */}
          <div className="space-y-2">
            <Label htmlFor="bgg-input">BGG ID or URL</Label>
            <div className="relative">
              <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
              <Input
                id="bgg-input"
                placeholder="e.g., 13 or https://boardgamegeek.com/boardgame/13/catan"
                value={bggInput}
                onChange={(e) => handleInputChange(e.target.value)}
                className="pl-9"
                aria-describedby={inputError ? 'bgg-input-error' : undefined}
              />
            </div>
            {inputError && (
              <p id="bgg-input-error" className="text-sm text-destructive">
                {inputError}
              </p>
            )}
          </div>

          {/* Preview section */}
          {(isPreviewLoading || previewData?.bggData) && (
            <div className="space-y-2">
              <Label>Preview</Label>
              <GamePreview game={previewData?.bggData ?? null} isLoading={isPreviewLoading} />
            </div>
          )}

          {/* Duplicate warning */}
          {previewData?.isDuplicate && (
            <Alert variant="default" className="border-blue-200 bg-blue-50 dark:bg-blue-950/20">
              <AlertCircle className="h-4 w-4 text-blue-600" />
              <AlertDescription className="text-blue-800 dark:text-blue-200">
                <p className="font-medium">Game already exists in catalog</p>
                <p className="mt-1 text-sm">
                  &quot;{previewData.existingGame?.title}&quot; is already in the catalog.
                  Go to the game detail page to update from BGG.
                </p>
              </AlertDescription>
            </Alert>
          )}

          {/* Preview error */}
          {previewError && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>{previewError}</AlertDescription>
            </Alert>
          )}

          {/* Auto-submit checkbox */}
          {!previewData?.isDuplicate && (
            <div className="flex items-center space-x-2">
              <Checkbox
                id="auto-submit"
                checked={autoSubmit}
                onCheckedChange={(checked) => setAutoSubmit(checked === true)}
              />
              <Label htmlFor="auto-submit" className="text-sm font-normal cursor-pointer">
                Auto-submit for approval after import
              </Label>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)}>
            Cancel
          </Button>
          <Button
            onClick={handleSubmit}
            disabled={!isFormValid || importMutation.isPending}
          >
            {importMutation.isPending ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Importing...
              </>
            ) : (
              <>
                <Upload className="mr-2 h-4 w-4" />
                Import Game
              </>
            )}
          </Button>
        </DialogFooter>

        {/* Import error */}
        {importMutation.isError && (
          <Alert variant="destructive" className="mt-4">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              {importMutation.error instanceof Error
                ? importMutation.error.message
                : 'Failed to import game. Please try again.'}
            </AlertDescription>
          </Alert>
        )}
      </DialogContent>
    </Dialog>
  );
}
