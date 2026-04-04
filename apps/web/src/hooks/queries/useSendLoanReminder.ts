/**
 * useSendLoanReminder - TanStack Query mutation hook for loan reminders
 *
 * Library Improvements: Loan flow UI
 *
 * Sends a reminder notification to a borrower who has not yet returned a game.
 * Handles 409 conflict (already reminded in last 24h) with a specific toast message.
 */

import { useMutation } from '@tanstack/react-query';

import { toast } from '@/components/layout/Toast';
import { api } from '@/lib/api';

/**
 * Hook to send a loan reminder for a game
 *
 * @param gameId - Game UUID
 * @returns UseMutationResult — call `mutate(customMessage?)` to trigger
 */
export function useSendLoanReminder(gameId: string) {
  return useMutation({
    mutationFn: (customMessage?: string) => api.library.sendLoanReminder(gameId, customMessage),
    onSuccess: () => {
      toast.success('Promemoria inviato correttamente.');
    },
    onError: (error: Error) => {
      if (error.message.includes('409')) {
        toast.error('Promemoria già inviato nelle ultime 24 ore.');
      } else {
        toast.error('Impossibile inviare il promemoria.');
      }
    },
  });
}
