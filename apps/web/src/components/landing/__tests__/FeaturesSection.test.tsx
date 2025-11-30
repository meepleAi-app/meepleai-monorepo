/**
 * FeaturesSection Tests
 *
 * Comprehensive unit tests for Features grid section.
 * Target: 100% coverage
 */

import { render, screen } from '@testing-library/react';
import { FeaturesSection } from '../FeaturesSection';

describe('FeaturesSection', () => {
  it('renders section with correct heading', () => {
    render(<FeaturesSection />);

    expect(screen.getByRole('heading', { name: /caratteristiche/i })).toBeInTheDocument();
  });

  it('renders section description', () => {
    render(<FeaturesSection />);

    expect(
      screen.getByText('Tutto quello che ti serve per giocare senza dubbi')
    ).toBeInTheDocument();
  });

  it('renders all three feature cards', () => {
    render(<FeaturesSection />);

    expect(screen.getByText('AI Intelligente')).toBeInTheDocument();
    expect(screen.getByText('Catalogo Ampio')).toBeInTheDocument();
    expect(screen.getByText('Mobile-First')).toBeInTheDocument();
  });

  it('renders feature descriptions', () => {
    render(<FeaturesSection />);

    expect(screen.getByText(/risposte immediate con citazioni dal manuale/i)).toBeInTheDocument();
    expect(screen.getByText(/migliaia di giochi già disponibili/i)).toBeInTheDocument();
    expect(screen.getByText(/perfetto durante le partite/i)).toBeInTheDocument();
  });

  it('applies responsive grid classes', () => {
    const { container } = render(<FeaturesSection />);

    const grid = container.querySelector('.grid');
    expect(grid).toHaveClass('grid-cols-1', 'md:grid-cols-2', 'lg:grid-cols-3');
  });

  it('has features section id for scroll navigation', () => {
    const { container } = render(<FeaturesSection />);

    const section = container.querySelector('#features');
    expect(section).toBeInTheDocument();
  });

  it('applies hover transition classes to cards', () => {
    const { container } = render(<FeaturesSection />);

    const cards = container.querySelectorAll('.group');
    expect(cards.length).toBe(3);
    cards.forEach(card => {
      expect(card).toHaveClass('hover:shadow-lg', 'transition-all');
    });
  });
});
