import { render, screen, fireEvent } from '@testing-library/react';
import { CardStackItem } from '../CardStackItem';
import type { HandCard } from '@/stores/use-card-hand';

const card: HandCard = {
  id: 'g1',
  entity: 'game',
  title: 'Catan',
  href: '/library/g1',
  subtitle: 'Klaus Teuber',
};

describe('CardStackItem', () => {
  it('renders Mini level with icon only', () => {
    render(
      <CardStackItem
        card={card}
        level="mini"
        index={0}
        isFocused={false}
        isPinned={false}
        onFocus={vi.fn()}
        onDiscard={vi.fn()}
      />
    );
    expect(screen.getByTestId('card-stack-item-g1')).toBeInTheDocument();
    // Mini level should NOT show title text
    expect(screen.queryByText('Catan')).not.toBeInTheDocument();
  });

  it('renders Card level with title and subtitle', () => {
    render(
      <CardStackItem
        card={card}
        level="card"
        index={0}
        isFocused={false}
        isPinned={false}
        onFocus={vi.fn()}
        onDiscard={vi.fn()}
      />
    );
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Klaus Teuber')).toBeInTheDocument();
  });

  it('highlights when focused', () => {
    render(
      <CardStackItem
        card={card}
        level="card"
        index={0}
        isFocused={true}
        isPinned={false}
        onFocus={vi.fn()}
        onDiscard={vi.fn()}
      />
    );
    const el = screen.getByTestId('card-stack-item-g1');
    expect(el.dataset.focused).toBe('true');
  });

  it('shows pin indicator when pinned', () => {
    render(
      <CardStackItem
        card={card}
        level="card"
        index={0}
        isFocused={false}
        isPinned={true}
        onFocus={vi.fn()}
        onDiscard={vi.fn()}
      />
    );
    expect(screen.getByLabelText('Pinned')).toBeInTheDocument();
  });

  it('calls onFocus when clicked', () => {
    const onFocus = vi.fn();
    render(
      <CardStackItem
        card={card}
        level="card"
        index={2}
        isFocused={false}
        isPinned={false}
        onFocus={onFocus}
        onDiscard={vi.fn()}
      />
    );
    fireEvent.click(screen.getByTestId('card-stack-item-g1'));
    expect(onFocus).toHaveBeenCalledWith(2);
  });

  it('calls onDiscard when X clicked', () => {
    const onDiscard = vi.fn();
    render(
      <CardStackItem
        card={card}
        level="card"
        index={0}
        isFocused={false}
        isPinned={false}
        onFocus={vi.fn()}
        onDiscard={onDiscard}
      />
    );
    fireEvent.click(screen.getByLabelText('Discard card'));
    expect(onDiscard).toHaveBeenCalledWith('g1');
  });
});
