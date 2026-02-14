/**
 * ChatBubble Tests (Task #5)
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ChatBubble } from '../ChatBubble';

describe('ChatBubble', () => {
  const baseProps = {
    content: 'Test message',
    timestamp: new Date(),
  };

  it('renders user message with correct styling', () => {
    render(<ChatBubble {...baseProps} type="user" />);

    const bubble = screen.getByTestId('chat-bubble-user');
    expect(bubble).toBeInTheDocument();
    expect(bubble).toHaveClass('flex-row-reverse'); // Right-aligned
  });

  it('renders agent message with avatar', () => {
    render(<ChatBubble {...baseProps} type="agent" agentType="tutor" />);

    const bubble = screen.getByTestId('chat-bubble-agent');
    expect(bubble).toBeInTheDocument();
    expect(screen.getByTitle('Tutor')).toBeInTheDocument();
  });

  it('displays confidence score for agent messages', () => {
    render(<ChatBubble {...baseProps} type="agent" confidence={0.92} />);

    expect(screen.getByText('92%')).toBeInTheDocument();
  });

  it('renders citations when provided', () => {
    const citations = [
      {
        documentId: 'doc-1',
        pageNumber: 5,
        text: 'Rule excerpt',
        confidence: 0.88,
      },
    ];

    render(<ChatBubble {...baseProps} type="agent" citations={citations} />);

    expect(screen.getByText(/Sources:/i)).toBeInTheDocument();
    expect(screen.getByText('Page 5')).toBeInTheDocument();
    expect(screen.getByText('(88% match)')).toBeInTheDocument();
  });

  it('renders system messages with distinct styling', () => {
    render(<ChatBubble {...baseProps} type="system" />);

    const bubble = screen.getByTestId('chat-bubble-system');
    expect(bubble).toBeInTheDocument();
  });

  it('shows relative time by default', () => {
    const oneMinAgo = new Date(Date.now() - 60 * 1000);
    render(<ChatBubble {...baseProps} timestamp={oneMinAgo} type="user" />);

    expect(screen.getByText(/ago/i)).toBeInTheDocument();
  });

  it('renders markdown content', () => {
    const markdownContent = 'This is **bold** and *italic* text with `code`.';
    render(<ChatBubble {...baseProps} type="agent" content={markdownContent} />);

    expect(screen.getByText('bold')).toBeInTheDocument();
    expect(screen.getByText('italic')).toBeInTheDocument();
    expect(screen.getByText('code')).toBeInTheDocument();
  });

  it('applies different colors for different agent types', () => {
    const { rerender } = render(
      <ChatBubble {...baseProps} type="agent" agentType="tutor" />
    );
    expect(screen.getByTitle('Tutor')).toBeInTheDocument();

    rerender(<ChatBubble {...baseProps} type="agent" agentType="arbitro" />);
    expect(screen.getByTitle('Arbitro')).toBeInTheDocument();

    rerender(<ChatBubble {...baseProps} type="agent" agentType="decisore" />);
    expect(screen.getByTitle('Decisore')).toBeInTheDocument();
  });
});
