import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CardDeck } from '@/components/ui/data-display/card-deck';

const mockItems = [
  { id: '1', title: 'Card 1' },
  { id: '2', title: 'Card 2' },
  { id: '3', title: 'Card 3' },
];

describe('CardDeck', () => {
  it('renders the active item', () => {
    render(
      <CardDeck
        items={mockItems}
        activeIndex={0}
        onIndexChange={() => {}}
        renderItem={item => <div>{item.title}</div>}
      />
    );
    expect(screen.getByText('Card 1')).toBeDefined();
  });

  it('shows dot indicators matching item count', () => {
    render(
      <CardDeck
        items={mockItems}
        activeIndex={1}
        onIndexChange={() => {}}
        renderItem={item => <div>{item.title}</div>}
        showIndicators
      />
    );
    const dots = screen.getAllByRole('tab');
    expect(dots).toHaveLength(3);
  });

  it('renders empty state when no items', () => {
    render(
      <CardDeck
        items={[]}
        activeIndex={0}
        onIndexChange={() => {}}
        renderItem={() => <div>Item</div>}
        emptyState={<div>No items</div>}
      />
    );
    expect(screen.getByText('No items')).toBeDefined();
  });

  it('calls onIndexChange when dot is clicked', async () => {
    const onChange = vi.fn();
    render(
      <CardDeck
        items={mockItems}
        activeIndex={0}
        onIndexChange={onChange}
        renderItem={item => <div>{item.title}</div>}
        showIndicators
      />
    );
    const dots = screen.getAllByRole('tab');
    dots[2].click();
    expect(onChange).toHaveBeenCalledWith(2);
  });
});
