'use client';

import { useEffect, useState, type FormEvent, type JSX } from 'react';

import { useMutation } from '@tanstack/react-query';

import { EmptyState } from '@/components/empty-state/EmptyState';
import { HubPageContainer } from '@/components/layout/PageContainer';
import { getEntityToken } from '@/components/ui/entity-tokens';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { ConfirmationDialog } from '@/components/ui/overlays/confirmation-dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { useGames } from '@/hooks/queries/useGames';
import { useTranslation } from '@/hooks/useTranslation';
import { api } from '@/lib/api';
import type { SetupGuideResponse, Snippet } from '@/lib/api/schemas';
import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';

import { CitationDialog } from './CitationDialog';
import { GuideProgress } from './GuideProgress';
import { SetupStepCard } from './SetupStepCard';

/**
 * Setup Guide page — client orchestrator (Issue: fix/setup-page-redesign).
 *
 * Selects a game, generates an AI setup guide (`POST /api/v1/agents/setup`)
 * and tracks step-completion progress locally. No auth guard here — `/setup`
 * is edge-protected by `proxy.ts` (see `page.tsx`).
 */
export function SetupView(): JSX.Element {
  const { t } = useTranslation();
  const toolkitToken = getEntityToken('toolkit');

  const gamesQuery = useGames();
  const gamesData = gamesQuery.data?.games;
  const games = gamesData ?? [];

  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);
  const [setupGuide, setSetupGuide] = useState<SetupGuideResponse | null>(null);
  const [completedSteps, setCompletedSteps] = useState<Set<number>>(new Set());
  const [selectedStepReferences, setSelectedStepReferences] = useState<Snippet[] | null>(null);
  const [resetConfirmOpen, setResetConfirmOpen] = useState(false);
  const [validationError, setValidationError] = useState<string | null>(null);

  // Auto-select the first game once the list loads, preserving legacy UX.
  useEffect(() => {
    if (!selectedGameId && gamesData && gamesData.length > 0) {
      setSelectedGameId(gamesData[0].id);
    }
  }, [gamesData, selectedGameId]);

  const generateMutation = useMutation({
    mutationFn: (gameId: string) => api.agents.generateSetupGuide({ gameId, chatId: null }),
    onSuccess: guide => {
      setSetupGuide(guide);
      setCompletedSteps(new Set());
    },
    onError: err => {
      logger.error(
        'Failed to generate setup guide',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('SetupView', 'generateSetupGuide', {
          gameId: selectedGameId,
          operation: 'generate_setup_guide',
        })
      );
    },
  });

  const handleGenerate = (e?: FormEvent) => {
    e?.preventDefault();
    if (!selectedGameId) {
      setValidationError(t('pages.setup.errors.noGameSelected'));
      return;
    }
    setValidationError(null);
    setSetupGuide(null);
    generateMutation.mutate(selectedGameId);
  };

  const toggleStepCompletion = (stepNumber: number) => {
    setCompletedSteps(prev => {
      const next = new Set(prev);
      if (next.has(stepNumber)) {
        next.delete(stepNumber);
      } else {
        next.add(stepNumber);
      }
      return next;
    });
  };

  const isLoadingGuide = generateMutation.isPending;
  const guideErrorMessage = validationError
    ? validationError
    : generateMutation.isError
      ? t('pages.setup.errors.generateGuide')
      : null;

  return (
    <HubPageContainer className="flex flex-col gap-6">
      {/* hero */}
      <div className="flex items-center gap-2">
        <span aria-hidden="true" className="text-2xl sm:text-3xl">
          {toolkitToken.emoji}
        </span>
        <div>
          <h1 className="font-quicksand text-2xl font-bold text-foreground sm:text-3xl">
            {t('pages.setup.title')}
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">{t('pages.setup.subtitle')}</p>
        </div>
      </div>

      {/* game selection */}
      <div className="rounded-lg border border-border bg-card p-6">
        <h2 className="mb-4 text-lg font-semibold text-foreground">
          {t('pages.setup.selectGame')}
        </h2>

        {gamesQuery.isLoading ? (
          <Skeleton className="h-11 w-full rounded-md" />
        ) : gamesQuery.isError ? (
          <EmptyState
            variant="error"
            title={t('common.error')}
            description={t('pages.setup.errors.loadGames')}
            action={{
              label: t('pages.setup.errors.retry'),
              onClick: () => void gamesQuery.refetch(),
            }}
          />
        ) : (
          <form onSubmit={handleGenerate} className="flex flex-col gap-3 sm:flex-row sm:items-end">
            <div className="flex-1">
              <label
                htmlFor="setup-game-select"
                className="mb-2 block text-sm font-medium text-muted-foreground"
              >
                {t('pages.setup.gameLabel')}
              </label>
              <Select
                value={selectedGameId ?? undefined}
                onValueChange={value => setSelectedGameId(value)}
                disabled={isLoadingGuide}
              >
                <SelectTrigger id="setup-game-select">
                  <SelectValue placeholder={t('pages.setup.selectGamePlaceholder')} />
                </SelectTrigger>
                <SelectContent>
                  {games.map(game => (
                    <SelectItem key={game.id} value={game.id}>
                      {/* eslint-disable-next-line local/prefer-use-game-title -- GameManagement's Game DTO has no `translations` field (that's a SharedGameCatalog-only concept); this is the canonical title, same as useGameTitle would resolve with translations: null. */}
                      {game.title}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>

            <Button
              type="submit"
              disabled={!selectedGameId || isLoadingGuide}
              aria-label={t('pages.setup.generateForGameAria')}
            >
              {isLoadingGuide ? t('pages.setup.generating') : t('pages.setup.generateButton')}
            </Button>
          </form>
        )}
      </div>

      {/* generation error */}
      {guideErrorMessage && !isLoadingGuide && (
        <div
          role="alert"
          className="flex flex-wrap items-center gap-3 rounded-lg border border-destructive/40 bg-destructive/10 px-4 py-3 text-sm text-destructive"
        >
          <span className="flex-1">{guideErrorMessage}</span>
          {generateMutation.isError && (
            <Button type="button" variant="outline" size="sm" onClick={() => handleGenerate()}>
              {t('pages.setup.errors.retry')}
            </Button>
          )}
        </div>
      )}

      {/* generating */}
      {isLoadingGuide && (
        <div
          role="status"
          aria-busy="true"
          className="rounded-lg border border-border bg-card p-12 text-center"
        >
          <p className="mb-2 text-base text-muted-foreground">{t('pages.setup.loading.title')}</p>
          <p className="text-sm text-muted-foreground">{t('pages.setup.loading.subtitle')}</p>
        </div>
      )}

      {/* generated guide */}
      {setupGuide && !isLoadingGuide && (
        <>
          <GuideProgress
            gameTitle={setupGuide.gameTitle}
            estimatedMinutes={setupGuide.estimatedSetupTimeMinutes}
            completedCount={completedSteps.size}
            totalCount={setupGuide.steps.length}
            confidence={setupGuide.confidence}
            canReset={completedSteps.size > 0}
            onReset={() => setResetConfirmOpen(true)}
          />

          <div className="flex flex-col gap-4">
            {setupGuide.steps.map(step => (
              <SetupStepCard
                key={step.stepNumber}
                step={step}
                isCompleted={completedSteps.has(step.stepNumber)}
                onToggleComplete={() => toggleStepCompletion(step.stepNumber)}
                onViewReferences={() => setSelectedStepReferences(step.references)}
              />
            ))}
          </div>
        </>
      )}

      {/* empty (no guide yet) */}
      {!setupGuide && !isLoadingGuide && !guideErrorMessage && (
        <EmptyState
          variant="noData"
          title={t('pages.setup.empty.title')}
          description={t('pages.setup.empty.description')}
        />
      )}

      <ConfirmationDialog
        isOpen={resetConfirmOpen}
        onClose={() => setResetConfirmOpen(false)}
        onConfirm={() => setCompletedSteps(new Set())}
        title={t('pages.setup.resetProgress')}
        message={t('pages.setup.resetConfirm')}
        confirmText={t('common.confirm')}
        cancelText={t('common.cancel')}
        variant="warning"
      />

      <CitationDialog
        snippets={selectedStepReferences}
        onClose={() => setSelectedStepReferences(null)}
      />
    </HubPageContainer>
  );
}
