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
 * - Error display with user-friendly messages
 * - Accessible modal with keyboard navigation
 * - Automatic download trigger
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

import { useState } from 'react';
import { AccessibleModal } from './accessible/AccessibleModal';
import { api, ExportFormat } from '@/lib/api';

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
  // Form state
  const [format, setFormat] = useState<ExportFormat>('pdf');
  const [dateFrom, setDateFrom] = useState<string>('');
  const [dateTo, setDateTo] = useState<string>('');

  // UI state
  const [isExporting, setIsExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleExport = async () => {
    setError(null);

    // Validate date range
    if (dateFrom && dateTo && new Date(dateFrom) > new Date(dateTo)) {
      setError('La data iniziale deve essere precedente alla data finale.');
      return;
    }

    setIsExporting(true);

    try {
      await api.chat.exportChat(chatId, {
        format,
        dateFrom: dateFrom || undefined,
        dateTo: dateTo || undefined,
      });

      // Close modal on success
      onClose();

      // Reset form state
      setFormat('pdf');
      setDateFrom('');
      setDateTo('');
    } catch (err) {
      console.error('Error exporting chat:', err);

      // Display user-friendly error message
      if (err instanceof Error) {
        setError(err.message || 'Errore durante l\'esportazione della chat.');
      } else {
        setError('Errore durante l\'esportazione della chat.');
      }
    } finally {
      setIsExporting(false);
    }
  };

  const handleCancel = () => {
    if (!isExporting) {
      setError(null);
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
      closeOnBackdropClick={!isExporting}
    >
      <div className="space-y-6">
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
                disabled={isExporting}
                className="w-4 h-4 text-primary-600 focus:ring-primary-500"
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
                disabled={isExporting}
                className="w-4 h-4 text-primary-600 focus:ring-primary-500"
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
                disabled={isExporting}
                className="w-4 h-4 text-primary-600 focus:ring-primary-500"
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
                value={dateFrom}
                onChange={(e) => setDateFrom(e.target.value)}
                disabled={isExporting}
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
                  focus:ring-primary-500
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
                value={dateTo}
                onChange={(e) => setDateTo(e.target.value)}
                disabled={isExporting}
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
                  focus:ring-primary-500
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
        {error && (
          <div
            role="alert"
            className="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg"
          >
            <p className="text-sm text-red-800 dark:text-red-200">{error}</p>
          </div>
        )}

        {/* Action Buttons */}
        <div className="flex gap-3 justify-end">
          <button
            onClick={handleCancel}
            disabled={isExporting}
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
              rounded-lg
              hover:bg-slate-50
              dark:hover:bg-slate-700
              focus:outline-none
              focus:ring-2
              focus:ring-primary-500
              disabled:opacity-50
              disabled:cursor-not-allowed
              transition-colors
            "
          >
            Annulla
          </button>
          <button
            onClick={() => void handleExport()}
            disabled={isExporting}
            className="
              px-4
              py-2
              text-sm
              font-medium
              text-white
              bg-primary-600
              hover:bg-primary-700
              rounded-lg
              focus:outline-none
              focus:ring-2
              focus:ring-primary-500
              disabled:opacity-50
              disabled:cursor-not-allowed
              transition-colors
              inline-flex
              items-center
              gap-2
            "
          >
            {isExporting && (
              <svg
                className="animate-spin h-4 w-4"
                xmlns="http://www.w3.org/2000/svg"
                fill="none"
                viewBox="0 0 24 24"
                aria-hidden="true"
              >
                <circle
                  className="opacity-25"
                  cx="12"
                  cy="12"
                  r="10"
                  stroke="currentColor"
                  strokeWidth="4"
                />
                <path
                  className="opacity-75"
                  fill="currentColor"
                  d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"
                />
              </svg>
            )}
            {isExporting ? 'Esportazione...' : 'Esporta'}
          </button>
        </div>
      </div>
    </AccessibleModal>
  );
}
