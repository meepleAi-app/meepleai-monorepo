'use client';

import { useState } from 'react';
import type { JSX } from 'react';

import { keepPreviousData, useQuery } from '@tanstack/react-query';

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
  AdminReferenceQuestion,
  AdminReferenceQuestionGame,
} from '@/lib/api/schemas/knowledge-base.schemas';

const ALL = 'all';
const IN_GATE = 'in';
const OUT_OF_GATE = 'out';

function gameLabel(game: { gameId: string; gameName: string | null }): string {
  return game.gameName ?? `Gioco ${game.gameId.slice(0, 8)}…`;
}

function inGateFilter(gate: string): boolean | undefined {
  if (gate === IN_GATE) return true;
  if (gate === OUT_OF_GATE) return false;
  return undefined;
}

/**
 * #4470 (ADR-095 fetta 7b, D8): le domande di riferimento con cui si valuta l'agente, in sola lettura.
 * Il set lo scrive l'utente, il seed lo carica; qui si leggono, per gioco e per gate di pubblicazione.
 */
export default function ReferenceQuestionsPage(): JSX.Element {
  const [gameId, setGameId] = useState<string>(ALL);
  const [gate, setGate] = useState<string>(ALL);

  const { data, isLoading, isError } = useQuery({
    queryKey: ['admin-reference-questions', gameId, gate],
    queryFn: () =>
      api.knowledgeBase.getAdminReferenceQuestions({
        gameId: gameId === ALL ? undefined : gameId,
        inGate: inGateFilter(gate),
      }),
    // The games of the filter come with the list: keep them on screen while a new filter loads.
    placeholderData: keepPreviousData,
  });

  // #4255: AdminShell scopes the dark theme without a text colour, so the page declares its own.
  return (
    <div className="space-y-6 text-foreground">
      <div>
        <h2 className="font-quicksand text-xl font-semibold text-foreground">
          Domande di riferimento
        </h2>
        <p className="text-sm text-muted-foreground mt-1">
          Le domande con cui si valuta l&apos;agente, con la risposta attesa. Il set lo scrive
          l&apos;utente e lo carica il seed: qui si leggono soltanto.
        </p>
      </div>

      {isLoading && <div className="animate-pulse h-24 rounded-lg bg-muted" />}

      {!isLoading && (isError || !data) && (
        <p
          role="alert"
          className="rounded-lg border border-entity-event/40 bg-card px-3.5 py-2.5 text-sm text-foreground"
        >
          Non è stato possibile caricare le domande di riferimento. Ricarica la pagina per
          riprovare.
        </p>
      )}

      {data && (
        <section className="rounded-[10px] border border-border/60 bg-card overflow-hidden">
          <div className="flex flex-wrap items-center gap-2.5 border-b border-border/60 bg-background px-3.5 py-2.5">
            <GameFilter games={data.games} value={gameId} onChange={setGameId} />
            <Select value={gate} onValueChange={setGate}>
              <SelectTrigger
                aria-label="Gate"
                className="h-7 w-40 text-[11px] font-quicksand font-bold bg-card border-border/60"
              >
                <SelectValue placeholder="Tutte" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value={ALL}>Tutte</SelectItem>
                <SelectItem value={IN_GATE}>Nel gate</SelectItem>
                <SelectItem value={OUT_OF_GATE}>Fuori dal gate</SelectItem>
              </SelectContent>
            </Select>
            <span className="font-mono text-[10px] text-muted-foreground ml-auto">
              {data.items.length} domande
            </span>
          </div>

          <div className="p-3.5 space-y-2">
            {data.items.map(item => (
              <ReferenceQuestionCard key={item.code} item={item} />
            ))}

            {data.items.length === 0 && (
              <div className="text-center py-8 font-mono text-[11px] text-muted-foreground">
                {data.games.length === 0
                  ? 'Nessuna domanda di riferimento caricata. Il seed le carica dal set all’avvio, nei profili Staging e Dev.'
                  : 'Nessuna domanda con questi filtri'}
              </div>
            )}
          </div>
        </section>
      )}
    </div>
  );
}

function GameFilter({
  games,
  value,
  onChange,
}: {
  games: AdminReferenceQuestionGame[];
  value: string;
  onChange: (value: string) => void;
}): JSX.Element {
  return (
    <Select value={value} onValueChange={onChange}>
      <SelectTrigger
        aria-label="Gioco"
        className="h-7 w-56 text-[11px] font-quicksand font-bold bg-card border-border/60"
      >
        <SelectValue placeholder="Tutti i giochi" />
      </SelectTrigger>
      <SelectContent>
        <SelectItem value={ALL}>Tutti i giochi</SelectItem>
        {games.map(g => (
          <SelectItem key={g.gameId} value={g.gameId}>
            {`${gameLabel(g)} (${g.questionCount})`}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

function ReferenceQuestionCard({ item }: { item: AdminReferenceQuestion }): JSX.Element {
  const labelId = `reference-question-${item.code}`;

  return (
    <article
      aria-labelledby={labelId}
      className="rounded-lg border border-border/60 bg-background p-3"
    >
      <div className="flex items-center gap-2 mb-1.5 flex-wrap">
        <span
          id={labelId}
          className="font-mono text-[10.5px] font-bold text-entity-kb bg-card border border-border/60 rounded px-1.5 py-px"
        >
          {item.code}
        </span>
        <span className="font-quicksand text-[12px] font-bold text-foreground">
          {gameLabel(item)}
        </span>
        <span className="font-mono text-[10px] text-muted-foreground">
          {item.category} · {item.difficulty} · {item.questionType}
        </span>
        {item.inGate && (
          <Badge
            variant="outline"
            className="border-0 text-[10px] font-mono font-bold uppercase tracking-wide rounded-full px-2 py-0.5 bg-entity-kb/12 text-entity-kb"
          >
            Nel gate
          </Badge>
        )}
        {item.outOfCorpus && (
          <Badge
            variant="outline"
            className="border-0 text-[10px] font-mono font-bold uppercase tracking-wide rounded-full px-2 py-0.5 bg-entity-event/12 text-entity-event"
          >
            Fuori dal corpus
          </Badge>
        )}
      </div>

      <p className="text-[13px] font-medium text-foreground leading-relaxed">{item.question}</p>
      <p className="text-[12.5px] text-muted-foreground leading-relaxed mt-1">
        {item.expectedAnswer}
      </p>
      <p className="font-mono text-[10px] text-muted-foreground mt-1.5">{item.sourceReference}</p>
    </article>
  );
}
