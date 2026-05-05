import { describe, it, expect } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { LibraryDesktop } from './LibraryDesktop';

const items = [
  {
    id: 'g1',
    title: 'Catan',
    publisher: 'Kosmos',
    owned: true,
    wishlist: false,
    description: 'Classico',
    sessionCount: 0,
    chatCount: 0,
  },
  {
    id: 'g2',
    title: 'Root',
    publisher: 'Leder',
    owned: false,
    wishlist: true,
    sessionCount: 0,
    chatCount: 0,
  },
];

describe('LibraryDesktop', () => {
  it('renders list on the left with all items', () => {
    render(<LibraryDesktop items={items} />);
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Root')).toBeInTheDocument();
  });

  it('shows empty detail state initially', () => {
    render(<LibraryDesktop items={items} />);
    expect(screen.getByText(/seleziona un gioco/i)).toBeInTheDocument();
  });

  it('shows game detail when item clicked', () => {
    render(<LibraryDesktop items={items} />);
    fireEvent.click(screen.getByRole('button', { name: /catan/i }));
    expect(screen.getByText('Classico')).toBeInTheDocument();
  });
});
