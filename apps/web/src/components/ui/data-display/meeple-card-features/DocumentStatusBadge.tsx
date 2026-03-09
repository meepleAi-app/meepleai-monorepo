/**
 * DocumentStatusBadge
 *
 * Displays the KB indexing status of a document on a MeepleCard.
 * Mirrors the AgentStatusBadge / ChatStatusBadge pattern.
 *
 * Status values:
 *   processing — any in-progress pipeline step (animated)
 *   indexed    — successfully indexed and available for RAG
 *   failed     — processing failed
 *   none       — not yet indexed / no PDF
 *
 * @see Issue #5001 — KB Card: azioni contestuali e visibilità condizionale
 */

'use client';

import { memo } from 'react';

import { CheckCircle2, XCircle, Loader2, MinusCircle } from 'lucide-react';

import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type DocumentIndexingStatus = 'processing' | 'indexed' | 'failed' | 'none';

interface DocumentStatusBadgeProps {
  status: DocumentIndexingStatus;
  size?: 'sm' | 'md';
  className?: string;
}

// ============================================================================
// Config
// ============================================================================

const statusConfig: Record<
  DocumentIndexingStatus,
  {
    label: string;
    icon: React.ElementType;
    animate?: boolean;
    containerClass: string;
    textClass: string;
    iconClass: string;
    testId: string;
  }
> = {
  processing: {
    label: 'In elaborazione',
    icon: Loader2,
    animate: true,
    containerClass: 'bg-blue-50 border-blue-200 dark:bg-blue-950/40 dark:border-blue-800',
    textClass: 'text-blue-700 dark:text-blue-300',
    iconClass: 'text-blue-500 dark:text-blue-400',
    testId: 'document-status-processing',
  },
  indexed: {
    label: 'Indicizzata',
    icon: CheckCircle2,
    containerClass: 'bg-green-50 border-green-200 dark:bg-green-950/40 dark:border-green-800',
    textClass: 'text-green-700 dark:text-green-300',
    iconClass: 'text-green-500 dark:text-green-400',
    testId: 'document-status-indexed',
  },
  failed: {
    label: 'Errore',
    icon: XCircle,
    containerClass: 'bg-red-50 border-red-200 dark:bg-red-950/40 dark:border-red-800',
    textClass: 'text-red-700 dark:text-red-300',
    iconClass: 'text-red-500 dark:text-red-400',
    testId: 'document-status-failed',
  },
  none: {
    label: 'Non indicizzata',
    icon: MinusCircle,
    containerClass: 'bg-muted/50 border-border/60 dark:bg-muted/30',
    textClass: 'text-muted-foreground',
    iconClass: 'text-muted-foreground/70',
    testId: 'document-status-none',
  },
};

// ============================================================================
// Component
// ============================================================================

export const DocumentStatusBadge = memo(function DocumentStatusBadge({
  status,
  size = 'sm',
  className,
}: DocumentStatusBadgeProps) {
  // eslint-disable-next-line security/detect-object-injection -- status is from typed union
  const config = statusConfig[status];
  const Icon = config.icon;

  return (
    <span
      className={cn(
        'inline-flex items-center gap-1 rounded-md border font-semibold',
        size === 'sm' ? 'px-1.5 py-0.5 text-[10px]' : 'px-2 py-1 text-xs',
        config.containerClass,
        config.textClass,
        className
      )}
      aria-label={`Stato documento: ${config.label}`}
      data-testid={config.testId}
    >
      <Icon
        aria-hidden="true"
        className={cn(
          size === 'sm' ? 'w-3 h-3' : 'w-3.5 h-3.5',
          config.iconClass,
          config.animate && 'animate-spin'
        )}
      />
      {config.label}
    </span>
  );
});
