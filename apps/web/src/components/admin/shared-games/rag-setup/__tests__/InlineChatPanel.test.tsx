/**
 * Tests for InlineChatPanel component
 * Admin RAG Dashboard - Issue #4364
 *
 * Coverage:
 * - Empty state when agentId is null
 * - Chat title rendering when agentId is provided
 * - Textarea placeholder text
 * - Send button disabled state when input is empty
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { InlineChatPanel } from '../InlineChatPanel';

// =========================================================================
// Mocks
// =========================================================================

vi.mock('@/hooks/useAgentChatStream', () => ({
  useAgentChatStream: () => ({
    state: {
      isStreaming: false,
      currentAnswer: '',
      statusMessage: '',
      error: null,
      followUpQuestions: [],
      chatThreadId: null,
      totalTokens: 0,
      debugSteps: [],
      modelDowngrade: null,
      strategyTier: null,
      executionId: null,
    },
    sendMessage: vi.fn(),
    stopStreaming: vi.fn(),
    reset: vi.fn(),
  }),
}));

// =========================================================================
// Tests
// =========================================================================

describe('InlineChatPanel', () => {
  // -----------------------------------------------------------------------
  // Empty state (no agent)
  // -----------------------------------------------------------------------

  describe('when agentId is null', () => {
    it('shows empty state message "Crea un agente per avviare la chat"', () => {
      render(<InlineChatPanel agentId={null} chatThreadId={null} />);

      expect(screen.getByText('Crea un agente per avviare la chat')).toBeInTheDocument();
    });

    it('still renders the "Chat di Test" title', () => {
      render(<InlineChatPanel agentId={null} chatThreadId={null} />);

      expect(screen.getByText('Chat di Test')).toBeInTheDocument();
    });

    it('shows instructional sub-text', () => {
      render(<InlineChatPanel agentId={null} chatThreadId={null} />);

      expect(
        screen.getByText('Prima carica documenti e crea un agente RAG')
      ).toBeInTheDocument();
    });

    it('does NOT render the textarea input', () => {
      render(<InlineChatPanel agentId={null} chatThreadId={null} />);

      expect(
        screen.queryByPlaceholderText('Scrivi un messaggio per testare il RAG...')
      ).not.toBeInTheDocument();
    });
  });

  // -----------------------------------------------------------------------
  // Active chat (agentId provided)
  // -----------------------------------------------------------------------

  describe('when agentId is provided', () => {
    it('renders "Chat di Test (Privata)" title', () => {
      render(<InlineChatPanel agentId="agent-123" chatThreadId={null} />);

      expect(screen.getByText('Chat di Test (Privata)')).toBeInTheDocument();
    });

    it('shows placeholder text in textarea', () => {
      render(<InlineChatPanel agentId="agent-123" chatThreadId={null} />);

      expect(
        screen.getByPlaceholderText('Scrivi un messaggio per testare il RAG...')
      ).toBeInTheDocument();
    });

    it('disables the send button when input is empty', () => {
      render(<InlineChatPanel agentId="agent-123" chatThreadId={null} />);

      // The send button has sr-only "Invia" text
      const sendButton = screen.getByRole('button', { name: /invia/i });
      expect(sendButton).toBeDisabled();
    });

    it('renders the message input area', () => {
      render(<InlineChatPanel agentId="agent-123" chatThreadId={null} />);

      const textarea = screen.getByRole('textbox');
      expect(textarea).toBeInTheDocument();
    });

    it('shows empty messages hint when no messages exist', () => {
      render(<InlineChatPanel agentId="agent-123" chatThreadId={null} />);

      expect(
        screen.getByText("Scrivi un messaggio per testare l'agente RAG")
      ).toBeInTheDocument();
    });

    it('accepts a chatThreadId without errors', () => {
      render(<InlineChatPanel agentId="agent-123" chatThreadId="thread-456" />);

      expect(
        screen.getByPlaceholderText('Scrivi un messaggio per testare il RAG...')
      ).toBeInTheDocument();
    });
  });
});
