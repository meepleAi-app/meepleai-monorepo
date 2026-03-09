'use client';

/**
 * SharedGameExtraMeepleCard — admin extra card for SharedGame entities
 *
 * Displays game details, PDF documents with indexing status, and KB cards.
 * Used in the admin shared-games catalog via a Sheet panel.
 */

import React, { useState } from 'react';

import {
  BookOpen,
  Bot,
  ExternalLink,
  FileText,
  Gamepad2,
  Star,
  Upload,
  Users,
  Clock,
} from 'lucide-react';
import { AlertCircle, Loader2 } from 'lucide-react';
import Link from 'next/link';

import type {
  SharedGameDetailData,
  SharedGameDocumentInfo,
  SharedGameKbCardInfo,
  SharedGameExtraMeepleCardProps,
  SharedGameExtraMeepleCardTab,
} from '@/components/ui/data-display/extra-meeple-card/types';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/navigation/tabs';
import { cn } from '@/lib/utils';

import { PdfIndexingStatus } from './PdfIndexingStatus';

// ============================================================================
// Constants
// ============================================================================

const DOC_TYPE_LABELS: Record<number, string> = {
  0: 'Rulebook',
  1: 'Errata',
  2: 'Homerule',
};

const KB_STATUS_STYLES: Record<string, string> = {
  completed: 'bg-green-100 text-green-700 border-green-200',
  processing: 'bg-blue-100 text-blue-700 border-blue-200',
  pending: 'bg-amber-100 text-amber-700 border-amber-200',
  failed: 'bg-red-100 text-red-700 border-red-200',
};

const COLORS = {
  hsl: '25 95% 45%',
  accent: 'text-orange-700',
  accentBg: 'bg-orange-100',
  accentBorder: 'border-orange-200/60',
  activeAccent: 'data-[state=active]:text-orange-700',
};

// ============================================================================
// Sub-components
// ============================================================================

function LoadingState({ className, testId }: { className?: string; testId?: string }) {
  return (
    <div
      className={cn(
        'flex h-[600px] w-full items-center justify-center rounded-2xl',
        'bg-white/70 backdrop-blur-md shadow-lg border border-white/20',
        className
      )}
      data-testid={testId}
    >
      <div className="flex flex-col items-center gap-3 text-slate-400">
        <Loader2 className="h-8 w-8 animate-spin" />
        <span className="font-nunito text-sm">Caricamento...</span>
      </div>
    </div>
  );
}

function ErrorState({
  error,
  className,
  testId,
}: {
  error: string;
  className?: string;
  testId?: string;
}) {
  return (
    <div
      className={cn(
        'flex h-[600px] w-full items-center justify-center rounded-2xl',
        'bg-white/70 backdrop-blur-md shadow-lg border border-white/20',
        className
      )}
      data-testid={testId}
    >
      <div className="flex flex-col items-center gap-3 text-red-400">
        <AlertCircle className="h-8 w-8" />
        <span className="font-nunito text-sm">{error}</span>
      </div>
    </div>
  );
}

function StatCard({
  label,
  value,
  icon: Icon,
}: {
  label: string;
  value: string;
  icon: React.ComponentType<{ className?: string }>;
}) {
  return (
    <div className={cn('rounded-lg border p-2.5', COLORS.accentBorder, `${COLORS.accentBg}/30`)}>
      <div className="flex items-center gap-1.5 mb-1">
        <Icon className={cn('h-3.5 w-3.5', COLORS.accent)} />
        <span className="font-nunito text-[10px] text-slate-500">{label}</span>
      </div>
      <p className={cn('font-quicksand text-sm font-bold', COLORS.accent)}>{value}</p>
    </div>
  );
}

function TabTrigger({
  value,
  icon: Icon,
  label,
  badge,
}: {
  value: string;
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  badge?: number;
}) {
  return (
    <TabsTrigger
      value={value}
      className={cn(
        'flex items-center gap-1.5 px-3 py-1.5 font-nunito text-xs font-medium',
        'data-[state=active]:bg-white data-[state=active]:shadow-sm',
        COLORS.activeAccent,
        'transition-all duration-200'
      )}
    >
      <Icon className="h-3.5 w-3.5" />
      <span>{label}</span>
      {badge !== undefined && badge > 0 && (
        <span
          className={cn(
            'ml-0.5 rounded-full px-1.5 py-0 text-[9px] font-bold',
            COLORS.accentBg,
            COLORS.accent
          )}
        >
          {badge}
        </span>
      )}
    </TabsTrigger>
  );
}

