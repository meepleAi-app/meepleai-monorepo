import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { ChatMainArea } from '../ChatMainArea';

describe('ChatMainArea', () => {
  it('renders empty state with welcome when no messages', () => {
    render(
      <ChatMainArea
        messages={[]}
        gameName="Azul"
        suggestedQuestions={['Come si vince?', 'Regole base']}
        onSend={() => {}}
      />
    );
    expect(screen.getByText(/Ciao! Sono il tuo assistente AI/i)).toBeInTheDocument();
    expect(screen.getByText('Come si vince?')).toBeInTheDocument();
    expect(screen.getByText('Regole base')).toBeInTheDocument();
  });

  it('renders messages list when messages present', () => {
    const messages = [
      {
        id: 'm1',
        role: 'user' as const,
        content: 'Come si vince?',
        authorName: 'Marco',
        timestamp: '14:32',
      },
      {
        id: 'm2',
        role: 'assistant' as const,
        content: 'Per vincere devi fare X.',
        authorName: 'MeepleAI',
        timestamp: '14:33',
      },
    ];
    render(
      <ChatMainArea messages={messages} gameName="Azul" suggestedQuestions={[]} onSend={() => {}} />
    );
    expect(screen.getByText('Come si vince?')).toBeInTheDocument();
    expect(screen.getByText(/Per vincere devi fare X/i)).toBeInTheDocument();
  });

  it('calls onSend when message is sent via input', async () => {
    const onSend = vi.fn();
    const user = userEvent.setup();
    render(<ChatMainArea messages={[]} gameName="Azul" suggestedQuestions={[]} onSend={onSend} />);
    await user.type(screen.getByRole('textbox'), 'hello{Enter}');
    expect(onSend).toHaveBeenCalledWith('hello');
  });

  it('calls onSend when a suggested question is clicked', async () => {
    const onSend = vi.fn();
    const user = userEvent.setup();
    render(
      <ChatMainArea
        messages={[]}
        gameName="Azul"
        suggestedQuestions={['Come si vince?']}
        onSend={onSend}
      />
    );
    await user.click(screen.getByRole('button', { name: 'Come si vince?' }));
    expect(onSend).toHaveBeenCalledWith('Come si vince?');
  });
});
