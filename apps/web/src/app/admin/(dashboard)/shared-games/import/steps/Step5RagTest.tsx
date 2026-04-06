'use client';

/**
 * Step 5: RAG Agent Test
 *
 * - Waits for indexing to complete (useIndexingStatus polling)
 * - Interactive Q&A panel: user sends queries, response shown with chunk sources
 * - Multi-turn: history of questions/answers within the session
 * - CTAs: "Vai alla scheda gioco" | "Pubblica gioco" | "Aggiungi documentazione"
 */

import { type FormEvent, useCallback, useEffect, useRef, useState } from 'react';
import type { JSX } from 'react';

import {
  AlertCircle,
  BookOpen,
  CheckCircle2,
  ChevronDown,
  ChevronUp,
  ExternalLink,
  FileText,
  Loader2,
  Send,
} from 'lucide-react';
import Link from 'next/link';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useIndexingStatus } from '@/hooks/useIndexingStatus';
import { api } from '@/lib/api';
import { useGameImportWizardStore, type RagTestMessage } from '@/stores/useGameImportWizardStore';

export function Step5RagTest(): JSX.Element {
  const {
    importResult,
    reviewedMetadata,
    ragTestHistory,
    isIndexingReady,
    setIndexingReady,
    addRagMessage,
    updateRagMessage,
  } = useGameImportWizardStore();

  const gameId = importResult?.gameId ?? null;
  const gameTitle = reviewedMetadata?.title ?? 'il gioco';

  const {
    isReady,
    isFailed,
    chunkCount,
    isPolling,
    error: statusError,
  } = useIndexingStatus(gameId, {
    enabled: !!gameId && !isIndexingReady,
    onReady: () => setIndexingReady(true),
  });

  const ready = isIndexingReady || isReady;

  const [query, setQuery] = useState('');
  const [isSending, setIsSending] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  // Auto-scroll to latest message
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [ragTestHistory]);

  const handleSend = useCallback(
    async (e: FormEvent) => {
      e.preventDefault();
      const q = query.trim();
      if (!q || !gameId || isSending) return;

      setQuery('');
      setIsSending(true);

      const msgId = addRagMessage(q);

      try {
        const result = await api.sharedGames.searchKnowledgeBase(gameId, q);
        updateRagMessage(msgId, {
          isLoading: false,
          answer: result.answer ?? 'Nessuna risposta trovata.',
          sources: result.chunks?.map(c => ({
            text: c.text,
            pageNumber: c.pageNumber ?? undefined,
          })),
        });
      } catch (err) {
        updateRagMessage(msgId, {
          isLoading: false,
          error: err instanceof Error ? err.message : 'Ricerca fallita',
        });
      } finally {
        setIsSending(false);
      }
    },
    [query, gameId, isSending, addRagMessage, updateRagMessage]
  );

  // ── Indexing wait skeleton ──────────────────────────────────────────────────
  if (!ready && !isFailed) {
    return (
      <div className="space-y-6 py-4">
        <div className="flex flex-col items-center gap-3 text-center">
          <Loader2 className="h-10 w-10 animate-spin text-primary" />
          <div>
            <p className="font-semibold">Indicizzazione in corso...</p>
            <p className="text-sm text-muted-foreground">
              Il regolamento di <strong>{gameTitle}</strong> viene indicizzato per il RAG.
              {isPolling && ' Aggiornamento in corso...'}
            </p>
          </div>
          {chunkCount != null && chunkCount > 0 && (
            <p className="text-xs text-muted-foreground">{chunkCount} chunk elaborati</p>
          )}
        </div>

        {statusError && (
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertTitle>Errore stato indicizzazione</AlertTitle>
            <AlertDescription className="text-sm">{statusError}</AlertDescription>
          </Alert>
        )}

        {/* Skeleton placeholder for future chat */}
        <div className="space-y-3 rounded-md border bg-muted/20 p-4">
          <Skeleton className="h-4 w-3/4" />
          <Skeleton className="h-4 w-1/2" />
          <Skeleton className="h-4 w-2/3" />
        </div>
      </div>
    );
  }

  // ── Indexing failed ─────────────────────────────────────────────────────────
  if (isFailed) {
    return (
      <div className="space-y-4 py-4">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Indicizzazione fallita</AlertTitle>
          <AlertDescription className="text-sm">
            L&apos;indicizzazione RAG non è riuscita. Il gioco è stato creato ma il test RAG non è
            disponibile. Puoi ritentare dalla scheda del gioco.
          </AlertDescription>
        </Alert>
        <Ctas gameId={gameId} />
      </div>
    );
  }

  // ── Ready ───────────────────────────────────────────────────────────────────
  return (
    <div className="space-y-6">
      {/* Status badge */}
      <div className="flex items-center gap-2 rounded-md border bg-green-50 px-4 py-2 text-sm text-green-700 dark:bg-green-900/20 dark:text-green-400">
        <CheckCircle2 className="h-4 w-4 shrink-0" />
        <span>
          <strong>{gameTitle}</strong> — indicizzazione completata
          {chunkCount != null && ` (${chunkCount} chunk)`}
        </span>
      </div>

      {/* Chat history */}
      <div className="max-h-[420px] overflow-y-auto space-y-4 rounded-md border bg-muted/10 p-4">
        {ragTestHistory.length === 0 && (
          <p className="py-8 text-center text-sm text-muted-foreground">
            Scrivi una domanda per testare il RAG del regolamento.
          </p>
        )}

        {ragTestHistory.map(msg => (
          <ChatMessage key={msg.id} message={msg} />
        ))}
        <div ref={messagesEndRef} />
      </div>

      {/* Input form */}
      <form onSubmit={handleSend} className="flex gap-2">
        <Input
          value={query}
          onChange={e => setQuery(e.target.value)}
          placeholder="Fai una domanda sul regolamento..."
          disabled={isSending}
          className="flex-1"
        />
        <Button type="submit" disabled={!query.trim() || isSending} size="icon">
          {isSending ? <Loader2 className="h-4 w-4 animate-spin" /> : <Send className="h-4 w-4" />}
          <span className="sr-only">Invia</span>
        </Button>
      </form>

      {/* CTAs */}
      <Ctas gameId={gameId} />
    </div>
  );
}

