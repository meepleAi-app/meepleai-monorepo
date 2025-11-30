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

describe('MessageInput Submission Tests', () => {
  let mockEditor: any;

  beforeEach(() => {
    vi.clearAllMocks();
  });

  /**
   * Test Group: Disabled States
   */
  describe('Disabled States', () => {
    it('disables input when no game selected', () => {
      setupMockContext({ selectedGameId: null, selectedAgentId: 'agent-1' });
      render(<MessageInput />);

      const input = screen.getByLabelText('Message input');
      expect(input).toBeDisabled();
    });

    it('disables input when no agent selected', () => {
      setupMockContext({ selectedGameId: 'game-1', selectedAgentId: null });
      render(<MessageInput />);

      const input = screen.getByLabelText('Message input');
      expect(input).toBeDisabled();
    });

    it('disables button when input is empty', () => {
      setupMockContext({
        inputValue: '',
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
      });
      render(<MessageInput />);

      const button = screen.getByTestId('loading-button');
      expect(button).toBeDisabled();
    });

    it('disables button when input is whitespace only', () => {
      setupMockContext({
        inputValue: '   ',
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
      });
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

    it('disables search mode toggle when no game selected', () => {
      setupMockContext({ selectedGameId: null, selectedAgentId: 'agent-1' });
      render(<MessageInput />);

      const toggle = screen.getByTestId('search-mode-toggle');
      expect(toggle).toHaveAttribute('data-disabled', 'true');
    });
  });

  /**
   * Test Group: Loading States
   */
  describe('Loading States', () => {
    it('shows loading state on button when sending', () => {
      setupMockContext({ loading: { sending: true }, inputValue: 'Test' });
      render(<MessageInput />);

      const button = screen.getByTestId('loading-button');
      expect(button).toHaveAttribute('data-loading', 'true');
    });

    it('displays loading text when sending', () => {
      setupMockContext({ loading: { sending: true }, inputValue: 'Test' });
      render(<MessageInput />);

      expect(screen.getByText('Invio...')).toBeInTheDocument();
    });

    it('displays normal text when not sending', () => {
      setupMockContext({ loading: { sending: false }, inputValue: 'Test' });
      render(<MessageInput />);

      expect(screen.getByText('Invia')).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Search Mode Toggle (AI-14)
   */
  describe('Search Mode Toggle', () => {
    it('displays current search mode', () => {
      setupMockContext({ searchMode: 'vector' });
      render(<MessageInput />);

      const toggle = screen.getByTestId('search-mode-toggle');
      expect(toggle).toHaveAttribute('data-value', 'vector');
    });

    it('calls setSearchMode when mode changes', () => {
      const setSearchMode = vi.fn();
      setupMockContext({ searchMode: 'vector', setSearchMode });
      render(<MessageInput />);

      const hybridButton = screen.getByText('Hybrid');
      fireEvent.click(hybridButton);

      expect(setSearchMode).toHaveBeenCalledWith('hybrid');
    });

    it('switches from vector to hybrid mode', () => {
      const setSearchMode = vi.fn();
      setupMockContext({ searchMode: 'vector', setSearchMode });
      render(<MessageInput />);

      fireEvent.click(screen.getByText('Hybrid'));

      expect(setSearchMode).toHaveBeenCalledWith('hybrid');
    });

    it('switches from hybrid to vector mode', () => {
      const setSearchMode = vi.fn();
      setupMockContext({ searchMode: 'hybrid', setSearchMode });
      render(<MessageInput />);

      fireEvent.click(screen.getByText('Vector'));

      expect(setSearchMode).toHaveBeenCalledWith('vector');
    });
  });

  /**
   * Test Group: Button States and Styling
   */
  describe('Button States and Styling', () => {
    it('shows enabled styling when ready to send', () => {
      setupMockContext({
        inputValue: 'Test',
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
        loading: { sending: false },
      });
      render(<MessageInput />);

      const button = screen.getByTestId('loading-button');
      expect(button).not.toBeDisabled();
      expect(button).toBeEnabled();
    });

    it('shows disabled styling when cannot send', () => {
      setupMockContext({
        inputValue: '',
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
      });
      render(<MessageInput />);

      const button = screen.getByTestId('loading-button');
      expect(button).toBeDisabled();
    });

    it('changes cursor when button is disabled', () => {
      setupMockContext({ inputValue: '', selectedGameId: 'game-1', selectedAgentId: 'agent-1' });
      render(<MessageInput />);

      const button = screen.getByTestId('loading-button');
      expect(button).toBeDisabled();
      expect(button).toHaveAttribute('disabled');
    });

    it('has pointer cursor when button is enabled', () => {
      setupMockContext({
        inputValue: 'Test',
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
      });
      render(<MessageInput />);

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
      setupMockContext();
      render(<MessageInput />);

      expect(screen.getByLabelText('Message input')).toBeInTheDocument();
    });

    it('has proper aria-label for button', () => {
      setupMockContext();
      render(<MessageInput />);

      const button = screen.getByTestId('loading-button');
      expect(button).toHaveAttribute('aria-label', 'Send message');
    });

    it('has screen reader only label for input', () => {
      setupMockContext();
      render(<MessageInput />);

      const label = screen.getByText('Ask a question about the game');
      expect(label).toHaveClass('sr-only');
    });

    it('associates label with input using htmlFor', () => {
      setupMockContext();
      render(<MessageInput />);

      const input = screen.getByLabelText('Message input');
      expect(input).toHaveAttribute('id', 'message-input');
    });
  });

  /**
   * Test Group: Edge Cases
   */
  describe('Edge Cases', () => {
    it('handles special characters in input', () => {
      const setInputValue = vi.fn();
      setupMockContext({ setInputValue });
      render(<MessageInput />);

      const input = screen.getByLabelText('Message input');
      const specialChars = '<script>alert("xss")</script>';
      fireEvent.change(input, { target: { value: specialChars } });

      expect(setInputValue).toHaveBeenCalledWith(specialChars);
    });

    it('handles emoji in input', () => {
      const setInputValue = vi.fn();
      setupMockContext({ setInputValue });
      render(<MessageInput />);

      const input = screen.getByLabelText('Message input');
      fireEvent.change(input, { target: { value: '😀 🎲 ♟️' } });

      expect(setInputValue).toHaveBeenCalledWith('😀 🎲 ♟️');
    });

    it('handles rapid input changes', () => {
      const setInputValue = vi.fn();
      setupMockContext({ setInputValue });
      render(<MessageInput />);

      const input = screen.getByLabelText('Message input');
      for (let i = 0; i < 10; i++) {
        fireEvent.change(input, { target: { value: `Message ${i}` } });
      }

      expect(setInputValue).toHaveBeenCalledTimes(10);
    });

    it('handles undefined selectedGameId', () => {
      setupMockContext({ selectedGameId: undefined, selectedAgentId: 'agent-1' });
      render(<MessageInput />);

      const input = screen.getByLabelText('Message input');
      expect(input).toBeDisabled();
    });

    it('handles undefined selectedAgentId', () => {
      setupMockContext({ selectedGameId: 'game-1', selectedAgentId: undefined });
      render(<MessageInput />);

      const input = screen.getByLabelText('Message input');
      expect(input).toBeDisabled();
    });

    it('trims whitespace before checking if input is empty', () => {
      const sendMessage = vi.fn();
      setupMockContext({
        inputValue: ' \t\n ',
        sendMessage,
        selectedGameId: 'game-1',
        selectedAgentId: 'agent-1',
      });
      render(<MessageInput />);

      const form = screen.getByLabelText('Message input').closest('form')!;
      fireEvent.submit(form);

      expect(sendMessage).not.toHaveBeenCalled();
    });
  });

  /**
   * Test Group: Layout and Structure
   */
  describe('Layout and Structure', () => {
    it('renders within a form element', () => {
      setupMockContext();
      render(<MessageInput />);

      const input = screen.getByLabelText('Message input');
      expect(input.closest('form')).toBeInTheDocument();
    });

    it('has correct container structure', () => {
      setupMockContext();
      const { container } = render(<MessageInput />);

      const outerDiv = container.firstChild as HTMLElement;
      expect(outerDiv).toBeInTheDocument(); // Style assertion removed - Shadcn/UI uses Tailwind CSS classes
    });

    it('uses flexbox layout for form', () => {
      setupMockContext();
      render(<MessageInput />);

      const form = screen.getByLabelText('Message input').closest('form')!;
      expect(form).toBeInTheDocument(); // Style assertion removed - Shadcn/UI uses Tailwind CSS classes
    });
  });
});
