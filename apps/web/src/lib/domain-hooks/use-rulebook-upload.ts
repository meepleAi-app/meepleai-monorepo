import { useMutation, useQueryClient } from '@tanstack/react-query';

interface RulebookUploadResult {
  pdfDocumentId: string;
  isNew: boolean;
  status: 'pending' | 'processing' | 'ready' | 'failed';
  message: string;
}

export function useRulebookUpload(gameId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (file: File): Promise<RulebookUploadResult> => {
      const formData = new FormData();
      formData.append('file', file);

      const response = await fetch(`/api/v1/games/${gameId}/rulebook`, {
        method: 'POST',
        body: formData,
      });

      if (!response.ok) {
        const error = await response.text();
        throw new Error(error || `Upload failed: ${response.status}`);
      }

      return response.json();
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['games', 'with-kb'] });
    },
  });
}
