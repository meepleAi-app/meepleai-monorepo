'use client';

/**
 * ChatExtraMeepleCard — expanded card for Chat entities
 * Issue #5027 — ChatExtraMeepleCard (Epic #5023)
 */

import React, { useState } from 'react';

import {
  Bot,
  Calendar,
  Clock,
  ExternalLink,
  Gamepad2,
  Hash,
  MessageCircle,
  MessageSquare,
  Settings,
  User,
} from 'lucide-react';

import { ChatStatusBadge } from '@/components/ui/data-display/meeple-card-features/ChatStatusBadge';
import { Tabs, TabsList, TabsContent } from '@/components/ui/navigation/tabs';
import { cn } from '@/lib/utils';

import {
  ENTITY_COLORS,
  EntityHeader,
  EntityTabTrigger,
  StatCard,
  EntityLoadingState,
  EntityErrorState,
} from '../shared';

import type { ChatDetailData, ChatDetailMessage } from '../types';

// ============================================================================
// Types
// ============================================================================

export interface ChatExtraMeepleCardProps {
  data: ChatDetailData;
  loading?: boolean;
  error?: string;
  className?: string;
  'data-testid'?: string;
}

type ChatTab = 'overview' | 'messages' | 'context';

// ============================================================================
// ChatExtraMeepleCard
// ============================================================================

