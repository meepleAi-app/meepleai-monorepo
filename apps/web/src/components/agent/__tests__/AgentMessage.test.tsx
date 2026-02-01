/**
 * AgentMessage Component Tests (Issue #3026)
 *
 * Test Coverage:
 * - User message rendering
 * - Agent message rendering with markdown
 * - System message rendering
 * - Citations display
 * - Copy functionality
 * - Timestamp formatting
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

import { AgentMessage } from '../AgentMessage';
import type { AgentMessage as AgentMessageType } from '@/types/agent';
import { createMockCitation } from '@/__tests__/fixtures/common-fixtures';

// Mock clipboard API
const mockWriteText = vi.fn();
vi.stubGlobal('navigator', {
  clipboard: {
    writeText: mockWriteText,
  },
});

describe('AgentMessage', () => {
  const baseTimestamp = new Date('2024-01-15T10:30:00Z');

  const createUserMessage = (overrides?: Partial<AgentMessageType>): AgentMessageType => ({
    type: 'user',
    content: 'Hello, how can I play this game?',
    timestamp: baseTimestamp,
    ...overrides,
  });

  const createAgentMessage = (overrides?: Partial<AgentMessageType>): AgentMessageType => ({
    type: 'agent',
    content: 'Here are the game rules...',
    timestamp: baseTimestamp,
    ...overrides,
  });

  const createSystemMessage = (overrides?: Partial<AgentMessageType>): AgentMessageType => ({
    type: 'system',
    content: 'Chat session started',
    timestamp: baseTimestamp,
    ...overrides,
  });

  beforeEach(() => {
    vi.clearAllMocks();
    mockWriteText.mockResolvedValue(undefined);
  });

  describe('User Message', () => {
    it('renders user message with correct label', () => {
      render(<AgentMessage message={createUserMessage()} />);

      expect(screen.getByLabelText('Your message')).toBeInTheDocument();
    });

    it('displays user message content', () => {
      render(<AgentMessage message={createUserMessage()} />);

      expect(screen.getByText('Hello, how can I play this game?')).toBeInTheDocument();
    });

    it('shows "Tu" as the sender', () => {
      render(<AgentMessage message={createUserMessage()} />);

      expect(screen.getByText('Tu')).toBeInTheDocument();
    });

    it('aligns user message to the right', () => {
      render(<AgentMessage message={createUserMessage()} />);

      const container = screen.getByLabelText('Your message');
      expect(container).toHaveClass('items-end');
    });

    it('applies user message styling', () => {
      const { container } = render(<AgentMessage message={createUserMessage()} />);

      const bubble = container.querySelector('.bg-\\[\\#e3f2fd\\]');
      expect(bubble).toBeInTheDocument();
    });
  });

  describe('Agent Message', () => {
    it('renders agent message with correct label', () => {
      render(<AgentMessage message={createAgentMessage()} />);

      expect(screen.getByLabelText('Agent response')).toBeInTheDocument();
    });

    it('displays agent message content', () => {
      render(<AgentMessage message={createAgentMessage()} />);

      expect(screen.getByText('Here are the game rules...')).toBeInTheDocument();
    });

    it('shows "Agent" as the sender', () => {
      render(<AgentMessage message={createAgentMessage()} />);

      expect(screen.getByText('Agent')).toBeInTheDocument();
    });

    it('aligns agent message to the left', () => {
      render(<AgentMessage message={createAgentMessage()} />);

      const container = screen.getByLabelText('Agent response');
      expect(container).toHaveClass('items-start');
    });

    it('applies agent message styling', () => {
      const { container } = render(<AgentMessage message={createAgentMessage()} />);

      const bubble = container.querySelector('.bg-\\[\\#f1f3f4\\]');
      expect(bubble).toBeInTheDocument();
    });

    it('renders markdown content for agent messages', () => {
      const message = createAgentMessage({
        content: '**Bold text** and *italic text*',
      });

      render(<AgentMessage message={message} />);

      // ReactMarkdown should render the bold and italic elements
      expect(screen.getByText('Bold text')).toBeInTheDocument();
      expect(screen.getByText('and')).toBeInTheDocument();
      expect(screen.getByText('italic text')).toBeInTheDocument();
    });
  });

  describe('System Message', () => {
    it('renders system message centered', () => {
      const { container } = render(<AgentMessage message={createSystemMessage()} />);

      const wrapper = container.querySelector('.justify-center');
      expect(wrapper).toBeInTheDocument();
    });

    it('displays system message content', () => {
      render(<AgentMessage message={createSystemMessage()} />);

      expect(screen.getByText('Chat session started')).toBeInTheDocument();
    });

    it('does not have user/agent labels for system messages', () => {
      render(<AgentMessage message={createSystemMessage()} />);

      expect(screen.queryByText('Tu')).not.toBeInTheDocument();
      expect(screen.queryByText('Agent')).not.toBeInTheDocument();
    });

    it('applies system message styling', () => {
      const { container } = render(<AgentMessage message={createSystemMessage()} />);

      const bubble = container.querySelector('.text-xs.text-\\[\\#5f6368\\]');
      expect(bubble).toBeInTheDocument();
    });
  });

  describe('Citations', () => {
    it('displays citations for agent messages', () => {
      const message = createAgentMessage({
        citations: [
          createMockCitation({ source: 'rules.pdf', pageNumber: 5 }),
          createMockCitation({ source: 'manual.pdf', pageNumber: 10 }),
        ],
      });

      render(<AgentMessage message={message} />);

      expect(screen.getByText('Fonti:')).toBeInTheDocument();
      expect(screen.getByText('rules.pdf')).toBeInTheDocument();
      expect(screen.getByText('manual.pdf')).toBeInTheDocument();
    });

    it('shows page numbers when available', () => {
      const message = createAgentMessage({
        citations: [createMockCitation({ source: 'rules.pdf', pageNumber: 5 })],
      });

      render(<AgentMessage message={message} />);

      expect(screen.getByText('p.5')).toBeInTheDocument();
    });

    it('shows relevance score when available', () => {
      const message = createAgentMessage({
        citations: [createMockCitation({ source: 'rules.pdf', score: 0.85 })],
      });

      render(<AgentMessage message={message} />);

      expect(screen.getByText('(85%)')).toBeInTheDocument();
    });

    it('does not display citations section when empty', () => {
      const message = createAgentMessage({ citations: [] });

      render(<AgentMessage message={message} />);

      expect(screen.queryByText('Fonti:')).not.toBeInTheDocument();
    });

    it('does not display citations for user messages', () => {
      const message = createUserMessage({
        citations: [createMockCitation({ source: 'rules.pdf' })],
      });

      render(<AgentMessage message={message} />);

      expect(screen.queryByText('Fonti:')).not.toBeInTheDocument();
    });

    it('has title attribute with snippet', () => {
      const message = createAgentMessage({
        citations: [
          createMockCitation({
            source: 'rules.pdf',
            snippet: 'This is a relevant snippet from the document',
          }),
        ],
      });

      render(<AgentMessage message={message} />);

      const citationButton = screen.getByRole('button', { name: /rules\.pdf/i });
      expect(citationButton).toHaveAttribute(
        'title',
        'This is a relevant snippet from the document'
      );
    });
  });

  describe('Copy Functionality', () => {
    it('renders copy button', () => {
      render(<AgentMessage message={createUserMessage()} />);

      expect(screen.getByRole('button', { name: /copy message/i })).toBeInTheDocument();
    });

    it('copies message content to clipboard', async () => {
      render(<AgentMessage message={createUserMessage()} />);

      const copyButton = screen.getByRole('button', { name: /copy message/i });
      fireEvent.click(copyButton);

      await waitFor(() => {
        expect(mockWriteText).toHaveBeenCalledWith('Hello, how can I play this game?');
      });
    });

    it('shows check icon after successful copy', async () => {
      render(<AgentMessage message={createUserMessage()} />);

      const copyButton = screen.getByRole('button', { name: /copy message/i });
      fireEvent.click(copyButton);

      await waitFor(() => {
        // The Check icon should be visible (green color class)
        const checkIcon = document.querySelector('.text-green-600');
        expect(checkIcon).toBeInTheDocument();
      });
    });
  });

  describe('Timestamp', () => {
    it('displays formatted timestamp', () => {
      render(<AgentMessage message={createUserMessage()} />);

      // The timestamp should be formatted as HH:MM in Italian locale
      // 10:30:00 UTC should show as formatted time
      const timeElement = document.querySelector('.text-\\[10px\\]');
      expect(timeElement).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has aria-label for user messages', () => {
      render(<AgentMessage message={createUserMessage()} />);

      expect(screen.getByLabelText('Your message')).toBeInTheDocument();
    });

    it('has aria-label for agent messages', () => {
      render(<AgentMessage message={createAgentMessage()} />);

      expect(screen.getByLabelText('Agent response')).toBeInTheDocument();
    });

    it('copy button has accessible label', () => {
      render(<AgentMessage message={createUserMessage()} />);

      expect(screen.getByRole('button', { name: /copy message/i })).toBeInTheDocument();
    });
  });
});
