'use client';

/**
 * Admin Game Import Wizard - Client Component
 *
 * 5-step PDF-direct import flow:
 * 1. Upload PDF (chunked, ≤150 MB)
 * 2. Review LLM-extracted metadata + cover image + live MeepleCard preview
 * 3. Preview & Confirm (read-only)
 * 4. Saga progress (ImportGameFromPdfCommand)
 * 5. RAG test panel (wait for indexing, then interactive Q&A)
 */

import { useCallback } from 'react';

import Link from 'next/link';

import { useAuthUser } from '@/components/auth/AuthProvider';
import { ErrorBoundary } from '@/components/errors/ErrorBoundary';
import { Spinner } from '@/components/loading';
import { Card } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { WizardSteps } from '@/components/wizard';
import { useGameImportWizardStore } from '@/stores/useGameImportWizardStore';

import { Step1UploadPdf } from './steps/Step1UploadPdf';
import { Step2MetadataReview } from './steps/Step2MetadataReview';
import { Step3PreviewConfirm } from './steps/Step3PreviewConfirm';
import { Step4CreationProgress } from './steps/Step4CreationProgress';
import { Step5RagTest } from './steps/Step5RagTest';

type WizardStep = 1 | 2 | 3 | 4 | 5;

interface StepConfig {
  id: WizardStep;
  label: string;
  description: string;
}

const STEPS: StepConfig[] = [
  { id: 1, label: '1. Upload PDF', description: 'Carica regolamento' },
  { id: 2, label: '2. Metadati', description: 'Rivedi e correggi' },
  { id: 3, label: '3. Anteprima', description: 'Conferma scheda' },
  { id: 4, label: '4. Creazione', description: 'Saga in corso' },
  { id: 5, label: '5. RAG Test', description: 'Testa il knowledge base' },
];

