'use client';

import React, { useState, useEffect } from 'react';
import { AlertCircle, Bot, Gamepad2, Loader2, MessageSquare, Plus } from 'lucide-react';

import { cn } from '@/lib/utils';
import { useAgentThreads, useAgentKbDocs } from '@/hooks/queries/useAgentData';
import { useAgentStatus } from '@/hooks/useAgentStatus';
import { api } from '@/lib/api';
import { ChatThreadView } from '@/components/chat-unified/ChatThreadView';

import type { AgentDetailData, ChatThreadPreview, KbDocumentPreview } from '../types';

export interface AgentChatDrawerLayoutProps {
  data: AgentDetailData;
  className?: string;
  'data-testid'?: string;
}

// ─── AgentContextCard ────────────────────────────────────────────────────────

function AgentContextCard({ gameId, gameName }: { gameId?: string; gameName?: string }) {
  if (!gameId || !gameName) {
    return (
      <div
        data-testid="no-game-placeholder"
        className="flex items-center gap-2 rounded-md bg-slate-100 px-2 py-2 text-xs text-slate-400"
      >
        <Bot className="h-3.5 w-3.5 shrink-0" />
        <span>Nessun gioco collegato</span>
      </div>
    );
  }
  return (
    <div className="flex items-center gap-2 rounded-md bg-amber-50 px-2 py-2 text-xs font-medium text-amber-800">
      <Gamepad2 className="h-3.5 w-3.5 shrink-0 text-amber-600" />
      <span className="truncate">{gameName}</span>
    </div>
  );
}

// ─── SidebarSection ──────────────────────────────────────────────────────────

function SidebarSection({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-1">
      <p className="px-1 text-[10px] font-semibold tracking-wider text-slate-400">{label}</p>
      {children}
    </div>
  );
}

// ─── RecentThreadItem ─────────────────────────────────────────────────────────

function RecentThreadItem({
  thread,
  isSelected,
  onClick,
}: {
  thread: ChatThreadPreview;
  isSelected: boolean;
  onClick: () => void;
}) {
  const date = new Date(thread.createdAt).toLocaleDateString('it-IT', {
    day: '2-digit',
    month: '2-digit',
  });
  const preview =
    thread.firstMessagePreview.length > 50
      ? thread.firstMessagePreview.slice(0, 50) + '…'
      : thread.firstMessagePreview;

  return (
    <button
      data-testid={`thread-item-${thread.id}`}
      onClick={onClick}
      className={cn(
        'flex w-full flex-col gap-0.5 rounded-md px-2 py-1.5 text-left text-xs transition-colors hover:bg-slate-100',
        isSelected && 'border border-blue-300/60 bg-blue-100',
      )}
    >
      <div className="flex items-center justify-between gap-1 text-[10px] text-slate-400">
        <span>{date}</span>
        <span>{thread.messageCount}m</span>
      </div>
      <p className="line-clamp-2 text-slate-600">{preview}</p>
    </button>
  );
}

// ─── KbDocItem ───────────────────────────────────────────────────────────────

const statusDotClass: Record<KbDocumentPreview['status'], string> = {
  indexed: 'bg-emerald-500',
  processing: 'bg-amber-500',
  failed: 'bg-red-500',
  none: 'bg-slate-400',
};

function KbDocItem({ doc }: { doc: KbDocumentPreview }) {
  return (
    <div className="flex items-center gap-1.5 px-2 py-1">
      <span className={cn('h-1.5 w-1.5 shrink-0 rounded-full', statusDotClass[doc.status])} />
      <span className="truncate text-xs text-slate-600">{doc.fileName}</span>
    </div>
  );
}

// ─── AgentChatArea ───────────────────────────────────────────────────────────

