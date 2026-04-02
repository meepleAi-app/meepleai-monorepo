import { render } from '@testing-library/react';

import { MeepleCardGrid } from '../variants/MeepleCardGrid';

describe('MeepleCardGrid — Warm Heritage MTG', () => {
  it('has fixed width and height of 200x280', () => {
    render(
      <MeepleCardGrid
        entity="game"
        title="Wingspan"
        identityChip1="Strategia"
        playerCountDisplay="1-5"
        playTimeDisplay="40-70min"
      />
    );
    const card = document.querySelector('[data-card-root]');
    expect(card).toHaveStyle({ width: '200px', height: '280px' });
  });

  it('renders SymbolStrip', () => {
    render(
      <MeepleCardGrid
        entity="game"
        title="Wingspan"
        identityChip1="Strategia"
        playerCountDisplay="1-5"
        playTimeDisplay="40min"
      />
    );
    expect(document.querySelector('[data-symbol-strip]')).toBeInTheDocument();
  });
});
