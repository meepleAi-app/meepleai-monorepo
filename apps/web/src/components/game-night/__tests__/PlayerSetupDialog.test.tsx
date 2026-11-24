/**
 * PlayerSetupDialog — #4770: il creatore di una partita a un gioco privato gioca, collegato al suo
 * account, come nei wizard di /sessions/new (#4767). Prima ogni giocatore era scritto per nome, e la
 * partita non risultava giocata da nessun account.
 */
import { fireEvent, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { PlayerSetupDialog } from '../PlayerSetupDialog';

const currentUser = vi.hoisted(() => ({
  current: null as { id: string; displayName?: string } | null,
}));
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => ({ data: currentUser.current }),
}));

function renderDialog(onStart = vi.fn()) {
  render(
    <PlayerSetupDialog
      open
      onOpenChange={vi.fn()}
      gameName="Il Mio Gioco"
      minPlayers={1}
      maxPlayers={6}
      onStart={onStart}
      isLoading={false}
    />
  );
  return onStart;
}

const nameInputs = () => screen.getAllByPlaceholderText('Nome giocatore') as HTMLInputElement[];
const startButton = () => screen.getByRole('button', { name: /inizia|avvia/i });

describe('PlayerSetupDialog — #4770: il creatore collegato al suo account', () => {
  beforeEach(() => {
    currentUser.current = { id: 'user-marta', displayName: 'Marta' };
  });

  it('il primo posto è il creatore, e parte con il suo account', () => {
    const onStart = renderDialog();
    expect(nameInputs()[0].value).toBe('Marta');

    fireEvent.click(screen.getByRole('button', { name: /aggiungi giocatore/i }));
    fireEvent.change(nameInputs()[1], { target: { value: 'Bob' } });
    fireEvent.click(startButton());

    expect(onStart).toHaveBeenCalledWith([
      { displayName: 'Marta', color: 'Red', userId: 'user-marta' },
      { displayName: 'Bob', color: 'Blue' },
    ]);
  });

  it('cambiare il nome tiene il collegamento; svuotarlo lo toglie', () => {
    const onStart = renderDialog();
    fireEvent.change(nameInputs()[0], { target: { value: 'Martina' } });
    fireEvent.click(startButton());
    expect(onStart).toHaveBeenLastCalledWith([
      { displayName: 'Martina', color: 'Red', userId: 'user-marta' },
    ]);

    fireEvent.change(nameInputs()[0], { target: { value: '' } });
    fireEvent.change(nameInputs()[0], { target: { value: 'Ospite' } });
    fireEvent.click(startButton());
    expect(onStart).toHaveBeenLastCalledWith([{ displayName: 'Ospite', color: 'Red' }]);
  });

  it('senza utente noto nessun posto è collegato e il primo resta vuoto', () => {
    currentUser.current = null;
    renderDialog();
    expect(nameInputs()[0].value).toBe('');
  });
});
