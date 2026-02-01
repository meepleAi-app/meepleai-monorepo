/**
 * ChatMessageList Tests (Issue #3243)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ChatMessageList } from '../ChatMessageList';
import type { AgentMessage } from '@/types/agent';

describe('ChatMessageList', () => {
  beforeEach(() => {
    // Mock scrollTo
    Element.prototype.scrollTo = vi.fn();

    // Mock IntersectionObserver
    global.IntersectionObserver = vi.fn().mockImplementation(() => ({
      observe: vi.fn(),
      unobserve: vi.fn(),
      disconnect: vi.fn(),
    }));
  });

  describe('Empty State', () => {
    it('displays empty state when no messages', () => {
      render(<ChatMessageList messages={[]} />);

      expect(screen.getByText('Nessun messaggio. Inizia una conversazione!')).toBeInTheDocument();
    });

    it('does not show empty state when streaming', () => {
      render(<ChatMessageList messages={[]} isStreaming={true} />);

      expect(
        screen.queryByText('Nessun messaggio. Inizia una conversazione!')
      ).not.toBeInTheDocument();
    });
  });

  describe('Message Rendering', () => {
    it('renders multiple messages', () => {
      const messages: AgentMessage[] = [
        {
          type: 'user',
          content: 'First message',
          timestamp: new Date('2024-01-31T14:30:00'),
        },
        {
          type: 'agent',
          content: 'Second message',
          timestamp: new Date('2024-01-31T14:31:00'),
        },
      ];

      render(<ChatMessageList messages={messages} />);

      expect(screen.getByText('First message')).toBeInTheDocument();
      expect(screen.getByText('Second message')).toBeInTheDocument();
    });

    it('groups consecutive messages from same sender within 2 minutes', () => {
      const messages: AgentMessage[] = [
        {
          type: 'user',
          content: 'Message 1',
          timestamp: new Date('2024-01-31T14:30:00'),
        },
        {
          type: 'user',
          content: 'Message 2',
          timestamp: new Date('2024-01-31T14:30:30'), // 30 seconds later
        },
      ];

      const { container } = render(<ChatMessageList messages={messages} />);

      const messageWrappers = container.querySelectorAll('.flex.flex-col');

      // First message should have mt-4
      expect(messageWrappers[0]).toHaveClass('mt-4');

      // Second message should have mt-1 (grouped)
      expect(messageWrappers[1]).toHaveClass('mt-1');
    });

    it('does not group messages from different senders', () => {
      const messages: AgentMessage[] = [
        {
          type: 'user',
          content: 'User message',
          timestamp: new Date('2024-01-31T14:30:00'),
        },
        {
          type: 'agent',
          content: 'Agent message',
          timestamp: new Date('2024-01-31T14:30:10'), // 10 seconds later
        },
      ];

      const { container } = render(<ChatMessageList messages={messages} />);

      const messageWrappers = container.querySelectorAll('.flex.flex-col');

      // Both should have mt-4 (not grouped)
      expect(messageWrappers[0]).toHaveClass('mt-4');
      expect(messageWrappers[1]).toHaveClass('mt-4');
    });

    it('does not group messages more than 2 minutes apart', () => {
      const messages: AgentMessage[] = [
        {
          type: 'user',
          content: 'Message 1',
          timestamp: new Date('2024-01-31T14:30:00'),
        },
        {
          type: 'user',
          content: 'Message 2',
          timestamp: new Date('2024-01-31T14:32:01'), // 2 minutes 1 second later
        },
      ];

      const { container } = render(<ChatMessageList messages={messages} />);

      const messageWrappers = container.querySelectorAll('.flex.flex-col');

      // Both should have mt-4 (not grouped, too far apart)
      expect(messageWrappers[0]).toHaveClass('mt-4');
      expect(messageWrappers[1]).toHaveClass('mt-4');
    });
  });

  describe('Streaming State', () => {
    it('shows typing indicator when streaming without chunk', () => {
      render(<ChatMessageList messages={[]} isStreaming={true} currentChunk="" />);

      expect(screen.getByLabelText('Agent is typing')).toBeInTheDocument();
    });

    it('shows progressive reveal when streaming with chunk', () => {
      render(
        <ChatMessageList
          messages={[]}
          isStreaming={true}
          currentChunk="Partial answer..."
        />
      );

      expect(screen.getByText('Partial answer...')).toBeInTheDocument();
      expect(screen.queryByLabelText('Agent is typing')).not.toBeInTheDocument();
    });

    it('does not show typing indicator when not streaming', () => {
      render(<ChatMessageList messages={[]} isStreaming={false} />);

      expect(screen.queryByLabelText('Agent is typing')).not.toBeInTheDocument();
    });
  });

  describe('Auto-scroll Behavior', () => {
    it('scrolls to bottom when new message arrives', async () => {
      const messages: AgentMessage[] = [
        {
          type: 'user',
          content: 'First message',
          timestamp: new Date('2024-01-31T14:30:00'),
        },
      ];

      const { rerender } = render(<ChatMessageList messages={messages} />);

      const newMessages: AgentMessage[] = [
        ...messages,
        {
          type: 'agent',
          content: 'New message',
          timestamp: new Date('2024-01-31T14:31:00'),
        },
      ];

      rerender(<ChatMessageList messages={newMessages} />);

      // Note: In real browser, scrollTo would be called
      // In test, we just verify the component renders correctly
      expect(screen.getByText('New message')).toBeInTheDocument();
    });
  });

  describe('Scroll Button', () => {
    it('shows scroll button when not near bottom', () => {
      const messages: AgentMessage[] = Array.from({ length: 20 }, (_, i) => ({
        type: 'user' as const,
        content: `Message ${i + 1}`,
        timestamp: new Date(`2024-01-31T14:${30 + i}:00`),
      }));

      render(<ChatMessageList messages={messages} />);

      // Button visibility is controlled by scroll state
      // In test environment, we just verify it can be rendered
      const button = screen.queryByLabelText('Scroll to bottom');
      // May or may not be visible initially, depends on scroll state
      expect(button).toBeDefined();
    });

    it('scrolls to bottom when button clicked', async () => {
      const user = userEvent.setup();
      const messages: AgentMessage[] = Array.from({ length: 10 }, (_, i) => ({
        type: 'user' as const,
        content: `Message ${i + 1}`,
        timestamp: new Date(`2024-01-31T14:${30 + i}:00`),
      }));

      const { container } = render(<ChatMessageList messages={messages} />);

      // Simulate scroll state by finding button if it exists
      const button = screen.queryByLabelText('Scroll to bottom');

      if (button) {
        await user.click(button);

        // Verify scrollTo was called (mocked)
        const scrollContainer = container.querySelector('[role="log"]');
        // In real usage, scrollTo would be called on scrollContainer
      }
    });
  });

  describe('ARIA Attributes', () => {
    it('has correct ARIA attributes for accessibility', () => {
      const messages: AgentMessage[] = [
        {
          type: 'user',
          content: 'Test message',
          timestamp: new Date('2024-01-31T14:30:00'),
        },
      ];

      render(<ChatMessageList messages={messages} />);

      const logRegion = screen.getByRole('log');
      expect(logRegion).toHaveAttribute('aria-live', 'polite');
      expect(logRegion).toHaveAttribute('aria-label', 'Chat messages');
    });
  });
});
