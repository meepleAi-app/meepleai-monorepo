'use client';

/**
 * User Wizard Client - 3-Step Game Addition
 * Issue #4: User Game Creation Wizard
 *
 * Flow:
 * Step 1: Create Game (required)
 * Step 2: Upload PDF (optional - can skip)
 * Step 3: Config Agent (optional - only if PDF uploaded)
 *
 * Uses refactored admin wizard components (Option A strategy)
 */

import { useState, useCallback } from 'react';

import { useRouter } from 'next/navigation';

import { GameCreationStep } from '@/app/(authenticated)/admin/wizard/steps/GameCreationStep';
import { PdfUploadStep } from '@/app/(authenticated)/admin/wizard/steps/PdfUploadStep';
import { toast } from '@/components/layout';
import { PdfProcessingStatus } from '@/components/library/PdfProcessingStatus';
import { Card } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';

// Reuse refactored admin wizard steps

// User-specific step
import { ConfigAgentStep } from './steps/ConfigAgentStep';

type WizardStep = 'game' | 'pdf' | 'agent' | 'complete';

interface UserWizardState {
  currentStep: WizardStep;
  gameId: string | null;
  gameName: string | null;
  pdfId: string | null;
  pdfFileName: string | null;
  /** True when the game was selected from the shared catalog (gameId = shared catalog ID). */
  isCatalogGame: boolean;
}

interface UserWizardClientProps {
  /**
   * Called when the wizard completes successfully (any completion path).
   * When provided the wizard does NOT push to /library/private.
   * Used by AddGameDrawer to close the sheet after completion.
   */
  onComplete?: () => void;
  /**
   * Called when the user cancels (Cancel button or back-to-start action).
   * When provided the internal header cancel button is hidden (drawer owns that).
   * Used by AddGameDrawer to go back to Step 0 (choice).
   */
  onCancel?: () => void;
  /**
   * Pre-set game ID when starting from a catalog selection (Issue #5169).
   * Combined with `startAtPdf` to skip the game creation step.
   */
  gameId?: string;
  /**
   * Pre-set game name when starting from a catalog selection (Issue #5169).
   */
  gameName?: string;
  /**
   * When true, wizard starts at the PDF upload step instead of game creation.
   * Requires `gameId` and `gameName` to be provided.
   * Used by AddGameDrawer after a successful catalog selection.
   */
  startAtPdf?: boolean;
}

