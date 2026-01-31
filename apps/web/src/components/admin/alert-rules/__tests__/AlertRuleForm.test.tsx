/**
 * AlertRuleForm Component Tests - Issue #2253
 * Tests template application and form pre-fill functionality
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AlertRuleForm } from '../AlertRuleForm';
import type { CreateAlertRule } from '@/lib/api/schemas/alert-rules.schemas';

// Mock API
vi.mock('@/lib/api/alert-rules.api', () => ({
  alertRulesApi: {
    create: vi.fn(() => Promise.resolve()),
    update: vi.fn(() => Promise.resolve()),
  },
}));

describe('AlertRuleForm', () => {
  const mockOnSubmit = vi.fn();
  const mockOnCancel = vi.fn();

  it('renders empty form by default', () => {
    render(<AlertRuleForm rule={null} onSubmit={mockOnSubmit} onCancel={mockOnCancel} />);

    expect(screen.getByLabelText(/name/i)).toHaveValue('');
    expect(screen.getByLabelText(/alert type/i)).toHaveValue('');
  });

  it('pre-fills form with initialData from template', () => {
    const initialData: Partial<CreateAlertRule> = {
      name: 'High Error Rate',
      alertType: 'HighErrorRate',
      severity: 'Critical',
      thresholdValue: 5,
      thresholdUnit: '%',
      durationMinutes: 10,
      description: 'Alert when error rate exceeds threshold',
    };

    render(
      <AlertRuleForm
        rule={null}
        initialData={initialData}
        onSubmit={mockOnSubmit}
        onCancel={mockOnCancel}
      />
    );

    expect(screen.getByLabelText(/name/i)).toHaveValue('High Error Rate');
    expect(screen.getByLabelText(/alert type/i)).toHaveValue('HighErrorRate');
    expect(screen.getByLabelText(/threshold value/i)).toHaveValue(5);
    expect(screen.getByLabelText(/unit/i)).toHaveValue('%');
    expect(screen.getByLabelText(/duration/i)).toHaveValue(10);
    expect(screen.getByLabelText(/description/i)).toHaveValue(
      'Alert when error rate exceeds threshold'
    );
  });

  it('prioritizes initialData over rule when both provided', () => {
    const initialData: Partial<CreateAlertRule> = {
      name: 'Template Name',
      alertType: 'TemplateType',
    };

    const existingRule = {
      id: 'rule-1',
      name: 'Existing Rule',
      alertType: 'ExistingType',
      severity: 'Warning' as const,
      thresholdValue: 1,
      thresholdUnit: 'ms',
      durationMinutes: 5,
      isEnabled: true,
      description: 'Existing description',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    };

    render(
      <AlertRuleForm
        rule={existingRule}
        initialData={initialData}
        onSubmit={mockOnSubmit}
        onCancel={mockOnCancel}
      />
    );

    // Should use template data, not existing rule
    expect(screen.getByLabelText(/name/i)).toHaveValue('Template Name');
    expect(screen.getByLabelText(/alert type/i)).toHaveValue('TemplateType');
  });

  it('handles partial initialData with fallback defaults', () => {
    const partialData: Partial<CreateAlertRule> = {
      name: 'Partial Template',
      // Missing other fields
    };

    render(
      <AlertRuleForm
        rule={null}
        initialData={partialData}
        onSubmit={mockOnSubmit}
        onCancel={mockOnCancel}
      />
    );

    expect(screen.getByLabelText(/name/i)).toHaveValue('Partial Template');
    expect(screen.getByLabelText(/alert type/i)).toHaveValue('');
    expect(screen.getByLabelText(/threshold value/i)).toHaveValue(0);
    expect(screen.getByLabelText(/unit/i)).toHaveValue('%');
    expect(screen.getByLabelText(/duration/i)).toHaveValue(5);
  });

  it('allows user to modify pre-filled template data', async () => {
    const user = userEvent.setup();
    const initialData: Partial<CreateAlertRule> = {
      name: 'Template Name',
      thresholdValue: 5,
    };

    render(
      <AlertRuleForm
        rule={null}
        initialData={initialData}
        onSubmit={mockOnSubmit}
        onCancel={mockOnCancel}
      />
    );

    const nameInput = screen.getByLabelText(/name/i);
    await user.clear(nameInput);
    await user.type(nameInput, 'Modified Name');

    expect(nameInput).toHaveValue('Modified Name');
  });

  it('handles cancel button click', async () => {
    const user = userEvent.setup();

    render(<AlertRuleForm rule={null} onSubmit={mockOnSubmit} onCancel={mockOnCancel} />);

    await user.click(screen.getByRole('button', { name: /cancel/i }));

    expect(mockOnCancel).toHaveBeenCalledTimes(1);
  });
});
