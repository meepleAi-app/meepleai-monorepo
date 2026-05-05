/**
 * Gamebook — PhotoUploader component (Sprint 1, Task 1.8)
 *
 * Main upload UI for the Libro Game AI Assistant Sprint 1.
 *
 * Features:
 *  - Multi-file dropzone (JPG/PNG/WEBP)
 *  - useMutation upload to POST /api/v1/gamebook/{gameId}/photos
 *  - useQuery polling for status (stops when terminal)
 *  - ConfidenceBadge displaying SmolDocling score
 *  - i18n via useTranslation (react-intl)
 *  - G6: capture="environment" for mobile camera direct access
 *  - G1 compromise: MAX_PHOTOS=5 limit + oversize warning
 *  - G8: "Inizia chat" button post-completion
 */

'use client';

import { useCallback, useRef, useState, type JSX } from 'react';

import Link from 'next/link';

import { useTranslation } from '@/hooks/useTranslation';
import { usePhotoBatchStatus } from '@/lib/gamebook/hooks/usePhotoBatchStatus';
import { usePhotoBatchUpload } from '@/lib/gamebook/hooks/usePhotoBatchUpload';
import { batchProgressPercent } from '@/lib/gamebook/schemas';

import { ConfidenceBadge } from './ConfidenceBadge';

export interface PhotoUploaderProps {
  gameId: string;
}

const ACCEPTED_MIME_TYPES = ['image/jpeg', 'image/png', 'image/webp'];
const MAX_FILE_SIZE_BYTES = 20 * 1024 * 1024; // 20 MB
// G1 compromise: limit to 5 photos per batch for smartphone compatibility
const MAX_PHOTOS = 5;
// G1: warn when any file exceeds 1 MB (not a hard block, just a warning)
const OVERSIZE_WARN_BYTES = 1_000_000; // 1 MB

/**
 * Photo upload and status UI for game-manual pages.
 *
 * @example
 * <PhotoUploader gameId="3fa85f64-5717-4562-b3fc-2c963f66afa6" />
 */
