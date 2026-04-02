'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Trash2 } from 'lucide-react';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';

interface Props {
  gameId: string;
}

export function GameKbDocuments({ gameId }: Props) {
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['admin-game-kb-documents', gameId],
    queryFn: () => api.knowledgeBase.getAdminGameKbDocuments(gameId),
  });

  const removeMutation = useMutation({
    mutationFn: (vectorDocId: string) => api.knowledgeBase.removeKbDocument(gameId, vectorDocId),
    onSuccess: () => {
      toast.success('Documento rimosso dalla KB');
      queryClient.invalidateQueries({ queryKey: ['admin-game-kb-documents', gameId] });
    },
    onError: () => toast.error('Errore nella rimozione del documento'),
  });

  if (isLoading) {
    return <div className="animate-pulse h-32 bg-muted rounded-lg" />;
  }

  if (!data?.documents.length) {
    return (
      <div className="text-center py-8 text-muted-foreground">Nessun documento indicizzato</div>
    );
  }

  return (
    <div className="space-y-2">
      <p className="text-sm text-muted-foreground">{data.documents.length} documento/i</p>
      {data.documents.map(doc => (
        <div
          key={doc.id}
          className="flex items-center justify-between p-3 border rounded-lg bg-white/60 dark:bg-zinc-800/60"
        >
          <div className="flex items-center gap-3">
            <Badge variant={doc.indexingStatus === 'completed' ? 'default' : 'secondary'}>
              {doc.indexingStatus}
            </Badge>
            <span className="text-sm font-mono">{doc.pdfDocumentId.slice(0, 8)}&hellip;</span>
            <span className="text-xs text-muted-foreground">
              {doc.chunkCount} chunk · {doc.language}
            </span>
          </div>
          <Button
            size="sm"
            variant="ghost"
            className="text-destructive hover:text-destructive"
            disabled={removeMutation.isPending}
            onClick={() => removeMutation.mutate(doc.id)}
          >
            <Trash2 className="h-4 w-4" />
          </Button>
        </div>
      ))}
    </div>
  );
}
