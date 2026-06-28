'use client';

/**
 * EndgamePhotoUploadSection — SP4 #2501
 *
 * Inline photo upload strip shown inside EndgameDialog after the game ends.
 * Supports per-photo state machine (idle | uploading | done | error) with retry,
 * HEIC→JPEG conversion, and a hard cap of MAX_FILES files.
 *
 * Design constraints:
 *  - Uses semantic tokens only (no hardcoded Tailwind color utilities)
 *  - recordId may be null while the PlayRecord is being created (polling window)
 *  - Hook must always be called (React hook rules) → use recordId ?? ''
 *  - Upload button is disabled while recordId === null
 */

import { useState, useCallback, useEffect, useRef } from 'react';

import { usePlayRecordPhotoUpload } from '@/hooks/mutations/usePlayRecordPhotoUpload';
import { useTranslation } from '@/hooks/useTranslation';
import { cn } from '@/lib/utils';

// ─── Constants ────────────────────────────────────────────────────────────────

const MAX_BYTES = 5 * 1024 * 1024; // 5 MB
const MAX_FILES = 10;
const ACCEPTED_MIME = ['image/jpeg', 'image/png', 'image/webp', 'image/heic'];

// ─── Per-photo state ──────────────────────────────────────────────────────────

type PhotoStatus = 'idle' | 'uploading' | 'done' | 'error';

interface PhotoEntry {
  /** Unique stable key for React list rendering */
  key: string;
  /** The (possibly HEIC-converted) file ready to upload */
  file: File;
  /** Object URL for preview thumbnail */
  objectUrl: string;
  status: PhotoStatus;
  errorMsg?: string;
}

// ─── Props ────────────────────────────────────────────────────────────────────

