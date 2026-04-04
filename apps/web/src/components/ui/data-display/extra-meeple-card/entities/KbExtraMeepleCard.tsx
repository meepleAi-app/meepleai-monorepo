'use client';

/**
 * KbExtraMeepleCard — expanded card for KB document entities
 * Issue #5028 — KbExtraMeepleCard (Epic #5023)
 */

import React, { useState } from 'react';

import {
  Activity,
  BookOpen,
  Calendar,
  CheckCircle2,
  Circle,
  File,
  FileText,
  Gamepad2,
  Loader2,
  RefreshCw,
  Trash2,
  XCircle,
} from 'lucide-react';

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

import type { KbDetailData } from '../types';

// ============================================================================
// Types
// ============================================================================

export interface KbExtraMeepleCardProps {
  data: KbDetailData;
  loading?: boolean;
  error?: string;
  /** Called when user confirms document deletion */
  onDelete?: () => void;
  /** Called when user clicks retry indexing */
  onRetryIndexing?: () => void;
  className?: string;
  'data-testid'?: string;
}

type KbTab = 'overview' | 'content' | 'status';

// ── KB helpers ──────────────────────────────────────────────────────────────

function formatBytes(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
}

type KbTimelineStatus = 'done' | 'active' | 'pending' | 'failed';

function deriveTimelineStatus(
  step: 1 | 2 | 3 | 4,
  status: KbDetailData['status'],
  hasContent: boolean
): KbTimelineStatus {
  if (step === 1) return 'done';
  if (status === 'indexed') return 'done';
  if (status === 'failed') {
    if (step === 2) return hasContent ? 'done' : 'failed';
    if (step === 3) return hasContent ? 'failed' : 'pending';
    return 'failed';
  }
  if (status === 'processing') {
    if (step === 2) return hasContent ? 'done' : 'active';
    if (step === 3) return hasContent ? 'active' : 'pending';
    return 'pending';
  }
  return 'pending';
}

// ============================================================================
// KbExtraMeepleCard
// ============================================================================