export function PhotoUploader({ gameId }: PhotoUploaderProps): JSX.Element {
  const { t } = useTranslation();
  const inputRef = useRef<HTMLInputElement>(null);

  const [files, setFiles] = useState<File[]>([]);
  const [batchId, setBatchId] = useState<string | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);
  const [oversizeWarning, setOversizeWarning] = useState<string | null>(null);
  const [isDragging, setIsDragging] = useState(false);

  // ── Mutation ────────────────────────────────────────────────────────────────
  const {
    mutate,
    isPending,
    isError,
    error: mutationError,
  } = usePhotoBatchUpload({
    onSuccess: ({ batchId: id }) => {
      setBatchId(id);
      setFiles([]);
    },
  });

  // ── Polling ─────────────────────────────────────────────────────────────────
  const { data: batchStatus } = usePhotoBatchStatus({ gameId, batchId });

  // ── File validation ──────────────────────────────────────────────────────────
  const validateFiles = useCallback((incoming: File[]): File[] => {
    const valid: File[] = [];
    for (const file of incoming) {
      if (!ACCEPTED_MIME_TYPES.includes(file.type)) continue;
      if (file.size > MAX_FILE_SIZE_BYTES) continue;
      valid.push(file);
    }
    return valid;
  }, []);

  const handleFilesSelected = useCallback(
    (incoming: File[]) => {
      const valid = validateFiles(incoming);
      if (valid.length === 0 && incoming.length > 0) {
        setValidationError(t('gamebook.upload.uploadError', 'Invalid files'));
        return;
      }
      // G1 compromise: enforce MAX_PHOTOS limit
      if (valid.length > MAX_PHOTOS) {
        setValidationError(
          t('gamebook.upload.tooManyFiles', {
            max: MAX_PHOTOS,
            selected: valid.length,
          })
        );
        return;
      }
      setValidationError(null);
      // G1: warn about oversize files (soft warning, not a block)
      const oversize = valid.filter(f => f.size > OVERSIZE_WARN_BYTES);
      if (oversize.length > 0) {
        setOversizeWarning(t('gamebook.upload.oversizeWarning', { count: oversize.length }));
      } else {
        setOversizeWarning(null);
      }
      setFiles(prev => {
        const merged = [...prev, ...valid.filter(f => !new Set(prev.map(p => p.name)).has(f.name))];
        // Enforce MAX_PHOTOS after merge too
        if (merged.length > MAX_PHOTOS) {
          setValidationError(
            t('gamebook.upload.tooManyFiles', {
              max: MAX_PHOTOS,
              selected: merged.length,
            })
          );
          return prev;
        }
        return merged;
      });
    },
    [validateFiles, t]
  );

  // ── Input / Drag handlers ────────────────────────────────────────────────────
  const handleInputChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const selected = Array.from(e.target.files ?? []);
      handleFilesSelected(selected);
      // Reset input so re-selecting same file fires change event
      if (inputRef.current) inputRef.current.value = '';
    },
    [handleFilesSelected]
  );

  const handleDrop = useCallback(
    (e: React.DragEvent<HTMLDivElement>) => {
      e.preventDefault();
      setIsDragging(false);
      const dropped = Array.from(e.dataTransfer.files);
      handleFilesSelected(dropped);
    },
    [handleFilesSelected]
  );

  const handleDragOver = useCallback((e: React.DragEvent<HTMLDivElement>) => {
    e.preventDefault();
    setIsDragging(true);
  }, []);

  const handleDragLeave = useCallback(() => {
    setIsDragging(false);
  }, []);

  const handleRemoveFile = useCallback((name: string) => {
    setFiles(prev => prev.filter(f => f.name !== name));
  }, []);

  // ── Submit ───────────────────────────────────────────────────────────────────
  const handleSubmit = useCallback(() => {
    if (files.length === 0 || isPending) return;
    mutate({ gameId, files });
  }, [files, gameId, isPending, mutate]);

  // ── Status label helper ──────────────────────────────────────────────────────
  const statusLabel = (() => {
    if (!batchStatus) return null;
    const map: Record<string, string> = {
      Pending: t('gamebook.upload.statusPending', 'Waiting to start...'),
      Processing: t('gamebook.upload.statusProcessing', 'Processing pages...'),
      Completed: t('gamebook.upload.statusCompleted', 'Processing complete'),
      Failed: t('gamebook.upload.statusFailed', 'Processing failed'),
      Cancelled: t('gamebook.upload.statusCancelled', 'Processing cancelled'),
    };
    return map[batchStatus.status] ?? batchStatus.status;
  })();

  return (
    <div className="space-y-6" data-testid="photo-uploader">
      {/* Dropzone */}
      <div
        role="region"
        aria-label={t('gamebook.upload.dropzoneLabel', 'Drop photos here or click to select')}
        onDrop={handleDrop}
        onDragOver={handleDragOver}
        onDragLeave={handleDragLeave}
        onClick={() => inputRef.current?.click()}
        className={`flex flex-col items-center justify-center rounded-lg border-2 border-dashed p-10 cursor-pointer transition-colors ${
          isDragging
            ? 'border-primary bg-primary/10'
            : 'border-muted-foreground/40 hover:border-primary/70'
        }`}
      >
        <input
          ref={inputRef}
          type="file"
          multiple
          accept={ACCEPTED_MIME_TYPES.join(',')}
          capture="environment"
          className="sr-only"
          aria-label={t('gamebook.upload.dropzoneLabel', 'Drop photos here or click to select')}
          onChange={handleInputChange}
          data-testid="file-input"
        />
        <p className="text-sm font-medium text-foreground">
          {t('gamebook.upload.dropzoneLabel', 'Drop photos here or click to select')}
        </p>
        <p className="mt-1 text-xs text-muted-foreground">
          {t('gamebook.upload.dropzoneHint', 'JPG, PNG, WEBP — up to 20 MB each')}
        </p>
        <p className="mt-1 text-xs text-muted-foreground" data-testid="photo-limit-hint">
          {t('gamebook.upload.photoLimitHint', { max: MAX_PHOTOS })}
        </p>
      </div>

      {/* Validation error */}
      {validationError && (
        <p role="alert" data-testid="validation-error" className="text-sm text-destructive">
          {validationError}
        </p>
      )}

      {/* G1: oversize warning (soft, non-blocking) */}
      {oversizeWarning && !validationError && (
        <p role="note" data-testid="oversize-warning" className="text-sm text-amber-600">
          {oversizeWarning}
        </p>
      )}

      {/* Selected file list */}
      {files.length > 0 && (
        <div data-testid="selected-files">
          <p className="mb-2 text-sm font-medium text-muted-foreground">
            {t('gamebook.upload.selectedFiles', { count: files.length })}
          </p>
          <ul className="space-y-1">
            {files.map(file => (
              <li
                key={file.name}
                className="flex items-center justify-between rounded-md bg-muted px-3 py-2 text-sm"
              >
                <span className="truncate">{file.name}</span>
                <button
                  type="button"
                  onClick={e => {
                    e.stopPropagation();
                    handleRemoveFile(file.name);
                  }}
                  aria-label={`Remove ${file.name}`}
                  className="ml-2 text-muted-foreground hover:text-destructive"
                >
                  ×
                </button>
              </li>
            ))}
          </ul>
        </div>
      )}

      {/* Upload button */}
      <button
        type="button"
        onClick={handleSubmit}
        disabled={files.length === 0 || isPending}
        data-testid="upload-button"
        className="w-full rounded-md bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90 disabled:opacity-50 disabled:cursor-not-allowed"
      >
        {isPending
          ? t('gamebook.upload.submitting', 'Uploading...')
          : t('gamebook.upload.submitButton', { count: files.length })}
      </button>

      {/* Mutation error */}
      {isError && (
        <p role="alert" className="text-sm text-destructive">
          {mutationError?.message ??
            t('gamebook.upload.uploadError', 'Upload failed. Please try again.')}
        </p>
      )}

      {/* Status panel */}
      {batchStatus && (
        <div
          data-testid="batch-status"
          className="rounded-lg border bg-card p-4 space-y-3"
          aria-live="polite"
        >
          <h2 className="text-sm font-semibold">
            {t('gamebook.upload.statusTitle', 'Processing status')}
          </h2>

          <div className="flex items-center justify-between text-sm">
            <span>{statusLabel}</span>
            <span className="text-muted-foreground">
              {t('gamebook.upload.progress', {
                processed: batchStatus.processedPages,
                total: batchStatus.totalPages,
              })}
            </span>
          </div>

          {/* Progress bar */}
          <div className="h-2 w-full overflow-hidden rounded-full bg-muted">
            <div
              role="progressbar"
              aria-valuenow={batchProgressPercent(batchStatus)}
              aria-valuemin={0}
              aria-valuemax={100}
              className="h-full bg-primary transition-all"
              style={{ width: `${batchProgressPercent(batchStatus)}%` }}
            />
          </div>

          {/* Confidence */}
          <div className="flex items-center gap-2 text-sm">
            <span className="text-muted-foreground">
              {t('gamebook.upload.confidence', 'Average confidence')}
            </span>
            <ConfidenceBadge confidence={batchStatus.averageConfidence} />
          </div>

          {/* Error message */}
          {batchStatus.errorMessage && (
            <p className="text-sm text-destructive">
              {t('gamebook.upload.errorMessage', { message: batchStatus.errorMessage })}
            </p>
          )}
        </div>
      )}

      {/* G8: Chat-to-game link after indexing completes */}
      {batchStatus?.status === 'Completed' && (
        <div
          data-testid="chat-link-panel"
          className="rounded-lg border border-green-200 bg-green-50 p-4 space-y-2"
        >
          <p className="text-sm font-semibold text-green-800">
            {t('gamebook.upload.indexingComplete', 'Indexing complete!')}
          </p>
          <p className="text-xs text-green-700">
            {t(
              'gamebook.upload.indexingCompleteHint',
              'You can now ask questions about the manual.'
            )}
          </p>
          <Link
            href={`/chat?gameId=${gameId}`}
            data-testid="start-chat-link"
            className="inline-block rounded-md bg-primary px-4 py-2 text-sm font-semibold text-primary-foreground hover:bg-primary/90"
          >
            {t('gamebook.upload.startChat', 'Start chat')}
          </Link>
        </div>
      )}
    </div>
  );
}
