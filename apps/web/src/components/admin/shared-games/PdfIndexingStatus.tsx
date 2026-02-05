/**
 * PDF Indexing Status Component - Issue #3642
 *
 * Displays real-time indexing progress for uploaded PDFs.
 * Polls the status endpoint and shows processing stages.
 *
 * Stages: Uploaded → Processing → Extracted → Chunked → Embedding → Indexed
 */

'use client';

import { useEffect, useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import {
  FileText,
  Loader2,
  CheckCircle2,
  XCircle,
  Clock,
  FileSearch,
  Scissors,
  Brain,
  Database,
  AlertTriangle,
  RefreshCw,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Progress } from '@/components/ui/feedback/progress';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// ========== Types ==========

export type IndexingStage =
  | 'uploaded'
  | 'processing'
  | 'extracted'
  | 'chunked'
  | 'embedding'
  | 'indexed'
  | 'failed';

export interface PdfIndexingStatusData {
  pdfId: string;
  status: IndexingStage;
  progress: number;
  currentStep?: string;
  error?: string;
  chunksProcessed?: number;
  totalChunks?: number;
  completedAt?: string;
}

export interface PdfIndexingStatusProps {
  /** PDF ID to track */
  pdfId: string;
  /** PDF filename for display */
  fileName?: string;
  /** Callback when indexing is complete */
  onComplete?: () => void;
  /** Callback when indexing fails */
  onError?: (error: string) => void;
  /** Whether to show in compact mode */
  compact?: boolean;
}

// ========== Stage Configuration ==========

const STAGES: {
  id: IndexingStage;
  label: string;
  icon: React.ElementType;
  description: string;
}[] = [
  { id: 'uploaded', label: 'Caricato', icon: FileText, description: 'PDF caricato' },
  { id: 'processing', label: 'Elaborazione', icon: Loader2, description: 'Analisi del documento' },
  { id: 'extracted', label: 'Estratto', icon: FileSearch, description: 'Testo estratto' },
  { id: 'chunked', label: 'Suddiviso', icon: Scissors, description: 'Diviso in blocchi' },
  { id: 'embedding', label: 'Embedding', icon: Brain, description: 'Generazione vettori' },
  { id: 'indexed', label: 'Indicizzato', icon: Database, description: 'Pronto per RAG' },
];

const STAGE_ORDER: Record<IndexingStage, number> = {
  uploaded: 0,
  processing: 1,
  extracted: 2,
  chunked: 3,
  embedding: 4,
  indexed: 5,
  failed: -1,
};

// ========== Component ==========

export function PdfIndexingStatus({
  pdfId,
  fileName,
  onComplete,
  onError,
  compact = false,
}: PdfIndexingStatusProps) {
  const [hasCompleted, setHasCompleted] = useState(false);

  const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

  // Poll status endpoint
  const {
    data: status,
    isLoading,
    error,
    refetch,
  } = useQuery<PdfIndexingStatusData>({
    queryKey: ['pdf-indexing-status', pdfId],
    queryFn: async () => {
      const response = await fetch(`${API_BASE}/api/v1/pdfs/${pdfId}/progress`, {
        credentials: 'include',
      });
      if (!response.ok) {
        throw new Error('Errore nel recupero dello stato');
      }
      return response.json();
    },
    refetchInterval: query => {
      // Stop polling when complete or failed
      const queryData = query.state.data;
      if (queryData?.status === 'indexed' || queryData?.status === 'failed') {
        return false;
      }
      return 3000; // Poll every 3 seconds
    },
    enabled: !!pdfId,
  });

  // Handle completion callback
  useEffect(() => {
    if (status?.status === 'indexed' && !hasCompleted) {
      setHasCompleted(true);
      onComplete?.();
    }
    if (status?.status === 'failed' && status.error) {
      onError?.(status.error);
    }
  }, [status, hasCompleted, onComplete, onError]);

  // Get current stage index
  const currentStageIndex = status ? STAGE_ORDER[status.status] : 0;
  const isComplete = status?.status === 'indexed';
  const isFailed = status?.status === 'failed';

  // Calculate overall progress percentage
  const calculateProgress = (): number => {
    if (!status) return 0;
    if (isComplete) return 100;
    if (isFailed) return 0;

    const baseProgress = (currentStageIndex / (STAGES.length - 1)) * 100;

    // Add partial progress within current stage
    if (status.progress) {
      const stageWeight = 100 / (STAGES.length - 1);
      return Math.min(baseProgress + (status.progress / 100) * stageWeight, 99);
    }

    return baseProgress;
  };

  if (isLoading) {
    return (
      <Card className={cn(compact && 'border-0 shadow-none')}>
        <CardContent className="flex items-center justify-center py-6">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
          <span className="ml-2 text-muted-foreground">Caricamento stato...</span>
        </CardContent>
      </Card>
    );
  }

  if (error) {
    return (
      <Card className={cn(compact && 'border-0 shadow-none')}>
        <CardContent className="py-6">
          <div className="flex items-center gap-2 text-destructive">
            <AlertTriangle className="h-5 w-5" />
            <span>Errore nel recupero dello stato</span>
          </div>
          <Button variant="outline" size="sm" className="mt-3" onClick={() => refetch()}>
            <RefreshCw className="h-4 w-4 mr-2" />
            Riprova
          </Button>
        </CardContent>
      </Card>
    );
  }

  // Compact mode for inline display
  if (compact) {
    return (
      <div className="flex items-center gap-3">
        {isComplete ? (
          <>
            <Badge variant="default" className="bg-green-600 hover:bg-green-700">
              <CheckCircle2 className="h-3 w-3 mr-1" />
              RAG Ready
            </Badge>
            {fileName && <span className="text-sm text-muted-foreground">{fileName}</span>}
          </>
        ) : isFailed ? (
          <>
            <Badge variant="destructive">
              <XCircle className="h-3 w-3 mr-1" />
              Errore
            </Badge>
            {status?.error && (
              <span className="text-sm text-destructive">{status.error}</span>
            )}
          </>
        ) : (
          <>
            <Badge variant="secondary" className="animate-pulse">
              <Loader2 className="h-3 w-3 mr-1 animate-spin" />
              {STAGES[currentStageIndex]?.label || 'In corso'}
            </Badge>
            <Progress value={calculateProgress()} className="h-1.5 w-24" />
          </>
        )}
      </div>
    );
  }

  // Full mode with all stages
  return (
    <Card>
      <CardHeader className="pb-3">
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="flex items-center gap-2 text-base">
              {isComplete ? (
                <CheckCircle2 className="h-5 w-5 text-green-600" />
              ) : isFailed ? (
                <XCircle className="h-5 w-5 text-destructive" />
              ) : (
                <Clock className="h-5 w-5 text-amber-500" />
              )}
              Stato Indicizzazione
            </CardTitle>
            {fileName && (
              <CardDescription className="mt-1">{fileName}</CardDescription>
            )}
          </div>
          {isComplete && (
            <Badge variant="default" className="bg-green-600 hover:bg-green-700">
              RAG Ready
            </Badge>
          )}
          {isFailed && <Badge variant="destructive">Fallito</Badge>}
        </div>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Progress Bar */}
        <div className="space-y-1">
          <div className="flex justify-between text-sm">
            <span className="text-muted-foreground">Progresso</span>
            <span className="font-medium">{Math.round(calculateProgress())}%</span>
          </div>
          <Progress
            value={calculateProgress()}
            className={cn('h-2', isFailed && 'bg-destructive/20')}
          />
        </div>

        {/* Stage Timeline */}
        <div className="space-y-2">
          {STAGES.map((stage, index) => {
            const Icon = stage.icon;
            const isCurrentStage = currentStageIndex === index;
            const isCompletedStage = currentStageIndex > index;
            const isFailedAtStage = isFailed && isCurrentStage;

            return (
              <div
                key={stage.id}
                className={cn(
                  'flex items-center gap-3 p-2 rounded-lg transition-colors',
                  isCompletedStage && 'bg-green-50 dark:bg-green-900/20',
                  isCurrentStage && !isFailed && 'bg-amber-50 dark:bg-amber-900/20',
                  isFailedAtStage && 'bg-destructive/10'
                )}
              >
                <div
                  className={cn(
                    'flex items-center justify-center h-8 w-8 rounded-full',
                    isCompletedStage && 'bg-green-100 dark:bg-green-800 text-green-600 dark:text-green-400',
                    isCurrentStage && !isFailed && 'bg-amber-100 dark:bg-amber-800 text-amber-600 dark:text-amber-400',
                    isFailedAtStage && 'bg-destructive/20 text-destructive',
                    !isCompletedStage && !isCurrentStage && 'bg-muted text-muted-foreground'
                  )}
                >
                  {isCompletedStage ? (
                    <CheckCircle2 className="h-4 w-4" />
                  ) : isCurrentStage && !isFailed ? (
                    <Icon className={cn('h-4 w-4', stage.id !== 'uploaded' && 'animate-spin')} />
                  ) : isFailedAtStage ? (
                    <XCircle className="h-4 w-4" />
                  ) : (
                    <Icon className="h-4 w-4" />
                  )}
                </div>
                <div className="flex-1">
                  <p
                    className={cn(
                      'font-medium text-sm',
                      isCompletedStage && 'text-green-700 dark:text-green-300',
                      isCurrentStage && !isFailed && 'text-amber-700 dark:text-amber-300',
                      isFailedAtStage && 'text-destructive',
                      !isCompletedStage && !isCurrentStage && 'text-muted-foreground'
                    )}
                  >
                    {stage.label}
                  </p>
                  <p className="text-xs text-muted-foreground">{stage.description}</p>
                </div>
                {isCurrentStage && status?.chunksProcessed !== undefined && status?.totalChunks && (
                  <span className="text-xs text-muted-foreground">
                    {status.chunksProcessed}/{status.totalChunks}
                  </span>
                )}
              </div>
            );
          })}
        </div>

        {/* Error Message */}
        {isFailed && status?.error && (
          <div className="p-3 bg-destructive/10 border border-destructive/20 rounded-lg">
            <p className="text-sm text-destructive">
              <strong>Errore:</strong> {status.error}
            </p>
          </div>
        )}

        {/* Completion Message */}
        {isComplete && (
          <div className="p-3 bg-green-50 dark:bg-green-900/20 border border-green-200 dark:border-green-800 rounded-lg">
            <p className="text-sm text-green-700 dark:text-green-300">
              Il PDF è stato indicizzato con successo e sarà disponibile per le ricerche RAG.
            </p>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
