import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { CatalogCarouselSection } from '../sections/CatalogCarouselSection';

describe('CatalogCarouselSection', () => {
  const games = [
    {
      id: 'c1',
      title: 'Gloomhaven',
      subtitle: 'Cephalofair',
      rating: 8.8,
    },
    {
      id: 'c2',
      title: 'Brass: Birmingham',
      subtitle: 'Roxley',
      rating: 8.6,
    },
  ];

  it('renders title with total count', () => {
    render(<CatalogCarouselSection games={games} totalCount={230} />);
    expect(screen.getByText(/Dal catalogo community/i)).toBeInTheDocument();
    expect(screen.getByText('230')).toBeInTheDocument();
  });

  it('renders all provided games', () => {
    render(<CatalogCarouselSection games={games} totalCount={230} />);
    expect(screen.getByText('Gloomhaven')).toBeInTheDocument();
    expect(screen.getByText('Brass: Birmingham')).toBeInTheDocument();
  });

  it('returns null when no games', () => {
    const { container } = render(<CatalogCarouselSection games={[]} totalCount={0} />);
    expect(container).toBeEmptyDOMElement();
  });
});
