/**
 * usePlayRecordPhotoUpload — mutation hook for POST /play-records/{id}/photos.
 * Invalidates the record detail query so the gallery refetches with the new photo.
 * #2436 PR-C.
 */
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { playRecordsApi, type UploadPlayRecordPhotoResult } from '@/lib/api/play-records.api';
import { playRecordsKeys } from '@/lib/domain-hooks/usePlayRecords';

export interface UploadPhotoVars {
  file: Blob;
  caption?: string;
  extractScoreFromPhoto?: boolean;
}

export function usePlayRecordPhotoUpload(recordId: string) {
  const queryClient = useQueryClient();

  return useMutation<UploadPlayRecordPhotoResult, Error, UploadPhotoVars>({
    mutationFn: ({ file, caption, extractScoreFromPhoto }) =>
      playRecordsApi.uploadPhoto(recordId, file, { caption, extractScoreFromPhoto }),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: playRecordsKeys.detail(recordId) });
    },
  });
}
