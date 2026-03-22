'use client';

/**
 * GameExtraMeepleCard — expanded card for Game entities
 * Issue #5029 - GameExtraMeepleCard: KB + Agent tabs (Epic #5023)
 */

import React, { useState } from 'react';

import {
  Bot,
  BookOpen,
  Clock,
  FileText,
  Gamepad2,
  HelpCircle,
  MessageCircle,
  Settings,
  Star,
  Trophy,
  Users,
} from 'lucide-react';

import { KbCardStatusRow } from '@/components/documents/KbCardStatusRow';
import { EntityLinkBadge } from '@/components/ui/data-display/entity-link-badge';
import { AgentStatusBadge } from '@/components/ui/data-display/meeple-card-features/AgentStatusBadge';
import { KbStatusBadge } from '@/components/ui/data-display/meeple-card-features/DocumentStatusBadge';
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

import type { GameDetailData, GameAgentPreview } from '../types';

// ============================================================================
// Types & Constants
// ============================================================================

export interface GameExtraMeepleCardProps {
  data: GameDetailData;
  loading?: boolean;
  error?: string;
  className?: string;
  'data-testid'?: string;
}

type GameTab = 'details' | 'rules' | 'stats' | 'kb' | 'agent';

/** Sort order for KB document status (Issue #5029) */
const KB_STATUS_ORDER: Record<'indexed' | 'processing' | 'failed' | 'none', number> = {
  indexed: 0,
  processing: 1,
  failed: 2,
  none: 3,
};

/** Sort order for full PdfDocumentDto by processingState (failed first for visibility, Issue #5195) */
const KB_PDF_STATE_ORDER: Record<string, number> = {
  Failed: 0,
  Extracting: 1,
  Chunking: 1,
  Embedding: 1,
  Indexing: 1,
  Uploading: 1,
  Pending: 2,
  Ready: 3,
};

// ============================================================================
// GameExtraMeepleCard
// ============================================================================

