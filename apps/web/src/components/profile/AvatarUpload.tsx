/**
 * AvatarUpload Component - Issue #2882
 *
 * Avatar upload component with image preview and crop functionality.
 * Features:
 * - Click avatar to open file picker
 * - Image preview before upload
 * - Crop/resize functionality (square, 400x400px)
 * - Optimistic UI update
 * - Purple camera icon overlay on hover
 */

'use client';

import { useCallback, useRef, useState } from 'react';

import { Camera, Loader2, X, ZoomIn, ZoomOut } from 'lucide-react';
import ReactCrop, { type Crop, centerCrop, makeAspectCrop } from 'react-image-crop';
import 'react-image-crop/dist/ReactCrop.css';

import { Avatar, AvatarFallback, AvatarImage } from '@/components/ui/data-display/avatar';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// Constants
const AVATAR_SIZE = 400; // Final avatar size in pixels
const MAX_FILE_SIZE = 5 * 1024 * 1024; // 5MB
const ACCEPTED_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif'];
const MIN_DIMENSION = 100; // Minimum image dimension for cropping

export interface AvatarUploadProps {
  /** Current avatar URL */
  currentAvatarUrl: string | null;
  /** Display name for fallback initials */
  displayName: string;
  /** Callback when avatar is uploaded successfully */
  onUpload: (file: File, previewUrl: string) => Promise<void>;
  /** Optional className for the container */
  className?: string;
  /** Avatar size in pixels (default: 96 = h-24 w-24) */
  size?: number;
  /** Whether the component is disabled */
  disabled?: boolean;
}

/**
 * Extracts initials from a display name
 */
function getInitials(name: string): string {
  return name
    .split(' ')
    .map(part => part.charAt(0))
    .join('')
    .toUpperCase()
    .slice(0, 2);
}

/**
 * Creates a centered crop with aspect ratio 1:1
 */
function centerAspectCrop(mediaWidth: number, mediaHeight: number, aspect: number): Crop {
  return centerCrop(
    makeAspectCrop(
      {
        unit: '%',
        width: 90,
      },
      aspect,
      mediaWidth,
      mediaHeight
    ),
    mediaWidth,
    mediaHeight
  );
}

/**
 * Converts a crop to a canvas and returns the cropped image as a Blob
 */
async function getCroppedImg(
  image: HTMLImageElement,
  crop: Crop,
  targetSize: number
): Promise<{ blob: Blob; url: string }> {
  const canvas = document.createElement('canvas');
  const ctx = canvas.getContext('2d');

  if (!ctx) {
    throw new Error('No 2d context');
  }

  // Calculate crop dimensions in pixels
  const scaleX = image.naturalWidth / image.width;
  const scaleY = image.naturalHeight / image.height;

  const cropX = (crop.x / 100) * image.width * scaleX;
  const cropY = (crop.y / 100) * image.height * scaleY;
  const cropWidth = (crop.width / 100) * image.width * scaleX;
  const cropHeight = (crop.height / 100) * image.height * scaleY;

  // Set canvas size to target dimensions
  canvas.width = targetSize;
  canvas.height = targetSize;

  // Enable smooth scaling
  ctx.imageSmoothingEnabled = true;
  ctx.imageSmoothingQuality = 'high';

  // Draw cropped image scaled to target size
  ctx.drawImage(image, cropX, cropY, cropWidth, cropHeight, 0, 0, targetSize, targetSize);

  // Convert to blob
  return new Promise((resolve, reject) => {
    canvas.toBlob(
      blob => {
        if (!blob) {
          reject(new Error('Canvas is empty'));
          return;
        }
        const url = URL.createObjectURL(blob);
        resolve({ blob, url });
      },
      'image/jpeg',
      0.92 // Quality
    );
  });
}

