import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { ContinuePlayingSection } from '../sections/ContinuePlayingSection';

describe('ContinuePlayingSection', () => {
  const games = [
    {
      id: 'g1',
      title: 'Azul',
      subtitle: 'Plan B Games',
      imageUrl: 'https://example.com/azul.jpg',
      rating: 7.8,
      lastPlayedLabel: 'In sessione · round 4/6',
    },
    {
      id: 'g2',
      title: 'Wingspan',
      subtitle: 'Stonemaier',
      rating: 8.1,
      lastPlayedLabel: '2 giorni fa',
    },
  ];

  it('renders section title and count', () => {
    render(<ContinuePlayingSection games={games} />);
    expect(screen.getByText(/Continua a giocare/i)).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
  });

  it('renders all games as cards', () => {
    render(<ContinuePlayingSection games={games} />);
    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('renders the last-played label for each game', () => {
    render(<ContinuePlayingSection games={games} />);
    expect(screen.getByText(/round 4\/6/i)).toBeInTheDocument();
    expect(screen.getByText(/2 giorni fa/i)).toBeInTheDocument();
  });

  it('returns null when no games', () => {
    const { container } = render(<ContinuePlayingSection games={[]} />);
    expect(container).toBeEmptyDOMElement();
  });
});