export const GameExtraMeepleCard = React.memo(function GameExtraMeepleCard({
  data,
  loading,
  error,
  className,
  'data-testid': testId,
}: GameExtraMeepleCardProps) {
  const [activeTab, setActiveTab] = useState<GameTab>('details');
  const colors = ENTITY_COLORS.game;

  if (loading) return <EntityLoadingState className={className} testId={testId} />;
  if (error) return <EntityErrorState error={error} className={className} testId={testId} />;

  const sortedKbDocs = [...(data.kbDocuments ?? [])].sort(
    (a, b) => KB_STATUS_ORDER[a.status] - KB_STATUS_ORDER[b.status]
  );
  // Sort full PdfDocumentDto data: failed first so user can act on errors (Issue #5195)
  const sortedPdfDocs = [...(data.pdfDocuments ?? [])].sort((a, b) => {
    const aOrder = KB_PDF_STATE_ORDER[a.processingState] ?? 4;
    const bOrder = KB_PDF_STATE_ORDER[b.processingState] ?? 4;
    return aOrder - bOrder;
  });
  const kbDocCount = sortedPdfDocs.length || sortedKbDocs.length;
  const indexedCount = (data.kbDocuments ?? []).filter(d => d.status === 'indexed').length;
  const agentStatus = data.agent?.isActive ? 'active' : undefined;

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
        title={data.title}
        subtitle={
          data.publisher
            ? `${data.publisher}${data.yearPublished ? ` (${data.yearPublished})` : ''}`
            : undefined
        }
        imageUrl={data.imageUrl}
        color={colors.hsl}
        badge={data.averageRating ? `${data.averageRating.toFixed(1)}` : undefined}
        badgeIcon={<Star className="h-3 w-3" />}
      />

      {/* Tabs */}
      <Tabs
        value={activeTab}
        onValueChange={v => setActiveTab(v as GameTab)}
        className="flex flex-1 flex-col"
      >
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-slate-100/80 rounded-lg p-1">
          <EntityTabTrigger
            value="details"
            icon={Gamepad2}
            label="Details"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="rules"
            icon={BookOpen}
            label="Rules & FAQs"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="stats"
            icon={Trophy}
            label="Stats"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="kb"
            icon={FileText}
            label="KB"
            activeAccent={colors.activeAccent}
            badge={indexedCount > 0 ? indexedCount : undefined}
          />
          <EntityTabTrigger
            value="agent"
            icon={Bot}
            label="Agent"
            activeAccent={colors.activeAccent}
            badge={agentStatus === 'active' ? 'Attivo' : undefined}
          />
        </TabsList>

        <div className="flex-1 overflow-y-auto px-4 py-3">
          {/* ── Details Tab ─────────────────────────────────────── */}
          <TabsContent value="details" className="mt-0">
            <div className="space-y-3">
              <div className="grid grid-cols-3 gap-2">
                {data.minPlayers != null && (
                  <StatCard
                    label="Players"
                    value={`${data.minPlayers}-${data.maxPlayers ?? data.minPlayers}`}
                    icon={Users}
                    variant="game"
                  />
                )}
                {data.playTimeMinutes != null && (
                  <StatCard
                    label="Play Time"
                    value={`${data.playTimeMinutes}m`}
                    icon={Clock}
                    variant="game"
                  />
                )}
                {data.totalPlays != null && (
                  <StatCard
                    label="Total Plays"
                    value={data.totalPlays.toString()}
                    icon={Gamepad2}
                    variant="game"
                  />
                )}
              </div>
              {data.description && (
                <p className="font-nunito text-xs text-slate-600 leading-relaxed">
                  {data.description}
                </p>
              )}
            </div>
          </TabsContent>

          {/* ── Rules & FAQs Tab ─────────────────────────────────── */}
          <TabsContent value="rules" className="mt-0">
            <div className="space-y-3">
              <StatCard
                label="Documents"
                value={(data.rulesDocumentCount ?? 0).toString()}
                icon={BookOpen}
                variant="game"
              />
              <StatCard
                label="FAQs"
                value={(data.faqCount ?? 0).toString()}
                icon={HelpCircle}
                variant="game"
              />
              <p className="font-nunito text-xs text-slate-400 text-center py-4">
                Rules and FAQs will be displayed here
              </p>
            </div>
          </TabsContent>

          {/* ── Stats Tab ─────────────────────────────────────────── */}
          <TabsContent value="stats" className="mt-0">
            <div className="space-y-3">
              {data.averageRating != null && (
                <div className="flex items-center gap-2 rounded-lg bg-orange-50/50 border border-orange-200/40 p-3">
                  <Star className="h-5 w-5 text-orange-500" />
                  <div>
                    <p className="font-quicksand text-lg font-bold text-orange-700">
                      {data.averageRating.toFixed(1)}
                    </p>
                    <p className="font-nunito text-[10px] text-orange-500">Average Rating</p>
                  </div>
                </div>
              )}
              {data.totalPlays != null && (
                <StatCard
                  label="Total Plays"
                  value={data.totalPlays.toString()}
                  icon={Gamepad2}
                  variant="game"
                />
              )}
            </div>
          </TabsContent>

          {/* ── KB Tab ────────────────────────────────────────────── */}
          <TabsContent value="kb" className="mt-0">
            {kbDocCount === 0 ? (
              <div className="flex flex-col items-center gap-3 py-8 text-center">
                <FileText className="h-8 w-8 text-slate-300" aria-hidden="true" />
                <div>
                  <p className="font-nunito text-sm font-medium text-slate-600">
                    Nessun documento caricato
                  </p>
                  <p className="font-nunito text-xs text-slate-400 mt-0.5">
                    Carica un PDF per creare la Knowledge Base di questo gioco
                  </p>
                </div>
                <a
                  href={`/library/games/${data.id}`}
                  className="inline-flex items-center gap-1.5 rounded-lg bg-orange-500 px-3 py-1.5 font-nunito text-xs font-semibold text-white hover:bg-orange-600 transition-colors"
                  data-testid="game-kb-upload-cta"
                >
                  <FileText className="h-3.5 w-3.5" aria-hidden="true" />
                  Carica PDF
                </a>
              </div>
            ) : (
              <div className="space-y-2">
                {/* Section header — EntityLinkBadge (Issue #5195) */}
                <div className="flex items-center justify-between">
                  <EntityLinkBadge
                    linkType="PartOf"
                    sourceEntityType="KbCard"
                    count={kbDocCount}
                    size="md"
                  />
                </div>
                {/* Document rows — KbCardStatusRow when full data available (Issue #5195) */}
                <div className="space-y-1.5" data-testid="game-kb-documents-list">
                  {sortedPdfDocs.length > 0
                    ? sortedPdfDocs.map(pdf => <KbCardStatusRow key={pdf.id} document={pdf} />)
                    : sortedKbDocs.map(doc => (
                        <div
                          key={doc.id}
                          className="flex items-center gap-3 rounded-lg border border-slate-200/60 bg-slate-50/50 p-2.5"
                          data-testid={`game-kb-doc-${doc.id}`}
                        >
                          <FileText
                            className="h-4 w-4 shrink-0 text-slate-400"
                            aria-hidden="true"
                          />
                          <div className="flex-1 min-w-0">
                            <p className="font-nunito text-xs font-medium text-slate-700 truncate">
                              {doc.fileName}
                            </p>
                            <p className="font-nunito text-[10px] text-slate-400">
                              {new Date(doc.uploadedAt).toLocaleDateString('it-IT', {
                                day: '2-digit',
                                month: '2-digit',
                                year: '2-digit',
                              })}
                            </p>
                          </div>
                          <KbStatusBadge status={doc.status} size="sm" />
                        </div>
                      ))}
                </div>
              </div>
            )}
          </TabsContent>

          {/* ── Agent Tab ─────────────────────────────────────────── */}
          <TabsContent value="agent" className="mt-0">
            {data.agent ? (
              <GameAgentCard agent={data.agent} gameId={data.id} />
            ) : (
              <div className="flex flex-col items-center gap-3 py-8 text-center">
                <Bot className="h-8 w-8 text-slate-300" aria-hidden="true" />
                <div>
                  <p className="font-nunito text-sm font-medium text-slate-600">
                    Nessun agente configurato
                  </p>
                  <p className="font-nunito text-xs text-slate-400 mt-0.5">
                    Crea un agente AI per rispondere alle domande su questo gioco
                  </p>
                </div>
                <a
                  href={`/library/games/${data.id}/agent`}
                  className="inline-flex items-center gap-1.5 rounded-lg bg-orange-500 px-3 py-1.5 font-nunito text-xs font-semibold text-white hover:bg-orange-600 transition-colors"
                  data-testid="game-agent-create-cta"
                >
                  <Bot className="h-3.5 w-3.5" aria-hidden="true" />
                  Crea Agente
                </a>
              </div>
            )}
          </TabsContent>
        </div>
      </Tabs>
    </div>
  );
});

