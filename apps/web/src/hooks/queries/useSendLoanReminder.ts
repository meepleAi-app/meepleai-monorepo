/**
 * useSendLoanReminder - TanStack Query mutation hook for sending loan reminders
 *
 * Sends a loan reminder to the borrower for a given game.
 * Handles rate-limit (409) errors with a specific message.
 */

import { useMutation } from '@tanstack/react-query';

import { toast } from '@/components/layout/Toast';
import { api } from '@/lib/api';

/**
 * Hook to send a loan reminder for a game
 *
 * Shows a success toast on success, or an error toast on failure.
 * Handles 409 (rate limit: already sent in last 24h) specifically.
 *
 * @param gameId - Game UUID
 * @returns UseMutationResult for sending loan reminder
 */
export function useSendLoanReminder(gameId: string) {
  return useMutation({
    mutationFn: (customMessage?: string) => api.library.sendLoanReminder(gameId, customMessage),
    onSuccess: () => {
      toast.success('Promemoria inviato correttamente.');
    },
    onError: (error: Error) => {
      if (error.message.includes('409')) {
        toast.error('Hai già inviato un promemoria nelle ultime 24 ore.');
      } else {
        toast.error("Errore durante l'invio del promemoria.");
      }
    },
  });
}
