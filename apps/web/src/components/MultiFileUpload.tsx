/**
 * PDF-05: Multi-file upload component
 * Allows users to upload multiple PDF files concurrently with drag-and-drop support
 */

import { type DragEvent, type ChangeEvent, useCallback, useRef, useState } from 'react';
import { useUploadQueue } from '../hooks/useUploadQueue';
import { UploadQueue } from './UploadQueue';
import { UploadSummary } from './UploadSummary';

interface MultiFileUploadProps {
  gameId: string;
  gameName: string;
  onUploadComplete?: () => void;
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
  try {
    const header = await readFileHeader(file, 5);
    if (header !== PDF_MAGIC_BYTES) {
      return `Invalid PDF format: ${file.name}`;
    }
  } catch (error) {
    return `Unable to read file: ${file.name}`;
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

export function MultiFileUpload({ gameId, gameName, onUploadComplete }: MultiFileUploadProps) {
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
    getStats
  } = useUploadQueue({
    concurrencyLimit: 3,
    maxRetries: 3,
    onUploadComplete: () => {
      onUploadComplete?.();
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
      addFiles(validFiles, gameId);
      setShowSummary(false);
    }
  }, [gameId, addFiles]);

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
    <div style={{ marginTop: '24px' }}>
      <h3 style={{ marginBottom: '16px', fontSize: '18px', fontWeight: 600 }}>
        Multi-File Upload
      </h3>

      {/* Game Info */}
      <div
        style={{
          padding: '12px 16px',
          backgroundColor: '#e8f5e9',
          border: '1px solid #34a853',
          borderRadius: '6px',
          marginBottom: '16px',
          fontSize: '14px',
          color: '#1e7e34'
        }}
      >
        <strong>Target Game:</strong> {gameName} ({gameId})
      </div>

      {/* Validation Errors */}
      {validationErrors.length > 0 && (
        <div
          role="alert"
          style={{
            padding: '12px 16px',
            backgroundColor: '#ffebee',
            border: '1px solid #d93025',
            borderRadius: '6px',
            marginBottom: '16px'
          }}
        >
          <div style={{ fontSize: '14px', fontWeight: 600, color: '#d93025', marginBottom: '8px' }}>
            Validation Errors:
          </div>
          <ul style={{ margin: 0, paddingLeft: '20px', fontSize: '13px', color: '#d93025' }}>
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
        style={{
          padding: '40px',
          border: `2px dashed ${isDragging ? '#0070f3' : '#dadce0'}`,
          borderRadius: '8px',
          backgroundColor: isDragging ? '#e3f2fd' : '#fafafa',
          textAlign: 'center',
          cursor: 'pointer',
          transition: 'all 0.2s ease',
          marginBottom: '24px'
        }}
        onClick={handleBrowseClick}
        role="button"
        tabIndex={0}
        aria-label="Click to browse files or drag and drop PDF files here"
        onKeyDown={(e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            handleBrowseClick();
          }
        }}
      >
        <div
          style={{
            fontSize: '48px',
            marginBottom: '16px',
            color: isDragging ? '#0070f3' : '#5f6368'
          }}
          aria-hidden="true"
        >
          📁
        </div>
        <div style={{ fontSize: '16px', fontWeight: 600, color: '#202124', marginBottom: '8px' }}>
          {isDragging ? 'Drop files here' : 'Drag and drop PDF files here'}
        </div>
        <div style={{ fontSize: '14px', color: '#5f6368', marginBottom: '16px' }}>
          or click to browse (up to {MAX_FILES_PER_BATCH} files, max 100 MB each)
        </div>
        <button
          type="button"
          onClick={(e) => {
            e.stopPropagation();
            handleBrowseClick();
          }}
          style={{
            padding: '10px 24px',
            fontSize: '14px',
            fontWeight: 500,
            color: 'white',
            backgroundColor: '#0070f3',
            border: 'none',
            borderRadius: '4px',
            cursor: 'pointer',
            transition: 'background-color 0.2s ease'
          }}
          onMouseEnter={(e) => {
            e.currentTarget.style.backgroundColor = '#0051cc';
          }}
          onMouseLeave={(e) => {
            e.currentTarget.style.backgroundColor = '#0070f3';
          }}
        >
          Select Files
        </button>
        <input
          ref={fileInputRef}
          type="file"
          accept="application/pdf"
          multiple
          onChange={handleFileInputChange}
          style={{ display: 'none' }}
          aria-label="File input for PDF upload"
        />
      </div>

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
