import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { CardDeckTool } from '../CardDeckTool';
import { useStandaloneToolkitStore } from '@/lib/stores/standalone-toolkit-store';

const defaultCards = Array.from({ length: 10 }, (_, i) => `Carta ${i + 1}`);

beforeEach(() => {
  useStandaloneToolkitStore.setState({ decks: {} });
});

describe('CardDeckTool', () => {
  it('mostra il numero iniziale di carte', () => {
    render(
      <CardDeckTool deckId="test" name="Mazzo Test" cards={defaultCards} reshuffleOnEmpty={false} />
    );
    expect(screen.getByText('10 carte')).toBeInTheDocument();
  });

  it('aggiorna il contatore dopo draw', () => {
    render(
      <CardDeckTool deckId="test" name="Mazzo Test" cards={defaultCards} reshuffleOnEmpty={false} />
    );
    fireEvent.click(screen.getByRole('button', { name: /pesca/i }));
    expect(screen.getByText('9 carte')).toBeInTheDocument();
  });

  it('mostra la carta pescata', () => {
    render(
      <CardDeckTool deckId="test" name="Mazzo Test" cards={defaultCards} reshuffleOnEmpty={false} />
    );
    fireEvent.click(screen.getByRole('button', { name: /pesca/i }));
    expect(screen.getByTestId('drawn-card')).toBeInTheDocument();
  });

  it('il bottone annulla è visibile subito dopo draw', () => {
    render(
      <CardDeckTool deckId="test" name="Mazzo Test" cards={defaultCards} reshuffleOnEmpty={false} />
    );
    fireEvent.click(screen.getByRole('button', { name: /pesca/i }));
    expect(screen.getByRole('button', { name: /annulla/i })).toBeInTheDocument();
  });

  it('annullare il draw ripristina il contatore', () => {
    render(
      <CardDeckTool deckId="test" name="Mazzo Test" cards={defaultCards} reshuffleOnEmpty={false} />
    );
    fireEvent.click(screen.getByRole('button', { name: /pesca/i }));
    fireEvent.click(screen.getByRole('button', { name: /annulla/i }));
    expect(screen.getByText('10 carte')).toBeInTheDocument();
  });

  it('deck vuoto con reshuffleOnEmpty ricarica le carte', () => {
    const twoCards = ['A', 'B'];
    render(<CardDeckTool deckId="test2" name="Mini" cards={twoCards} reshuffleOnEmpty={true} />);
    fireEvent.click(screen.getByRole('button', { name: /pesca/i }));
    fireEvent.click(screen.getByRole('button', { name: /pesca/i }));
    // 0 cards in draw pile, discard has 2 — next draw reshuffles
    fireEvent.click(screen.getByRole('button', { name: /pesca/i }));
    expect(screen.queryByText(/mazzo esaurito/i)).not.toBeInTheDocument();
  });
});
