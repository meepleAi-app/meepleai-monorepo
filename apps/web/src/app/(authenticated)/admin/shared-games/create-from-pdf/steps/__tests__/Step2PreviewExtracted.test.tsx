/**
 * Step2PreviewExtracted Tests - Issue #4141
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { Step2PreviewExtracted } from '../Step2PreviewExtracted';

// Mock store
vi.mock('@/lib/stores/pdf-wizard-store', () => ({
  usePdfWizardStore: vi.fn((selector) => {
    const mockStore = {
      extractedTitle: 'Catan',
      qualityScore: 0.85,
      duplicateWarnings: [],
      setStep2Data: vi.fn(),
    };
    return selector ? selector(mockStore) : mockStore;
  }),
}));

describe('Step2PreviewExtracted', () => {
  const mockOnNext = vi.fn();
  const mockOnSkipBgg = vi.fn();
  const mockOnBack = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('should render extracted title', () => {
    render(
      <Step2PreviewExtracted
        onNext={mockOnNext}
        onSkipBgg={mockOnSkipBgg}
        onBack={mockOnBack}
      />
    );

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText(/Quality: 85%/i)).toBeInTheDocument();
  });

  it('should render manual input fields with placeholders', () => {
    render(
      <Step2PreviewExtracted
        onNext={mockOnNext}
        onSkipBgg={mockOnSkipBgg}
        onBack={mockOnBack}
      />
    );

    expect(screen.getByPlaceholderText('Default: 1')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Default: 4')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Default: 60')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('Default: 8')).toBeInTheDocument();
  });

  it('should allow inputting manual fields', () => {
    render(
      <Step2PreviewExtracted
        onNext={mockOnNext}
        onSkipBgg={mockOnSkipBgg}
        onBack={mockOnBack}
      />
    );

    const minPlayersInput = screen.getByPlaceholderText('Default: 1');
    fireEvent.change(minPlayersInput, { target: { value: '2' } });

    expect(minPlayersInput).toHaveValue(2);
  });

  it('should render description textarea', () => {
    render(
      <Step2PreviewExtracted
        onNext={mockOnNext}
        onSkipBgg={mockOnSkipBgg}
        onBack={mockOnBack}
      />
    );

    expect(
      screen.getByPlaceholderText(/Provide a brief description/i)
    ).toBeInTheDocument();
  });

  // TODO: Add test for duplicate warnings with proper mock setup

  it('should call onBack when back button clicked', () => {
    render(
      <Step2PreviewExtracted
        onNext={mockOnNext}
        onSkipBgg={mockOnSkipBgg}
        onBack={mockOnBack}
      />
    );

    const backButton = screen.getByRole('button', { name: /back/i });
    fireEvent.click(backButton);

    expect(mockOnBack).toHaveBeenCalledTimes(1);
  });

  it('should call onNext when next button clicked', () => {
    render(
      <Step2PreviewExtracted
        onNext={mockOnNext}
        onSkipBgg={mockOnSkipBgg}
        onBack={mockOnBack}
      />
    );

    const nextButton = screen.getByRole('button', { name: /Next: BGG Match/i });
    fireEvent.click(nextButton);

    expect(mockOnNext).toHaveBeenCalledTimes(1);
  });

  it('should call onSkipBgg when skip button clicked', () => {
    render(
      <Step2PreviewExtracted
        onNext={mockOnNext}
        onSkipBgg={mockOnSkipBgg}
        onBack={mockOnBack}
      />
    );

    const skipButton = screen.getByRole('button', { name: /Skip BGG Match/i });
    fireEvent.click(skipButton);

    expect(mockOnSkipBgg).toHaveBeenCalledTimes(1);
  });

  // TODO: Add test for store integration with proper mock setup
});
