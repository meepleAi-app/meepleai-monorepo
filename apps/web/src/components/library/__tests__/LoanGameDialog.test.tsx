/**
 * LoanGameDialog Component Tests (Task 7)
 *
 * Test Coverage:
 * - Renders borrower info input field
 * - Confirm button disabled when borrowerInfo is empty
 * - Confirm button enabled when borrowerInfo is filled
 *
 * Target: ≥90% coverage
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

import { LoanGameDialog } from '../LoanGameDialog';

// ============================================================================
// Mock Setup
// ============================================================================

// Track mock state
let mockMutate: ReturnType<typeof vi.fn>;
let mockIsPending: boolean;

vi.mock('@/hooks/queries/useLoanStatus', () => ({
  useMarkAsOnLoan: () => ({
    mutate: mockMutate,
    isPending: mockIsPending,
  }),
}));

// ============================================================================
// Tests
// ============================================================================

describe('LoanGameDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockMutate = vi.fn();
    mockIsPending = false;
  });

  it('mostra il campo "Prestato a"', () => {
    render(<LoanGameDialog gameId="123" gameTitle="Catan" open onOpenChange={() => {}} />);
    expect(screen.getByLabelText(/prestato a/i)).toBeInTheDocument();
  });

  it('disabilita il pulsante conferma se borrowerInfo è vuoto', () => {
    render(<LoanGameDialog gameId="123" gameTitle="Catan" open onOpenChange={() => {}} />);
    expect(screen.getByRole('button', { name: /conferma prestito/i })).toBeDisabled();
  });

  it('abilita il pulsante conferma quando borrowerInfo è compilato', () => {
    render(<LoanGameDialog gameId="123" gameTitle="Catan" open onOpenChange={() => {}} />);
    fireEvent.change(screen.getByLabelText(/prestato a/i), { target: { value: 'Mario Rossi' } });
    expect(screen.getByRole('button', { name: /conferma prestito/i })).not.toBeDisabled();
  });

  it('chiama mutate con borrowerInfo trimmed quando si clicca conferma', () => {
    render(<LoanGameDialog gameId="123" gameTitle="Catan" open onOpenChange={() => {}} />);
    fireEvent.change(screen.getByLabelText(/prestato a/i), {
      target: { value: '  Mario Rossi  ' },
    });
    fireEvent.click(screen.getByRole('button', { name: /conferma prestito/i }));
    expect(mockMutate).toHaveBeenCalledWith('Mario Rossi', expect.any(Object));
  });

  it('chiama onOpenChange(false) dopo successo', async () => {
    const onOpenChange = vi.fn();
    mockMutate = vi.fn((_, options) => options?.onSuccess?.());
    render(<LoanGameDialog gameId="123" gameTitle="Catan" open onOpenChange={onOpenChange} />);
    fireEvent.change(screen.getByLabelText(/prestato a/i), { target: { value: 'Mario Rossi' } });
    fireEvent.click(screen.getByRole('button', { name: /conferma prestito/i }));
    await waitFor(() => expect(onOpenChange).toHaveBeenCalledWith(false));
  });
});
