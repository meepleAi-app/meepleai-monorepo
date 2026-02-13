/**
 * Step1PdfUpload - Issue #4141
 *
 * PDF upload step with drag & drop, chunked upload, and extraction polling.
 */

'use client';

import { useState, useCallback } from 'react';

import { Upload, FileText, AlertCircle } from 'lucide-react';
import { useDropzone } from 'react-dropzone';

import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Progress } from '@/components/ui/feedback/progress';
import { usePdfProgress } from '@/hooks/usePdfProgress';
import { usePdfWizardStore } from '@/lib/stores/pdf-wizard-store';
import { cn } from '@/lib/utils';
import { pollExtractionStatus } from '@/lib/utils/extraction';
import { uploadChunks } from '@/lib/utils/upload';

interface Step1PdfUploadProps {
  onNext: () => void;
}

type UploadState = 'idle' | 'uploading' | 'extracting' | 'completed' | 'error';

/**
 * Step 1: PDF Upload Component
 *
 * Features:
 * - Drag & drop zone (react-dropzone)
 * - File validation (PDF only, max 100MB)
 * - Chunked upload for files >50MB
 * - Progress tracking with usePdfProgress hook
 * - Extraction status polling
 * - Quality badge (green ≥0.80, yellow ≥0.50, red <0.50)
 */
