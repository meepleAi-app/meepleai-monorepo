'use client';

import { useState, useCallback } from 'react';

import {
  Dialog,
  DialogContent,
  DialogTitle,
  DialogDescription,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { useCustomCoverUpload } from '@/hooks/mutations/useCustomCoverUpload';
import { useRemoveCustomCover } from '@/hooks/mutations/useRemoveCustomCover';

import { CropDialog } from './CropDialog';

interface CustomCoverDialogProps {
  gameId: string;
  open: boolean;
  onClose: () => void;
  hasCustomCover: boolean;
}

const MAX_INPUT_BYTES = 10 * 1024 * 1024; // 10MB
const ACCEPTED_MIME = ['image/jpeg', 'image/png', 'image/webp', 'image/heic'];

export function CustomCoverDialog({
  gameId,
  open,
  onClose,
  hasCustomCover,
}: CustomCoverDialogProps): React.JSX.Element {
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [validationError, setValidationError] = useState<string | null>(null);

  const uploadMutation = useCustomCoverUpload(gameId);
  const removeMutation = useRemoveCustomCover(gameId);

  const handleFileSelect = useCallback(async (e: React.ChangeEvent<HTMLInputElement>) => {
    setValidationError(null);
    const file = e.target.files?.[0];
    if (!file) return;

    if (file.size > MAX_INPUT_BYTES) {
      setValidationError('File troppo grande, massimo 10MB');
      return;
    }

    if (!ACCEPTED_MIME.includes(file.type)) {
      setValidationError('Formato non supportato. Usa JPG, PNG, WebP o HEIC.');
      return;
    }

    // HEIC iOS: lazy-load heic2any and convert to JPEG
    if (file.type === 'image/heic') {
      try {
        const heic2any = (await import('heic2any')).default;
        const result = await heic2any({ blob: file, toType: 'image/jpeg', quality: 0.9 });
        // heic2any can return Blob or Blob[] for multi-image HEIC
        const jpegBlob = Array.isArray(result) ? result[0] : result;
        const jpegFile = new File([jpegBlob], file.name.replace(/\.heic$/i, '.jpg'), {
          type: 'image/jpeg',
        });
        setSelectedFile(jpegFile);
      } catch {
        setValidationError('Conversione HEIC fallita. Riprova con JPEG o PNG.');
      }
    } else {
      setSelectedFile(file);
    }
  }, []);

  const handleCropConfirm = useCallback(
    (blob: Blob) => {
      uploadMutation.mutate(blob, {
        onSuccess: () => {
          setSelectedFile(null);
          onClose();
        },
      });
    },
    [uploadMutation, onClose]
  );

  const handleRemove = useCallback(() => {
    removeMutation.mutate(undefined, {
      onSuccess: () => {
        onClose();
      },
    });
  }, [removeMutation, onClose]);

  return (
    <>
      <Dialog open={open && !selectedFile} onOpenChange={o => !o && onClose()}>
        <DialogContent>
          <DialogTitle>Personalizza copertina</DialogTitle>
          <DialogDescription>
            Carica una foto della tua copia del gioco (200×300, max 200KB output).
          </DialogDescription>
          <label className="block">
            <span className="text-sm font-medium">Seleziona foto</span>
            <input
              type="file"
              accept="image/jpeg,image/png,image/webp,image/heic"
              capture="environment"
              onChange={handleFileSelect}
              className="mt-1 block w-full"
            />
          </label>
          {validationError && <p className="text-sm text-destructive">{validationError}</p>}
          {hasCustomCover && (
            <div className="border-t pt-4">
              <Button
                variant="outline"
                onClick={handleRemove}
                disabled={removeMutation.isPending}
                aria-label="Rimuovi copertina personalizzata"
              >
                {removeMutation.isPending ? 'Rimozione...' : 'Rimuovi copertina personalizzata'}
              </Button>
            </div>
          )}
        </DialogContent>
      </Dialog>
      {selectedFile && (
        <CropDialog
          open={true}
          imageFile={selectedFile}
          onConfirm={handleCropConfirm}
          onCancel={() => setSelectedFile(null)}
        />
      )}
    </>
  );
}