// ── Game sub-components ────────────────────────────────────────────────────

/** Compact agent card rendered inside GameExtraMeepleCard Agent tab (Issue #5029) */
function GameAgentCard({ agent, gameId }: { agent: GameAgentPreview; gameId: string }) {
  const status = agent.isActive ? ('active' as const) : ('idle' as const);
  return (
    <div className="space-y-3" data-testid="game-agent-card">
      {/* Status + info row */}
      <div className="flex items-center justify-between gap-2 rounded-lg bg-orange-50/50 border border-orange-200/40 p-3">
        <div className="flex items-center gap-2 min-w-0">
          <Bot className="h-4 w-4 shrink-0 text-orange-500" aria-hidden="true" />
          <div className="min-w-0">
            <p className="font-quicksand text-sm font-bold text-orange-700 truncate">
              {agent.name}
            </p>
            {agent.model && (
              <p className="font-nunito text-[10px] text-orange-500">{agent.model}</p>
            )}
          </div>
        </div>
        <AgentStatusBadge status={status} showLabel size="sm" />
      </div>

      {/* CTAs */}
      <div className="flex gap-2">
        <a
          href={agent.id ? `/chat/new?agentId=${agent.id}` : `/library/games/${gameId}/agent`}
          className="flex-1 inline-flex items-center justify-center gap-1.5 rounded-lg bg-orange-500 px-3 py-2 font-nunito text-xs font-semibold text-white hover:bg-orange-600 transition-colors"
          data-testid="game-agent-start-chat"
        >
          <MessageCircle className="h-3.5 w-3.5" aria-hidden="true" />
          Avvia Chat
        </a>
        <a
          href={`/library/games/${gameId}/agent`}
          className="flex-1 inline-flex items-center justify-center gap-1.5 rounded-lg border border-orange-200 bg-orange-50/60 px-3 py-2 font-nunito text-xs font-semibold text-orange-700 hover:bg-orange-100 transition-colors"
          data-testid="game-agent-configure"
        >
          <Settings className="h-3.5 w-3.5" aria-hidden="true" />
          Configura Agente
        </a>
      </div>
    </div>
  );
}
