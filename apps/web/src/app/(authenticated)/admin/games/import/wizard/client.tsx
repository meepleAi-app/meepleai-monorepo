'use client';

/**
 * Admin Game Import Wizard - Client Component
 * Issue #4161: PDF Wizard Container & State Management
 *
 * 4-step wizard for admin game import from PDF:
 * 1. Upload PDF
 * 2. Review Extracted Metadata
 * 3. Select BGG Game
 * 4. Resolve Conflicts & Finalize
 */

import { useCallback } from 'react';

import Link from 'next/link';

import { useAuthUser } from '@/components/auth/AuthProvider';
import { Spinner } from '@/components/loading';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/data-display/card';
import { WizardSteps } from '@/components/wizard';
import { useGameImportWizardStore } from '@/stores/useGameImportWizardStore';

import { Step1UploadPdf } from './steps/Step1UploadPdf';

type WizardStep = 1 | 2 | 3 | 4;

interface StepConfig {
  id: WizardStep;
  label: string;
  description: string;
  icon: string;
}

const STEPS: StepConfig[] = [
  { id: 1, label: '1. Upload PDF', description: 'Carica regolamento', icon: '📄' },
  { id: 2, label: '2. Metadata', description: 'Rivedi dati estratti', icon: '📝' },
  { id: 3, label: '3. BGG Match', description: 'Seleziona da BGG', icon: '🎲' },
  { id: 4, label: '4. Finalize', description: 'Risolvi conflitti', icon: '✅' },
];

