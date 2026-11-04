/**
 * AgentSelector Tests (Task #4; #4404)
 *
 * The selector chooses the intent of the answer: Auto, Tutor, Risposta secca. It no longer offers an «agent»
 * (Arbitro, Stratega, Narratore): there is one, and the backend refused two of those five anyway.
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { AgentSelector } from '../AgentSelector';

describe('AgentSelector', () => {
  const mockOnChange = vi.fn();

  const defaultProps = {
    value: 'auto' as const,
    onChange: mockOnChange,
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders the selector dropdown, labelled as the answer and not as an agent', () => {
    render(<AgentSelector {...defaultProps} />);

    expect(screen.getByRole('combobox')).toBeInTheDocument();
    expect(screen.getByText(/Risposta:/i)).toBeInTheDocument();
    expect(screen.queryByText(/Agent:/i)).not.toBeInTheDocument();
  });

  it('offers exactly the three intents when opened', async () => {
    render(<AgentSelector {...defaultProps} />);

    fireEvent.click(screen.getByRole('combobox'));

    await waitFor(() => {
      expect(screen.getAllByRole('option').map(o => o.textContent)).toEqual([
        expect.stringContaining('Auto'),
        expect.stringContaining('Tutor'),
        expect.stringContaining('Risposta secca'),
      ]);
    });
    for (const retired of ['Arbitro', 'Stratega', 'Narratore']) {
      expect(screen.queryByText(retired)).not.toBeInTheDocument();
    }
  });

  it('describes each intent by what it does to the answer', async () => {
    render(<AgentSelector {...defaultProps} />);

    fireEvent.click(screen.getByRole('combobox'));

    await waitFor(() => {
      expect(screen.getAllByText('La risposta di sempre').length).toBeGreaterThan(0);
    });
    expect(screen.getByText('Spiega la regola passo passo, con un esempio')).toBeInTheDocument();
    expect(screen.getByText('Una riga e la citazione')).toBeInTheDocument();
  });

  it('does NOT show fabricated "online" status indicators (#3393)', async () => {
    render(<AgentSelector {...defaultProps} />);

    fireEvent.click(screen.getByRole('combobox'));

    // Options render (descriptions confirm the dropdown opened) ...
    await waitFor(() => {
      expect(screen.getAllByText('La risposta di sempre').length).toBeGreaterThan(0);
    });

    // ... but the hardcoded "Online" status pill is gone.
    expect(screen.queryByText('Online')).not.toBeInTheDocument();
  });

  it('calls onChange with the id of the intent that is selected', async () => {
    render(<AgentSelector {...defaultProps} />);

    fireEvent.click(screen.getByRole('combobox'));

    await waitFor(() => {
      fireEvent.click(screen.getByText('Risposta secca'));
    });

    expect(mockOnChange).toHaveBeenCalledWith('secca');
  });

  it('shows a badge with the name of the selected intent (non-auto)', () => {
    render(<AgentSelector {...defaultProps} value="secca" />);

    // The trigger and the badge both carry it.
    expect(screen.getAllByText('Risposta secca').length).toBeGreaterThan(1);
  });

  it('shows no badge for auto', () => {
    render(<AgentSelector {...defaultProps} value="auto" />);

    expect(screen.getAllByText('Auto')).toHaveLength(1);
  });

  it('can be disabled', () => {
    render(<AgentSelector {...defaultProps} disabled />);

    const trigger = screen.getByRole('combobox');
    expect(trigger).toBeDisabled();
  });
});
