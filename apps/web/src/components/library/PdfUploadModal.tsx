/**
 * PdfUploadModal Component (Issue #2518, #2616)
 *
 * Modal for uploading custom PDF rulebook for a game with:
 * - Drag & drop area
 * - File input (PDF only, within the backend's size limit — #4449)
 * - PDF preview before upload (Issue #2616)
 * - Progress bar during upload
 * - Replace with Standard PDF button
 *
 * Flow: File Selection → Preview → Confirm → Upload
 */

'use client';

import { useState, ChangeEvent, FormEvent, useCallback, useEffect } from 'react';

import { useQueryClient } from '@tanstack/react-query';
import { Loader2, Upload, FileText, X, Check, Eye, ChevronLeft } from 'lucide-react';

import { toast } from '@/components/layout/Toast';
import { PdfPreview, PdfProgressBar, PdfErrorCard } from '@/components/pdf';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
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
import { libraryKeys } from '@/hooks/queries/useLibrary';
import { usePdfUploadLimits } from '@/hooks/queries/usePdfUploadLimits';
import { api } from '@/lib/api';
import { formatSizeLimit } from '@/lib/format/file-size';

import { OwnershipDeclarationDialog } from './OwnershipDeclarationDialog';

// Validation constants (Issue #2616, #4133)

type UploadStep = 'select' | 'preview' | 'uploading';

/**
 * #4756: an upload does not declare ownership, and the rulebook it adds stays closed to the
 * assistant until the user does (RagAccessService.CanAccessRagAsync). True when the game is in the
 * user's library, its manual is not readable yet, and the domain would accept the declaration
 * (it refuses it from the wishlist). Any failure answers false: the upload already succeeded, and
 * not proposing is the safe side.
 */
async function libraryEntryLacksManualAccess(gameId: string): Promise<boolean> {
  try {
    const entry = await api.library.getGameDetail(gameId);
    return !entry.hasRagAccess && entry.currentState !== 'Wishlist';
  } catch {
    return false;
  }
}

interface PdfUploadModalProps {
  isOpen: boolean;
  onClose: () => void;
  gameId: string;
  gameTitle: string;
  onUploadSuccess?: () => void;
}

