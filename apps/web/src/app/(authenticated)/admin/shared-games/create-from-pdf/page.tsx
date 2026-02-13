/**
 * Create Game from PDF - Issue #4141
 *
 * 4-step wizard for creating games in SharedGameCatalog from PDF upload with BGG integration.
 */

'use client';

import { useEffect } from 'react';

import { WizardProgress } from '@/components/wizard/WizardProgress';
import { usePdfWizardStore } from '@/lib/stores/pdf-wizard-store';

import { Step1PdfUpload } from './steps/Step1PdfUpload';
import { Step2PreviewExtracted } from './steps/Step2PreviewExtracted';
import { Step3BggMatch } from './steps/Step3BggMatch';
import { Step4Confirm } from './steps/Step4Confirm';

/**
 * Create Game from PDF Page
 *
 * Multi-step wizard:
 * 1. Upload PDF (drag & drop, chunked upload, extraction)
 * 2. Preview Data (manual fields, duplicate warnings)
 * 3. BGG Match (search or manual ID, optional)
 * 4. Confirm (summary, submit with approval workflow)
 */
export default function CreateFromPdfPage() {
  const currentStep = usePdfWizardStore((state) => state.currentStep);
  const setCurrentStep = usePdfWizardStore((state) => state.setCurrentStep);
  const reset = usePdfWizardStore((state) => state.reset);

  // Reset wizard on mount
  useEffect(() => {
    reset();
  }, [reset]);

  const handleNextStep = () => {
    setCurrentStep(currentStep + 1);
  };

  const handleBackStep = () => {
    setCurrentStep(currentStep - 1);
  };

  const handleSkipBgg = () => {
    setCurrentStep(4); // Skip to confirm step
  };

  return (
    <div className="container mx-auto px-4 py-8 max-w-4xl">
      {/* Page Header */}
      <div className="mb-8">
        <h1 className="font-quicksand text-4xl font-bold text-gray-900 mb-2">
          Create Game from PDF
        </h1>
        <p className="font-nunito text-base text-gray-600">
          Upload a PDF rulebook to automatically create a game entry with BGG integration.
        </p>
      </div>

      {/* Wizard Progress Indicator */}
      <WizardProgress currentStep={currentStep} />

      {/* Step Content */}
      <div className="mt-8">
        {currentStep === 1 && <Step1PdfUpload onNext={handleNextStep} />}

        {currentStep === 2 && (
          <Step2PreviewExtracted
            onNext={handleNextStep}
            onSkipBgg={handleSkipBgg}
            onBack={handleBackStep}
          />
        )}

        {currentStep === 3 && (
          <Step3BggMatch onNext={handleNextStep} onBack={handleBackStep} />
        )}

        {currentStep === 4 && <Step4Confirm onBack={handleBackStep} userRole="Admin" />}
      </div>
    </div>
  );
}
