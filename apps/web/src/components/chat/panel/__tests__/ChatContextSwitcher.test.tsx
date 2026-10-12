import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { ChatContextSwitcher } from '../ChatContextSwitcher';

describe('ChatContextSwitcher', () => {
  const game = {
    id: 'azul',
    name: 'Azul',
    year: 2017,
    pdfCount: 3,
    kbStatus: 'ready' as const,
  };

  it('renders the game name and meta when context is set', () => {
    render(<ChatContextSwitcher gameContext={game} onPickGame={() => {}} />);
    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText(/2017/)).toBeInTheDocument();
    expect(screen.getByText(/3 PDF/)).toBeInTheDocument();
  });

  it('renders a placeholder when no context', () => {
    render(<ChatContextSwitcher gameContext={null} onPickGame={() => {}} />);
    expect(screen.getByText(/Seleziona gioco/i)).toBeInTheDocument();
  });

  it('calls onPickGame when pill clicked', async () => {
    const onPickGame = vi.fn();
    const user = userEvent.setup();
    render(<ChatContextSwitcher gameContext={game} onPickGame={onPickGame} />);
    await user.click(screen.getByRole('button', { name: /azul/i }));
    expect(onPickGame).toHaveBeenCalled();
  });

  describe('#4187: la libreria, con ricerca', () => {
    it('con onSearchChange la tendina si apre anche senza giochi, con il campo di ricerca', async () => {
      const user = userEvent.setup();
      const onSearchChange = vi.fn();
      render(
        <ChatContextSwitcher
          gameContext={null}
          onPickGame={() => {}}
          availableGames={[]}
          search=""
          onSearchChange={onSearchChange}
        />
      );

      await user.click(screen.getByRole('button', { name: /seleziona gioco/i }));
      await user.type(screen.getByRole('searchbox', { name: /cerca nella tua libreria/i }), 'W');

      expect(onSearchChange).toHaveBeenCalledWith('W');
    });

    it('a libreria vuota offre di aggiungere un gioco dal catalogo', async () => {
      const user = userEvent.setup();
      render(
        <ChatContextSwitcher
          gameContext={null}
          onPickGame={() => {}}
          availableGames={[]}
          search=""
          onSearchChange={() => {}}
        />
      );

      await user.click(screen.getByRole('button', { name: /seleziona gioco/i }));

      expect(screen.getByRole('link', { name: /aggiungi dal catalogo/i })).toHaveAttribute(
        'href',
        '/games'
      );
    });

    it('seguire «Aggiungi dal catalogo» avvisa il pannello, che deve chiudersi', async () => {
      const user = userEvent.setup();
      const onCatalogNavigate = vi.fn();
      render(
        <ChatContextSwitcher
          gameContext={null}
          onPickGame={() => {}}
          availableGames={[]}
          search=""
          onSearchChange={() => {}}
          onCatalogNavigate={onCatalogNavigate}
        />
      );

      await user.click(screen.getByRole('button', { name: /seleziona gioco/i }));
      await user.click(screen.getByRole('link', { name: /aggiungi dal catalogo/i }));

      expect(onCatalogNavigate).toHaveBeenCalledOnce();
    });

    it('una ricerca senza risultati lo dice, e offre comunque il catalogo', async () => {
      const user = userEvent.setup();
      render(
        <ChatContextSwitcher
          gameContext={null}
          onPickGame={() => {}}
          availableGames={[]}
          search="Zzz"
          onSearchChange={() => {}}
        />
      );

      await user.click(screen.getByRole('button', { name: /seleziona gioco/i }));

      expect(screen.getByText(/nessun gioco della tua libreria/i)).toBeInTheDocument();
      expect(screen.getByRole('link', { name: /aggiungi dal catalogo/i })).toBeInTheDocument();
    });
  });
});
