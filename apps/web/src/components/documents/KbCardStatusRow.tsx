/**
 * KbCardStatusRow
 *
 * Displays a single PDF document row with:
 *   - File name (truncated)
 *   - Status badge (processing / indexed / failed / none)
 *   - Progress bar (visible when processing)
 *   - "Riprova" retry button (visible when failed and retries remain)
 *
 * Used in GameDetailDrawer's "Documenti KB" section.
 * Issue #5192: New KbCardStatusRow component.
 *
 * @example
 * ```tsx
 * <KbCardStatusRow
 *   document={pdfDto}
 *   onRetry={handleRetry}
 *   isRetrying={isRetryingId === pdfDto.id}
 * />
 * ```
 */

'use client';

import { memo } from 'react';
import { RefreshCw, FileText } from 'lucide-react';

import { cn } from '@/lib/utils';
import { DocumentStatusBadge } from '@/components/ui/data-display/meeple-card-features/DocumentStatusBadge';
import type { DocumentIndexingStatus } from '@/components/ui/data-display/meeple-card-features/DocumentStatusBadge';
import type { PdfDocumentDto } from '@/lib/api/schemas/pdf.schemas';

// ============================================================================
// Types
// ============================================================================

export interface KbCardStatusRowProps {
  /** PDF document DTO (with processingState, progressPercentage, retryCount, maxRetries) */
  document: PdfDocumentDto;
  /** Called when user clicks the retry button */
  onRetry?: (documentId: string) => void;
  /** Whether this row is currently being retried (shows spinner on button) */
  isRetrying?: boolean;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Helpers
// ============================================================================

/**
 * Map PdfDocumentDto.processingState (backend PdfProcessingState enum) →
 * DocumentIndexingStatus (frontend 4-state union used by DocumentStatusBadge).
 */
function mapProcessingState(state: string): DocumentIndexingStatus {
  switch (state) {
    case 'Ready':
      return 'indexed';
    case 'Failed':
      return 'failed';
    case 'Extracting':
    case 'Chunking':
    case 'Embedding':
    case 'Indexing':
    case 'Uploading':
      return 'processing';
    default:
      return 'none';
  }
}

// ============================================================================
// Component
// ============================================================================

export const KbCardStatusRow = memo(function KbCardStatusRow({
  document,
  onRetry,
  isRetrying = false,
  className,
}: KbCardStatusRowProps) {
  const status = mapProcessingState(document.processingState);
  const isFailed = status === 'failed';
  const isProcessing = status === 'processing';
  const canRetry = isFailed && document.retryCount < document.maxRetries;

  return (
    <div
      className={cn(
        'flex flex-col gap-1.5 rounded-lg border border-border/60 bg-card/50 px-3 py-2.5',
        className,
      )}
      data-testid={`kb-card-status-row-${document.id}`}
    >
      {/* Top row: icon + filename + badge */}
      <div className="flex items-center gap-2 min-w-0">
        <FileText
          aria-hidden="true"
          className="w-3.5 h-3.5 shrink-0 text-muted-foreground/70"
        />

        <span
          className="flex-1 truncate text-xs font-medium text-foreground/90"
          title={document.fileName}
          data-testid="kb-card-status-row-filename"
        >
          {document.fileName}
        </span>

        <DocumentStatusBadge
          status={status}
          size="sm"
        />

        {/* Retry button */}
        {canRetry && (
          <button
            type="button"
            onClick={() => onRetry?.(document.id)}
            disabled={isRetrying}
            aria-label={`Riprova elaborazione di ${document.fileName}`}
            className={cn(
              'ml-1 shrink-0 inline-flex items-center gap-1 rounded px-1.5 py-0.5',
              'text-[10px] font-semibold text-destructive border border-destructive/40',
              'hover:bg-destructive/10 transition-colors',
              'disabled:opacity-50 disabled:cursor-not-allowed',
            )}
            data-testid="kb-card-status-row-retry"
          >
            <RefreshCw
              aria-hidden="true"
              className={cn('w-2.5 h-2.5', isRetrying && 'animate-spin')}
            />
            Riprova
          </button>
        )}
      </div>

      {/* Progress bar: only when processing */}
      {isProcessing && (
        <div
          role="progressbar"
          aria-valuenow={document.progressPercentage}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-label={`Progresso: ${document.progressPercentage}%`}
          className="h-1 w-full rounded-full bg-muted overflow-hidden"
          data-testid="kb-card-status-row-progress"
        >
          <div
            className="h-full rounded-full bg-blue-500 transition-all duration-500"
            style={{ width: `${document.progressPercentage}%` }}
          />
        </div>
      )}

      {/* Retry counter: show when retries > 0 */}
      {document.retryCount > 0 && (
        <span
          className="text-[10px] text-muted-foreground"
          data-testid="kb-card-status-row-retry-count"
        >
          Tentativo {document.retryCount}/{document.maxRetries}
        </span>
      )}
    </div>
  );
});
