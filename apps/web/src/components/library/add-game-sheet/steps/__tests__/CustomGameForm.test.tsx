/**
 * CustomGameForm Component Tests
 * Issue #4819: AddGameSheet Step 1 - Game Source
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { CustomGameForm } from '../CustomGameForm';

describe('CustomGameForm', () => {
  const defaultProps = {
    onSubmit: vi.fn(),
    onCancel: vi.fn(),
  };

  it('should render all form fields', () => {
    render(<CustomGameForm {...defaultProps} />);
    expect(screen.getByLabelText('Nome gioco *')).toBeInTheDocument();
    expect(screen.getByLabelText('Min giocatori *')).toBeInTheDocument();
    expect(screen.getByLabelText('Max giocatori *')).toBeInTheDocument();
    expect(screen.getByLabelText('Tempo di gioco (minuti)')).toBeInTheDocument();
    expect(screen.getByLabelText('Descrizione')).toBeInTheDocument();
  });

  it('should render action buttons', () => {
    render(<CustomGameForm {...defaultProps} />);
    expect(screen.getByText('Annulla')).toBeInTheDocument();
    expect(screen.getByText('Crea gioco')).toBeInTheDocument();
  });

  it('should call onCancel when cancel is clicked', () => {
    render(<CustomGameForm {...defaultProps} />);
    fireEvent.click(screen.getByText('Annulla'));
    expect(defaultProps.onCancel).toHaveBeenCalled();
  });

  it('should show validation error for empty title', () => {
    render(<CustomGameForm {...defaultProps} />);
    fireEvent.click(screen.getByText('Crea gioco'));
    expect(screen.getByText('Il nome è obbligatorio')).toBeInTheDocument();
  });

  it('should show validation error for empty min players', () => {
    render(<CustomGameForm {...defaultProps} />);
    fireEvent.change(screen.getByLabelText('Nome gioco *'), { target: { value: 'Test' } });
    fireEvent.click(screen.getByText('Crea gioco'));
    expect(screen.getAllByText('Min 1, Max 99').length).toBeGreaterThan(0);
  });

  it('should show error when max < min players', () => {
    render(<CustomGameForm {...defaultProps} />);
    fireEvent.change(screen.getByLabelText('Nome gioco *'), { target: { value: 'Test' } });
    fireEvent.change(screen.getByLabelText('Min giocatori *'), { target: { value: '4' } });
    fireEvent.change(screen.getByLabelText('Max giocatori *'), { target: { value: '2' } });
    fireEvent.click(screen.getByText('Crea gioco'));
    expect(screen.getByText('Deve essere ≥ min giocatori')).toBeInTheDocument();
  });

  it('should call onSubmit with valid data', () => {
    const onSubmit = vi.fn();
    render(<CustomGameForm {...defaultProps} onSubmit={onSubmit} />);

    fireEvent.change(screen.getByLabelText('Nome gioco *'), { target: { value: 'My Game' } });
    fireEvent.change(screen.getByLabelText('Min giocatori *'), { target: { value: '2' } });
    fireEvent.change(screen.getByLabelText('Max giocatori *'), { target: { value: '6' } });
    fireEvent.change(screen.getByLabelText('Tempo di gioco (minuti)'), { target: { value: '45' } });
    fireEvent.click(screen.getByText('Crea gioco'));

    expect(onSubmit).toHaveBeenCalledWith({
      title: 'My Game',
      minPlayers: 2,
      maxPlayers: 6,
      playingTimeMinutes: 45,
      description: undefined,
    });
  });

  it('should disable fields when submitting', () => {
    render(<CustomGameForm {...defaultProps} submitting={true} />);
    expect(screen.getByLabelText('Nome gioco *')).toBeDisabled();
    expect(screen.getByLabelText('Min giocatori *')).toBeDisabled();
    expect(screen.getByText('Annulla').closest('button')).toBeDisabled();
  });

  it('should trim title whitespace', () => {
    const onSubmit = vi.fn();
    render(<CustomGameForm {...defaultProps} onSubmit={onSubmit} />);

    fireEvent.change(screen.getByLabelText('Nome gioco *'), { target: { value: '  Catan  ' } });
    fireEvent.change(screen.getByLabelText('Min giocatori *'), { target: { value: '3' } });
    fireEvent.change(screen.getByLabelText('Max giocatori *'), { target: { value: '4' } });
    fireEvent.click(screen.getByText('Crea gioco'));

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Catan' })
    );
  });
});