// ============================================================================
// Details Tab
// ============================================================================

function DetailsTab({ data }: { data: SharedGameDetailData }) {
  return (
    <div className="space-y-3">
      <div className="grid grid-cols-3 gap-2">
        {data.minPlayers != null && (
          <StatCard
            label="Giocatori"
            value={`${data.minPlayers}-${data.maxPlayers ?? data.minPlayers}`}
            icon={Users}
          />
        )}
        {data.playTimeMinutes != null && (
          <StatCard label="Durata" value={`${data.playTimeMinutes}m`} icon={Clock} />
        )}
        {data.totalPlays != null && (
          <StatCard label="Partite" value={data.totalPlays.toString()} icon={Gamepad2} />
        )}
      </div>
      {data.averageRating != null && (
        <div className="flex items-center gap-2 rounded-lg bg-orange-50/50 border border-orange-200/40 p-3">
          <Star className="h-5 w-5 text-orange-500" />
          <div>
            <p className="font-quicksand text-lg font-bold text-orange-700">
              {data.averageRating.toFixed(1)}
            </p>
            <p className="font-nunito text-[10px] text-orange-500">Rating medio</p>
          </div>
        </div>
      )}
      {data.description && (
        <p className="font-nunito text-xs text-slate-600 leading-relaxed">{data.description}</p>
      )}
      {data.linkedAgent && (
        <div className="flex items-center gap-2 rounded-lg bg-amber-50/50 border border-amber-200/40 p-3">
          <Bot className="h-4 w-4 text-amber-600" />
          <div className="flex-1 min-w-0">
            <p className="font-nunito text-[10px] text-amber-500 uppercase tracking-wider">
              Agente Collegato
            </p>
            <p className="font-quicksand text-sm font-bold text-amber-700 truncate">
              {data.linkedAgent.name}
            </p>
          </div>
          <span
            className={cn(
              'rounded-full px-2 py-0.5 text-[9px] font-bold font-nunito',
              data.linkedAgent.isActive
                ? 'bg-green-100 text-green-700'
                : 'bg-slate-100 text-slate-500'
            )}
          >
            {data.linkedAgent.isActive ? 'Attivo' : 'Inattivo'}
          </span>
        </div>
      )}
    </div>
  );
}

// ============================================================================
// Documents Tab
// ============================================================================

function DocumentRow({ doc }: { doc: SharedGameDocumentInfo }) {
  const typeLabel = DOC_TYPE_LABELS[doc.documentType] ?? `Tipo ${doc.documentType}`;

  return (
    <div className="rounded-lg bg-white/50 border border-slate-200/40 p-3 space-y-2">
      <div className="flex items-center justify-between gap-2">
        <div className="flex items-center gap-2 min-w-0">
          <FileText className="h-4 w-4 text-slate-400 shrink-0" />
          <span className="font-nunito text-xs font-medium text-slate-700 truncate">
            {typeLabel}
          </span>
          <span className="font-nunito text-[10px] text-slate-400">v{doc.version}</span>
        </div>
        {doc.isActive && (
          <span className="shrink-0 rounded-full px-2 py-0.5 text-[9px] font-bold font-nunito bg-green-100 text-green-700">
            Attivo
          </span>
        )}
      </div>
      <PdfIndexingStatus pdfId={doc.pdfDocumentId} compact />
    </div>
  );
}

