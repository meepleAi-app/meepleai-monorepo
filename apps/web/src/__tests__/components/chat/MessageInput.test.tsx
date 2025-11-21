/**
 * MessageInput Component Tests
 *
 * Tests for the MessageInput component that handles user message input
 * and submission with search mode toggle (AI-14).
 *
 * Migrated to Zustand (Issue #1083)
 *
 * Target Coverage: 90%+ (from 60%)
 */

import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { MessageInput } from '../../../components/chat/MessageInput';
import { renderWithChatStore, resetChatStore } from '@/__tests__/utils/zustand-test-utils';
import { useChatStore } from '@/store/chat/store';

// Mock LoadingButton component
jest.mock('../../../components/loading/LoadingButton', () => ({
  LoadingButton: ({ children, onClick, disabled, isLoading, loadingText, ...props }: any) => (
    <button
      onClick={onClick}
      disabled={disabled}
      data-testid="loading-button"
      data-loading={isLoading}
      {...props}
    >
      {isLoading ? loadingText : children}
    </button>
  ),
}));

// Mock SearchModeToggle component
jest.mock('../../../components', () => ({
  SearchModeToggle: ({ value, onChange, disabled }: any) => (
    <div data-testid="search-mode-toggle" data-value={value} data-disabled={disabled ? 'true' : 'false'}>
      <button onClick={() => onChange('Vector')} disabled={disabled}>
        Vector
      </button>
      <button onClick={() => onChange('Hybrid')} disabled={disabled}>
        Hybrid
      </button>
    </div>
  ),
}));

// Mock useChatOptimistic hook
const mockSendMessageOptimistic = jest.fn();
const mockToastError = jest.fn();

jest.mock('@/hooks/useChatOptimistic', () => ({
  useChatOptimistic: () => ({
    sendMessageOptimistic: mockSendMessageOptimistic,
    isOptimisticUpdate: false,
  }),
}));

// Mock keyboard shortcuts hook
jest.mock('@/hooks/useKeyboardShortcuts', () => ({
  useMessageInputShortcuts: jest.fn(),
  modKey: 'Cmd',
}));

// Mock toast
jest.mock('sonner', () => ({
  toast: {
    error: (...args: any[]) => mockToastError(...args),
  },
}));

