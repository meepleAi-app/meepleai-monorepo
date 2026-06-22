import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { GameDetailChatTab, type GameDetailChatPreviewMessage } from '../GameDetailChatTab';

const labels = {
  title: 'Chat',
  empty: 'Nessun messaggio',
  openCta: 'Apri chat',
  userPrefix: 'Tu',
  assistantPrefix: 'Agente',
};

const messages: ReadonlyArray<GameDetailChatPreviewMessage> = [
  { id: '1', role: 'user', content: 'Quanto dura una partita?', timestamp: '12:01' },
  { id: '2', role: 'assistant', content: 'Circa 45 minuti.', timestamp: '12:01' },
  { id: '3', role: 'user', content: 'Grazie!', timestamp: '12:02' },
];

describe('GameDetailChatTab (Issue #1471)', () => {
  it('renders title, open CTA with href, and one row per message (capped at maxItems)', () => {
    render(
      <GameDetailChatTab
        messages={messages}
        openHref="/library/games/g1/agent"
        labels={labels}
        maxItems={2}
      />
    );
    expect(screen.getByRole('heading', { name: 'Chat' })).toBeInTheDocument();
    const cta = screen.getByRole('link', { name: 'Apri chat' });
    expect(cta).toHaveAttribute('href', '/library/games/g1/agent');
    expect(screen.getAllByRole('listitem')).toHaveLength(2);
  });

  it('shows empty state when messages is empty', () => {
    render(<GameDetailChatTab messages={[]} openHref="/x" labels={labels} />);
    expect(screen.getByText('Nessun messaggio')).toBeInTheDocument();
    expect(screen.queryAllByRole('listitem')).toHaveLength(0);
  });

  it('marks each message with the role prefix and a data-role attribute', () => {
    const { container } = render(
      <GameDetailChatTab messages={messages} openHref="/x" labels={labels} />
    );
    expect(container.querySelector('[data-role="user"]')).toBeInTheDocument();
    expect(container.querySelector('[data-role="assistant"]')).toBeInTheDocument();
    expect(screen.getAllByText('Tu').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Agente').length).toBeGreaterThan(0);
  });

  it('composes className with base classes and uses DS-15 entity-agent token', () => {
    const { container } = render(
      <GameDetailChatTab messages={messages} openHref="/x" labels={labels} className="extra" />
    );
    const card = container.querySelector('[data-slot="game-detail-chat-tab"]');
    expect(card).toHaveClass('extra');
    expect(card).toHaveClass('bg-card');
    expect(card).toHaveClass('border-border');
    expect(container.querySelector('.text-entity-agent')).toBeInTheDocument();
  });
});
