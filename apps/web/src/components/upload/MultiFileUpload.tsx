/**
 * PDF-05: Multi-file upload component
 * Allows users to upload multiple PDF files concurrently with drag-and-drop support
 */

import { type DragEvent, type ChangeEvent, useCallback, useRef, useState } from 'react';
import { useUploadQueue } from '@/hooks/useUploadQueue';
import { UploadQueue } from './UploadQueue';
import { UploadSummary } from './UploadSummary';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';

interface MultiFileUploadProps {
  gameId: string;
  gameName: string;
  language: string; // Document language for OCR/parsing
  autoUpload?: boolean; // Auto-start uploads when files added (default: true)
  onUploadComplete?: () => void;
  // Test observability hooks
  onUploadStart?: (item: any) => void;
  onUploadSuccess?: (item: any) => void;
  onUploadError?: (item: any, error: string) => void;
  onQueueAdd?: (items: any[]) => void;
  onRetry?: (item: any, attempt: number, error: Error) => void;
}

const MAX_PDF_SIZE_BYTES = 104857600; // 100 MB
const ALLOWED_MIME_TYPES = ['application/pdf'];
const PDF_MAGIC_BYTES = '%PDF-';
const MAX_FILES_PER_BATCH = 20;

// Client-side PDF validation helper
async function validatePdfFile(file: File): Promise<string | null> {
  // MIME type check
  if (!ALLOWED_MIME_TYPES.includes(file.type)) {
    return `Invalid file type: ${file.name} (must be PDF)`;
  }

  // Size check
  if (file.size > MAX_PDF_SIZE_BYTES) {
    const sizeMB = (file.size / 1024 / 1024).toFixed(1);
    const maxMB = (MAX_PDF_SIZE_BYTES / 1024 / 1024).toFixed(0);
    return `File too large: ${file.name} (${sizeMB} MB exceeds ${maxMB} MB limit)`;
  }

  if (file.size === 0) {
    return `Empty file: ${file.name}`;
  }

  // Magic bytes check
  if (file.size >= PDF_MAGIC_BYTES.length) {
    try {
      const header = await readFileHeader(file, PDF_MAGIC_BYTES.length);
      if (header !== PDF_MAGIC_BYTES) {
        return `Invalid PDF format: ${file.name}`;
      }
    } catch (error) {
      return `Unable to read file: ${file.name}`;
    }
  }

  return null;
}

// Helper to read file header
async function readFileHeader(file: File, bytesToRead: number): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = (e) => {
      if (e.target?.result) {
        const bytes = new Uint8Array(e.target.result as ArrayBuffer);
        const header = String.fromCharCode(...Array.from(bytes));
        resolve(header);
      } else {
        reject(new Error('Failed to read file'));
      }
    };
    reader.onerror = () => reject(new Error('FileReader error'));
    reader.readAsArrayBuffer(file.slice(0, bytesToRead));
  });
}

