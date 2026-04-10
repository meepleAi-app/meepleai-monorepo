import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { InlineGamePicker } from '../planning/InlineGamePicker';
import type { GameNightGame } from '@/stores/game-night';

const kbReadyGame: GameNightGame = { id: 'g1', title: 'Terraforming Mars', kbStatus: 'indexed' };
const noKbGame: GameNightGame = { id: 'g2', title: 'Dixit', kbStatus: 'not_indexed' };
const unknownKbGame: GameNightGame = { id: 'g3', title: 'Catan' };

describe('InlineGamePicker — KB filter', () => {
  it('shows only KB-ready games when filterKbReady=true', () => {
    render(
      <InlineGamePicker
        games={[kbReadyGame, noKbGame, unknownKbGame]}
        onSelect={vi.fn()}
        filterKbReady
      />
    );
    expect(screen.getByText('Terraforming Mars')).toBeInTheDocument();
    expect(screen.queryByText('Dixit')).not.toBeInTheDocument();
    expect(screen.queryByText('Catan')).not.toBeInTheDocument();
  });

  it('shows all games when filterKbReady=false (default)', () => {
    render(<InlineGamePicker games={[kbReadyGame, noKbGame, unknownKbGame]} onSelect={vi.fn()} />);
    expect(screen.getByText('Terraforming Mars')).toBeInTheDocument();
    expect(screen.getByText('Dixit')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('shows empty state when no KB-ready games exist', () => {
    render(<InlineGamePicker games={[noKbGame]} onSelect={vi.fn()} filterKbReady />);
    expect(screen.getByText(/Nessun gioco.*con AI disponibile/)).toBeInTheDocument();
  });

  it('renders AI badge on KB-ready games', () => {
    render(<InlineGamePicker games={[kbReadyGame]} onSelect={vi.fn()} />);
    expect(screen.getByTestId('kb-badge-g1')).toBeInTheDocument();
  });
});
