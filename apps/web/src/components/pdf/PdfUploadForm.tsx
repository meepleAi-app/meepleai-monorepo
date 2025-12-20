import { type ChangeEvent, type FormEvent, useState } from 'react';

import { AlertCircle, Upload } from 'lucide-react';
import dynamic from 'next/dynamic';

import { LoadingButton } from '@/components/loading';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Progress } from '@/components/ui/progress';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/select';
import { useChunkedUpload } from '@/hooks/useChunkedUpload';
import { ApiError } from '@/lib/api';
import { categorizeError, type CategorizedError, extractCorrelationId } from '@/lib/errorUtils';
import { retryWithBackoff, isRetryableError } from '@/lib/retryUtils';

// Dynamic import to prevent SSR issues with react-pdf
const PdfPreview = dynamic(
  () => import('./PdfPreview').then(mod => ({ default: mod.PdfPreview })),
  {
    ssr: false,
    loading: () => (
      <div className="p-5 text-center text-sm text-muted-foreground">Loading PDF preview...</div>
    ),
  }
);

interface PdfUploadFormProps {
  gameId: string;
  gameName: string;
  onUploadSuccess: (documentId: string) => void;
  onUploadError: (error: CategorizedError) => void;
  onUploadStart?: () => void;
}

// Validation constants
const MAX_PDF_SIZE_BYTES = 104857600; // 100 MB
const ALLOWED_MIME_TYPES = ['application/pdf'];
const PDF_MAGIC_BYTES = '%PDF-';

interface ValidationResult {
  isValid: boolean;
  errors: Record<string, string>;
}

// Client-side PDF validation helper
async function validatePdfFile(file: File): Promise<ValidationResult> {
  const errors: Record<string, string> = {};

  // MIME type check
  if (!ALLOWED_MIME_TYPES.includes(file.type)) {
    errors.mimeType = `Invalid file type. Expected PDF, got ${file.type || 'unknown'}`;
  }

  // Size check
  if (file.size > MAX_PDF_SIZE_BYTES) {
    const sizeMB = (file.size / 1024 / 1024).toFixed(1);
    const maxMB = (MAX_PDF_SIZE_BYTES / 1024 / 1024).toFixed(0);
    errors.size = `File size (${sizeMB} MB) exceeds maximum allowed (${maxMB} MB)`;
  }

  if (file.size === 0) {
    errors.size = 'File is empty (0 bytes)';
  }

  // Magic bytes check
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
  // eslint-disable-next-line security/detect-object-injection
  return `${Math.round((bytes / Math.pow(k, i)) * 100) / 100} ${sizes[i]}`;
}

/**
 * PdfUploadForm - PDF file upload with comprehensive validation
 *
 * Features:
 * - Client-side PDF validation (type, size, magic bytes)
 * - Retry logic with exponential backoff
 * - Language selection
 * - PDF preview
 * - Error categorization and display
 * - Loading states
 */