export const ChatExtraMeepleCard = React.memo(function ChatExtraMeepleCard({
  data,
  loading,
  error,
  className,
  'data-testid': testId,
}: ChatExtraMeepleCardProps) {
  const [activeTab, setActiveTab] = useState<ChatTab>('overview');
  const colors = ENTITY_COLORS.chat;

  if (loading) return <EntityLoadingState className={className} testId={testId} />;
  if (error) return <EntityErrorState error={error} className={className} testId={testId} />;

  const startDate = new Date(data.startedAt).toLocaleDateString('it-IT', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
  });

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
        title={data.agentName ?? 'Chat'}
        subtitle={`Thread · ${data.messageCount} messaggi`}
        color={colors.hsl}
      />

      {/* Tabs */}
      <Tabs
        value={activeTab}
        onValueChange={v => setActiveTab(v as ChatTab)}
        className="flex flex-1 flex-col"
      >
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-slate-100/80 rounded-lg p-1">
          <EntityTabTrigger
            value="overview"
            icon={MessageCircle}
            label="Overview"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="messages"
            icon={MessageSquare}
            label="Messaggi"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="context"
            icon={Settings}
            label="Contesto"
            activeAccent={colors.activeAccent}
          />
        </TabsList>

        <div className="flex-1 overflow-y-auto px-4 py-3">
          {/* ── Overview Tab ─────────────────────────────────────── */}
          <TabsContent value="overview" className="mt-0">
            <div className="space-y-3">
              {/* Status + model row */}
              <div
                className={cn(
                  'flex items-center justify-between gap-2 rounded-lg border p-3',
                  colors.accentBorder,
                  `${colors.accentBg}/30`
                )}
              >
                <ChatStatusBadge status={data.status} showLabel size="md" />
                {data.agentModel && (
                  <span
                    className={cn(
                      'inline-flex items-center gap-1 rounded-full px-2.5 py-1',
                      colors.accentBg,
                      colors.accent,
                      'font-nunito text-[10px] font-semibold'
                    )}
                  >
                    <Bot className="h-3 w-3" aria-hidden="true" />
                    {data.agentModel}
                  </span>
                )}
              </div>

              {/* Game context chip */}
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
                      Gioco di contesto
                    </p>
                    <p className={cn('font-quicksand text-sm font-bold truncate', colors.accent)}>
                      {data.gameName}
                    </p>
                  </div>
                </div>
              )}

              {/* Metadata stat cards */}
              <div className="grid grid-cols-3 gap-2">
                <StatCard label="Inizio" value={startDate} icon={Calendar} variant="chat" />
                {data.durationMinutes != null && (
                  <StatCard
                    label="Durata"
                    value={`${data.durationMinutes}m`}
                    icon={Clock}
                    variant="chat"
                  />
                )}
                <StatCard
                  label="Messaggi"
                  value={data.messageCount.toString()}
                  icon={Hash}
                  variant="chat"
                />
              </div>

              {/* CTA */}
              <a
                href={`/chat/${data.id}`}
                className={cn(
                  'flex items-center justify-center gap-1.5 rounded-lg border py-2.5 px-3 w-full',
                  'bg-violet-600 border-violet-700 text-white',
                  'font-nunito text-xs font-medium',
                  'transition-colors duration-150 hover:bg-violet-700',
                  'focus:outline-none focus:ring-2 focus:ring-violet-500 focus:ring-offset-1'
                )}
                data-testid="chat-action-resume"
              >
                <MessageCircle className="h-3.5 w-3.5" aria-hidden="true" />
                Riprendi Chat
              </a>
            </div>
          </TabsContent>

          {/* ── Messages Tab ──────────────────────────────────────── */}
          <TabsContent value="messages" className="mt-0">
            {data.messages.length === 0 ? (
              <div className="flex flex-col items-center gap-2 py-8 text-center">
                <MessageSquare className="h-8 w-8 text-slate-300" aria-hidden="true" />
                <p className="font-nunito text-xs text-slate-400">
                  Nessun messaggio in questo thread
                </p>
              </div>
            ) : (
              <div className="space-y-2">
                {data.messages.map(msg => (
                  <ChatBubble key={msg.id} message={msg} />
                ))}
                {/* Footer CTA */}
                <div className="pt-2">
                  <a
                    href={`/chat/${data.id}`}
                    className={cn(
                      'flex items-center justify-center gap-1.5 rounded-lg border py-2 px-3 w-full',
                      'bg-white border-slate-200 text-violet-700',
                      'font-nunito text-xs font-medium',
                      'transition-colors duration-150 hover:bg-violet-50',
                      'focus:outline-none focus:ring-2 focus:ring-violet-400 focus:ring-offset-1'
                    )}
                    data-testid="chat-action-view-full"
                  >
                    <ExternalLink className="h-3.5 w-3.5" aria-hidden="true" />
                    Vedi conversazione completa
                  </a>
                </div>
              </div>
            )}
          </TabsContent>

          {/* ── Context Tab ───────────────────────────────────────── */}
          <TabsContent value="context" className="mt-0">
            <div className="space-y-3">
              {/* Compact game card */}
              {data.gameName && (
                <div className="flex items-center gap-3 rounded-lg bg-white/60 border border-slate-200/40 p-3">
                  {data.gameThumbnailUrl ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img
                      src={data.gameThumbnailUrl}
                      alt={data.gameName}
                      className="h-10 w-10 rounded-lg object-cover shrink-0"
                    />
                  ) : (
                    <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-orange-100">
                      <Gamepad2 className="h-5 w-5 text-orange-600" aria-hidden="true" />
                    </div>
                  )}
                  <div className="min-w-0">
                    <p className="font-nunito text-[10px] text-slate-400 uppercase tracking-wider">
                      Gioco
                    </p>
                    <p className="font-quicksand text-sm font-semibold text-slate-700 truncate">
                      {data.gameName}
                    </p>
                  </div>
                </div>
              )}

              {/* Compact agent card */}
              {data.agentName && (
                <div className="flex items-center gap-3 rounded-lg bg-white/60 border border-slate-200/40 p-3">
                  <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-blue-100">
                    <Bot className="h-5 w-5 text-blue-600" aria-hidden="true" />
                  </div>
                  <div className="min-w-0">
                    <p className="font-nunito text-[10px] text-slate-400 uppercase tracking-wider">
                      Agente
                    </p>
                    <p className="font-quicksand text-sm font-semibold text-slate-700 truncate">
                      {data.agentName}
                    </p>
                    {data.agentModel && (
                      <p className="font-nunito text-[10px] text-slate-400 truncate">
                        {data.agentModel}
                      </p>
                    )}
                  </div>
                </div>
              )}

              {/* Session parameters */}
              {(data.temperature != null || data.maxTokens != null || data.systemPrompt) && (
                <div className="rounded-lg bg-white/60 border border-slate-200/40 p-3 space-y-2">
                  <p className="font-nunito text-[10px] text-slate-400 uppercase tracking-wider">
                    Parametri sessione
                  </p>
                  {data.temperature != null && (
                    <div className="flex items-center justify-between">
                      <span className="font-nunito text-xs text-slate-500">Temperatura</span>
                      <span className="font-quicksand text-xs font-semibold text-slate-700">
                        {data.temperature}
                      </span>
                    </div>
                  )}
                  {data.maxTokens != null && (
                    <div className="flex items-center justify-between">
                      <span className="font-nunito text-xs text-slate-500">Max token</span>
                      <span className="font-quicksand text-xs font-semibold text-slate-700">
                        {data.maxTokens}
                      </span>
                    </div>
                  )}
                  {data.systemPrompt && (
                    <div className="space-y-1">
                      <p className="font-nunito text-[10px] text-slate-400">System prompt</p>
                      <p className="font-nunito text-xs text-slate-600 leading-relaxed">
                        {data.systemPrompt.length > 150
                          ? `${data.systemPrompt.slice(0, 150)}…`
                          : data.systemPrompt}
                      </p>
                    </div>
                  )}
                </div>
              )}

              {/* Empty state */}
              {!data.gameName &&
                !data.agentName &&
                data.temperature == null &&
                data.maxTokens == null &&
                !data.systemPrompt && (
                  <div className="flex flex-col items-center gap-2 py-8 text-center">
                    <Settings className="h-8 w-8 text-slate-300" aria-hidden="true" />
                    <p className="font-nunito text-xs text-slate-400">
                      Nessun contesto disponibile
                    </p>
                  </div>
                )}
            </div>
          </TabsContent>
        </div>
      </Tabs>
    </div>
  );
});