export function AdminGameImportWizardClient() {
  const { user, loading: authLoading } = useAuthUser();

  const {
    currentStep,
    uploadedPdf,
    extractedMetadata,
    selectedBggId,
    enrichedData,
    isProcessing,
    error,
    goNext,
    goBack,
    canGoNext,
    canGoBack,
    reset,
    submitWizard,
    setUploadedPdf,
    setStep,
  } = useGameImportWizardStore();

  // Handle submission
  const handleSubmit = useCallback(async () => {
    try {
      await submitWizard();
      // Toast and navigation handled by store
    } catch (err) {
      // Error already handled by store
      console.error('Wizard submission failed:', err);
    }
  }, [submitWizard]);

  // Breadcrumb navigation handler
  const handleStepClick = useCallback(
    (stepId: string) => {
      const step = parseInt(stepId) as WizardStep;
      setStep(step);
    },
    [setStep]
  );

  // Progress calculation (0% → 33% → 67% → 100%)
  const progress = ((currentStep - 1) / (STEPS.length - 1)) * 100;

  // Map STEPS to WizardSteps format
  const wizardSteps = STEPS.map(s => ({
    id: s.id.toString(),
    label: s.label,
    description: s.description,
  }));

  // Auth loading state
  if (authLoading) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4">
        <Spinner size="lg" />
        <p className="text-muted-foreground">Loading...</p>
      </div>
    );
  }

  // Not authenticated
  if (!user) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center gap-4">
        <h1 className="text-2xl font-bold">Authentication Required</h1>
        <p className="text-muted-foreground">Please sign in to access the admin wizard.</p>
        <Link href="/auth/signin">
          <Button>Sign In</Button>
        </Link>
      </div>
    );
  }

  const currentStepConfig = STEPS.find(s => s.id === currentStep);

  return (
    <div className="container mx-auto max-w-5xl py-8">
      {/* Header */}
      <div className="mb-8">
        <div className="mb-4 flex items-center justify-between">
          <h1 className="text-3xl font-bold">Game Import Wizard</h1>
          <Button variant="outline" size="sm" onClick={reset}>
            Reset
          </Button>
        </div>
        <p className="text-muted-foreground">
          Import a game from PDF by uploading, reviewing metadata, matching with BGG, and finalizing.
        </p>
      </div>

      {/* Progress Bar */}
      <div className="mb-4">
        <div className="mb-2 flex justify-between text-sm text-muted-foreground">
          <span>
            Step {currentStep} of {STEPS.length}
          </span>
          <span>{Math.round(progress)}%</span>
        </div>
        <div className="h-2 overflow-hidden rounded-full bg-muted">
          <div
            className="h-full bg-primary transition-all duration-300"
            style={{ width: `${progress}%` }}
            aria-valuenow={progress}
            aria-valuemin={0}
            aria-valuemax={100}
            role="progressbar"
          />
        </div>
      </div>

      {/* Wizard Steps with Breadcrumb Navigation */}
      <WizardSteps
        steps={wizardSteps}
        currentStep={currentStep.toString()}
        onStepClick={handleStepClick}
        allowSkip={true}
      />

      {/* Error Display */}
      {error && (
        <div className="mb-6 rounded-md border border-destructive bg-destructive/10 p-4 text-destructive">
          <p className="font-medium">Error</p>
          <p className="text-sm">{error}</p>
        </div>
      )}

      {/* Main Content Card */}
      <Card className="mb-6 p-6">
        <div className="mb-4">
          <h2 className="text-xl font-semibold">
            {currentStepConfig?.icon} {currentStepConfig?.label}
          </h2>
          <p className="text-sm text-muted-foreground">{currentStepConfig?.description}</p>
        </div>

        <div className="rounded-md border bg-muted/50 p-8">
          {/* Step 1: Upload PDF */}
          {currentStep === 1 && (
            <Step1UploadPdf
              onUploadComplete={pdf => {
                setUploadedPdf(pdf);
              }}
            />
          )}

          {/* Step 2: Review Extracted Metadata */}
          {currentStep === 2 && (
            <div className="space-y-4">
              <h3 className="text-lg font-medium">Review Extracted Metadata</h3>
              <p className="text-sm text-muted-foreground">
                Review the metadata automatically extracted from the PDF.
              </p>
              {extractedMetadata ? (
                <div className="rounded-md border bg-background p-4">
                  <p className="text-sm">
                    <strong>Title:</strong> {extractedMetadata.title || 'N/A'}
                  </p>
                  <p className="text-sm">
                    <strong>Players:</strong> {extractedMetadata.minPlayers || '?'}-
                    {extractedMetadata.maxPlayers || '?'}
                  </p>
                  <p className="text-sm">
                    <strong>Play Time:</strong> {extractedMetadata.playTime || 'N/A'} min
                  </p>
                </div>
              ) : (
                <div className="rounded-md border-2 border-dashed p-8 text-center">
                  <p className="text-sm text-muted-foreground">
                    Metadata review component will be implemented in a separate issue
                  </p>
                </div>
              )}
            </div>
          )}

          {/* Step 3: BGG Selection */}
          {currentStep === 3 && (
            <div className="space-y-4">
              <h3 className="text-lg font-medium">Select BoardGameGeek Game</h3>
              <p className="text-sm text-muted-foreground">
                Search and select the matching game from BoardGameGeek.
              </p>
              {selectedBggId ? (
                <div className="rounded-md border bg-background p-4">
                  <p className="text-sm">
                    <strong>Selected BGG ID:</strong> {selectedBggId}
                  </p>
                </div>
              ) : (
                <div className="rounded-md border-2 border-dashed p-8 text-center">
                  <p className="text-sm text-muted-foreground">
                    BGG selection component will be implemented in a separate issue
                  </p>
                </div>
              )}
            </div>
          )}

          {/* Step 4: Resolve Conflicts */}
          {currentStep === 4 && (
            <div className="space-y-4">
              <h3 className="text-lg font-medium">Resolve Conflicts & Finalize</h3>
              <p className="text-sm text-muted-foreground">
                Review and resolve any conflicts between extracted metadata and BGG data.
              </p>
              {enrichedData ? (
                <div className="rounded-md border bg-background p-4">
                  <p className="text-sm">
                    <strong>Final Title:</strong> {enrichedData.title}
                  </p>
                  <p className="text-sm">
                    <strong>Players:</strong> {enrichedData.minPlayers || '?'}-
                    {enrichedData.maxPlayers || '?'}
                  </p>
                  <p className="text-sm">
                    <strong>BGG ID:</strong> {enrichedData.bggId || 'N/A'}
                  </p>
                </div>
              ) : (
                <div className="rounded-md border-2 border-dashed p-8 text-center">
                  <p className="text-sm text-muted-foreground">
                    Conflict resolution component will be implemented in a separate issue
                  </p>
                </div>
              )}
            </div>
          )}
        </div>
      </Card>

      {/* Status Summary */}
      <Card className="mb-6 p-4">
        <h3 className="mb-2 text-sm font-medium">Wizard State</h3>
        <div className="space-y-1 text-xs text-muted-foreground">
          <p>
            <strong>Step:</strong> {currentStep}/4
          </p>
          <p>
            <strong>PDF:</strong> {uploadedPdf ? `✓ ${uploadedPdf.fileName}` : '✗ Not uploaded'}
          </p>
          <p>
            <strong>Metadata:</strong> {extractedMetadata ? '✓ Extracted' : '✗ Not extracted'}
          </p>
          <p>
            <strong>BGG:</strong> {selectedBggId ? `✓ ID ${selectedBggId}` : '✗ Not selected'}
          </p>
          <p>
            <strong>Enriched:</strong> {enrichedData ? '✓ Ready' : '✗ Not ready'}
          </p>
        </div>
      </Card>

      {/* Navigation Buttons */}
      <div className="flex items-center justify-between">
        <Button variant="outline" onClick={goBack} disabled={!canGoBack() || isProcessing}>
          ← Previous
        </Button>

        <div className="flex gap-2">
          {currentStep < 4 ? (
            <Button onClick={goNext} disabled={!canGoNext() || isProcessing}>
              {isProcessing ? (
                <>
                  <Spinner size="sm" className="mr-2" />
                  Processing...
                </>
              ) : (
                'Next →'
              )}
            </Button>
          ) : (
            <Button onClick={handleSubmit} disabled={!enrichedData || isProcessing}>
              {isProcessing ? (
                <>
                  <Spinner size="sm" className="mr-2" />
                  Submitting...
                </>
              ) : (
                '✓ Submit & Import'
              )}
            </Button>
          )}
        </div>
      </div>
    </div>
  );
}
