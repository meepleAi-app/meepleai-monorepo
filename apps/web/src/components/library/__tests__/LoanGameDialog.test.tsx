import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { LoanGameDialog } from '../LoanGameDialog';

vi.mock('@/hooks/queries/useLoanStatus', () => ({
  useMarkAsOnLoan: () => ({
    mutate: vi.fn(),
    isPending: false,
  }),
}));

// Mock window.matchMedia (required in jsdom)
Object.defineProperty(window, 'matchMedia', {
  writable: true,
  value: vi.fn().mockImplementation(query => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: vi.fn(),
    removeListener: vi.fn(),
    addEventListener: vi.fn(),
    removeEventListener: vi.fn(),
    dispatchEvent: vi.fn(),
  })),
});

describe('LoanGameDialog', () => {
  it('mostra il campo borrowerInfo', () => {
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
});
