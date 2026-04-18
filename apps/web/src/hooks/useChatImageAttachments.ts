'use client';

import { useState, useCallback } from 'react';

export interface ChatImagePreview {
  file: File;
  previewUrl: string;
  mediaType: string;
}

const MAX_FILE_SIZE = 10 * 1024 * 1024;
const SUPPORTED_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

export function useChatImageAttachments(maxImages: number = 5) {
  const [images, setImages] = useState<ChatImagePreview[]>([]);

  const addImage = useCallback(
    (file: File): string | null => {
      if (!SUPPORTED_TYPES.includes(file.type))
        return 'Formato non supportato. Usa JPEG, PNG o WebP.';
      if (file.size > MAX_FILE_SIZE) return 'Immagine troppo grande. Massimo 10MB.';
      let rejected = false;
      setImages(prev => {
        if (prev.length >= maxImages) {
          rejected = true;
          return prev;
        }
        return [...prev, { file, previewUrl: URL.createObjectURL(file), mediaType: file.type }];
      });
      return rejected ? `Massimo ${maxImages} immagini per messaggio.` : null;
    },
    [maxImages]
  );

  const removeImage = useCallback((index: number) => {
    setImages(prev => {
      const removed = prev[index];
      if (removed) URL.revokeObjectURL(removed.previewUrl);
      return prev.filter((_, i) => i !== index);
    });
  }, []);

  const clearImages = useCallback(() => {
    setImages(prev => {
      prev.forEach(img => URL.revokeObjectURL(img.previewUrl));
      return [];
    });
  }, []);

  return {
    images,
    addImage,
    removeImage,
    clearImages,
    hasImages: images.length > 0,
    canAddMore: images.length < maxImages,
  };
}
