'use client';

/**
 * FirstGameStep Component
 * Issue #132 - Add first game during onboarding
 *
 * The user looks a game up in the public catalog and adds it to their library.
 *
 * #4227: the search is the canonical GamePicker on the catalog. The step used to keep its
 * own search on `GET /games`, which also lists games that are not published.
 */

import { useCallback, useId, useState } from 'react';

import { toast } from 'sonner';

import { AccessibleButton } from '@/components/accessible';
import { GamePicker, type GameOption } from '@/components/features/game-picker';
import { useTranslation } from '@/hooks/useTranslation';
import { api } from '@/lib/api';
import { getErrorMessage } from '@/lib/utils/errorHandler';

export interface FirstGameStepProps {
  onComplete: () => void;
  onSkip: () => void;
  onGameAdded: (gameId: string, gameName: string) => void;
}

export function FirstGameStep({ onComplete, onSkip, onGameAdded }: FirstGameStepProps) {
  const { t } = useTranslation();
  const [selectedGame, setSelectedGame] = useState<GameOption | null>(null);
  const [isAdding, setIsAdding] = useState(false);
  // #4155: «I own a copy». Off by default: declaring ownership stays an explicit choice.
  const [ownsCopy, setOwnsCopy] = useState(false);
  const ownsCopyHintId = useId();
  const [errorMessage, setErrorMessage] = useState('');

  const handleGameChange = useCallback((game: GameOption | null) => {
    setSelectedGame(game);
    // The declaration is about one game: it does not carry over to the next one chosen.
    setOwnsCopy(false);
    setErrorMessage('');
  }, []);

  const handleAddGame = async () => {
    if (!selectedGame) return;

    setIsAdding(true);
    setErrorMessage('');
    try {
      await api.library.addGame(selectedGame.id);
      toast.success(t('pages.onboarding.firstGame.toast.added', { title: selectedGame.title }));
      // #4155: the declaration needs the library entry, so it comes after the add. If it fails the
      // game stays added and onboarding goes on: the chat offers the declaration again.
      if (ownsCopy) {
        try {
          await api.library.declareOwnership(selectedGame.id);
        } catch {
          toast.error(
            t('pages.onboarding.firstGame.toast.ownershipFailed', { title: selectedGame.title })
          );
        }
      }
      onGameAdded(selectedGame.id, selectedGame.title);
      onComplete();
    } catch (err) {
      setErrorMessage(getErrorMessage(err, t('pages.onboarding.firstGame.error.addFailed')));
    } finally {
      setIsAdding(false);
    }
  };

  return (
    <div className="space-y-6">
      <p className="text-sm text-muted-foreground">{t('pages.onboarding.firstGame.intro')}</p>

      {errorMessage && (
        <div
          role="alert"
          aria-live="polite"
          className="rounded-lg border border-destructive/30 bg-destructive/5 px-4 py-3 text-sm text-foreground"
        >
          {errorMessage}
        </div>
      )}

      <GamePicker
        source="catalog"
        value={selectedGame}
        onChange={handleGameChange}
        placeholder={t('pages.onboarding.firstGame.searchPlaceholder')}
        maxResults={8}
        listClassName="max-h-60 overflow-y-auto"
        disabled={isAdding}
        testId="first-game"
      />

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
            {t('pages.onboarding.firstGame.ownsCopy.label')}
          </label>
          <p id={ownsCopyHintId} className="mt-1 pl-6 text-xs text-muted-foreground">
            {t('pages.onboarding.firstGame.ownsCopy.hint')}
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
            loadingText={t('pages.onboarding.firstGame.adding')}
          >
            {t('pages.onboarding.firstGame.add')}
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
          {t('pages.onboarding.firstGame.skip')}
        </button>
      </div>
    </div>
  );
}
