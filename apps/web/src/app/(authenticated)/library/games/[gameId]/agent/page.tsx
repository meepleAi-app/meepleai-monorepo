/**
 * Agent Configuration Page
 * Issue #4948: Agent config page redesign
 *
 * 2-column layout:
 * - Left: KbStatusPanel — PDF indexing status, chunk count, add document CTA
 * - Right: AgentConfigForm — typology, strategy, cost estimate, create CTA
 * - Bottom: Recent chat threads for this game
 */

'use client';

import { useCallback, useState } from 'react';

import { ArrowLeft, Bot, Clock, MessageSquare } from 'lucide-react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';

import {
  AgentConfigForm,
  type AgentStrategy,
  type AgentTypology,
} from '@/components/library/AgentConfigForm';
import { KbStatusPanel } from '@/components/library/KbStatusPanel';
import { Button } from '@/components/ui/primitives/button';
import { useRecentChatSessions } from '@/hooks/queries/useChatSessions';
import { useCreateAgentFlow } from '@/hooks/queries/useCreateAgentFlow';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
import type { ChatSessionSummaryDto } from '@/lib/api/schemas/chat-sessions.schemas';
import { cn } from '@/lib/utils';

// ============================================================================
// Recent chat thread row
// ============================================================================

function ChatThreadRow({ session }: { session: ChatSessionSummaryDto }) {
  const dateLabel = session.lastMessageAt
    ? new Date(session.lastMessageAt).toLocaleDateString('it-IT', {
        day: '2-digit',
        month: 'short',
        year: 'numeric',
      })
    : null;

  return (
    <Link
      href={`/chat/${session.id}`}
      className={cn(
        'flex items-center gap-3 px-4 py-3 rounded-lg border',
        'border-border hover:border-primary/40 hover:bg-primary/5',
        'transition-all duration-150'
      )}
    >
      <MessageSquare className="h-4 w-4 text-muted-foreground shrink-0" />
      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium truncate">{session.title ?? 'Chat senza titolo'}</p>
        {session.lastMessagePreview && (
          <p className="text-xs text-muted-foreground truncate mt-0.5">
            {session.lastMessagePreview}
          </p>
        )}
      </div>
      {dateLabel && <span className="text-xs text-muted-foreground shrink-0">{dateLabel}</span>}
      <ArrowLeft className="h-3.5 w-3.5 text-muted-foreground rotate-180 shrink-0" />
    </Link>
  );
}

// ============================================================================
// Main Page
// ============================================================================

export default function AgentConfigPage() {
  const params = useParams();
  const router = useRouter();
  const gameId = (params?.gameId as string) || '';

  // Game details (for breadcrumb title)
  const { data: gameDetail, isLoading: gameLoading } = useLibraryGameDetail(gameId);

  // hasIndexedKb is derived from KbStatusPanel via onStatusChange callback
  // to avoid a duplicate usePdfProcessingStatus subscription (KbStatusPanel owns the hook)
  const [hasIndexedKb, setHasIndexedKb] = useState(false);
  const handleStatusChange = useCallback((isIndexed: boolean) => {
    setHasIndexedKb(isIndexed);
  }, []);

  // Recent chat sessions for this game (last 5)
  // Note: limited to 100 most-recent sessions across all games to reduce risk
  // of game-specific sessions being pushed out of the window.
  const { data: chatData } = useRecentChatSessions(100);
  const gameChats = (chatData?.sessions ?? []).filter(s => s.gameId === gameId).slice(0, 5);

  // Agent creation flow — on success navigate to the new chat thread
  const { mutateAsync: createAgent } = useCreateAgentFlow({
    onSuccess: result => {
      router.push(`/chat/${result.threadId}`);
    },
  });

  const handleSave = async (typology: AgentTypology, strategy: AgentStrategy) => {
    await createAgent({
      gameId,
      addToCollection: false,
      agentType: typology,
      strategyName: strategy,
    });
  };

  const gameTitle = gameDetail?.gameTitle ?? (gameLoading ? '…' : 'Gioco');

  // Gate on gameId to prevent flash of empty state before route params resolve
  if (!gameId) return null;

  return (
    <div className="min-h-screen bg-background">
      <div className="max-w-5xl mx-auto px-4 sm:px-6 py-6 space-y-6">
        {/* Breadcrumb / Back */}
        <div>
          <Button variant="ghost" size="sm" className="-ml-2" asChild>
            <Link href={`/library/games/${gameId}`}>
              <ArrowLeft className="h-4 w-4 mr-1" />
              {gameTitle}
            </Link>
          </Button>
        </div>

        {/* Page title */}
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <Bot className="h-6 w-6 text-primary" />
            Configura il tuo agente AI
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Carica i documenti nella Knowledge Base e scegli il tipo di assistente
          </p>
        </div>

        {/* 2-column layout */}
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {/* Left: KB Status Panel — single source of truth for PDF status */}
          <KbStatusPanel gameId={gameId} onStatusChange={handleStatusChange} />

          {/* Right: Agent Config Form */}
          <div className="rounded-xl border border-border/50 bg-card p-5">
            <AgentConfigForm gameId={gameId} hasIndexedKb={hasIndexedKb} onSave={handleSave} />
          </div>
        </div>

        {/* Recent chat threads */}
        {gameChats.length > 0 && (
          <div>
            <div className="flex items-center gap-2 mb-3">
              <Clock className="h-4 w-4 text-muted-foreground" />
              <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide">
                Chat recenti con questo gioco
              </h2>
            </div>
            <div className="flex flex-col gap-2">
              {gameChats.map(session => (
                <ChatThreadRow key={session.id} session={session} />
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