export function UserWizardClient({
  onComplete,
  onCancel,
  gameId: initialGameId,
  gameName: initialGameName,
  startAtPdf = false,
}: UserWizardClientProps = {}) {
  const router = useRouter();
  const { t } = useTranslation();

  const STEPS: { id: WizardStep; label: string; icon: string }[] = [
    { id: 'game', label: t('privateGames.steps.createGame'), icon: '🎮' },
    { id: 'pdf', label: t('privateGames.steps.uploadPdf'), icon: '📄' },
    { id: 'agent', label: t('privateGames.steps.configAgent'), icon: '🤖' },
  ];

  const [state, setState] = useState<UserWizardState>({
    currentStep: startAtPdf && initialGameId ? 'pdf' : 'game',
    gameId: initialGameId ?? null,
    gameName: initialGameName ?? null,
    pdfId: null,
    pdfFileName: null,
    // When startAtPdf is true the wizard was entered from the catalog: gameId is a
    // shared catalog ID, NOT a PrivateGame ID — upload must use 'gameId' form field.
    isCatalogGame: !!(startAtPdf && initialGameId),
  });

  // Track whether to show PdfProcessingStatus after upload (stays on 'pdf' step)
  const [showProcessing, setShowProcessing] = useState(false);

  // Step 1: Game created (manual flow — gameId is a PrivateGame ID)
  const handleGameCreated = useCallback((gameId: string, gameName: string) => {
    setState(prev => ({ ...prev, gameId, gameName, currentStep: 'pdf', isCatalogGame: false }));
  }, []);

  // Step 1: Skip PDF (go directly to complete)
  const handleSkipPdf = useCallback(() => {
    toast.success(`Gioco "${state.gameName}" aggiunto alla tua libreria!`);
    if (onComplete) { onComplete(); } else { router.push('/library/private'); }
  }, [onComplete, router, state.gameName]);

  // Step 2: PDF uploaded
  // For catalog games (startAtPdf=true): the gameId is a shared catalog UUID, not a PrivateGame ID.
  // PdfProcessingStatus polls /library/games/{gameId}/pdf-status which only works for PrivateGame IDs.
  // For catalog games we complete the flow immediately after upload; the PDF indexes in the background.
  const handlePdfUploaded = useCallback((pdfId: string, fileName: string) => {
    if (startAtPdf) {
      toast.success(`PDF caricato con successo!`);
      if (onComplete) { onComplete(); } else { router.push('/library/private'); }
    } else {
      setState(prev => ({ ...prev, pdfId, pdfFileName: fileName }));
      setShowProcessing(true);
    }
  }, [startAtPdf, onComplete, router]);

  // Step 2: User clicks "Continue to agent" from PdfProcessingStatus
  const handleContinueToAgent = useCallback(() => {
    setShowProcessing(false);
    setState(prev => ({ ...prev, currentStep: 'agent' }));
  }, []);

  // Step 2: Skip PDF from upload step
  const handleSkipPdfStep = useCallback(() => {
    toast.success(`Gioco "${state.gameName}" aggiunto senza PDF!`);
    if (onComplete) { onComplete(); } else { router.push('/library/private'); }
  }, [onComplete, router, state.gameName]);

  // Step 3: Agent configured
  const handleAgentConfigured = useCallback(() => {
    toast.success(`Gioco "${state.gameName}" aggiunto con agente RAG!`);
    if (onComplete) { onComplete(); } else { router.push('/library/private'); }
  }, [onComplete, router, state.gameName]);

  // Step 3: Skip agent
  const handleSkipAgent = useCallback(() => {
    toast.success(`Gioco "${state.gameName}" aggiunto con PDF!`);
    if (onComplete) { onComplete(); } else { router.push('/library/private'); }
  }, [onComplete, router, state.gameName]);

  // Back navigation
  const goBack = useCallback(() => {
    // If started at PDF (catalog flow) and on pdf step, back goes to catalog
    if (startAtPdf && state.currentStep === 'pdf' && onCancel) {
      onCancel();
      return;
    }
    const stepOrder: WizardStep[] = ['game', 'pdf', 'agent'];
    const currentIndex = stepOrder.indexOf(state.currentStep);
    if (currentIndex > 0) {
      setState(prev => ({ ...prev, currentStep: stepOrder[currentIndex - 1] }));
    }
  }, [startAtPdf, state.currentStep, onCancel]);

  const currentStepIndex = STEPS.findIndex(s => s.id === state.currentStep);

  return (
    <div className={onCancel ? 'px-4 py-4' : 'min-h-screen bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-900 dark:to-slate-800'}>
      <div className={onCancel ? '' : 'max-w-4xl mx-auto px-4 py-8'}>
        {/* Header — hidden when embedded in drawer (drawer owns header) */}
        {!onCancel && (
          <div className="flex items-center justify-between mb-8">
            <div>
              <h1 className="text-3xl font-bold text-slate-900 dark:text-white mb-2">
                {t('privateGames.addToLibrary')}
              </h1>
              <p className="text-slate-600 dark:text-slate-400">
                {t('privateGames.addToLibrarySubtitle')}
              </p>
            </div>
            <Button
              variant="outline"
              onClick={() => router.push('/library/private')}
            >
              {t('privateGames.cancelWizard')}
            </Button>
          </div>
        )}

        {/* Step Indicator */}
        <div className="mb-8">
          <div className="flex items-center justify-between">
            {STEPS.map((step, index) => {
              const isActive = step.id === state.currentStep;
              const isCompleted = index < currentStepIndex;

              // Skip agent step indicator if no PDF
              if (step.id === 'agent' && !state.pdfId && state.currentStep !== 'agent') {
                return null;
              }

              return (
                <div
                  key={step.id}
                  className={`flex-1 ${index < STEPS.length - 1 ? 'relative' : ''}`}
                >
                  <div className="flex flex-col items-center">
                    <div
                      className={`w-12 h-12 rounded-full flex items-center justify-center text-xl mb-2 transition-colors ${
                        isActive
                          ? 'bg-blue-600 text-white'
                          : isCompleted
                            ? 'bg-green-600 text-white'
                            : 'bg-slate-200 dark:bg-slate-700 text-slate-500'
                      }`}
                    >
                      {isCompleted ? '✓' : step.icon}
                    </div>
                    <span className={`text-sm font-medium ${
                      isActive ? 'text-blue-600' : isCompleted ? 'text-green-600' : 'text-slate-500'
                    }`}>
                      {step.label}
                    </span>
                  </div>
                  {index < STEPS.length - 1 && (
                    <div
                      className={`absolute top-6 left-1/2 w-full h-0.5 ${
                        isCompleted ? 'bg-green-600' : 'bg-slate-200 dark:bg-slate-700'
                      }`}
                      style={{ transform: 'translateX(50%)' }}
                    />
                  )}
                </div>
              );
            })}
          </div>
        </div>

        {/* Step Content */}
        <Card className="p-6">
          {state.currentStep === 'game' && (
            <GameCreationStep
              pdfId={null}
              pdfFileName={null}
              allowSkipPdf={true}
              mode="user"
              onComplete={handleGameCreated}
              onSkipPdf={handleSkipPdf}
              onBack={onCancel ?? (() => router.push('/library/private'))}
            />
          )}

          {state.currentStep === 'pdf' && state.gameId && !showProcessing && (
            <PdfUploadStep
              {...(state.isCatalogGame
                ? { gameId: state.gameId }        // shared catalog game → 'gameId' field
                : { privateGameId: state.gameId } // manually created PrivateGame → 'privateGameId' field
              )}
              isPrivate
              onComplete={handlePdfUploaded}
              onSkip={handleSkipPdfStep}
            />
          )}

          {state.currentStep === 'pdf' && state.gameId && showProcessing && state.pdfFileName && (
            <PdfProcessingStatus
              gameId={state.gameId}
              pdfFileName={state.pdfFileName}
              onContinue={handleContinueToAgent}
            />
          )}

          {state.currentStep === 'agent' && state.gameId && state.pdfId && (
            <div className="space-y-6">
              {/* PDF indexing progress — shown while indexing is in progress (Issue #4946) */}
              <PdfProcessingStatus gameId={state.gameId} />

              <ConfigAgentStep
                gameId={state.gameId}
                gameName={state.gameName || 'Game'}
                pdfId={state.pdfId}
                onComplete={handleAgentConfigured}
                onSkip={handleSkipAgent}
                onBack={goBack}
              />
            </div>
          )}
        </Card>

        {/* Progress Summary */}
        {state.gameId && (
          <Card className="mt-6 p-4 bg-slate-100 dark:bg-slate-700/50">
            <h3 className="text-sm font-semibold mb-2">{t('privateGames.progress')}</h3>
            <div className="grid grid-cols-2 gap-4 text-sm">
              {state.gameName && (
                <div>
                  <span className="text-slate-500">Game:</span>{' '}
                  <span className="font-medium">{state.gameName}</span>
                </div>
              )}
              {state.pdfFileName && (
                <div>
                  <span className="text-slate-500">PDF:</span>{' '}
                  <span className="font-medium">{state.pdfFileName}</span>
                </div>
              )}
            </div>
          </Card>
        )}
      </div>
    </div>
  );
}