function AgentChatArea({
  agentId,
  agentName,
  selectedThreadId,
  onThreadCreated,
  readiness,
  readinessLoading,
}: {
  agentId: string;
  agentName: string;
  selectedThreadId: string | null;
  onThreadCreated: (id: string) => void;
  readiness: ReturnType<typeof useAgentStatus>['status'];
  readinessLoading: boolean;
}) {
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState<string | null>(null);

  const onThreadCreatedRef = React.useRef(onThreadCreated);
  useEffect(() => {
    onThreadCreatedRef.current = onThreadCreated;
  });

  useEffect(() => {
    if (selectedThreadId !== 'new') return;
    let cancelled = false;

    setCreating(true);
    setCreateError(null);

    api.chat
      .createThread({ agentId, title: `Chat con ${agentName}` })
      .then((thread) => {
        if (!cancelled && thread?.id) onThreadCreatedRef.current(thread.id);
      })
      .catch((err: unknown) => {
        if (!cancelled)
          setCreateError(err instanceof Error ? err.message : 'Errore nella creazione della chat');
        if (!cancelled) setCreating(false);
      })
      .finally(() => {
        if (!cancelled) setCreating(false);
      });

    return () => {
      cancelled = true;
    };
  }, [selectedThreadId, agentId, agentName]); // onThreadCreated via ref, stabile

  // 1. Loading readiness
  if (readinessLoading) {
    return (
      <div className="flex flex-1 flex-col items-center justify-center gap-2 text-slate-400">
        <Loader2 className="h-5 w-5 animate-spin" />
        <span className="text-xs">Verifica disponibilità agente…</span>
      </div>
    );
  }

  // 2. Not ready
  if (readiness?.isReady === false) {
    return (
      <div className="flex flex-1 flex-col items-center justify-center gap-3 px-6 text-center">
        <AlertCircle className="h-8 w-8 text-amber-400" />
        <p className="text-sm font-semibold text-slate-700">Agente non configurato</p>
        {readiness.blockingReason && (
          <p className="text-xs text-slate-500">{readiness.blockingReason}</p>
        )}
        <button
          className="mt-1 rounded-md bg-blue-600 px-3 py-1.5 text-xs font-semibold text-white hover:bg-blue-700"
          onClick={() => {
            window.location.href = `/admin/ai-lab/agents/${agentId}/edit`;
          }}
        >
          Configura Agente
        </button>
      </div>
    );
  }

  // 3. Thread selected (real UUID)
  if (selectedThreadId && selectedThreadId !== 'new') {
    return <ChatThreadView threadId={selectedThreadId} />;
  }

  // 4. Creating new thread
  if (selectedThreadId === 'new') {
    if (creating) {
      return (
        <div className="flex flex-1 flex-col items-center justify-center gap-2 text-slate-400">
          <Loader2 className="h-5 w-5 animate-spin" />
          <span className="text-xs">Creazione chat in corso…</span>
        </div>
      );
    }
    if (createError) {
      return (
        <div className="flex flex-1 flex-col items-center justify-center gap-2 text-red-500">
          <AlertCircle className="h-5 w-5" />
          <span className="text-xs">{createError}</span>
        </div>
      );
    }
    return null;
  }

  // 5. Default empty state
  return (
    <div
      data-testid="chat-empty-state"
      className="flex flex-1 flex-col items-center justify-center gap-3 px-6 text-center text-slate-400"
    >
      <MessageSquare className="h-8 w-8" />
      <p className="text-sm font-semibold text-slate-600">Chat con {agentName}</p>
      <p className="text-xs">Seleziona una chat recente oppure avvia una nuova conversazione</p>
    </div>
  );
}

// ─── AgentChatDrawerLayout ────────────────────────────────────────────────────

export const AgentChatDrawerLayout = React.memo(function AgentChatDrawerLayout({
  data,
  className,
  'data-testid': testId,
}: AgentChatDrawerLayoutProps) {
  const [selectedThreadId, setSelectedThreadId] = useState<string | null>(null);

  const { data: threads = [], isLoading: threadsLoading } = useAgentThreads(data.id);
  const { data: docs = [], isLoading: docsLoading } = useAgentKbDocs(data.gameId);
  const { status: readiness, isLoading: readinessLoading } = useAgentStatus(data.id);

  const isReady = readiness?.isReady ?? false;
  const buttonDisabled = !isReady || readinessLoading;

  return (
    <div
      className={cn('flex h-full w-full overflow-hidden', className)}
      data-testid={testId ?? 'agent-chat-drawer-layout'}
    >
      {/* Sidebar */}
      <aside
        data-testid="agent-chat-sidebar"
        className="w-[220px] shrink-0 flex flex-col gap-4 overflow-y-auto border-r border-slate-200/60 bg-slate-50/80 px-3 py-4"
      >
        <AgentContextCard gameId={data.gameId} gameName={data.gameName} />

        <button
          data-testid="new-chat-button"
          disabled={buttonDisabled}
          title={
            buttonDisabled
              ? readinessLoading
                ? 'Verifica disponibilità agente…'
                : (readiness?.blockingReason ?? 'Configura la KB per abilitare la chat')
              : undefined
          }
          onClick={() => setSelectedThreadId('new')}
          className={cn(
            'flex w-full items-center justify-center gap-1.5 rounded-md px-3 py-1.5 text-xs font-semibold font-nunito',
            buttonDisabled
              ? 'cursor-not-allowed bg-slate-200 text-slate-400'
              : 'bg-blue-600 text-white hover:bg-blue-700',
          )}
        >
          <Plus className="h-3.5 w-3.5" />
          Nuova chat
        </button>

        <SidebarSection label="CHAT RECENTI">
          {threadsLoading ? (
            <Loader2 className="mx-auto h-4 w-4 animate-spin text-slate-400" />
          ) : threads.length === 0 ? (
            <p className="px-2 text-xs text-slate-400">Nessuna chat</p>
          ) : (
            threads.slice(0, 8).map((thread) => (
              <RecentThreadItem
                key={thread.id}
                thread={thread}
                isSelected={selectedThreadId === thread.id}
                onClick={() => setSelectedThreadId(thread.id)}
              />
            ))
          )}
        </SidebarSection>

        <SidebarSection label="KNOWLEDGE BASE">
          {docsLoading ? (
            <Loader2 className="mx-auto h-4 w-4 animate-spin text-slate-400" />
          ) : docs.length === 0 ? (
            <p className="px-2 text-xs text-slate-400">Nessun documento</p>
          ) : (
            docs.slice(0, 5).map((doc) => <KbDocItem key={doc.id} doc={doc} />)
          )}
        </SidebarSection>
      </aside>

      {/* Chat area */}
      <div
        data-testid="agent-chat-area"
        data-thread-id={selectedThreadId ?? ''}
        className="flex flex-1 flex-col overflow-hidden"
      >
        <AgentChatArea
          agentId={data.id}
          agentName={data.name}
          selectedThreadId={selectedThreadId}
          onThreadCreated={(id) => setSelectedThreadId(id)}
          readiness={readiness}
          readinessLoading={readinessLoading}
        />
      </div>
    </div>
  );
});