function DocumentsTab({
  data,
  onUploadPdf,
}: {
  data: SharedGameDetailData;
  onUploadPdf?: () => void;
}) {
  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <span className="font-nunito text-xs text-slate-500">
          {data.documents.length} documento{data.documents.length !== 1 ? 'i' : ''}
        </span>
        <div className="flex items-center gap-2">
          <Link
            href="/admin/knowledge-base/queue"
            className="flex items-center gap-1 font-nunito text-[10px] text-orange-600 hover:text-orange-700 transition-colors"
          >
            Vedi coda
            <ExternalLink className="h-3 w-3" />
          </Link>
          <button
            type="button"
            onClick={onUploadPdf}
            className={cn(
              'flex items-center gap-1 rounded-lg px-2.5 py-1.5',
              'font-nunito text-[10px] font-bold',
              'bg-orange-100 text-orange-700 border border-orange-200',
              'hover:bg-orange-200 transition-colors'
            )}
          >
            <Upload className="h-3 w-3" />
            Upload PDF
          </button>
        </div>
      </div>

      {data.documents.length === 0 ? (
        <div className="rounded-lg bg-slate-50 border border-slate-200/40 p-6 text-center">
          <FileText className="h-8 w-8 text-slate-300 mx-auto mb-2" />
          <p className="font-nunito text-xs text-slate-400">Nessun documento caricato</p>
          <p className="font-nunito text-[10px] text-slate-300 mt-1">
            Carica un PDF per iniziare l&apos;indicizzazione
          </p>
        </div>
      ) : (
        <div className="space-y-2">
          {data.documents.map(doc => (
            <DocumentRow key={doc.id} doc={doc} />
          ))}
        </div>
      )}
    </div>
  );
}

// ============================================================================
// KB Cards Tab
// ============================================================================

