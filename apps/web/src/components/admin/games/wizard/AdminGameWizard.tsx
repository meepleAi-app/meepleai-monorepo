'use client';

/**
 * Admin Game Wizard - Multi-step shell
 * Wizard flow: BGG Search → Game Details → PDF Upload → Launch Processing
 */

import { useState, useCallback } from 'react';
import { useRouter } from 'next/navigation';

import { ArrowLeftIcon } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';

import type { BggSearchResult } from '@/lib/api/schemas/games.schemas';
import type { CreateGameFromWizardResult } from '@/hooks/queries/useAdminGameWizard';

import { BggSearchStep } from './steps/BggSearchStep';
import { GameDetailsStep } from './steps/GameDetailsStep';
import { PdfUploadStep } from './steps/PdfUploadStep';
import { LaunchProcessingStep } from './steps/LaunchProcessingStep';

// ─── Types ───────────────────────────────────────────────────────────────────

type WizardStep = 'bgg-search' | 'game-details' | 'pdf-upload' | 'launch-processing';

const STEP_CONFIG: { id: WizardStep; label: string; number: number }[] = [
  { id: 'bgg-search', label: 'Search BGG', number: 1 },
  { id: 'game-details', label: 'Game Details', number: 2 },
  { id: 'pdf-upload', label: 'Upload PDF', number: 3 },
  { id: 'launch-processing', label: 'Launch', number: 4 },
];

// ─── Component ───────────────────────────────────────────────────────────────

export function AdminGameWizard() {
  const router = useRouter();
  const [currentStep, setCurrentStep] = useState<WizardStep>('bgg-search');
  const [selectedGame, setSelectedGame] = useState<BggSearchResult | null>(null);
  const [createdGame, setCreatedGame] = useState<CreateGameFromWizardResult | null>(null);
  const [pdfDocumentId, setPdfDocumentId] = useState<string | null>(null);

  const currentStepIndex = STEP_CONFIG.findIndex((s) => s.id === currentStep);

  const handleGameSelected = useCallback((game: BggSearchResult) => {
    setSelectedGame(game);
    setCurrentStep('game-details');
  }, []);

  const handleBack = useCallback(() => {
    if (currentStep === 'game-details') {
      setCurrentStep('bgg-search');
    } else if (currentStep === 'pdf-upload') {
      setCurrentStep('game-details');
    } else if (currentStep === 'launch-processing') {
      setCurrentStep('pdf-upload');
    }
  }, [currentStep]);

  const handleGameCreated = useCallback((result: CreateGameFromWizardResult) => {
    setCreatedGame(result);
    setCurrentStep('pdf-upload');
  }, []);

  const handlePdfUploaded = useCallback((documentId: string) => {
    setPdfDocumentId(documentId);
    setCurrentStep('launch-processing');
  }, []);

  const handleProcessingLaunched = useCallback(
    (gameId: string) => {
      const title = createdGame?.title ? `?title=${encodeURIComponent(createdGame.title)}` : '';
      router.push(`/admin/games/${gameId}/processing${title}`);
    },
    [router, createdGame]
  );

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        <Link href="/admin/shared-games/all">
          <Button variant="ghost" size="icon" className="h-8 w-8">
            <ArrowLeftIcon className="h-4 w-4" />
          </Button>
        </Link>
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Add Game
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Import a game from BoardGameGeek, upload its rulebook, and launch processing
          </p>
        </div>
      </div>

      {/* Stepper */}
      <nav aria-label="Wizard progress">
        <ol className="flex items-center gap-2">
          {STEP_CONFIG.map((step, index) => {
            const isActive = step.id === currentStep;
            const isCompleted = index < currentStepIndex;

            return (
              <li key={step.id} className="flex items-center gap-2">
                {index > 0 && (
                  <div
                    className={`h-px w-8 ${
                      isCompleted
                        ? 'bg-amber-500'
                        : 'bg-slate-200 dark:bg-zinc-700'
                    }`}
                  />
                )}
                <div className="flex items-center gap-2">
                  <div
                    className={`flex h-7 w-7 items-center justify-center rounded-full text-xs font-semibold transition-colors ${
                      isActive
                        ? 'bg-amber-500 text-white'
                        : isCompleted
                          ? 'bg-amber-500/20 text-amber-600 dark:text-amber-400'
                          : 'bg-slate-100 dark:bg-zinc-800 text-muted-foreground'
                    }`}
                  >
                    {isCompleted ? (
                      <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
                        <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                      </svg>
                    ) : (
                      step.number
                    )}
                  </div>
                  <span
                    className={`text-sm font-medium ${
                      isActive
                        ? 'text-foreground'
                        : 'text-muted-foreground'
                    }`}
                  >
                    {step.label}
                  </span>
                </div>
              </li>
            );
          })}
        </ol>
      </nav>

      {/* Step Content */}
      <div className="min-h-[400px]">
        {currentStep === 'bgg-search' && (
          <BggSearchStep onGameSelected={handleGameSelected} />
        )}
        {currentStep === 'game-details' && selectedGame && (
          <GameDetailsStep
            selectedGame={selectedGame}
            onBack={handleBack}
            onGameCreated={handleGameCreated}
          />
        )}
        {currentStep === 'pdf-upload' && createdGame && (
          <PdfUploadStep
            gameId={createdGame.sharedGameId}
            gameTitle={createdGame.title}
            onPdfUploaded={handlePdfUploaded}
          />
        )}
        {currentStep === 'launch-processing' && createdGame && pdfDocumentId && (
          <LaunchProcessingStep
            gameId={createdGame.sharedGameId}
            gameTitle={createdGame.title}
            pdfDocumentId={pdfDocumentId}
            onBack={handleBack}
            onProcessingLaunched={handleProcessingLaunched}
          />
        )}
      </div>
    </div>
  );
}
