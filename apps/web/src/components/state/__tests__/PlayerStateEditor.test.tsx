/**
 * PlayerStateEditor Component Tests - Issue #2420
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { PlayerStateEditor } from '../PlayerStateEditor';
import type { PlayerState } from '../StateEditor';

describe('PlayerStateEditor', () => {
  const mockOnChange = vi.fn();

  it('renders empty state', () => {
    render(<PlayerStateEditor players={[]} onChange={mockOnChange} />);

    expect(screen.getByText('Giocatori')).toBeInTheDocument();
    expect(screen.getByText('Nessun giocatore aggiunto')).toBeInTheDocument();
  });

  it('renders players list', () => {
    const players: PlayerState[] = [
      { id: '1', name: 'Alice', score: 15 },
      { id: '2', name: 'Bob', score: 12 },
    ];

    render(<PlayerStateEditor players={players} onChange={mockOnChange} />);

    expect(screen.getByDisplayValue('Alice')).toBeInTheDocument();
    expect(screen.getByDisplayValue('Bob')).toBeInTheDocument();
    expect(screen.getByDisplayValue('15')).toBeInTheDocument();
    expect(screen.getByDisplayValue('12')).toBeInTheDocument();
  });

  it('shows add player button', () => {
    render(<PlayerStateEditor players={[]} onChange={mockOnChange} />);

    expect(screen.getByText(/Aggiungi Giocatore/)).toBeInTheDocument();
  });

  it('calls onChange when adding player', () => {
    render(<PlayerStateEditor players={[]} onChange={mockOnChange} />);

    const addButton = screen.getByText(/Aggiungi Giocatore/);
    fireEvent.click(addButton);

    expect(mockOnChange).toHaveBeenCalledWith(
      expect.arrayContaining([
        expect.objectContaining({
          name: expect.stringContaining('Giocatore'),
          score: 0,
        }),
      ])
    );
  });

  it('calls onChange when removing player', () => {
    const players: PlayerState[] = [{ id: '1', name: 'Alice', score: 15 }];

    render(<PlayerStateEditor players={players} onChange={mockOnChange} />);

    const removeButton = screen.getByRole('button', { name: '' }); // Trash icon button
    fireEvent.click(removeButton);

    expect(mockOnChange).toHaveBeenCalledWith([]);
  });

  it('calls onChange when updating player name', () => {
    const players: PlayerState[] = [{ id: '1', name: 'Alice', score: 15 }];

    render(<PlayerStateEditor players={players} onChange={mockOnChange} />);

    const nameInput = screen.getByDisplayValue('Alice');
    fireEvent.change(nameInput, { target: { value: 'Alice Updated' } });

    expect(mockOnChange).toHaveBeenCalledWith([
      expect.objectContaining({
        id: '1',
        name: 'Alice Updated',
        score: 15,
      }),
    ]);
  });

  it('calls onChange when updating player score', () => {
    const players: PlayerState[] = [{ id: '1', name: 'Alice', score: 15 }];

    render(<PlayerStateEditor players={players} onChange={mockOnChange} />);

    const scoreInput = screen.getByDisplayValue('15');
    fireEvent.change(scoreInput, { target: { value: '25' } });

    expect(mockOnChange).toHaveBeenCalledWith([
      expect.objectContaining({
        id: '1',
        name: 'Alice',
        score: 25,
      }),
    ]);
  });

  it('displays validation errors', () => {
    const players: PlayerState[] = [{ id: '1', name: '', score: 15 }];

    const validationErrors = {
      'players.0.name': 'Nome giocatore obbligatorio',
    };

    render(
      <PlayerStateEditor
        players={players}
        onChange={mockOnChange}
        validationErrors={validationErrors}
      />
    );

    expect(screen.getByText('Nome giocatore obbligatorio')).toBeInTheDocument();
  });

  it('shows player count summary', () => {
    const players: PlayerState[] = [
      { id: '1', name: 'Alice', score: 15 },
      { id: '2', name: 'Bob', score: 12 },
    ];

    render(<PlayerStateEditor players={players} onChange={mockOnChange} />);

    // Text is split across <strong> and text nodes, use custom matcher
    expect(screen.getByText(/Giocatori:/)).toBeInTheDocument();
    expect(screen.getByText(/Punteggio totale:/)).toBeInTheDocument();
    // Find the summary div and check its full text content
    const summaryDiv = screen.getByText((content, element) => {
      return element?.tagName === 'DIV' &&
             element?.className.includes('bg-blue-50') &&
             element?.textContent?.includes('Giocatori:') === true &&
             element?.textContent?.includes('2') === true &&
             element?.textContent?.includes('Punteggio totale:') === true &&
             element?.textContent?.includes('27') === true;
    });
    expect(summaryDiv).toBeInTheDocument();
  });

  it('hides add/remove buttons in readonly mode', () => {
    const players: PlayerState[] = [{ id: '1', name: 'Alice', score: 15 }];

    render(<PlayerStateEditor players={players} onChange={mockOnChange} readonly />);

    expect(screen.queryByText(/Aggiungi Giocatore/)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '' })).not.toBeInTheDocument(); // Trash button
  });

  it('disables inputs in readonly mode', () => {
    const players: PlayerState[] = [{ id: '1', name: 'Alice', score: 15 }];

    render(<PlayerStateEditor players={players} onChange={mockOnChange} readonly />);

    const nameInput = screen.getByDisplayValue('Alice');
    const scoreInput = screen.getByDisplayValue('15');

    expect(nameInput).toBeDisabled();
    expect(scoreInput).toBeDisabled();
  });

  it('handles player with color', () => {
    const players: PlayerState[] = [{ id: '1', name: 'Alice', score: 15, color: '#ff0000' }];

    render(<PlayerStateEditor players={players} onChange={mockOnChange} />);

    const colorInput = screen.getByDisplayValue('#ff0000');
    expect(colorInput).toBeInTheDocument();
  });

  it('handles multiple validation errors', () => {
    const players: PlayerState[] = [
      { id: '1', name: '', score: 15 },
      { id: '2', name: 'Bob', score: -5 },
    ];

    const validationErrors = {
      'players.0.name': 'Nome giocatore obbligatorio',
      'players.1.score': 'Punteggio non può essere negativo',
    };

    render(
      <PlayerStateEditor
        players={players}
        onChange={mockOnChange}
        validationErrors={validationErrors}
      />
    );

    expect(screen.getByText('Nome giocatore obbligatorio')).toBeInTheDocument();
    expect(screen.getByText('Punteggio non può essere negativo')).toBeInTheDocument();
  });

  it('renders all player fields', () => {
    const players: PlayerState[] = [{ id: '1', name: 'Alice', score: 15, color: '#ff0000' }];

    render(<PlayerStateEditor players={players} onChange={mockOnChange} />);

    expect(screen.getByLabelText(/Nome/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Punteggio/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Colore/)).toBeInTheDocument();
  });

  it('shows zero score correctly', () => {
    const players: PlayerState[] = [{ id: '1', name: 'Alice', score: 0 }];

    render(<PlayerStateEditor players={players} onChange={mockOnChange} />);

    expect(screen.getByDisplayValue('0')).toBeInTheDocument();
    expect(screen.getByText('Punteggio totale:')).toBeInTheDocument();
  });
});
