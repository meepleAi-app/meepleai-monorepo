'use client';

import { useCallback, useState, type JSX } from 'react';

import Cropper from 'react-easy-crop';

import { Dialog, DialogContent, DialogTitle } from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';

import type { Area } from 'react-easy-crop';

interface CropDialogProps {
  open: boolean;
  imageFile: File;
  onConfirm: (blob: Blob) => void;
  onCancel: () => void;
}

const TARGET_WIDTH = 200;
const TARGET_HEIGHT = 300;
const MAX_OUTPUT_BYTES = 200_000; // 200 KB
const MIN_QUALITY = 0.4;

/**
 * Modal crop UI with react-easy-crop.
 * Output webp ≤200 KB via quality dial-down loop (q=0.8 → 0.4).
 * Issue #1824 L3.
 */
export function CropDialog({ open, imageFile, onConfirm, onCancel }: CropDialogProps): JSX.Element {
  const [crop, setCrop] = useState({ x: 0, y: 0 });
  const [zoom, setZoom] = useState(1);
  const [croppedPixels, setCroppedPixels] = useState<Area | null>(null);
  const [imageUrl] = useState(() => URL.createObjectURL(imageFile));
  const [isProcessing, setIsProcessing] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleCropComplete = useCallback((_area: Area, pixels: Area) => {
    setCroppedPixels(pixels);
  }, []);

  const handleConfirm = useCallback(async () => {
    if (!croppedPixels) return;
    setIsProcessing(true);
    setError(null);

    try {
      const blob = await renderCropToWebp(imageUrl, croppedPixels);
      onConfirm(blob);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Errore di compressione');
    } finally {
      setIsProcessing(false);
    }
  }, [croppedPixels, imageUrl, onConfirm]);

  return (
    <Dialog open={open} onOpenChange={o => !o && onCancel()}>
      <DialogContent className="max-w-[600px]">
        <DialogTitle>Ritaglia la copertina (200×300)</DialogTitle>
        <div className="relative h-[400px] w-full bg-muted">
          <Cropper
            image={imageUrl}
            crop={crop}
            zoom={zoom}
            aspect={TARGET_WIDTH / TARGET_HEIGHT}
            onCropChange={setCrop}
            onZoomChange={setZoom}
            onCropComplete={handleCropComplete}
          />
        </div>
        {error && <p className="text-sm text-destructive">{error}</p>}
        <div className="flex justify-end gap-2">
          <Button variant="outline" onClick={onCancel} disabled={isProcessing}>
            Annulla
          </Button>
          <Button onClick={handleConfirm} disabled={!croppedPixels || isProcessing}>
            {isProcessing ? 'Elaborazione...' : 'Conferma'}
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

/**
 * Render crop area to webp blob with quality dial-down to fit ≤200 KB.
 * @throws {Error} if output exceeds 200 KB at MIN_QUALITY.
 */
async function renderCropToWebp(imageUrl: string, pixels: Area): Promise<Blob> {
  const img = await loadImage(imageUrl);
  const canvas = document.createElement('canvas');
  canvas.width = TARGET_WIDTH;
  canvas.height = TARGET_HEIGHT;
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Canvas context unavailable');

  ctx.drawImage(
    img,
    pixels.x,
    pixels.y,
    pixels.width,
    pixels.height,
    0,
    0,
    TARGET_WIDTH,
    TARGET_HEIGHT
  );

  // Dial down quality until ≤200 KB
  for (let q = 0.8; q >= MIN_QUALITY - 0.001; q -= 0.1) {
    const blob = await canvasToBlob(canvas, 'image/webp', q);
    if (blob.size <= MAX_OUTPUT_BYTES) {
      return blob;
    }
  }
  throw new Error('Immagine troppo complessa per comprimere, riprova con foto più semplice');
}

function loadImage(url: string): Promise<HTMLImageElement> {
  return new Promise((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = reject;
    img.src = url;
  });
}

function canvasToBlob(canvas: HTMLCanvasElement, type: string, quality: number): Promise<Blob> {
  return new Promise((resolve, reject) => {
    canvas.toBlob(
      blob => {
        if (!blob) reject(new Error('Canvas toBlob returned null'));
        else resolve(blob);
      },
      type,
      quality
    );
  });
}
