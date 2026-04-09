import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ChatRecentCards } from '../sections/ChatRecentCards';

describe('ChatRecentCards', () => {
  const chats = [
    {
      id: 'c1',
      gameName: 'Azul',
      topic: 'Regola turno finale',
      snippet: 'Il turno finale inizia quando un giocatore completa una riga orizzontale.',
      confidence: 0.94,
      timestamp: 'Oggi, 14:32',
    },
    {
      id: 'c2',
      gameName: 'Wingspan',
      topic: 'Carte bonus a fine partita',
      snippet: 'Le carte bonus vengono rivelate solo alla fine della partita.',
      confidence: 0.98,
      timestamp: 'Ieri, 21:15',
    },
  ];

  it('renders the section header', () => {
    render(<ChatRecentCards chats={chats} />);
    expect(screen.getByText(/Chat recenti/i)).toBeInTheDocument();
  });

  it('renders all chat cards with titles and snippets', () => {
    render(<ChatRecentCards chats={chats} />);
    expect(screen.getByText('Azul · Regola turno finale')).toBeInTheDocument();
    expect(screen.getByText('Wingspan · Carte bonus a fine partita')).toBeInTheDocument();
    expect(screen.getAllByText(/accurata/i)).toHaveLength(2);
  });

  it('renders nothing when no chats', () => {
    const { container } = render(<ChatRecentCards chats={[]} />);
    expect(container).toBeEmptyDOMElement();
  });
});
