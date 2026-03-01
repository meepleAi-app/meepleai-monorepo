/**
 * PdfProcessingStatus — Wizard step-2 processing stages panel.
 *
 * Issue #4946: Show PDF indexing progress in wizard.
 * Renders four stage indicators (upload → extract → chunk → index) with a
 * progress bar and a "continue in background" hint.
 *
 * When `onContinue` is provided, shows a "Continue" button so the user can
 * proceed to the agent configuration step while indexing finishes in background.
 */

'use client';

import { CheckCircle2, Circle, Loader2, XCircle, Info } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { usePdfProcessingStatus } from '@/hooks/queries/usePdfProcessingStatus';
import { useTranslation } from '@/hooks/useTranslation';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

export interface PdfProcessingStatusProps {
  /** Private-game UUID — drives the polling query */
  gameId: string | null | undefined;
  /** Optional filename shown in the header */
  pdfFileName?: string | null;
  /** If provided, renders a "Continue" button that calls this when clicked */
  onContinue?: () => void;
  /** Extra CSS classes for the wrapper */
  className?: string;
}

type StageState = 'completed' | 'active' | 'pending' | 'failed';

interface Stage {
  key: string;
  labelKey: string;
}

const STAGES: Stage[] = [
  { key: 'uploading', labelKey: 'pdfIndexing.steps.uploading' },
  { key: 'extracting', labelKey: 'pdfIndexing.steps.extracting' },
  { key: 'chunking', labelKey: 'pdfIndexing.steps.chunking' },
  { key: 'indexing', labelKey: 'pdfIndexing.steps.indexing' },
];

// ---------------------------------------------------------------------------
// Helper — derive per-stage state from overall status
// ---------------------------------------------------------------------------

function getStageStates(
  status: 'pending' | 'processing' | 'indexed' | 'failed' | undefined
): StageState[] {
  switch (status) {
    case 'pending':
      return ['active', 'pending', 'pending', 'pending'];
    case 'processing':
      // Three stages completed, last active — approximation without granular step data
      return ['completed', 'completed', 'active', 'pending'];
    case 'indexed':
      return ['completed', 'completed', 'completed', 'completed'];
    case 'failed':
      return ['completed', 'completed', 'failed', 'failed'];
    default:
      return ['pending', 'pending', 'pending', 'pending'];
  }
}

// ---------------------------------------------------------------------------
// Stage row
// ---------------------------------------------------------------------------

function StageRow({ stage, state, label, stateLabel }: { stage: Stage; state: StageState; label: string; stateLabel: string }) {
  const icon = (() => {
    switch (state) {
      case 'completed':
        return <CheckCircle2 className="h-4 w-4 text-teal-500 shrink-0" aria-hidden="true" />;
      case 'active':
        return (
          <Loader2 className="h-4 w-4 animate-spin text-orange-500 shrink-0" aria-hidden="true" />
        );
      case 'failed':
        return <XCircle className="h-4 w-4 text-red-500 shrink-0" aria-hidden="true" />;
      default:
        return <Circle className="h-4 w-4 text-muted-foreground/40 shrink-0" aria-hidden="true" />;
    }
  })();

  const textClass =
    state === 'completed'
      ? 'text-foreground line-through opacity-60'
      : state === 'active'
        ? 'text-foreground font-medium'
        : state === 'failed'
          ? 'text-red-600 dark:text-red-400'
          : 'text-muted-foreground/50';

  return (
    <div className="flex items-center gap-3" key={stage.key}>
      {icon}
      <span className={`flex-1 text-sm ${textClass}`}>{label}</span>
      <span
        className={`text-xs tabular-nums ${
          state === 'active' ? 'text-orange-500' : 'text-muted-foreground/50'
        }`}
      >
        {stateLabel}
      </span>
    </div>
  );
}

// ---------------------------------------------------------------------------
// Progress bar
// ---------------------------------------------------------------------------

