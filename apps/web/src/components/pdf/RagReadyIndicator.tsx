/**
 * RagReadyIndicator Component (Issue #4065)
 *
 * Displays embedding status with chunk progress, RAG readiness badge,
 * and "Chat Now" CTA when knowledge base processing completes.
 *
 * Features:
 * - Multi-stage progress (Extracting → Chunking → Embedding → Ready)
 * - Chunk progress display (e.g., "87/120")
 * - "Knowledge Base Ready" badge with fade-in animation
 * - "Chat Now" CTA button → /chat?gameId={id}
 * - Error state with "Retry Upload" button
 * - ARIA live region for screen reader announcements
 */

'use client';

import { useCallback, useEffect, useRef } from 'react';

import {
  Brain,
  CheckCircle2,
  FileText,
  Loader2,
  MessageSquare,
  RefreshCw,
  Scissors,
  XCircle,
} from 'lucide-react';
import Link from 'next/link';

import { Progress } from '@/components/ui/feedback/progress';
import { Button } from '@/components/ui/primitives/button';
import { useEmbeddingStatus, type UseEmbeddingStatusOptions } from '@/hooks/useEmbeddingStatus';
import { cn } from '@/lib/utils';

import type { EmbeddingStatus } from '@/lib/api/schemas/knowledge-base.schemas';

// ============================================================================
// Types
// ============================================================================

