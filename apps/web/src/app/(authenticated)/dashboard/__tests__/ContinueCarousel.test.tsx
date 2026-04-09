import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ContinueCarousel } from '../sections/ContinueCarousel';

describe('ContinueCarousel', () => {
  const games = [
    {
      id: 'g1',
      title: 'Azul',
      subtitle: 'Plan B Games',
      imageUrl: 'https://example.com/azul.jpg',
      rating: 7.8,
      players: '2–4',
      duration: '45m',
    },
    {
      id: 'g2',
      title: 'Wingspan',
      subtitle: 'Stonemaier',
      rating: 8.1,
      players: '1–5',
      duration: '70m',
    },
  ];

  it('renders the section header and see-all link', () => {
    render(<ContinueCarousel games={games} />);
    expect(screen.getByText(/Continua a giocare/i)).toBeInTheDocument();
    expect(screen.getByText(/Vedi tutto/i)).toBeInTheDocument();
  });

  it('renders all provided games as cards', () => {
    render(<ContinueCarousel games={games} />);
    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('renders nothing when no games', () => {
    const { container } = render(<ContinueCarousel games={[]} />);
    expect(container).toBeEmptyDOMElement();
  });
});
