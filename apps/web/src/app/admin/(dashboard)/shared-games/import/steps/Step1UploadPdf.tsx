'use client';

/**
 * Step 1: Upload PDF
 * Issue #4162: PDF upload step for game import wizard
 *
 * Features:
 * - Native file input (project standard, no react-dropzone)
 * - Client-side validation: PDF format (magic bytes), max 50MB
 * - Upload progress tracking
 * - Error handling with clear messages
 * - Integration with wizard store
 */

import { type ChangeEvent, useState, useCallback } from 'react';
import type { JSX } from 'react';

import { AlertCircle, Upload, FileText, CheckCircle } from 'lucide-react';

import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/data-display/card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Progress } from '@/components/ui/feedback/progress';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { useUploadPdf } from '@/hooks/queries/useUploadPdf';
import type { UploadedPdf } from '@/stores/useGameImportWizardStore';

export interface Step1UploadPdfProps {
  onUploadComplete?: (pdf: UploadedPdf) => void;
}

// Validation constants (stricter than project's 100MB)
const MAX_PDF_SIZE_BYTES = 52428800; // 50 MB
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
  const [uploadSuccess, setUploadSuccess] = useState(false);

  const {
    mutate: uploadPdf,
    isPending: isUploading,
    progress,
    error: uploadError,
  } = useUploadPdf();

  // Handle file selection
  const handleFileChange = useCallback(async (e: ChangeEvent<HTMLInputElement>) => {
    const selectedFile = e.target.files?.[0];
    if (!selectedFile) {
      setFile(null);
      setValidationErrors({});
      setUploadSuccess(false);
      return;
    }

    setValidating(true);
    setValidationErrors({});
    setUploadSuccess(false);

    // Validate file
    const validation = await validatePdfFile(selectedFile);

    if (!validation.isValid) {
      setValidationErrors(validation.errors);
      setFile(null);
      setValidating(false);
      return;
    }

    setFile(selectedFile);
    setValidating(false);
  }, []);

  // Handle upload
  const handleUpload = useCallback(() => {
    if (!file) return;

    uploadPdf(file, {
      onSuccess: data => {
        setUploadSuccess(true);
        onUploadComplete?.({
          id: data.documentId,
          fileName: data.fileName,
        });
      },
    });
  }, [file, uploadPdf, onUploadComplete]);

  const hasValidationErrors = Object.keys(validationErrors).length > 0;
  const canUpload = file && !hasValidationErrors && !isUploading && !uploadSuccess;

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-lg font-semibold">Upload PDF Rulebook</h3>
        <p className="text-sm text-muted-foreground">
          Select a PDF file to extract game metadata. Maximum file size: 50 MB.
        </p>
      </div>

      {/* File Input */}
      <Card className="p-6">
        <div className="space-y-4">
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
            <p className="text-xs text-muted-foreground">Supported format: PDF | Max size: 50 MB</p>
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
              <AlertTitle>Upload Failed</AlertTitle>
              <AlertDescription className="text-sm">
                {uploadError instanceof Error
                  ? uploadError.message
                  : 'An error occurred during upload'}
              </AlertDescription>
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
              <AlertTitle>Upload Successful</AlertTitle>
              <AlertDescription className="text-sm">
                PDF uploaded successfully. Click "Next" to continue.
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
