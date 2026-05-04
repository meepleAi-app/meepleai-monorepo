/**
 * Wave C.1 (Issue #581) — GameDetailKpiCards unit tests.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { GameDetailKpiCards, type GameDetailKpiCard } from '../GameDetailKpiCards';

const cards: ReadonlyArray<GameDetailKpiCard> = [
  { key: 'rating', label: 'Valutazione', value: '8.1', unit: '/10', icon: '⭐', accent: 'rating' },
  {
    key: 'complexity',
    label: 'Complessità',
    value: '2.4',
    unit: '/5',
    icon: '🧠',
    accent: 'complexity',
  },
  { key: 'players', label: 'Giocatori', value: '1-5', icon: '👥', accent: 'players' },
  { key: 'time', label: 'Durata', value: '70', unit: 'min', icon: '⏱', accent: 'time' },
];

describe('GameDetailKpiCards (Wave C.1)', () => {
  it('renders one card per entry as listitem', () => {
    render(<GameDetailKpiCards cards={cards} />);
    const items = screen.getAllByRole('listitem');
    expect(items).toHaveLength(4);
  });

  it('renders labels and values and unit suffixes', () => {
    render(<GameDetailKpiCards cards={cards} />);
    expect(screen.getByText('Valutazione')).toBeInTheDocument();
    expect(screen.getByText('8.1')).toBeInTheDocument();
    expect(screen.getByText('/10')).toBeInTheDocument();
    expect(screen.getByText('1-5')).toBeInTheDocument();
  });

  it('exposes data-kpi-key for downstream selectors', () => {
    const { container } = render(<GameDetailKpiCards cards={cards} />);
    const ratingCard = container.querySelector('[data-kpi-key="rating"]');
    expect(ratingCard).toBeInTheDocument();
  });

  it('renders without unit when unit is undefined', () => {
    const cardsNoUnit: ReadonlyArray<GameDetailKpiCard> = [
      { key: 'players', label: 'Giocatori', value: '4' },
    ];
    const { container } = render(<GameDetailKpiCards cards={cardsNoUnit} />);
    expect(container.querySelector('[data-kpi-key="players"]')).toHaveTextContent('Giocatori4');
  });

  it('still renders a card without an icon', () => {
    const cardsNoIcon: ReadonlyArray<GameDetailKpiCard> = [
      { key: 'rating', label: 'Rating', value: '7' },
    ];
    render(<GameDetailKpiCards cards={cardsNoIcon} />);
    expect(screen.getByText('Rating')).toBeInTheDocument();
    expect(screen.getByText('7')).toBeInTheDocument();
  });
});
