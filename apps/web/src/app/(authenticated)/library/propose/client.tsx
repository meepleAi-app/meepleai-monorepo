'use client';

/**
 * Editor Proposal Wizard - 5-Step Workflow
 * Issue #6: Editor Proposal Wizard
 *
 * Flow: Upload PDF → Create Game → Config Agent → Test Q&A → Request Approval
 * Result: Creates ShareRequest (status: PendingApproval) for admin review
 *
 * Reuses admin wizard steps 1-4, new step 5: RequestApprovalStep
 */

import { useState, useCallback } from 'react';

import { useRouter } from 'next/navigation';

import { ChatSetupStep } from '@/app/(authenticated)/admin/wizard/steps/ChatSetupStep';
import { GameCreationStep } from '@/app/(authenticated)/admin/wizard/steps/GameCreationStep';
import { PdfUploadStep } from '@/app/(authenticated)/admin/wizard/steps/PdfUploadStep';
import { QAStep } from '@/app/(authenticated)/admin/wizard/steps/QAStep';
import { toast } from '@/components/layout';
import { Card } from '@/components/ui/data-display/card';

// Reuse admin wizard steps

// New step for editor workflow
import { RequestApprovalStep } from './steps/RequestApprovalStep';

type WizardStep = 'upload' | 'game' | 'chat' | 'qa' | 'approval';

interface EditorWizardState {
  currentStep: WizardStep;
  pdfId: string | null;
  pdfFileName: string | null;
  gameId: string | null;
  gameName: string | null;
  chatThreadId: string | null;
}

const STEPS = [
  { id: 'upload', label: '1. Upload PDF', icon: '📄' },
  { id: 'game', label: '2. Create Game', icon: '🎮' },
  { id: 'chat', label: '3. Config Agent', icon: '🤖' },
  { id: 'qa', label: '4. Test Q&A', icon: '💬' },
  { id: 'approval', label: '5. Request Approval', icon: '📤' },
];

export function EditorProposalClient() {
  const router = useRouter();

  const [state, setState] = useState<EditorWizardState>({
    currentStep: 'upload',
    pdfId: null,
    pdfFileName: null,
    gameId: null,
    gameName: null,
    chatThreadId: null,
  });

  const goToNextStep = useCallback(() => {
    const stepOrder: WizardStep[] = ['upload', 'game', 'chat', 'qa', 'approval'];
    const currentIndex = stepOrder.indexOf(state.currentStep);
    if (currentIndex < stepOrder.length - 1) {
      setState(prev => ({ ...prev, currentStep: stepOrder[currentIndex + 1] }));
    }
  }, [state.currentStep]);

  const goBack = useCallback(() => {
    const stepOrder: WizardStep[] = ['upload', 'game', 'chat', 'qa', 'approval'];
    const currentIndex = stepOrder.indexOf(state.currentStep);
    if (currentIndex > 0) {
      setState(prev => ({ ...prev, currentStep: stepOrder[currentIndex - 1] }));
    }
  }, [state.currentStep]);

  // Step 1: PDF uploaded
  const handlePdfUploaded = useCallback((pdfId: string, fileName: string) => {
    setState(prev => ({ ...prev, pdfId, pdfFileName: fileName }));
    goToNextStep();
  }, [goToNextStep]);

  // Step 2: Game created
  const handleGameCreated = useCallback((gameId: string, gameName: string) => {
    setState(prev => ({ ...prev, gameId, gameName }));
    goToNextStep();
  }, [goToNextStep]);

  // Step 3: Chat ready
  const handleChatReady = useCallback((chatThreadId: string | null) => {
    setState(prev => ({ ...prev, chatThreadId }));
    goToNextStep();
  }, [goToNextStep]);

  // Step 4: Q&A complete
  const handleQAComplete = useCallback(() => {
    goToNextStep();
  }, [goToNextStep]);

  // Step 5: Approval requested
  const handleApprovalRequested = useCallback(() => {
    toast.success('Proposta inviata! In attesa di approvazione admin.');
    router.push('/library/proposals');
  }, [router]);

  const currentStepIndex = STEPS.findIndex(s => s.id === state.currentStep);

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-900 dark:to-slate-800">
      <div className="max-w-4xl mx-auto px-4 py-8">
        {/* Header */}
        <div className="flex items-center justify-between mb-8">
          <div>
            <h1 className="text-3xl font-bold text-slate-900 dark:text-white mb-2">
              Propose Game to Catalog
            </h1>
            <p className="text-slate-600 dark:text-slate-400">
              Submit a game for admin approval to shared catalog
            </p>
          </div>
          <button
            onClick={() => router.push('/library')}
            className="px-4 py-2 text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white"
          >
            ← Cancel
          </button>
        </div>

        {/* Step Indicator */}
        <div className="mb-8 flex items-center justify-between">
          {STEPS.map((step, index) => {
            const isActive = step.id === state.currentStep;
            const isCompleted = index < currentStepIndex;

            return (
              <div key={step.id} className="flex-1 flex flex-col items-center relative">
                <div className={`w-12 h-12 rounded-full flex items-center justify-center text-xl mb-2 transition-colors ${
                  isActive ? 'bg-blue-600 text-white' :
                  isCompleted ? 'bg-green-600 text-white' :
                  'bg-slate-200 dark:bg-slate-700 text-slate-500'
                }`}>
                  {isCompleted ? '✓' : step.icon}
                </div>
                <span className={`text-sm font-medium ${
                  isActive ? 'text-blue-600' : isCompleted ? 'text-green-600' : 'text-slate-500'
                }`}>
                  {step.label}
                </span>
                {index < STEPS.length - 1 && (
                  <div className={`absolute top-6 left-1/2 w-full h-0.5 ${
                    isCompleted ? 'bg-green-600' : 'bg-slate-200 dark:bg-slate-700'
                  }`} style={{ transform: 'translateX(50%)' }} />
                )}
              </div>
            );
          })}
        </div>

        {/* Step Content */}
        <Card className="p-6">
          {state.currentStep === 'upload' && (
            <PdfUploadStep onComplete={handlePdfUploaded} />
          )}

          {state.currentStep === 'game' && state.pdfId && (
            <GameCreationStep
              pdfId={state.pdfId}
              pdfFileName={state.pdfFileName}
              onComplete={handleGameCreated}
              onBack={goBack}
            />
          )}

          {state.currentStep === 'chat' && state.gameId && state.pdfId && (
            <ChatSetupStep
              gameId={state.gameId}
              gameName={state.gameName || 'Game'}
              pdfId={state.pdfId}
              onComplete={handleChatReady}
              onBack={goBack}
            />
          )}

          {state.currentStep === 'qa' && state.gameId && state.chatThreadId && (
            <QAStep
              gameId={state.gameId}
              gameName={state.gameName || 'Game'}
              chatThreadId={state.chatThreadId}
              onReset={() => router.push('/library')}
              onNext={handleQAComplete}
            />
          )}

          {state.currentStep === 'approval' && state.gameId && state.pdfId && (
            <RequestApprovalStep
              gameId={state.gameId}
              gameName={state.gameName || 'Game'}
              pdfId={state.pdfId}
              pdfFileName={state.pdfFileName || 'PDF'}
              onComplete={handleApprovalRequested}
              onBack={goBack}
            />
          )}
        </Card>
      </div>
    </div>
  );
}
