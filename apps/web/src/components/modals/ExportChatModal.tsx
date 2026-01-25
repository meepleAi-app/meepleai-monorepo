/**
 * ExportChatModal Component (Migrated to RHF + Zod)
 *
 * Modal dialog for exporting chat conversations to various formats (PDF, TXT, Markdown).
 * Allows optional date range filtering for exported messages.
 *
 * Features:
 * - React Hook Form for state management (NO useState)
 * - Zod for schema validation
 * - Format selection (PDF, TXT, Markdown)
 * - Optional date range filtering
 * - Loading state with disabled controls
 * - Error display with localized Italian messages
 * - Accessible modal with keyboard navigation
 * - Automatic download trigger via Server Action
 *
 * Issue #1082: FE-IMP-006 — Form System (RHF + Zod)
 */

import { useEffect } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';

import { exportChatAction, type ExportChatActionState } from '@/actions/chat';
import { Form, FormControl, FormField, FormItem, FormLabel, FormError } from '@/components/forms';
import { LoadingButton } from '@/components/loading/LoadingButton';
import { Input } from '@/components/ui/primitives/input';
import { exportChatFormSchema, type ExportChatFormData } from '@/lib/schemas/forms';

import { AccessibleModal } from '../accessible/AccessibleModal';

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
export function ExportChatModal({ isOpen, onClose, chatId, gameName }: ExportChatModalProps) {
  // React Hook Form with Zod validation (NO useState)
  const form = useForm<ExportChatFormData>({
    resolver: zodResolver(exportChatFormSchema),
    defaultValues: {
      chatId,
      format: 'pdf',
      dateFrom: '',
      dateTo: '',
    },
  });

  // Submit handler (Server Action)
  const onSubmit = async (data: ExportChatFormData) => {
    try {
      const formData = new FormData();
      formData.append('chatId', data.chatId);
      formData.append('format', data.format);
      if (data.dateFrom) {
        formData.append('dateFrom', data.dateFrom);
      }
      if (data.dateTo) {
        formData.append('dateTo', data.dateTo);
      }

      const result = await exportChatAction({} as ExportChatActionState, formData);

      if (result.success) {
        onClose();
        form.reset();
      } else if (result.error) {
        form.setError('root', {
          message: result.error.message,
        });
      }
    } catch (error) {
      form.setError('root', {
        message: error instanceof Error ? error.message : "Errore durante l'esportazione",
      });
    }
  };

  // Close modal and reset on success (deprecated - already handled in onSubmit)
  useEffect(() => {
    if (!isOpen) {
      form.reset();
    }
  }, [isOpen, form]);

  const isSubmitting = form.formState.isSubmitting;

  const handleCancel = () => {
    if (!isSubmitting) {
      onClose();
    }
  };

  return (
    <AccessibleModal
      isOpen={isOpen}
      onClose={handleCancel}
      title="Esporta Chat"
      titleTestId="export-modal-title"
      description={`Esporta la conversazione per ${gameName}`}
      size="md"
      closeOnBackdropClick={!isSubmitting}
    >
      <Form form={form} onSubmit={onSubmit} className="space-y-6">
        {/* Hidden chatId field (managed by form state) */}
        <input type="hidden" {...form.register('chatId')} />

        {/* Format Selection */}
        <FormField
          control={form.control}
          name="format"
          render={({ field }) => (
            <FormItem>
              <FormLabel data-testid="export-format-label">Formato di esportazione</FormLabel>
              <FormControl>
                <div className="space-y-2" role="group" aria-label="Export format selection">
                  {/* PDF Option */}
                  <label className="flex items-center gap-3 p-3 border border-slate-200 dark:border-slate-700 rounded-lg cursor-pointer hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors">
                    <input
                      type="radio"
                      {...field}
                      value="pdf"
                      checked={field.value === 'pdf'}
                      disabled={isSubmitting}
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
                      {...field}
                      value="txt"
                      checked={field.value === 'txt'}
                      disabled={isSubmitting}
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
                      {...field}
                      value="md"
                      checked={field.value === 'md'}
                      disabled={isSubmitting}
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
              </FormControl>
              <FormError />
            </FormItem>
          )}
        />

        {/* Date Range Filter */}
        <div>
          <label className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-3">
            Filtro per data (opzionale)
          </label>
          <div className="grid grid-cols-2 gap-4">
            {/* Date From */}
            <FormField
              control={form.control}
              name="dateFrom"
              render={({ field }) => (
                <FormItem>
                  <FormLabel className="text-xs text-slate-600 dark:text-slate-400">Da</FormLabel>
                  <FormControl>
                    <Input {...field} type="date" disabled={isSubmitting} />
                  </FormControl>
                  <FormError />
                </FormItem>
              )}
            />

            {/* Date To */}
            <FormField
              control={form.control}
              name="dateTo"
              render={({ field }) => (
                <FormItem>
                  <FormLabel className="text-xs text-slate-600 dark:text-slate-400">A</FormLabel>
                  <FormControl>
                    <Input {...field} type="date" disabled={isSubmitting} />
                  </FormControl>
                  <FormError />
                </FormItem>
              )}
            />
          </div>
          <p className="mt-2 text-xs text-slate-500 dark:text-slate-400">
            Lascia vuoto per esportare tutti i messaggi
          </p>
        </div>

        {/* Root Error Display */}
        {form.formState.errors.root && (
          <div
            role="alert"
            className="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg"
          >
            <p className="text-sm text-red-800 dark:text-red-200">
              {form.formState.errors.root.message}
            </p>
          </div>
        )}

        {/* Action Buttons */}
        <div className="flex gap-3 justify-end">
          <LoadingButton
            type="button"
            onClick={handleCancel}
            isLoading={isSubmitting}
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
            isLoading={isSubmitting}
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
      </Form>
    </AccessibleModal>
  );
}