describe('MessageInput Component', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    resetChatStore();
    mockSendMessageOptimistic.mockResolvedValue(undefined);
  });

  /**
   * Test Group: Basic Rendering
   */
  describe('Basic Rendering', () => {
    it('renders text input field', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      expect(screen.getByLabelText('Scrivi un messaggio')).toBeInTheDocument();
    });

    it('renders send button', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      expect(screen.getByTestId('loading-button')).toBeInTheDocument();
      expect(screen.getByText('Invia')).toBeInTheDocument();
    });

    it('renders search mode toggle (AI-14)', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      expect(screen.getByTestId('search-mode-toggle')).toBeInTheDocument();
    });

    it('displays placeholder text', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      expect(screen.getByPlaceholderText('Scrivi un messaggio...')).toBeInTheDocument();
    });

    it('has screen reader only label', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const label = screen.getByText('Scrivi un messaggio');
      expect(label).toHaveClass('sr-only');
    });
  });

  /**
   * Test Group: Input Handling
   */
  describe('Input Handling', () => {
    it('updates input value when typing', () => {
      const setInputValueSpy = jest.spyOn(useChatStore.getState(), 'setInputValue');
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const input = screen.getByLabelText('Scrivi un messaggio');
      fireEvent.change(input, { target: { value: 'Test message' } });

      expect(setInputValueSpy).toHaveBeenCalledWith('Test message');
    });

    it('displays current input value', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Current message',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const input = screen.getByLabelText('Scrivi un messaggio') as HTMLInputElement;
      expect(input.value).toBe('Current message');
    });

    it('handles multiple character inputs', () => {
      const setInputValueSpy = jest.spyOn(useChatStore.getState(), 'setInputValue');
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const input = screen.getByLabelText('Scrivi un messaggio');
      fireEvent.change(input, { target: { value: 'A' } });
      fireEvent.change(input, { target: { value: 'AB' } });
      fireEvent.change(input, { target: { value: 'ABC' } });

      expect(setInputValueSpy).toHaveBeenCalledTimes(3);
    });

    it('handles clearing input', () => {
      const setInputValueSpy = jest.spyOn(useChatStore.getState(), 'setInputValue');
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Text',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const input = screen.getByLabelText('Scrivi un messaggio');
      fireEvent.change(input, { target: { value: '' } });

      expect(setInputValueSpy).toHaveBeenCalledWith('');
    });

    it('accepts long messages', () => {
      const setInputValueSpy = jest.spyOn(useChatStore.getState(), 'setInputValue');
      const longMessage = 'A'.repeat(1000);
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const input = screen.getByLabelText('Scrivi un messaggio');
      fireEvent.change(input, { target: { value: longMessage } });

      expect(setInputValueSpy).toHaveBeenCalledWith(longMessage);
    });
  });

  /**
   * Test Group: Form Submission
   */
  describe('Form Submission', () => {
    it('calls sendMessageOptimistic on form submit', async () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test message',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const form = screen.getByLabelText('Scrivi un messaggio').closest('form')!;
      fireEvent.submit(form);

      await waitFor(() => {
        expect(mockSendMessageOptimistic).toHaveBeenCalledWith('Test message');
      });
    });

    it('calls sendMessageOptimistic on button click', async () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test message',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const button = screen.getByTestId('loading-button');
      fireEvent.click(button);

      await waitFor(() => {
        expect(mockSendMessageOptimistic).toHaveBeenCalledWith('Test message');
      });
    });

    it('prevents default form submission behavior', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const form = screen.getByLabelText('Scrivi un messaggio').closest('form')!;
      const event = new Event('submit', { bubbles: true, cancelable: true });
      const preventDefaultSpy = jest.spyOn(event, 'preventDefault');

      form.dispatchEvent(event);

      expect(preventDefaultSpy).toHaveBeenCalled();
    });

    it('does not submit when input is empty', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: '',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const form = screen.getByLabelText('Scrivi un messaggio').closest('form')!;
      fireEvent.submit(form);

      expect(mockSendMessageOptimistic).not.toHaveBeenCalled();
    });

    it('does not submit when input is only whitespace', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: '   ',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const form = screen.getByLabelText('Scrivi un messaggio').closest('form')!;
      fireEvent.submit(form);

      expect(mockSendMessageOptimistic).not.toHaveBeenCalled();
    });

    it('does not submit when no game is selected', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test',
          selectedGameId: null,
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const form = screen.getByLabelText('Scrivi un messaggio').closest('form')!;
      fireEvent.submit(form);

      expect(mockSendMessageOptimistic).not.toHaveBeenCalled();
    });

    it('does not submit when no agent is selected', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: null,
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const form = screen.getByLabelText('Scrivi un messaggio').closest('form')!;
      fireEvent.submit(form);

      expect(mockSendMessageOptimistic).not.toHaveBeenCalled();
    });

    it('handles async sendMessageOptimistic call with void operator', async () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const form = screen.getByLabelText('Scrivi un messaggio').closest('form')!;
      fireEvent.submit(form);

      await waitFor(() => {
        expect(mockSendMessageOptimistic).toHaveBeenCalled();
      });
    });

    it('clears input immediately on submit', async () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test message',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const form = screen.getByLabelText('Scrivi un messaggio').closest('form')!;
      fireEvent.submit(form);

      // Input should be cleared immediately for better UX
      await waitFor(() => {
        const input = screen.getByLabelText('Scrivi un messaggio') as HTMLInputElement;
        expect(input.value).toBe('');
      });
    });

    it('restores input on send error', async () => {
      mockSendMessageOptimistic.mockRejectedValueOnce(new Error('Network error'));

      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test message',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const form = screen.getByLabelText('Scrivi un messaggio').closest('form')!;
      fireEvent.submit(form);

      await waitFor(() => {
        const input = screen.getByLabelText('Scrivi un messaggio') as HTMLInputElement;
        expect(input.value).toBe('Test message');
        expect(mockToastError).toHaveBeenCalledWith("Errore nell'invio del messaggio: Network error");
      });
    });
  });

  /**
   * Test Group: Disabled States
   */
  describe('Disabled States', () => {
    it('disables input when sending', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: true, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const input = screen.getByLabelText('Scrivi un messaggio');
      expect(input).toBeDisabled();
    });

    it('disables button when sending', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: true, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const button = screen.getByTestId('loading-button');
      expect(button).toBeDisabled();
    });

    it('disables input when no game selected', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: null,
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const input = screen.getByLabelText('Scrivi un messaggio');
      expect(input).toBeDisabled();
    });

    it('disables input when no agent selected', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: null,
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const input = screen.getByLabelText('Scrivi un messaggio');
      expect(input).toBeDisabled();
    });

    it('disables button when input is empty', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: '',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const button = screen.getByTestId('loading-button');
      expect(button).toBeDisabled();
    });

    it('disables button when input is whitespace only', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: '   ',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const button = screen.getByTestId('loading-button');
      expect(button).toBeDisabled();
    });

    it('renders search mode toggle when sending', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: true, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const toggle = screen.getByTestId('search-mode-toggle');
      expect(toggle).toBeInTheDocument();
    });

    it('renders search mode toggle when no game selected', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: null,
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const toggle = screen.getByTestId('search-mode-toggle');
      expect(toggle).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Loading States
   */
  describe('Loading States', () => {
    it('shows loading state on button when sending', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: true, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const button = screen.getByTestId('loading-button');
      expect(button).toHaveAttribute('data-loading', 'true');
    });

    it('displays loading text when sending', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: true, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      expect(screen.getByText('Invio...')).toBeInTheDocument();
    });

    it('displays normal text when not sending', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      expect(screen.getByText('Invia')).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Search Mode Toggle (AI-14)
   */
  describe('Search Mode Toggle', () => {
    it('displays current search mode', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          searchMode: 'Vector',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const toggle = screen.getByTestId('search-mode-toggle');
      expect(toggle).toHaveAttribute('data-value', 'Vector');
    });

    it('calls setSearchMode when mode changes', () => {
      const setSearchModeSpy = jest.spyOn(useChatStore.getState(), 'setSearchMode');
      renderWithChatStore(<MessageInput />, {
        initialState: {
          searchMode: 'Vector',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const hybridButton = screen.getByText('Hybrid');
      fireEvent.click(hybridButton);

      expect(setSearchModeSpy).toHaveBeenCalledWith('Hybrid');
    });

    it('switches from vector to hybrid mode', () => {
      const setSearchModeSpy = jest.spyOn(useChatStore.getState(), 'setSearchMode');
      renderWithChatStore(<MessageInput />, {
        initialState: {
          searchMode: 'Vector',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      fireEvent.click(screen.getByText('Hybrid'));

      expect(setSearchModeSpy).toHaveBeenCalledWith('Hybrid');
    });

    it('switches from hybrid to vector mode', () => {
      const setSearchModeSpy = jest.spyOn(useChatStore.getState(), 'setSearchMode');
      renderWithChatStore(<MessageInput />, {
        initialState: {
          searchMode: 'Hybrid',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      fireEvent.click(screen.getByText('Vector'));

      expect(setSearchModeSpy).toHaveBeenCalledWith('Vector');
    });
  });

  /**
   * Test Group: Button States and Styling
   */
  describe('Button States and Styling', () => {
    it('shows enabled styling when ready to send', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const button = screen.getByTestId('loading-button');
      expect(button).not.toBeDisabled();
      expect(button).toBeEnabled();
    });

    it('shows disabled styling when cannot send', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: '',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const button = screen.getByTestId('loading-button');
      expect(button).toBeDisabled();
    });

    it('changes cursor when button is disabled', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: '',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const button = screen.getByTestId('loading-button');
      expect(button).toBeDisabled();
      expect(button).toHaveAttribute('disabled');
    });

    it('has pointer cursor when button is enabled', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: 'Test',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const button = screen.getByTestId('loading-button');
      expect(button).not.toBeDisabled();
      expect(button).toBeEnabled();
    });
  });

  /**
   * Test Group: Accessibility
   */
  describe('Accessibility', () => {
    it('has proper aria-label for input', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      expect(screen.getByLabelText('Scrivi un messaggio')).toBeInTheDocument();
    });

    it('has proper aria-label for button', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const button = screen.getByTestId('loading-button');
      expect(button).toHaveAttribute('aria-label', 'Send message (Cmd+Enter)');
    });

    it('has screen reader only label for input', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const label = screen.getByText('Scrivi un messaggio');
      expect(label).toHaveClass('sr-only');
    });

    it('associates label with input using htmlFor', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const input = screen.getByLabelText('Scrivi un messaggio');
      expect(input).toHaveAttribute('id', 'messageInput');
    });
  });

  /**
   * Test Group: Edge Cases
   */
  describe('Edge Cases', () => {
    it('handles special characters in input', () => {
      const setInputValueSpy = jest.spyOn(useChatStore.getState(), 'setInputValue');
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const input = screen.getByLabelText('Scrivi un messaggio');
      const specialChars = '<script>alert("xss")</script>';
      fireEvent.change(input, { target: { value: specialChars } });

      expect(setInputValueSpy).toHaveBeenCalledWith(specialChars);
    });

    it('handles emoji in input', () => {
      const setInputValueSpy = jest.spyOn(useChatStore.getState(), 'setInputValue');
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const input = screen.getByLabelText('Scrivi un messaggio');
      fireEvent.change(input, { target: { value: '😀 🎲 ♟️' } });

      expect(setInputValueSpy).toHaveBeenCalledWith('😀 🎲 ♟️');
    });

    it('handles rapid input changes', () => {
      const setInputValueSpy = jest.spyOn(useChatStore.getState(), 'setInputValue');
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const input = screen.getByLabelText('Scrivi un messaggio');
      for (let i = 0; i < 10; i++) {
        fireEvent.change(input, { target: { value: `Message ${i}` } });
      }

      expect(setInputValueSpy).toHaveBeenCalledTimes(10);
    });

    it('handles undefined selectedGameId', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: undefined,
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const input = screen.getByLabelText('Scrivi un messaggio');
      expect(input).toBeDisabled();
    });

    it('handles undefined selectedAgentId', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: undefined,
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const input = screen.getByLabelText('Scrivi un messaggio');
      expect(input).toBeDisabled();
    });

    it('trims whitespace before checking if input is empty', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          inputValue: ' \t\n ',
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const form = screen.getByLabelText('Scrivi un messaggio').closest('form')!;
      fireEvent.submit(form);

      expect(mockSendMessageOptimistic).not.toHaveBeenCalled();
    });
  });

  /**
   * Test Group: Layout and Structure
   */
  describe('Layout and Structure', () => {
    it('renders within a form element', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const input = screen.getByLabelText('Scrivi un messaggio');
      expect(input.closest('form')).toBeInTheDocument();
    });

    it('has correct container structure', () => {
      const { container } = renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const outerDiv = container.firstChild as HTMLElement;
      expect(outerDiv).toBeInTheDocument();
    });

    it('uses flexbox layout for form', () => {
      renderWithChatStore(<MessageInput />, {
        initialState: {
          selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
          selectedAgentId: 'agent-1',
          loading: { sending: false, chats: false, messages: false, creating: false, updating: false, deleting: false, games: false, agents: false }
        }
      });

      const form = screen.getByLabelText('Scrivi un messaggio').closest('form')!;
      expect(form).toBeInTheDocument();
    });
  });
});
