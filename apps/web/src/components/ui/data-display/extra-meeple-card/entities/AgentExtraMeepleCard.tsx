'use client';

/**
 * AgentExtraMeepleCard — expanded card for Agent entities
 * Issue #5026 — Agent detail card with 5 tabs (Epic #5023)
 *
 * When enableChat is true, adds an embedded Chat tab with:
 * - Agent readiness validation (KB populated, RAG initialized)
 * - Blocking UI if agent not ready with link to configuration
 * - Embedded ChatThreadView with SSE streaming
 * - Fullscreen toggle for immersive chat
 */

import React, { useState } from 'react';

import {
  AlertCircle,
  Bot,
  ExternalLink,
  FileText,
  Gamepad2,
  Loader2,
  Maximize2,
  MessageCircle,
  MessageSquare,
  Minimize2,
  Settings,
  Zap,
} from 'lucide-react';

import { ChatThreadView } from '@/components/chat-unified/ChatThreadView';
import { AgentModelInfo } from '@/components/ui/data-display/meeple-card-features/AgentModelInfo';
import { AgentStatsDisplay } from '@/components/ui/data-display/meeple-card-features/AgentStatsDisplay';
import { AgentStatusBadge } from '@/components/ui/data-display/meeple-card-features/AgentStatusBadge';
import { DocumentStatusBadge } from '@/components/ui/data-display/meeple-card-features/DocumentStatusBadge';
import { Tabs, TabsList, TabsContent } from '@/components/ui/navigation/tabs';
import { Button } from '@/components/ui/primitives/button';
import { useAgentKbDocs, useAgentThreads } from '@/hooks/queries/useAgentData';
import { useAgentStatus } from '@/hooks/useAgentStatus';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

import {
  ENTITY_COLORS,
  EntityHeader,
  EntityTabTrigger,
  EntityLoadingState,
  EntityErrorState,
} from '../shared';

import type { AgentDetailData, ChatThreadPreview, KbDocumentPreview } from '../types';

// ============================================================================
// Types
// ============================================================================

export interface AgentExtraMeepleCardProps {
  data: AgentDetailData;
  /** Enable embedded chat tab with SSE streaming and readiness validation */
  enableChat?: boolean;
  loading?: boolean;
  error?: string;
  className?: string;
  'data-testid'?: string;
}

type AgentTab = 'chat' | 'overview' | 'stats' | 'history' | 'kb';

// ============================================================================
// AgentExtraMeepleCard
// ============================================================================