export function MultiFileUpload({
  gameId,
  gameName,
  language,
  autoUpload = true,
  onUploadComplete,
  onUploadStart,
  onUploadSuccess,
  onUploadError,
  onQueueAdd,
  onRetry
}: MultiFileUploadProps) {
  const [isDragging, setIsDragging] = useState(false);
  const [validationErrors, setValidationErrors] = useState<string[]>([]);
  const [showSummary, setShowSummary] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const {
    queue,
    addFiles,
    removeFile,
    cancelUpload,
    retryUpload,
    clearCompleted,
    clearAll,
    getStats,
    startUpload,
    isWorkerReady,
    workerError
  } = useUploadQueue({
    onUploadComplete: () => {
      onUploadComplete?.();
    },
    onUploadError: (item, error) => {
      onUploadError?.(item, error);
    },
    onAllComplete: (stats) => {
      setShowSummary(true);
      if (stats.succeeded > 0) {
        onUploadComplete?.();
      }
    }
  });

  const stats = getStats();

  /**
   * Validates and adds files to the queue
   */
  const handleFilesSelected = useCallback(async (files: FileList | File[]) => {
    const fileArray = Array.from(files);

    // Check batch size limit
    if (fileArray.length > MAX_FILES_PER_BATCH) {
      setValidationErrors([`Too many files selected. Maximum ${MAX_FILES_PER_BATCH} files allowed per batch.`]);
      return;
    }

    setValidationErrors([]);
    const errors: string[] = [];
    const validFiles: File[] = [];

    // Validate each file
    for (const file of fileArray) {
      const error = await validatePdfFile(file);
      if (error) {
        errors.push(error);
      } else {
        validFiles.push(file);
      }
    }

    if (errors.length > 0) {
      setValidationErrors(errors);
    }

    if (validFiles.length > 0) {
      await addFiles(validFiles, gameId, language);
      setShowSummary(false);
    }
  }, [gameId, language, addFiles]);

  /**
   * Handle file input change
   */
  const handleFileInputChange = useCallback((event: ChangeEvent<HTMLInputElement>) => {
    const files = event.target.files;
    if (files && files.length > 0) {
      void handleFilesSelected(files);
    }
    // Reset input to allow selecting the same files again
    event.target.value = '';
  }, [handleFilesSelected]);

  /**
   * Handle drag enter
   */
  const handleDragEnter = useCallback((e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(true);
  }, []);

  /**
   * Handle drag leave
   */
  const handleDragLeave = useCallback((e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    // Only set dragging to false if leaving the drop zone entirely
    if (e.currentTarget === e.target) {
      setIsDragging(false);
    }
  }, []);

  /**
   * Handle drag over
   */
  const handleDragOver = useCallback((e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
  }, []);

  /**
   * Handle drop
   */
  const handleDrop = useCallback((e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragging(false);

    const files = e.dataTransfer.files;
    if (files && files.length > 0) {
      void handleFilesSelected(files);
    }
  }, [handleFilesSelected]);

  /**
   * Trigger file input click
   */
  const handleBrowseClick = useCallback(() => {
    fileInputRef.current?.click();
  }, []);

  return (
    <div data-testid="multi-file-upload" data-game-id={gameId} data-game-name={gameName} className="mt-6">
      <h3 className="mb-4 text-lg font-semibold">
        Multi-File Upload
      </h3>

      {/* Game Info */}
      <div
        data-testid="game-info-badge"
        className="p-3 mb-4"
      >
        <Badge variant="default" className="text-sm px-4 py-2">
          Target Game: {gameName} ({gameId})
        </Badge>
      </div>

      {/* Worker Error */}
      {workerError && (
        <div
          role="alert"
          className="p-3 bg-red-50 border border-red-600 rounded-md mb-4"
          data-testid="worker-error"
        >
          <div className="text-sm font-semibold text-red-600 mb-2">
            Upload System Error:
          </div>
          <p className="text-xs text-red-600 mb-2">
            {workerError.message}
          </p>
          <Button
            onClick={() => window.location.reload()}
            variant="outline"
            size="sm"
            className="text-red-600 border-red-600"
          >
            Reload Page
          </Button>
        </div>
      )}

      {/* Validation Errors */}
      {validationErrors.length > 0 && (
        <div
          role="alert"
          className="p-3 bg-red-50 border border-red-600 rounded-md mb-4"
        >
          <div className="text-sm font-semibold text-red-600 mb-2">
            Validation Errors:
          </div>
          <ul className="m-0 pl-5 text-xs text-red-600">
            {validationErrors.map((error, index) => (
              <li key={index}>{error}</li>
            ))}
          </ul>
        </div>
      )}

      {/* Drag and Drop Zone */}
      <div
        onDragEnter={handleDragEnter}
        onDragLeave={handleDragLeave}
        onDragOver={handleDragOver}
        onDrop={handleDrop}
        className="p-10 border-2 border-dashed rounded-lg text-center cursor-pointer transition-all mb-6"
        style={{
          borderColor: isDragging ? '#0070f3' : '#dadce0',
          backgroundColor: isDragging ? '#e3f2fd' : '#fafafa'
        }}
        onClick={handleBrowseClick}
        role="button"
        tabIndex={0}
        aria-label="Click to browse files or drag and drop PDFs here"
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            handleBrowseClick();
          }
        }}
      >
        <div
          className="text-5xl mb-4"
          style={{
            color: isDragging ? '#0070f3' : '#5f6368'
          }}
          aria-hidden="true"
        >
          📁
        </div>
        <div className="text-base font-semibold text-gray-900 mb-2">
          {isDragging ? 'Drop files here' : 'Drag and drop PDF files here'}
        </div>
        <div className="text-sm text-gray-600 mb-4">
          or click to browse (up to {MAX_FILES_PER_BATCH} files, max 100 MB each)
        </div>
        <Button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            handleBrowseClick();
          }}
        >
          Select Files
        </Button>
        <Input
          ref={fileInputRef}
          type="file"
          accept="application/pdf"
          multiple
          onChange={handleFileInputChange}
          className="hidden"
          aria-label="File input for PDF upload"
        />
      </div>

      {/* Manual Upload Button (shown when autoUpload disabled and files pending) */}
      {!autoUpload && stats.pending > 0 && (
        <div className="mb-4 text-center">
          <Button
            type="button"
            onClick={() => void startUpload()}
            data-testid="start-upload-button"
            size="lg"
            className="bg-green-600"
          >
            Start Upload ({stats.pending} file{stats.pending > 1 ? 's' : ''})
          </Button>
        </div>
      )}

      {/* Upload Summary (shown after all complete) */}
      {showSummary && (
        <UploadSummary
          stats={stats}
          onClose={() => setShowSummary(false)}
          onClearAll={clearAll}
        />
      )}

      {/* Upload Queue */}
      {queue.length > 0 && (
        <UploadQueue
          items={queue}
          stats={stats}
          onCancel={cancelUpload}
          onRetry={retryUpload}
          onRemove={removeFile}
          onClearCompleted={clearCompleted}
        />
      )}
    </div>
  );
}