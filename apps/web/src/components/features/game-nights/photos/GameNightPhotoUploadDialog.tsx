'use client';

import { useCallback, useEffect, useState } from 'react';

import { toast } from 'sonner';

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { useUploadGameNightPhoto } from '@/hooks/queries/useGameNights';
import { useTranslation } from '@/hooks/useTranslation';

const MAX_BYTES = 5 * 1024 * 1024; // 5MB (matches the BE cap)
const ACCEPTED_MIME = ['image/jpeg', 'image/png', 'image/webp'];

export interface GameNightPhotoUploadDialogProps {
  gameNightId: string;
  open: boolean;
  onClose: () => void;
}

export function GameNightPhotoUploadDialog({
  gameNightId,
  open,
  onClose,
}: GameNightPhotoUploadDialogProps): React.JSX.Element {
  const { t } = useTranslation();
  const [file, setFile] = useState<File | null>(null);
  const [previewUrl, setPreviewUrl] = useState<string | null>(null);
  const [caption, setCaption] = useState('');
  const [extractScore, setExtractScore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const upload = useUploadGameNightPhoto(gameNightId);

  // Object-URL preview with proper revocation (avoid per-render leaks).
  useEffect(() => {
    if (!file || typeof URL.createObjectURL !== 'function') {
      setPreviewUrl(null);
      return;
    }
    const url = URL.createObjectURL(file);
    setPreviewUrl(url);
    return () => URL.revokeObjectURL(url);
  }, [file]);

  const reset = useCallback(() => {
    setFile(null);
    setCaption('');
    setExtractScore(false);
    setError(null);
  }, []);

  const handleSelect = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      setError(null);
      const selected = e.target.files?.[0] ?? null;
      if (!selected) return;
      if (!ACCEPTED_MIME.includes(selected.type)) {
        setError(t('gameNightDetail.photos.badFormat'));
        return;
      }
      if (selected.size > MAX_BYTES) {
        setError(t('gameNightDetail.photos.tooLarge'));
        return;
      }
      setFile(selected);
    },
    [t]
  );

  const handleUpload = useCallback(async () => {
    if (!file) return;
    setError(null);
    try {
      const res = await upload.mutateAsync({
        file,
        caption: caption || undefined,
        extractScoreFromPhoto: extractScore || undefined,
      });
      if (res.wasDeduplicated) {
        toast.info(t('gameNightDetail.photos.dedupToast'));
      } else if (res.ocrText) {
        toast.success(`${t('gameNightDetail.photos.ocrResultTitle')}: ${res.ocrText}`);
      }
      reset();
      onClose();
    } catch {
      setError(t('gameNightDetail.photos.uploadError'));
    }
  }, [file, caption, extractScore, upload, t, reset, onClose]);

  return (
    <Dialog open={open} onOpenChange={o => !o && onClose()}>
      <DialogContent>
        <DialogTitle>{t('gameNightDetail.photos.dialogTitle')}</DialogTitle>
        <DialogDescription>{t('gameNightDetail.photos.dialogDescription')}</DialogDescription>

        <label className="block">
          <span className="text-sm font-medium">{t('gameNightDetail.photos.selectLabel')}</span>
          <input
            type="file"
            accept="image/jpeg,image/png,image/webp"
            onChange={handleSelect}
            aria-label={t('gameNightDetail.photos.selectLabel')}
            className="mt-1 block w-full"
          />
        </label>

        {previewUrl && (
          // eslint-disable-next-line @next/next/no-img-element -- local object-URL preview
          <img
            src={previewUrl}
            alt={t('gameNightDetail.photos.previewAlt')}
            className="max-h-48 w-full rounded-md object-contain"
          />
        )}

        <label className="block">
          <span className="text-sm font-medium">{t('gameNightDetail.photos.captionLabel')}</span>
          <input
            type="text"
            value={caption}
            maxLength={500}
            onChange={e => setCaption(e.target.value)}
            placeholder={t('gameNightDetail.photos.captionPlaceholder')}
            aria-label={t('gameNightDetail.photos.captionLabel')}
            className="mt-1 block w-full rounded-md border border-border bg-card px-3 py-1.5 text-sm"
          />
        </label>

        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={extractScore}
            onChange={e => setExtractScore(e.target.checked)}
            aria-label={t('gameNightDetail.photos.extractScoreLabel')}
          />
          {t('gameNightDetail.photos.extractScoreLabel')}
        </label>

        {error && (
          <p role="alert" className="text-sm text-destructive">
            {error}
          </p>
        )}

        <div className="flex justify-end gap-2 pt-2">
          <Button onClick={handleUpload} disabled={!file || upload.isPending}>
            {upload.isPending
              ? t('gameNightDetail.photos.uploading')
              : t('gameNightDetail.photos.uploadCta')}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