export const AgentExtraMeepleCard = React.memo(function AgentExtraMeepleCard({
  data,
  enableChat = false,
  loading,
  error,
  className,
  'data-testid': testId,
}: AgentExtraMeepleCardProps) {
  const [activeTab, setActiveTab] = useState<AgentTab>(enableChat ? 'chat' : 'overview');
  const [chatThreadId, setChatThreadId] = useState<string | null>(null);
  const [isFullscreen, setIsFullscreen] = useState(false);
  const { data: threads = [], isLoading: threadsLoading } = useAgentThreads(data.id);
  const { data: docs = [], isLoading: docsLoading } = useAgentKbDocs(data.gameId);
  const colors = ENTITY_COLORS.agent;

  // Agent readiness validation — always called (rules of hooks) but result
  // is only consumed when enableChat is true
  const {
    status: chatReadiness,
    isLoading: readinessLoading,
    error: readinessError,
  } = useAgentStatus(data.id);

  // Derive AgentStatus from flags
  const agentStatus = !data.isActive ? 'error' : data.isIdle ? 'idle' : 'active';

  // Derive model name from strategyParameters or strategyName
  const modelName =
    (data.strategyParameters?.model as string | undefined) ?? data.strategyName ?? 'Unknown';

  if (loading) return <EntityLoadingState className={className} testId={testId} />;
  if (error) return <EntityErrorState error={error} className={className} testId={testId} />;

  return (
    <div
      className={cn(
        'flex w-[600px] flex-col rounded-2xl overflow-hidden',
        'bg-white/70 backdrop-blur-md shadow-[0_8px_32px_rgba(0,0,0,0.12)] border border-white/20',
        'max-md:w-full',
        enableChat && 'w-full max-w-[800px]',
        className
      )}
      style={enableChat ? { minHeight: '600px' } : undefined}
      data-testid={testId ?? 'agent-info-card'}
    >
      {/* Header */}
      <EntityHeader
        title={data.name}
        subtitle={data.type ? `Tipo: ${data.type}` : undefined}
        color={colors.hsl}
        badge={data.invocationCount > 0 ? data.invocationCount.toString() : undefined}
        badgeIcon={<Zap className="h-3 w-3" />}
      />

      {/* Tabs */}
      <Tabs
        value={activeTab}
        onValueChange={v => setActiveTab(v as AgentTab)}
        className="flex flex-1 flex-col"
      >
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-slate-100/80 rounded-lg p-1">
          {enableChat && (
            <EntityTabTrigger
              value="chat"
              icon={MessageCircle}
              label="Chat"
              activeAccent={colors.activeAccent}
            />
          )}
          <EntityTabTrigger
            value="overview"
            icon={Bot}
            label="Overview"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="stats"
            icon={Zap}
            label="Stats"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="history"
            icon={MessageSquare}
            label="History"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="kb"
            icon={FileText}
            label="KB"
            activeAccent={colors.activeAccent}
          />
        </TabsList>

        <div className="flex-1 overflow-y-auto px-4 py-3">
          {/* ── Chat Tab (readiness validation + embedded ChatThreadView) ── */}
          {enableChat && (
            <TabsContent value="chat" className="mt-0 flex flex-col h-full">
              <AgentChatTab
                agentId={data.id}
                agentName={data.name}
                readiness={chatReadiness}
                readinessLoading={readinessLoading}
                readinessError={readinessError}
                chatThreadId={chatThreadId}
                onThreadCreated={setChatThreadId}
                isFullscreen={isFullscreen}
                onFullscreenToggle={() => setIsFullscreen(!isFullscreen)}
              />
            </TabsContent>
          )}

          {/* ── Overview Tab ─────────────────────────────────────── */}
          <TabsContent value="overview" className="mt-0">
            <div className="space-y-3">
              {/* Status + Model row */}
              <div className="flex items-center justify-between gap-2 rounded-lg bg-blue-50/50 border border-blue-200/40 p-3">
                <AgentStatusBadge status={agentStatus} showLabel size="md" />
                <AgentModelInfo modelName={modelName} showIcon size="md" />
              </div>

              {/* Linked game chip */}
              {data.gameName && (
                <div
                  className={cn(
                    'flex items-center gap-2 rounded-lg border p-3',
                    colors.accentBorder,
                    `${colors.accentBg}/30`
                  )}
                >
                  <Gamepad2 className={cn('h-4 w-4 shrink-0', colors.accent)} aria-hidden="true" />
                  <div className="flex-1 min-w-0">
                    <p className="font-nunito text-[10px] text-slate-500 uppercase tracking-wider">
                      Gioco collegato
                    </p>
                    <p className={cn('font-quicksand text-sm font-bold truncate', colors.accent)}>
                      {data.gameName}
                    </p>
                  </div>
                </div>
              )}

              {/* Quick actions */}
              <div className="grid grid-cols-2 gap-2">
                {enableChat ? (
                  <button
                    onClick={() => setActiveTab('chat')}
                    className={cn(
                      'flex items-center justify-center gap-1.5 rounded-lg border py-2.5 px-3',
                      'bg-blue-600 border-blue-700 text-white',
                      'font-nunito text-xs font-medium',
                      'transition-colors duration-150 hover:bg-blue-700',
                      'focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-1'
                    )}
                    data-testid="agent-action-start-chat"
                  >
                    <MessageCircle className="h-3.5 w-3.5" aria-hidden="true" />
                    Avvia Chat
                  </button>
                ) : (
                  <a
                    href={`/chat/new?agentId=${data.id}`}
                    className={cn(
                      'flex items-center justify-center gap-1.5 rounded-lg border py-2.5 px-3',
                      'bg-blue-600 border-blue-700 text-white',
                      'font-nunito text-xs font-medium',
                      'transition-colors duration-150 hover:bg-blue-700',
                      'focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-1'
                    )}
                    data-testid="agent-action-start-chat"
                  >
                    <MessageCircle className="h-3.5 w-3.5" aria-hidden="true" />
                    Avvia Chat
                  </a>
                )}
                {data.gameId && (
                  <a
                    href={`/library/games/${data.gameId}/agent`}
                    className={cn(
                      'flex items-center justify-center gap-1.5 rounded-lg border py-2.5 px-3',
                      'bg-white border-blue-200 text-blue-700',
                      'font-nunito text-xs font-medium',
                      'transition-colors duration-150 hover:bg-blue-50',
                      'focus:outline-none focus:ring-2 focus:ring-blue-400 focus:ring-offset-1'
                    )}
                    data-testid="agent-action-configure"
                  >
                    <Settings className="h-3.5 w-3.5" aria-hidden="true" />
                    Configura
                  </a>
                )}
              </div>
            </div>
          </TabsContent>

          {/* ── Stats Tab ─────────────────────────────────────────── */}
          <TabsContent value="stats" className="mt-0">
            <div className="space-y-3">
              <AgentStatsDisplay
                stats={{
                  invocationCount: data.invocationCount,
                  lastExecutedAt: data.lastInvokedAt ?? undefined,
                }}
                layout="vertical"
              />
              {data.invocationCount === 0 && (
                <p className="font-nunito text-xs text-slate-400 text-center py-4">
                  Nessuna conversazione ancora
                </p>
              )}
            </div>
          </TabsContent>

          {/* ── History Tab ───────────────────────────────────────── */}
          <TabsContent value="history" className="mt-0">
            {threadsLoading ? (
              <div className="flex justify-center py-8">
                <Loader2 className="h-5 w-5 animate-spin text-blue-400" />
              </div>
            ) : threads.length === 0 ? (
              <div className="flex flex-col items-center gap-2 py-8 text-center">
                <MessageSquare className="h-8 w-8 text-slate-300" aria-hidden="true" />
                <p className="font-nunito text-xs text-slate-400">Nessun thread di chat</p>
              </div>
            ) : (
              <div className="space-y-2">
                {threads.map(thread => (
                  <ThreadItem key={thread.id} thread={thread} accentColor={colors.accent} />
                ))}
              </div>
            )}
          </TabsContent>

          {/* ── KB Tab ────────────────────────────────────────────── */}
          <TabsContent value="kb" className="mt-0">
            {docsLoading ? (
              <div className="flex justify-center py-8">
                <Loader2 className="h-5 w-5 animate-spin text-blue-400" />
              </div>
            ) : !data.gameId ? (
              <div className="flex flex-col items-center gap-2 py-8 text-center">
                <FileText className="h-8 w-8 text-slate-300" aria-hidden="true" />
                <p className="font-nunito text-xs text-slate-400">
                  Nessun gioco collegato a questo agente
                </p>
              </div>
            ) : docs.length === 0 ? (
              <div className="flex flex-col items-center gap-2 py-8 text-center">
                <FileText className="h-8 w-8 text-slate-300" aria-hidden="true" />
                <p className="font-nunito text-xs text-slate-400 mb-2">Nessun documento KB</p>
                <a
                  href={`/library/games/${data.gameId}/agent`}
                  className="font-nunito text-xs text-blue-600 underline hover:text-blue-700"
                >
                  Aggiungi documento
                </a>
              </div>
            ) : (
              <div className="space-y-2">
                {docs.map(doc => (
                  <KbDocItem key={doc.id} doc={doc} />
                ))}
              </div>
            )}
          </TabsContent>
        </div>
      </Tabs>

      {/* Fullscreen Modal for Chat */}
      {isFullscreen && chatThreadId && (
        <div className="fixed inset-0 z-50 bg-background" data-testid="fullscreen-chat-modal">
          <div className="absolute top-4 right-4 z-10">
            <button
              onClick={() => setIsFullscreen(false)}
              className="p-2 rounded-lg bg-white/80 dark:bg-card/80 backdrop-blur-md border border-border/50 hover:bg-white dark:hover:bg-card transition-colors"
              title="Esci da schermo intero"
              data-testid="fullscreen-close"
            >
              <Minimize2 className="h-5 w-5 text-muted-foreground" />
            </button>
          </div>
          <div className="h-full w-full">
            <ChatThreadView threadId={chatThreadId} />
          </div>
        </div>
      )}
    </div>
  );
});

