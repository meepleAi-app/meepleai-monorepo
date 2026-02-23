/**
 * PDF Upload Section for Admin SharedGame Wizard - Issue #3642
 *
 * Allows admins to upload rulebook PDFs during game creation/editing.
 * Features:
 * - Drag-and-drop file selection
 * - Upload progress tracking
 * - File validation (PDF, 50MB limit)
 * - Links uploaded PDF to SharedGame
 * - Live processing status polling after upload (Issue #5196)
 */

'use client';

import { useState, useCallback, useEffect, useRef } from 'react';

import { FileUp, X, FileText, CheckCircle2, AlertCircle, Loader2 } from 'lucide-react';
import { toast } from 'sonner';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Progress } from '@/components/ui/feedback/progress';
import { Button } from '@/components/ui/primitives/button';
import { KbCardStatusRow } from '@/components/documents/KbCardStatusRow';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

// ========== Types ==========

export interface UploadedPdf {
  id: string;
  fileName: string;
  fileSize: number;
}

export interface PdfUploadSectionProps {
  /** Game ID to link the PDF to (null for new games) */
  gameId?: string | null;
  /** Callback when PDF is uploaded successfully */
  onPdfUploaded?: (pdf: UploadedPdf) => void;
  /** Callback when PDF is removed */
  onPdfRemoved?: () => void;
  /** Whether the section is disabled */
  disabled?: boolean;
  /** Initially uploaded PDF (for edit mode) */
  existingPdf?: UploadedPdf | null;
}

// ========== Component ==========