function KbCardsTab({
  data,
  onCreateAgent,
}: {
  data: SharedGameDetailData;
  onCreateAgent?: () => void;
}) {
  const completedCards = data.kbCards.filter(c => c.indexingStatus === 'completed');

  // Group by pdfDocumentId to show cards per source PDF
  const groupedByPdf = data.kbCards.reduce<Record<string, SharedGameKbCardInfo[]>>((acc, card) => {
    if (!acc[card.pdfDocumentId]) acc[card.pdfDocumentId] = [];

    acc[card.pdfDocumentId].push(card);
    return acc;
  }, {});

  const groups = Object.entries(groupedByPdf);

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <span className="font-nunito text-xs text-slate-500">
          {completedCards.length} card indicizzat{completedCards.length !== 1 ? 'e' : 'a'}
        </span>
        <button
          type="button"
          disabled={completedCards.length === 0}
          onClick={onCreateAgent}
          className={cn(
            'flex items-center gap-1 rounded-lg px-2.5 py-1.5',
            'font-nunito text-[10px] font-bold transition-colors',
            completedCards.length > 0
              ? 'bg-amber-100 text-amber-700 border border-amber-200 hover:bg-amber-200'
              : 'bg-slate-100 text-slate-400 border border-slate-200 cursor-not-allowed'
          )}
        >
          <Bot className="h-3 w-3" />
          Crea Agente
        </button>
      </div>

      {groups.length === 0 ? (
        <div className="rounded-lg bg-slate-50 border border-slate-200/40 p-6 text-center">
          <BookOpen className="h-8 w-8 text-slate-300 mx-auto mb-2" />
          <p className="font-nunito text-xs text-slate-400">Nessuna KB card generata</p>
          <p className="font-nunito text-[10px] text-slate-300 mt-1">
            Indicizza un PDF per creare le card
          </p>
        </div>
      ) : (
        <div className="space-y-3">
          {groups.map(([pdfId, cards]) => {
            const fileName = cards[0]?.fileName ?? pdfId.slice(0, 8);
            return (
              <div
                key={pdfId}
                className="rounded-lg bg-white/50 border border-slate-200/40 overflow-hidden"
              >
                {/* Group header */}
                <div className="flex items-center gap-2 px-3 py-2 bg-slate-50/50 border-b border-slate-200/40">
                  <FileText className="h-3.5 w-3.5 text-slate-400 shrink-0" />
                  <span className="font-nunito text-[10px] font-bold text-slate-600 truncate">
                    {fileName}
                  </span>
                  <span className="ml-auto font-nunito text-[9px] text-slate-400 shrink-0">
                    {cards.length} card
                  </span>
                </div>
                {/* Cards list */}
                <div className="divide-y divide-slate-100/60">
                  {cards.map(card => {
                    const statusStyle =
                      KB_STATUS_STYLES[card.indexingStatus] ??
                      'bg-slate-100 text-slate-600 border-slate-200';
                    return (
                      <div
                        key={card.id}
                        className="flex items-center justify-between px-3 py-2 gap-2"
                      >
                        <div className="flex items-center gap-2 min-w-0">
                          <span
                            className={cn(
                              'rounded-full px-2 py-0.5 text-[9px] font-bold font-nunito border',
                              statusStyle
                            )}
                          >
                            {card.indexingStatus}
                          </span>
                          {card.documentType && (
                            <span className="font-nunito text-[9px] text-slate-400">
                              {card.documentType}
                            </span>
                          )}
                        </div>
                        <div className="flex items-center gap-2 shrink-0">
                          <span className="font-nunito text-[9px] text-slate-400">
                            {card.chunkCount} chunk
                          </span>
                          {card.indexedAt && (
                            <span className="font-nunito text-[9px] text-slate-300">
                              {new Date(card.indexedAt).toLocaleDateString('it-IT', {
                                day: '2-digit',
                                month: '2-digit',
                              })}
                            </span>
                          )}
                        </div>
                      </div>
                    );
                  })}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </div>
  );
}

// ============================================================================
// SharedGameExtraMeepleCard
// ============================================================================

export const SharedGameExtraMeepleCard = React.memo(function SharedGameExtraMeepleCard({
  data,
  onUploadPdf,
  onCreateAgent,
  loading,
  error,
  className,
  'data-testid': testId,
}: SharedGameExtraMeepleCardProps) {
  const [activeTab, setActiveTab] = useState<SharedGameExtraMeepleCardTab>('details');

  if (loading) return <LoadingState className={className} testId={testId} />;
  if (error) return <ErrorState error={error} className={className} testId={testId} />;

  return (
    <div
      className={cn(
        'flex w-full flex-col rounded-2xl overflow-hidden',
        'bg-white/70 backdrop-blur-md shadow-[0_8px_32px_rgba(0,0,0,0.12)] border border-white/20',
        className
      )}
      data-testid={testId}
    >
      {/* Header */}
      <div className="relative h-[140px] overflow-hidden">
        {data.imageUrl ? (
          <div
            className="absolute inset-0 bg-cover bg-center"
            style={{ backgroundImage: `url(${data.imageUrl})` }}
          />
        ) : (
          <div className="absolute inset-0" style={{ background: `hsl(${COLORS.hsl})` }} />
        )}
        <div className="absolute inset-0 bg-gradient-to-b from-black/40 via-black/50 to-black/70" />
        <div
          className="absolute bottom-0 left-0 right-0 h-1"
          style={{ background: `hsl(${COLORS.hsl})` }}
        />

        <div className="relative flex h-full flex-col justify-end p-5">
          <div className="flex items-end justify-between">
            <div className="space-y-0.5">
              <h2 className="font-quicksand text-xl font-bold text-white leading-tight line-clamp-2">
                {data.title}
              </h2>
              {data.publisher && (
                <p className="font-nunito text-sm text-white/70">
                  {data.publisher}
                  {data.yearPublished ? ` (${data.yearPublished})` : ''}
                </p>
              )}
            </div>
            <div className="flex items-center gap-1 rounded-full bg-white/20 backdrop-blur-sm px-2.5 py-1">
              <FileText className="h-3 w-3 text-white" />
              <span className="font-quicksand text-sm font-bold text-white">
                {data.documents.length} doc
              </span>
            </div>
          </div>
        </div>
      </div>

      {/* Tabs */}
      <Tabs
        value={activeTab}
        onValueChange={v => setActiveTab(v as SharedGameExtraMeepleCardTab)}
        className="flex flex-1 flex-col"
      >
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-slate-100/80 rounded-lg p-1">
          <TabTrigger value="details" icon={Gamepad2} label="Dettagli" />
          <TabTrigger
            value="documents"
            icon={FileText}
            label="Documenti"
            badge={data.documents.length}
          />
          <TabTrigger
            value="kb-cards"
            icon={BookOpen}
            label="KB Cards"
            badge={data.kbCards.length}
          />
        </TabsList>

        <div className="flex-1 overflow-y-auto px-4 py-3">
          <TabsContent value="details" className="mt-0">
            <DetailsTab data={data} />
          </TabsContent>

          <TabsContent value="documents" className="mt-0">
            <DocumentsTab data={data} onUploadPdf={onUploadPdf} />
          </TabsContent>

          <TabsContent value="kb-cards" className="mt-0">
            <KbCardsTab data={data} onCreateAgent={onCreateAgent} />
          </TabsContent>
        </div>
      </Tabs>
    </div>
  );
});
