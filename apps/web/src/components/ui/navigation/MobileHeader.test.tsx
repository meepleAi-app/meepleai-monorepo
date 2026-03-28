import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { MobileHeader } from './MobileHeader';

describe('MobileHeader', () => {
  it('renders title', () => {
    render(<MobileHeader title="La Mia Libreria" />);
    expect(screen.getByText('La Mia Libreria')).toBeInTheDocument();
  });

  it('renders subtitle when provided', () => {
    render(<MobileHeader title="Libreria" subtitle="12 giochi" />);
    expect(screen.getByText('12 giochi')).toBeInTheDocument();
  });

  it('renders right actions when provided', () => {
    render(<MobileHeader title="Test" rightActions={<button>Filter</button>} />);
    expect(screen.getByRole('button', { name: 'Filter' })).toBeInTheDocument();
  });

  it('renders back button when onBack is provided', () => {
    render(<MobileHeader title="Detail" onBack={() => {}} />);
    expect(screen.getByLabelText('Torna indietro')).toBeInTheDocument();
  });

  it('does not render back button when onBack is not provided', () => {
    render(<MobileHeader title="Home" />);
    expect(screen.queryByLabelText('Torna indietro')).not.toBeInTheDocument();
  });

  it('is hidden on desktop (lg:hidden class)', () => {
    const { container } = render(<MobileHeader title="Test" />);
    expect(container.firstChild).toHaveClass('lg:hidden');
  });
});