export interface RagReadyIndicatorProps {
  /** Game ID to track embedding status */
  gameId: string;
  /** Game name for display purposes */
  gameName?: string;
  /** Enable polling (default: true) */
  enabled?: boolean;
  /** Callback when RAG is ready */
  onReady?: (gameName?: string) => void;
  /** Callback when processing fails */
  onError?: (errorMessage: string) => void;
  /** Callback for retry upload action */
  onRetryUpload?: () => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Constants
// ============================================================================

const EMBEDDING_STAGES: EmbeddingStatus[] = ['Extracting', 'Chunking', 'Embedding', 'Completed'];

interface StageConfig {
  icon: typeof Brain;
  label: string;
}

const STAGE_CONFIG: Record<string, StageConfig> = {
  Extracting: { icon: FileText, label: 'Estrazione' },
  Chunking: { icon: Scissors, label: 'Suddivisione' },
  Embedding: { icon: Brain, label: 'Embedding' },
  Completed: { icon: CheckCircle2, label: 'Pronto' },
};

function getStageIndex(status: EmbeddingStatus): number {
  return EMBEDDING_STAGES.indexOf(status);
}

// ============================================================================
// Stage Indicator Sub-Component
// ============================================================================

interface StageIndicatorProps {
  stage: EmbeddingStatus;
  currentStatus: EmbeddingStatus;
  index: number;
}

function StageIndicator({ stage, currentStatus, index }: StageIndicatorProps) {
  // eslint-disable-next-line security/detect-object-injection -- stage from typed enum
  const config = STAGE_CONFIG[stage];
  if (!config) return null;
  const Icon = config.icon;

  const currentIdx = getStageIndex(currentStatus);
  const stageIdx = getStageIndex(stage);
  const isFailed = currentStatus === 'Failed';

  const isCompleted = !isFailed && stageIdx < currentIdx;
  const isActive = !isFailed && stageIdx === currentIdx;

  return (
    <div
      className={cn(
        'flex flex-col items-center gap-1.5 transition-all duration-300',
        isCompleted && 'opacity-100',
        isActive && 'scale-105',
        !isCompleted && !isActive && 'opacity-40',
      )}
      data-testid={`embedding-stage-${stage.toLowerCase()}`}
    >
      <div
        className={cn(
          'flex h-8 w-8 items-center justify-center rounded-full border-2 transition-all duration-300',
          isCompleted && 'border-green-500 bg-green-500/10 text-green-600',
          isActive && 'border-primary bg-primary/10 text-primary animate-pulse',
          !isCompleted && !isActive && 'border-muted-foreground/30 text-muted-foreground/40',
          isFailed && 'border-destructive bg-destructive/10 text-destructive',
        )}
      >
        {isActive && !isFailed ? (
          <Loader2 className="h-4 w-4 animate-spin" />
        ) : (
          <Icon className="h-4 w-4" />
        )}

        {index < EMBEDDING_STAGES.length - 1 && (
          <div
            className={cn(
              'absolute left-full top-1/2 h-0.5 w-6 -translate-y-1/2 transition-colors duration-300',
              isCompleted ? 'bg-green-500' : 'bg-muted-foreground/20',
            )}
          />
        )}
      </div>
      <span
        className={cn(
          'text-[10px] font-medium',
          isCompleted && 'text-green-600',
          isActive && 'text-primary',
          !isCompleted && !isActive && 'text-muted-foreground/40',
        )}
      >
        {config.label}
      </span>
    </div>
  );
}

// ============================================================================
// Main Component
// ============================================================================

export function RagReadyIndicator({
  gameId,
  gameName,
  enabled = true,
  onReady,
  onError,
  onRetryUpload,
  className,
}: RagReadyIndicatorProps) {
  const hookOptions: UseEmbeddingStatusOptions = {
    enabled,
    onReady,
    onError,
  };

  const { data, isLoading, isPolling, isReady, isFailed, stageLabel, chunkProgress, error, refetch } =
    useEmbeddingStatus(gameId, hookOptions);

  // Browser notification on completion
  const hasNotifiedBrowserRef = useRef(false);
  const sendBrowserNotification = useCallback(
    (name?: string) => {
      if (hasNotifiedBrowserRef.current) return;
      if (typeof Notification === 'undefined' || Notification.permission !== 'granted') return;

      hasNotifiedBrowserRef.current = true;
      const displayName = name || gameName || 'Game';
      new Notification('Knowledge Base Ready', {
        body: `${displayName} is now ready for AI chat`,
        icon: '/favicon.ico',
      });
    },
    [gameName],
  );

  useEffect(() => {
    if (isReady) sendBrowserNotification(data?.gameName);
  }, [isReady, data?.gameName, sendBrowserNotification]);

  const currentStatus = data?.status ?? 'Pending';

  // Loading state
  if (isLoading) {
    return (
      <div className={cn('rounded-lg border bg-card p-4', className)} data-testid="rag-indicator-loading">
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <Loader2 className="h-4 w-4 animate-spin" />
          Verifica stato Knowledge Base...
        </div>
      </div>
    );
  }

  // Network error
  if (error && !data) {
    return (
      <div
        className={cn('rounded-lg border border-destructive/50 bg-destructive/5 p-4', className)}
        role="alert"
        data-testid="rag-indicator-error"
      >
        <div className="flex items-center gap-3">
          <XCircle className="h-5 w-5 shrink-0 text-destructive" />
          <div className="flex-1">
            <p className="text-sm font-medium text-destructive">Errore di connessione</p>
            <p className="text-xs text-muted-foreground">{error.message}</p>
          </div>
          <Button variant="outline" size="sm" onClick={refetch}>
            <RefreshCw className="mr-1.5 h-3.5 w-3.5" />
            Riprova
          </Button>
        </div>
      </div>
    );
  }

  // Pending state (no processing yet)
  if (currentStatus === 'Pending' && !isPolling) {
    return null;
  }

  return (
    <div
      className={cn('rounded-lg border bg-card p-4', className)}
      role="region"
      aria-label="Stato Knowledge Base"
      data-testid="rag-ready-indicator"
    >
      {/* Stage indicators (only during processing) */}
      {!isReady && !isFailed && (
        <div className="mb-3 flex items-center justify-between px-2" aria-label="Fasi embedding">
          {EMBEDDING_STAGES.map((stage, index) => (
            <StageIndicator key={stage} stage={stage} currentStatus={currentStatus} index={index} />
          ))}
        </div>
      )}

      {/* Progress bar (during processing) */}
      {!isReady && !isFailed && data && (
        <div className="mb-3">
          <div className="mb-1.5 flex items-center justify-between text-xs">
            <span className="text-muted-foreground" aria-live="polite" data-testid="stage-label">
              {stageLabel}
            </span>
            <span className="font-medium text-muted-foreground" data-testid="chunk-progress">
              {chunkProgress}
            </span>
          </div>
          <Progress
            value={data.progress}
            className="h-1.5"
            aria-label={`Embedding ${data.progress}% completato`}
          />
        </div>
      )}

      {/* RAG Ready State */}
      {isReady && (
        <div className="animate-in fade-in-0 duration-500" data-testid="rag-ready-badge">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-green-500/10">
              <CheckCircle2 className="h-5 w-5 text-green-600" />
            </div>
            <div className="flex-1">
              <p className="text-sm font-semibold text-green-600 dark:text-green-400" aria-live="polite">
                Knowledge Base Pronta
              </p>
              <p className="text-xs text-muted-foreground">
                {gameName || data?.gameName || 'Il gioco'} è pronto per le domande AI
              </p>
            </div>
            <Button asChild size="sm" className="bg-green-600 hover:bg-green-700" data-testid="chat-now-button">
              <Link href={`/chat?gameId=${encodeURIComponent(gameId)}`}>
                <MessageSquare className="mr-1.5 h-4 w-4" />
                Chat Now
              </Link>
            </Button>
          </div>
        </div>
      )}

      {/* Failed State */}
      {isFailed && (
        <div role="alert" aria-live="assertive" data-testid="rag-failed">
          <div className="flex items-center gap-3">
            <XCircle className="h-5 w-5 shrink-0 text-destructive" />
            <div className="flex-1">
              <p className="text-sm font-medium text-destructive">Elaborazione fallita</p>
              {data?.errorMessage && (
                <p className="text-xs text-muted-foreground">{data.errorMessage}</p>
              )}
            </div>
            {onRetryUpload && (
              <Button
                variant="outline"
                size="sm"
                onClick={onRetryUpload}
                data-testid="retry-upload-button"
              >
                <RefreshCw className="mr-1.5 h-3.5 w-3.5" />
                Ricarica PDF
              </Button>
            )}
          </div>
        </div>
      )}
    </div>
  );
}

export default RagReadyIndicator;
