/**
 * Gamebook upload page — client shell (Sprint 1, Task 1.8)
 *
 * G7 (smartphone-ready): Two-step wizard:
 *   Step 1 — pick-game: search and select a game from SharedGameCatalog
 *   Step 2 — upload: photo capture + status polling (PhotoUploader)
 *
 * If `?gameId=` is already present in the URL the wizard skips step 1
 * and goes directly to step 2 (backward-compatible with existing links).
 */

'use client';

import { useCallback, useState, type JSX } from 'react';

import { useQuery } from '@tanstack/react-query';
import { useSearchParams } from 'next/navigation';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { useTranslation } from '@/hooks/useTranslation';
import { searchSharedGames, type SharedGameV2 } from '@/lib/api/shared-games';

import { PhotoUploader } from './PhotoUploader';

// ── Types ────────────────────────────────────────────────────────────────────

type Step = 'pick-game' | 'upload';

// ── Sub-component: game picker ────────────────────────────────────────────────

interface GamePickerProps {
  onSelect: (game: SharedGameV2) => void;
}

function GamePicker({ onSelect }: GamePickerProps): JSX.Element {
  const { t } = useTranslation();
  const [search, setSearch] = useState('');

  const { data, isLoading, isError } = useQuery({
    queryKey: ['shared-games', 'picker', search],
    queryFn: () =>
      searchSharedGames({
        search: search.trim() || undefined,
        pageSize: 20,
        pageNumber: 1,
      }),
    // Debounce: only refetch when search string stabilises (staleTime = 0 ensures reactivity)
    staleTime: 0,
  });

  const handleSearchChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    setSearch(e.target.value);
  }, []);

  return (
    <section data-testid="game-picker">
      <h2 className="text-xl font-bold mb-1">
        {t('gamebook.upload.pickerTitle', 'Choose a game manual to photograph')}
      </h2>
      <p className="text-sm text-muted-foreground mb-4">
        {t(
          'gamebook.upload.pickerSubtitle',
          'Select the board game whose manual you want to index'
        )}
      </p>

      {/* Search input */}
      <input
        type="search"
        value={search}
        onChange={handleSearchChange}
        placeholder={t('gamebook.upload.pickerSearchPlaceholder', 'Search games...')}
        data-testid="game-search-input"
        className="w-full rounded-md border border-input bg-background px-3 py-2 text-sm placeholder:text-muted-foreground mb-4 focus:outline-none focus:ring-2 focus:ring-ring"
        aria-label={t('gamebook.upload.pickerSearchPlaceholder', 'Search games...')}
      />

      {/* Loading */}
      {isLoading && (
        <p className="text-sm text-muted-foreground" data-testid="picker-loading">
          {t('gamebook.upload.pickerLoading', 'Loading games...')}
        </p>
      )}

      {/* Error */}
      {isError && (
        <p role="alert" className="text-sm text-destructive" data-testid="picker-error">
          {t('gamebook.upload.pickerError', 'Could not load games. Please try again.')}
        </p>
      )}

      {/* Game grid */}
      {!isLoading && !isError && (
        <div
          className="grid grid-cols-2 sm:grid-cols-3 gap-3"
          data-testid="game-picker-grid"
          role="list"
          aria-label={t('gamebook.upload.pickerTitle', 'Choose a game manual to photograph')}
        >
          {data?.items.map(game => (
            <button
              key={game.id}
              type="button"
              role="listitem"
              onClick={() => onSelect(game)}
              data-testid={`game-option-${game.id}`}
              className="text-left rounded-lg focus:outline-none focus:ring-2 focus:ring-ring"
              aria-label={game.title}
            >
              <MeepleCard
                entity="game"
                variant="compact"
                title={game.title}
                subtitle={game.yearPublished ? String(game.yearPublished) : undefined}
                imageUrl={game.imageUrl || undefined}
              />
            </button>
          ))}

          {/* Empty state */}
          {data?.items.length === 0 && (
            <p className="col-span-full text-sm text-muted-foreground" data-testid="picker-empty">
              {search.trim()
                ? t('gamebook.upload.pickerNoResults', { search })
                : t('gamebook.upload.pickerNoGames', 'No games available yet.')}
            </p>
          )}
        </div>
      )}
    </section>
  );
}

// ── Main component ────────────────────────────────────────────────────────────

/**
 * Two-step wizard: pick a game → upload photos.
 *
 * If `?gameId=<uuid>` is present the wizard skips step 1.
 */
export function GamebookUploadClient(): JSX.Element {
  const { t } = useTranslation();
  const searchParams = useSearchParams();
  const initialGameId = searchParams.get('gameId');

  const [step, setStep] = useState<Step>(initialGameId ? 'upload' : 'pick-game');
  const [gameId, setGameId] = useState<string | null>(initialGameId);
  const [gameTitle, setGameTitle] = useState<string | null>(null);

  const handleGameSelect = useCallback((game: SharedGameV2) => {
    setGameId(game.id);
    setGameTitle(game.title);
    setStep('upload');
  }, []);

  const handleBack = useCallback(() => {
    setStep('pick-game');
    // Keep gameId so if user comes back they see the same game pre-selected
  }, []);

  // ── Step 1: game picker ──────────────────────────────────────────────────
  if (step === 'pick-game') {
    return (
      <div className="space-y-6" data-testid="gamebook-wizard-pick">
        <GamePicker onSelect={handleGameSelect} />
      </div>
    );
  }

  // ── Step 2: photo upload ─────────────────────────────────────────────────
  return (
    <div className="space-y-6" data-testid="gamebook-wizard-upload">
      {/* Back + game title */}
      <div>
        <button
          type="button"
          onClick={handleBack}
          data-testid="back-to-picker"
          className="mb-2 inline-flex items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
          aria-label={t('gamebook.upload.backToPicker', 'Change game')}
        >
          <span aria-hidden>←</span>
          {t('gamebook.upload.backToPicker', 'Change game')}
        </button>
        <h1 className="text-2xl font-bold">
          {t('gamebook.upload.title', 'Upload Game Manual Photos')}
        </h1>
        {gameTitle && (
          <p className="mt-1 text-sm font-medium text-primary" data-testid="selected-game-title">
            {gameTitle}
          </p>
        )}
        <p className="mt-1 text-sm text-muted-foreground">
          {t(
            'gamebook.upload.subtitle',
            'Upload photos of your board game manual for AI-powered indexing'
          )}
        </p>
      </div>

      <PhotoUploader gameId={gameId!} />
    </div>
  );
}
