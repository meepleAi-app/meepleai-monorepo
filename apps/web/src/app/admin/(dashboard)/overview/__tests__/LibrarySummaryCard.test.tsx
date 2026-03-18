import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { LibrarySummaryCard } from '../LibrarySummaryCard';

const baseRecentGames = [
  { id: 'g1', title: 'Catan', createdAt: '2026-03-10T10:00:00Z' },
  { id: 'g2', title: 'Pandemic', createdAt: '2026-03-08T12:00:00Z' },
];

describe('LibrarySummaryCard', () => {
  it('renders header and total games stat', () => {
    render(<LibrarySummaryCard totalGames={123} recentGames={[]} />);

    expect(screen.getByText('Libreria Condivisa')).toBeInTheDocument();
    expect(screen.getByTestId('library-total-games')).toHaveTextContent('123');
    expect(screen.getByText('giochi nel catalogo')).toBeInTheDocument();
  });

  it('renders recent games list with titles and links', () => {
    render(<LibrarySummaryCard totalGames={50} recentGames={baseRecentGames} />);

    const catanLink = screen.getByRole('link', { name: /catan/i });
    expect(catanLink).toBeInTheDocument();
    expect(catanLink).toHaveAttribute('href', '/admin/shared-games/g1');

    const pandemicLink = screen.getByRole('link', { name: /pandemic/i });
    expect(pandemicLink).toBeInTheDocument();
    expect(pandemicLink).toHaveAttribute('href', '/admin/shared-games/g2');
  });

  it('renders manage catalog link with correct href', () => {
    render(<LibrarySummaryCard totalGames={10} recentGames={baseRecentGames} />);

    const link = screen.getByRole('link', { name: /gestisci catalogo/i });
    expect(link).toBeInTheDocument();
    expect(link).toHaveAttribute('href', '/admin/shared-games/all');
  });

  it('renders empty state when recentGames is empty', () => {
    render(<LibrarySummaryCard totalGames={0} recentGames={[]} />);

    expect(screen.getByText('Nessun gioco recente')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /catan/i })).not.toBeInTheDocument();
  });

  it('renders section label for recent games', () => {
    render(<LibrarySummaryCard totalGames={5} recentGames={baseRecentGames} />);

    expect(screen.getByText('Ultimi aggiunti')).toBeInTheDocument();
  });
});
