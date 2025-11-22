/**
 * Integration tests for Message component with optimistic updates
 * Issue #1167: Chat Optimistic Updates
 *
 * Test scenarios:
 * - Optimistic message rendering with visual feedback
 * - Loading indicator display for temporary messages
 * - Actions disabled for optimistic messages
 * - ARIA attributes for accessibility
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import { Message } from '../Message';
import { Message as MessageType } from '@/types';
import { useChatStore } from '@/store/chat/store';

// Mock dependencies
jest.mock('@/store/chat/store', () => ({
  useChatStore: jest.fn(),
}));

jest.mock('../MessageActions', () => ({
  MessageActions: () => <div data-testid="message-actions">Actions</div>,
}));

jest.mock('../MessageEditForm', () => ({
  MessageEditForm: () => <div data-testid="message-edit-form">Edit Form</div>,
}));

jest.mock('../../FollowUpQuestions', () => ({
  FollowUpQuestions: () => <div data-testid="follow-up-questions">Questions</div>,
}));

const mockUseChatStore = useChatStore as jest.MockedFunction<typeof useChatStore>;

describe('Message - Optimistic Updates (#1167)', () => {
  beforeEach(() => {
    mockUseChatStore.mockReturnValue({
      editingMessageId: null,
      startEditMessage: jest.fn(),
      deleteMessage: jest.fn(),
      setMessageFeedback: jest.fn(),
      loading: {
        games: false,
        agents: false,
        chats: false,
        messages: false,
        sending: false,
        creating: false,
        updating: false,
        deleting: false,
      },
      setInputValue: jest.fn(),
      // Other context values
      authUser: null,
      games: [],
      agents: [],
      chats: [],
      activeChatId: null,
      messages: [],
      selectedGameId: null,
      selectedAgentId: null,
      createChat: jest.fn(),
      deleteChat: jest.fn(),
      selectChat: jest.fn(),
      selectGame: jest.fn(),
      selectAgent: jest.fn(),
      sendMessage: jest.fn(),
      editMessage: jest.fn(),
      errorMessage: '',
      sidebarCollapsed: false,
      toggleSidebar: jest.fn(),
      editContent: '',
      setEditContent: jest.fn(),
      cancelEdit: jest.fn(),
      saveEdit: jest.fn(),
      inputValue: '',
      searchMode: 'hybrid',
      setSearchMode: jest.fn(),
    });
  });

  describe('Optimistic message rendering', () => {
    it('should render optimistic message with sending indicator', () => {
      const optimisticMessage: MessageType = {
        id: 'temp-123',
        role: 'user',
        content: 'Test optimistic message',
        timestamp: new Date(),
        isOptimistic: true, // #1167
      };

      render(<Message message={optimisticMessage} isUser={true} />);

      expect(screen.getByText('Test optimistic message')).toBeInTheDocument();
      expect(screen.getByText('(invio...)')).toBeInTheDocument();
    });

    it('should apply opacity and pulse animation to optimistic messages', () => {
      const optimisticMessage: MessageType = {
        id: 'temp-123',
        role: 'user',
        content: 'Test message',
        timestamp: new Date(),
        isOptimistic: true,
      };

      const { container } = render(<Message message={optimisticMessage} isUser={true} />);

      const messageBubble = container.querySelector('[aria-busy="true"]');
      expect(messageBubble).toBeInTheDocument();
      expect(messageBubble).toHaveClass('opacity-60', 'animate-pulse');
    });

    it('should set ARIA busy attribute for optimistic messages', () => {
      const optimisticMessage: MessageType = {
        id: 'temp-123',
        role: 'user',
        content: 'Test message',
        timestamp: new Date(),
        isOptimistic: true,
      };

      const { container } = render(<Message message={optimisticMessage} isUser={true} />);

      const messageBubble = container.querySelector('[aria-busy="true"]');
      expect(messageBubble).toHaveAttribute('aria-label', 'Sending message...');
    });

    it('should not render actions for optimistic messages', () => {
      const optimisticMessage: MessageType = {
        id: 'temp-123',
        role: 'user',
        content: 'Test message',
        timestamp: new Date(),
        isOptimistic: true,
      };

      render(<Message message={optimisticMessage} isUser={true} />);

      expect(screen.queryByTestId('message-actions')).not.toBeInTheDocument();
    });

    it('should not show edited badge for optimistic messages', () => {
      const optimisticMessage: MessageType = {
        id: 'temp-123',
        role: 'user',
        content: 'Test message',
        timestamp: new Date(),
        isOptimistic: true,
        updatedAt: new Date().toISOString(), // Has updatedAt but is optimistic
      };

      render(<Message message={optimisticMessage} isUser={true} />);

      expect(screen.queryByText('(modificato)')).not.toBeInTheDocument();
      expect(screen.getByText('(invio...)')).toBeInTheDocument();
    });
  });

  describe('Normal message rendering (regression)', () => {
    it('should render normal message without optimistic indicators', () => {
      const normalMessage: MessageType = {
        id: 'msg-123',
        role: 'user',
        content: 'Normal message',
        timestamp: new Date(),
        isOptimistic: false,
      };

      const { container } = render(<Message message={normalMessage} isUser={true} />);

      expect(screen.getByText('Normal message')).toBeInTheDocument();
      expect(screen.queryByText('(invio...)')).not.toBeInTheDocument();

      const messageBubble = container.querySelector('[aria-busy="true"]');
      expect(messageBubble).not.toBeInTheDocument();
    });

    it('should render actions for normal user messages', () => {
      const normalMessage: MessageType = {
        id: 'msg-123',
        role: 'user',
        content: 'Normal message',
        timestamp: new Date(),
      };

      render(<Message message={normalMessage} isUser={true} />);

      expect(screen.getByTestId('message-actions')).toBeInTheDocument();
    });

    it('should show edited badge for edited messages', () => {
      const editedMessage: MessageType = {
        id: 'msg-123',
        role: 'user',
        content: 'Edited message',
        timestamp: new Date(),
        updatedAt: new Date().toISOString(),
      };

      render(<Message message={editedMessage} isUser={true} />);

      expect(screen.getByText('(modificato)')).toBeInTheDocument();
    });
  });

  describe('Edge cases', () => {
    it('should handle undefined isOptimistic as false', () => {
      const message: MessageType = {
        id: 'msg-123',
        role: 'user',
        content: 'Message without isOptimistic',
        timestamp: new Date(),
        // isOptimistic is undefined
      };

      const { container } = render(<Message message={message} isUser={true} />);

      const messageBubble = container.querySelector('[aria-busy="true"]');
      expect(messageBubble).not.toBeInTheDocument();
      expect(screen.getByTestId('message-actions')).toBeInTheDocument();
    });

    it('should not show actions for deleted messages even if not optimistic', () => {
      const deletedMessage: MessageType = {
        id: 'msg-123',
        role: 'user',
        content: 'Deleted message',
        timestamp: new Date(),
        isDeleted: true,
        isOptimistic: false,
      };

      render(<Message message={deletedMessage} isUser={true} />);

      expect(screen.queryByTestId('message-actions')).not.toBeInTheDocument();
      expect(screen.getByText('[Messaggio eliminato]')).toBeInTheDocument();
    });
  });
});