export const KbExtraMeepleCard = React.memo(function KbExtraMeepleCard({
  data,
  loading,
  error,
  onDelete,
  onRetryIndexing,
  className,
  'data-testid': testId,
}: KbExtraMeepleCardProps) {
  const [activeTab, setActiveTab] = useState<KbTab>('overview');
  const [confirmingDelete, setConfirmingDelete] = useState(false);
  const colors = ENTITY_COLORS.kb;

  if (loading) return <EntityLoadingState className={className} testId={testId} />;
  if (error) return <EntityErrorState error={error} className={className} testId={testId} />;

  const hasContent = Boolean(data.extractedContent);
  const step2 = deriveTimelineStatus(2, data.status, hasContent);
  const step3 = deriveTimelineStatus(3, data.status, hasContent);
  const step4 = deriveTimelineStatus(4, data.status, hasContent);

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
        title={data.fileName}
        subtitle="Documento KB"
        color={colors.hsl}
        badge={data.status === 'indexed' ? 'Indicizzata' : undefined}
        badgeIcon={
          data.status === 'indexed' ? (
            <CheckCircle2 className="h-3.5 w-3.5 text-white" />
          ) : undefined
        }
      />

      {/* Tabs */}
      <Tabs
        value={activeTab}
        onValueChange={v => setActiveTab(v as KbTab)}
        className="flex flex-1 flex-col"
      >
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-slate-100/80 rounded-lg p-1">
          <EntityTabTrigger
            value="overview"
            icon={FileText}
            label="Overview"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="content"
            icon={BookOpen}
            label="Contenuto"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="status"
            icon={Activity}
            label="Stato"
            activeAccent={colors.activeAccent}
          />
        </TabsList>

        {/* ── Overview ───────────────────────────────────────────── */}
        <TabsContent value="overview" className="flex-1 overflow-y-auto p-4 space-y-4">
          {/* Status badge */}
          <KbStatusBadge status={data.status} size="md" />

          {/* Filename chip */}
          <div className="flex items-center gap-2 rounded-lg border border-teal-200/40 bg-teal-50/60 px-3 py-2">
            <File className="h-4 w-4 shrink-0 text-teal-600" aria-hidden="true" />
            <span className="font-nunito text-sm font-medium text-slate-700 truncate">
              {data.fileName}
            </span>
          </div>

          {/* Game chip */}
          {data.gameName && (
            <div className="flex items-center gap-2.5 rounded-xl border border-slate-200/40 bg-slate-50/80 px-3 py-2.5">
              <Gamepad2 className="h-4 w-4 shrink-0 text-slate-500" aria-hidden="true" />
              <div className="min-w-0">
                <p className="font-nunito text-[10px] text-slate-400">Gioco</p>
                <p className="font-quicksand text-sm font-semibold text-slate-700 truncate">
                  {data.gameName}
                </p>
              </div>
            </div>
          )}

          {/* Stat cards */}
          <div className="grid grid-cols-2 gap-2">
            {data.fileSize != null && (
              <StatCard
                label="Dimensione"
                value={formatBytes(data.fileSize)}
                icon={File}
                variant="kb"
              />
            )}
            {data.pageCount != null && (
              <StatCard
                label="Pagine"
                value={String(data.pageCount)}
                icon={FileText}
                variant="kb"
              />
            )}
            {data.uploadedAt && (
              <StatCard
                label="Caricato"
                value={new Date(data.uploadedAt).toLocaleDateString('it-IT', {
                  day: '2-digit',
                  month: '2-digit',
                  year: '2-digit',
                })}
                icon={Calendar}
                variant="kb"
              />
            )}
            {data.processedAt && data.status === 'indexed' && (
              <StatCard
                label="Indicizzato"
                value={new Date(data.processedAt).toLocaleDateString('it-IT', {
                  day: '2-digit',
                  month: '2-digit',
                  year: '2-digit',
                })}
                icon={CheckCircle2}
                variant="kb"
              />
            )}
          </div>

          {/* Delete action */}
          {data.status !== 'processing' && onDelete && (
            <div className="pt-1 border-t border-slate-200/40">
              {confirmingDelete ? (
                <div className="flex items-center gap-2" role="alert">
                  <span className="font-nunito text-xs text-slate-600">
                    Eliminare definitivamente?
                  </span>
                  <button
                    onClick={() => {
                      onDelete();
                      setConfirmingDelete(false);
                    }}
                    className="font-nunito text-xs font-semibold text-red-600 hover:text-red-700 rounded px-2 py-1 hover:bg-red-50 transition-colors"
                    data-testid="kb-action-delete-confirm"
                  >
                    Conferma
                  </button>
                  <button
                    onClick={() => setConfirmingDelete(false)}
                    className="font-nunito text-xs text-slate-500 hover:text-slate-700 rounded px-2 py-1 hover:bg-slate-100 transition-colors"
                    data-testid="kb-action-delete-cancel"
                  >
                    Annulla
                  </button>
                </div>
              ) : (
                <button
                  onClick={() => setConfirmingDelete(true)}
                  className="flex items-center gap-1.5 font-nunito text-xs text-red-500 hover:text-red-600 transition-colors py-1"
                  data-testid="kb-action-delete"
                >
                  <Trash2 className="h-3.5 w-3.5" aria-hidden="true" />
                  Elimina documento
                </button>
              )}
            </div>
          )}
        </TabsContent>

        {/* ── Content ────────────────────────────────────────────── */}
        <TabsContent value="content" className="flex-1 overflow-hidden p-4">
          {data.status !== 'indexed' ? (
            <div className="flex h-full flex-col items-center justify-center gap-3 text-slate-400 py-8">
              <Loader2 className="h-8 w-8 animate-spin text-teal-400" aria-hidden="true" />
              <p className="font-nunito text-sm text-center max-w-[240px]">
                Il contenuto sarà disponibile al termine dell&apos;indicizzazione
              </p>
            </div>
          ) : !data.extractedContent ? (
            <div className="flex h-full flex-col items-center justify-center gap-2 py-8">
              <BookOpen className="h-8 w-8 text-slate-300" aria-hidden="true" />
              <p className="font-nunito text-sm text-slate-400">Nessun testo estratto</p>
            </div>
          ) : (
            <div className="relative h-full overflow-hidden">
              <div className="h-full overflow-y-auto pr-1">
                <div
                  className="font-nunito text-xs text-slate-600 leading-relaxed whitespace-pre-wrap break-words"
                  data-testid="kb-extracted-content"
                >
                  {data.extractedContent}
                </div>
              </div>
              {data.hasMoreContent && (
                <div className="absolute bottom-0 left-0 right-0 flex items-end justify-center pb-2 pt-8 bg-gradient-to-t from-white/95 to-transparent">
                  <a
                    href={`/library/documents/${data.id}`}
                    className={cn(
                      'flex items-center gap-1 font-nunito text-xs font-semibold rounded-full px-3 py-1.5',
                      'bg-teal-50 border border-teal-200/60',
                      colors.accent
                    )}
                    data-testid="kb-action-view-full"
                  >
                    Vedi documento completo
                  </a>
                </div>
              )}
            </div>
          )}
        </TabsContent>

        {/* ── Status / Timeline ──────────────────────────────────── */}
        <TabsContent
          value="status"
          className="flex-1 overflow-y-auto p-4 space-y-4"
          aria-live="polite"
          aria-atomic="true"
        >
          {/* Indexing timeline */}
          <div className="space-y-3">
            <KbTimelineStep label="Caricato" status="done" timestamp={data.uploadedAt} />
            <KbTimelineStep label="Estrazione testo" status={step2} />
            <KbTimelineStep label="Indicizzazione" status={step3} />
            <KbTimelineStep
              label={data.status === 'failed' ? 'Errore' : 'Completato'}
              status={step4}
              timestamp={data.status === 'indexed' ? data.processedAt : undefined}
            />
          </div>

          {/* Processing progress bar */}
          {data.status === 'processing' && (
            <div className="space-y-1.5">
              <div
                className="h-1.5 w-full overflow-hidden rounded-full bg-teal-100"
                role="progressbar"
                aria-label="Elaborazione in corso"
                aria-valuemin={0}
                aria-valuemax={100}
              >
                <div className="h-full rounded-full bg-teal-500 animate-[pulse_1.5s_ease-in-out_infinite] w-2/3" />
              </div>
              <p className="font-nunito text-xs text-slate-500">Elaborazione in corso…</p>
            </div>
          )}

          {/* Failed state */}
          {data.status === 'failed' && (
            <div className="rounded-xl border border-red-200/60 bg-red-50/60 p-3 space-y-2">
              <p className="font-nunito text-sm text-red-700">
                {data.errorMessage ?? "Si è verificato un errore durante l'indicizzazione."}
              </p>
              {onRetryIndexing && (
                <button
                  onClick={onRetryIndexing}
                  className="flex items-center gap-1.5 font-nunito text-xs font-semibold text-red-600 hover:text-red-700 rounded-lg border border-red-200/60 bg-white/60 px-3 py-1.5 transition-colors hover:bg-red-50"
                  data-testid="kb-action-retry-indexing"
                >
                  <RefreshCw className="h-3.5 w-3.5" aria-hidden="true" />
                  Riprova indicizzazione
                </button>
              )}
            </div>
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
});

// ── KB sub-components ───────────────────────────────────────────────────────

const KB_TIMELINE_CONFIG: Record<
  KbTimelineStatus,
  {
    Icon: React.ComponentType<{ className?: string }>;
    iconClass: string;
    labelClass: string;
  }
> = {
  done: { Icon: CheckCircle2, iconClass: 'text-teal-500', labelClass: 'text-slate-700' },
  active: { Icon: Loader2, iconClass: 'text-blue-500', labelClass: 'text-slate-700' },
  pending: { Icon: Circle, iconClass: 'text-slate-300', labelClass: 'text-slate-400' },
  failed: { Icon: XCircle, iconClass: 'text-red-500', labelClass: 'text-slate-700' },
};

function KbTimelineStep({
  label,
  status,
  timestamp,
}: {
  label: string;
  status: KbTimelineStatus;
  timestamp?: string;
}) {
  const { Icon, iconClass, labelClass } = KB_TIMELINE_CONFIG[status];
  return (
    <div className="flex items-start gap-3">
      <Icon
        className={cn('h-4 w-4 mt-0.5 shrink-0', iconClass, status === 'active' && 'animate-spin')}
        aria-hidden="true"
      />
      <div className="flex-1 min-w-0">
        <p className={cn('font-nunito text-sm', labelClass)}>{label}</p>
        {timestamp && status === 'done' && (
          <p className="font-nunito text-[10px] text-slate-400">
            {new Date(timestamp).toLocaleDateString('it-IT', {
              day: '2-digit',
              month: '2-digit',
              year: '2-digit',
            })}
          </p>
        )}
      </div>
    </div>
  );
}
