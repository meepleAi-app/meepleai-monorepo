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

import { toast } from '@/components/layout';
import { Card } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';

// Reuse refactored admin wizard steps
import { GameCreationStep } from '@/app/(authenticated)/admin/wizard/steps/GameCreationStep';
import { PdfUploadStep } from '@/app/(authenticated)/admin/wizard/steps/PdfUploadStep';

// User-specific step
import { ConfigAgentStep } from './steps/ConfigAgentStep';

type WizardStep = 'game' | 'pdf' | 'agent' | 'complete';

interface UserWizardState {
  currentStep: WizardStep;
  gameId: string | null;
  gameName: string | null;
  pdfId: string | null;
  pdfFileName: string | null;
}

const STEPS: { id: WizardStep; label: string; icon: string }[] = [
  { id: 'game', label: '1. Create Game', icon: '🎮' },
  { id: 'pdf', label: '2. Upload PDF (Optional)', icon: '📄' },
  { id: 'agent', label: '3. Config Agent (Optional)', icon: '🤖' },
];

export function UserWizardClient() {
  const router = useRouter();

  const [state, setState] = useState<UserWizardState>({
    currentStep: 'game',
    gameId: null,
    gameName: null,
    pdfId: null,
    pdfFileName: null,
  });

  // Step 1: Game created
  const handleGameCreated = useCallback((gameId: string, gameName: string) => {
    setState(prev => ({ ...prev, gameId, gameName, currentStep: 'pdf' }));
  }, []);

  // Step 1: Skip PDF (go directly to complete)
  const handleSkipPdf = useCallback(() => {
    toast.success(`Gioco "${state.gameName}" aggiunto alla tua libreria!`);
    router.push('/library/private');
  }, [router, state.gameName]);

  // Step 2: PDF uploaded
  const handlePdfUploaded = useCallback((pdfId: string, fileName: string) => {
    setState(prev => ({ ...prev, pdfId, pdfFileName: fileName, currentStep: 'agent' }));
  }, []);

  // Step 2: Skip PDF from upload step
  const handleSkipPdfStep = useCallback(() => {
    toast.success(`Gioco "${state.gameName}" aggiunto senza PDF!`);
    router.push('/library/private');
  }, [router, state.gameName]);

  // Step 3: Agent configured
  const handleAgentConfigured = useCallback(() => {
    toast.success(`Gioco "${state.gameName}" aggiunto con agente RAG!`);
    router.push('/library/private');
  }, [router, state.gameName]);

  // Step 3: Skip agent
  const handleSkipAgent = useCallback(() => {
    toast.success(`Gioco "${state.gameName}" aggiunto con PDF!`);
    router.push('/library/private');
  }, [router, state.gameName]);

  // Back navigation
  const goBack = useCallback(() => {
    const stepOrder: WizardStep[] = ['game', 'pdf', 'agent'];
    const currentIndex = stepOrder.indexOf(state.currentStep);
    if (currentIndex > 0) {
      setState(prev => ({ ...prev, currentStep: stepOrder[currentIndex - 1] }));
    }
  }, [state.currentStep]);

  const currentStepIndex = STEPS.findIndex(s => s.id === state.currentStep);

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-900 dark:to-slate-800">
      <div className="max-w-4xl mx-auto px-4 py-8">
        {/* Header */}
        <div className="flex items-center justify-between mb-8">
          <div>
            <h1 className="text-3xl font-bold text-slate-900 dark:text-white mb-2">
              Add Game to Library
            </h1>
            <p className="text-slate-600 dark:text-slate-400">
              Add a new game to your private collection
            </p>
          </div>
          <Button
            variant="outline"
            onClick={() => router.push('/library/private')}
          >
            ← Cancel
          </Button>
        </div>

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
              onComplete={handleGameCreated}
              onSkipPdf={handleSkipPdf}
              onBack={() => router.push('/library/private')}
            />
          )}

          {state.currentStep === 'pdf' && state.gameId && (
            <PdfUploadStep
              onComplete={handlePdfUploaded}
            />
          )}

          {state.currentStep === 'agent' && state.gameId && state.pdfId && (
            <ConfigAgentStep
              gameId={state.gameId}
              gameName={state.gameName || 'Game'}
              pdfId={state.pdfId}
              onComplete={handleAgentConfigured}
              onSkip={handleSkipAgent}
              onBack={goBack}
            />
          )}
        </Card>

        {/* Progress Summary */}
        {state.gameId && (
          <Card className="mt-6 p-4 bg-slate-100 dark:bg-slate-700/50">
            <h3 className="text-sm font-semibold mb-2">Progress</h3>
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
