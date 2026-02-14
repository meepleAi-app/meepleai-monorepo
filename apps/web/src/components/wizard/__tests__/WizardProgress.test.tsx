/**
 * WizardProgress Tests - Issue #4141
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

import { WizardProgress } from '../WizardProgress';

describe('WizardProgress', () => {
  it('should render all 4 steps', () => {
    render(<WizardProgress currentStep={1} />);

    expect(screen.getByText('Upload PDF')).toBeInTheDocument();
    expect(screen.getByText('Preview Data')).toBeInTheDocument();
    expect(screen.getByText('BGG Match')).toBeInTheDocument();
    expect(screen.getByText('Confirm')).toBeInTheDocument();
  });

  it('should highlight current step', () => {
    render(<WizardProgress currentStep={2} />);

    const step2 = screen.getByLabelText('Step 2: Preview Data');
    expect(step2).toHaveAttribute('aria-current', 'step');
  });

  it('should call onStepClick when step is clicked and allowSkip is true', () => {
    const mockOnStepClick = vi.fn();
    render(<WizardProgress currentStep={3} onStepClick={mockOnStepClick} allowSkip={true} />);

    const step1 = screen.getByLabelText('Step 1: Upload PDF');
    step1.click();

    expect(mockOnStepClick).toHaveBeenCalledWith(1);
  });

  it('should not allow clicking ahead when allowSkip is false', () => {
    const mockOnStepClick = vi.fn();
    render(<WizardProgress currentStep={1} onStepClick={mockOnStepClick} allowSkip={false} />);

    const step3 = screen.getByLabelText('Step 3: BGG Match');
    expect(step3).toBeDisabled();
  });

  it('should show step descriptions', () => {
    render(<WizardProgress currentStep={1} />);

    expect(screen.getByText('Upload and extract')).toBeInTheDocument();
    expect(screen.getByText('Manual fields')).toBeInTheDocument();
    expect(screen.getByText('Optional')).toBeInTheDocument();
    expect(screen.getByText('Review and submit')).toBeInTheDocument();
  });

  it('should update when currentStep changes', () => {
    const { rerender } = render(<WizardProgress currentStep={1} />);

    expect(screen.getByLabelText('Step 1: Upload PDF')).toHaveAttribute('aria-current', 'step');

    rerender(<WizardProgress currentStep={4} />);

    expect(screen.getByLabelText('Step 4: Confirm')).toHaveAttribute('aria-current', 'step');
  });
});
