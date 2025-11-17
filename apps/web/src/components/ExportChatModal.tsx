/**
 * ExportChatModal Component (CHAT-05)
 *
 * Modal dialog for exporting chat conversations to various formats (PDF, TXT, Markdown).
 * Allows optional date range filtering for exported messages.
 *
 * Features:
 * - Format selection (PDF, TXT, Markdown)
 * - Optional date range filtering
 * - Loading state with disabled controls
 * - Error display with localized Italian messages
 * - Accessible modal with keyboard navigation
 * - Automatic download trigger via Server Action
 *
 * Issue #1078: FE-IMP-002 — Server Actions per Auth & Export
 *
 * @example
 * ```tsx
 * const [showExport, setShowExport] = useState(false);
 *
 * <ExportChatModal
 *   isOpen={showExport}
 *   onClose={() => setShowExport(false)}
 *   chatId="chat-uuid-123"
 *   gameName="Chess"
 * />
 * ```
 */

import { useState, useActionState, useEffect } from 'react';
import { AccessibleModal } from './accessible/AccessibleModal';
import { ExportFormat } from '@/lib/api';
import { LoadingButton } from '@/components/loading/LoadingButton';
import { exportChatAction, type ExportChatActionState } from '@/actions/chat';

export interface ExportChatModalProps {
  /**
   * Whether the modal is open
   */
  isOpen: boolean;

  /**
   * Callback when modal should close
   */
  onClose: () => void;

  /**
   * ID of the chat to export
   */
  chatId: string;

  /**
   * Name of the game (for display purposes)
   */
  gameName: string;
}

/**
 * ExportChatModal component for exporting chat conversations
 */
export function ExportChatModal({
  isOpen,
  onClose,
  chatId,
  gameName,
}: ExportChatModalProps) {
  // Use React 19 useActionState for form handling
  const [state, formAction, isPending] = useActionState<ExportChatActionState, FormData>(
    exportChatAction,
    { success: false }
  );

  // Local state for format selection (controlled input)
  const [format, setFormat] = useState<ExportFormat>('pdf');

  // Close modal and reset on success
  useEffect(() => {
    if (state.success) {
      onClose();
      // Reset format after modal closes
      setTimeout(() => setFormat('pdf'), 300);
    }
  }, [state.success, onClose]);

  const handleCancel = () => {
    if (!isPending) {
      onClose();
    }
  };

  return (
    <AccessibleModal
      isOpen={isOpen}
      onClose={handleCancel}
      title="Esporta Chat"
      description={`Esporta la conversazione per ${gameName}`}
      size="md"
      closeOnBackdropClick={!isPending}
    >
      <form action={formAction} className="space-y-6">
        {/* Hidden chatId field */}
        <input type="hidden" name="chatId" value={chatId} />

        {/* Format Selection */}
        <div>
          <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-3">
            Formato di esportazione
          </label>
          <div className="space-y-2" role="group" aria-label="Export format selection">
            {/* PDF Option */}
            <label className="flex items-center gap-3 p-3 border border-slate-200 dark:border-slate-700 rounded-lg cursor-pointer hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors">
              <input
                type="radio"
                name="format"
                value="pdf"
                checked={format === 'pdf'}
                onChange={(e) => setFormat(e.target.value as ExportFormat)}
                disabled={isPending}
                className="w-4 h-4 text-primary focus:ring-ring"
              />
              <div className="flex-1">
                <div className="font-medium text-slate-900 dark:text-white">PDF</div>
                <div className="text-xs text-slate-500 dark:text-slate-400">
                  Documento formattato per la stampa
                </div>
              </div>
            </label>

            {/* TXT Option */}
            <label className="flex items-center gap-3 p-3 border border-slate-200 dark:border-slate-700 rounded-lg cursor-pointer hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors">
              <input
                type="radio"
                name="format"
                value="txt"
                checked={format === 'txt'}
                onChange={(e) => setFormat(e.target.value as ExportFormat)}
                disabled={isPending}
                className="w-4 h-4 text-primary focus:ring-ring"
              />
              <div className="flex-1">
                <div className="font-medium text-slate-900 dark:text-white">Testo (TXT)</div>
                <div className="text-xs text-slate-500 dark:text-slate-400">
                  Testo semplice senza formattazione
                </div>
              </div>
            </label>

            {/* Markdown Option */}
            <label className="flex items-center gap-3 p-3 border border-slate-200 dark:border-slate-700 rounded-lg cursor-pointer hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors">
              <input
                type="radio"
                name="format"
                value="md"
                checked={format === 'md'}
                onChange={(e) => setFormat(e.target.value as ExportFormat)}
                disabled={isPending}
                className="w-4 h-4 text-primary focus:ring-ring"
              />
              <div className="flex-1">
                <div className="font-medium text-slate-900 dark:text-white">Markdown</div>
                <div className="text-xs text-slate-500 dark:text-slate-400">
                  Formato Markdown per documentazione
                </div>
              </div>
            </label>
          </div>
        </div>

        {/* Date Range Filter */}
        <div>
          <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-3">
            Filtro per data (opzionale)
          </label>
          <div className="grid grid-cols-2 gap-4">
            <div>
              <label
                htmlFor="dateFrom"
                className="block text-xs text-slate-600 dark:text-slate-400 mb-1"
              >
                Da
              </label>
              <input
                type="date"
                id="dateFrom"
                name="dateFrom"
                disabled={isPending}
                className="
                  w-full
                  px-3
                  py-2
                  border
                  border-slate-300
                  dark:border-slate-600
                  rounded-md
                  text-sm
                  focus:outline-none
                  focus:ring-2
                  focus:ring-ring
                  disabled:bg-slate-100
                  disabled:cursor-not-allowed
                  dark:bg-slate-800
                  dark:text-white
                "
              />
            </div>
            <div>
              <label
                htmlFor="dateTo"
                className="block text-xs text-slate-600 dark:text-slate-400 mb-1"
              >
                A
              </label>
              <input
                type="date"
                id="dateTo"
                name="dateTo"
                disabled={isPending}
                className="
                  w-full
                  px-3
                  py-2
                  border
                  border-slate-300
                  dark:border-slate-600
                  rounded-md
                  text-sm
                  focus:outline-none
                  focus:ring-2
                  focus:ring-ring
                  disabled:bg-slate-100
                  disabled:cursor-not-allowed
                  dark:bg-slate-800
                  dark:text-white
                "
              />
            </div>
          </div>
          <p className="mt-2 text-xs text-slate-500 dark:text-slate-400">
            Lascia vuoto per esportare tutti i messaggi
          </p>
        </div>

        {/* Error Display */}
        {state.error && (
          <div
            role="alert"
            className="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg"
          >
            <p className="text-sm text-red-800 dark:text-red-200">{state.error.message}</p>
          </div>
        )}

        {/* Action Buttons */}
        <div className="flex gap-3 justify-end">
          <LoadingButton
            type="button"
            onClick={handleCancel}
            isLoading={isPending}
            variant="outline"
            className="
              px-4
              py-2
              text-sm
              font-medium
              text-slate-700
              dark:text-slate-300
              bg-white
              dark:bg-slate-800
              border
              border-slate-300
              dark:border-slate-600
              hover:bg-slate-50
              dark:hover:bg-slate-700
            "
          >
            Annulla
          </LoadingButton>
          <LoadingButton
            type="submit"
            isLoading={isPending}
            loadingText="Esportazione..."
            className="
              px-4
              py-2
              text-sm
              font-medium
            "
          >
            Esporta
          </LoadingButton>
        </div>
      </form>
    </AccessibleModal>
  );
}