// ── Chat message ───────────────────────────────────────────────────────────────

function ChatMessage({ message }: { message: RagTestMessage }): JSX.Element {
  const [sourcesOpen, setSourcesOpen] = useState(false);

  return (
    <div className="space-y-2">
      {/* Question */}
      <div className="flex justify-end">
        <div className="max-w-[80%] rounded-lg bg-primary px-3 py-2 text-sm text-primary-foreground">
          {message.question}
        </div>
      </div>

      {/* Answer */}
      <div className="flex justify-start">
        <div className="max-w-[85%] space-y-2">
          <div className="rounded-lg border bg-background px-3 py-2 text-sm">
            {message.isLoading ? (
              <span className="flex items-center gap-2 text-muted-foreground">
                <Loader2 className="h-3.5 w-3.5 animate-spin" />
                Ricerca in corso...
              </span>
            ) : message.error ? (
              <span className="text-destructive">{message.error}</span>
            ) : (
              message.answer
            )}
          </div>

          {/* Sources */}
          {!message.isLoading &&
            !message.error &&
            message.sources &&
            message.sources.length > 0 && (
              <div className="text-xs">
                <button
                  onClick={() => setSourcesOpen(o => !o)}
                  className="flex items-center gap-1 text-muted-foreground hover:text-foreground"
                >
                  <FileText className="h-3 w-3" />
                  {message.sources.length} fonte{message.sources.length > 1 ? 'i' : ''}
                  {sourcesOpen ? (
                    <ChevronUp className="h-3 w-3" />
                  ) : (
                    <ChevronDown className="h-3 w-3" />
                  )}
                </button>

                {sourcesOpen && (
                  <div className="mt-1 space-y-1.5">
                    {message.sources.map((src, i) => (
                      <div key={i} className="rounded border bg-muted/40 px-2 py-1.5">
                        {src.pageNumber != null && (
                          <p className="mb-0.5 font-medium text-muted-foreground">
                            Pagina {src.pageNumber}
                          </p>
                        )}
                        <p className="line-clamp-3 text-muted-foreground">{src.text}</p>
                      </div>
                    ))}
                  </div>
                )}
              </div>
            )}
        </div>
      </div>
    </div>
  );
}

// ── CTAs ───────────────────────────────────────────────────────────────────────

function Ctas({ gameId }: { gameId: string | null }): JSX.Element {
  return (
    <div className="flex flex-wrap gap-2 border-t pt-4">
      {gameId && (
        <>
          <Link href={`/admin/shared-games/${gameId}`}>
            <Button variant="default" size="sm">
              <ExternalLink className="mr-1.5 h-3.5 w-3.5" />
              Vai alla scheda gioco
            </Button>
          </Link>
          <Link href={`/admin/shared-games/${gameId}/documents`}>
            <Button variant="outline" size="sm">
              <BookOpen className="mr-1.5 h-3.5 w-3.5" />
              Aggiungi documentazione
            </Button>
          </Link>
        </>
      )}
    </div>
  );
}
