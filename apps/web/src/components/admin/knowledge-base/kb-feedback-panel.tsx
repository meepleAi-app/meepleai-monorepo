'use client';

import { useEffect, useState } from 'react';

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

const OUTCOME_ALL = 'all';

export function KbFeedbackPanel({ gameId }: Props) {
  const [outcomeFilter, setOutcomeFilter] = useState<string>(OUTCOME_ALL);
  const [page, setPage] = useState(1);

  // #1665: changing the gameId prop while paged past page 1 used to leave
  // `page` stale and surface an empty page. Reset whenever gameId changes.
  useEffect(() => {
    setPage(1);
  }, [gameId]);

  const { data, isLoading } = useQuery({
    queryKey: ['admin-kb-feedback', gameId, outcomeFilter, page],
    queryFn: () =>
      api.knowledgeBase.getAdminKbFeedback(gameId, {
        outcome:
          outcomeFilter === OUTCOME_ALL ? undefined : (outcomeFilter as 'helpful' | 'not_helpful'),
        page,
        pageSize: 20,
      }),
  });

  return (
    <section className="rounded-[10px] border border-border/60 bg-card overflow-hidden">
      {/* Panel header: filter + meta */}
      <div className="flex items-center gap-2.5 border-b border-border/60 bg-background px-3.5 py-2.5">
        <Select
          value={outcomeFilter}
          onValueChange={value => {
            setOutcomeFilter(value);
            // #1665: reset to page 1 so the filtered set is not paged past its end.
            setPage(1);
          }}
        >
          <SelectTrigger className="h-7 w-36 text-[11px] font-quicksand font-bold bg-card border-border/60">
            <SelectValue placeholder="Tutti" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={OUTCOME_ALL}>Tutti</SelectItem>
            <SelectItem value="helpful">Utili</SelectItem>
            <SelectItem value="not_helpful">Non utili</SelectItem>
          </SelectContent>
        </Select>
        {data && (
          <span className="font-mono text-[10px] text-muted-foreground ml-auto">
            {data.total} feedback totali
          </span>
        )}
      </div>

      {/* Panel body */}
      <div className="p-3.5 space-y-2">
        {/* Loading skeleton */}
        {isLoading && <div className="animate-pulse h-48 rounded-lg bg-muted" />}

        {/* Feedback list */}
        {data?.items.map(item => (
          <div
            key={item.id}
            className={`flex items-start gap-3 rounded-lg border p-3 bg-background ${
              item.outcome === 'helpful' ? 'border-entity-toolkit/30' : 'border-entity-event/30'
            }`}
          >
            {/* Thumb icon */}
            {item.outcome === 'helpful' ? (
              <ThumbsUp className="h-4 w-4 text-entity-toolkit mt-0.5 shrink-0" />
            ) : (
              <ThumbsDown className="h-4 w-4 text-entity-event mt-0.5 shrink-0" />
            )}

            {/* Body */}
            <div className="flex-1 min-w-0">
              <div className="flex items-center gap-2 mb-1 flex-wrap">
                {/* Outcome badge */}
                <Badge
                  variant="outline"
                  className={`border-0 text-[10px] font-mono font-bold uppercase tracking-wide rounded-full px-2 py-0.5 ${
                    item.outcome === 'helpful'
                      ? 'bg-entity-toolkit/12 text-entity-toolkit'
                      : 'bg-entity-event/12 text-entity-event'
                  }`}
                >
                  {item.outcome === 'helpful' ? 'Utile' : 'Non utile'}
                </Badge>
                {/* Message ID */}
                <span className="font-mono text-[10.5px] font-bold text-entity-kb bg-background border border-border/60 rounded px-1.5 py-px">
                  msg: {item.messageId.slice(0, 8)}&hellip;
                </span>
                {/* Date */}
                <span className="font-mono text-[10px] text-muted-foreground">
                  {new Date(item.createdAt).toLocaleDateString('it-IT')}
                </span>
              </div>

              {/* Optional comment */}
              {item.comment && (
                <p
                  className={`text-[12.5px] text-foreground leading-relaxed bg-background border-l-2 pl-2.5 pr-2 py-1.5 rounded-r-[6px] flex items-start gap-1 ${
                    item.outcome === 'helpful'
                      ? 'border-entity-toolkit/50'
                      : 'border-entity-event/50'
                  }`}
                >
                  <MessageSquare className="h-3 w-3 mt-0.5 shrink-0 text-muted-foreground" />
                  {item.comment}
                </p>
              )}
            </div>
          </div>
        ))}

        {/* Empty state */}
        {data && data.items.length === 0 && !isLoading && (
          <div className="text-center py-8 font-mono text-[11px] text-muted-foreground">
            Nessun feedback trovato
          </div>
        )}

        {/* Pagination */}
        {data && data.total > 20 && (
          <div className="flex items-center justify-center gap-2 pt-2 border-t border-border/60 mt-2">
            <button
              type="button"
              className="rounded-md border border-border/60 bg-card px-2 py-1 text-[11px] font-quicksand font-bold hover:bg-muted disabled:opacity-40 disabled:cursor-not-allowed focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
              disabled={page === 1}
              onClick={() => setPage(p => p - 1)}
            >
              &larr; Prec
            </button>
            <span className="font-mono text-[11px] text-muted-foreground px-1">Pag {page}</span>
            <button
              type="button"
              className="rounded-md border border-border/60 bg-card px-2 py-1 text-[11px] font-quicksand font-bold hover:bg-muted disabled:opacity-40 disabled:cursor-not-allowed focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
              disabled={page * 20 >= data.total}
              onClick={() => setPage(p => p + 1)}
            >
              Succ &rarr;
            </button>
          </div>
        )}
      </div>
    </section>
  );
}