export function PdfUploadModal({
  isOpen,
  onClose,
  gameId,
  gameTitle,
  onUploadSuccess,
}: PdfUploadModalProps) {
  const [file, setFile] = useState<File | null>(null);
  const [step, setStep] = useState<UploadStep>('select');
  const [uploadProgress, setUploadProgress] = useState(0);
  const [validationErrors, setValidationErrors] = useState<string[]>([]);
  const [isValidating, setIsValidating] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  // #4756: its own state, because the effect below resets the rest when the modal closes and the
  // proposal is made right as it does.
  const [ownershipOpen, setOwnershipOpen] = useState(false);
  const queryClient = useQueryClient();
  // #4449: the backend's size limit for this user, not a constant of this component.
  const { data: uploadLimits } = usePdfUploadLimits();
  const maxSizeBytes = uploadLimits?.maxFileSizeBytes;

  // Reset state when modal opens/closes
  useEffect(() => {
    if (!isOpen) {
      // Reset all state when modal closes
      setFile(null);
      setStep('select');
      setUploadProgress(0);
      setValidationErrors([]);
      setIsValidating(false);
      setUploadError(null);
    }
  }, [isOpen]);

  // Synchronous file validation (type, size, empty)
  const validateFileSync = useCallback(
    (selectedFile: File): string[] => {
      const errors: string[] = [];

      // Check file type
      if (selectedFile.type !== 'application/pdf') {
        errors.push('Il file deve essere in formato PDF');
      }

      // Check file size (Issue #2616; #4449: the backend's limit)
      if (maxSizeBytes !== undefined && selectedFile.size > maxSizeBytes) {
        errors.push(`Il file è troppo grande (max ${formatSizeLimit(maxSizeBytes)})`);
      }

      // Check if file is not empty
      if (selectedFile.size === 0) {
        errors.push('Il file è vuoto');
      }

      return errors;
    },
    [maxSizeBytes]
  );

  const handleFileChange = useCallback(
    async (e: ChangeEvent<HTMLInputElement>) => {
      const selectedFile = e.target.files?.[0];
      if (!selectedFile) return;

      // Reset state
      setValidationErrors([]);

      // Sync validation only
      const syncErrors = validateFileSync(selectedFile);
      if (syncErrors.length > 0) {
        setValidationErrors(syncErrors);
        setFile(null);
        return;
      }

      // All validations passed
      setFile(selectedFile);
      setUploadProgress(0);
    },
    [validateFileSync]
  );

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
    setIsValidating(false);
    setUploadError(null);
    onClose();
  }, [onClose]);

  // Retry upload after error
  const handleRetryUpload = useCallback(() => {
    setUploadError(null);
    setStep('preview');
  }, []);

  // Confirm upload from preview
  const handleConfirmUpload = useCallback(async () => {
    if (!file) return;

    setStep('uploading');
    setUploadProgress(0);
    setUploadError(null);

    try {
      await api.pdf.uploadPdf(gameId, file, percent => {
        setUploadProgress(percent);
      });

      setUploadProgress(100);
      toast.success(`PDF "${file.name}" caricato con successo per "${gameTitle}"!`);
      onUploadSuccess?.();

      // #4756: propose, never write, the ownership declaration the new rulebook depends on.
      const proposeOwnership = await libraryEntryLacksManualAccess(gameId);

      // Close modal after short delay
      setTimeout(() => {
        handleClose();
        if (proposeOwnership) setOwnershipOpen(true);
      }, 500);
    } catch (error) {
      const errorMessage =
        error instanceof Error ? error.message : 'Errore durante il caricamento del PDF';
      setUploadError(errorMessage);
      toast.error(errorMessage);
      setUploadProgress(0);
      // For duplicate content (409 Conflict), go back to select step (retry won't help)
      const isDuplicate =
        (error as Error & { statusCode?: number }).statusCode === 409 ||
        (error as Error & { errorCode?: string }).errorCode === 'duplicate_content';
      if (isDuplicate) {
        setStep('select');
        setUploadError(null);
        setValidationErrors([errorMessage]);
      }
    }
  }, [file, gameId, gameTitle, handleClose, onUploadSuccess]);

  const handleUpload = async (e: FormEvent) => {
    e.preventDefault();
    handleShowPreview();
  };

  // #4756: the manual may still be processing, so this does not promise the assistant can read it.
  const handleOwnershipDeclared = useCallback(() => {
    toast.success(
      `Possesso di "${gameTitle}" dichiarato. L'assistente potrà consultare il manuale appena sarà elaborato.`
    );
    void queryClient.invalidateQueries({ queryKey: libraryKeys.all });
  }, [gameTitle, queryClient]);

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
  };

  // Determine modal size based on step
  const modalSizeClass = step === 'preview' ? 'sm:max-w-6xl h-[90vh]' : 'sm:max-w-[500px]';

  return (
    <>
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
                <>
                  Carica un regolamento personalizzato per <strong>{gameTitle}</strong>
                </>
              )}
              {step === 'preview' && (
                <>Verifica il documento prima di procedere con il caricamento</>
              )}
              {step === 'uploading' && <>Attendere il completamento del caricamento</>}
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
                    Formato: PDF
                    {maxSizeBytes !== undefined &&
                      ` • Dimensione massima: ${formatSizeLimit(maxSizeBytes)}`}
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
                          {formatFileSize(file.size)}
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
                {!uploadError ? (
                  <>
                    <div className="flex flex-col items-center gap-4">
                      <Loader2 className="h-12 w-12 text-primary animate-spin" />
                      <div className="text-center">
                        <p className="font-medium" data-testid="uploading-filename">
                          {file?.name}
                        </p>
                        <p className="text-sm text-muted-foreground">Caricamento in corso...</p>
                      </div>
                    </div>

                    {/* New: PdfProgressBar with state (Issue #4217) */}
                    <PdfProgressBar
                      state="uploading"
                      progress={uploadProgress}
                      showLabel
                      className="mt-4"
                    />
                  </>
                ) : (
                  /* New: PdfErrorCard on upload failure (Issue #4217) */
                  <PdfErrorCard
                    error={uploadError}
                    category="network"
                    canRetry
                    onRetry={handleRetryUpload}
                    className="mt-4"
                  />
                )}
              </div>

              <DialogFooter>
                <Button
                  variant="outline"
                  onClick={handleClose}
                  disabled={!uploadError}
                  data-testid="cancel-button-uploading"
                >
                  <X className="mr-2 h-4 w-4" />
                  {uploadError ? 'Chiudi' : 'Annulla'}
                </Button>
              </DialogFooter>
            </>
          )}
        </DialogContent>
      </Dialog>
      <OwnershipDeclarationDialog
        gameId={gameId}
        gameName={gameTitle}
        open={ownershipOpen}
        onOpenChange={setOwnershipOpen}
        onOwnershipDeclared={handleOwnershipDeclared}
      />
    </>
  );
}
