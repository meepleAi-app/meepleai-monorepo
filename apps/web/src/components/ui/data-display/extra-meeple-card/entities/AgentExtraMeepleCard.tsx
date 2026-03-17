'use client';

/**
 * AgentExtraMeepleCard — expanded card for Agent entities
 * Issue #5026 — Agent detail card with 4 tabs (Epic #5023)
 */

import React, { useState } from 'react';

import {
  Bot,
  ExternalLink,
  FileText,
  Gamepad2,
  Loader2,
  MessageCircle,
  MessageSquare,
  Settings,
  Zap,
} from 'lucide-react';

import { AgentModelInfo } from '@/components/ui/data-display/meeple-card-features/AgentModelInfo';
import { AgentStatsDisplay } from '@/components/ui/data-display/meeple-card-features/AgentStatsDisplay';
import { AgentStatusBadge } from '@/components/ui/data-display/meeple-card-features/AgentStatusBadge';
import { DocumentStatusBadge } from '@/components/ui/data-display/meeple-card-features/DocumentStatusBadge';
import { Tabs, TabsList, TabsContent } from '@/components/ui/navigation/tabs';
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
  loading?: boolean;
  error?: string;
  className?: string;
  'data-testid'?: string;
}

type AgentTab = 'overview' | 'stats' | 'history' | 'kb';

// ============================================================================
// AgentExtraMeepleCard
// ============================================================================

export const AgentExtraMeepleCard = React.memo(function AgentExtraMeepleCard({
  data,
  loading,
  error,
  className,
  'data-testid': testId,
}: AgentExtraMeepleCardProps) {
  const [activeTab, setActiveTab] = useState<AgentTab>('overview');
  const { threads, loading: threadsLoading } = useAgentThreads(data.id);
  const { docs, loading: docsLoading } = useAgentKbDocs(data.gameId);
  const colors = ENTITY_COLORS.agent;

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
        className
      )}
      data-testid={testId}
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
    </div>
  );
});

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

// ── Agent data-fetching hooks  ──────────────────────────────────────────────

function useAgentThreads(agentId: string) {
  const [threads, setThreads] = React.useState<ChatThreadPreview[]>([]);
  const [loading, setLoading] = React.useState(true);

  React.useEffect(() => {
    if (!agentId) return;
    const controller = new AbortController();

    (async () => {
      try {
        const res = await fetch(`/api/v1/chat/threads?agentId=${agentId}`, {
          signal: controller.signal,
        });
        if (!res.ok) return;
        const json = (await res.json()) as unknown[];
        setThreads(mapThreads(json));
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return;
      } finally {
        setLoading(false);
      }
    })();

    return () => {
      controller.abort();
    };
  }, [agentId]);

  return { threads, loading };
}

function useAgentKbDocs(gameId: string | undefined) {
  const [docs, setDocs] = React.useState<KbDocumentPreview[]>([]);
  const [loading, setLoading] = React.useState(!!gameId);

  React.useEffect(() => {
    if (!gameId) {
      setLoading(false);
      return;
    }
    const controller = new AbortController();

    (async () => {
      try {
        const res = await fetch(`/api/v1/library/games/${gameId}/documents`, {
          signal: controller.signal,
        });
        if (!res.ok) return;
        const json = (await res.json()) as unknown[];
        setDocs(mapKbDocs(json));
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return;
      } finally {
        setLoading(false);
      }
    })();

    return () => {
      controller.abort();
    };
  }, [gameId]);

  return { docs, loading };
}

function mapThreads(json: unknown[]): ChatThreadPreview[] {
  return json.map(raw => {
    const t = raw as Record<string, unknown>;
    const messages = Array.isArray(t.messages) ? (t.messages as Record<string, unknown>[]) : [];
    const firstMsg = messages[0];
    const preview = typeof firstMsg?.content === 'string' ? firstMsg.content : '';
    return {
      id: String(t.id ?? ''),
      createdAt: String(t.createdAt ?? t.startedAt ?? new Date().toISOString()),
      messageCount: messages.length,
      firstMessagePreview: preview,
    };
  });
}

function mapKbDocs(json: unknown[]): KbDocumentPreview[] {
  return json.map(raw => {
    const d = raw as Record<string, unknown>;
    const statusMap: Record<string, KbDocumentPreview['status']> = {
      indexed: 'indexed',
      processing: 'processing',
      failed: 'failed',
      none: 'none',
    };
    const rawStatus = String(d.status ?? 'none').toLowerCase();
    return {
      id: String(d.id ?? ''),
      fileName: String(d.fileName ?? d.name ?? 'Documento'),
      uploadedAt: String(d.uploadedAt ?? d.createdAt ?? new Date().toISOString()),
      status: statusMap[rawStatus] ?? 'none',
    };
  });
}