export function AdminGameImportWizardClient() {
  const { user, loading: authLoading } = useAuthUser();

  const {
    currentStep,
    uploadedPdf,
    reviewedMetadata,
    importResult,
    isProcessing,
    error,
    goNext,
    goBack,
    canGoNext,
    canGoBack,
    reset,
    setUploadedPdf,
    setStep,
  } = useGameImportWizardStore();

  const handleStepClick = useCallback(
    (stepId: string) => {
      const step = parseInt(stepId) as WizardStep;
      // Only allow navigating to already-reached steps
      if (step < currentStep) {
        setStep(step);
      }
    },
    [currentStep, setStep]
  );

  const progress = ((currentStep - 1) / (STEPS.length - 1)) * 100;

  const wizardSteps = STEPS.map(s => ({
    id: s.id.toString(),
    label: s.label,
    description: s.description,
  }));

  if (authLoading) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4">
        <Spinner size="lg" />
        <p className="text-muted-foreground">Caricamento...</p>
      </div>
    );
  }

  if (!user) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4">
        <h1 className="text-2xl font-bold">Autenticazione richiesta</h1>
        <p className="text-muted-foreground">Accedi per usare il wizard di importazione.</p>
        <Link href="/login">
          <Button>Accedi</Button>
        </Link>
      </div>
    );
  }

  const currentStepConfig = STEPS.find(s => s.id === currentStep);

  // Step 4 manages its own progress and auto-advances — hide nav buttons
  const isStep4 = currentStep === 4;
  // Step 5 is terminal — show only reset
  const isStep5 = currentStep === 5;

  return (
    <ErrorBoundary
      componentName="GameImportWizard"
      fallback={(_err, resetBoundary) => (
        <div className="container mx-auto max-w-5xl py-8">
          <Card className="p-8 text-center">
            <h2 className="mb-2 text-2xl font-bold text-destructive">Errore nel wizard</h2>
            <p className="mb-6 text-muted-foreground">
              Si è verificato un errore. Prova a ricominciare o contatta il supporto.
            </p>
            <div className="flex flex-col gap-3 sm:flex-row sm:justify-center">
              <Button onClick={resetBoundary} variant="default">
                Ricomincia
              </Button>
              <Link href="/admin/shared-games">
                <Button variant="outline">Torna ai giochi</Button>
              </Link>
            </div>
          </Card>
        </div>
      )}
    >
      <div className="container mx-auto max-w-5xl py-8">
        {/* Header */}
        <div className="mb-8">
          <div className="mb-4 flex items-center justify-between">
            <h1 className="text-3xl font-bold">Importa gioco da PDF</h1>
            <Button variant="outline" size="sm" onClick={reset}>
              Ricomincia
            </Button>
          </div>
          <p className="text-muted-foreground">
            Carica un regolamento PDF, revisiona i metadati estratti dall&apos;IA e crea la scheda
            gioco con knowledge base RAG integrato.
          </p>
          <p className="mt-2 text-sm text-muted-foreground">
            Oppure{' '}
            <Link
              href="/admin/shared-games/new"
              className="font-medium text-primary underline underline-offset-2 hover:text-primary/80"
            >
              crea il gioco manualmente
            </Link>{' '}
            senza PDF.
          </p>
        </div>

        {/* Progress Bar */}
        <div className="mb-4">
          <div className="mb-2 flex justify-between text-sm text-muted-foreground">
            <span>
              Step {currentStep} di {STEPS.length}
            </span>
            <span>{Math.round(progress)}%</span>
          </div>
          <div className="h-2 overflow-hidden rounded-full bg-muted">
            <div
              className="h-full bg-primary transition-all duration-300"
              style={{ width: `${progress}%` }}
              role="progressbar"
              aria-valuenow={progress}
              aria-valuemin={0}
              aria-valuemax={100}
            />
          </div>
        </div>

        {/* Step breadcrumbs */}
        <WizardSteps
          steps={wizardSteps}
          currentStep={currentStep.toString()}
          onStepClick={handleStepClick}
          allowSkip={false}
        />

        {/* Global error */}
        {error && !isStep4 && (
          <div className="mb-6 rounded-md border border-destructive bg-destructive/10 p-4 text-destructive">
            <p className="font-medium">Errore</p>
            <p className="text-sm">{error}</p>
          </div>
        )}

        {/* Main Content */}
        <Card className="mb-6 p-6">
          <div className="mb-6">
            <h2 className="text-xl font-semibold">{currentStepConfig?.label}</h2>
            <p className="text-sm text-muted-foreground">{currentStepConfig?.description}</p>
          </div>

          {currentStep === 1 && (
            <Step1UploadPdf
              onUploadComplete={pdf => {
                setUploadedPdf(pdf);
              }}
            />
          )}

          {currentStep === 2 && <Step2MetadataReview />}

          {currentStep === 3 && <Step3PreviewConfirm />}

          {currentStep === 4 && <Step4CreationProgress />}

          {currentStep === 5 && <Step5RagTest />}
        </Card>

        {/* Navigation — hidden during saga (step 4) and final step */}
        {!isStep4 && !isStep5 && (
          <div className="flex items-center justify-between">
            <Button variant="outline" onClick={goBack} disabled={!canGoBack() || isProcessing}>
              ← Indietro
            </Button>

            <Button onClick={goNext} disabled={!canGoNext() || isProcessing}>
              {isProcessing ? (
                <>
                  <Spinner size="sm" className="mr-2" />
                  In corso...
                </>
              ) : currentStep === 3 ? (
                'Crea gioco →'
              ) : (
                'Avanti →'
              )}
            </Button>
          </div>
        )}

        {/* Step 5: only show a "reset" if user wants to import another game */}
        {isStep5 && (
          <div className="flex justify-center">
            <Button variant="outline" onClick={reset}>
              Importa un altro gioco
            </Button>
          </div>
        )}
      </div>
    </ErrorBoundary>
  );
}
