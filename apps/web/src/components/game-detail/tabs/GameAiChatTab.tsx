'use client';

import { useQuery } from '@tanstack/react-query';
import { ExternalLink, MessageCircle } from 'lucide-react';
import Link from 'next/link';

import {
  getKbPipColor,
  type KbPipState,
} from '@/components/ui/data-display/meeple-card/parts/ManaPips';
import { Button } from '@/components/ui/primitives/button';
import { useChatPanel } from '@/hooks/useChatPanel';
import { api } from '@/lib/api';
import type { ChatThreadDto } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

import type { GameTabProps } from './types';

// ========== Helpers ==========

function kbStatusLabel(state: KbPipState): string {
  if (state.kbIndexedCount > 0) return 'Knowledge Base pronta';
  if (state.kbProcessingCount > 0) return 'Indicizzazione in corso...';
  return 'Nessun documento caricato';
}

function formatDate(iso: string): string {
  try {
    return new Intl.DateTimeFormat('it-IT', {
      day: 'numeric',
      month: 'short',
      hour: '2-digit',
      minute: '2-digit',
    }).format(new Date(iso));
  } catch {
    return '';
  }
}

// ========== Component ==========

/**
 * AI Chat tab — shows KB status, chat CTAs, and existing conversation threads.
 * Replaces the former placeholder with real data fetching and navigation.
 */
export function GameAiChatTab({ gameId, variant, isNotInLibrary }: GameTabProps) {
  const containerClass = cn('flex flex-col', variant === 'desktop' ? 'gap-4 p-6' : 'gap-3 p-4');
  const { open: openChatPanel } = useChatPanel();

  // ---- KB documents for status ----
  const { data: kbDocs } = useQuery({
    queryKey: ['game-kb-docs', gameId],
    queryFn: () => api.knowledgeBase.getGameDocuments(gameId),
    enabled: !!gameId && !isNotInLibrary,
    staleTime: 2 * 60_000,
  });

  // ---- Existing chat threads for this game ----
  const { data: threads } = useQuery<ChatThreadDto[]>({
    queryKey: ['game-chat-threads', gameId],
    queryFn: () => api.chat.getThreadsByGame(gameId),
    enabled: !!gameId && !isNotInLibrary,
    staleTime: 60_000,
  });

  // ---- Not in library → CTA ----
  if (isNotInLibrary) {
    return (
      <div role="tabpanel" aria-labelledby="game-tab-aiChat" className={containerClass}>
        <p className="text-sm text-muted-foreground">
          Aggiungi il gioco alla libreria per chattare con l&apos;AI sulle sue regole.
        </p>
      </div>
    );
  }

  // ---- KB status ----
  const kbState: KbPipState = {
    kbIndexedCount: kbDocs?.filter(d => d.status === 'indexed').length ?? 0,
    kbProcessingCount: kbDocs?.filter(d => d.status === 'processing').length ?? 0,
  };
  const pipColor = getKbPipColor(kbState);
  const isKbReady = kbState.kbIndexedCount > 0;

  return (
    <div role="tabpanel" aria-labelledby="game-tab-aiChat" className={containerClass}>
      {/* Header */}
      <h3
        className={cn(
          'font-heading font-bold text-foreground',
          variant === 'desktop' ? 'text-lg' : 'text-base'
        )}
      >
        AI Chat
      </h3>

      {/* KB Status indicator */}
      <div className="flex items-center gap-2">
        <span
          className="inline-block h-2.5 w-2.5 rounded-full"
          style={{ backgroundColor: pipColor }}
          aria-hidden="true"
        />
        <span className="text-sm text-muted-foreground">{kbStatusLabel(kbState)}</span>
      </div>

      {/* CTAs — only when KB is indexed */}
      {isKbReady && (
        <div className="flex flex-wrap items-center gap-2">
          <Button
            size="sm"
            onClick={() =>
              openChatPanel({
                id: gameId,
                name: '',
                pdfCount: kbDocs?.length ?? 0,
                kbStatus: 'ready',
              })
            }
          >
            <MessageCircle className="mr-1.5 h-4 w-4" />
            Chatta con AI
          </Button>
          <Button size="sm" variant="outline" asChild>
            <Link href={`/chat/new?game=${encodeURIComponent(gameId)}`}>
              <ExternalLink className="mr-1.5 h-4 w-4" />
              Apri chat completa
            </Link>
          </Button>
        </div>
      )}

      {/* No documents message */}
      {!isKbReady && kbState.kbProcessingCount === 0 && (
        <p className="text-sm text-muted-foreground">
          Carica un PDF delle regole per abilitare la chat AI.
        </p>
      )}

      {/* Existing threads */}
      {threads && threads.length > 0 && (
        <div className="flex flex-col gap-2">
          <h4 className="text-sm font-semibold text-muted-foreground">Conversazioni precedenti</h4>
          <ul className="flex flex-col gap-1">
            {threads.map(thread => (
              <li key={thread.id}>
                <Link
                  href={`/chat/${thread.id}`}
                  className="flex items-center justify-between rounded-md px-3 py-2 text-sm transition-colors hover:bg-muted"
                >
                  <span className="truncate font-medium">
                    {thread.title ?? `Chat del ${formatDate(thread.createdAt)}`}
                  </span>
                  <span className="ml-2 shrink-0 text-xs text-muted-foreground">
                    {thread.messageCount} msg
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
