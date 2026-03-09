'use client';

/**
 * PDF Upload Step
 * Step 3 of the Admin Game Wizard.
 * Reuses existing PdfUploadForm component for chunked upload with validation.
 */

import { useCallback, useState } from 'react';

import { FileTextIcon, CheckCircle2Icon } from 'lucide-react';

import { PdfUploadForm } from '@/components/pdf/PdfUploadForm';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import type { CategorizedError } from '@/lib/errorUtils';

// ─── Types ───────────────────────────────────────────────────────────────────

interface PdfUploadStepProps {
  gameId: string;
  gameTitle: string;
  onPdfUploaded: (pdfDocumentId: string) => void;
}

// ─── Component ───────────────────────────────────────────────────────────────

export function PdfUploadStep({ gameId, gameTitle, onPdfUploaded }: PdfUploadStepProps) {
  const [uploadedId, setUploadedId] = useState<string | null>(null);
  const [uploadError, setUploadError] = useState<CategorizedError | null>(null);

  const handleUploadSuccess = useCallback(
    (documentId: string) => {
      setUploadedId(documentId);
      setUploadError(null);
      onPdfUploaded(documentId);
    },
    [onPdfUploaded]
  );

  const handleUploadError = useCallback((error: CategorizedError) => {
    setUploadError(error);
  }, []);

  if (uploadedId) {
    return (
      <Card className="bg-green-50 dark:bg-green-950/20 border-green-200 dark:border-green-800/40">
        <CardContent className="flex items-center gap-3 p-4">
          <CheckCircle2Icon className="h-5 w-5 text-green-600 dark:text-green-400 shrink-0" />
          <div>
            <p className="font-medium text-green-700 dark:text-green-400">
              PDF uploaded successfully
            </p>
            <p className="text-sm text-green-600/80 dark:text-green-400/60">
              Document ID: {uploadedId}
            </p>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <div className="space-y-4">
      <Card className="bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl border-slate-200/60 dark:border-zinc-700/60">
        <CardHeader>
          <CardTitle className="flex items-center gap-2 text-base">
            <FileTextIcon className="h-5 w-5 text-amber-500" />
            Upload Rulebook PDF
          </CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground mb-4">
            Upload the rulebook PDF for <strong>{gameTitle}</strong>. The PDF will be processed with
            admin priority.
          </p>
          <PdfUploadForm
            gameId={gameId}
            gameName={gameTitle}
            onUploadSuccess={handleUploadSuccess}
            onUploadError={handleUploadError}
          />
        </CardContent>
      </Card>

      {uploadError && (
        <div className="rounded-lg border border-red-200 dark:border-red-900/40 bg-red-50 dark:bg-red-950/20 p-3">
          <p className="text-sm font-medium text-red-700 dark:text-red-400">Upload failed</p>
          <p className="text-sm text-red-600 dark:text-red-400/80">{uploadError.message}</p>
        </div>
      )}
    </div>
  );
}
