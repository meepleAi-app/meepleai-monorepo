/**
 * RAG Wizard — Main Shell
 *
 * 4-step wizard for uploading and processing PDFs for RAG indexing:
 * 1. Upload — select PDF files
 * 2. Configura — per-file document type & version
 * 3. Processing — upload + SSE progress
 * 4. Completo — success summary
 */

'use client';

import { useState, useCallback } from 'react';

import { CheckCircle2 } from 'lucide-react';

import { cn } from '@/lib/utils';

import { StepComplete } from './step-complete';
import { StepConfigure } from './step-configure';
import { StepProgress } from './step-progress';
import { StepUpload } from './step-upload';

import type { AddRagResult, DocumentType } from '../lib/rag-api';

// ── Types ───────────────────────────────────────────────────────────────

export interface FileConfig {
  file: File;
  documentType: DocumentType;
  version: string;
}

export interface FileResult {
  fileName: string;
  result: AddRagResult | null;
  error: string | null;
}

interface RagWizardProps {
  sharedGameId: string;
  onClose: () => void;
}

// ── Step Indicator ──────────────────────────────────────────────────────

const STEPS = [
  { label: 'Upload', step: 0 },
  { label: 'Configura', step: 1 },
  { label: 'Processing', step: 2 },
  { label: 'Completo', step: 3 },
] as const;

function StepIndicator({ currentStep }: { currentStep: number }) {
  return (
    <div className="flex items-center justify-between mb-8">
      {STEPS.map(({ label, step }, i) => (
        <div key={step} className="flex items-center flex-1 last:flex-none">
          <div className="flex items-center gap-2">
            <div
              className={cn(
                'flex h-8 w-8 items-center justify-center rounded-full text-sm font-medium transition-colors',
                currentStep > step
                  ? 'bg-green-600 text-white'
                  : currentStep === step
                    ? 'bg-primary text-primary-foreground'
                    : 'bg-muted text-muted-foreground'
              )}
            >
              {currentStep > step ? <CheckCircle2 className="h-4 w-4" /> : step + 1}
            </div>
            <span
              className={cn(
                'text-sm font-medium hidden sm:inline',
                currentStep >= step ? 'text-foreground' : 'text-muted-foreground'
              )}
            >
              {label}
            </span>
          </div>
          {i < STEPS.length - 1 && (
            <div
              className={cn('h-px flex-1 mx-3', currentStep > step ? 'bg-green-600' : 'bg-muted')}
            />
          )}
        </div>
      ))}
    </div>
  );
}

// ── Main Wizard ─────────────────────────────────────────────────────────

export function RagWizard({ sharedGameId, onClose }: RagWizardProps) {
  const [currentStep, setCurrentStep] = useState(0);
  const [files, setFiles] = useState<File[]>([]);
  const [configs, setConfigs] = useState<FileConfig[]>([]);
  const [results, setResults] = useState<FileResult[]>([]);

  const handleFilesSelected = useCallback((selectedFiles: File[]) => {
    setFiles(selectedFiles);
  }, []);

  const handleGoToConfigure = useCallback(() => {
    // Initialize configs from files
    setConfigs(
      files.map(file => ({
        file,
        documentType: 'Rulebook' as DocumentType,
        version: '1.0',
      }))
    );
    setCurrentStep(1);
  }, [files]);

  const handleStartProcessing = useCallback((updatedConfigs: FileConfig[]) => {
    setConfigs(updatedConfigs);
    setCurrentStep(2);
  }, []);

  const handleProcessingComplete = useCallback((fileResults: FileResult[]) => {
    setResults(fileResults);
    setCurrentStep(3);
  }, []);

  return (
    <div className="space-y-4">
      <StepIndicator currentStep={currentStep} />

      {currentStep === 0 && (
        <StepUpload
          files={files}
          onFilesChange={handleFilesSelected}
          onNext={handleGoToConfigure}
        />
      )}

      {currentStep === 1 && (
        <StepConfigure
          configs={configs}
          onBack={() => setCurrentStep(0)}
          onStartProcessing={handleStartProcessing}
        />
      )}

      {currentStep === 2 && (
        <StepProgress
          sharedGameId={sharedGameId}
          configs={configs}
          onComplete={handleProcessingComplete}
        />
      )}

      {currentStep === 3 && <StepComplete results={results} onClose={onClose} />}
    </div>
  );
}
