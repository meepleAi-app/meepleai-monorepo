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
  updatedAt: null,
};

describe('AlertRuleList — #1840 SP5 F4-C7 TestAlert button', () => {
  it('renders test alert button disabled when onTestAlert is not provided', () => {
    render(<AlertRuleList rules={[sampleRule]} onDelete={vi.fn()} onToggle={vi.fn()} />);
    const btn = screen.getByTestId('test-alert-rule-1');
    expect(btn).toBeDisabled();
    expect(btn).toHaveAttribute('title', expect.stringMatching(/BE endpoint/i));
  });

  it('renders test alert button enabled when onTestAlert is provided', () => {
    const onTestAlert = vi.fn();
    render(
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
});
