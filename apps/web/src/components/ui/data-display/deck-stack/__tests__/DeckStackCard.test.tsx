import { render, screen, fireEvent } from '@testing-library/react';
import { DeckStackCard } from '../DeckStackCard';

describe('DeckStackCard', () => {
  it('renders mana symbol, title, and optional status', () => {
    render(
      <DeckStackCard
        item={{ id: '1', entityType: 'game', title: 'Terraforming Mars', status: 'Owned' }}
        index={0}
        onClick={vi.fn()}
      />
    );
    expect(screen.getByText('Terraforming Mars')).toBeInTheDocument();
    expect(screen.getByText('Owned')).toBeInTheDocument();
    expect(screen.getByTestId('mana-symbol-game')).toBeInTheDocument();
  });

  it('renders without status when not provided', () => {
    render(
      <DeckStackCard
        item={{ id: '1', entityType: 'session', title: 'Session 1' }}
        index={0}
        onClick={vi.fn()}
      />
    );
    expect(screen.getByText('Session 1')).toBeInTheDocument();
    expect(screen.queryByTestId('deck-stack-card-status')).not.toBeInTheDocument();
  });

  it('applies rotation based on index', () => {
    const { container } = render(
      <DeckStackCard
        item={{ id: '1', entityType: 'session', title: 'S' }}
        index={2}
        onClick={vi.fn()}
      />
    );
    const card = container.firstChild as HTMLElement;
    expect(card.style.transform).toContain('rotate');
  });

  it('calls onClick with item id and entityType', () => {
    const onClick = vi.fn();
    render(
      <DeckStackCard
        item={{ id: 'abc', entityType: 'game', title: 'Test' }}
        index={0}
        onClick={onClick}
      />
    );
    fireEvent.click(screen.getByText('Test'));
    expect(onClick).toHaveBeenCalledWith('abc', 'game');
  });
});
