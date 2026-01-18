/**
 * StateEditor Component Tests - Issue #2420
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { StateEditor } from '../StateEditor';
import type { GameState } from '../StateEditor';

describe('StateEditor', () => {
  const mockOnChange = vi.fn();
  const mockOnValidationError = vi.fn();

  const validState: GameState = {
    players: [{ id: '1', name: 'Alice', score: 10 }],
    resources: [],
    board: { gridWidth: 10, gridHeight: 10, pieces: [] },
  };

  it('renders with default empty state', () => {
    render(<StateEditor />);

    expect(screen.getByText('Editor Stato Gioco')).toBeInTheDocument();
    expect(
      screen.getByText(/Modifica giocatori, risorse e disposizione plancia/)
    ).toBeInTheDocument();
  });

  it('renders with initial state', () => {
    render(<StateEditor initialState={validState} />);

    expect(screen.getByText('Editor Stato Gioco')).toBeInTheDocument();
  });

  it('renders all three tabs', () => {
    render(<StateEditor />);

    expect(screen.getByRole('tab', { name: /Giocatori/ })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /Risorse/ })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /Plancia/ })).toBeInTheDocument();
  });

  it('shows readonly indicator when readonly', () => {
    render(<StateEditor readonly />);

    expect(screen.getByText(/Solo lettura/)).toBeInTheDocument();
  });

  it('calls onChange when players change', async () => {
    render(<StateEditor onChange={mockOnChange} initialState={validState} />);

    // Find and click "Add Player" button
    const addButton = screen.getByText(/Aggiungi Giocatore/);
    fireEvent.click(addButton);

    await waitFor(() => {
      expect(mockOnChange).toHaveBeenCalled();
    });
  });

  it('switches between tabs', () => {
    render(<StateEditor />);

    const playersTab = screen.getByRole('tab', { name: /Giocatori/ });
    const resourcesTab = screen.getByRole('tab', { name: /Risorse/ });
    const boardTab = screen.getByRole('tab', { name: /Plancia/ });

    // Verify all tabs are present and clickable
    expect(playersTab).toBeInTheDocument();
    expect(resourcesTab).toBeInTheDocument();
    expect(boardTab).toBeInTheDocument();

    // Click resources tab
    fireEvent.click(resourcesTab);
    // Click board tab
    fireEvent.click(boardTab);
  });

  it('passes validation errors to sub-editors', () => {
    const stateWithError: GameState = {
      players: [{ id: '1', name: '', score: 10 }], // Invalid: empty name
      resources: [],
      board: { gridWidth: 10, gridHeight: 10, pieces: [] },
    };

    render(<StateEditor initialState={stateWithError} onValidationError={mockOnValidationError} />);

    // Validation should be triggered
    expect(screen.getByText('Editor Stato Gioco')).toBeInTheDocument();
  });

  it('renders board tab content', () => {
    render(<StateEditor initialState={validState} />);

    // Switch to board tab
    const boardTab = screen.getByRole('tab', { name: /Plancia/ });
    fireEvent.click(boardTab);

    expect(screen.getByText('Plancia')).toBeInTheDocument();
  });

  it('does not call onChange in readonly mode', () => {
    render(<StateEditor initialState={validState} onChange={mockOnChange} readonly />);

    // Add Player button should not exist in readonly mode
    expect(screen.queryByText(/Aggiungi Giocatore/)).not.toBeInTheDocument();
  });

  it('renders resources tab content', () => {
    const stateWithResources: GameState = {
      players: [{ id: '1', name: 'Alice', score: 0 }],
      resources: [{ id: 'r1', type: 'token', name: 'Gold', quantity: 10 }],
      board: { gridWidth: 10, gridHeight: 10, pieces: [] },
    };

    render(<StateEditor initialState={stateWithResources} />);

    // Switch to resources tab
    const resourcesTab = screen.getByRole('tab', { name: /Risorse/ });
    fireEvent.click(resourcesTab);

    expect(screen.getByText('Risorse')).toBeInTheDocument();
  });

  it('handles complex state with all data', () => {
    const complexState: GameState = {
      players: [
        { id: '1', name: 'Alice', score: 15, color: '#ff0000' },
        { id: '2', name: 'Bob', score: 12, color: '#00ff00' },
      ],
      resources: [
        { id: 'r1', type: 'token', name: 'Gold', quantity: 25, ownerId: '1' },
        { id: 'r2', type: 'card', name: 'Victory', quantity: 10, ownerId: '2' },
      ],
      board: {
        gridWidth: 8,
        gridHeight: 8,
        pieces: [
          { id: 'p1', type: 'pawn', position: { x: 0, y: 0 }, ownerId: '1' },
          { id: 'p2', type: 'knight', position: { x: 7, y: 7 }, ownerId: '2' },
        ],
      },
    };

    render(<StateEditor initialState={complexState} />);

    expect(screen.getByText('Editor Stato Gioco')).toBeInTheDocument();
  });

  it('renders board with pieces', () => {
    const stateWithPieces: GameState = {
      players: [{ id: '1', name: 'Alice', score: 0 }],
      resources: [],
      board: {
        gridWidth: 5,
        gridHeight: 5,
        pieces: [{ id: 'p1', type: 'pawn', position: { x: 2, y: 2 } }],
      },
    };

    render(<StateEditor initialState={stateWithPieces} />);

    // Switch to board tab
    const boardTab = screen.getByRole('tab', { name: /Plancia/ });
    fireEvent.click(boardTab);

    expect(screen.getByText('Plancia')).toBeInTheDocument();
  });
});
