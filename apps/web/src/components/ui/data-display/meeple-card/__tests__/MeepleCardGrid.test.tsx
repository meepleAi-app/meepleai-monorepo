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

  it('non renderizza il bottom bar quando bottomStatValue è undefined', () => {
    const { container } = render(<MeepleCardGrid entity="game" title="Wingspan" />);
    const bottomBar = container.querySelector('[data-testid="meeple-card-bottom-bar"]');
    expect(bottomBar).not.toBeInTheDocument();
  });

  it('renderizza il bottom bar quando bottomStatValue è definito', () => {
    const { container } = render(
      <MeepleCardGrid
        entity="game"
        title="Wingspan"
        bottomStatLabel="Partite"
        bottomStatValue="42"
      />
    );
    const bar = container.querySelector('[data-testid="meeple-card-bottom-bar"]');
    expect(bar).toBeInTheDocument();
    expect(bar).toHaveTextContent('42');
  });
});
