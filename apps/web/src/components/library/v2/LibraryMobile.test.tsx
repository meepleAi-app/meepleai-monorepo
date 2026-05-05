import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { LibraryMobile, type LibraryGameItem } from './LibraryMobile';

const items: LibraryGameItem[] = [
  { id: 'g1', title: 'Catan', publisher: 'Kosmos', owned: true, wishlist: false },
  {
    id: 'g2',
    title: 'Ticket to Ride',
    publisher: 'Days of Wonder',
    owned: false,
    wishlist: true,
  },
  { id: 'g3', title: 'Carcassonne', publisher: 'HiG', owned: true, wishlist: false },
];

describe('LibraryMobile', () => {
  it('renders all items by default', () => {
    render(<LibraryMobile items={items} onSelect={() => {}} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Ticket to Ride')).toBeInTheDocument();
    expect(screen.getByText('Carcassonne')).toBeInTheDocument();
  });

  it('filters by owned when owned tab clicked', () => {
    render(<LibraryMobile items={items} onSelect={() => {}} />);
    fireEvent.click(screen.getByRole('tab', { name: /possedut/i }));
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.queryByText('Ticket to Ride')).not.toBeInTheDocument();
    expect(screen.getByText('Carcassonne')).toBeInTheDocument();
  });

  it('renders empty state when filter produces no results', () => {
    render(
      <LibraryMobile
        items={[{ id: 'g1', title: 'Catan', publisher: 'Kosmos', owned: true, wishlist: false }]}
        onSelect={() => {}}
      />
    );
    fireEvent.click(screen.getByRole('tab', { name: /wishlist/i }));
    expect(screen.getByText(/nessun gioco/i)).toBeInTheDocument();
  });
});
