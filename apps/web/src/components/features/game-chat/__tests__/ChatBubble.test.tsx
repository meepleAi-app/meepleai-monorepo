import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { ChatBubble } from '../ChatBubble';

describe('ChatBubble', () => {
  it('renders user role with content (no header)', () => {
    render(<ChatBubble role="user" content="ciao" />);
    expect(screen.getByText('ciao')).toBeInTheDocument();
    expect(screen.queryByText(/Tutor/i)).not.toBeInTheDocument();
  });

  it('renders agent role with header (avatar + name)', () => {
    render(<ChatBubble role="agent" content="risposta" agentName="Tutor Wingspan" avatar="🧙" />);
    expect(screen.getByText('Tutor Wingspan')).toBeInTheDocument();
    expect(screen.getByText('🧙')).toBeInTheDocument();
  });

  it('renders children slot inside the bubble (for agent extras)', () => {
    render(
      <ChatBubble role="agent" content="risposta" agentName="Tutor">
        <div data-testid="extras">extras here</div>
      </ChatBubble>
    );
    expect(screen.getByTestId('extras')).toBeInTheDocument();
  });

  it('content can be a ReactNode (e.g. paragraphs)', () => {
    render(
      <ChatBubble
        role="agent"
        agentName="X"
        content={<><p>p1</p><p>p2</p></>}
      />
    );
    expect(screen.getByText('p1')).toBeInTheDocument();
    expect(screen.getByText('p2')).toBeInTheDocument();
  });

  it('exposes role via data attribute', () => {
    render(<ChatBubble role="user" content="x" />);
    expect(screen.getByTestId('chat-bubble')).toHaveAttribute('data-role', 'user');
  });

  it('applies historical visual variant when isHistorical=true', () => {
    render(<ChatBubble role="agent" content="old" agentName="Tutor" isHistorical />);
    const bubble = screen.getByTestId('chat-bubble');
    expect(bubble).toHaveAttribute('data-historical', 'true');
  });

  it('does not apply historical variant by default', () => {
    render(<ChatBubble role="agent" content="new" agentName="Tutor" />);
    const bubble = screen.getByTestId('chat-bubble');
    expect(bubble).not.toHaveAttribute('data-historical');
  });
});
