/**
 * AgentChat Component Tests (Issue #4085)
 *
 * Tests for base chat UI with 3 layout variants.
 */

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { axe } from 'jest-axe';

import { AgentChat } from '../AgentChat';

// Mock chat sub-components
vi.mock('@/components/agent/chat', () => ({
  ChatMessageList: ({ messages, isStreaming }: any) => (
    <div data-testid="chat-message-list">
      {messages.map((m: any, i: number) => (
        <div key={i} data-testid={`message-${i}`}>{m.content}</div>
      ))}
      {isStreaming && <div data-testid="streaming-indicator">Typing...</div>}
    </div>
  ),
  ChatInput: ({ onSendMessage, isStreaming, error, onRetry, placeholder }: any) => (
    <div data-testid="chat-input">
      <input
        data-testid="chat-input-field"
        placeholder={placeholder}
        disabled={isStreaming}
        onKeyDown={(e) => {
          if (e.key === 'Enter' && !e.shiftKey && !isStreaming) {
            e.preventDefault();
            onSendMessage(e.currentTarget.value);
            e.currentTarget.value = '';
          }
        }}
      />
      {error && <div data-testid="chat-error">{error}</div>}
      {error && onRetry && <button data-testid="retry-button" onClick={onRetry}>Retry</button>}
    </div>
  ),
  TypingIndicator: () => <div data-testid="typing-indicator">...</div>,
}));

