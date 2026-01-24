/**
 * @vitest-environment jsdom
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { GameHeroSection } from '../game-hero-section';

describe('GameHeroSection', () => {
  it('renders game title', () => {
    render(
      <GameHeroSection title="Catan" status="Owned" imageUrl={null} publisher={null} year={null} />
    );

    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('renders status badge with correct color', () => {
    render(<GameHeroSection title="Test" status="Owned" />);

    const badge = screen.getByRole('status');
    expect(badge).toHaveTextContent('Owned');
    expect(badge).toHaveClass('bg-green-100');
  });

  it('renders publisher and year when provided', () => {
    render(<GameHeroSection title="Test" status="Owned" publisher="KOSMOS" year={1995} />);

    expect(screen.getByText(/KOSMOS/)).toBeInTheDocument();
    expect(screen.getByText(/1995/)).toBeInTheDocument();
  });
});
