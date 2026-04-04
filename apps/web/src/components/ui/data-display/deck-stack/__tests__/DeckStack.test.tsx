import { render, screen, fireEvent } from '@testing-library/react';
import { DeckStack } from '../DeckStack';

describe('DeckStack', () => {
  const items = [
    { id: '1', entityType: 'game' as const, title: 'Catan' },
    { id: '2', entityType: 'game' as const, title: 'Wingspan' },
    { id: '3', entityType: 'game' as const, title: 'Spirit Island' },
  ];

  it('renders items when open', () => {
    render(<DeckStack isOpen items={items} onItemClick={vi.fn()} onClose={vi.fn()} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Spirit Island')).toBeInTheDocument();
  });

  it('renders nothing when closed', () => {
    render(<DeckStack isOpen={false} items={items} onItemClick={vi.fn()} onClose={vi.fn()} />);
    expect(screen.queryByText('Catan')).not.toBeInTheDocument();
  });

  it('shows "View all N" when more than 5 items', () => {
    const manyItems = Array.from({ length: 8 }, (_, i) => ({
      id: String(i),
      entityType: 'game' as const,
      title: `Game ${i}`,
    }));
    render(<DeckStack isOpen items={manyItems} onItemClick={vi.fn()} onClose={vi.fn()} />);
    expect(screen.getByText(/View all 8/)).toBeInTheDocument();
    // Only first 5 should be visible as cards
    expect(screen.getByText('Game 0')).toBeInTheDocument();
    expect(screen.getByText('Game 4')).toBeInTheDocument();
    expect(screen.queryByText('Game 5')).not.toBeInTheDocument();
  });

  it('calls onClose when clicking backdrop', () => {
    const onClose = vi.fn();
    render(<DeckStack isOpen items={items} onItemClick={vi.fn()} onClose={onClose} />);
    fireEvent.click(screen.getByTestId('deck-stack-backdrop'));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('calls onClose on Escape key', () => {
    const onClose = vi.fn();
    render(<DeckStack isOpen items={items} onItemClick={vi.fn()} onClose={onClose} />);
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('calls onItemClick when a card is clicked', () => {
    const onItemClick = vi.fn();
    render(<DeckStack isOpen items={items} onItemClick={onItemClick} onClose={vi.fn()} />);
    fireEvent.click(screen.getByText('Catan'));
    expect(onItemClick).toHaveBeenCalledWith('1', 'game');
  });

  it('renders nothing when items array is empty', () => {
    const { container } = render(
      <DeckStack isOpen items={[]} onItemClick={vi.fn()} onClose={vi.fn()} />
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('does not add keydown listener when closed', () => {
    const onClose = vi.fn();
    render(<DeckStack isOpen={false} items={items} onItemClick={vi.fn()} onClose={onClose} />);
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).not.toHaveBeenCalled();
  });
});
