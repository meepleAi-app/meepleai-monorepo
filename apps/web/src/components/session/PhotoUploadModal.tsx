'use client';

/**
 * PhotoUploadModal - Upload photos during a live game session.
 * Issue #5368 - PhotoUploadModal frontend component.
 * Epic #5358 - Session Photo Attachments (Phase 0).
 */

import { useState, useCallback, useRef, useEffect } from 'react';

import { Camera, Upload, X, AlertCircle, ImageIcon } from 'lucide-react';

import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { cn } from '@/lib/utils';

/** Matches backend AttachmentType enum values. */
export type AttachmentType =
  | 'PlayerArea'
  | 'BoardState'
  | 'CharacterSheet'
  | 'ResourceInventory'
  | 'Custom';

const ATTACHMENT_TYPE_LABELS: Record<AttachmentType, string> = {
  PlayerArea: 'Player Area',
  BoardState: 'Board State',
  CharacterSheet: 'Character Sheet',
  ResourceInventory: 'Resource Inventory',
  Custom: 'Custom',
};

/** DTO returned by the upload API. */
export interface SessionAttachmentDto {
  id: string;
  sessionId: string;
  playerId: string;
  attachmentType: AttachmentType;
  blobUrl: string;
  thumbnailUrl: string | null;
  caption: string | null;
  contentType: string;
  fileSizeBytes: number;
  snapshotIndex: number | null;
  createdAt: string;
}

export interface PhotoUploadModalProps {
  sessionId: string;
  playerId: string;
  snapshotIndex?: number;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onUploadComplete: (attachment: SessionAttachmentDto) => void;
}

const MAX_FILE_SIZE = 10 * 1024 * 1024; // 10MB
const ACCEPTED_TYPES = ['image/jpeg', 'image/png'];
const MAX_CAPTION_LENGTH = 200;

