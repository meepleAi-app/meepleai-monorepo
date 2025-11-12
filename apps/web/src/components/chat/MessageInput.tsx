/**
 * MessageInput - Message input form with send button
 *
 * Handles user message input and submission.
 * Integrates with ChatProvider for state and submission.
 * AI-14: Includes SearchModeToggle for hybrid search feature.
 * Migrated to shadcn/ui components.
 */

import React, { FormEvent } from 'react';
import { useChatContext } from './ChatProvider';
import { LoadingButton } from '../loading/LoadingButton';
import { SearchModeToggle, SearchMode } from '@/components';
import { Input } from '@/components/ui/input';

export function MessageInput() {
  const {
    inputValue,
    setInputValue,
    sendMessage,
    selectedGameId,
    selectedAgentId,
    loading,
    searchMode,
    setSearchMode
  } = useChatContext();

  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (!inputValue.trim() || !selectedGameId || !selectedAgentId) {
      return;
    }
    void sendMessage(inputValue);
  };

  const isDisabled = loading.sending || !selectedGameId || !selectedAgentId;
  const isSendDisabled = !inputValue.trim() || isDisabled;

  return (
    <div
      style={{
        padding: 16,
        borderTop: '1px solid #dadce0',
        background: 'white',
        display: 'flex',
        flexDirection: 'column',
        gap: 12
      }}
    >
      {/* AI-14: Search Mode Toggle */}
      <SearchModeToggle
        value={searchMode as SearchMode}
        onChange={(mode) => setSearchMode(mode)}
        disabled={isDisabled}
      />

      {/* Message Input Form */}
      <form
        onSubmit={handleSubmit}
        style={{
          display: 'flex',
          gap: 8
        }}
      >
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
          isLoading={loading.sending}
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
