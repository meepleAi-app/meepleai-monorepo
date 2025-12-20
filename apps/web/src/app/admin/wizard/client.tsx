'use client';

/**
 * Admin Game Setup Wizard - Client Component
 *
 * 4-step wizard for admin game setup:
 * 1. Upload PDF → Public library (visible only to registered users)
 * 2. Create game → With name, icon, image (URL or file upload)
 * 3. Chat setup → Wait for processing, create chat thread
 * 4. Q&A → Ask a question about the game rules
 */

import { useState, useCallback } from 'react';
import Link from 'next/link';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';
import { Card } from '@/components/ui/card';
import { Spinner } from '@/components/loading';
import { toast } from '@/components/layout';
import { api } from '@/lib/api';
import { PdfUploadStep } from './steps/PdfUploadStep';
import { GameCreationStep } from './steps/GameCreationStep';
import { ChatSetupStep } from './steps/ChatSetupStep';
import { QAStep } from './steps/QAStep';

type WizardStep = 'upload' | 'game' | 'chat' | 'qa';

interface WizardState {
  currentStep: WizardStep;
  pdfId: string | null;
  pdfFileName: string | null;
  isPublic: boolean;
  gameId: string | null;
  gameName: string | null;
  chatThreadId: string | null;
  processingComplete: boolean;
}

const STEPS: { id: WizardStep; label: string; description: string; icon: string }[] = [
  { id: 'upload', label: '1. Upload PDF', description: 'Carica regolamento', icon: '📄' },
  { id: 'game', label: '2. Crea Gioco', description: 'Nome e immagini', icon: '🎮' },
  { id: 'chat', label: '3. Setup Chat', description: 'Prepara agente RAG', icon: '💬' },
  { id: 'qa', label: '4. Q&A', description: 'Testa le regole', icon: '❓' },
];

