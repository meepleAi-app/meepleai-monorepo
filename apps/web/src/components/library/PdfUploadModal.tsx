/**
 * PdfUploadModal Component (Issue #2518, #2616)
 *
 * Modal for uploading custom PDF rulebook for a game with:
 * - Drag & drop area
 * - File input (PDF only, max 50MB)
 * - PDF preview before upload (Issue #2616)
 * - Progress bar during upload
 * - Replace with Standard PDF button
 *
 * Flow: File Selection → Preview → Confirm → Upload
 */

'use client';

import { useState, ChangeEvent, FormEvent, useCallback, useEffect } from 'react';

import { Loader2, Upload, FileText, X, Check, Eye, ChevronLeft } from 'lucide-react';
import { pdfjs } from 'react-pdf';

import { toast } from '@/components/layout/Toast';
import { PdfPreview } from '@/components/pdf/PdfPreview';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Progress } from '@/components/ui/feedback/progress';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';

// Configure PDF.js worker for page count validation
// Use pdfjs.version to ensure worker matches the bundled API version
if (typeof window !== 'undefined') {
  pdfjs.GlobalWorkerOptions.workerSrc = `/pdf.worker.min.mjs?v=${pdfjs.version}`;
}

// Validation constants (Issue #2616)
const MAX_FILE_SIZE_MB = 50;
const MAX_FILE_SIZE_BYTES = MAX_FILE_SIZE_MB * 1024 * 1024;
const MAX_PAGE_COUNT = 500;

type UploadStep = 'select' | 'preview' | 'uploading';

interface PdfUploadModalProps {
  isOpen: boolean;
  onClose: () => void;
  gameId: string;
  gameTitle: string;
}