// ── Chat sub-components ────────────────────────────────────────────────────

function ChatBubble({ message }: { message: ChatDetailMessage }) {
  const isUser = message.role === 'user';
  const text = message.content.length > 200 ? `${message.content.slice(0, 200)}…` : message.content;

  const diffMs = Date.now() - new Date(message.createdAt).getTime();
  const diffMin = Math.floor(diffMs / 60_000);
  const diffHr = Math.floor(diffMin / 60);
  const diffDay = Math.floor(diffHr / 24);
  const relTime =
    diffMin < 1
      ? 'ora'
      : diffMin < 60
        ? `${diffMin}m fa`
        : diffHr < 24
          ? `${diffHr}h fa`
          : `${diffDay}g fa`;

  return (
    <div className={cn('flex gap-2', isUser ? 'flex-row-reverse' : 'flex-row')}>
      {/* Avatar */}
      <div
        className={cn(
          'flex h-7 w-7 shrink-0 items-center justify-center rounded-full',
          isUser ? 'bg-violet-100 text-violet-700' : 'bg-slate-100 text-slate-500'
        )}
      >
        {isUser ? (
          <User className="h-3.5 w-3.5" aria-hidden="true" />
        ) : (
          <Bot className="h-3.5 w-3.5" aria-hidden="true" />
        )}
      </div>

      {/* Bubble */}
      <div
        className={cn(
          'max-w-[75%] rounded-2xl px-3 py-2',
          isUser
            ? 'bg-violet-100 border border-violet-200/60'
            : 'bg-white/80 border border-slate-200/40'
        )}
      >
        <p className="font-nunito text-xs text-slate-700 leading-relaxed">{text}</p>
        <p className="font-nunito text-[10px] text-slate-400 mt-1">{relTime}</p>
      </div>
    </div>
  );
}
