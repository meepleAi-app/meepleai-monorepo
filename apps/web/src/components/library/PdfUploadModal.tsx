/**
 * PdfUploadModal Component (Issue #2518)
 *
 * Modal for uploading custom PDF rulebook for a game with:
 * - Drag & drop area
 * - File input (PDF only, max 100MB)
 * - Progress bar during upload
 * - PDF preview after upload
 * - Replace with Standard PDF button
 */

'use client';

import { useState, ChangeEvent, FormEvent } from 'react';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Progress } from '@/components/ui/progress';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { toast } from '@/components/layout/Toast';
import { Loader2, Upload, FileText, X, Check } from 'lucide-react';

interface PdfUploadModalProps {
  isOpen: boolean;
  onClose: () => void;
  gameId: string;
  gameTitle: string;
}

export function PdfUploadModal({ isOpen, onClose, gameId, gameTitle }: PdfUploadModalProps) {
  const [file, setFile] = useState<File | null>(null);
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [validationErrors, setValidationErrors] = useState<string[]>([]);

  // File validation
  const validateFile = (selectedFile: File): string[] => {
    const errors: string[] = [];

    // Check file type
    if (selectedFile.type !== 'application/pdf') {
      errors.push('Il file deve essere in formato PDF');
    }

    // Check file size (max 100MB)
    const maxSizeBytes = 100 * 1024 * 1024; // 100MB
    if (selectedFile.size > maxSizeBytes) {
      errors.push(`Il file è troppo grande. Dimensione massima: 100MB`);
    }

    // Check if file is not empty
    if (selectedFile.size === 0) {
      errors.push('Il file è vuoto');
    }

    return errors;
  };

  const handleFileChange = (e: ChangeEvent<HTMLInputElement>) => {
    const selectedFile = e.target.files?.[0];
    if (!selectedFile) return;

    const errors = validateFile(selectedFile);

    if (errors.length > 0) {
      setValidationErrors(errors);
      setFile(null);
    } else {
      setFile(selectedFile);
      setValidationErrors([]);
      setUploadProgress(0);
    }
  };

  const handleUpload = async (e: FormEvent) => {
    e.preventDefault();
    if (!file) return;

    setUploading(true);
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
    } finally {
      setUploading(false);
    }
  };

  const handleClose = () => {
    setFile(null);
    setUploadProgress(0);
    setValidationErrors([]);
    setUploading(false);
    onClose();
  };

  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return bytes + ' B';
    if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + ' KB';
    return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
  };

  return (
    <Dialog open={isOpen} onOpenChange={handleClose}>
      <DialogContent className="sm:max-w-[500px]">
        <DialogHeader>
          <DialogTitle>Carica PDF Personalizzato</DialogTitle>
          <DialogDescription>
            Carica un regolamento personalizzato per <strong>{gameTitle}</strong>
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={handleUpload} className="space-y-4">
          {/* File Input */}
          <div className="space-y-2">
            <Label htmlFor="pdf-file">File PDF</Label>
            <Input
              id="pdf-file"
              type="file"
              accept="application/pdf"
              onChange={handleFileChange}
              disabled={uploading}
              className={validationErrors.length > 0 ? 'border-destructive' : ''}
            />
            <p className="text-xs text-muted-foreground">
              Formato: PDF • Dimensione massima: 100MB
            </p>

            {/* Validation Errors */}
            {validationErrors.length > 0 && (
              <Alert variant="destructive">
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
            {file && validationErrors.length === 0 && (
              <div className="flex items-center gap-2 p-3 bg-green-50 dark:bg-green-950 rounded-md border border-green-200 dark:border-green-800">
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

          {/* Upload Progress */}
          {uploading && (
            <div className="space-y-2">
              <div className="flex items-center justify-between text-sm">
                <span className="text-muted-foreground">Caricamento in corso...</span>
                <span className="font-medium">{uploadProgress}%</span>
              </div>
              <Progress value={uploadProgress} className="h-2" />
            </div>
          )}

          {/* TODO: PDF Preview after upload (when endpoint available) */}
        </form>

        <DialogFooter>
          <Button variant="outline" onClick={handleClose} disabled={uploading}>
            <X className="mr-2 h-4 w-4" />
            Annulla
          </Button>
          <Button
            onClick={handleUpload}
            disabled={!file || validationErrors.length > 0 || uploading}
          >
            {uploading ? (
              <>
                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                Caricamento...
              </>
            ) : (
              <>
                <Upload className="mr-2 h-4 w-4" />
                Carica PDF
              </>
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