export function Step1PdfUpload({ onNext }: Step1PdfUploadProps) {
  const [uploadState, setUploadState] = useState<UploadState>('idle');
  const [uploadProgress, setUploadProgress] = useState(0);
  const [errorMessage, setErrorMessage] = useState<string | null>(null);
  const [currentPdfId, setCurrentPdfId] = useState<string | null>(null);

  const setStep1Data = usePdfWizardStore((state) => state.setStep1Data);

  // Use PDF progress hook for extraction tracking (only when extracting)
  const { status: pdfStatus } = usePdfProgress(
    uploadState === 'extracting' ? currentPdfId : null,
    {
      enableMetrics: false,
    }
  );

  const onDrop = useCallback(
    async (acceptedFiles: File[]) => {
      const file = acceptedFiles[0];
      if (!file) return;

      // Validate file
      if (file.type !== 'application/pdf') {
        setErrorMessage('Please select a PDF file');
        return;
      }

      if (file.size > 100 * 1024 * 1024) {
        setErrorMessage('File size must be less than 100MB');
        return;
      }

      setErrorMessage(null);
      setUploadState('uploading');
      setUploadProgress(0);

      try {
        // Generate session ID for chunked upload
        const sessionId = `upload-${Date.now()}-${Math.random().toString(36).substring(7)}`;

        // Upload file in chunks
        const pdfId = await uploadChunks(file, sessionId, (progress) => {
          setUploadProgress(progress);
        });

        setCurrentPdfId(pdfId);
        setUploadState('extracting');

        // Poll extraction status
        const result = await pollExtractionStatus(pdfId);

        // Save to wizard store
        setStep1Data({
          pdfDocumentId: result.pdfDocumentId,
          qualityScore: result.qualityScore,
          extractedTitle: result.extractedTitle,
        });

        setUploadState('completed');
      } catch (error) {
        console.error('Upload/extraction error:', error);
        setErrorMessage(
          error instanceof Error ? error.message : 'Upload failed. Please try again.'
        );
        setUploadState('error');
      }
    },
    [setStep1Data]
  );

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    accept: {
      'application/pdf': ['.pdf'],
    },
    maxFiles: 1,
    disabled: uploadState === 'uploading' || uploadState === 'extracting',
  });

  const getQualityBadgeColor = (score: number) => {
    if (score >= 0.8) return 'bg-green-100 text-green-900 border-green-300';
    if (score >= 0.5) return 'bg-yellow-100 text-yellow-900 border-yellow-300';
    return 'bg-red-100 text-red-900 border-red-300';
  };

  const wizardData = usePdfWizardStore((state) => ({
    qualityScore: state.qualityScore,
    extractedTitle: state.extractedTitle,
  }));

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-2xl font-bold text-gray-900 mb-2">
          Upload PDF Rulebook
        </h2>
        <p className="font-nunito text-sm text-gray-600">
          Upload a PDF file to automatically extract game information.
        </p>
      </div>

      {/* Dropzone */}
      {uploadState === 'idle' || uploadState === 'error' ? (
        <div
          {...getRootProps()}
          className={cn(
            'border-2 border-dashed rounded-lg p-12 text-center cursor-pointer transition-all',
            'bg-white/70 backdrop-blur-md',
            isDragActive
              ? 'border-amber-500 bg-amber-50/70'
              : 'border-gray-300 hover:border-amber-400 hover:bg-white/90',
            'focus:outline-none focus:ring-2 focus:ring-amber-500 focus:ring-offset-2'
          )}
          role="button"
          aria-label="Upload PDF file"
        >
          <input {...getInputProps()} />
          <Upload
            className={cn(
              'mx-auto mb-4',
              isDragActive ? 'w-16 h-16 text-amber-500' : 'w-12 h-12 text-gray-400'
            )}
            aria-hidden="true"
          />
          {isDragActive ? (
            <p className="font-nunito text-lg text-amber-900 font-semibold">
              Drop PDF here...
            </p>
          ) : (
            <>
              <p className="font-nunito text-base text-gray-700 mb-2">
                Drag & drop a PDF file here, or click to browse
              </p>
              <p className="font-nunito text-sm text-gray-500">
                Max size: 100MB • PDF format only
              </p>
            </>
          )}
        </div>
      ) : null}

      {/* Upload Progress */}
      {uploadState === 'uploading' && (
        <div className="bg-white/70 backdrop-blur-md rounded-lg p-6 border-2 border-gray-200">
          <div className="flex items-center gap-3 mb-4">
            <FileText className="w-6 h-6 text-amber-500" aria-hidden="true" />
            <p className="font-nunito text-sm font-semibold text-gray-900">
              Uploading PDF...
            </p>
          </div>
          <Progress value={uploadProgress} className="mb-2" />
          <p className="font-nunito text-xs text-gray-600 text-center">
            {uploadProgress}% completed
          </p>
        </div>
      )}

      {/* Extraction Progress */}
      {uploadState === 'extracting' && (
        <div className="bg-white/70 backdrop-blur-md rounded-lg p-6 border-2 border-gray-200">
          <div className="flex items-center gap-3 mb-4">
            <FileText className="w-6 h-6 text-blue-500" aria-hidden="true" />
            <p className="font-nunito text-sm font-semibold text-gray-900">
              Extracting game information...
            </p>
          </div>
          <Progress
            value={pdfStatus?.progress ?? 0}
            className="mb-2"
          />
          <p className="font-nunito text-xs text-gray-600 text-center">
            {pdfStatus?.state ?? 'Processing'} • {pdfStatus?.progress ?? 0}%
          </p>
        </div>
      )}

      {/* Completion Summary */}
      {uploadState === 'completed' && wizardData.extractedTitle && (
        <div className="bg-white/70 backdrop-blur-md rounded-lg p-6 border-2 border-green-300">
          <div className="space-y-4">
            <div className="flex items-start justify-between">
              <div className="flex-1">
                <p className="font-nunito text-sm text-gray-600 mb-1">Extracted Title</p>
                <h3 className="font-quicksand text-xl font-bold text-gray-900">
                  {wizardData.extractedTitle}
                </h3>
              </div>

              {/* Quality Badge */}
              <div
                className={cn(
                  'px-3 py-1 rounded-full text-sm font-semibold border-2',
                  getQualityBadgeColor(wizardData.qualityScore)
                )}
              >
                Quality: {Math.round(wizardData.qualityScore * 100)}%
              </div>
            </div>

            <Button onClick={onNext} className="w-full" size="lg">
              Continue to Preview
            </Button>
          </div>
        </div>
      )}

      {/* Error Alert */}
      {errorMessage && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{errorMessage}</AlertDescription>
        </Alert>
      )}
    </div>
  );
}
