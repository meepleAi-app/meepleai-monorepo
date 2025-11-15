/**
 * MessageInput - Message input form with send button
 *
 * Handles user message input and submission.
 * Integrates with ChatProvider for state and submission.
 * AI-14: Includes SearchModeToggle for hybrid search feature.
 * Migrated to shadcn/ui components.
 */

import React, { FormEvent, useState } from 'react';
import { useChatContext } from './ChatProvider';
import { useChatOptimistic } from '@/hooks/useChatOptimistic';
import { LoadingButton } from '../loading/LoadingButton';
import { SearchModeToggle, SearchMode } from '@/components';
import { Input } from '@/components/ui/input';
import { toast } from 'sonner';

export function MessageInput() {
  const {
    inputValue,
    setInputValue,
    selectedGameId,
    selectedAgentId,
    loading,
    searchMode,
    setSearchMode
  } = useChatContext();

  // #1167: Use optimistic updates hook
  const { sendMessageOptimistic, isOptimisticUpdate } = useChatOptimistic();
  const [isSending, setIsSending] = useState(false);

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!inputValue.trim() || !selectedGameId || !selectedAgentId) {
      return;
    }

    // #1167: Use optimistic update
    setIsSending(true);
    const messageContent = inputValue;
    setInputValue(''); // Clear input immediately for better UX

    try {
      await sendMessageOptimistic(messageContent);
    } catch (err) {
      // Restore input on error
      setInputValue(messageContent);

      // Show error toast (categorized error handling)
      const errorMessage = err instanceof Error ? err.message : 'Errore di comunicazione';
      toast.error(`Errore nell'invio del messaggio: ${errorMessage}`);
    } finally {
      setIsSending(false);
    }
  };

  const isDisabled = loading.sending || isSending || isOptimisticUpdate || !selectedGameId || !selectedAgentId;
  const isSendDisabled = !inputValue.trim() || isDisabled;

  return (
    <div className="p-4 border-t border-[#dadce0] bg-white flex flex-col gap-3">
      {/* AI-14: Search Mode Toggle */}
      <SearchModeToggle
        value={searchMode as SearchMode}
        onChange={(mode) => setSearchMode(mode)}
        disabled={isDisabled}
      />

      {/* Message Input Form */}
      <form onSubmit={handleSubmit} className="flex gap-2">
        <label htmlFor="message-input" className="sr-only">
          Ask a question about the game
        </label>
        <Input
          id="message-input"
          type="text"
          value={inputValue}
          onChange={(e) => setInputValue(e.target.value)}
          placeholder="Fai una domanda sul gioco..."
          disabled={isDisabled}
          aria-label="Message input"
          className="flex-1"
        />
        <LoadingButton
          type="submit"
          isLoading={loading.sending || isSending}
          loadingText="Invio..."
          disabled={isSendDisabled}
          aria-label="Send message"
        >
          Invia
        </LoadingButton>
      </form>
    </div>
  );
}
