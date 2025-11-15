/**
 * Integration tests for MessageInput with optimistic updates
 * Issue #1167: Chat Optimistic Updates
 *
 * Test scenarios:
 * - Optimistic message sending workflow
 * - Input clearing on send
 * - Input restoration on error
 * - Error toast display
 * - Button disabled states during optimistic update
 */

import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MessageInput } from '../MessageInput';
import { useChatContext } from '../ChatProvider';
import { useChatOptimistic } from '@/hooks/useChatOptimistic';
import { toast } from 'sonner';

// Mock dependencies
jest.mock('../ChatProvider', () => ({
  useChatContext: jest.fn(),
}));

jest.mock('@/hooks/useChatOptimistic');
jest.mock('sonner');

// Mock shadcn/ui components
jest.mock('@/components/ui/input', () => ({
  Input: (props: any) => <input {...props} />,
}));

jest.mock('../../loading/LoadingButton', () => ({
  LoadingButton: ({ children, isLoading, disabled, ...props }: any) => (
    <button disabled={disabled || isLoading} {...props}>
      {isLoading ? 'Loading...' : children}
    </button>
  ),
}));

jest.mock('@/components', () => ({
  SearchModeToggle: () => <div data-testid="search-mode-toggle">Search Mode</div>,
}));

const mockUseChatContext = useChatContext as jest.MockedFunction<typeof useChatContext>;
const mockUseChatOptimistic = useChatOptimistic as jest.MockedFunction<typeof useChatOptimistic>;
const mockToast = toast as jest.Mocked<typeof toast>;

