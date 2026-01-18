/**
 * BoardStateEditor Component Tests - Issue #2420
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { BoardStateEditor } from '../BoardStateEditor';
import type { BoardState, PlayerState } from '../StateEditor';

describe('BoardStateEditor', () => {
  const mockOnChange = vi.fn();

  const mockPlayers: PlayerState[] = [
    { id: '1', name: 'Alice', score: 0, color: '#ff0000' },
    { id: '2', name: 'Bob', score: 0, color: '#00ff00' },
  ];

  const emptyBoard: BoardState = {
    gridWidth: 10,
    gridHeight: 10,
    pieces: [],
  };

  it('renders board with default grid', () => {
    render(<BoardStateEditor board={emptyBoard} players={mockPlayers} onChange={mockOnChange} />);

    expect(screen.getByText('Plancia')).toBeInTheDocument();
    expect(screen.getByText(/Configura griglia e posiziona pezzi/)).toBeInTheDocument();
  });

  it('renders grid dimensions', () => {
    render(<BoardStateEditor board={emptyBoard} players={mockPlayers} onChange={mockOnChange} />);

    expect(screen.getByDisplayValue('10')).toBeInTheDocument(); // gridWidth
    expect(screen.getAllByDisplayValue('10')).toHaveLength(2); // gridWidth + gridHeight
  });

  it('shows add piece button', () => {
    render(<BoardStateEditor board={emptyBoard} players={mockPlayers} onChange={mockOnChange} />);

    expect(screen.getByText(/Aggiungi Pezzo/)).toBeInTheDocument();
  });

  it('calls onChange when adding piece', () => {
    render(<BoardStateEditor board={emptyBoard} players={mockPlayers} onChange={mockOnChange} />);

    const addButton = screen.getByText(/Aggiungi Pezzo/);
    fireEvent.click(addButton);

    expect(mockOnChange).toHaveBeenCalledWith(
      expect.objectContaining({
        pieces: expect.arrayContaining([
          expect.objectContaining({
            type: 'pawn',
            position: { x: 0, y: 0 },
          }),
        ]),
      })
    );
  });

  it('calls onChange when removing piece', () => {
    const boardWithPiece: BoardState = {
      gridWidth: 10,
      gridHeight: 10,
      pieces: [{ id: 'p1', type: 'pawn', position: { x: 0, y: 0 } }],
    };

    render(
      <BoardStateEditor board={boardWithPiece} players={mockPlayers} onChange={mockOnChange} />
    );

    const removeButton = screen.getAllByRole('button', { name: '' })[0]; // Trash icon button
    fireEvent.click(removeButton);

    expect(mockOnChange).toHaveBeenCalledWith(
      expect.objectContaining({
        pieces: [],
      })
    );
  });

  it('calls onChange when updating grid width', () => {
    render(<BoardStateEditor board={emptyBoard} players={mockPlayers} onChange={mockOnChange} />);

    const widthInput = screen.getByLabelText(/Larghezza/);
    fireEvent.change(widthInput, { target: { value: '15' } });

    expect(mockOnChange).toHaveBeenCalledWith(
      expect.objectContaining({
        gridWidth: 15,
      })
    );
  });

  it('calls onChange when updating grid height', () => {
    render(<BoardStateEditor board={emptyBoard} players={mockPlayers} onChange={mockOnChange} />);

    const heightInput = screen.getByLabelText(/Altezza/);
    fireEvent.change(heightInput, { target: { value: '20' } });

    expect(mockOnChange).toHaveBeenCalledWith(
      expect.objectContaining({
        gridHeight: 20,
      })
    );
  });

  it('displays validation errors', () => {
    const invalidBoard: BoardState = {
      gridWidth: 0,
      gridHeight: 10,
      pieces: [],
    };

    const validationErrors = {
      'board.gridWidth': 'Larghezza griglia minima: 1',
    };

    render(
      <BoardStateEditor
        board={invalidBoard}
        players={mockPlayers}
        onChange={mockOnChange}
        validationErrors={validationErrors}
      />
    );

    expect(screen.getByText('Larghezza griglia minima: 1')).toBeInTheDocument();
  });

  it('shows board summary', () => {
    const boardWithPieces: BoardState = {
      gridWidth: 8,
      gridHeight: 8,
      pieces: [
        { id: 'p1', type: 'pawn', position: { x: 0, y: 0 } },
        { id: 'p2', type: 'knight', position: { x: 1, y: 1 } },
      ],
    };

    render(
      <BoardStateEditor board={boardWithPieces} players={mockPlayers} onChange={mockOnChange} />
    );

    expect(screen.getByText(/Pezzi posizionati: 2/)).toBeInTheDocument();
    expect(screen.getByText(/8 × 8 celle/)).toBeInTheDocument();
  });

  it('renders grid preview', () => {
    render(<BoardStateEditor board={emptyBoard} players={mockPlayers} onChange={mockOnChange} />);

    expect(screen.getByText('Anteprima Griglia')).toBeInTheDocument();
  });

  it('hides add/remove buttons in readonly mode', () => {
    render(
      <BoardStateEditor board={emptyBoard} players={mockPlayers} onChange={mockOnChange} readonly />
    );

    expect(screen.queryByText(/Aggiungi Pezzo/)).not.toBeInTheDocument();
  });

  it('handles piece with owner', () => {
    const boardWithOwner: BoardState = {
      gridWidth: 10,
      gridHeight: 10,
      pieces: [{ id: 'p1', type: 'pawn', position: { x: 0, y: 0 }, ownerId: '1' }],
    };

    render(
      <BoardStateEditor board={boardWithOwner} players={mockPlayers} onChange={mockOnChange} />
    );

    expect(screen.getByDisplayValue('pawn')).toBeInTheDocument();
  });

  it('shows piece position inputs', () => {
    const boardWithPiece: BoardState = {
      gridWidth: 10,
      gridHeight: 10,
      pieces: [{ id: 'p1', type: 'pawn', position: { x: 5, y: 7 } }],
    };

    render(
      <BoardStateEditor board={boardWithPiece} players={mockPlayers} onChange={mockOnChange} />
    );

    expect(screen.getByDisplayValue('5')).toBeInTheDocument(); // x position
    expect(screen.getByDisplayValue('7')).toBeInTheDocument(); // y position
  });

  it('handles empty pieces list', () => {
    render(<BoardStateEditor board={emptyBoard} players={mockPlayers} onChange={mockOnChange} />);

    expect(screen.getByText('Nessun pezzo posizionato')).toBeInTheDocument();
  });

  it('handles board without players', () => {
    render(<BoardStateEditor board={emptyBoard} players={[]} onChange={mockOnChange} />);

    expect(screen.getByText('Plancia')).toBeInTheDocument();
  });

  it('shows grid size in preview', () => {
    const smallBoard: BoardState = {
      gridWidth: 5,
      gridHeight: 5,
      pieces: [],
    };

    render(<BoardStateEditor board={smallBoard} players={mockPlayers} onChange={mockOnChange} />);

    expect(screen.getByText('5 × 5')).toBeInTheDocument();
  });

  it('shows truncation message for large grids', () => {
    const largeBoard: BoardState = {
      gridWidth: 30,
      gridHeight: 30,
      pieces: [],
    };

    render(<BoardStateEditor board={largeBoard} players={mockPlayers} onChange={mockOnChange} />);

    expect(screen.getByText(/Anteprima limitata/)).toBeInTheDocument();
  });

  it('handles multiple pieces', () => {
    const boardWithMultiplePieces: BoardState = {
      gridWidth: 8,
      gridHeight: 8,
      pieces: [
        { id: 'p1', type: 'pawn', position: { x: 0, y: 0 }, ownerId: '1' },
        { id: 'p2', type: 'knight', position: { x: 1, y: 1 }, ownerId: '1' },
        { id: 'p3', type: 'rook', position: { x: 7, y: 7 }, ownerId: '2' },
      ],
    };

    render(
      <BoardStateEditor
        board={boardWithMultiplePieces}
        players={mockPlayers}
        onChange={mockOnChange}
      />
    );

    expect(screen.getByDisplayValue('pawn')).toBeInTheDocument();
    expect(screen.getByDisplayValue('knight')).toBeInTheDocument();
    expect(screen.getByDisplayValue('rook')).toBeInTheDocument();
  });

  it('disables inputs in readonly mode', () => {
    const boardWithPiece: BoardState = {
      gridWidth: 10,
      gridHeight: 10,
      pieces: [{ id: 'p1', type: 'pawn', position: { x: 0, y: 0 } }],
    };

    render(
      <BoardStateEditor
        board={boardWithPiece}
        players={mockPlayers}
        onChange={mockOnChange}
        readonly
      />
    );

    const widthInput = screen.getByLabelText(/Larghezza/);
    const heightInput = screen.getByLabelText(/Altezza/);
    const typeInput = screen.getByDisplayValue('pawn');

    expect(widthInput).toBeDisabled();
    expect(heightInput).toBeDisabled();
    expect(typeInput).toBeDisabled();
  });
});
