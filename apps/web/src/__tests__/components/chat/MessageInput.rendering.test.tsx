/**
 * MessageInput Component Tests
 *
 * Tests for the MessageInput component that handles user message input
 * and submission with search mode toggle (AI-14).
 *
 * Target Coverage: 90%+ (from 60%)
 */

import React from 'react';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { vi } from 'vitest';
import { MessageInput } from '../../../components/chat/MessageInput';

// Mock the ChatProvider context
const mockUseChatContext = vi.fn();
vi.mock('../../../components/chat/ChatProvider', () => ({
  useChatContext: () => mockUseChatContext(),
}));

// Mock LoadingButton component
vi.mock('../../../components/loading/LoadingButton', () => ({
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
vi.mock('../../../components', () => ({
  SearchModeToggle: ({ value, onChange, disabled }: any) => (
    <div data-testid="search-mode-toggle" data-value={value} data-disabled={disabled}>
      <button onClick={() => onChange('vector')} disabled={disabled}>
        Vector
      </button>
      <button onClick={() => onChange('hybrid')} disabled={disabled}>
        Hybrid
      </button>
    </div>
  ),
}));

/**
 * Helper to setup mock context with default values
 */
const setupMockContext = (overrides?: any) => {
  mockUseChatContext.mockReturnValue({
    inputValue: '',
    setInputValue: vi.fn(),
    sendMessage: vi.fn(),
    selectedGameId: 'game-1',
    selectedAgentId: 'agent-1',
    loading: { sending: false },
    searchMode: 'vector',
    setSearchMode: vi.fn(),
    ...overrides,
  });
};

describe('MessageInput Component', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  /**
   * Test Group: Basic Rendering
   */
  describe('Basic Rendering', () => {
    it('renders text input field', () => {
      setupMockContext();
      render(<MessageInput />);

      expect(screen.getByLabelText('Message input')).toBeInTheDocument();
    });

    it('renders send button', () => {
      setupMockContext();
      render(<MessageInput />);

      expect(screen.getByTestId('loading-button')).toBeInTheDocument();
      expect(screen.getByText('Invia')).toBeInTheDocument();
    });

    it('renders search mode toggle (AI-14)', () => {
      setupMockContext();
      render(<MessageInput />);

      expect(screen.getByTestId('search-mode-toggle')).toBeInTheDocument();
    });

    it('displays placeholder text', () => {
      setupMockContext();
      render(<MessageInput />);

      expect(screen.getByPlaceholderText('Fai una domanda sul gioco...')).toBeInTheDocument();
    });

    it('has screen reader only label', () => {
      setupMockContext();
      render(<MessageInput />);

      const label = screen.getByText('Ask a question about the game');
      expect(label).toHaveClass('sr-only');
    });
  });

  /**
   * Test Group: Input Handling
   */
  describe('Input Handling', () => {
    it('updates input value when typing', () => {
      const setInputValue = vi.fn();
      setupMockContext({ setInputValue });
      render(<MessageInput />);

      const input = screen.getByLabelText('Message input');
      fireEvent.change(input, { target: { value: 'Test message' } });

      expect(setInputValue).toHaveBeenCalledWith('Test message');
    });

    it('displays current input value', () => {
      setupMockContext({ inputValue: 'Current message' });
      render(<MessageInput />);

      const input = screen.getByLabelText('Message input') as HTMLInputElement;
      expect(input.value).toBe('Current message');
    });

    it('handles multiple character inputs', () => {
      const setInputValue = vi.fn();
      setupMockContext({ setInputValue });
      render(<MessageInput />);

      const input = screen.getByLabelText('Message input');
      fireEvent.change(input, { target: { value: 'A' } });
      fireEvent.change(input, { target: { value: 'AB' } });
      fireEvent.change(input, { target: { value: 'ABC' } });

      expect(setInputValue).toHaveBeenCalledTimes(3);
    });

    it('handles clearing input', () => {
      const setInputValue = vi.fn();
      setupMockContext({ inputValue: 'Text', setInputValue });
      render(<MessageInput />);

      const input = screen.getByLabelText('Message input');
      fireEvent.change(input, { target: { value: '' } });

      expect(setInputValue).toHaveBeenCalledWith('');
    });

    it('accepts long messages', () => {
      const setInputValue = vi.fn();
      const longMessage = 'A'.repeat(1000);
      setupMockContext({ setInputValue });
      render(<MessageInput />);

      const input = screen.getByLabelText('Message input');
      fireEvent.change(input, { target: { value: longMessage } });

      expect(setInputValue).toHaveBeenCalledWith(longMessage);
    });
  });

  /**
   * Test Group: Form Submission
   */
  describe('Form Submission', () => {
    it('calls sendMessage on form submit', () => {
      const sendMessage = vi.fn();
      setupMockContext({
        inputValue: 'Test message',
        sendMessage,
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
      });
      render(<MessageInput />);

      const form = screen.getByLabelText('Message input').closest('form')!;
      fireEvent.submit(form);

      expect(sendMessage).toHaveBeenCalledWith('Test message');
    });

    it('calls sendMessage on button click', () => {
      const sendMessage = vi.fn();
      setupMockContext({
        inputValue: 'Test message',
        sendMessage,
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
      });
      render(<MessageInput />);

      const button = screen.getByTestId('loading-button');
      fireEvent.click(button);

      expect(sendMessage).toHaveBeenCalledWith('Test message');
    });

    it('prevents default form submission behavior', () => {
      setupMockContext({
        inputValue: 'Test',
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
      });
      render(<MessageInput />);

      const form = screen.getByLabelText('Message input').closest('form')!;
      const event = new Event('submit', { bubbles: true, cancelable: true });
      const preventDefaultSpy = vi.spyOn(event, 'preventDefault');

      form.dispatchEvent(event);

      expect(preventDefaultSpy).toHaveBeenCalled();
    });

    it('does not submit when input is empty', () => {
      const sendMessage = vi.fn();
      setupMockContext({
        inputValue: '',
        sendMessage,
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
      });
      render(<MessageInput />);

      const form = screen.getByLabelText('Message input').closest('form')!;
      fireEvent.submit(form);

      expect(sendMessage).not.toHaveBeenCalled();
    });

    it('does not submit when input is only whitespace', () => {
      const sendMessage = vi.fn();
      setupMockContext({
        inputValue: '   ',
        sendMessage,
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
      });
      render(<MessageInput />);

      const form = screen.getByLabelText('Message input').closest('form')!;
      fireEvent.submit(form);

      expect(sendMessage).not.toHaveBeenCalled();
    });

    it('does not submit when no game is selected', () => {
      const sendMessage = vi.fn();
      setupMockContext({
        inputValue: 'Test',
        sendMessage,
        selectedGameId: null,
        selectedAgentId: 'agent-1',
      });
      render(<MessageInput />);

      const form = screen.getByLabelText('Message input').closest('form')!;
      fireEvent.submit(form);

      expect(sendMessage).not.toHaveBeenCalled();
    });

    it('does not submit when no agent is selected', () => {
      const sendMessage = vi.fn();
      setupMockContext({
        inputValue: 'Test',
        sendMessage,
        selectedGameId: 'game-1',
        selectedAgentId: null,
      });
      render(<MessageInput />);

      const form = screen.getByLabelText('Message input').closest('form')!;
      fireEvent.submit(form);

      expect(sendMessage).not.toHaveBeenCalled();
    });

    it('handles async sendMessage call with void operator', async () => {
      const sendMessage = vi.fn().mockResolvedValue(undefined);
      setupMockContext({
        inputValue: 'Test',
        sendMessage,
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
      });
      render(<MessageInput />);

      const form = screen.getByLabelText('Message input').closest('form')!;
      fireEvent.submit(form);

      await waitFor(() => {
        expect(sendMessage).toHaveBeenCalled();
      });
    });
  });

  /**
   * Test Group: Disabled States
   */
  describe('Disabled States', () => {
    it('disables input when sending', () => {
      setupMockContext({ loading: { sending: true } });
      render(<MessageInput />);

      const input = screen.getByLabelText('Message input');
      expect(input).toBeDisabled();
    });

    it('disables button when sending', () => {
      setupMockContext({ loading: { sending: true }, inputValue: 'Test' });
      render(<MessageInput />);

      const button = screen.getByTestId('loading-button');
      expect(button).toBeDisabled();
    });

    it('disables search mode toggle when sending', () => {
      setupMockContext({ loading: { sending: true } });
      render(<MessageInput />);

      const toggle = screen.getByTestId('search-mode-toggle');
      expect(toggle).toHaveAttribute('data-disabled', 'true');
    });

    it('shows loading state on button', () => {
      setupMockContext({ loading: { sending: true }, inputValue: 'Test' });
      render(<MessageInput />);

      const button = screen.getByTestId('loading-button');
      expect(button).toHaveAttribute('data-loading', 'true');
      expect(button).toHaveTextContent('Invio...');
    });
  });
});
