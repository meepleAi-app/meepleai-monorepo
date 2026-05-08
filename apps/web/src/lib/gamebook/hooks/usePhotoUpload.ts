'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';

import { uploadPhoto, type GamebookPhotoArtifact } from '@/lib/api/gamebook-photos';

export interface UploadPhotoArgs {
  file: File;
  pageType: 'Storybook' | 'Encounter';
}

export function usePhotoUpload(campaignId: string) {
  const qc = useQueryClient();
  return useMutation<GamebookPhotoArtifact, Error, UploadPhotoArgs>({
    mutationFn: ({ file, pageType }) => uploadPhoto(campaignId, file, pageType),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['gamebook', 'photos', campaignId] });
    },
  });
}
