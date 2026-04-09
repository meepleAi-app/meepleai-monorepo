import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { LibraryHubCarousel } from '../sections/LibraryHubCarousel';

describe('LibraryHubCarousel', () => {
  it('renders title and count', () => {
    render(
      <LibraryHubCarousel
        title="Continua a giocare"
        count={4}
        seeAllHref="/library?tab=continue"
        entity="session"
      >
        <div>card 1</div>
        <div>card 2</div>
      </LibraryHubCarousel>
    );
    expect(screen.getByText('Continua a giocare')).toBeInTheDocument();
    expect(screen.getByText('4')).toBeInTheDocument();
  });

  it('renders children inside carousel track', () => {
    render(
      <LibraryHubCarousel title="X" seeAllHref="/x" entity="game">
        <div data-testid="child-1">1</div>
        <div data-testid="child-2">2</div>
      </LibraryHubCarousel>
    );
    expect(screen.getByTestId('child-1')).toBeInTheDocument();
    expect(screen.getByTestId('child-2')).toBeInTheDocument();
  });

  it('renders see-all link to seeAllHref', () => {
    render(
      <LibraryHubCarousel title="X" seeAllHref="/library?tab=personal" entity="game">
        <div>x</div>
      </LibraryHubCarousel>
    );
    const link = screen.getByRole('link', { name: /Vedi tutto/i });
    expect(link).toHaveAttribute('href', '/library?tab=personal');
  });

  it('omits count badge when count is undefined', () => {
    const { container } = render(
      <LibraryHubCarousel title="X" seeAllHref="/x" entity="game">
        <div>x</div>
      </LibraryHubCarousel>
    );
    expect(container.querySelector('[data-testid="carousel-count"]')).not.toBeInTheDocument();
  });

  it('uses entity accent color for the bar', () => {
    const { container } = render(
      <LibraryHubCarousel title="X" seeAllHref="/x" entity="player">
        <div>x</div>
      </LibraryHubCarousel>
    );
    const bar = container.querySelector('[data-testid="carousel-accent-bar"]');
    expect(bar).toHaveAttribute('data-entity', 'player');
  });
});