function ProgressBar({ value }: { value: number }) {
  return (
    <div
      className="h-2 bg-muted rounded-full overflow-hidden"
      role="progressbar"
      aria-valuenow={value}
      aria-valuemin={0}
      aria-valuemax={100}
    >
      <div
        className="h-full bg-orange-500 transition-all duration-500 rounded-full"
        style={{ width: `${value}%` }}
      />
    </div>
  );
}

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

export function PdfProcessingStatus({ gameId, pdfFileName, onContinue, className }: PdfProcessingStatusProps) {
  const { t } = useTranslation();
  const { data, isLoading } = usePdfProcessingStatus(gameId);

  const status = data?.status;
  const progress = data?.progress ?? (status === 'indexed' ? 100 : status === 'processing' ? 68 : 0);
  const stageStates = getStageStates(status);

  // Don't render if no gameId or no status available and not loading
  if (!gameId || (!isLoading && !data)) return null;

  const isTerminal = status === 'indexed' || status === 'failed';

  const stageLabelByState = (state: StageState) => {
    switch (state) {
      case 'completed': return t('pdfIndexing.stageCompleted');
      case 'active': return t('pdfIndexing.stageActive');
      case 'failed': return t('pdfIndexing.stageFailed');
      default: return t('pdfIndexing.stagePending');
    }
  };

  return (
    <div
      className={[
        'rounded-xl border border-orange-500/20 bg-orange-500/5 dark:bg-orange-900/10 px-5 py-4 space-y-4',
        className ?? '',
      ].join(' ')}
      data-testid="pdf-processing-status"
      aria-live="polite"
    >
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <p className="text-sm font-semibold text-foreground">
            {status === 'indexed'
              ? t('pdfIndexing.indexed')
              : status === 'failed'
                ? t('pdfIndexing.failed')
                : t('pdfIndexing.processing')}
          </p>
          {pdfFileName && (
            <p className="text-xs text-muted-foreground truncate max-w-xs mt-0.5">{pdfFileName}</p>
          )}
        </div>
        {!isTerminal && (
          <span className="text-xs text-muted-foreground tabular-nums">
            {t('pdfIndexing.progress', { progress })}
          </span>
        )}
        {status === 'indexed' && data?.chunkCount && (
          <span className="text-xs text-teal-600 dark:text-teal-400 tabular-nums">
            {t('pdfIndexing.chunksCount', { count: data.chunkCount })}
          </span>
        )}
      </div>

      {/* Progress bar (hidden once indexed/failed) */}
      {!isTerminal && <ProgressBar value={progress} />}

      {/* Stage rows */}
      <div className="space-y-2">
        {STAGES.map((stage, i) => (
          <StageRow
            key={stage.key}
            stage={stage}
            state={stageStates[i]}
            label={t(stage.labelKey as Parameters<typeof t>[0])}
            stateLabel={stageLabelByState(stageStates[i])}
          />
        ))}
      </div>

      {/* Background note + Continue button */}
      {!isTerminal && (
        <div className="flex items-start justify-between gap-4">
          <p className="flex items-start gap-2 text-xs text-muted-foreground leading-relaxed">
            <Info className="h-3.5 w-3.5 mt-0.5 shrink-0 text-orange-400" aria-hidden="true" />
            {t('pdfIndexing.backgroundNote')}
          </p>
          {onContinue && (
            <Button
              size="sm"
              variant="outline"
              onClick={onContinue}
              className="shrink-0 text-xs"
            >
              {t('pdfIndexing.continueToAgent')}
            </Button>
          )}
        </div>
      )}

      {/* On success with onContinue: show Continue button */}
      {status === 'indexed' && onContinue && (
        <Button
          size="sm"
          onClick={onContinue}
          className="w-full"
        >
          {t('pdfIndexing.continueToAgent')}
        </Button>
      )}
    </div>
  );
}
