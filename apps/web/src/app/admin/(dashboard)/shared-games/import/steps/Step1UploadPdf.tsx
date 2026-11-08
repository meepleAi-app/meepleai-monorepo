'use client';

/**
 * Step 1: Upload PDF
 *
 * Features:
 * - Native file input (project standard, no react-dropzone)
 * - Client-side validation: PDF format (magic bytes), size within the backend's limit (#4449)
 * - The game comes first (#4401): the title creates a draft game, and the PDF is uploaded for it
 *   (wizardChunkedUpload, 10 MB chunks). A retry reuses the draft already created.
 * - Real upload progress tracking
 * - Error handling with clear messages
 */

import { type ChangeEvent, useState, useCallback } from 'react';
import type { JSX } from 'react';

import { AlertCircle, Upload, FileText, CheckCircle } from 'lucide-react';

import { Card } from '@/components/ui/data-display/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Progress } from '@/components/ui/feedback/progress';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { usePdfUploadLimits } from '@/hooks/queries/usePdfUploadLimits';
import { api } from '@/lib/api';
import { formatSizeLimit } from '@/lib/format/file-size';
import { type UploadedPdf, useGameImportWizardStore } from '@/stores/useGameImportWizardStore';

export interface Step1UploadPdfProps {
  onUploadComplete?: (pdf: UploadedPdf) => void;
}

const ALLOWED_MIME_TYPES = ['application/pdf'];
const PDF_MAGIC_BYTES = '%PDF-';

interface ValidationResult {
  isValid: boolean;
  errors: Record<string, string>;
}

// Client-side PDF validation helper. #4449: the size limit is the backend's (it was 150 MB here,
// above the 100 the backend accepts); while it is not known the backend's 400 is the check.
async function validatePdfFile(
  file: File,
  maxSizeBytes: number | undefined
): Promise<ValidationResult> {
  const errors: Record<string, string> = {};

  // MIME type check
  if (!ALLOWED_MIME_TYPES.includes(file.type)) {
    errors.mimeType = `Invalid file type. Expected PDF, got ${file.type || 'unknown'}`;
  }

  // Size check
  if (maxSizeBytes !== undefined && file.size > maxSizeBytes) {
    const sizeMB = (file.size / 1024 / 1024).toFixed(1);
    errors.size = `File size (${sizeMB} MB) exceeds maximum allowed (${formatSizeLimit(maxSizeBytes)})`;
  }

  if (file.size === 0) {
    errors.size = 'File is empty (0 bytes)';
  }

  // Magic bytes check (security: verify actual PDF format)
  try {
    const bytes = await file.slice(0, 5).arrayBuffer();
    const magicString = new TextDecoder('utf-8').decode(bytes);
    if (!magicString.startsWith(PDF_MAGIC_BYTES)) {
      errors.format = 'File does not appear to be a valid PDF (invalid header)';
    }
  } catch {
    errors.format = 'Unable to validate PDF format';
  }

  return {
    isValid: Object.keys(errors).length === 0,
    errors,
  };
}

function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 Bytes';
  const k = 1024;
  const sizes = ['Bytes', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));

  return `${Math.round((bytes / Math.pow(k, i)) * 100) / 100} ${sizes[i]}`;
}

