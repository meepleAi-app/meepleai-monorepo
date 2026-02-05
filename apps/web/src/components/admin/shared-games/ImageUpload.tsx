'use client';

/**
 * ImageUpload Component (Issue #3384)
 *
 * Drag-and-drop image upload with preview.
 * Supports: JPG, PNG, WebP (max 5MB)
 * Features:
 * - Drag-and-drop zone
 * - Preview before upload
 * - URL fallback input
 * - Validation (type, size)
 */

import { type ChangeEvent, useCallback, useState, useRef, type DragEvent } from 'react';

import { AlertCircle, ImageIcon, Upload, X, Link as LinkIcon } from 'lucide-react';
import Image from 'next/image';

import { toast } from '@/components/layout/Toast';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Progress } from '@/components/ui/feedback/progress';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { cn } from '@/lib/utils';

// Validation constants
const MAX_IMAGE_SIZE_BYTES = 5 * 1024 * 1024; // 5 MB
const ALLOWED_MIME_TYPES = ['image/jpeg', 'image/png', 'image/webp'];
const ALLOWED_EXTENSIONS = ['.jpg', '.jpeg', '.png', '.webp'];

interface ImageUploadProps {
  /** Current image URL (for edit mode) */
  currentImageUrl?: string | null;
  /** Called when file is selected/uploaded */
  onFileChange: (file: File | null) => void;
  /** Called when URL input changes */
  onUrlChange?: (url: string) => void;
  /** Current URL value (for controlled input) */
  urlValue?: string;
  /** Whether the input is disabled */
  disabled?: boolean;
  /** Label for the upload field */
  label?: string;
  /** Image type hint for display */
  imageTypeHint?: 'icon' | 'cover';
  /** Upload progress (0-100) */
  uploadProgress?: number;
  /** Whether upload is in progress */
  isUploading?: boolean;
}

interface ValidationResult {
  isValid: boolean;
  errors: string[];
}

/**
 * Validates an image file before upload
 */
function validateImageFile(file: File): ValidationResult {
  const errors: string[] = [];

  // MIME type check
  if (!ALLOWED_MIME_TYPES.includes(file.type)) {
    errors.push(`Invalid file type: ${file.type || 'unknown'}. Allowed: JPG, PNG, WebP`);
  }

  // Extension check (fallback)
  const extension = file.name.toLowerCase().substring(file.name.lastIndexOf('.'));
  if (!ALLOWED_EXTENSIONS.includes(extension)) {
    errors.push(`Invalid file extension: ${extension}. Allowed: ${ALLOWED_EXTENSIONS.join(', ')}`);
  }

  // Size check
  if (file.size > MAX_IMAGE_SIZE_BYTES) {
    const sizeMB = (file.size / 1024 / 1024).toFixed(1);
    errors.push(`File size (${sizeMB} MB) exceeds maximum allowed (5 MB)`);
  }

  if (file.size === 0) {
    errors.push('File is empty (0 bytes)');
  }

  return {
    isValid: errors.length === 0,
    errors,
  };
}

/**
 * Formats file size for display
 */
function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 Bytes';
  const k = 1024;
  const sizes = ['Bytes', 'KB', 'MB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  const sizeLabel = sizes[i] ?? 'MB';
  return `${Math.round((bytes / Math.pow(k, i)) * 100) / 100} ${sizeLabel}`;
}

