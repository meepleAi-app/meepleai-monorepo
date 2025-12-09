/**
 * MessageInput - Message input form with send button
 *
 * Handles user message input and submission.
 * Integrates with ChatProvider for state and submission.
 * AI-14: Includes SearchModeToggle for hybrid search feature.
 * Migrated to shadcn/ui components.
 */

import React, { FormEvent } from 'react';
import { useChatWithStreaming } from '@/hooks/useChatWithStreaming';
import { LoadingButton } from '../loading/LoadingButton';
import { SearchModeToggle, SearchMode } from '@/components';
import { Input } from '@/components/ui/input';

export function MessageInput() {
  // Issue #1676: Uses streaming-enabled hook (combines Zustand + SSE)
  const {
    inputValue,
    setInputValue,
    sendMessage,
    selectedGameId,
    selectedAgentId,
    loading,
    searchMode,
    setSearchMode,
    // Streaming state (Issue #1007)
    isStreaming,
    stopStreaming,
  } = useChatWithStreaming();

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (!inputValue.trim() || !selectedGameId || !selectedAgentId) {
      return;
    }
    void sendMessage(inputValue);
  };

  const handleStop = () => {
    stopStreaming();
  };

  const isDisabled = loading.sending || isStreaming || !selectedGameId || !selectedAgentId;
  const isSendDisabled = !inputValue.trim() || isDisabled;

  return (
    <div className="p-4 border-t border-[#dadce0] bg-white flex flex-col gap-3">
      {/* AI-14: Search Mode Toggle */}
      <SearchModeToggle
        value={searchMode as SearchMode}
        onChange={mode => setSearchMode(mode)}
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
          onChange={e => setInputValue(e.target.value)}
          placeholder="Fai una domanda sul gioco..."
          disabled={isDisabled}
          aria-label="Message input"
          className="flex-1"
        />
        {isStreaming ? (
          <LoadingButton
            type="button"
            onClick={handleStop}
            isLoading={false}
            disabled={false}
            aria-label="Stop streaming"
            className="bg-red-600 hover:bg-red-700"
          >
            ⏹ Stop
          </LoadingButton>
        ) : (
          <LoadingButton
            type="submit"
            isLoading={loading.sending}
            loadingText="Invio..."
            disabled={isSendDisabled}
            aria-label="Send message"
          >
            Invia
          </LoadingButton>
        )}
      </form>
    </div>
  );
}
