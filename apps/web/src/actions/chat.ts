/**
 * Chat Export Actions
 * Client-side action functions for chat export operations using React 19 useActionState pattern
 *
 * Issue #1078: FE-IMP-002 — Server Actions per Auth & Export
 *
 * These actions wrap chat export API calls and provide:
 * - Typed error handling
 * - Localized error messages (Italian)
 * - Progress states for UI
 * - File download handling
 */

'use client';

import { api, ApiError, type ExportFormat } from '@/lib/api';
import { getLocalizedError, type LocalizedError, successMessages } from '@/lib/i18n/errors';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';

// ============================================================================
// Types
// ============================================================================

/**
 * Export chat action state
 */
export interface ExportChatActionState {
  success: boolean;
  error?: LocalizedError;
  message?: string;
}

// ============================================================================
// Export Chat Action
// ============================================================================

/**
 * Export chat action for use with useActionState
 *
 * @example
 * const [state, exportAction, isPending] = useActionState(exportChatAction, { success: false });
 *
 * <form action={exportAction}>
 *   <input type="hidden" name="chatId" value={chatId} />
 *   <select name="format">
 *     <option value="pdf">PDF</option>
 *     <option value="txt">TXT</option>
 *     <option value="md">Markdown</option>
 *   </select>
 *   <input type="date" name="dateFrom" />
 *   <input type="date" name="dateTo" />
 *   <button disabled={isPending}>Esporta</button>
 * </form>
 */
export async function exportChatAction(
  prevState: ExportChatActionState,
  formData: FormData
): Promise<ExportChatActionState> {
  try {
    const chatId = formData.get('chatId') as string;
    const format = formData.get('format') as ExportFormat;
    const dateFrom = formData.get('dateFrom') as string | null;
    const dateTo = formData.get('dateTo') as string | null;

    // Validation
    if (!chatId) {
      return {
        success: false,
        error: {
          type: 'validation',
          message: 'ID della chat mancante.'
        }
      };
    }

    if (!format || !['pdf', 'txt', 'md'].includes(format)) {
      return {
        success: false,
        error: {
          type: 'validation',
          message: 'Formato di esportazione non valido.'
        }
      };
    }

    // Validate date range if both dates provided
    if (dateFrom && dateTo) {
      const fromDate = new Date(dateFrom);
      const toDate = new Date(dateTo);

      if (fromDate > toDate) {
        return {
          success: false,
          error: {
            type: 'validation',
            message: 'La data iniziale deve essere precedente alla data finale.'
          }
        };
      }
    }

    // Call API (this will trigger download)
    await (api.chat as any).exportChat(chatId, {
      format,
      dateFrom: dateFrom || undefined,
      dateTo: dateTo || undefined
    });

    return {
      success: true,
      message: successMessages.exportSuccess
    };
  } catch (error) {
    logger.error(
      'Export chat action failed',
      error instanceof Error ? error : new Error(String(error)),
      createErrorContext('ChatActions', 'exportChatAction', { chatId: formData.get('chatId') as string, format: formData.get('format') as string, operation: 'export_chat' })
    );

    if (error instanceof ApiError) {
      return {
        success: false,
        error: getLocalizedError(error.statusCode, error.message)
      };
    }

    return {
      success: false,
      error: {
        type: 'network',
        message: 'Impossibile esportare la chat. Verifica la tua connessione.'
      }
    };
  }
}
