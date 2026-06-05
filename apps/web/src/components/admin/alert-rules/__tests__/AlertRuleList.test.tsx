import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { AlertRuleList } from '../AlertRuleList';

import type { AlertRule } from '@/lib/api/schemas/alert-rules.schemas';

const sampleRule: AlertRule = {
  id: 'rule-1',
  name: 'Error rate high',
  alertType: 'ErrorRate',
  severity: 'Critical',
  thresholdValue: 5,
  thresholdUnit: '%',
  durationMinutes: 5,
  isEnabled: true,
  description: 'Trigger when error rate > 5%',
  createdAt: '2026-06-02T10:00:00Z',
  updatedAt: '2026-06-02T10:00:00Z',
};

function renderWithProviders(ui: React.ReactElement) {
  // Disable network for useAlertChannels — the test only cares about the
  // TestAlert button + Delete row action wiring. Empty channels list is fine.
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0, staleTime: 0 } },
  });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe('AlertRuleList — #1840 SP5 F4-C7 TestAlert button', () => {
  it('renders test alert button disabled when onTestAlert is not provided', () => {
    renderWithProviders(
      <AlertRuleList rules={[sampleRule]} onDelete={vi.fn()} onToggle={vi.fn()} />
    );
    const btn = screen.getByTestId('test-alert-rule-1');
    expect(btn).toBeDisabled();
    expect(btn).toHaveAttribute('title', expect.stringMatching(/non disponibile/i));
  });

  it('renders test alert button enabled when onTestAlert is provided', () => {
    const onTestAlert = vi.fn();
    renderWithProviders(
      <AlertRuleList
        rules={[sampleRule]}
        onDelete={vi.fn()}
        onToggle={vi.fn()}
        onTestAlert={onTestAlert}
      />
    );
    const btn = screen.getByTestId('test-alert-rule-1');
    expect(btn).not.toBeDisabled();
    fireEvent.click(btn);
    expect(onTestAlert).toHaveBeenCalledWith('rule-1');
  });

  it('renders empty state when no rules are configured', () => {
    renderWithProviders(<AlertRuleList rules={[]} onDelete={vi.fn()} onToggle={vi.fn()} />);
    expect(screen.getByText(/nessuna regola configurata/i)).toBeInTheDocument();
  });

  it('shows 7-col mockup columns (Regola, Metrica, Condizione, Finestra, Severità, Canale, Attiva)', () => {
    renderWithProviders(
      <AlertRuleList rules={[sampleRule]} onDelete={vi.fn()} onToggle={vi.fn()} />
    );
    expect(screen.getByText('Regola')).toBeInTheDocument();
    expect(screen.getByText('Metrica')).toBeInTheDocument();
    expect(screen.getByText('Condizione')).toBeInTheDocument();
    expect(screen.getByText('Finestra')).toBeInTheDocument();
    expect(screen.getByText('Severità')).toBeInTheDocument();
    expect(screen.getByText('Canale')).toBeInTheDocument();
    expect(screen.getByText('Attiva')).toBeInTheDocument();
    expect(screen.getByText('Azioni')).toBeInTheDocument();
    expect(screen.getByText('Error rate high')).toBeInTheDocument();
  });
});
