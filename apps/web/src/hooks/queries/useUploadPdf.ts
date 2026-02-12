/**
 * useUploadPdf - React Query hook for PDF upload
 * Issue #4162: Upload PDF step for wizard
 *
 * Handles PDF file upload with progress tracking and error handling.
 */

import { useState } from 'react';

import { useMutation, type UseMutationOptions } from '@tanstack/react-query';

import { api } from '@/lib/api';

export interface UploadPdfResult {
  documentId: string;
  fileName: string;
}

export interface UseUploadPdfOptions
  extends Omit<UseMutationOptions<UploadPdfResult, Error, File>, 'mutationFn'> {
  onProgress?: (percent: number) => void;
}

/**
 * Hook for uploading PDF files with progress tracking
 *
 * @param options - Mutation options including onProgress callback
 * @returns Mutation result with progress state
 *
 * @example
 * ```tsx
 * const { mutate, isPending, progress } = useUploadPdf();
 *
 * const handleUpload = (file: File) => {
 *   mutate(file, {
 *     onSuccess: (data) => console.log('Uploaded:', data.documentId),
 *     onError: (error) => console.error('Failed:', error),
 *   });
 * };
 * ```
 */
export function useUploadPdf(options: UseUploadPdfOptions = {}) {
  const { onProgress, ...mutationOptions } = options;
  const [progress, setProgress] = useState<number>(0);

  const mutation = useMutation<UploadPdfResult, Error, File>({
    mutationFn: async (file: File) => {
      // Reset progress
      setProgress(0);

      // Upload with progress tracking
      const result = await api.pdf.uploadPdf(
        'wizard-temp', // Temporary gameId for wizard uploads
        file,
        percent => {
          setProgress(percent);
          onProgress?.(percent);
        }
      );

      return result;
    },
    onError: () => {
      // Reset progress on error
      setProgress(0);
    },
    ...mutationOptions,
  });

  return {
    ...mutation,
    progress,
  };
}