export function PdfUploadModal({ isOpen, onClose, gameId: _gameId, gameTitle }: PdfUploadModalProps) {
  const [file, setFile] = useState<File | null>(null);
  const [step, setStep] = useState<UploadStep>('select');
  const [uploadProgress, setUploadProgress] = useState(0);
  const [validationErrors, setValidationErrors] = useState<string[]>([]);
  const [pageCount, setPageCount] = useState<number | null>(null);
  const [isValidating, setIsValidating] = useState(false);

  // Reset state when modal opens/closes
  useEffect(() => {
    if (!isOpen) {
      // Reset all state when modal closes
      setFile(null);
      setStep('select');
      setUploadProgress(0);
      setValidationErrors([]);
      setPageCount(null);
      setIsValidating(false);
    }
  }, [isOpen]);

  // Synchronous file validation (type, size, empty)
  const validateFileSync = useCallback((selectedFile: File): string[] => {
    const errors: string[] = [];

    // Check file type
    if (selectedFile.type !== 'application/pdf') {
      errors.push('Il file deve essere in formato PDF');
    }

    // Check file size (max 50MB - Issue #2616)
    if (selectedFile.size > MAX_FILE_SIZE_BYTES) {
      errors.push(`Il file è troppo grande (max ${MAX_FILE_SIZE_MB}MB)`);
    }

    // Check if file is not empty
    if (selectedFile.size === 0) {
      errors.push('Il file è vuoto');
    }

    return errors;
  }, []);

  // Async page count validation (Issue #2616)
  const validatePageCount = useCallback(async (selectedFile: File): Promise<string | null> => {
    try {
      const arrayBuffer = await selectedFile.arrayBuffer();
      const pdf = await pdfjs.getDocument({ data: new Uint8Array(arrayBuffer) }).promise;
      const pages = pdf.numPages;
      setPageCount(pages);

      if (pages > MAX_PAGE_COUNT) {
        return `Il PDF ha troppe pagine: ${pages} (max ${MAX_PAGE_COUNT} pagine)`;
      }
      return null;
    } catch (_error) {
      return 'Impossibile leggere il PDF. Il file potrebbe essere corrotto.';
    }
  }, []);

  const handleFileChange = useCallback(async (e: ChangeEvent<HTMLInputElement>) => {
    const selectedFile = e.target.files?.[0];
    if (!selectedFile) return;

    // Reset state
    setValidationErrors([]);
    setPageCount(null);

    // Sync validation first
    const syncErrors = validateFileSync(selectedFile);
    if (syncErrors.length > 0) {
      setValidationErrors(syncErrors);
      setFile(null);
      return;
    }

    // Async page count validation
    setIsValidating(true);
    const pageError = await validatePageCount(selectedFile);
    setIsValidating(false);

    if (pageError) {
      setValidationErrors([pageError]);
      setFile(null);
      return;
    }

    // All validations passed
    setFile(selectedFile);
    setUploadProgress(0);
  }, [validateFileSync, validatePageCount]);

  // Show preview step
  const handleShowPreview = useCallback(() => {
    if (file) {
      setStep('preview');
    }
  }, [file]);

  // Go back to select step
  const handleBackToSelect = useCallback(() => {
    setStep('select');
  }, []);

  // Close modal and reset state
  const handleClose = useCallback(() => {
    setFile(null);
    setStep('select');
    setUploadProgress(0);
    setValidationErrors([]);
    setPageCount(null);
    setIsValidating(false);
    onClose();
  }, [onClose]);

  // Confirm upload from preview
  const handleConfirmUpload = useCallback(async () => {
    if (!file) return;

    setStep('uploading');
    setUploadProgress(0);

    try {
      // Simulate upload progress (replace with actual upload logic)
      // TODO: Implement actual PDF upload with backend endpoint
      const interval = setInterval(() => {
        setUploadProgress((prev) => {
          if (prev >= 90) {
            clearInterval(interval);
            return 90;
          }
          return prev + 10;
        });
      }, 200);

      // Simulate API call
      await new Promise((resolve) => setTimeout(resolve, 2000));
      clearInterval(interval);
      setUploadProgress(100);

      toast.success(`PDF "${file.name}" caricato con successo per "${gameTitle}"!`);

      // Close modal after short delay
      setTimeout(() => {
        handleClose();
      }, 500);
    } catch (error) {
      toast.error(
        error instanceof Error ? error.message : 'Errore durante il caricamento del PDF'
      );
      setUploadProgress(0);
      setStep('preview'); // Go back to preview on error
    }
  }, [file, gameTitle, handleClose]);

  const handleUpload = async (e: FormEvent) => {
    e.preventDefault();
    handleShowPreview();
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
  };

  // Determine modal size based on step
  const modalSizeClass = step === 'preview' ? 'sm:max-w-6xl h-[90vh]' : 'sm:max-w-[500px]';

  return (
    <Dialog open={isOpen} onOpenChange={handleClose}>
      <DialogContent
        className={`${modalSizeClass} flex flex-col`}
        data-testid="pdf-upload-modal"
        data-step={step}
      >
        <DialogHeader>
          <DialogTitle data-testid="modal-title">
            {step === 'select' && 'Carica PDF Personalizzato'}
            {step === 'preview' && `Anteprima: ${file?.name ?? 'PDF'}`}
            {step === 'uploading' && 'Caricamento in corso...'}
          </DialogTitle>
          <DialogDescription>
            {step === 'select' && (
              <>Carica un regolamento personalizzato per <strong>{gameTitle}</strong></>
            )}
            {step === 'preview' && (
              <>Verifica il documento prima di procedere con il caricamento</>
            )}
            {step === 'uploading' && (
              <>Attendere il completamento del caricamento</>
            )}
          </DialogDescription>
        </DialogHeader>

        {/* Step 1: File Selection */}
        {step === 'select' && (
          <>
            <form onSubmit={handleUpload} className="space-y-4">
              {/* File Input */}
              <div className="space-y-2">
                <Label htmlFor="pdf-file">File PDF</Label>
                <Input
                  id="pdf-file"
                  type="file"
                  accept="application/pdf"
                  onChange={handleFileChange}
                  disabled={isValidating}
                  className={validationErrors.length > 0 ? 'border-destructive' : ''}
                  data-testid="pdf-file-input"
                />
                <p className="text-xs text-muted-foreground">
                  Formato: PDF • Dimensione massima: {MAX_FILE_SIZE_MB}MB • Max {MAX_PAGE_COUNT} pagine
                </p>

                {/* Validating State */}
                {isValidating && (
                  <div
                    className="flex items-center gap-2 p-3 bg-blue-50 dark:bg-blue-950 rounded-md border border-blue-200 dark:border-blue-800"
                    data-testid="validating-state"
                  >
                    <Loader2 className="h-5 w-5 text-blue-600 dark:text-blue-400 animate-spin" />
                    <p className="text-sm text-blue-900 dark:text-blue-100">
                      Validazione PDF in corso...
                    </p>
                  </div>
                )}

                {/* Validation Errors */}
                {validationErrors.length > 0 && (
                  <Alert variant="destructive" data-testid="validation-errors">
                    <AlertTitle>Errore di Validazione</AlertTitle>
                    <AlertDescription>
                      <ul className="list-disc pl-5 space-y-1">
                        {validationErrors.map((error, index) => (
                          <li key={index} className="text-sm">
                            {error}
                          </li>
                        ))}
                      </ul>
                    </AlertDescription>
                  </Alert>
                )}

                {/* File Selected (Success State) */}
                {file && validationErrors.length === 0 && !isValidating && (
                  <div
                    className="flex items-center gap-2 p-3 bg-green-50 dark:bg-green-950 rounded-md border border-green-200 dark:border-green-800"
                    data-testid="file-selected"
                  >
                    <FileText className="h-5 w-5 text-green-600 dark:text-green-400" />
                    <div className="flex-1 min-w-0">
                      <p className="text-sm font-medium text-green-900 dark:text-green-100 truncate">
                        {file.name}
                      </p>
                      <p className="text-xs text-green-700 dark:text-green-300">
                        {formatFileSize(file.size)} • {pageCount} {pageCount === 1 ? 'pagina' : 'pagine'}
                      </p>
                    </div>
                    <Check className="h-5 w-5 text-green-600 dark:text-green-400" />
                  </div>
                )}
              </div>
            </form>

            <DialogFooter>
              <Button variant="outline" onClick={handleClose} data-testid="cancel-button">
                <X className="mr-2 h-4 w-4" />
                Annulla
              </Button>
              <Button
                onClick={handleShowPreview}
                disabled={!file || validationErrors.length > 0 || isValidating}
                data-testid="preview-button"
              >
                <Eye className="mr-2 h-4 w-4" />
                Anteprima
              </Button>
            </DialogFooter>
          </>
        )}

        {/* Step 2: PDF Preview */}
        {step === 'preview' && file && (
          <>
            <div className="flex-1 overflow-hidden" data-testid="pdf-preview-container">
              <PdfPreview file={file} />
            </div>

            <DialogFooter className="border-t pt-4">
              <Button variant="outline" onClick={handleBackToSelect} data-testid="back-button">
                <ChevronLeft className="mr-2 h-4 w-4" />
                Indietro
              </Button>
              <Button variant="outline" onClick={handleClose} data-testid="cancel-button-preview">
                <X className="mr-2 h-4 w-4" />
                Annulla
              </Button>
              <Button onClick={handleConfirmUpload} data-testid="confirm-upload-button">
                <Upload className="mr-2 h-4 w-4" />
                Conferma Upload
              </Button>
            </DialogFooter>
          </>
        )}

        {/* Step 3: Uploading */}
        {step === 'uploading' && (
          <>
            <div className="space-y-4 py-8" data-testid="uploading-state">
              <div className="flex flex-col items-center gap-4">
                <Loader2 className="h-12 w-12 text-primary animate-spin" />
                <div className="text-center">
                  <p className="font-medium" data-testid="uploading-filename">{file?.name}</p>
                  <p className="text-sm text-muted-foreground">
                    Caricamento in corso...
                  </p>
                </div>
              </div>

              <div className="space-y-2">
                <div className="flex items-center justify-between text-sm">
                  <span className="text-muted-foreground">Progresso</span>
                  <span className="font-medium" data-testid="upload-progress">{uploadProgress}%</span>
                </div>
                <Progress value={uploadProgress} className="h-2" data-testid="upload-progress-bar" />
              </div>
            </div>

            <DialogFooter>
              <Button variant="outline" onClick={handleClose} disabled data-testid="cancel-button-uploading">
                <X className="mr-2 h-4 w-4" />
                Annulla
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}
