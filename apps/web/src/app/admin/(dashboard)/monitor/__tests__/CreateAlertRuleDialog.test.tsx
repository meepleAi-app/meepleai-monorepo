import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { CreateAlertRuleDialog } from '../CreateAlertRuleDialog';

const mockCreate = vi.hoisted(() => vi.fn().mockResolvedValue({ id: 'new-id' }));
vi.mock('@/lib/api/alert-rules.api', () => ({
  alertRulesApi: { create: mockCreate },
}));
vi.mock('@/hooks/useToast', () => ({ useToast: () => ({ toast: vi.fn() }) }));

describe('CreateAlertRuleDialog', () => {
  const defaultProps = { open: true, onClose: vi.fn(), onCreated: vi.fn() };

  it('mostra il form quando aperto', () => {
    render(<CreateAlertRuleDialog {...defaultProps} />);
    expect(screen.getByLabelText(/nome/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/alert type/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/severity/i)).toBeInTheDocument();
  });

  it('disabilita Submit se name è vuoto', () => {
    render(<CreateAlertRuleDialog {...defaultProps} />);
    expect(screen.getByRole('button', { name: /crea regola/i })).toBeDisabled();
  });

  it('chiama alertRulesApi.create con i dati corretti', async () => {
    render(<CreateAlertRuleDialog {...defaultProps} />);

    fireEvent.change(screen.getByLabelText(/nome/i), { target: { value: 'High Error Rate' } });
    fireEvent.change(screen.getByLabelText(/alert type/i), { target: { value: 'error_rate' } });
    fireEvent.change(screen.getByLabelText(/threshold/i), { target: { value: '5' } });
    fireEvent.change(screen.getByLabelText(/unit/i), { target: { value: '%' } });
    fireEvent.change(screen.getByLabelText(/duration/i), { target: { value: '10' } });

    fireEvent.click(screen.getByRole('button', { name: /crea regola/i }));

    await waitFor(() =>
      expect(mockCreate).toHaveBeenCalledWith(
        expect.objectContaining({
          name: 'High Error Rate',
          alertType: 'error_rate',
          thresholdValue: 5,
          thresholdUnit: '%',
          durationMinutes: 10,
        })
      )
    );
  });

  it('chiama onCreated dopo successo', async () => {
    const onCreated = vi.fn();
    render(<CreateAlertRuleDialog {...defaultProps} onCreated={onCreated} />);

    fireEvent.change(screen.getByLabelText(/nome/i), { target: { value: 'Test Rule' } });
    fireEvent.change(screen.getByLabelText(/alert type/i), { target: { value: 'cpu_usage' } });
    fireEvent.change(screen.getByLabelText(/threshold/i), { target: { value: '80' } });
    fireEvent.change(screen.getByLabelText(/unit/i), { target: { value: '%' } });
    fireEvent.change(screen.getByLabelText(/duration/i), { target: { value: '5' } });
    fireEvent.click(screen.getByRole('button', { name: /crea regola/i }));

    await waitFor(() => expect(onCreated).toHaveBeenCalled());
  });
});