export function ImageUpload({
  currentImageUrl,
  onFileChange,
  onUrlChange,
  urlValue = '',
  disabled = false,
  label = 'Cover Image',
  imageTypeHint = 'cover',
  uploadProgress = 0,
  isUploading = false,
}: ImageUploadProps) {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [validationErrors, setValidationErrors] = useState<string[]>([]);
  const [isDragOver, setIsDragOver] = useState(false);
  const [mode, setMode] = useState<'file' | 'url'>('file');
  const inputRef = useRef<HTMLInputElement>(null);

  /**
   * Processes a file selection (from input or drop)
   */
  const processFile = useCallback(
    (selectedFile: File | null) => {
      if (!selectedFile) {
        setFile(null);
        setPreview(null);
        setValidationErrors([]);
        onFileChange(null);
        return;
      }

      const validation = validateImageFile(selectedFile);

      if (!validation.isValid) {
        setValidationErrors(validation.errors);
        setFile(null);
        setPreview(null);
        onFileChange(null);
        toast.error('Invalid file: ' + validation.errors[0]);
        return;
      }

      // Create preview URL
      const objectUrl = URL.createObjectURL(selectedFile);
      setPreview(objectUrl);
      setFile(selectedFile);
      setValidationErrors([]);
      onFileChange(selectedFile);
    },
    [onFileChange]
  );

  /**
   * Handles file input change
   */
  const handleFileChange = useCallback(
    (e: ChangeEvent<HTMLInputElement>) => {
      const selectedFile = e.target.files?.[0] ?? null;
      processFile(selectedFile);
    },
    [processFile]
  );

  /**
   * Handles drag over event
   */
  const handleDragOver = useCallback((e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragOver(true);
  }, []);

  /**
   * Handles drag leave event
   */
  const handleDragLeave = useCallback((e: DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    e.stopPropagation();
    setIsDragOver(false);
  }, []);

  /**
   * Handles drop event
   */
  const handleDrop = useCallback(
    (e: DragEvent<HTMLDivElement>) => {
      e.preventDefault();
      e.stopPropagation();
      setIsDragOver(false);

      if (disabled) return;

      const droppedFile = e.dataTransfer.files?.[0];
      if (droppedFile) {
        processFile(droppedFile);
      }
    },
    [disabled, processFile]
  );

  /**
   * Opens file picker
   */
  const handleClick = useCallback(() => {
    if (!disabled && inputRef.current) {
      inputRef.current.click();
    }
  }, [disabled]);

  /**
   * Clears the current file
   */
  const handleClear = useCallback(() => {
    setFile(null);
    setPreview(null);
    setValidationErrors([]);
    onFileChange(null);
    if (inputRef.current) {
      inputRef.current.value = '';
    }
  }, [onFileChange]);

  /**
   * Handles URL input change
   */
  const handleUrlInputChange = useCallback(
    (e: ChangeEvent<HTMLInputElement>) => {
      onUrlChange?.(e.target.value);
    },
    [onUrlChange]
  );

  const hasErrors = validationErrors.length > 0;
  const showPreview = preview || (currentImageUrl && !file);
  const previewSrc = preview || currentImageUrl || '';
  const recommendedSize = imageTypeHint === 'icon' ? '128×128 or 256×256' : '800×600 or larger';

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <Label>{label}</Label>
        <div className="flex gap-1">
          <Button
            type="button"
            variant={mode === 'file' ? 'default' : 'ghost'}
            size="sm"
            onClick={() => setMode('file')}
            disabled={disabled}
          >
            <Upload className="h-3 w-3 mr-1" />
            File
          </Button>
          <Button
            type="button"
            variant={mode === 'url' ? 'default' : 'ghost'}
            size="sm"
            onClick={() => setMode('url')}
            disabled={disabled}
          >
            <LinkIcon className="h-3 w-3 mr-1" />
            URL
          </Button>
        </div>
      </div>

      {mode === 'file' ? (
        <>
          {/* Drag-and-drop zone */}
          <div
            role="button"
            tabIndex={disabled ? -1 : 0}
            aria-label="Upload image"
            className={cn(
              'relative flex flex-col items-center justify-center',
              'border-2 border-dashed rounded-lg p-6',
              'transition-colors duration-200',
              'cursor-pointer focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2',
              isDragOver && !disabled && 'border-primary bg-primary/5',
              hasErrors && 'border-destructive',
              !isDragOver && !hasErrors && 'border-muted-foreground/25 hover:border-muted-foreground/50',
              disabled && 'opacity-50 cursor-not-allowed'
            )}
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onDrop={handleDrop}
            onClick={handleClick}
            onKeyDown={(e) => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                handleClick();
              }
            }}
          >
            <input
              ref={inputRef}
              type="file"
              accept={ALLOWED_MIME_TYPES.join(',')}
              onChange={handleFileChange}
              disabled={disabled}
              className="sr-only"
              aria-describedby="image-upload-hint"
            />

            {showPreview ? (
              <div className="relative w-full max-w-[200px] aspect-square">
                <Image
                  src={previewSrc}
                  alt="Preview"
                  fill
                  className="object-contain rounded-md"
                  unoptimized
                />
              </div>
            ) : (
              <>
                <ImageIcon className="h-10 w-10 text-muted-foreground mb-2" />
                <p className="text-sm font-medium text-foreground">
                  {isDragOver ? 'Drop image here' : 'Drop image or click to upload'}
                </p>
              </>
            )}

            <p
              id="image-upload-hint"
              className="text-xs text-muted-foreground mt-2 text-center"
            >
              JPG, PNG, WebP • Max 5MB • Recommended: {recommendedSize}
            </p>
          </div>

          {/* File info and clear button */}
          {file && (
            <div className="flex items-center justify-between p-2 bg-muted/50 rounded-md">
              <div className="flex items-center gap-2 text-sm">
                <span className="font-medium truncate max-w-[200px]">{file.name}</span>
                <span className="text-muted-foreground">({formatFileSize(file.size)})</span>
              </div>
              <Button
                type="button"
                variant="ghost"
                size="icon"
                onClick={(e) => {
                  e.stopPropagation();
                  handleClear();
                }}
                disabled={disabled || isUploading}
              >
                <X className="h-4 w-4" />
                <span className="sr-only">Clear file</span>
              </Button>
            </div>
          )}

          {/* Upload progress */}
          {isUploading && (
            <div className="space-y-1">
              <Progress value={uploadProgress} className="h-2" />
              <p className="text-xs text-muted-foreground text-center">
                Uploading... {uploadProgress}%
              </p>
            </div>
          )}

          {/* Validation errors */}
          {hasErrors && (
            <Alert variant="destructive">
              <AlertCircle className="h-4 w-4" />
              <AlertDescription>
                <ul className="list-disc pl-5 space-y-1">
                  {validationErrors.map((error, i) => (
                    <li key={i}>{error}</li>
                  ))}
                </ul>
              </AlertDescription>
            </Alert>
          )}
        </>
      ) : (
        /* URL input mode */
        <div className="space-y-2">
          <Input
            type="url"
            placeholder="https://example.com/image.jpg"
            value={urlValue}
            onChange={handleUrlInputChange}
            disabled={disabled}
          />
          <p className="text-xs text-muted-foreground">
            Enter a direct URL to an image. The image will be fetched and stored.
          </p>

          {/* URL preview */}
          {urlValue && (
            <div className="relative w-full max-w-[200px] aspect-square border rounded-md overflow-hidden">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={urlValue}
                alt="URL Preview"
                className="w-full h-full object-contain"
                onError={(e) => {
                  (e.target as HTMLImageElement).style.display = 'none';
                }}
              />
            </div>
          )}
        </div>
      )}
    </div>
  );
}
