import { screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

const mockCreate = vi.hoisted(() => vi.fn().mockResolvedValue({ id: 'new-id' }));
vi.mock('@/lib/api/alert-rules.api', () => ({
  alertRulesApi: { create: mockCreate },
}));
vi.mock('@/hooks/useToast', () => ({ useToast: () => ({ toast: vi.fn() }) }));

// MetricSelector wraps a custom combobox + chips UI. For tests we stub it with
// a plain labelled input so getByLabelText(/Metrica Prometheus/i) finds it.
vi.mock('@/components/admin/alert-rules/MetricSelector', () => ({
  MetricSelector: ({
    id,
    value,
    onChange,
  }: {
    id: string;
    value: string;
    onChange: (v: string) => void;
    required?: boolean;
  }) => (
    <input
      id={id}
      type="text"
      value={value}
      onChange={e => onChange(e.target.value)}
      data-testid="metric-selector-stub"
    />
  ),
}));

// useAlertChannels triggers useQuery; we don't need real channel data here.
vi.mock('@/hooks/useAlertChannels', () => ({
  useAlertChannels: () => ({ channels: [], isLoading: false, isError: false }),
  ALERT_CHANNELS_QUERY_KEY: ['admin', 'alert-channels'],
}));

import { CreateAlertRuleDialog } from '../CreateAlertRuleDialog';
import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

describe('CreateAlertRuleDialog', () => {
  const defaultProps = { open: true, onClose: vi.fn(), onCreated: vi.fn() };

  it('mostra il form quando aperto', () => {
    renderWithQuery(<CreateAlertRuleDialog {...defaultProps} />);
    expect(screen.getByLabelText(/^nome$/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/metrica prometheus/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/severità/i)).toBeInTheDocument();
  });

  it('disabilita Submit se name è vuoto', () => {
    renderWithQuery(<CreateAlertRuleDialog {...defaultProps} />);
    expect(screen.getByRole('button', { name: /crea regola/i })).toBeDisabled();
  });

  it('chiama alertRulesApi.create con i dati corretti', async () => {
    renderWithQuery(<CreateAlertRuleDialog {...defaultProps} />);

    fireEvent.change(screen.getByLabelText(/^nome$/i), { target: { value: 'High Error Rate' } });
    fireEvent.change(screen.getByLabelText(/metrica prometheus/i), {
      target: { value: 'error_rate' },
    });
    fireEvent.change(screen.getByLabelText(/valore soglia/i), { target: { value: '5' } });
    fireEvent.change(screen.getByLabelText(/unità/i), { target: { value: '%' } });
    fireEvent.change(screen.getByLabelText(/finestra/i), { target: { value: '10' } });

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
    renderWithQuery(<CreateAlertRuleDialog {...defaultProps} onCreated={onCreated} />);

    fireEvent.change(screen.getByLabelText(/^nome$/i), { target: { value: 'Test Rule' } });
    fireEvent.change(screen.getByLabelText(/metrica prometheus/i), {
      target: { value: 'cpu_usage' },
    });
    fireEvent.change(screen.getByLabelText(/valore soglia/i), { target: { value: '80' } });
    fireEvent.change(screen.getByLabelText(/unità/i), { target: { value: '%' } });
    fireEvent.change(screen.getByLabelText(/finestra/i), { target: { value: '5' } });
    fireEvent.click(screen.getByRole('button', { name: /crea regola/i }));

    await waitFor(() => expect(onCreated).toHaveBeenCalled());
  });
});