export function PdfUploadSection({
  gameId,
  onPdfUploaded,
  onPdfRemoved,
  disabled = false,
  existingPdf = null,
}: PdfUploadSectionProps) {
  const [file, setFile] = useState<File | null>(null);
  const [uploadedPdf, setUploadedPdf] = useState<UploadedPdf | null>(existingPdf);
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [dragActive, setDragActive] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Processing status polling (Issue #5196)
  const [processingDoc, setProcessingDoc] = useState<PdfDocumentDto | null>(null);
  const pollingRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

  const TERMINAL_STATES = new Set(['Ready', 'Failed']);

  // Poll document processing status after upload (Issue #5196)
  useEffect(() => {
    if (!processingDoc || !gameId || TERMINAL_STATES.has(processingDoc.processingState)) {
      if (pollingRef.current) {
        clearInterval(pollingRef.current);
        pollingRef.current = null;
      }
      return;
    }

    const poll = async () => {
      try {
        const res = await fetch(`/api/v1/games/${gameId}/pdfs`, { credentials: 'include' });
        if (!res.ok) return;
        const json = await res.json() as { pdfs?: PdfDocumentDto[] } | PdfDocumentDto[];
        const pdfs: PdfDocumentDto[] = Array.isArray(json) ? json : (json.pdfs ?? []);
        const doc = pdfs.find((p) => p.id === processingDoc.id);
        if (doc) {
          setProcessingDoc(doc);
          if (TERMINAL_STATES.has(doc.processingState)) {
            if (pollingRef.current) {
              clearInterval(pollingRef.current);
              pollingRef.current = null;
            }
            if (doc.processingState === 'Ready') {
              toast.success('PDF indicizzato con successo!');
            }
          }
        }
      } catch {
        // ignore transient errors
      }
    };

    pollingRef.current = setInterval(() => { void poll(); }, 3000);
    return () => {
      if (pollingRef.current) {
        clearInterval(pollingRef.current);
        pollingRef.current = null;
      }
    };
  // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [processingDoc?.id, processingDoc?.processingState, gameId]);

  // Validate file selection
  const validateFile = useCallback((selectedFile: File): boolean => {
    setError(null);

    if (selectedFile.type !== 'application/pdf') {
      setError('Il file deve essere un PDF');
      toast.error('Il file deve essere un PDF');
      return false;
    }

    if (selectedFile.size > 50 * 1024 * 1024) {
      setError('Il file deve essere inferiore a 50MB');
      toast.error('Il file deve essere inferiore a 50MB');
      return false;
    }

    return true;
  }, []);

  // Handle file selection
  const handleFileSelect = useCallback(
    (selectedFile: File) => {
      if (validateFile(selectedFile)) {
        setFile(selectedFile);
        setError(null);
      }
    },
    [validateFile]
  );

  // Handle drag and drop
  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setDragActive(false);
      const droppedFile = e.dataTransfer.files[0];
      if (droppedFile) {
        handleFileSelect(droppedFile);
      }
    },
    [handleFileSelect]
  );

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragActive(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setDragActive(false);
  }, []);

  // Link PDF to SharedGame
  const linkPdfToGame = useCallback(async (targetGameId: string, pdfId: string) => {
    try {
      const response = await fetch(`${API_BASE}/api/v1/admin/shared-games/${targetGameId}/documents`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        credentials: 'include',
        body: JSON.stringify({ documentId: pdfId, setAsActive: true }),
      });

      if (!response.ok) {
        throw new Error('Errore nel collegamento del PDF al gioco');
      }

      toast.success('PDF collegato al gioco');
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore sconosciuto';
      toast.error(`Collegamento fallito: ${message}`);
    }
  }, [API_BASE]);

  // Upload PDF
  const handleUpload = useCallback(async () => {
    if (!file) {
      toast.error('Seleziona un file PDF');
      return;
    }

    setUploading(true);
    setUploadProgress(0);
    setError(null);

    try {
      const formData = new FormData();
      formData.append('file', file);
      formData.append('language', 'it');

      // Use XHR for progress tracking
      const xhr = new XMLHttpRequest();

      const uploadPromise = new Promise<{ documentId: string; fileName: string }>((resolve, reject) => {
        xhr.upload.addEventListener('progress', e => {
          if (e.lengthComputable) {
            setUploadProgress(Math.round((e.loaded / e.total) * 100));
          }
        });

        xhr.addEventListener('load', () => {
          if (xhr.status >= 200 && xhr.status < 300) {
            try {
              const response = JSON.parse(xhr.responseText);
              resolve(response);
            } catch {
              reject(new Error('Risposta server non valida'));
            }
          } else {
            try {
              const errorResponse = JSON.parse(xhr.responseText);
              reject(new Error(errorResponse.error || 'Upload fallito'));
            } catch {
              reject(new Error(`Upload fallito: ${xhr.statusText}`));
            }
          }
        });

        xhr.addEventListener('error', () => {
          reject(new Error('Errore di rete durante l\'upload'));
        });

        xhr.open('POST', '/api/v1/ingest/pdf');
        xhr.withCredentials = true;
        xhr.send(formData);
      });

      const result = await uploadPromise;

      const uploaded: UploadedPdf = {
        id: result.documentId,
        fileName: file.name,
        fileSize: file.size,
      };

      setUploadedPdf(uploaded);
      setFile(null);
      toast.success('PDF caricato con successo!');
      onPdfUploaded?.(uploaded);

      // If we have a gameId, link the PDF to the game and start polling
      if (gameId) {
        await linkPdfToGame(gameId, result.documentId);
        // Start processing status polling (Issue #5196)
        setProcessingDoc({
          id: result.documentId,
          gameId,
          fileName: file.name,
          filePath: '',
          fileSizeBytes: file.size,
          processingStatus: 'Processing',
          uploadedAt: new Date().toISOString(),
          processedAt: null,
          pageCount: null,
          documentType: 'base',
          isPublic: false,
          processingState: 'Uploading',
          progressPercentage: 0,
          retryCount: 0,
          maxRetries: 3,
        });
      }
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Errore sconosciuto';
      setError(message);
      toast.error(`Upload fallito: ${message}`);
    } finally {
      setUploading(false);
    }
  }, [file, API_BASE, gameId, onPdfUploaded, linkPdfToGame]);

  // Remove uploaded PDF
  const handleRemove = useCallback(() => {
    setUploadedPdf(null);
    setFile(null);
    setError(null);
    setProcessingDoc(null);
    onPdfRemoved?.();
  }, [onPdfRemoved]);

  // Format file size
  const formatFileSize = (bytes: number): string => {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(2)} MB`;
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <FileUp className="h-5 w-5" />
          PDF Regolamento
        </CardTitle>
        <CardDescription>
          Carica un PDF del regolamento per abilitare la ricerca RAG (opzionale)
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Uploaded PDF Display */}
        {uploadedPdf && (
          <div className="flex items-center justify-between p-4 bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 rounded-lg">
            <div className="flex items-center gap-3">
              <CheckCircle2 className="h-5 w-5 text-green-600 dark:text-green-400" />
              <div>
                <p className="font-medium text-green-900 dark:text-green-100">
                  {uploadedPdf.fileName}
                </p>
                <p className="text-sm text-green-700 dark:text-green-300">
                  {formatFileSize(uploadedPdf.fileSize)} • ID: {uploadedPdf.id.slice(0, 8)}...
                </p>
              </div>
            </div>
            <Button
              variant="ghost"
              size="sm"
              onClick={handleRemove}
              disabled={disabled}
              className="text-green-700 hover:text-green-900 dark:text-green-300"
            >
              <X className="h-4 w-4" />
            </Button>
          </div>
        )}

        {/* Processing status (polling after upload) - Issue #5196 */}
        {processingDoc && (
          <div data-testid="pdf-processing-status">
            <p className="text-xs text-muted-foreground mb-1.5 font-medium uppercase tracking-wider">
              Stato elaborazione
            </p>
            <KbCardStatusRow document={processingDoc} />
          </div>
        )}

        {/* Drop Zone (only show if no uploaded PDF) */}
        {!uploadedPdf && (
          <>
            <div
              className={`border-2 border-dashed rounded-xl p-6 text-center transition-colors ${
                disabled
                  ? 'opacity-50 cursor-not-allowed'
                  : dragActive
                    ? 'border-primary bg-primary/5 cursor-pointer'
                    : file
                      ? 'border-blue-500 bg-blue-50 dark:bg-blue-900/20 cursor-pointer'
                      : 'border-muted-foreground/25 hover:border-primary/50 hover:bg-muted/50 cursor-pointer'
              }`}
              onDrop={!disabled ? handleDrop : undefined}
              onDragOver={!disabled ? handleDragOver : undefined}
              onDragLeave={!disabled ? handleDragLeave : undefined}
              onClick={() => !disabled && document.getElementById('admin-pdf-input')?.click()}
            >
              <input
                id="admin-pdf-input"
                type="file"
                accept="application/pdf"
                className="hidden"
                disabled={disabled}
                onChange={e => {
                  const selectedFile = e.target.files?.[0];
                  if (selectedFile) handleFileSelect(selectedFile);
                }}
              />

              {file ? (
                <div className="space-y-2">
                  <FileText className="h-10 w-10 mx-auto text-blue-600 dark:text-blue-400" />
                  <p className="font-medium">{file.name}</p>
                  <p className="text-sm text-muted-foreground">{formatFileSize(file.size)}</p>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={e => {
                      e.stopPropagation();
                      setFile(null);
                    }}
                  >
                    Cambia File
                  </Button>
                </div>
              ) : (
                <div className="space-y-2">
                  <FileUp className="h-10 w-10 mx-auto text-muted-foreground" />
                  <p className="font-medium">Trascina un PDF o clicca per selezionare</p>
                  <p className="text-sm text-muted-foreground">PDF fino a 50MB</p>
                </div>
              )}
            </div>

            {/* Progress Bar */}
            {uploading && (
              <div className="space-y-2">
                <div className="flex justify-between text-sm">
                  <span className="flex items-center gap-2">
                    <Loader2 className="h-4 w-4 animate-spin" />
                    Caricamento...
                  </span>
                  <span>{uploadProgress}%</span>
                </div>
                <Progress value={uploadProgress} className="h-2" />
              </div>
            )}

            {/* Error Display */}
            {error && (
              <div className="flex items-center gap-2 p-3 bg-destructive/10 border border-destructive/20 rounded-lg text-destructive text-sm">
                <AlertCircle className="h-4 w-4 flex-shrink-0" />
                <span>{error}</span>
              </div>
            )}

            {/* Upload Button */}
            {file && !uploading && (
              <Button onClick={handleUpload} disabled={disabled} className="w-full">
                <FileUp className="h-4 w-4 mr-2" />
                Carica PDF
              </Button>
            )}
          </>
        )}

        {/* Info Box */}
        <div className="p-3 bg-muted/50 border rounded-lg">
          <p className="text-sm text-muted-foreground">
            <strong>Info:</strong> Il PDF caricato sarà indicizzato automaticamente per la ricerca
            RAG. Questo processo può richiedere alcuni minuti.
          </p>
        </div>
      </CardContent>
    </Card>
  );
}
