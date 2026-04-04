'use client';

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { MessageSquare, ThumbsDown, ThumbsUp } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { api } from '@/lib/api';

interface Props {
  gameId: string;
}

export function KbFeedbackPanel({ gameId }: Props) {
  const [outcomeFilter, setOutcomeFilter] = useState<string>('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useQuery({
    queryKey: ['admin-kb-feedback', gameId, outcomeFilter, page],
    queryFn: () =>
      api.knowledgeBase.getAdminKbFeedback(gameId, {
        outcome: outcomeFilter ? (outcomeFilter as 'helpful' | 'not_helpful') : undefined,
        page,
        pageSize: 20,
      }),
  });

  return (
    <div className="space-y-4">
      <div className="flex items-center gap-3">
        <Select value={outcomeFilter} onValueChange={setOutcomeFilter}>
          <SelectTrigger className="w-40">
            <SelectValue placeholder="Tutti" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="">Tutti</SelectItem>
            <SelectItem value="helpful">Utili</SelectItem>
            <SelectItem value="not_helpful">Non utili</SelectItem>
          </SelectContent>
        </Select>
        {data && (
          <span className="text-sm text-muted-foreground">{data.total} feedback totali</span>
        )}
      </div>

      {isLoading && <div className="animate-pulse h-48 bg-muted rounded-lg" />}

      <div className="space-y-2">
        {data?.items.map(item => (
          <div
            key={item.id}
            className="flex items-start gap-3 p-3 border rounded-lg bg-white/60 dark:bg-zinc-800/60"
          >
            {item.outcome === 'helpful' ? (
              <ThumbsUp className="h-4 w-4 text-green-500 mt-0.5 shrink-0" />
            ) : (
              <ThumbsDown className="h-4 w-4 text-red-500 mt-0.5 shrink-0" />
            )}
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 mb-1">
                <Badge
                  variant={item.outcome === 'helpful' ? 'default' : 'destructive'}
                  className="text-xs"
                >
                  {item.outcome === 'helpful' ? 'Utile' : 'Non utile'}
                </Badge>
                <span className="text-xs text-muted-foreground font-mono">
                  msg: {item.messageId.slice(0, 8)}&hellip;
                </span>
                <span className="text-xs text-muted-foreground">
                  {new Date(item.createdAt).toLocaleDateString('it-IT')}
                </span>
              </div>
              {item.comment && (
                <p className="text-sm text-muted-foreground flex items-start gap-1">
                  <MessageSquare className="h-3 w-3 mt-0.5 shrink-0" />
                  {item.comment}
                </p>
              )}
            </div>
          </div>
        ))}

        {data && data.items.length === 0 && !isLoading && (
          <div className="text-center py-8 text-muted-foreground">Nessun feedback trovato</div>
        )}
      </div>

      {data && data.total > 20 && (
        <div className="flex justify-center gap-2">
          <button
            className="px-3 py-1 text-sm border rounded disabled:opacity-50"
            disabled={page === 1}
            onClick={() => setPage(p => p - 1)}
          >
            &larr; Prec
          </button>
          <span className="px-3 py-1 text-sm">Pag {page}</span>
          <button
            className="px-3 py-1 text-sm border rounded disabled:opacity-50"
            disabled={page * 20 >= data.total}
            onClick={() => setPage(p => p + 1)}
          >
            Succ &rarr;
          </button>
        </div>
      )}
    </div>
  );
}
