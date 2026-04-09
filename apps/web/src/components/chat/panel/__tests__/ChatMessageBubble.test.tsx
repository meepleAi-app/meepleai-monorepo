import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ChatMessageBubble } from '../ChatMessageBubble';

describe('ChatMessageBubble', () => {
  it('renders user message with initials', () => {
    render(
      <ChatMessageBubble
        role="user"
        content="Come si vince?"
        authorName="Marco Rossi"
        timestamp="14:32"
      />
    );
    expect(screen.getByText('Come si vince?')).toBeInTheDocument();
    expect(screen.getByText('MR')).toBeInTheDocument();
  });

  it('renders assistant message with dice avatar', () => {
    render(
      <ChatMessageBubble
        role="assistant"
        content="Per vincere devi…"
        authorName="MeepleAI"
        timestamp="14:33"
      />
    );
    expect(screen.getByText(/Per vincere devi/i)).toBeInTheDocument();
    expect(screen.getByText('🎲')).toBeInTheDocument();
  });

  it('applies data-role attribute', () => {
    render(<ChatMessageBubble role="assistant" content="hi" authorName="AI" timestamp="00:00" />);
    const bubble = screen.getByTestId('chat-message-bubble');
    expect(bubble).toHaveAttribute('data-role', 'assistant');
  });
});