// ============================================================================
// AgentChatTab — embedded chat with readiness validation
// ============================================================================

interface AgentChatTabProps {
  agentId: string;
  agentName: string;
  readiness: {
    isReady: boolean;
    documentCount: number;
    ragStatus: string;
    blockingReason?: string | null;
  } | null;
  readinessLoading: boolean;
  readinessError: string | null;
  chatThreadId: string | null;
  onThreadCreated: (threadId: string) => void;
  isFullscreen: boolean;
  onFullscreenToggle: () => void;
}

function AgentChatTab({
  agentId,
  agentName,
  readiness,
  readinessLoading,
  readinessError,
  chatThreadId,
  onThreadCreated,
  isFullscreen,
  onFullscreenToggle,
}: AgentChatTabProps) {
  const [threadError, setThreadError] = useState<string | null>(null);
  const [creatingThread, setCreatingThread] = useState(false);
  // Loading status
  if (readinessLoading) {
    return (
      <div className="flex flex-1 flex-col items-center justify-center text-center p-6">
        <Loader2 className="mb-3 h-8 w-8 animate-spin text-blue-500" />
        <p className="font-nunito text-sm text-slate-500">
          Verifica disponibilit&agrave; agente...
        </p>
      </div>
    );
  }

  // Error loading status
  if (readinessError) {
    return (
      <div className="flex flex-1 flex-col items-center justify-center text-center p-6">
        <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-red-50">
          <AlertCircle className="h-8 w-8 text-red-400" />
        </div>
        <h4 className="mb-2 font-quicksand text-lg font-semibold text-slate-800">Errore</h4>
        <p className="max-w-xs font-nunito text-sm text-red-600 mb-4">{readinessError}</p>
      </div>
    );
  }

  // Agent not ready - Blocking UI
  if (readiness && !readiness.isReady) {
    return (
      <div className="flex flex-1 flex-col items-center justify-center text-center p-6">
        <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-amber-50">
          <AlertCircle className="h-8 w-8 text-amber-500" />
        </div>
        <h4 className="mb-2 font-quicksand text-lg font-semibold text-slate-800">
          Agente non configurato
        </h4>
        <p className="max-w-xs font-nunito text-sm text-slate-500 mb-1">
          {readiness.blockingReason || 'Configura la Knowledge Base per abilitare la chat'}
        </p>
        <p className="max-w-xs font-nunito text-xs text-slate-400 mb-4">
          Documenti: {readiness.documentCount} | Status RAG: {readiness.ragStatus}
        </p>
        <Button
          variant="outline"
          size="sm"
          onClick={() => (window.location.href = `/admin/ai-lab/agents/${agentId}/edit`)}
          className="rounded-full gap-2"
        >
          <Settings className="h-4 w-4" />
          Configura Agente
        </Button>
      </div>
    );
  }

  // Agent ready - show chat or start button
  if (readiness?.isReady) {
    if (chatThreadId) {
      return (
        <div className="flex-1 min-h-0 flex flex-col relative">
          {/* Fullscreen toggle button */}
          <div className="absolute top-2 right-2 z-10">
            <button
              onClick={onFullscreenToggle}
              className="p-2 rounded-lg bg-white/80 dark:bg-card/80 backdrop-blur-md border border-border/50 hover:bg-white dark:hover:bg-card transition-colors"
              title={isFullscreen ? 'Esci da schermo intero' : 'Schermo intero'}
              data-testid="fullscreen-toggle"
            >
              {isFullscreen ? (
                <Minimize2 className="h-4 w-4 text-muted-foreground" />
              ) : (
                <Maximize2 className="h-4 w-4 text-muted-foreground" />
              )}
            </button>
          </div>

          {!isFullscreen && (
            <div className="flex-1 min-h-0">
              <ChatThreadView threadId={chatThreadId} />
            </div>
          )}
        </div>
      );
    }

    return (
      <div className="flex flex-1 flex-col items-center justify-center text-center p-6">
        <div className="mb-4 flex h-16 w-16 items-center justify-center rounded-2xl bg-blue-50">
          <MessageSquare className="h-8 w-8 text-blue-500" />
        </div>
        <h4 className="mb-2 font-quicksand text-lg font-semibold text-slate-800">
          Chat con {agentName}
        </h4>
        <p className="max-w-xs font-nunito text-sm text-slate-500 mb-4">
          Pronto per chattare &bull; {readiness.documentCount} documenti nella KB
        </p>
        <Button
          variant="default"
          size="sm"
          disabled={creatingThread}
          onClick={async () => {
            setThreadError(null);
            setCreatingThread(true);
            try {
              const thread = await api.chat.createThread({
                agentId,
                title: `Chat con ${agentName}`,
              });
              if (thread?.id) {
                onThreadCreated(thread.id);
              }
            } catch (err) {
              const message =
                err instanceof Error ? err.message : 'Impossibile creare la conversazione';
              setThreadError(message);
            } finally {
              setCreatingThread(false);
            }
          }}
          className="rounded-full"
        >
          {creatingThread ? (
            <>
              <Loader2 className="mr-1.5 h-4 w-4 animate-spin" aria-hidden="true" />
              Creazione...
            </>
          ) : (
            'Inizia Conversazione'
          )}
        </Button>
        {threadError && (
          <p
            className="mt-2 max-w-xs font-nunito text-xs text-red-600"
            role="alert"
            data-testid="thread-creation-error"
          >
            {threadError}
          </p>
        )}
      </div>
    );
  }

  return null;
}