export function PhotoUploadModal({
  sessionId,
  playerId,
  snapshotIndex,
  open,
  onOpenChange,
  onUploadComplete,
}: PhotoUploadModalProps) {
  const [file, setFile] = useState<File | null>(null);
  const [preview, setPreview] = useState<string | null>(null);
  const [attachmentType, setAttachmentType] = useState<AttachmentType>('BoardState');
  const [caption, setCaption] = useState('');
  const [uploading, setUploading] = useState(false);
  const [uploadProgress, setUploadProgress] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [dragActive, setDragActive] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  // Reset state when modal opens
  useEffect(() => {
    if (open) {
      setFile(null);
      setPreview(null);
      setAttachmentType('BoardState');
      setCaption('');
      setUploading(false);
      setUploadProgress(0);
      setError(null);
      setDragActive(false);
    }
  }, [open]);

  // Clean up preview URL on unmount or file change
  useEffect(() => {
    return () => {
      if (preview) URL.revokeObjectURL(preview);
    };
  }, [preview]);

  const validateFile = useCallback((f: File): string | null => {
    if (!ACCEPTED_TYPES.includes(f.type)) {
      return 'Only JPEG and PNG images are accepted.';
    }
    if (f.size > MAX_FILE_SIZE) {
      return 'File must be 10 MB or smaller.';
    }
    return null;
  }, []);

  const handleFileSelect = useCallback(
    (selectedFile: File) => {
      const validationError = validateFile(selectedFile);
      if (validationError) {
        setError(validationError);
        return;
      }
      setError(null);
      setFile(selectedFile);
      if (preview) URL.revokeObjectURL(preview);
      setPreview(URL.createObjectURL(selectedFile));
    },
    [validateFile, preview]
  );

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      e.stopPropagation();
      setDragActive(false);
      const droppedFile = e.dataTransfer.files[0];
      if (droppedFile) handleFileSelect(droppedFile);
    },
    [handleFileSelect]
  );

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(true);
  }, []);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(false);
  }, []);

  const handleClearFile = useCallback(() => {
    setFile(null);
    if (preview) URL.revokeObjectURL(preview);
    setPreview(null);
    setError(null);
    setUploadProgress(0);
    if (inputRef.current) inputRef.current.value = '';
  }, [preview]);

  const handleUpload = useCallback(async () => {
    if (!file) return;
    setUploading(true);
    setUploadProgress(0);
    setError(null);

    try {
      const formData = new FormData();
      formData.append('file', file);
      formData.append('attachmentType', attachmentType);
      formData.append('playerId', playerId);
      if (caption.trim()) formData.append('caption', caption.trim());
      if (snapshotIndex != null) formData.append('snapshotIndex', String(snapshotIndex));

      const result = await new Promise<SessionAttachmentDto>((resolve, reject) => {
        const xhr = new XMLHttpRequest();
        xhr.open('POST', `/api/v1/live-sessions/${sessionId}/attachments`);

        xhr.upload.addEventListener('progress', e => {
          if (e.lengthComputable) {
            setUploadProgress(Math.round((e.loaded / e.total) * 100));
          }
        });

        xhr.addEventListener('load', () => {
          if (xhr.status === 201) {
            try {
              resolve(JSON.parse(xhr.responseText) as SessionAttachmentDto);
            } catch {
              reject(new Error('Invalid server response.'));
            }
          } else {
            let message = 'Upload failed.';
            try {
              const body = JSON.parse(xhr.responseText) as { error?: string };
              if (body.error) message = body.error;
            } catch {
              // use default message
            }
            reject(new Error(message));
          }
        });

        xhr.addEventListener('error', () => reject(new Error('Network error during upload.')));
        xhr.addEventListener('abort', () => reject(new Error('Upload was cancelled.')));

        xhr.send(formData);
      });

      onUploadComplete(result);
      onOpenChange(false);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Upload failed.');
    } finally {
      setUploading(false);
    }
  }, [
    file,
    attachmentType,
    playerId,
    caption,
    snapshotIndex,
    sessionId,
    onUploadComplete,
    onOpenChange,
  ]);

  const formatSize = (bytes: number) => `${(bytes / (1024 * 1024)).toFixed(1)} MB`;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-md" data-testid="photo-upload-modal">
        <DialogHeader>
          <DialogTitle className="flex items-center gap-2">
            <Camera className="h-5 w-5" aria-hidden="true" />
            Upload Photo
          </DialogTitle>
          <DialogDescription>
            Capture or select a photo to attach to this session.
          </DialogDescription>
        </DialogHeader>

        <div className="space-y-4">
          {/* Drop zone / file picker */}
          <div
            className={cn(
              'relative rounded-xl border-2 border-dashed p-4 text-center transition-colors cursor-pointer',
              dragActive && 'border-amber-400 bg-amber-400/5',
              file && !dragActive && 'border-amber-500/50 bg-amber-500/5',
              !file &&
                !dragActive &&
                'border-border hover:border-muted-foreground/50 hover:bg-muted/30',
              uploading && 'pointer-events-none opacity-60'
            )}
            onDrop={handleDrop}
            onDragOver={handleDragOver}
            onDragLeave={handleDragLeave}
            onClick={() => !uploading && inputRef.current?.click()}
            data-testid="photo-drop-area"
            role="button"
            aria-label="Select a photo to upload"
            tabIndex={0}
            onKeyDown={e => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault();
                inputRef.current?.click();
              }
            }}
          >
            <input
              ref={inputRef}
              type="file"
              accept="image/jpeg,image/png"
              capture="environment"
              className="hidden"
              onChange={e => {
                const selected = e.target.files?.[0];
                if (selected) handleFileSelect(selected);
              }}
              data-testid="photo-file-input"
            />

            {preview && file ? (
              <div className="space-y-2">
                <div className="relative mx-auto max-h-48 overflow-hidden rounded-lg">
                  {/* eslint-disable-next-line @next/next/no-img-element */}
                  <img
                    src={preview}
                    alt="Preview"
                    className="mx-auto max-h-48 rounded-lg object-contain"
                    data-testid="photo-preview"
                  />
                </div>
                <div className="flex items-center justify-center gap-2 text-sm text-muted-foreground">
                  <ImageIcon className="h-4 w-4" aria-hidden="true" />
                  <span>{file.name}</span>
                  <span>({formatSize(file.size)})</span>
                  {!uploading && (
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-5 w-5"
                      onClick={e => {
                        e.stopPropagation();
                        handleClearFile();
                      }}
                      aria-label="Remove file"
                    >
                      <X className="h-3 w-3" />
                    </Button>
                  )}
                </div>
              </div>
            ) : (
              <div className="space-y-2 py-4">
                <Camera className="mx-auto h-10 w-10 text-muted-foreground" aria-hidden="true" />
                <p className="text-sm font-medium">Tap to take a photo or select from gallery</p>
                <p className="text-xs text-muted-foreground">JPEG or PNG, max 10 MB</p>
              </div>
            )}
          </div>

          {/* Attachment type selector */}
          <div className="space-y-1.5">
            <label htmlFor="attachment-type" className="text-sm font-medium">
              Photo Type
            </label>
            <select
              id="attachment-type"
              value={attachmentType}
              onChange={e => setAttachmentType(e.target.value as AttachmentType)}
              disabled={uploading}
              className={cn(
                'flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm',
                'transition-colors placeholder:text-muted-foreground',
                'focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring',
                'disabled:cursor-not-allowed disabled:opacity-50'
              )}
              data-testid="attachment-type-select"
            >
              {(Object.entries(ATTACHMENT_TYPE_LABELS) as [AttachmentType, string][]).map(
                ([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                )
              )}
            </select>
          </div>

          {/* Caption input */}
          <div className="space-y-1.5">
            <label htmlFor="photo-caption" className="text-sm font-medium">
              Caption <span className="text-muted-foreground font-normal">(optional)</span>
            </label>
            <input
              id="photo-caption"
              type="text"
              value={caption}
              onChange={e => setCaption(e.target.value.slice(0, MAX_CAPTION_LENGTH))}
              placeholder="Describe this photo..."
              disabled={uploading}
              maxLength={MAX_CAPTION_LENGTH}
              className={cn(
                'flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm',
                'transition-colors placeholder:text-muted-foreground',
                'focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring',
                'disabled:cursor-not-allowed disabled:opacity-50'
              )}
              data-testid="photo-caption-input"
            />
            <p className="text-xs text-muted-foreground text-right">
              {caption.length}/{MAX_CAPTION_LENGTH}
            </p>
          </div>

          {/* Progress bar */}
          {uploading && (
            <div className="space-y-1.5" data-testid="upload-progress">
              <div className="flex justify-between text-xs text-muted-foreground">
                <span>Uploading...</span>
                <span>{uploadProgress}%</span>
              </div>
              <div className="h-1.5 overflow-hidden rounded-full bg-muted">
                <div
                  className="h-full rounded-full bg-amber-500 transition-all duration-300"
                  style={{ width: `${uploadProgress}%` }}
                  role="progressbar"
                  aria-valuenow={uploadProgress}
                  aria-valuemin={0}
                  aria-valuemax={100}
                />
              </div>
            </div>
          )}

          {/* Error message */}
          {error && (
            <div
              className="flex items-center gap-2 rounded-lg border border-destructive/30 bg-destructive/10 px-3 py-2 text-xs text-destructive"
              role="alert"
              data-testid="upload-error"
            >
              <AlertCircle className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
              {error}
            </div>
          )}
        </div>

        <DialogFooter>
          <Button
            variant="outline"
            onClick={() => onOpenChange(false)}
            disabled={uploading}
            data-testid="cancel-button"
          >
            Cancel
          </Button>
          <Button
            onClick={() => void handleUpload()}
            disabled={!file || uploading}
            className="bg-amber-600 hover:bg-amber-700 text-white"
            data-testid="upload-button"
          >
            <Upload className="mr-1.5 h-4 w-4" aria-hidden="true" />
            {uploading ? 'Uploading...' : 'Upload Photo'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
