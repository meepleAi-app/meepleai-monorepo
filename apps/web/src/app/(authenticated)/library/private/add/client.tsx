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

import { Bot, Check, FileText, Gamepad2 } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { GameCreationStep } from '@/app/(authenticated)/admin/wizard/steps/GameCreationStep';
import { PdfUploadStep } from '@/app/(authenticated)/admin/wizard/steps/PdfUploadStep';
import { toast } from '@/components/layout';
import { PdfProcessingStatus } from '@/components/library/PdfProcessingStatus';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';

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

  const STEPS: { id: WizardStep; label: string; icon: React.ReactNode }[] = [
    {
      id: 'game',
      label: t('privateGames.steps.createGame'),
      icon: <Gamepad2 className="h-4.5 w-4.5" />,
    },
    {
      id: 'pdf',
      label: t('privateGames.steps.uploadPdf'),
      icon: <FileText className="h-4.5 w-4.5" />,
    },
    {
      id: 'agent',
      label: t('privateGames.steps.configAgent'),
      icon: <Bot className="h-4.5 w-4.5" />,
    },
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
    if (onComplete) {
      onComplete();
    } else {
      router.push('/library/private');
    }
  }, [onComplete, router, state.gameName]);

  // Step 2: PDF uploaded
  // Issue #5217: For catalog games (startAtPdf=true) the backend endpoint now supports shared
  // catalog game IDs via the library membership check. Show PdfProcessingStatus so the user
  // can see KB creation progress before the drawer closes.
  const handlePdfUploaded = useCallback((pdfId: string, fileName: string) => {
    setState(prev => ({ ...prev, pdfId, pdfFileName: fileName }));
    setShowProcessing(true);
  }, []);

  // Step 2: User clicks "Continue to agent" from PdfProcessingStatus
  // For catalog games (isCatalogGame=true), advance to agent step.
  // For manually-created private games, the agent creation endpoint requires a shared
  // catalog game ID — skip directly to completion to avoid a confusing 404 error.
  const handleContinueToAgent = useCallback(() => {
    setShowProcessing(false);
    if (state.isCatalogGame) {
      setState(prev => ({ ...prev, currentStep: 'agent' }));
    } else {
      toast.success(`Gioco "${state.gameName}" aggiunto con PDF!`);
      if (onComplete) {
        onComplete();
      } else {
        router.push('/library/private');
      }
    }
  }, [state.isCatalogGame, state.gameName, onComplete, router]);

  // Step 2: Skip PDF from upload step
  const handleSkipPdfStep = useCallback(() => {
    toast.success(`Gioco "${state.gameName}" aggiunto senza PDF!`);
    if (onComplete) {
      onComplete();
    } else {
      router.push('/library/private');
    }
  }, [onComplete, router, state.gameName]);

  // Step 3: Agent configured
  const handleAgentConfigured = useCallback(() => {
    toast.success(`Gioco "${state.gameName}" aggiunto con agente RAG!`);
    if (onComplete) {
      onComplete();
    } else {
      router.push('/library/private');
    }
  }, [onComplete, router, state.gameName]);

  // Step 3: Skip agent
  const handleSkipAgent = useCallback(() => {
    toast.success(`Gioco "${state.gameName}" aggiunto con PDF!`);
    if (onComplete) {
      onComplete();
    } else {
      router.push('/library/private');
    }
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

  // Filter visible steps (hide agent if no PDF and not on agent step)
  const visibleSteps = STEPS.filter(
    step => !(step.id === 'agent' && !state.pdfId && state.currentStep !== 'agent')
  );

  return (
    <div
      className={
        onCancel
          ? 'px-5 py-5'
          : 'min-h-screen bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-900 dark:to-slate-800'
      }
    >
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
            <Button variant="outline" onClick={() => router.push('/library/private')}>
              {t('privateGames.cancelWizard')}
            </Button>
          </div>
        )}

        {/* Step Indicator */}
        <div className={cn('mb-6', onCancel && 'px-2')}>
          <div className="flex items-center">
            {visibleSteps.map((step, index) => {
              const globalIndex = STEPS.findIndex(s => s.id === step.id);
              const isActive = step.id === state.currentStep;
              const isCompleted = globalIndex < currentStepIndex;

              return (
                <div key={step.id} className="flex items-center flex-1 last:flex-initial">
                  {/* Step node */}
                  <div className="flex flex-col items-center gap-1.5">
                    <div
                      className={cn(
                        'w-10 h-10 rounded-full flex items-center justify-center transition-all duration-300',
                        isCompleted && 'bg-emerald-500 text-white shadow-sm shadow-emerald-500/30',
                        isActive &&
                          'bg-amber-500 text-white shadow-md shadow-amber-500/30 ring-4 ring-amber-500/15',
                        !isActive && !isCompleted && 'bg-muted text-muted-foreground/50'
                      )}
                    >
                      {isCompleted ? <Check className="h-4 w-4 stroke-[3]" /> : step.icon}
                    </div>
                    <span
                      className={cn(
                        'text-xs font-medium text-center max-w-[80px] leading-tight',
                        isActive && 'text-amber-600 dark:text-amber-400',
                        isCompleted && 'text-emerald-600 dark:text-emerald-400',
                        !isActive && !isCompleted && 'text-muted-foreground/50'
                      )}
                    >
                      {step.label}
                    </span>
                  </div>
                  {/* Connector line */}
                  {index < visibleSteps.length - 1 && (
                    <div className="flex-1 mx-3 mb-5">
                      <div
                        className={cn(
                          'h-0.5 rounded-full transition-colors duration-300',
                          isCompleted ? 'bg-emerald-500' : 'bg-muted'
                        )}
                      />
                    </div>
                  )}
                </div>
              );
            })}
          </div>
        </div>

        {/* Step Content */}
        <div
          className={cn(
            'rounded-xl border border-border/60 bg-card/80 backdrop-blur-sm p-5',
            onCancel && 'border-border/40'
          )}
        >
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
                ? { gameId: state.gameId } // shared catalog game → 'gameId' field
                : { privateGameId: state.gameId })} // manually created PrivateGame → 'privateGameId' field
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
        </div>

        {/* Progress Summary */}
        {state.gameId && (state.gameName || state.pdfFileName) && (
          <div className="mt-4 rounded-lg border border-border/40 bg-muted/30 px-4 py-3">
            <h3 className="text-xs font-semibold uppercase tracking-wider text-muted-foreground mb-2">
              {t('privateGames.progress')}
            </h3>
            <div className="flex flex-wrap gap-x-6 gap-y-1 text-sm">
              {state.gameName && (
                <div className="flex items-center gap-1.5">
                  <Gamepad2 className="h-3.5 w-3.5 text-amber-500" />
                  <span className="font-medium text-foreground">{state.gameName}</span>
                </div>
              )}
              {state.pdfFileName && (
                <div className="flex items-center gap-1.5">
                  <FileText className="h-3.5 w-3.5 text-emerald-500" />
                  <span className="font-medium text-foreground truncate max-w-[180px]">
                    {state.pdfFileName}
                  </span>
                </div>
              )}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
