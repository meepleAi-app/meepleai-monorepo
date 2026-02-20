import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { GameCatalogGrid } from '../game-catalog-grid';

describe('GameCatalogGrid', () => {
  it('renders stats summary', () => {
    render(<GameCatalogGrid />);

    expect(screen.getByText('Total Games')).toBeInTheDocument();
    expect(screen.getByText('Published')).toBeInTheDocument();
    expect(screen.getByText('Draft')).toBeInTheDocument();
  });

  it('displays game cards in grid', () => {
    render(<GameCatalogGrid />);

    // Check for some game titles
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('shows status badges on cards', () => {
    const { container } = render(<GameCatalogGrid />);

    // Check for status badges
    const publishedBadges = screen.getAllByText('Published');
    expect(publishedBadges.length).toBeGreaterThan(0);
  });

  it('calculates stats correctly', () => {
    render(<GameCatalogGrid />);

    // Should show correct totals from mock data (8 games total)
    expect(screen.getByText('8')).toBeInTheDocument();
  });
});
