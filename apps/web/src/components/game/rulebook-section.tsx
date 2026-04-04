'use client';

/**
 * RulebookSection - Displays the rulebook(s) attached to a game.
 *
 * States per rulebook:
 * - processing: spinner + "In elaborazione" text
 * - ready:      file name + optional "Chatta" button
 * - failed:     "Elaborazione fallita" + optional retry/remove icon buttons
 *
 * Section-level:
 * - No rulebooks     → upload button "Carica regolamento"
 * - Any rulebooks    → upload button "Carica altro"
 * - 1 rulebook       → title "Regolamento"
 * - multiple         → title "Regolamenti"
 * - any ready        → header "Pronto" badge visible
 */

import { useRef } from 'react';

import { Loader2, MessageSquare, RefreshCw, Trash2, Upload } from 'lucide-react';
import { toast } from 'sonner';

import { useRulebookUpload } from '@/lib/domain-hooks/use-rulebook-upload';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type RulebookKbStatus = 'processing' | 'ready' | 'failed';

export interface RulebookEntry {
  pdfDocumentId: string;
  fileName: string;
  kbStatus: RulebookKbStatus;
  indexedAt: string | null;
}

export interface RulebookSectionProps {
  gameId: string;
  rulebooks: RulebookEntry[];
  /** Called when user clicks the "Chatta" button on a ready rulebook */
  onChatClick?: (pdfDocumentId: string) => void;
  /** Called when user clicks the retry icon on a failed rulebook */
  onRetry?: (pdfDocumentId: string) => void;
  /** Called when user clicks the remove icon on a failed rulebook */
  onRemove?: (pdfDocumentId: string) => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export function RulebookSection({
  gameId,
  rulebooks,
  onChatClick,
  onRetry,
  onRemove,
  className,
}: RulebookSectionProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const { mutateAsync: uploadRulebook, isPending } = useRulebookUpload(gameId);

  const hasRulebooks = rulebooks.length > 0;
  const hasMultiple = rulebooks.length > 1;
  const hasReady = rulebooks.some(r => r.kbStatus === 'ready');

  const title = hasMultiple ? 'Regolamenti' : 'Regolamento';

  async function handleFileChange(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;

    try {
      await uploadRulebook(file);
      toast.success('Regolamento caricato con successo');
    } catch {
      toast.error('Errore durante il caricamento del regolamento');
    } finally {
      // Reset so the same file can be re-selected
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  }

  return (
    <div className={cn('flex flex-col gap-3', className)}>
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <h3 className="text-sm font-semibold">{title}</h3>
          {hasReady && (
            <span className="inline-flex items-center rounded-full bg-green-100 px-2 py-0.5 text-xs font-medium text-green-800 dark:bg-green-900/30 dark:text-green-300">
              Pronto
            </span>
          )}
        </div>
      </div>

      {/* Rulebook list */}
      {hasRulebooks && (
        <ul className="flex flex-col gap-2">
          {rulebooks.map(rulebook => (
            <li
              key={rulebook.pdfDocumentId}
              className="flex items-center justify-between rounded-lg border border-border/50 bg-muted/30 px-3 py-2"
            >
              <div className="flex flex-col gap-0.5 min-w-0">
                <span className="truncate text-sm font-medium">{rulebook.fileName}</span>
                {rulebook.kbStatus === 'processing' && (
                  <span className="flex items-center gap-1 text-xs text-muted-foreground">
                    <Loader2 className="h-3 w-3 animate-spin" aria-hidden="true" />
                    In elaborazione...
                  </span>
                )}
                {rulebook.kbStatus === 'ready' && (
                  <span className="text-xs text-green-600 dark:text-green-400">Pronto</span>
                )}
                {rulebook.kbStatus === 'failed' && (
                  <span className="text-xs text-red-600 dark:text-red-400">
                    Elaborazione fallita
                  </span>
                )}
              </div>

              <div className="flex items-center gap-1 shrink-0 ml-2">
                {/* Chat button — only for ready rulebooks when onChatClick provided */}
                {rulebook.kbStatus === 'ready' && onChatClick && (
                  <button
                    type="button"
                    aria-label="Chatta con questo regolamento"
                    onClick={() => onChatClick(rulebook.pdfDocumentId)}
                    className="inline-flex items-center gap-1 rounded-md bg-primary px-2 py-1 text-xs font-medium text-white hover:bg-primary/90 transition-colors"
                  >
                    <MessageSquare className="h-3 w-3" aria-hidden="true" />
                    Chatta
                  </button>
                )}

                {/* Retry button — only for failed rulebooks when onRetry provided */}
                {rulebook.kbStatus === 'failed' && onRetry && (
                  <button
                    type="button"
                    aria-label="Riprova elaborazione"
                    onClick={() => onRetry(rulebook.pdfDocumentId)}
                    className="rounded-md p-1 text-muted-foreground hover:text-foreground hover:bg-muted transition-colors"
                  >
                    <RefreshCw className="h-4 w-4" aria-hidden="true" />
                  </button>
                )}

                {/* Remove button — only for failed rulebooks when onRemove provided */}
                {rulebook.kbStatus === 'failed' && onRemove && (
                  <button
                    type="button"
                    aria-label="Rimuovi regolamento"
                    onClick={() => onRemove(rulebook.pdfDocumentId)}
                    className="rounded-md p-1 text-muted-foreground hover:text-destructive hover:bg-muted transition-colors"
                  >
                    <Trash2 className="h-4 w-4" aria-hidden="true" />
                  </button>
                )}
              </div>
            </li>
          ))}
        </ul>
      )}

      {/* Upload button */}
      <div>
        <input
          ref={fileInputRef}
          type="file"
          accept=".pdf"
          className="sr-only"
          onChange={handleFileChange}
          aria-hidden="true"
          tabIndex={-1}
        />
        <button
          type="button"
          disabled={isPending}
          onClick={() => fileInputRef.current?.click()}
          className="inline-flex items-center gap-1.5 rounded-md border border-dashed border-border px-3 py-1.5 text-xs text-muted-foreground hover:text-foreground hover:border-border/80 hover:bg-muted/30 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
        >
          <Upload className="h-3 w-3" aria-hidden="true" />
          {hasRulebooks ? 'Carica altro' : 'Carica regolamento'}
        </button>
      </div>
    </div>
  );
}