export interface EndgamePhotoUploadSectionProps {
  /** Null during polling → upload is disabled until the record is created */
  recordId: string | null;
  /** Called with true when any upload starts, false when all uploads finish */
  onUploadingChange?: (uploading: boolean) => void;
  className?: string;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function EndgamePhotoUploadSection({
  recordId,
  onUploadingChange,
  className,
}: EndgamePhotoUploadSectionProps): React.JSX.Element {
  const { t } = useTranslation();

  // Always call the hook — satisfy React hook rules.
  // Empty string is a valid (non-null) recordId placeholder; actual upload is
  // gated by the disabled state of the upload button.
  const upload = usePlayRecordPhotoUpload(recordId ?? '');

  const [photos, setPhotos] = useState<PhotoEntry[]>([]);
  const [globalWarning, setGlobalWarning] = useState<string | null>(null);

  // Stable key counter so we can uniquely identify each photo entry
  const keyCounterRef = useRef(0);

  // Revoke object URLs on unmount to avoid memory leaks
  const photosRef = useRef(photos);
  photosRef.current = photos;

  useEffect(() => {
    return () => {
      for (const p of photosRef.current) {
        URL.revokeObjectURL(p.objectUrl);
      }
    };
  }, []);

  // ─── File selection ────────────────────────────────────────────────────────

  const handleFileChange = useCallback(
    async (e: React.ChangeEvent<HTMLInputElement>) => {
      setGlobalWarning(null);

      const selected = Array.from(e.target.files ?? []);
      if (selected.length === 0) return;

      let capped = selected;
      if (selected.length > MAX_FILES) {
        capped = selected.slice(0, MAX_FILES);
        setGlobalWarning(
          t('pages.pages.sessionLive.endgameDialog.photoUpload.tooManyFiles', { max: MAX_FILES })
        );
      }

      const entries: PhotoEntry[] = [];
      for (const raw of capped) {
        // Validate MIME
        if (!ACCEPTED_MIME.includes(raw.type)) {
          setGlobalWarning(t('pages.sessionLive.endgameDialog.photoUpload.badFormat'));
          continue;
        }

        let file = raw;

        // Convert HEIC → JPEG
        if (raw.type === 'image/heic') {
          try {
            const heic2any = (await import('heic2any')).default;
            const result = await heic2any({ blob: raw, toType: 'image/jpeg', quality: 0.9 });
            const jpegBlob = Array.isArray(result) ? result[0] : result;
            file = new File([jpegBlob as Blob], raw.name.replace(/\.heic$/i, '.jpg'), {
              type: 'image/jpeg',
            });
          } catch {
            setGlobalWarning(t('pages.sessionLive.endgameDialog.photoUpload.heicFailed'));
            continue;
          }
        }

        // Validate size (after potential conversion)
        if (file.size > MAX_BYTES) {
          setGlobalWarning(t('pages.sessionLive.endgameDialog.photoUpload.tooLarge'));
          continue;
        }

        const objectUrl = URL.createObjectURL(file);
        keyCounterRef.current += 1;
        entries.push({
          key: String(keyCounterRef.current),
          file,
          objectUrl,
          status: 'idle',
        });
      }

      // Append to existing photos (up to MAX_FILES total)
      setPhotos(prev => {
        const combined = [...prev, ...entries];
        return combined.slice(0, MAX_FILES);
      });

      // Reset input so the same file can be selected again if needed
      e.target.value = '';
    },
    [t]
  );

  // ─── Upload all idle/error photos ─────────────────────────────────────────

  const handleUpload = useCallback(async () => {
    if (!recordId) return;

    const toUpload = photos.filter(p => p.status === 'idle' || p.status === 'error');
    if (toUpload.length === 0) return;

    onUploadingChange?.(true);

    for (const photo of toUpload) {
      // Mark as uploading
      setPhotos(prev =>
        prev.map(p =>
          p.key === photo.key ? { ...p, status: 'uploading', errorMsg: undefined } : p
        )
      );

      try {
        await upload.mutateAsync({ file: photo.file });
        setPhotos(prev => prev.map(p => (p.key === photo.key ? { ...p, status: 'done' } : p)));
      } catch {
        setPhotos(prev =>
          prev.map(p =>
            p.key === photo.key
              ? {
                  ...p,
                  status: 'error',
                  errorMsg: t('pages.sessionLive.endgameDialog.photoUpload.errorGeneric'),
                }
              : p
          )
        );
      }
    }

    onUploadingChange?.(false);
  }, [recordId, photos, upload, onUploadingChange, t]);

  // ─── Retry a single photo ──────────────────────────────────────────────────

  const handleRetry = useCallback(
    async (key: string) => {
      if (!recordId) return;

      const photo = photos.find(p => p.key === key);
      if (!photo) return;

      onUploadingChange?.(true);

      setPhotos(prev =>
        prev.map(p => (p.key === key ? { ...p, status: 'uploading', errorMsg: undefined } : p))
      );

      try {
        await upload.mutateAsync({ file: photo.file });
        setPhotos(prev => prev.map(p => (p.key === key ? { ...p, status: 'done' } : p)));
      } catch {
        setPhotos(prev =>
          prev.map(p =>
            p.key === key
              ? {
                  ...p,
                  status: 'error',
                  errorMsg: t('pages.sessionLive.endgameDialog.photoUpload.errorGeneric'),
                }
              : p
          )
        );
      }

      onUploadingChange?.(false);
    },
    [recordId, photos, upload, onUploadingChange, t]
  );

  // ─── Derived state ─────────────────────────────────────────────────────────

  const hasFilesToUpload = photos.some(p => p.status === 'idle' || p.status === 'error');
  const isUploading = photos.some(p => p.status === 'uploading');
  const uploadDisabled = !recordId || !hasFilesToUpload || isUploading;

  // ─── Render ────────────────────────────────────────────────────────────────

  return (
    <section
      data-slot="endgame-photo-upload"
      className={cn('flex flex-col gap-3', className)}
      aria-label={t('pages.sessionLive.endgameDialog.photoUpload.sectionTitle')}
    >
      {/* Section title */}
      <h3 className="text-sm font-semibold text-foreground">
        {t('pages.sessionLive.endgameDialog.photoUpload.sectionTitle')}
      </h3>

      {/* File input */}
      <label className="block">
        <span className="sr-only">
          {t('pages.sessionLive.endgameDialog.photoUpload.addPhotosCta')}
        </span>
        <input
          type="file"
          accept="image/jpeg,image/png,image/webp,image/heic"
          multiple
          onChange={handleFileChange}
          aria-label={t('pages.sessionLive.endgameDialog.photoUpload.addPhotosCta')}
          className="block w-full text-sm text-muted-foreground file:mr-3 file:rounded-md file:border-0 file:bg-card file:px-3 file:py-1 file:text-sm file:font-medium file:text-foreground hover:file:cursor-pointer"
        />
      </label>

      {/* Global warning (too many files, bad format, too large) */}
      {globalWarning && (
        <p role="alert" className="text-sm text-destructive">
          {globalWarning}
        </p>
      )}

      {/* Per-photo preview list */}
      {photos.length > 0 && (
        <ul className="flex flex-col gap-2" aria-label="foto selezionate">
          {photos.map(photo => (
            <li
              key={photo.key}
              data-testid="photo-preview-item"
              data-status={photo.status}
              className="flex items-center gap-3 rounded-md border border-border bg-card p-2"
            >
              {/* Thumbnail */}
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={photo.objectUrl}
                alt={photo.file.name}
                className="h-10 w-10 rounded object-cover"
              />

              {/* File name + status */}
              <span className="flex-1 truncate text-sm text-foreground">{photo.file.name}</span>

              {/* Status indicators */}
              {photo.status === 'uploading' && (
                <span className="text-xs text-muted-foreground" aria-live="polite">
                  {t('pages.sessionLive.endgameDialog.photoUpload.uploadingLabel')}
                </span>
              )}

              {photo.status === 'done' && (
                <span className="rounded-full bg-card px-2 py-0.5 text-xs font-medium text-foreground ring-1 ring-border">
                  {t('pages.sessionLive.endgameDialog.photoUpload.doneBadge')}
                </span>
              )}

              {photo.status === 'error' && (
                <span className="flex items-center gap-2">
                  <span role="alert" className="text-xs text-destructive">
                    {photo.errorMsg ??
                      t('pages.sessionLive.endgameDialog.photoUpload.errorGeneric')}
                  </span>
                  <button
                    type="button"
                    onClick={() => void handleRetry(photo.key)}
                    className="text-xs font-medium text-foreground underline underline-offset-2 hover:no-underline"
                  >
                    {t('pages.sessionLive.endgameDialog.photoUpload.retryCta')}
                  </button>
                </span>
              )}
            </li>
          ))}
        </ul>
      )}

      {/* Upload CTA */}
      {photos.length > 0 && (
        <button
          type="button"
          onClick={() => void handleUpload()}
          disabled={uploadDisabled}
          aria-busy={isUploading}
          className="self-end rounded-md bg-card px-4 py-2 text-sm font-medium text-foreground ring-1 ring-border hover:bg-muted disabled:cursor-not-allowed disabled:opacity-50"
        >
          {isUploading
            ? t('pages.sessionLive.endgameDialog.photoUpload.uploadingLabel')
            : t('pages.sessionLive.endgameDialog.photoUpload.uploadCta')}
        </button>
      )}
    </section>
  );
}
