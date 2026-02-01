/**
 * ChatMessage Tests (Issue #3243)
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { ChatMessage } from '../ChatMessage';
import type { AgentMessage } from '@/types/agent';

describe('ChatMessage', () => {
  beforeEach(() => {
    // Mock clipboard API
    Object.defineProperty(navigator, 'clipboard', {
      value: {
        writeText: vi.fn().mockResolvedValue(undefined),
      },
      writable: true,
      configurable: true,
    });
  });

  describe('User Messages', () => {
    it('renders user message with correct styling', () => {
      const message: AgentMessage = {
        type: 'user',
        content: 'Hello, agent!',
        timestamp: new Date('2024-01-31T14:30:00'),
      };

      render(<ChatMessage message={message} />);

      const messageEl = screen.getByLabelText('Your message');
      expect(messageEl).toBeInTheDocument();
      expect(messageEl).toHaveClass('items-end'); // Right-aligned

      // Check the message bubble (second div, not the text wrapper)
      const bubble = messageEl.querySelector('.bg-cyan-50');
      expect(bubble).toBeInTheDocument();
    });

    it('displays user message header when not grouped', () => {
      const message: AgentMessage = {
        type: 'user',
        content: 'Test message',
        timestamp: new Date('2024-01-31T14:30:00'),
      };

      render(<ChatMessage message={message} isGrouped={false} />);

      expect(screen.getByText('Tu')).toBeInTheDocument();
      expect(screen.getByText(/fa$/i)).toBeInTheDocument(); // Relative time
    });

    it('hides user message header when grouped', () => {
      const message: AgentMessage = {
        type: 'user',
        content: 'Test message',
        timestamp: new Date('2024-01-31T14:30:00'),
      };

      render(<ChatMessage message={message} isGrouped={true} />);

      expect(screen.queryByText('Tu')).not.toBeInTheDocument();
    });
  });

  describe('Agent Messages', () => {
    it('renders agent message with correct styling', () => {
      const message: AgentMessage = {
        type: 'agent',
        content: 'I can help you with that.',
        timestamp: new Date('2024-01-31T14:30:00'),
      };

      render(<ChatMessage message={message} />);

      const messageEl = screen.getByLabelText('Agent response');
      expect(messageEl).toBeInTheDocument();
      expect(messageEl).toHaveClass('items-start'); // Left-aligned

      // Check the message bubble
      const bubble = messageEl.querySelector('.bg-gray-800');
      expect(bubble).toBeInTheDocument();
    });

    it('renders markdown content for agent messages', () => {
      const message: AgentMessage = {
        type: 'agent',
        content: '**Bold text** and `code`',
        timestamp: new Date('2024-01-31T14:30:00'),
      };

      const { container } = render(<ChatMessage message={message} />);

      const strong = container.querySelector('strong');
      const code = container.querySelector('code');

      expect(strong).toBeInTheDocument();
      expect(code).toBeInTheDocument();
    });

    it('displays citations when provided', () => {
      const message: AgentMessage = {
        type: 'agent',
        content: 'According to the rulebook...',
        timestamp: new Date('2024-01-31T14:30:00'),
        citations: [
          {
            source: 'Rulebook',
            pageNumber: 5,
            score: 0.95,
            snippet: 'Setup instructions...',
          },
        ],
      };

      render(<ChatMessage message={message} />);

      expect(screen.getByText('Fonti:')).toBeInTheDocument();
      expect(screen.getByText('Rulebook')).toBeInTheDocument();
      expect(screen.getByText('p.5')).toBeInTheDocument();
      expect(screen.getByText('(95%)')).toBeInTheDocument();
    });

    it('does not display citations section when no citations', () => {
      const message: AgentMessage = {
        type: 'agent',
        content: 'No citations here.',
        timestamp: new Date('2024-01-31T14:30:00'),
      };

      render(<ChatMessage message={message} />);

      expect(screen.queryByText('Fonti:')).not.toBeInTheDocument();
    });
  });

  describe('System Messages', () => {
    it('renders system message with center alignment', () => {
      const message: AgentMessage = {
        type: 'system',
        content: 'Connection established',
        timestamp: new Date('2024-01-31T14:30:00'),
      };

      const { container } = render(<ChatMessage message={message} />);

      const wrapper = container.querySelector('.flex.justify-center');
      expect(wrapper).toBeInTheDocument();
      expect(screen.getByText('Connection established')).toBeInTheDocument();
    });
  });

  describe('Copy Functionality', () => {
    it('copies message content to clipboard on button click', async () => {
      const user = userEvent.setup();
      const writeTextSpy = vi.fn().mockResolvedValue(undefined);

      Object.defineProperty(navigator, 'clipboard', {
        value: { writeText: writeTextSpy },
        writable: true,
        configurable: true,
      });

      const message: AgentMessage = {
        type: 'user',
        content: 'Copy this message',
        timestamp: new Date('2024-01-31T14:30:00'),
      };

      render(<ChatMessage message={message} />);

      const copyButton = screen.getByLabelText('Copy message');
      await user.click(copyButton);

      expect(writeTextSpy).toHaveBeenCalledWith('Copy this message');
    });

    it('shows checkmark temporarily after successful copy', async () => {
      const user = userEvent.setup();
      const message: AgentMessage = {
        type: 'user',
        content: 'Test',
        timestamp: new Date('2024-01-31T14:30:00'),
      };

      render(<ChatMessage message={message} />);

      const copyButton = screen.getByLabelText('Copy message');
      await user.click(copyButton);

      // Check icon should appear briefly
      await waitFor(() => {
        const svg = copyButton.querySelector('svg');
        expect(svg).toBeInTheDocument();
      });
    });

    it('handles clipboard error gracefully', async () => {
      const user = userEvent.setup();
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

      // Replace clipboard mock with error version
      Object.defineProperty(navigator, 'clipboard', {
        value: {
          writeText: vi.fn().mockRejectedValueOnce(new Error('Clipboard error')),
        },
        writable: true,
        configurable: true,
      });

      const message: AgentMessage = {
        type: 'user',
        content: 'Test',
        timestamp: new Date('2024-01-31T14:30:00'),
      };

      render(<ChatMessage message={message} />);

      const copyButton = screen.getByLabelText('Copy message');
      await user.click(copyButton);

      expect(consoleSpy).toHaveBeenCalledWith(
        'Failed to copy message:',
        expect.any(Error)
      );

      consoleSpy.mockRestore();
    });
  });

  describe('Message Grouping', () => {
    it('applies reduced margin when grouped', () => {
      const message: AgentMessage = {
        type: 'user',
        content: 'Grouped message',
        timestamp: new Date('2024-01-31T14:30:00'),
      };

      const { container, rerender } = render(<ChatMessage message={message} isGrouped={false} />);

      let wrapper = container.querySelector('.flex.flex-col');
      expect(wrapper).toHaveClass('mt-4');

      rerender(<ChatMessage message={message} isGrouped={true} />);

      wrapper = container.querySelector('.flex.flex-col');
      expect(wrapper).toHaveClass('mt-1');
    });
  });
});