export function PdfUploadForm({
  gameId,
  gameName: _gameName,
  onUploadSuccess,
  onUploadError,
  onUploadStart,
}: PdfUploadFormProps) {
  const [file, setFile] = useState<File | null>(null);
  const [language, setLanguage] = useState('en');
  const [uploading, setUploading] = useState(false);
  const [validating, setValidating] = useState(false);
  const [validationErrors, setValidationErrors] = useState<Record<string, string>>({});
  const [retryCount, setRetryCount] = useState(0);
  const [retryMessage, setRetryMessage] = useState<string | null>(null);

  const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

  // Chunked upload hook for large files
  const {
    progress: chunkedProgress,
    uploadChunked,
    shouldUseChunkedUpload,
    isUploading: isChunkedUploading,
    reset: resetChunkedUpload,
  } = useChunkedUpload(API_BASE);

  // Determine if current file needs chunked upload
  const needsChunkedUpload = file ? shouldUseChunkedUpload(file) : false;

  const handleFileChange = async (e: ChangeEvent<HTMLInputElement>) => {
    const selectedFile = e.target.files?.[0];
    if (!selectedFile) {
      setFile(null);
      setValidationErrors({});
      resetChunkedUpload();
      return;
    }

    setValidating(true);
    setValidationErrors({});
    setRetryMessage(null);
    resetChunkedUpload();

    try {
      const validation = await validatePdfFile(selectedFile);

      if (!validation.isValid) {
        setValidationErrors(validation.errors);
        setFile(null);
        e.target.value = '';
      } else {
        setFile(selectedFile);
        setValidationErrors({});
      }
    } catch (error) {
      console.error('Validation error:', error);
      setValidationErrors({ general: 'Failed to validate file. Please try again.' });
      setFile(null);
      e.target.value = '';
    } finally {
      setValidating(false);
    }
  };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!file) return;

    onUploadStart?.();

    // Use chunked upload for large files
    if (needsChunkedUpload) {
      const result = await uploadChunked(file, gameId);

      if (result.success && result.documentId) {
        onUploadSuccess(result.documentId);
      } else {
        onUploadError(categorizeError(new Error(result.error || 'Chunked upload failed')));
      }
      return;
    }

    // Standard upload for smaller files
    setUploading(true);
    setRetryMessage(null);
    setRetryCount(0);

    try {
      const formData = new FormData();
      formData.append('file', file);
      formData.append('gameId', gameId);
      formData.append('language', language);

      const response = await retryWithBackoff(
        async () => {
          const res = await fetch(`${API_BASE}/api/v1/ingest/pdf`, {
            method: 'POST',
            body: formData,
            credentials: 'include',
          });

          if (!res.ok) {
            const correlationId = extractCorrelationId(res);
            const errorBody = await res.json().catch(() => ({}));
            const errorMessage = errorBody.error ?? res.statusText;

            const apiError = new ApiError({
              message: errorMessage,
              statusCode: res.status,
              correlationId,
              response: res,
            });
            throw apiError;
          }

          return res;
        },
        {
          maxAttempts: 3,
          shouldRetry: error => isRetryableError(error),
          onRetry: (error, attempt, delayMs) => {
            setRetryCount(attempt);
            setRetryMessage(
              `Upload failed. Retrying (attempt ${attempt}/3) in ${Math.round(delayMs / 1000)}s...`
            );
          },
        }
      );

      const data = (await response.json()) as { documentId: string };
      onUploadSuccess(data.documentId);
    } catch (error) {
      const correlationId = error instanceof ApiError ? error.correlationId : undefined;
      const response = error instanceof ApiError ? error.response : undefined;

      const categorized = categorizeError(error, response, correlationId);
      onUploadError(categorized);
    } finally {
      setUploading(false);
      setRetryMessage(null);
      setRetryCount(0);
    }
  };

  const hasErrors = Object.keys(validationErrors).length > 0;

  return (
    <Card className="p-4">
      <form onSubmit={handleSubmit} className="space-y-4">
        <div>
          <Label htmlFor="pdf-file">PDF File</Label>
          <Input
            id="pdf-file"
            type="file"
            accept="application/pdf"
            onChange={handleFileChange}
            disabled={uploading || validating || isChunkedUploading}
            className={hasErrors ? 'border-destructive' : ''}
          />
          {validating && <p className="text-sm text-muted-foreground mt-1">Validating file...</p>}
          {file && !hasErrors && (
            <div className="mt-1">
              <p className="text-sm text-green-600">
                ✓ {file.name} ({formatFileSize(file.size)})
              </p>
              {needsChunkedUpload && (
                <p className="text-xs text-muted-foreground mt-1">
                  <Upload className="inline h-3 w-3 mr-1" />
                  Large file - will use chunked upload
                </p>
              )}
            </div>
          )}
          {hasErrors && (
            <Alert variant="destructive" className="mt-2">
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Validation Failed</AlertTitle>
              <AlertDescription>
                <ul className="list-disc pl-5 space-y-1">
                  {Object.values(validationErrors).map((error, i) => (
                    <li key={i}>{error}</li>
                  ))}
                </ul>
              </AlertDescription>
            </Alert>
          )}
        </div>

        <div>
          <Label htmlFor="language">Document Language</Label>
          <Select
            value={language}
            onValueChange={setLanguage}
            disabled={uploading || isChunkedUploading}
          >
            <SelectTrigger id="language">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="en">English</SelectItem>
              <SelectItem value="it">Italiano</SelectItem>
              <SelectItem value="de">Deutsch</SelectItem>
              <SelectItem value="fr">Français</SelectItem>
              <SelectItem value="es">Español</SelectItem>
            </SelectContent>
          </Select>
        </div>

        {file && !hasErrors && (
          <div className="border rounded-md p-2 bg-muted/20">
            <p className="text-xs font-medium mb-1 text-muted-foreground">Preview</p>
            <PdfPreview file={file} />
          </div>
        )}

        {retryMessage && (
          <Alert>
            <AlertDescription className="flex items-center gap-2">
              <div className="animate-spin h-4 w-4 border-2 border-primary border-t-transparent rounded-full" />
              {retryMessage}
            </AlertDescription>
          </Alert>
        )}

        {/* Chunked upload progress */}
        {isChunkedUploading && (
          <div className="space-y-2">
            <div className="flex justify-between text-sm">
              <span className="text-muted-foreground">
                {chunkedProgress.status === 'initializing' && 'Initializing upload...'}
                {chunkedProgress.status === 'uploading' &&
                  `Uploading chunk ${chunkedProgress.currentChunk}/${chunkedProgress.totalChunks}`}
                {chunkedProgress.status === 'completing' && 'Finalizing upload...'}
              </span>
              <span className="font-medium">{chunkedProgress.progressPercentage.toFixed(0)}%</span>
            </div>
            <Progress value={chunkedProgress.progressPercentage} className="h-2" />
            <p className="text-xs text-muted-foreground">
              {formatFileSize(chunkedProgress.uploadedBytes)} /{' '}
              {formatFileSize(chunkedProgress.totalBytes)}
            </p>
          </div>
        )}

        {chunkedProgress.status === 'error' && chunkedProgress.error && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>Upload Failed</AlertTitle>
            <AlertDescription>{chunkedProgress.error}</AlertDescription>
          </Alert>
        )}

        <LoadingButton
          type="submit"
          isLoading={uploading || isChunkedUploading}
          disabled={!file || hasErrors || validating || isChunkedUploading}
          className="w-full"
        >
          {isChunkedUploading
            ? chunkedProgress.status === 'completing'
              ? 'Finalizing...'
              : `Uploading ${chunkedProgress.progressPercentage.toFixed(0)}%`
            : uploading
              ? retryCount > 0
                ? `Uploading (retry ${retryCount}/3)...`
                : 'Uploading...'
              : 'Upload PDF'}
        </LoadingButton>
      </form>
    </Card>
  );
}
