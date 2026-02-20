/**
 * StepIndicator Component Tests
 * Issue #4818: AddGameSheet Drawer + State Machine
 */

import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { StepIndicator } from '../StepIndicator';

const defaultSteps = [
  { label: 'Sorgente' },
  { label: 'PDF' },
  { label: 'Info & Salva' },
];

describe('StepIndicator', () => {
  it('should render all step labels', () => {
    render(<StepIndicator currentStep={1} steps={defaultSteps} />);

    expect(screen.getByText('Sorgente')).toBeInTheDocument();
    expect(screen.getByText('PDF')).toBeInTheDocument();
    expect(screen.getByText('Info & Salva')).toBeInTheDocument();
  });

  it('should show step 1 as active when currentStep is 1', () => {
    render(<StepIndicator currentStep={1} steps={defaultSteps} />);

    expect(screen.getByText('1')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('should show checkmark for completed steps', () => {
    render(<StepIndicator currentStep={3} steps={defaultSteps} />);

    const checkmarks = screen.getAllByText('✓');
    expect(checkmarks).toHaveLength(2); // Steps 1 and 2 completed
    expect(screen.getByText('3')).toBeInTheDocument(); // Step 3 active
  });

  it('should show step 2 as active when currentStep is 2', () => {
    render(<StepIndicator currentStep={2} steps={defaultSteps} />);

    const checkmarks = screen.getAllByText('✓');
    expect(checkmarks).toHaveLength(1); // Step 1 completed
    expect(screen.getByText('2')).toBeInTheDocument(); // Step 2 active
    expect(screen.getByText('3')).toBeInTheDocument(); // Step 3 pending
  });
});
