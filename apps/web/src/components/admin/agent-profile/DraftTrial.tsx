'use client';

/**
 * #4169 (6c-3, ADR-095 D8): try the draft before publishing it.
 *
 * The question goes through the same answer pipeline as the users' questions, with the draft instead
 * of the published version. The trial does not use the answer cache and does not count against the
 * plan quota; its cost is recorded as an admin operation, with the draft version.
 */
import { useEffect, useId, useRef, useState } from 'react';

import { Loader2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api } from '@/lib/api';
import { TRIAL_EVENT, tryAgentProfileDraft } from '@/lib/api/clients/agentProfileTrialStream';
import type { GameKbStatusItem } from '@/lib/api/schemas';

interface TrialResult {
  answer: string;
  sources: number;
  done: boolean;
  /** La versione provata, fissata alla domanda: una bozza salvata o pubblicata dopo non la cambia. */
  version: number | null;
}

function tokenOf(data: unknown): string {
  return typeof data === 'object' && data !== null && 'token' in data
    ? String((data as { token: unknown }).token ?? '')
    : '';
}

function sourcesOf(data: unknown): number {
  const citations = (data as { citations?: unknown } | null)?.citations;
  return Array.isArray(citations) ? citations.length : 0;
}

/** `StreamingError` arriva come `{ errorMessage, errorCode }` (camelCase del backend). */
function messageOf(data: unknown): string {
  const error = data as { errorMessage?: unknown; message?: unknown } | null;
  const message = error?.errorMessage ?? error?.message;
  return typeof message === 'string' && message ? message : 'The trial failed.';
}

export function DraftTrial({
  draftVersion,
  dirty,
}: {
  draftVersion: number | null;
  dirty: boolean;
}) {
  const titleId = useId();
  const gameId = useId();
  const questionId = useId();
  const [games, setGames] = useState<GameKbStatusItem[] | null>(null);
  const [game, setGame] = useState('');
  const [question, setQuestion] = useState('');
  const [running, setRunning] = useState(false);
  const [result, setResult] = useState<TrialResult | null>(null);
  const [error, setError] = useState<string | null>(null);
  const abort = useRef<AbortController | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        const withKb = (await api.admin.getGameKbStatuses()).items
          .filter(g => g.kbStatus !== 'none')
          .sort((a, b) => a.gameName.localeCompare(b.gameName));
        setGames(withKb);
        setGame(current => current || (withKb[0]?.gameId ?? ''));
      } catch {
        setGames([]);
      }
    })();
    return () => abort.current?.abort();
  }, []);

  const canAsk =
    draftVersion !== null && !dirty && !running && game !== '' && question.trim() !== '';

  const ask = async () => {
    abort.current?.abort();
    const controller = new AbortController();
    abort.current = controller;
    setRunning(true);
    setError(null);
    setResult({ answer: '', sources: 0, done: false, version: draftVersion });
    try {
      for await (const event of tryAgentProfileDraft(
        { gameId: game, question: question.trim() },
        controller.signal
      )) {
        if (event.type === TRIAL_EVENT.Token) {
          const token = tokenOf(event.data);
          setResult(r => (r ? { ...r, answer: r.answer + token } : r));
        } else if (event.type === TRIAL_EVENT.Citations) {
          const sources = sourcesOf(event.data);
          setResult(r => (r ? { ...r, sources } : r));
        } else if (event.type === TRIAL_EVENT.Complete) {
          setResult(r => (r ? { ...r, done: true } : r));
        } else if (event.type === TRIAL_EVENT.Error) {
          setError(messageOf(event.data));
        }
      }
    } catch (err: unknown) {
      if (!controller.signal.aborted) {
        setError(err instanceof Error && err.message ? err.message : 'The trial failed.');
      }
    } finally {
      setRunning(false);
    }
  };

  return (
    <section
      aria-labelledby={titleId}
      className="space-y-3 rounded-xl border border-border/60 bg-card/70 p-4"
    >
      <h2 id={titleId} className="text-sm font-semibold text-foreground">
        Try the draft
      </h2>
      <p className="text-xs text-muted-foreground">
        Asks a question with the draft instead of the published version, through the same answer
        pipeline. It does not use the answer cache and does not count against any plan; its cost is
        recorded as an admin operation.
      </p>

      <div className="space-y-1">
        <Label htmlFor={gameId}>Game</Label>
        <select
          id={gameId}
          value={game}
          onChange={e => setGame(e.target.value)}
          className="block h-9 w-full max-w-md rounded-md border border-border bg-background px-2 text-sm text-foreground"
        >
          {(games ?? []).map(g => (
            <option key={g.gameId} value={g.gameId}>
              {g.gameName}
            </option>
          ))}
        </select>
        {games !== null && games.length === 0 && (
          <p className="text-xs text-muted-foreground">
            No game has an indexed knowledge base yet.
          </p>
        )}
      </div>

      <div className="space-y-1">
        <Label htmlFor={questionId}>Question</Label>
        <Textarea
          id={questionId}
          value={question}
          rows={2}
          onChange={e => setQuestion(e.target.value)}
        />
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <Button onClick={() => void ask()} disabled={!canAsk}>
          {running && <Loader2 className="mr-1 h-4 w-4 animate-spin" aria-hidden="true" />}
          Ask the draft
        </Button>
        {draftVersion === null ? (
          <p className="text-xs text-muted-foreground">There is no draft to try.</p>
        ) : (
          dirty && <p className="text-xs text-muted-foreground">Save the draft to try it.</p>
        )}
      </div>

      {error && (
        <div
          role="alert"
          className="rounded-lg border border-destructive/40 bg-destructive/10 p-3 text-sm text-foreground"
        >
          {error}
        </div>
      )}

      {result && (result.answer || result.done) && (
        <div
          className="space-y-1 rounded-lg border border-border/60 bg-background p-3"
          aria-live="polite"
        >
          <p className="whitespace-pre-wrap text-sm text-foreground">{result.answer}</p>
          <p className="text-xs text-muted-foreground">
            {result.sources} {result.sources === 1 ? 'source' : 'sources'}
            {result.done && result.version !== null
              ? ` · answered by draft version ${result.version}`
              : ''}
          </p>
        </div>
      )}
    </section>
  );
}
