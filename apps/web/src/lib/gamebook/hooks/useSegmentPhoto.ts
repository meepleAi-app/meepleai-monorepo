'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';

import { segmentPhoto, type GamebookPhotoArtifact } from '@/lib/api/gamebook-photos';

export interface SegmentPhotoArgs {
  photoId: string;
}

export function useSegmentPhoto(campaignId: string) {
  const qc = useQueryClient();
  return useMutation<GamebookPhotoArtifact, Error, SegmentPhotoArgs>({
    mutationFn: ({ photoId }) => segmentPhoto(campaignId, photoId),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ['gamebook', 'photos', campaignId] });
    },
  });
}
