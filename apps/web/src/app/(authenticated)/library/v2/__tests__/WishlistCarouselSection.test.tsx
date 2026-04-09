import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { WishlistCarouselSection } from '../sections/WishlistCarouselSection';

describe('WishlistCarouselSection', () => {
  const games = [
    {
      id: 'w1',
      title: 'Terraforming Mars',
      subtitle: 'FryxGames',
      rating: 8.4,
    },
    {
      id: 'w2',
      title: 'Heat: Pedal to the Metal',
      subtitle: 'Days of Wonder',
      rating: 8.0,
    },
  ];

  it('renders title with total count', () => {
    render(<WishlistCarouselSection games={games} totalCount={8} />);
    expect(screen.getByText(/La tua wishlist/i)).toBeInTheDocument();
    expect(screen.getByText('8')).toBeInTheDocument();
  });

  it('renders all wishlist games', () => {
    render(<WishlistCarouselSection games={games} totalCount={8} />);
    expect(screen.getByText('Terraforming Mars')).toBeInTheDocument();
    expect(screen.getByText('Heat: Pedal to the Metal')).toBeInTheDocument();
  });

  it('returns null when no games', () => {
    const { container } = render(<WishlistCarouselSection games={[]} totalCount={0} />);
    expect(container).toBeEmptyDOMElement();
  });
});
