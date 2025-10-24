/**
 * MessageInput - Message input form with send button
 *
 * Handles user message input and submission.
 * Integrates with ChatProvider for state and submission.
 */

import React, { FormEvent } from 'react';
import { useChatContext } from './ChatProvider';
import { LoadingButton } from '../loading/LoadingButton';

export function MessageInput() {
  const {
    inputValue,
    setInputValue,
    sendMessage,
    selectedGameId,
    selectedAgentId,
    loading
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
    <form
      onSubmit={handleSubmit}
      style={{
        padding: 16,
        borderTop: '1px solid #dadce0',
        background: 'white',
        display: 'flex',
        gap: 8
      }}
    >
      <label htmlFor="message-input" className="sr-only">
        Ask a question about the game
      </label>
      <input
        id="message-input"
        type="text"
        value={inputValue}
        onChange={(e) => setInputValue(e.target.value)}
        placeholder="Fai una domanda sul gioco..."
        disabled={isDisabled}
        aria-label="Message input"
        style={{
          flex: 1,
          padding: 12,
          fontSize: 14,
          border: '1px solid #dadce0',
          borderRadius: 4
        }}
      />
      <LoadingButton
        type="submit"
        isLoading={loading.sending}
        loadingText="Invio..."
        disabled={isSendDisabled}
        spinnerSize="sm"
        aria-label="Send message"
        style={{
          padding: '12px 24px',
          background: isSendDisabled ? '#dadce0' : '#0070f3',
          color: 'white',
          border: 'none',
          borderRadius: 4,
          fontSize: 14,
          fontWeight: 500,
          cursor: isSendDisabled ? 'not-allowed' : 'pointer'
        }}
      >
        Invia
      </LoadingButton>
    </form>
  );
}