// ── Agent sub-components ───────────────────────────────────────────────────

function ThreadItem({ thread, accentColor }: { thread: ChatThreadPreview; accentColor: string }) {
  const date = new Date(thread.createdAt).toLocaleDateString('it-IT', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
  });
  const preview =
    thread.firstMessagePreview.length > 80
      ? `${thread.firstMessagePreview.slice(0, 80)}…`
      : thread.firstMessagePreview;

  return (
    <div className="flex items-start gap-3 rounded-lg bg-white/50 border border-slate-200/40 p-2.5">
      <MessageSquare className="h-4 w-4 mt-0.5 shrink-0 text-slate-400" aria-hidden="true" />
      <div className="flex-1 min-w-0 space-y-0.5">
        <div className="flex items-center justify-between gap-2">
          <p className="font-nunito text-[10px] text-slate-400">{date}</p>
          <p className="font-nunito text-[10px] text-slate-400">{thread.messageCount} msg</p>
        </div>
        {preview && <p className="font-nunito text-xs text-slate-600 leading-relaxed">{preview}</p>}
      </div>
      <a
        href={`/chat/${thread.id}`}
        className={cn(
          'shrink-0 font-nunito text-[10px] font-medium',
          accentColor,
          'hover:underline'
        )}
        aria-label={`Vai al thread del ${date}`}
      >
        <ExternalLink className="h-3.5 w-3.5" aria-hidden="true" />
      </a>
    </div>
  );
}

function KbDocItem({ doc }: { doc: KbDocumentPreview }) {
  const date = new Date(doc.uploadedAt).toLocaleDateString('it-IT', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
  });

  return (
    <div className="flex items-center gap-3 rounded-lg bg-white/50 border border-slate-200/40 p-2.5">
      <FileText className="h-4 w-4 shrink-0 text-slate-400" aria-hidden="true" />
      <div className="flex-1 min-w-0">
        <p className="font-nunito text-xs font-medium text-slate-700 truncate">{doc.fileName}</p>
        <p className="font-nunito text-[10px] text-slate-400">{date}</p>
      </div>
      <DocumentStatusBadge status={doc.status} size="sm" />
    </div>
  );
}