export function Step1UploadPdf({ onUploadComplete }: Step1UploadPdfProps): JSX.Element {
  const [file, setFile] = useState<File | null>(null);
  const [validating, setValidating] = useState(false);
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});
  const [isUploading, setIsUploading] = useState(false);
  const [progress, setProgress] = useState<number>(0);
  const [uploadSuccess, setUploadSuccess] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);

  const importTitle = useGameImportWizardStore(s => s.importTitle);
  const importGameId = useGameImportWizardStore(s => s.importGameId);
  const setImportTitle = useGameImportWizardStore(s => s.setImportTitle);
  const setImportGameId = useGameImportWizardStore(s => s.setImportGameId);
  const { data: uploadLimits } = usePdfUploadLimits();
  const maxSizeBytes = uploadLimits?.maxFileSizeBytes;

  // Handle file selection
  const handleFileChange = useCallback(
    async (e: ChangeEvent<HTMLInputElement>) => {
      const selectedFile = e.target.files?.[0];
      if (!selectedFile) {
        setFile(null);
        setValidationErrors({});
        setUploadSuccess(false);
        setUploadError(null);
        return;
      }

      setValidating(true);
      setValidationErrors({});
      setUploadSuccess(false);
      setUploadError(null);

      const validation = await validatePdfFile(selectedFile, maxSizeBytes);

      if (!validation.isValid) {
        setValidationErrors(validation.errors);
        setFile(null);
        setValidating(false);
        return;
      }

      setFile(selectedFile);
      setValidating(false);
    },
    [maxSizeBytes]
  );

  // Handle upload — always use chunked upload
  const handleUpload = useCallback(async () => {
    const title = importTitle.trim();
    if (!file || !title) return;

    setIsUploading(true);
    setProgress(0);
    setUploadError(null);

    try {
      let gameId = importGameId;
      if (!gameId) {
        gameId = (await api.sharedGames.startGameImport(title)).gameId;
        setImportGameId(gameId);
      }
      const result = await api.sharedGames.wizardChunkedUpload(file, gameId, pct =>
        setProgress(pct)
      );
      setUploadSuccess(true);
      onUploadComplete?.({
        pdfDocumentId: result.pdfDocumentId,
        id: result.pdfDocumentId, // deprecated alias
        fileName: result.fileName,
      });
    } catch (err) {
      setUploadError(err instanceof Error ? err.message : 'Upload fallito');
    } finally {
      setIsUploading(false);
    }
  }, [file, importTitle, importGameId, setImportGameId, onUploadComplete]);

  const hasValidationErrors = Object.keys(validationErrors).length > 0;
  const canUpload =
    file && importTitle.trim() && !hasValidationErrors && !isUploading && !uploadSuccess;

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold">Carica regolamento PDF</h3>
        <p className="text-sm text-muted-foreground">
          Scrivi il titolo del gioco e seleziona il PDF del regolamento: il gioco viene creato come
          bozza e il PDF caricato per quel gioco. I metadati estratti dal PDF li rivedi allo step 2.
          {maxSizeBytes !== undefined && ` Dimensione massima: ${formatSizeLimit(maxSizeBytes)}.`}
        </p>
      </div>

      {/* File Input */}
      <Card className="p-6">
        <div className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="game-title">Titolo del gioco</Label>
            <Input
              id="game-title"
              value={importTitle}
              onChange={e => setImportTitle(e.target.value)}
              maxLength={300}
              disabled={isUploading || uploadSuccess || importGameId !== null}
              placeholder="Es. Catan"
            />
            {importGameId !== null && !uploadSuccess && (
              <p className="text-xs text-muted-foreground">
                La bozza del gioco è già stata creata: il nuovo tentativo carica il PDF per lei. Il
                titolo si corregge allo step 2.
              </p>
            )}
          </div>

          <div className="space-y-2">
            <Label htmlFor="pdf-file">Select PDF File</Label>
            <Input
              id="pdf-file"
              type="file"
              accept=".pdf,application/pdf"
              onChange={handleFileChange}
              disabled={isUploading || uploadSuccess}
              className="cursor-pointer file:mr-4 file:cursor-pointer file:rounded-md file:border-0 file:bg-primary file:px-4 file:py-2 file:text-sm file:font-medium file:text-primary-foreground hover:file:bg-primary/90"
            />
            <p className="text-xs text-muted-foreground">
              Formato supportato: PDF
              {maxSizeBytes !== undefined && ` | Dimensione max: ${formatSizeLimit(maxSizeBytes)}`}
            </p>
          </div>

          {/* Validation Errors */}
          {hasValidationErrors && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Validation Failed</AlertTitle>
              <AlertDescription>
                <ul className="mt-2 space-y-1 text-sm">
                  {Object.entries(validationErrors).map(([key, message]) => (
                    <li key={key}>• {message}</li>
                  ))}
                </ul>
              </AlertDescription>
            </Alert>
          )}

          {/* Upload Error */}
          {uploadError && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Upload fallito</AlertTitle>
              <AlertDescription className="text-sm">{uploadError}</AlertDescription>
            </Alert>
          )}

          {/* Selected File Info */}
          {file && !hasValidationErrors && (
            <div className="rounded-md border bg-muted/50 p-4">
              <div className="flex items-start gap-3">
                <FileText className="h-5 w-5 text-muted-foreground" />
                <div className="flex-1 space-y-1">
                  <p className="text-sm font-medium">{file.name}</p>
                  <p className="text-xs text-muted-foreground">{formatFileSize(file.size)}</p>
                </div>
                {uploadSuccess && <CheckCircle className="h-5 w-5 text-green-500" />}
              </div>
            </div>
          )}

          {/* Upload Progress */}
          {isUploading && progress !== undefined && (
            <div className="space-y-2">
              <div className="flex items-center justify-between text-sm">
                <span className="text-muted-foreground">Uploading...</span>
                <span className="font-medium">{Math.round(progress)}%</span>
              </div>
              <Progress value={progress} className="h-2" />
            </div>
          )}

          {/* Success Message */}
          {uploadSuccess && (
            <Alert>
              <CheckCircle className="h-4 w-4 text-green-500" />
              <AlertTitle>Upload completato</AlertTitle>
              <AlertDescription className="text-sm">
                PDF caricato con successo. Clicca &quot;Avanti&quot; per continuare.
              </AlertDescription>
            </Alert>
          )}

          {/* Upload Button */}
          {!uploadSuccess && (
            <Button onClick={handleUpload} disabled={!canUpload || validating} className="w-full">
              {isUploading ? (
                <>Uploading...</>
              ) : validating ? (
                <>Validating...</>
              ) : (
                <>
                  <Upload className="mr-2 h-4 w-4" />
                  Upload PDF
                </>
              )}
            </Button>
          )}
        </div>
      </Card>
    </div>
  );
}
