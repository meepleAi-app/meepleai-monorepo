'use client';

/**
 * AgentCharacterSheet — RPG-style scrollable character sheet for Agent detail page.
 * Replaces the tabbed AgentExtraMeepleCard layout with a two-column design:
 *   Left (280px, sticky): Portrait — avatar, stats, links, configure button
 *   Right (scrollable): Equipaggiamento (KB), Area Azione (Chat), Storia (threads)
 */

import React, { useState } from 'react';

import {
  AlertCircle,
  Bot,
  ExternalLink,
  FileText,
  Loader2,
  MessageCircle,
  MessageSquare,
  Settings,
  Upload,
  Zap,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import { ChatThreadView } from '@/components/chat-unified/ChatThreadView';
import { AgentStatusBadge } from '@/components/ui/data-display/meeple-card-features/AgentStatusBadge';
import { DocumentStatusBadge } from '@/components/ui/data-display/meeple-card-features/DocumentStatusBadge';
import { Button } from '@/components/ui/primitives/button';
import { useAgentKbDocs, useAgentThreads } from '@/hooks/queries/useAgentData';
import { useAgentStatus } from '@/hooks/useAgentStatus';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

import type {
  AgentDetailData,
  ChatThreadPreview,
  KbDocumentPreview,
} from '../ui/data-display/extra-meeple-card/types';

// ============================================================================
// Types
// ============================================================================

export interface AgentCharacterSheetProps {
  data: AgentDetailData;
  className?: string;
  'data-testid'?: string;
}

// ============================================================================
// AgentCharacterSheet
// ============================================================================

export const AgentCharacterSheet = React.memo(function AgentCharacterSheet({
  data,
  className,
  'data-testid': testId,
}: AgentCharacterSheetProps) {
  const [chatThreadId, setChatThreadId] = useState<string | null>(null);

  const { data: threads = [], isLoading: threadsLoading } = useAgentThreads(data.id);
  const { data: docs = [], isLoading: docsLoading } = useAgentKbDocs(data.gameId);
  const {
    status: chatReadiness,
    isLoading: readinessLoading,
    error: readinessError,
  } = useAgentStatus(data.id);

  // Derive status from flags
  const agentStatus: 'active' | 'idle' | 'error' = !data.isActive
    ? 'error'
    : data.isIdle
      ? 'idle'
      : 'active';

  // Avatar gradient using agent entity hsl — '220 70% 55%' from ENTITY_COLORS.agent
  const agentHsl = '220 70% 55%';
  const avatarGradient = `linear-gradient(135deg, hsl(${agentHsl}) 0%, hsl(230 80% 40%) 100%)`;

  return (
    <div
      className={cn('flex flex-col lg:flex-row lg:items-start gap-6 w-full', className)}
      data-testid={testId ?? 'agent-character-sheet'}
    >
      {/* ── Portrait (sticky left column) ────────────────────────────────── */}
      <aside
        className={cn(
          'w-full lg:w-[280px] lg:shrink-0',
          'lg:sticky lg:top-8',
          'flex flex-col gap-4',
          'rounded-2xl overflow-hidden',
          'bg-[#161b22] border border-[#30363d]',
          'p-5'
        )}
        data-testid="agent-portrait"
      >
        {/* Avatar */}
        <div className="flex flex-col items-center gap-3 pb-3 border-b border-[#30363d]">
          <div
            className="w-20 h-20 rounded-2xl flex items-center justify-center shadow-lg"
            style={{ background: avatarGradient }}
            aria-hidden="true"
          >
            <Bot className="h-10 w-10 text-white" />
          </div>

          {/* Name + type badge */}
          <div className="text-center">
            <h2
              className="font-quicksand text-lg font-bold text-[#e6edf3] leading-tight"
              data-testid="agent-name"
            >
              {data.name}
            </h2>
            <span
              className="inline-flex items-center gap-1 mt-1 px-2 py-0.5 rounded-full bg-blue-500/20 text-blue-400 font-nunito text-[10px] font-semibold uppercase tracking-wider"
              data-testid="agent-type-badge"
            >
              {data.type}
            </span>
          </div>

          {/* Status badge */}
          <AgentStatusBadge status={agentStatus} showLabel size="sm" />
        </div>

        {/* Linked game */}
        {data.gameName && data.gameId && (
          <a
            href={`/library/games/${data.gameId}`}
            className="flex items-center gap-2 text-blue-400 hover:text-blue-400 transition-colors"
            data-testid="agent-game-link"
          >
            <ExternalLink className="h-3.5 w-3.5 shrink-0" aria-hidden="true" />
            <span className="font-nunito text-xs font-medium truncate">
              Collegato a {data.gameName}
            </span>
          </a>
        )}

        {/* Stats grid 2x2 */}
        <div className="grid grid-cols-2 gap-2" data-testid="agent-stats-grid">
          <StatPip
            label="Invocazioni"
            value={data.invocationCount.toLocaleString()}
            icon={<Zap className="h-3 w-3" />}
          />
          <StatPip
            label="Chat"
            value={threads.length.toLocaleString()}
            icon={<MessageCircle className="h-3 w-3" />}
          />
          <StatPip
            label="Documenti"
            value={docs.length.toLocaleString()}
            icon={<FileText className="h-3 w-3" />}
          />
          <StatPip
            label="Ultimo uso"
            value={data.lastInvokedAt ? formatRelativeTime(data.lastInvokedAt) : '—'}
            icon={<Zap className="h-3 w-3 opacity-40" />}
          />
        </div>

        {/* Mana pips — connection count */}
        <div className="flex items-center gap-1.5" data-testid="agent-mana-pips">
          {Array.from({ length: 5 }).map((_, i) => (
            <span
              key={i}
              className={cn(
                'w-8 h-8 rounded-full border-2 transition-colors',
                i < docs.length ? 'bg-blue-500 border-blue-600' : 'bg-[#30363d] border-[#484f58]'
              )}
              aria-hidden="true"
            />
          ))}
          {docs.length > 5 && (
            <span className="font-nunito text-[10px] text-[#8b949e]">+{docs.length - 5}</span>
          )}
        </div>

        {/* Configura button */}
        {data.gameId && (
          <a
            href={`/library/games/${data.gameId}/agent`}
            className={cn(
              'flex items-center justify-center gap-2 rounded-xl border py-2.5 px-4',
              'bg-[#21262d] border-blue-500/30 text-blue-400',
              'font-nunito text-xs font-semibold',
              'transition-colors hover:bg-blue-500/10',
              'focus:outline-none focus:ring-2 focus:ring-blue-400 focus:ring-offset-1'
            )}
            data-testid="agent-configure-btn"
          >
            <Settings className="h-3.5 w-3.5" aria-hidden="true" />
            Configura
          </a>
        )}
      </aside>

      {/* ── Scrollable body ───────────────────────────────────────────────── */}
      <div className="flex-1 flex flex-col gap-6 min-w-0">
        {/* Section 1: Equipaggiamento — Knowledge Base */}
        <section
          className="rounded-2xl bg-[#161b22] border border-[#30363d] p-5"
          data-testid="section-equipaggiamento"
        >
          <SectionHeader
            icon={<FileText className="h-4 w-4" />}
            title="Equipaggiamento"
            subtitle="Knowledge Base"
          />

          {docsLoading ? (
            <div className="flex justify-center py-6">
              <Loader2 className="h-5 w-5 animate-spin text-blue-400" />
            </div>
          ) : !data.gameId ? (
            <EmptyState
              icon={<FileText className="h-8 w-8 text-[#484f58]" />}
              message="Nessun gioco collegato a questo agente"
            />
          ) : docs.length === 0 ? (
            <EmptyState
              icon={<FileText className="h-8 w-8 text-[#484f58]" />}
              message="Equipaggia il tuo agente — carica un regolamento"
              cta={
                <a
                  href={`/library/games/${data.gameId}/agent`}
                  className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-full bg-blue-600 text-white font-nunito text-xs font-semibold hover:bg-blue-700 transition-colors"
                  data-testid="cta-carica-pdf"
                >
                  <Upload className="h-3.5 w-3.5" aria-hidden="true" />
                  Carica PDF
                </a>
              }
            />
          ) : (
            <ul className="space-y-2" data-testid="kb-docs-list">
              {docs.map(doc => (
                <KbDocItem key={doc.id} doc={doc} />
              ))}
            </ul>
          )}
        </section>

        {/* Section 2: Area Azione — Chat */}
        <section
          className="rounded-2xl bg-[#161b22] border border-[#30363d] p-5"
          data-testid="section-area-azione"
        >
          <SectionHeader
            icon={<MessageCircle className="h-4 w-4" />}
            title="Area Azione"
            subtitle="Chat"
          />

          <AgentChatSection
            agentId={data.id}
            agentName={data.name}
            readiness={chatReadiness}
            readinessLoading={readinessLoading}
            readinessError={readinessError}
            chatThreadId={chatThreadId}
            onThreadCreated={setChatThreadId}
          />
        </section>

        {/* Section 3: Storia — Conversazioni Recenti */}
        <section
          className="rounded-2xl bg-[#161b22] border border-[#30363d] p-5"
          data-testid="section-storia"
        >
          <SectionHeader
            icon={<MessageSquare className="h-4 w-4" />}
            title="Storia"
            subtitle="Conversazioni Recenti"
          />

          {threadsLoading ? (
            <div className="flex justify-center py-6">
              <Loader2 className="h-5 w-5 animate-spin text-blue-400" />
            </div>
          ) : threads.length === 0 ? (
            <EmptyState
              icon={<MessageSquare className="h-8 w-8 text-[#484f58]" />}
              message="Inizia la tua prima conversazione"
            />
          ) : (
            <ul className="space-y-2" data-testid="threads-list">
              {threads.map(thread => (
                <ThreadItem key={thread.id} thread={thread} />
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  );
});

// ============================================================================
// AgentChatSection — embedded chat with readiness validation
// ============================================================================

interface AgentChatSectionProps {
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
}

function AgentChatSection({
  agentId,
  agentName,
  readiness,
  readinessLoading,
  readinessError,
  chatThreadId,
  onThreadCreated,
}: AgentChatSectionProps) {
  const router = useRouter();
  const [threadError, setThreadError] = useState<string | null>(null);
  const [creatingThread, setCreatingThread] = useState(false);

  if (readinessLoading) {
    return (
      <div className="flex flex-col items-center justify-center py-8 text-center">
        <Loader2 className="mb-3 h-8 w-8 animate-spin text-blue-500" />
        <p className="font-nunito text-sm text-[#8b949e]">
          Verifica disponibilit&agrave; agente...
        </p>
      </div>
    );
  }

  if (readinessError) {
    return (
      <div className="flex flex-col items-center justify-center py-8 text-center">
        <div className="mb-3 flex h-14 w-14 items-center justify-center rounded-2xl bg-red-500/10">
          <AlertCircle className="h-7 w-7 text-red-400" />
        </div>
        <p className="font-nunito text-sm text-red-600">{readinessError}</p>
      </div>
    );
  }

  if (readiness && !readiness.isReady) {
    return (
      <div className="flex flex-col items-center justify-center py-8 text-center">
        <div className="mb-3 flex h-14 w-14 items-center justify-center rounded-2xl bg-amber-500/10">
          <AlertCircle className="h-7 w-7 text-amber-500" />
        </div>
        <h4 className="mb-1 font-quicksand text-base font-semibold text-[#e6edf3]">
          Agente non configurato
        </h4>
        <p className="max-w-xs font-nunito text-sm text-[#8b949e] mb-4">
          {readiness.blockingReason || 'Configura la Knowledge Base per abilitare la chat'}
        </p>
        <Button
          variant="outline"
          size="sm"
          onClick={() => router.push(`/admin/ai-lab/agents/${agentId}/edit`)}
          className="rounded-full gap-2"
        >
          <Settings className="h-4 w-4" />
          Configura Agente
        </Button>
      </div>
    );
  }

  if (readiness?.isReady && chatThreadId) {
    return (
      <div
        className="h-[480px] rounded-xl overflow-hidden border border-[#30363d]/60"
        data-testid="chat-thread-view-wrapper"
      >
        <ChatThreadView threadId={chatThreadId} />
      </div>
    );
  }

  if (readiness?.isReady) {
    return (
      <div className="flex flex-col items-center justify-center py-8 text-center">
        <div className="mb-3 flex h-14 w-14 items-center justify-center rounded-2xl bg-blue-500/10">
          <MessageSquare className="h-7 w-7 text-blue-500" />
        </div>
        <h4 className="mb-1 font-quicksand text-base font-semibold text-[#e6edf3]">
          Chat con {agentName}
        </h4>
        <p className="max-w-xs font-nunito text-sm text-[#8b949e] mb-4">
          Pronto &bull; {readiness.documentCount} documenti nella KB
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

  // Null state while readiness is not yet determined
  return (
    <EmptyState
      icon={<MessageCircle className="h-8 w-8 text-[#484f58]" />}
      message="Inizia la tua prima conversazione"
    />
  );
}

// ============================================================================
// Sub-components
// ============================================================================

function SectionHeader({
  icon,
  title,
  subtitle,
}: {
  icon: React.ReactNode;
  title: string;
  subtitle: string;
}) {
  return (
    <div className="flex items-center gap-2 mb-4 pb-3 border-b border-[#30363d]">
      <span className="text-blue-400" aria-hidden="true">
        {icon}
      </span>
      <div>
        <h3 className="font-quicksand text-sm font-bold text-[#e6edf3] leading-tight">{title}</h3>
        <p className="font-nunito text-[10px] text-[#8b949e] uppercase tracking-wider">
          {subtitle}
        </p>
      </div>
    </div>
  );
}

function StatPip({ label, value, icon }: { label: string; value: string; icon: React.ReactNode }) {
  return (
    <div className="rounded-xl bg-[#21262d] border border-[#30363d] p-2.5 flex flex-col gap-0.5">
      <div className="flex items-center gap-1 text-[#8b949e]">
        {icon}
        <span className="font-nunito text-[9px] uppercase tracking-wider">{label}</span>
      </div>
      <span className="font-quicksand text-sm font-bold text-[#e6edf3]">{value}</span>
    </div>
  );
}

function EmptyState({
  icon,
  message,
  cta,
}: {
  icon: React.ReactNode;
  message: string;
  cta?: React.ReactNode;
}) {
  return (
    <div className="flex flex-col items-center gap-2 py-8 text-center">
      {icon}
      <p className="font-nunito text-xs text-[#8b949e]">{message}</p>
      {cta}
    </div>
  );
}

function ThreadItem({ thread }: { thread: ChatThreadPreview }) {
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
    <li className="flex items-start gap-3 rounded-lg bg-[#21262d] border border-[#30363d] p-2.5">
      <MessageSquare className="h-4 w-4 mt-0.5 shrink-0 text-[#8b949e]" aria-hidden="true" />
      <div className="flex-1 min-w-0 space-y-0.5">
        <div className="flex items-center justify-between gap-2">
          <p className="font-nunito text-[10px] text-[#8b949e]">{date}</p>
          <p className="font-nunito text-[10px] text-[#8b949e]">{thread.messageCount} msg</p>
        </div>
        {preview && <p className="font-nunito text-xs text-[#8b949e] leading-relaxed">{preview}</p>}
      </div>
      <a
        href={`/chat/${thread.id}`}
        className="shrink-0 font-nunito text-[10px] font-medium text-blue-400 hover:underline"
        aria-label={`Vai al thread del ${date}`}
      >
        <ExternalLink className="h-3.5 w-3.5" aria-hidden="true" />
      </a>
    </li>
  );
}

function KbDocItem({ doc }: { doc: KbDocumentPreview }) {
  const date = new Date(doc.uploadedAt).toLocaleDateString('it-IT', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
  });

  return (
    <li className="flex items-center gap-3 rounded-lg bg-[#21262d] border border-[#30363d] p-2.5">
      <FileText className="h-4 w-4 shrink-0 text-[#8b949e]" aria-hidden="true" />
      <div className="flex-1 min-w-0">
        <p className="font-nunito text-xs font-medium text-[#e6edf3] truncate">{doc.fileName}</p>
        <p className="font-nunito text-[10px] text-[#8b949e]">{date}</p>
      </div>
      <DocumentStatusBadge status={doc.status} size="sm" />
    </li>
  );
}

function formatRelativeTime(timestamp: string): string {
  const date = new Date(timestamp);
  const diffMs = Date.now() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMs / 3600000);
  const diffDays = Math.floor(diffMs / 86400000);

  if (diffMins < 1) return 'Proprio ora';
  if (diffMins < 60) return `${diffMins}m fa`;
  if (diffHours < 24) return `${diffHours}h fa`;
  return `${diffDays}d fa`;
}
