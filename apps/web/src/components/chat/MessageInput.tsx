/**
 * MessageInput - Message input form with send button
 *
 * Migrated to Zustand (Issue #1083):
 * - Uses store for input value, search mode, loading
 * - Optimistic updates via useChatOptimistic hook
 * - Granular subscriptions (only needed slices)
 */

import React, { FormEvent, useCallback } from 'react';
import { useChatStoreWithSelectors } from '@/store/chat';
import { useChatOptimistic } from '@/hooks/useChatOptimistic';
import { LoadingButton } from '../loading/LoadingButton';
import { SearchModeToggle, SearchMode } from '@/components';
import { Input } from '@/components/ui/input';
import { toast } from 'sonner';
import { useMessageInputShortcuts, modKey } from '@/hooks/useKeyboardShortcuts';

export function MessageInput() {
  const inputValue = useChatStoreWithSelectors.use.inputValue();
  const setInputValue = useChatStoreWithSelectors.use.setInputValue();
  const selectedGameId = useChatStoreWithSelectors.use.selectedGameId();
  const selectedAgentId = useChatStoreWithSelectors.use.selectedAgentId();
  const loading = useChatStoreWithSelectors.use.loading();
  const searchMode = useChatStoreWithSelectors.use.searchMode();
  const setSearchMode = useChatStoreWithSelectors.use.setSearchMode();

  // #1167: Use optimistic updates hook (works with both Context and Zustand)
  const { sendMessageOptimistic, isOptimisticUpdate } = useChatOptimistic();

  const handleSubmit = useCallback(async (e?: FormEvent) => {
    if (e) {
      e.preventDefault();
    }
    if (!inputValue.trim() || !selectedGameId || !selectedAgentId) {
      return;
    }

    // #1167: Use optimistic update
    const messageContent = inputValue;
    setInputValue(''); // Clear input immediately for better UX

    try {
      await sendMessageOptimistic(messageContent);
    } catch (err) {
      // Restore input on error
      setInputValue(messageContent);

      // Show error toast
      const errorMessage = err instanceof Error ? err.message : 'Errore di comunicazione';
      toast.error(`Errore nell'invio del messaggio: ${errorMessage}`);
    }
  }, [inputValue, selectedGameId, selectedAgentId, sendMessageOptimistic, setInputValue]);

  const isDisabled = loading.sending || isOptimisticUpdate || !selectedGameId || !selectedAgentId;

  // Issue #1100: Cmd+Enter to send message
  useMessageInputShortcuts(() => handleSubmit(), !isDisabled && !!inputValue.trim());
  const isSendDisabled = !inputValue.trim() || isDisabled;

  return (
    <div className="p-4 border-t border-[#dadce0] bg-white flex flex-col gap-3">
      {/* AI-14: Search Mode Toggle */}
      <SearchModeToggle
        value={searchMode as SearchMode}
        onChange={(mode) => setSearchMode(mode)}
      />

      <form onSubmit={handleSubmit} className="flex gap-2 items-end">
        <div className="flex-1">
          <label htmlFor="messageInput" className="sr-only">
            Scrivi un messaggio
          </label>
          <Input
            id="messageInput"
            type="text"
            value={inputValue}
            onChange={(e) => setInputValue(e.target.value)}
            placeholder="Scrivi un messaggio..."
            disabled={isDisabled}
            aria-busy={isDisabled}
            aria-invalid={!selectedGameId || !selectedAgentId}
            aria-describedby={
              !selectedGameId || !selectedAgentId
                ? 'input-helper-text'
                : undefined
            }
            className="w-full"
            autoComplete="off"
          />
          {(!selectedGameId || !selectedAgentId) && (
            <span id="input-helper-text" className="text-xs text-[#d93025] mt-1 block">
              Seleziona un gioco e un agente per iniziare
            </span>
          )}
        </div>

        <LoadingButton
          type="submit"
          isLoading={loading.sending || isOptimisticUpdate}
          loadingText="Invio..."
          disabled={isSendDisabled}
          aria-label={`Send message (${modKey}+Enter)`}
          className="px-6 py-2.5 bg-[#1a73e8] text-white border-none rounded text-sm font-medium cursor-pointer hover:bg-[#1557b0] disabled:bg-[#dadce0] disabled:cursor-not-allowed transition-colors"
        >
          Invia
        </LoadingButton>
      </form>
    </div>
  );
}
