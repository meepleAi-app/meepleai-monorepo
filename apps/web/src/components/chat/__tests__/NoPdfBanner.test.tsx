import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { NoPdfBanner } from '../NoPdfBanner';

describe('NoPdfBanner', () => {
  it('shows fallback warning when no PDF', () => {
    render(<NoPdfBanner gameId="game-1" gameName="Azul" hasPdf={false} />);
    expect(screen.getByText(/conoscenza generale/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /carica regolamento/i })).toBeInTheDocument();
  });

  it('does not render when PDF exists', () => {
    const { container } = render(<NoPdfBanner gameId="game-1" gameName="Azul" hasPdf={true} />);
    expect(container.firstChild).toBeNull();
  });

  it('shows processing state when PDF is processing', () => {
    render(
      <NoPdfBanner gameId="game-1" gameName="Azul" hasPdf={false} pdfProcessingState="Extracting" />
    );
    expect(screen.getByText(/analisi in corso/i)).toBeInTheDocument();
  });

  it('links to the game detail page for upload', () => {
    render(<NoPdfBanner gameId="game-1" gameName="Azul" hasPdf={false} />);
    const link = screen.getByRole('link', { name: /carica regolamento/i });
    expect(link.closest('a')?.getAttribute('href')).toBe('/library/games/game-1');
  });
});