export function AvatarUpload({
  currentAvatarUrl,
  displayName,
  onUpload,
  className,
  size = 96,
  disabled = false,
}: AvatarUploadProps) {
  // State
  const [isHovered, setIsHovered] = useState(false);
  const [isDialogOpen, setIsDialogOpen] = useState(false);
  const [selectedImage, setSelectedImage] = useState<string | null>(null);
  const [crop, setCrop] = useState<Crop>();
  const [isUploading, setIsUploading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [zoom, setZoom] = useState(1);

  // Refs
  const fileInputRef = useRef<HTMLInputElement>(null);
  const imageRef = useRef<HTMLImageElement>(null);

  // Handlers
  const handleAvatarClick = useCallback(() => {
    if (disabled || isUploading) return;
    fileInputRef.current?.click();
  }, [disabled, isUploading]);

  const handleFileSelect = useCallback((event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) return;

    // Reset input
    event.target.value = '';

    // Validate file type
    if (!ACCEPTED_TYPES.includes(file.type)) {
      setError('Formato non supportato. Usa JPEG, PNG, WebP o GIF.');
      return;
    }

    // Validate file size
    if (file.size > MAX_FILE_SIZE) {
      setError('Immagine troppo grande. Massimo 5MB.');
      return;
    }

    // Create preview URL and validate dimensions
    const reader = new FileReader();
    reader.onload = () => {
      const img = new Image();
      img.onload = () => {
        if (img.width < MIN_DIMENSION || img.height < MIN_DIMENSION) {
          setError(`Immagine troppo piccola. Minimo ${MIN_DIMENSION}x${MIN_DIMENSION}px.`);
          return;
        }
        setSelectedImage(reader.result as string);
        setError(null);
        setIsDialogOpen(true);
      };
      img.onerror = () => {
        setError('Impossibile caricare l\'immagine. Verifica che sia un\'immagine valida.');
      };
      img.src = reader.result as string;
    };
    reader.onerror = () => {
      setError('Errore nella lettura del file.');
    };
    reader.readAsDataURL(file);
  }, []);

  const handleImageLoad = useCallback((e: React.SyntheticEvent<HTMLImageElement>) => {
    const { width, height } = e.currentTarget;
    setCrop(centerAspectCrop(width, height, 1));
  }, []);

  const handleCropComplete = useCallback(async () => {
    if (!imageRef.current || !crop) return;

    setIsUploading(true);
    setError(null);

    try {
      const { blob, url } = await getCroppedImg(imageRef.current, crop, AVATAR_SIZE);

      // Create a File from the Blob
      const file = new File([blob], 'avatar.jpg', { type: 'image/jpeg' });

      // Call the upload callback with optimistic preview
      await onUpload(file, url);

      // Close dialog on success
      setIsDialogOpen(false);
      setSelectedImage(null);
      setCrop(undefined);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Errore durante l\'upload dell\'avatar.');
    } finally {
      setIsUploading(false);
    }
  }, [crop, onUpload]);

  const handleDialogClose = useCallback(() => {
    if (isUploading) return;
    setIsDialogOpen(false);
    setSelectedImage(null);
    setCrop(undefined);
    setError(null);
    setZoom(1);
  }, [isUploading]);

  const handleZoomIn = useCallback(() => {
    setZoom(prev => Math.min(prev + 0.1, 3));
  }, []);

  const handleZoomOut = useCallback(() => {
    setZoom(prev => Math.max(prev - 0.1, 1));
  }, []);

  // Size classes based on size prop
  const sizeClass = `h-[${size}px] w-[${size}px]`;
  const sizeStyle = { width: size, height: size };

  return (
    <>
      {/* Avatar with camera overlay */}
      <div
        className={cn('relative cursor-pointer group', className)}
        onMouseEnter={() => setIsHovered(true)}
        onMouseLeave={() => setIsHovered(false)}
        onClick={handleAvatarClick}
        onKeyDown={e => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            handleAvatarClick();
          }
        }}
        role="group"
        tabIndex={-1}
        aria-label={`Avatar di ${displayName}`}
        aria-disabled={disabled}
      >
        <Avatar className={sizeClass} style={sizeStyle}>
          <AvatarImage src={currentAvatarUrl || undefined} alt={displayName} />
          <AvatarFallback className="text-2xl bg-orange-100 text-orange-600">
            {getInitials(displayName || 'U')}
          </AvatarFallback>
        </Avatar>

        {/* Purple camera icon overlay on hover - DoD requirement */}
        <div
          className={cn(
            'absolute inset-0 rounded-full bg-purple-600/60 flex items-center justify-center transition-opacity duration-200',
            isHovered && !disabled ? 'opacity-100' : 'opacity-0'
          )}
          aria-hidden="true"
        >
          <Camera className="h-8 w-8 text-white" />
        </div>

        {/* Camera button positioned at bottom-right */}
        <button
          type="button"
          className={cn(
            'absolute bottom-0 right-0 p-1.5 bg-purple-500 text-white rounded-full',
            'hover:bg-purple-600 transition-colors shadow-md',
            'focus:outline-none focus:ring-2 focus:ring-purple-500 focus:ring-offset-2',
            disabled && 'opacity-50 cursor-not-allowed'
          )}
          onClick={e => {
            e.stopPropagation();
            handleAvatarClick();
          }}
          disabled={disabled || isUploading}
          aria-label="Cambia avatar"
        >
          {isUploading ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : (
            <Camera className="h-4 w-4" />
          )}
        </button>
      </div>

      {/* Hidden file input */}
      <input
        ref={fileInputRef}
        type="file"
        accept={ACCEPTED_TYPES.join(',')}
        onChange={handleFileSelect}
        className="hidden"
        aria-hidden="true"
      />

      {/* Error alert (shown outside dialog) */}
      {error && !isDialogOpen && (
        <Alert variant="destructive" className="mt-2">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {/* Crop dialog */}
      <Dialog open={isDialogOpen} onOpenChange={open => !open && handleDialogClose()}>
        <DialogContent className="sm:max-w-lg">
          <DialogHeader>
            <DialogTitle>Ritaglia avatar</DialogTitle>
            <DialogDescription>
              Seleziona l'area dell'immagine da usare come avatar. L'immagine verrà ridimensionata
              a 400x400 pixel.
            </DialogDescription>
          </DialogHeader>

          {/* Error inside dialog */}
          {error && (
            <Alert variant="destructive">
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          )}

          {/* Crop area */}
          <div className="flex flex-col items-center gap-4">
            {selectedImage && (
              <div className="relative max-h-[400px] overflow-hidden rounded-lg bg-muted">
                <ReactCrop
                  crop={crop}
                  onChange={(_, percentCrop) => setCrop(percentCrop)}
                  aspect={1}
                  circularCrop
                  keepSelection
                  minWidth={50}
                  minHeight={50}
                >
                  {/* eslint-disable-next-line @next/next/no-img-element -- react-image-crop requires native img element for cropping */}
                  <img
                    ref={imageRef}
                    src={selectedImage}
                    alt="Immagine da ritagliare"
                    onLoad={handleImageLoad}
                    style={{
                      maxHeight: '400px',
                      maxWidth: '100%',
                      transform: `scale(${zoom})`,
                      transformOrigin: 'center',
                      transition: 'transform 0.2s ease-out',
                    }}
                  />
                </ReactCrop>
              </div>
            )}

            {/* Zoom controls */}
            <div className="flex items-center gap-2">
              <Button
                type="button"
                variant="outline"
                size="icon"
                onClick={handleZoomOut}
                disabled={zoom <= 1}
                aria-label="Zoom out"
              >
                <ZoomOut className="h-4 w-4" />
              </Button>
              <span className="text-sm text-muted-foreground w-16 text-center">
                {Math.round(zoom * 100)}%
              </span>
              <Button
                type="button"
                variant="outline"
                size="icon"
                onClick={handleZoomIn}
                disabled={zoom >= 3}
                aria-label="Zoom in"
              >
                <ZoomIn className="h-4 w-4" />
              </Button>
            </div>
          </div>

          <DialogFooter className="gap-2 sm:gap-0">
            <Button
              type="button"
              variant="outline"
              onClick={handleDialogClose}
              disabled={isUploading}
            >
              <X className="h-4 w-4 mr-2" />
              Annulla
            </Button>
            <Button
              type="button"
              onClick={handleCropComplete}
              disabled={isUploading || !crop}
              className="bg-purple-500 hover:bg-purple-600"
            >
              {isUploading ? (
                <>
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  Caricamento...
                </>
              ) : (
                'Salva avatar'
              )}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
