/**
 * RAG Readiness Indicator - Admin RAG Dashboard
 *
 * Horizontal stepper showing cross-BC readiness status:
 * 📄 Documenti → ⚙️ Elaborazione → 🤖 Agente → 💬 Chat
 */

'use client';

import {
  FileText,
  Loader2,
  CheckCircle2,
  XCircle,
  Bot,
  MessageSquare,
  AlertTriangle,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { Progress } from '@/components/ui/feedback/progress';
import type { GameRagReadiness } from '@/lib/api/schemas/rag-setup.schemas';
import { READINESS_STATES } from '@/lib/api/schemas/rag-setup.schemas';
import { cn } from '@/lib/utils';

interface RagReadinessIndicatorProps {
  readiness: GameRagReadiness;
}

const STEPS = [
  { id: 'documents', label: 'Documenti', icon: FileText },
  { id: 'processing', label: 'Elaborazione', icon: Loader2 },
  { id: 'agent', label: 'Agente', icon: Bot },
  { id: 'chat', label: 'Chat', icon: MessageSquare },
] as const;

function getActiveStep(overallReadiness: string): number {
  switch (overallReadiness) {
    case READINESS_STATES.NO_DOCUMENTS:
      return 0;
    case READINESS_STATES.DOCUMENTS_PROCESSING:
      return 1;
    case READINESS_STATES.DOCUMENTS_FAILED:
      return 1;
    case READINESS_STATES.READY_FOR_AGENT:
      return 2;
    case READINESS_STATES.FULLY_OPERATIONAL:
      return 4; // all complete
    default:
      return 0;
  }
}

function getStatusBadge(overallReadiness: string) {
  switch (overallReadiness) {
    case READINESS_STATES.NO_DOCUMENTS:
      return (
        <Badge variant="secondary">
          <FileText className="mr-1 h-3 w-3" />
          Nessun documento
        </Badge>
      );
    case READINESS_STATES.DOCUMENTS_PROCESSING:
      return (
        <Badge variant="secondary" className="animate-pulse">
          <Loader2 className="mr-1 h-3 w-3 animate-spin" />
          Elaborazione in corso
        </Badge>
      );
    case READINESS_STATES.DOCUMENTS_FAILED:
      return (
        <Badge variant="destructive">
          <XCircle className="mr-1 h-3 w-3" />
          Elaborazione fallita
        </Badge>
      );
    case READINESS_STATES.READY_FOR_AGENT:
      return (
        <Badge variant="default" className="bg-amber-600 hover:bg-amber-700">
          <Bot className="mr-1 h-3 w-3" />
          Pronto per agente
        </Badge>
      );
    case READINESS_STATES.FULLY_OPERATIONAL:
      return (
        <Badge variant="default" className="bg-green-600 hover:bg-green-700">
          <CheckCircle2 className="mr-1 h-3 w-3" />
          Operativo
        </Badge>
      );
    default:
      return <Badge variant="secondary">Sconosciuto</Badge>;
  }
}

export function RagReadinessIndicator({ readiness }: RagReadinessIndicatorProps) {
  const activeStep = getActiveStep(readiness.overallReadiness);
  const isFailed = readiness.overallReadiness === READINESS_STATES.DOCUMENTS_FAILED;

  return (
    <Card>
      <CardContent className="py-4">
        {/* Status badge */}
        <div className="mb-4 flex items-center justify-between">
          <span className="text-sm font-medium text-muted-foreground">Stato RAG</span>
          {getStatusBadge(readiness.overallReadiness)}
        </div>

        {/* Horizontal stepper */}
        <div className="flex items-center gap-2">
          {STEPS.map((step, index) => {
            const Icon = step.icon;
            const isComplete = index < activeStep;
            const isCurrent = index === activeStep && activeStep < 4;
            const isFailedStep = isFailed && index === 1;

            return (
              <div key={step.id} className="flex flex-1 items-center gap-2">
                <div
                  className={cn(
                    'flex h-9 w-9 shrink-0 items-center justify-center rounded-full border-2 transition-colors',
                    isComplete &&
                      'border-green-500 bg-green-50 text-green-600 dark:bg-green-900/30 dark:text-green-400',
                    isCurrent &&
                      !isFailedStep &&
                      'border-amber-500 bg-amber-50 text-amber-600 dark:bg-amber-900/30 dark:text-amber-400',
                    isFailedStep &&
                      'border-destructive bg-destructive/10 text-destructive',
                    !isComplete &&
                      !isCurrent &&
                      !isFailedStep &&
                      'border-muted text-muted-foreground'
                  )}
                >
                  {isComplete ? (
                    <CheckCircle2 className="h-4 w-4" />
                  ) : isFailedStep ? (
                    <AlertTriangle className="h-4 w-4" />
                  ) : isCurrent && step.id === 'processing' ? (
                    <Loader2 className="h-4 w-4 animate-spin" />
                  ) : (
                    <Icon className="h-4 w-4" />
                  )}
                </div>
                <span
                  className={cn(
                    'text-xs font-medium',
                    isComplete && 'text-green-600 dark:text-green-400',
                    isCurrent && !isFailedStep && 'text-amber-600 dark:text-amber-400',
                    isFailedStep && 'text-destructive',
                    !isComplete && !isCurrent && !isFailedStep && 'text-muted-foreground'
                  )}
                >
                  {step.label}
                </span>
                {index < STEPS.length - 1 && (
                  <div
                    className={cn(
                      'h-0.5 flex-1',
                      isComplete ? 'bg-green-500' : 'bg-muted'
                    )}
                  />
                )}
              </div>
            );
          })}
        </div>

        {/* Per-document progress (when processing) */}
        {readiness.processingDocuments > 0 && (
          <div className="mt-4 space-y-2">
            {readiness.documents
              .filter(
                (d) => d.processingState !== 'Ready' && d.processingState !== 'Failed'
              )
              .map((doc) => (
                <div key={doc.documentId} className="flex items-center gap-3">
                  <span className="min-w-0 flex-1 truncate text-xs text-muted-foreground">
                    {doc.fileName}
                  </span>
                  <Progress
                    value={doc.progressPercentage}
                    className="h-1.5 w-24"
                  />
                  <span className="text-xs font-medium tabular-nums">
                    {doc.progressPercentage}%
                  </span>
                </div>
              ))}
          </div>
        )}

        {/* Failed documents warning */}
        {readiness.failedDocuments > 0 && (
          <div className="mt-3 flex items-center gap-2 rounded-lg border border-destructive/20 bg-destructive/5 p-2 text-xs text-destructive">
            <XCircle className="h-3.5 w-3.5 shrink-0" />
            <span>
              {readiness.failedDocuments} documento/i fallito/i
              {readiness.documents
                .filter((d) => d.processingState === 'Failed')
                .map((d) => d.errorMessage)
                .filter(Boolean)
                .length > 0 &&
                `: ${readiness.documents
                  .filter((d) => d.processingState === 'Failed')
                  .map((d) => d.errorMessage)
                  .filter(Boolean)
                  .join(', ')}`}
            </span>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