describe('MessageInput - Optimistic Updates (#1167)', () => {
  const mockSetInputValue = jest.fn();
  const mockSendMessageOptimistic = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();

    mockUseChatContext.mockReturnValue({
      inputValue: '',
      setInputValue: mockSetInputValue,
      selectedGameId: 'game-1',
      selectedAgentId: 'agent-1',
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
      searchMode: 'hybrid',
      setSearchMode: jest.fn(),
      // Other context values
      authUser: null,
      games: [],
      agents: [],
      chats: [],
      activeChatId: null,
      messages: [],
      createChat: jest.fn(),
      deleteChat: jest.fn(),
      selectChat: jest.fn(),
      selectGame: jest.fn(),
      selectAgent: jest.fn(),
      sendMessage: jest.fn(),
      editMessage: jest.fn(),
      deleteMessage: jest.fn(),
      setMessageFeedback: jest.fn(),
      errorMessage: '',
      sidebarCollapsed: false,
      toggleSidebar: jest.fn(),
      editingMessageId: null,
      editContent: '',
      setEditContent: jest.fn(),
      startEditMessage: jest.fn(),
      cancelEdit: jest.fn(),
      saveEdit: jest.fn(),
    });

    mockUseChatOptimistic.mockReturnValue({
      sendMessageOptimistic: mockSendMessageOptimistic,
      isOptimisticUpdate: false,
      messages: [],
    });

    mockToast.error = jest.fn();
  });

  describe('Optimistic sending workflow', () => {
    it('should send message optimistically on form submit', async () => {
      mockUseChatContext.mockReturnValue({
        ...mockUseChatContext(),
        inputValue: 'Test message',
      });

      mockSendMessageOptimistic.mockResolvedValueOnce(undefined);

      render(<MessageInput />);

      const form = screen.getByRole('textbox').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(mockSendMessageOptimistic).toHaveBeenCalledWith('Test message');
      });
    });

    it('should clear input immediately after submit', async () => {
      mockUseChatContext.mockReturnValue({
        ...mockUseChatContext(),
        inputValue: 'Test message',
      });

      mockSendMessageOptimistic.mockResolvedValueOnce(undefined);

      render(<MessageInput />);

      const form = screen.getByRole('textbox').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(mockSetInputValue).toHaveBeenCalledWith('');
      });
    });

    it('should disable input and button during optimistic update', async () => {
      mockUseChatOptimistic.mockReturnValue({
        sendMessageOptimistic: mockSendMessageOptimistic,
        isOptimisticUpdate: true, // Optimistic update in progress
        messages: [],
      });

      mockUseChatContext.mockReturnValue({
        ...mockUseChatContext(),
        inputValue: 'Test message',
      });

      render(<MessageInput />);

      const input = screen.getByRole('textbox');
      const button = screen.getByRole('button', { name: /send message/i });

      expect(input).toBeDisabled();
      expect(button).toBeDisabled();
    });
  });

  describe('Error handling and rollback', () => {
    it('should restore input value on error', async () => {
      const error = new Error('Network error');
      mockSendMessageOptimistic.mockRejectedValueOnce(error);

      mockUseChatContext.mockReturnValue({
        ...mockUseChatContext(),
        inputValue: 'Test message',
      });

      render(<MessageInput />);

      const form = screen.getByRole('textbox').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        // First call clears input, second call restores on error
        expect(mockSetInputValue).toHaveBeenCalledWith('');
        expect(mockSetInputValue).toHaveBeenCalledWith('Test message');
      });
    });

    it('should show error toast on send failure', async () => {
      const error = new Error('Network error');
      mockSendMessageOptimistic.mockRejectedValueOnce(error);

      mockUseChatContext.mockReturnValue({
        ...mockUseChatContext(),
        inputValue: 'Test message',
      });

      render(<MessageInput />);

      const form = screen.getByRole('textbox').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(mockToast.error).toHaveBeenCalledWith(
          expect.stringContaining('Network error')
        );
      });
    });

    it('should handle non-Error exceptions gracefully', async () => {
      mockSendMessageOptimistic.mockRejectedValueOnce('String error');

      mockUseChatContext.mockReturnValue({
        ...mockUseChatContext(),
        inputValue: 'Test message',
      });

      render(<MessageInput />);

      const form = screen.getByRole('textbox').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(mockToast.error).toHaveBeenCalledWith(
          expect.stringContaining('Errore di comunicazione')
        );
      });
    });
  });

  describe('Validation', () => {
    it('should not send empty message', async () => {
      mockUseChatContext.mockReturnValue({
        ...mockUseChatContext(),
        inputValue: '   ',
      });

      render(<MessageInput />);

      const form = screen.getByRole('textbox').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(mockSendMessageOptimistic).not.toHaveBeenCalled();
      });
    });

    it('should not send if game not selected', async () => {
      mockUseChatContext.mockReturnValue({
        ...mockUseChatContext(),
        inputValue: 'Test message',
        selectedGameId: null,
      });

      render(<MessageInput />);

      const form = screen.getByRole('textbox').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(mockSendMessageOptimistic).not.toHaveBeenCalled();
      });
    });

    it('should not send if agent not selected', async () => {
      mockUseChatContext.mockReturnValue({
        ...mockUseChatContext(),
        inputValue: 'Test message',
        selectedAgentId: null,
      });

      render(<MessageInput />);

      const form = screen.getByRole('textbox').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(mockSendMessageOptimistic).not.toHaveBeenCalled();
      });
    });
  });

  describe('Button states', () => {
    it('should show loading state during send', async () => {
      mockUseChatContext.mockReturnValue({
        ...mockUseChatContext(),
        inputValue: 'Test message',
      });

      // Delay resolution to test loading state
      mockSendMessageOptimistic.mockImplementation(
        () => new Promise(resolve => setTimeout(resolve, 100))
      );

      render(<MessageInput />);

      const form = screen.getByRole('textbox').closest('form');
      fireEvent.submit(form!);

      // Should show loading state
      await waitFor(() => {
        const button = screen.getByRole('button', { name: /send message/i });
        expect(button).toHaveTextContent('Loading...');
      });
    });

    it('should disable button when input is empty', () => {
      mockUseChatContext.mockReturnValue({
        ...mockUseChatContext(),
        inputValue: '',
      });

      render(<MessageInput />);

      const button = screen.getByRole('button', { name: /send message/i });
      expect(button).toBeDisabled();
    });

    it('should enable button when input has content', () => {
      mockUseChatContext.mockReturnValue({
        ...mockUseChatContext(),
        inputValue: 'Test message',
      });

      render(<MessageInput />);

      const button = screen.getByRole('button', { name: /send message/i });
      expect(button).not.toBeDisabled();
    });
  });
});