describe('AgentChat', () => {
  const defaultProps = {
    agentId: 'agent-123',
    gameId: 'game-456',
    gameName: 'Catan',
    agentName: 'Tutor Agent',
    strategy: 'RetrievalOnly' as const,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders chat container', () => {
      render(<AgentChat {...defaultProps} />);
      expect(screen.getByTestId('agent-chat')).toBeInTheDocument();
    });

    it('renders header with agent name', () => {
      render(<AgentChat {...defaultProps} />);
      expect(screen.getByText('Tutor Agent')).toBeInTheDocument();
    });

    it('renders game name in header', () => {
      render(<AgentChat {...defaultProps} />);
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('renders strategy badge', () => {
      render(<AgentChat {...defaultProps} />);
      expect(screen.getByTestId('strategy-badge')).toBeInTheDocument();
      expect(screen.getByText('Solo Retrieval')).toBeInTheDocument();
    });

    it('renders message list', () => {
      render(<AgentChat {...defaultProps} />);
      expect(screen.getByTestId('chat-message-list')).toBeInTheDocument();
    });

    it('renders input field', () => {
      render(<AgentChat {...defaultProps} />);
      expect(screen.getByTestId('chat-input')).toBeInTheDocument();
    });

    it('renders Bot icon in header', () => {
      const { container } = render(<AgentChat {...defaultProps} />);
      const header = screen.getByTestId('agent-chat-header');
      const svg = header.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });
  });

  describe('Layout Variants', () => {
    it('applies modal layout classes', () => {
      render(<AgentChat {...defaultProps} layout="modal" />);
      const chat = screen.getByTestId('agent-chat');
      expect(chat).toHaveAttribute('data-layout', 'modal');
      expect(chat).toHaveClass('max-w-[600px]');
    });

    it('applies sidebar layout classes', () => {
      render(<AgentChat {...defaultProps} layout="sidebar" />);
      const chat = screen.getByTestId('agent-chat');
      expect(chat).toHaveAttribute('data-layout', 'sidebar');
      expect(chat).toHaveClass('w-[400px]');
    });

    it('applies full-page layout classes', () => {
      render(<AgentChat {...defaultProps} layout="full-page" />);
      const chat = screen.getByTestId('agent-chat');
      expect(chat).toHaveAttribute('data-layout', 'full-page');
      expect(chat).toHaveClass('max-w-[800px]');
    });

    it('shows overlay for modal layout', () => {
      render(<AgentChat {...defaultProps} layout="modal" />);
      expect(screen.getByTestId('agent-chat-overlay')).toBeInTheDocument();
    });

    it('shows overlay for sidebar layout', () => {
      render(<AgentChat {...defaultProps} layout="sidebar" />);
      expect(screen.getByTestId('agent-chat-overlay')).toBeInTheDocument();
    });

    it('does not show overlay for full-page layout', () => {
      render(<AgentChat {...defaultProps} layout="full-page" />);
      expect(screen.queryByTestId('agent-chat-overlay')).not.toBeInTheDocument();
    });
  });

  describe('Close Button', () => {
    it('shows close button in modal layout', () => {
      const onClose = vi.fn();
      render(<AgentChat {...defaultProps} layout="modal" onClose={onClose} />);
      expect(screen.getByTestId('close-chat-button')).toBeInTheDocument();
    });

    it('shows close button in sidebar layout', () => {
      const onClose = vi.fn();
      render(<AgentChat {...defaultProps} layout="sidebar" onClose={onClose} />);
      expect(screen.getByTestId('close-chat-button')).toBeInTheDocument();
    });

    it('does not show close button in full-page layout', () => {
      const onClose = vi.fn();
      render(<AgentChat {...defaultProps} layout="full-page" onClose={onClose} />);
      expect(screen.queryByTestId('close-chat-button')).not.toBeInTheDocument();
    });

    it('calls onClose when close button clicked', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      render(<AgentChat {...defaultProps} layout="modal" onClose={onClose} />);

      await user.click(screen.getByTestId('close-chat-button'));
      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('does not crash without onClose callback', () => {
      expect(() => {
        render(<AgentChat {...defaultProps} layout="modal" />);
      }).not.toThrow();
    });
  });

  describe('Messaging', () => {
    it('sends message on Enter key', async () => {
      const user = userEvent.setup();
      render(<AgentChat {...defaultProps} />);

      const input = screen.getByTestId('chat-input-field');
      await user.type(input, 'Hello agent{Enter}');

      // Message added to list
      expect(screen.getByText('Hello agent')).toBeInTheDocument();
    });

    it('shows streaming indicator during response', async () => {
      const user = userEvent.setup();
      render(<AgentChat {...defaultProps} />);

      const input = screen.getByTestId('chat-input-field');
      await user.type(input, 'Test{Enter}');

      // During streaming
      expect(screen.getByTestId('streaming-indicator')).toBeInTheDocument();
    });

    it('shows agent response after streaming completes', async () => {
      const user = userEvent.setup();
      render(<AgentChat {...defaultProps} />);

      const input = screen.getByTestId('chat-input-field');
      await user.type(input, 'Test message{Enter}');

      // Wait for streaming to complete (real timers)
      await new Promise(resolve => setTimeout(resolve, 2000));

      expect(screen.getByText('Echo: Test message')).toBeInTheDocument();
    });

    it('shows streaming indicator while processing', async () => {
      const user = userEvent.setup();
      render(<AgentChat {...defaultProps} />);

      const input = screen.getByTestId('chat-input-field');
      await user.type(input, 'Test{Enter}');

      // Streaming indicator shown means input is being processed
      expect(screen.getByTestId('streaming-indicator')).toBeInTheDocument();
    });
  });

  describe('Strategy Badge', () => {
    it('shows RetrievalOnly label', () => {
      render(<AgentChat {...defaultProps} strategy="RetrievalOnly" />);
      expect(screen.getByText('Solo Retrieval')).toBeInTheDocument();
    });

    it('shows SingleModel label', () => {
      render(<AgentChat {...defaultProps} strategy="SingleModel" />);
      expect(screen.getByText('Modello Singolo')).toBeInTheDocument();
    });

    it('shows MultiModelConsensus label', () => {
      render(<AgentChat {...defaultProps} strategy="MultiModelConsensus" />);
      expect(screen.getByText('Consenso Multi-Modello')).toBeInTheDocument();
    });

    it('uses correct color for RetrievalOnly', () => {
      render(<AgentChat {...defaultProps} strategy="RetrievalOnly" />);
      const badge = screen.getByTestId('strategy-badge');
      expect(badge).toHaveClass('bg-blue-100', 'text-blue-800');
    });

    it('uses correct color for SingleModel', () => {
      render(<AgentChat {...defaultProps} strategy="SingleModel" />);
      const badge = screen.getByTestId('strategy-badge');
      expect(badge).toHaveClass('bg-purple-100', 'text-purple-800');
    });
  });

  describe('Accessibility', () => {
    it('has no axe violations in modal layout', async () => {
      const { container } = render(<AgentChat {...defaultProps} layout="modal" />);
      await vi.waitFor(async () => {
        const results = await axe(container);
        expect(results).toHaveNoViolations();
      }, { timeout: 10000 });
    });

    it('has no axe violations in sidebar layout', async () => {
      const { container } = render(<AgentChat {...defaultProps} layout="sidebar" />);
      await vi.waitFor(async () => {
        const results = await axe(container);
        expect(results).toHaveNoViolations();
      }, { timeout: 10000 });
    });

    it('has no axe violations in full-page layout', async () => {
      const { container } = render(<AgentChat {...defaultProps} layout="full-page" />);
      await vi.waitFor(async () => {
        const results = await axe(container);
        expect(results).toHaveNoViolations();
      }, { timeout: 10000 });
    });

    it('has region role with label', () => {
      render(<AgentChat {...defaultProps} />);
      expect(screen.getByRole('region', { name: /Chat with Tutor Agent/i })).toBeInTheDocument();
    });

    it('close button has aria-label', () => {
      const onClose = vi.fn();
      render(<AgentChat {...defaultProps} layout="modal" onClose={onClose} />);
      expect(screen.getByLabelText('Close chat')).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('renders without game name', () => {
      render(<AgentChat agentId="agent-1" agentName="Agent" />);
      expect(screen.queryByText('Catan')).not.toBeInTheDocument();
    });

    it('renders without strategy when not provided', () => {
      // Don't pass strategy prop at all
      render(<AgentChat agentId="agent-1" agentName="Agent" />);
      // Badge should show default RetrievalOnly
      expect(screen.getByTestId('strategy-badge')).toBeInTheDocument();
      expect(screen.getByText('Solo Retrieval')).toBeInTheDocument();
    });

    it('uses default agent name when not provided', () => {
      render(<AgentChat agentId="agent-1" />);
      expect(screen.getByText('AI Assistant')).toBeInTheDocument();
    });

    it('defaults to full-page layout', () => {
      render(<AgentChat agentId="agent-1" />);
      const chat = screen.getByTestId('agent-chat');
      expect(chat).toHaveAttribute('data-layout', 'full-page');
    });
  });
});
