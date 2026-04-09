import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { LibraryHeader } from '../sections/LibraryHeader';

describe('LibraryHeader', () => {
  const stats = {
    owned: 47,
    catalog: 230,
    wishlist: 8,
  };

  it('renders the title and subtitle', () => {
    render(<LibraryHeader stats={stats} />);
    expect(screen.getByText(/La tua libreria/i)).toBeInTheDocument();
    expect(screen.getByText(/Personal collection/i)).toBeInTheDocument();
  });

  it('renders all 3 stat values', () => {
    render(<LibraryHeader stats={stats} />);
    expect(screen.getByText('47')).toBeInTheDocument();
    expect(screen.getByText('230')).toBeInTheDocument();
    expect(screen.getByText('8')).toBeInTheDocument();
  });

  it('renders all 3 stat labels', () => {
    render(<LibraryHeader stats={stats} />);
    expect(screen.getByText(/Tuoi giochi/i)).toBeInTheDocument();
    expect(screen.getByText(/Catalogo/i)).toBeInTheDocument();
    expect(screen.getByText(/Wishlist/i)).toBeInTheDocument();
  });
});
