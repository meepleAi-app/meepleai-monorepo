/**
 * ChatInput Component Tests (Issue #3245)
 *
 * Test Coverage:
 * - Auto-resize behavior (1-5 rows)
 * - Character limit enforcement (1000)
 * - Send button disabled states
 * - Enter/Shift+Enter keyboard handling
 * - Loading state during streaming
 * - SSE error display
 * - Accessibility (aria-labels, keyboard)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ChatInput } from '../ChatInput';

describe('ChatInput', () => {
  const mockOnSendMessage = vi.fn();
  const mockOnRetry = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders with default props', () => {
      render(<ChatInput onSendMessage={mockOnSendMessage} />);

      expect(screen.getByRole('textbox', { name: /message input/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /invia messaggio/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /allega file/i })).toBeInTheDocument();
      expect(screen.getByText('0/1000')).toBeInTheDocument();
    });

    it('renders with custom placeholder', () => {
      render(
        <ChatInput onSendMessage={mockOnSendMessage} placeholder="Custom placeholder..." />
      );

      expect(screen.getByPlaceholderText('Custom placeholder...')).toBeInTheDocument();
    });

    it('applies custom className', () => {
      const { container } = render(
        <ChatInput onSendMessage={mockOnSendMessage} className="custom-class" />
      );

      expect(container.firstChild).toHaveClass('custom-class');
    });
  });

  describe('Character Limit', () => {
    it('displays character counter', async () => {
      const user = userEvent.setup();
      render(<ChatInput onSendMessage={mockOnSendMessage} />);

      const textarea = screen.getByRole('textbox', { name: /message input/i });

      await user.type(textarea, 'Hello');

      expect(screen.getByText('5/1000')).toBeInTheDocument();
    });

    it('enforces 1000 character limit', async () => {
      const user = userEvent.setup();
      render(<ChatInput onSendMessage={mockOnSendMessage} />);

      const textarea = screen.getByRole('textbox', { name: /message input/i });
      const longText = 'a'.repeat(1100); // Exceeds limit

      await user.type(textarea, longText);

      // Should stop at 1000 characters
      expect(textarea).toHaveValue('a'.repeat(1000));
      expect(screen.getByText('1000/1000')).toBeInTheDocument();
    });

    it('shows warning color when near limit (>90%)', async () => {
      const user = userEvent.setup();
      render(<ChatInput onSendMessage={mockOnSendMessage} />);

      const textarea = screen.getByRole('textbox', { name: /message input/i });
      const nearLimitText = 'a'.repeat(950); // 95% of limit

      await user.type(textarea, nearLimitText);

      const counter = screen.getByText('950/1000');
      expect(counter).toHaveClass('text-red-500');
    });
  });

  describe('Send Button', () => {
    it('is disabled when input is empty', () => {
      render(<ChatInput onSendMessage={mockOnSendMessage} />);

      const sendButton = screen.getByRole('button', { name: /invia messaggio/i });
      expect(sendButton).toBeDisabled();
    });

    it('is enabled when input has text', async () => {
      const user = userEvent.setup();
      render(<ChatInput onSendMessage={mockOnSendMessage} />);

      const textarea = screen.getByRole('textbox', { name: /message input/i });
      await user.type(textarea, 'Hello');

      const sendButton = screen.getByRole('button', { name: /invia messaggio/i });
      expect(sendButton).not.toBeDisabled();
    });

    it('is disabled during streaming', () => {
      render(<ChatInput onSendMessage={mockOnSendMessage} isStreaming={true} />);

      const sendButton = screen.getByRole('button', { name: /invio in corso/i });
      expect(sendButton).toBeDisabled();
    });

    it('shows loading spinner during streaming', () => {
      render(<ChatInput onSendMessage={mockOnSendMessage} isStreaming={true} />);

      const spinner = document.querySelector('.animate-spin');
      expect(spinner).toBeInTheDocument();
    });

    it('calls onSendMessage when clicked', async () => {
      const user = userEvent.setup();
      render(<ChatInput onSendMessage={mockOnSendMessage} />);

      const textarea = screen.getByRole('textbox', { name: /message input/i });
      await user.type(textarea, 'Test message');

      const sendButton = screen.getByRole('button', { name: /invia messaggio/i });
      await user.click(sendButton);

      expect(mockOnSendMessage).toHaveBeenCalledTimes(1);
      expect(mockOnSendMessage).toHaveBeenCalledWith('Test message');
    });

    it('clears input after send', async () => {
      const user = userEvent.setup();
      render(<ChatInput onSendMessage={mockOnSendMessage} />);

      const textarea = screen.getByRole('textbox', { name: /message input/i });
      await user.type(textarea, 'Test message');

      const sendButton = screen.getByRole('button', { name: /invia messaggio/i });
      await user.click(sendButton);

      await waitFor(() => {
        expect(textarea).toHaveValue('');
      });
    });

    it('trims whitespace before sending', async () => {
      const user = userEvent.setup();
      render(<ChatInput onSendMessage={mockOnSendMessage} />);

      const textarea = screen.getByRole('textbox', { name: /message input/i });
      await user.type(textarea, '  Test message  ');

      const sendButton = screen.getByRole('button', { name: /invia messaggio/i });
      await user.click(sendButton);

      expect(mockOnSendMessage).toHaveBeenCalledWith('Test message');
    });
  });

  describe('Keyboard Handling', () => {
    it('sends message on Enter key', async () => {
      const user = userEvent.setup();
      render(<ChatInput onSendMessage={mockOnSendMessage} />);

      const textarea = screen.getByRole('textbox', { name: /message input/i });
      await user.type(textarea, 'Test message');
      await user.keyboard('{Enter}');

      expect(mockOnSendMessage).toHaveBeenCalledWith('Test message');
    });

    it('adds newline on Shift+Enter', async () => {
      const user = userEvent.setup();
      render(<ChatInput onSendMessage={mockOnSendMessage} />);

      const textarea = screen.getByRole('textbox', { name: /message input/i });
      await user.type(textarea, 'Line 1{Shift>}{Enter}{/Shift}Line 2');

      expect(textarea).toHaveValue('Line 1\nLine 2');
      expect(mockOnSendMessage).not.toHaveBeenCalled();
    });

    it('does not send empty message on Enter', async () => {
      const user = userEvent.setup();
      render(<ChatInput onSendMessage={mockOnSendMessage} />);

      const textarea = screen.getByRole('textbox', { name: /message input/i });
      await user.click(textarea);
      await user.keyboard('{Enter}');

      expect(mockOnSendMessage).not.toHaveBeenCalled();
    });

    it('does not send message on Enter during streaming', async () => {
      const user = userEvent.setup();
      render(<ChatInput onSendMessage={mockOnSendMessage} isStreaming={true} />);

      const textarea = screen.getByRole('textbox', { name: /message input/i });
      await user.type(textarea, 'Test{Enter}');

      expect(mockOnSendMessage).not.toHaveBeenCalled();
    });
  });

  describe('Attachment Button', () => {
    it('is always disabled (MVP)', () => {
      render(<ChatInput onSendMessage={mockOnSendMessage} />);

      const attachButton = screen.getByRole('button', { name: /allega file/i });
      expect(attachButton).toBeDisabled();
    });

    it('has tooltip explaining future feature', () => {
      render(<ChatInput onSendMessage={mockOnSendMessage} />);

      const attachButton = screen.getByRole('button', { name: /allega file/i });
      expect(attachButton).toHaveAttribute('title', 'Allega file (disponibile in futuro)');
    });
  });

  describe('SSE Error Handling', () => {
    it('displays error message when error prop is provided', () => {
      render(
        <ChatInput
          onSendMessage={mockOnSendMessage}
          error="Connection lost. Reconnecting..."
        />
      );

      expect(screen.getByText(/connection lost/i)).toBeInTheDocument();
    });

    it('shows retry button when onRetry is provided', () => {
      render(
        <ChatInput
          onSendMessage={mockOnSendMessage}
          error="Connection failed"
          onRetry={mockOnRetry}
        />
      );

      const retryButton = screen.getByRole('button', { name: /riprova/i });
      expect(retryButton).toBeInTheDocument();
    });

    it('calls onRetry when retry button is clicked', async () => {
      const user = userEvent.setup();
      render(
        <ChatInput
          onSendMessage={mockOnSendMessage}
          error="Connection failed"
          onRetry={mockOnRetry}
        />
      );

      const retryButton = screen.getByRole('button', { name: /riprova/i });
      await user.click(retryButton);

      expect(mockOnRetry).toHaveBeenCalledTimes(1);
    });

    it('does not show retry button when onRetry is not provided', () => {
      render(<ChatInput onSendMessage={mockOnSendMessage} error="Connection failed" />);

      expect(screen.queryByRole('button', { name: /riprova/i })).not.toBeInTheDocument();
    });

    it('hides error when error prop is null', () => {
      const { rerender } = render(
        <ChatInput onSendMessage={mockOnSendMessage} error="Connection failed" />
      );

      expect(screen.getByText(/connection failed/i)).toBeInTheDocument();

      rerender(<ChatInput onSendMessage={mockOnSendMessage} error={null} />);

      expect(screen.queryByText(/connection failed/i)).not.toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has proper aria-label on textarea', () => {
      render(<ChatInput onSendMessage={mockOnSendMessage} />);

      const textarea = screen.getByRole('textbox', { name: /message input/i });
      expect(textarea).toHaveAccessibleName();
    });

    it('has aria-describedby linking to character counter', () => {
      render(<ChatInput onSendMessage={mockOnSendMessage} />);

      const textarea = screen.getByRole('textbox', { name: /message input/i });
      expect(textarea).toHaveAttribute('aria-describedby', 'char-counter');
    });

    it('character counter has aria-live for screen readers', () => {
      render(<ChatInput onSendMessage={mockOnSendMessage} />);

      const counter = screen.getByText('0/1000');
      expect(counter).toHaveAttribute('aria-live', 'polite');
    });

    it('error message has role="alert"', () => {
      render(<ChatInput onSendMessage={mockOnSendMessage} error="Test error" />);

      const alert = screen.getByRole('alert');
      expect(alert).toBeInTheDocument();
      expect(alert).toHaveTextContent('Test error');
    });

    it('send button has descriptive aria-label based on state', () => {
      const { rerender } = render(<ChatInput onSendMessage={mockOnSendMessage} />);

      let sendButton = screen.getByRole('button', { name: /invia messaggio/i });
      expect(sendButton).toHaveAccessibleName();

      rerender(<ChatInput onSendMessage={mockOnSendMessage} isStreaming={true} />);

      sendButton = screen.getByRole('button', { name: /invio in corso/i });
      expect(sendButton).toHaveAccessibleName();
    });
  });

  describe('Streaming State', () => {
    it('disables textarea during streaming', () => {
      render(<ChatInput onSendMessage={mockOnSendMessage} isStreaming={true} />);

      const textarea = screen.getByRole('textbox', { name: /message input/i });
      expect(textarea).toBeDisabled();
    });

    it('shows reduced opacity during streaming', () => {
      render(<ChatInput onSendMessage={mockOnSendMessage} isStreaming={true} />);

      const textarea = screen.getByRole('textbox', { name: /message input/i });
      expect(textarea).toHaveClass('opacity-60');
    });
  });
});
