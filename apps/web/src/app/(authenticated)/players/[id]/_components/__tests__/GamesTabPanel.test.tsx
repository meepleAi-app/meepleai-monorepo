import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { GamesTabPanel } from '../GamesTabPanel';

const labels = {
  title: 'Giochi giocati',
  viewAll: 'Vedi tutti',
  empty: 'Nessun gioco giocato',
  playsSuffix: 'partite',
};

describe('GamesTabPanel', () => {
  const playerId = 'p-test';

  it('renders the panel root with data-slot="games-tab-panel"', () => {
    const { container } = render(
      <GamesTabPanel playerId={playerId} gamePlayCounts={{ Azul: 23, Wingspan: 17 }} labels={labels} />,
    );
    expect(container.querySelector('[data-slot="games-tab-panel"]')).not.toBeNull();
  });

  it('lists each game with its play count ranked descending', () => {
    render(
      <GamesTabPanel
        playerId={playerId}
        gamePlayCounts={{ Azul: 23, Wingspan: 17, Brass: 12 }}
        labels={labels}
      />,
    );
    const items = screen.getAllByRole('listitem');
    expect(items).toHaveLength(3);
    expect(items[0]).toHaveTextContent('Azul');
    expect(items[0]).toHaveTextContent('23');
    expect(items[1]).toHaveTextContent('Wingspan');
    expect(items[2]).toHaveTextContent('Brass');
  });

  it('renders a CTA link to /players/<id>/games when non-empty', () => {
    render(<GamesTabPanel playerId={playerId} gamePlayCounts={{ Azul: 23 }} labels={labels} />);
    const cta = screen.getByRole('link', { name: /vedi tutti/i });
    expect(cta).toHaveAttribute('href', '/players/p-test/games');
  });

  it('shows the empty label when gamePlayCounts is empty', () => {
    render(<GamesTabPanel playerId={playerId} gamePlayCounts={{}} labels={labels} />);
    expect(screen.getByText(/nessun gioco giocato/i)).toBeInTheDocument();
  });
});
