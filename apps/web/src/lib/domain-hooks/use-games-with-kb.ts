import { useQuery } from '@tanstack/react-query';

interface RulebookInfo {
  pdfDocumentId: string;
  fileName: string;
  kbStatus: 'ready' | 'processing' | 'failed';
  indexedAt: string | null;
}

export interface GameWithKb {
  gameId: string;
  title: string;
  imageUrl: string | null;
  overallKbStatus: 'ready' | 'processing' | 'failed';
  rulebooks: RulebookInfo[];
}

export function useGamesWithKb(userId: string) {
  return useQuery({
    queryKey: ['games', 'with-kb', userId],
    queryFn: async (): Promise<GameWithKb[]> => {
      const response = await fetch(`/api/v1/users/${userId}/games/with-kb`);

      if (!response.ok) {
        throw new Error('Failed to fetch games with KB');
      }

      return response.json();
    },
    enabled: !!userId,
    staleTime: 2 * 60 * 1000,
  });
}
