import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { PersonalLibrarySection } from '../sections/PersonalLibrarySection';

describe('PersonalLibrarySection', () => {
  const games = [
    {
      id: 'g1',
      title: 'Ark Nova',
      subtitle: 'Feuerland',
      rating: 8.6,
      imageUrl: 'https://example.com/arknova.jpg',
    },
    {
      id: 'g2',
      title: 'Wingspan',
      subtitle: 'Stonemaier',
      rating: 8.1,
    },
  ];

  it('renders title with total count', () => {
    render(<PersonalLibrarySection games={games} totalCount={47} onAddGame={() => {}} />);
    expect(screen.getByText(/Libreria personale/i)).toBeInTheDocument();
    expect(screen.getByText('47')).toBeInTheDocument();
  });

  it('renders all provided games', () => {
    render(<PersonalLibrarySection games={games} totalCount={47} onAddGame={() => {}} />);
    expect(screen.getByText('Ark Nova')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('renders the add-new-game ghost card', () => {
    render(<PersonalLibrarySection games={games} totalCount={47} onAddGame={() => {}} />);
    expect(screen.getByRole('button', { name: /aggiungi gioco/i })).toBeInTheDocument();
  });

  it('calls onAddGame when ghost card is clicked', async () => {
    const onAddGame = vi.fn();
    const user = userEvent.setup();
    render(<PersonalLibrarySection games={games} totalCount={47} onAddGame={onAddGame} />);
    await user.click(screen.getByRole('button', { name: /aggiungi gioco/i }));
    expect(onAddGame).toHaveBeenCalled();
  });

  it('renders the add card even when no games', () => {
    render(<PersonalLibrarySection games={[]} totalCount={0} onAddGame={() => {}} />);
    expect(screen.getByRole('button', { name: /aggiungi gioco/i })).toBeInTheDocument();
  });
});
