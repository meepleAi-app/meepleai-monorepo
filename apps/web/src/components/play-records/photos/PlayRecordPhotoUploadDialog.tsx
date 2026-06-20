'use client';

import { useState, useCallback } from 'react';

import { toast } from 'sonner';

import {
  Dialog,
  DialogContent,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { usePlayRecordPhotoUpload } from '@/hooks/mutations/usePlayRecordPhotoUpload';
import { useTranslation } from '@/hooks/useTranslation';

const MAX_BYTES = 5 * 1024 * 1024; // 5MB — matches BE validator
const MAX_FILES = 10;
const ACCEPTED_MIME = ['image/jpeg', 'image/png', 'image/webp', 'image/heic'];

export interface PlayRecordPhotoUploadDialogProps {
  recordId: string;
  open: boolean;
  onClose: () => void;
}

export function PlayRecordPhotoUploadDialog({
  recordId,
  open,
  onClose,
}: PlayRecordPhotoUploadDialogProps): React.JSX.Element {
  const { t } = useTranslation();
  const [files, setFiles] = useState<File[]>([]);
  const [caption, setCaption] = useState('');
  const [extractScore, setExtractScore] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const upload = usePlayRecordPhotoUpload(recordId);

  const handleSelect = useCallback(
    async (e: React.ChangeEvent<HTMLInputElement>) => {
      setError(null);
      const selected = Array.from(e.target.files ?? []);
      if (selected.length === 0) return;
      if (selected.length > MAX_FILES) {
        setError(t('playRecords.photos.tooMany'));
        return;
      }

      const out: File[] = [];
      for (const file of selected) {
        if (!ACCEPTED_MIME.includes(file.type)) {
          setError(t('playRecords.photos.badFormat'));
          return;
        }
        let candidate = file;
        if (file.type === 'image/heic') {
          try {
            const heic2any = (await import('heic2any')).default;
            const result = await heic2any({ blob: file, toType: 'image/jpeg', quality: 0.9 });
            const jpegBlob = Array.isArray(result) ? result[0] : result;
            candidate = new File([jpegBlob], file.name.replace(/\.heic$/i, '.jpg'), {
              type: 'image/jpeg',
            });
          } catch {
            setError(t('playRecords.photos.heicFailed'));
            return;
          }
        }
        if (candidate.size > MAX_BYTES) {
          setError(t('playRecords.photos.tooLarge'));
          return;
        }
        out.push(candidate);
      }
      setFiles(out);
    },
    [t]
  );

  const handleUpload = useCallback(async () => {
    setError(null);
    try {
      for (const file of files) {
        const res = await upload.mutateAsync({
          file,
          caption: caption || undefined,
          extractScoreFromPhoto: extractScore || undefined,
        });
        if (res.wasDeduplicated) {
          toast.info(t('playRecords.photos.dedupToast'));
        } else if (res.ocrText) {
          toast.success(`${t('playRecords.photos.ocrResultTitle')}: ${res.ocrText}`);
        }
      }
      setFiles([]);
      setCaption('');
      setExtractScore(false);
      onClose();
    } catch {
      setError(t('playRecords.photos.uploadError'));
    }
  }, [files, caption, extractScore, upload, t, onClose]);

  return (
    <Dialog open={open} onOpenChange={o => !o && onClose()}>
      <DialogContent>
        <DialogTitle>{t('playRecords.photos.dialogTitle')}</DialogTitle>
        <DialogDescription>{t('playRecords.photos.dialogDescription')}</DialogDescription>

        <label className="block">
          <span className="text-sm font-medium">{t('playRecords.photos.selectLabel')}</span>
          <input
            type="file"
            accept="image/jpeg,image/png,image/webp,image/heic"
            multiple
            capture="environment"
            onChange={handleSelect}
            aria-label={t('playRecords.photos.selectLabel')}
            className="mt-1 block w-full"
          />
        </label>

        {files.length > 0 && (
          <p className="text-sm text-muted-foreground">{files.map(f => f.name).join(', ')}</p>
        )}

        <label className="block">
          <span className="text-sm font-medium">{t('playRecords.photos.captionLabel')}</span>
          <input
            type="text"
            value={caption}
            maxLength={500}
            onChange={e => setCaption(e.target.value)}
            placeholder={t('playRecords.photos.captionPlaceholder')}
            aria-label={t('playRecords.photos.captionLabel')}
            className="mt-1 block w-full rounded-md border border-border bg-card px-3 py-1.5 text-sm"
          />
        </label>

        <label className="flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={extractScore}
            onChange={e => setExtractScore(e.target.checked)}
            aria-label={t('playRecords.photos.extractScoreLabel')}
          />
          {t('playRecords.photos.extractScoreLabel')}
        </label>

        {error && (
          <p role="alert" className="text-sm text-destructive">
            {error}
          </p>
        )}

        <div className="flex justify-end gap-2 pt-2">
          <Button onClick={handleUpload} disabled={files.length === 0 || upload.isPending}>
            {upload.isPending
              ? t('playRecords.photos.uploading')
              : t('playRecords.photos.uploadCta')}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
