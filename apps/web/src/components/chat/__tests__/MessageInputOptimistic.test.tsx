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
import { screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MessageInput } from '../MessageInput';
import { renderWithChatStore, resetChatStore } from '@/__tests__/utils/zustand-test-utils';
import { useChatStore } from '@/store/chat/store';
import { useChatOptimistic } from '@/hooks/useChatOptimistic';
import { toast } from 'sonner';

// Mock dependencies
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

const mockUseChatOptimistic = useChatOptimistic as jest.MockedFunction<typeof useChatOptimistic>;
const mockToast = toast as jest.Mocked<typeof toast>;

describe('MessageInput - Optimistic Updates (#1167)', () => {
  const mockSendMessageOptimistic = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
    resetChatStore();

    mockUseChatOptimistic.mockReturnValue({
      sendMessageOptimistic: mockSendMessageOptimistic,
      isOptimisticUpdate: false,
      messages: [],
    });

    mockToast.error = jest.fn();
  });

  describe('Optimistic sending workflow', () => {
    it('should send message optimistically on form submit', async () => {
      mockSendMessageOptimistic.mockResolvedValueOnce(undefined);

      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test message',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
        }
      });

      const form = screen.getByRole('textbox').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(mockSendMessageOptimistic).toHaveBeenCalledWith('Test message');
      });
    });

    it('should clear input immediately after submit', async () => {
      const setInputValueSpy = jest.spyOn(useChatStore.getState(), 'setInputValue');
      mockSendMessageOptimistic.mockResolvedValueOnce(undefined);

      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test message',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
        }
      });

      const form = screen.getByRole('textbox').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(setInputValueSpy).toHaveBeenCalledWith('');
      });
    });

    it('should disable input and button during optimistic update', async () => {
      mockUseChatOptimistic.mockReturnValue({
        sendMessageOptimistic: mockSendMessageOptimistic,
        isOptimisticUpdate: true, // Optimistic update in progress
        messages: [],
      });

      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test message',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
        }
      });

      const input = screen.getByRole('textbox');
      const button = screen.getByRole('button', { name: /send message/i });

      expect(input).toBeDisabled();
      expect(button).toBeDisabled();
    });
  });

  describe('Error handling and rollback', () => {
    it('should restore input value on error', async () => {
      const setInputValueSpy = jest.spyOn(useChatStore.getState(), 'setInputValue');
      const error = new Error('Network error');
      mockSendMessageOptimistic.mockRejectedValueOnce(error);

      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test message',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
        }
      });

      const form = screen.getByRole('textbox').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        // First call clears input, second call restores on error
        expect(setInputValueSpy).toHaveBeenCalledWith('');
        expect(setInputValueSpy).toHaveBeenCalledWith('Test message');
      });
    });

    it('should show error toast on send failure', async () => {
      const error = new Error('Network error');
      mockSendMessageOptimistic.mockRejectedValueOnce(error);

      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test message',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
        }
      });

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

      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test message',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
        }
      });

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
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: '   ',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
        }
      });

      const form = screen.getByRole('textbox').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(mockSendMessageOptimistic).not.toHaveBeenCalled();
      });
    });

    it('should not send if game not selected', async () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test message',
          selectedGameId: null,
          selectedAgentId: 'agent-1',
        }
      });

      const form = screen.getByRole('textbox').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(mockSendMessageOptimistic).not.toHaveBeenCalled();
      });
    });

    it('should not send if agent not selected', async () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test message',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: null,
        }
      });

      const form = screen.getByRole('textbox').closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(mockSendMessageOptimistic).not.toHaveBeenCalled();
      });
    });
  });

  describe('Button states', () => {
    it('should show loading state during send', async () => {
      const user = userEvent.setup();

      // Mock to start with isOptimisticUpdate=false, then set to true during send
      let isOptimistic = false;
      mockUseChatOptimistic.mockImplementation(() => ({
        sendMessageOptimistic: async (msg: string) => {
          isOptimistic = true;
          await new Promise(resolve => setTimeout(resolve, 50));
          isOptimistic = false;
        },
        isOptimisticUpdate: isOptimistic,
        messages: [],
      }));

      const { rerender } = renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test message',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
        }
      });

      const button = screen.getByRole('button', { name: /send message/i });
      await user.click(button);

      // Rerender to reflect isOptimisticUpdate change
      mockUseChatOptimistic.mockReturnValue({
        sendMessageOptimistic: jest.fn(),
        isOptimisticUpdate: true,
        messages: [],
      });
      rerender(<MessageInput />);

      // Should show loading state
      await waitFor(() => {
        expect(button).toHaveTextContent('Loading...');
      });
    });

    it('should disable button when input is empty', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: '',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
        }
      });

      const button = screen.getByRole('button', { name: /send message/i });
      expect(button).toBeDisabled();
    });

    it('should enable button when input has content', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test message',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
        }
      });

      const button = screen.getByRole('button', { name: /send message/i });
      expect(button).not.toBeDisabled();
    });
  });
});
