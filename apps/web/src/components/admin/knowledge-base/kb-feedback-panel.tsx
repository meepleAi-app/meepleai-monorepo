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
import type {
  AdminKbFeedbackGame,
  AdminKbFeedbackVersion,
} from '@/lib/api/schemas/knowledge-base.schemas';

import { versionKey, versionLabel } from './kb-feedback-version-summary';

interface Props {
  /** Le versioni con feedback, dal riepilogo (#4469). */
  versions: AdminKbFeedbackVersion[];
  /** I giochi con feedback, per sceglierli per nome invece che per UUID (#4469). */
  games: AdminKbFeedbackGame[];
  /** La versione filtrata, con la chiave di {@link versionKey}; null per tutte. */
  version: string | null;
  onVersionChange: (key: string | null) => void;
}

const ALL = 'all';
const PAGE_SIZE = 20;

export function KbFeedbackPanel({ versions, games, version, onVersionChange }: Props) {
  const [gameId, setGameId] = useState<string>(ALL);
  const [outcomeFilter, setOutcomeFilter] = useState<string>(ALL);
  const [page, setPage] = useState(1);

  // #1665: a filter that changes while paged past page 1 used to leave `page` stale and surface an
  // empty page. The version filter can also change from the summary, so reset on it too.
  useEffect(() => {
    setPage(1);
  }, [version]);

  const { data, isLoading } = useQuery({
    queryKey: ['admin-kb-feedback', version, gameId, outcomeFilter, page],
    queryFn: () =>
      api.knowledgeBase.getAdminKbFeedback({
        version: version ?? undefined,
        gameId: gameId === ALL ? undefined : gameId,
        outcome: outcomeFilter === ALL ? undefined : (outcomeFilter as 'helpful' | 'not_helpful'),
        page,
        pageSize: PAGE_SIZE,
      }),
  });

  return (
    <section className="rounded-[10px] border border-border/60 bg-card overflow-hidden">
      {/* Panel header: filters + meta */}
      <div className="flex flex-wrap items-center gap-2.5 border-b border-border/60 bg-background px-3.5 py-2.5">
        <Select
          value={version ?? ALL}
          onValueChange={value => onVersionChange(value === ALL ? null : value)}
        >
          <SelectTrigger
            aria-label="Versione del profilo"
            className="h-7 w-40 text-[11px] font-quicksand font-bold bg-card border-border/60"
          >
            <SelectValue placeholder="Tutte le versioni" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Tutte le versioni</SelectItem>
            {versions.map(v => (
              <SelectItem key={versionKey(v)} value={versionKey(v)}>
                {versionLabel(v)}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select
          value={gameId}
          onValueChange={value => {
            setGameId(value);
            setPage(1);
          }}
        >
          <SelectTrigger
            aria-label="Gioco"
            className="h-7 w-48 text-[11px] font-quicksand font-bold bg-card border-border/60"
          >
            <SelectValue placeholder="Tutti i giochi" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Tutti i giochi</SelectItem>
            {games.map(g => (
              <SelectItem key={g.gameId} value={g.gameId}>
                {g.gameName ?? `Gioco ${g.gameId.slice(0, 8)}…`}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select
          value={outcomeFilter}
          onValueChange={value => {
            setOutcomeFilter(value);
            // #1665: reset to page 1 so the filtered set is not paged past its end.
            setPage(1);
          }}
        >
          <SelectTrigger
            aria-label="Esito"
            className="h-7 w-36 text-[11px] font-quicksand font-bold bg-card border-border/60"
          >
            <SelectValue placeholder="Tutti" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Tutti</SelectItem>
            <SelectItem value="helpful">Utili</SelectItem>
            <SelectItem value="not_helpful">Non utili</SelectItem>
          </SelectContent>
        </Select>
        {data && (
          <span className="font-mono text-[10px] text-muted-foreground ml-auto">
            {data.total} feedback
          </span>
        )}
      </div>

      {/* Panel body */}
      <div className="p-3.5 space-y-2">
        {/* Loading skeleton */}
        {isLoading && <div className="animate-pulse h-48 rounded-lg bg-muted" />}

        {/* Feedback list */}
        {data?.items.map(item => (
          <article
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
                {/* Game */}
                <span className="font-quicksand text-[12px] font-bold text-foreground">
                  {item.gameName ?? `Gioco ${item.gameId.slice(0, 8)}…`}
                </span>
                {/* Profile version */}
                <span className="font-mono text-[10.5px] font-bold text-entity-kb bg-background border border-border/60 rounded px-1.5 py-px">
                  {versionLabel(item)}
                </span>
                {/* Date */}
                <span className="font-mono text-[10px] text-muted-foreground">
                  {new Date(item.createdAt).toLocaleDateString('it-IT')}
                </span>
              </div>

              {/* The answer being judged */}
              <p className="text-[12.5px] text-muted-foreground leading-relaxed mb-1">
                {item.answerExcerpt ?? 'Risposta non più disponibile'}
              </p>

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
          </article>
        ))}

        {/* Empty state */}
        {data && data.items.length === 0 && !isLoading && (
          <div className="text-center py-8 font-mono text-[11px] text-muted-foreground">
            Nessun feedback trovato
          </div>
        )}

        {/* Pagination */}
        {data && data.total > PAGE_SIZE && (
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
              disabled={page * PAGE_SIZE >= data.total}
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