export function AdminWizardClient() {
  const { user, loading: authLoading } = useAuthUser();

  const [state, setState] = useState<WizardState>({
    currentStep: 'upload',
    pdfId: null,
    pdfFileName: null,
    isPublic: true, // Default to public for admin wizard
    gameId: null,
    gameName: null,
    chatThreadId: null,
    processingComplete: false,
  });

  const [_loading, _setLoading] = useState(false);

  // Step navigation
  const _goToStep = useCallback((step: WizardStep) => {
    setState(prev => ({ ...prev, currentStep: step }));
  }, []);

  const goToNextStep = useCallback(() => {
    const currentIndex = STEPS.findIndex(s => s.id === state.currentStep);
    if (currentIndex < STEPS.length - 1) {
      setState(prev => ({ ...prev, currentStep: STEPS[currentIndex + 1].id }));
    }
  }, [state.currentStep]);

  const goToPrevStep = useCallback(() => {
    const currentIndex = STEPS.findIndex(s => s.id === state.currentStep);
    if (currentIndex > 0) {
      setState(prev => ({ ...prev, currentStep: STEPS[currentIndex - 1].id }));
    }
  }, [state.currentStep]);

  // Step 1: PDF Upload complete
  const handlePdfUploaded = useCallback(
    async (pdfId: string, fileName: string, isPublic: boolean) => {
      setState(prev => ({
        ...prev,
        pdfId,
        pdfFileName: fileName,
        isPublic,
      }));

      // Set visibility if needed
      if (isPublic) {
        try {
          await api.pdf.setVisibility(pdfId, true);
          toast.success('PDF aggiunto alla libreria pubblica');
        } catch (_err) {
          toast.error('Errore nel rendere pubblico il PDF');
        }
      }

      goToNextStep();
    },
    [goToNextStep]
  );

  // Step 2: Game created
  const handleGameCreated = useCallback(
    (gameId: string, gameName: string) => {
      setState(prev => ({
        ...prev,
        gameId,
        gameName,
      }));
      goToNextStep();
    },
    [goToNextStep]
  );

  // Step 3: Chat setup complete
  const handleChatReady = useCallback(
    (chatThreadId: string) => {
      setState(prev => ({
        ...prev,
        chatThreadId,
        processingComplete: true,
      }));
      goToNextStep();
    },
    [goToNextStep]
  );

  // Reset wizard
  const resetWizard = useCallback(() => {
    setState({
      currentStep: 'upload',
      pdfId: null,
      pdfFileName: null,
      isPublic: true,
      gameId: null,
      gameName: null,
      chatThreadId: null,
      processingComplete: false,
    });
  }, []);

  if (authLoading) {
    return (
      <div className="min-h-dvh flex items-center justify-center bg-slate-50 dark:bg-slate-900">
        <Spinner size="lg" />
        <p className="ml-4 text-slate-600 dark:text-slate-400">Caricamento...</p>
      </div>
    );
  }

  if (!user) return null;

  const currentStepIndex = STEPS.findIndex(s => s.id === state.currentStep);

  return (
    <AdminAuthGuard
      loading={authLoading}
      user={user}
      backgroundClass="min-h-dvh bg-slate-50 dark:bg-slate-900"
    >
      <div className="min-h-dvh bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-900 dark:to-slate-800">
        <div className="max-w-4xl mx-auto px-4 py-8">
          {/* Header */}
          <div className="flex items-center justify-between mb-8">
            <div>
              <h1 className="text-3xl font-bold text-slate-900 dark:text-white mb-2">
                Admin Game Setup Wizard
              </h1>
              <p className="text-slate-600 dark:text-slate-400">
                Configura un nuovo gioco con regolamento PDF e agente RAG
              </p>
            </div>
            <Link
              href="/admin"
              className="px-4 py-2 bg-slate-600 text-white rounded-lg hover:bg-slate-700 transition-colors"
            >
              ← Torna ad Admin
            </Link>
          </div>

          {/* Step Indicator */}
          <div className="mb-8">
            <div className="flex items-center justify-between">
              {STEPS.map((step, index) => {
                const isActive = step.id === state.currentStep;
                const isCompleted = index < currentStepIndex;
                const _isUpcoming = index > currentStepIndex;

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
                              : 'bg-slate-200 dark:bg-slate-700 text-slate-500 dark:text-slate-400'
                        }`}
                      >
                        {isCompleted ? '✓' : step.icon}
                      </div>
                      <span
                        className={`text-sm font-medium ${
                          isActive
                            ? 'text-blue-600 dark:text-blue-400'
                            : isCompleted
                              ? 'text-green-600 dark:text-green-400'
                              : 'text-slate-500 dark:text-slate-400'
                        }`}
                      >
                        {step.label}
                      </span>
                      <span className="text-xs text-slate-500 dark:text-slate-400">
                        {step.description}
                      </span>
                    </div>
                    {/* Connector line */}
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
          <Card className="p-6 bg-white dark:bg-slate-800 shadow-lg">
            {state.currentStep === 'upload' && <PdfUploadStep onComplete={handlePdfUploaded} />}

            {state.currentStep === 'game' && state.pdfId && (
              <GameCreationStep
                pdfId={state.pdfId}
                pdfFileName={state.pdfFileName ?? 'PDF'}
                onComplete={handleGameCreated}
                onBack={goToPrevStep}
              />
            )}

            {state.currentStep === 'chat' && state.gameId && (
              <ChatSetupStep
                gameId={state.gameId}
                gameName={state.gameName ?? 'Gioco'}
                pdfId={state.pdfId ?? ''}
                onComplete={handleChatReady}
                onBack={goToPrevStep}
              />
            )}

            {state.currentStep === 'qa' && state.gameId && state.chatThreadId && (
              <QAStep
                gameId={state.gameId}
                gameName={state.gameName ?? 'Gioco'}
                chatThreadId={state.chatThreadId}
                onReset={resetWizard}
              />
            )}
          </Card>

          {/* Status Summary */}
          {(state.pdfId || state.gameId) && (
            <Card className="mt-6 p-4 bg-slate-100 dark:bg-slate-700/50">
              <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-300 mb-2">
                Riepilogo
              </h3>
              <div className="grid grid-cols-2 gap-4 text-sm">
                {state.pdfFileName && (
                  <div>
                    <span className="text-slate-500">PDF:</span>{' '}
                    <span className="font-medium">{state.pdfFileName}</span>
                    {state.isPublic && (
                      <span className="ml-2 text-xs bg-green-100 text-green-700 px-2 py-0.5 rounded">
                        Pubblico
                      </span>
                    )}
                  </div>
                )}
                {state.gameName && (
                  <div>
                    <span className="text-slate-500">Gioco:</span>{' '}
                    <span className="font-medium">{state.gameName}</span>
                  </div>
                )}
              </div>
            </Card>
          )}
        </div>
      </div>
    </AdminAuthGuard>
  );
}
