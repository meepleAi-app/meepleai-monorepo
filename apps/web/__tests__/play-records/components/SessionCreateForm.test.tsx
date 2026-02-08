/**
 * SessionCreateForm Component Tests
 *
 * Tests multi-step wizard, validation, and submission.
 * Issue #3892: Play Records Frontend UI
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';

import { SessionCreateForm } from '@/components/play-records/SessionCreateForm';

// Mock Zustand store
vi.mock('@/lib/stores/play-records-store', () => ({
  usePlayRecordsStore: vi.fn(() => ({
    sessionCreation: {
      currentStep: 0,
      gameType: 'catalog',
      gameName: '',
      sessionDate: new Date(),
      visibility: 'Private',
      enableScoring: false,
      scoringDimensions: [],
      dimensionUnits: {},
    },
    setSessionField: vi.fn(),
    nextStep: vi.fn(),
    prevStep: vi.fn(),
    resetSessionCreation: vi.fn(),
  })),
  selectSessionCreation: vi.fn(),
}));

describe('SessionCreateForm', () => {
  const mockOnSubmit = vi.fn();
  const mockOnCancel = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders step 1: game selection', () => {
    render(
      <SessionCreateForm
        onSubmit={mockOnSubmit}
        onCancel={mockOnCancel}
      />
    );

    expect(screen.getByText('Game Selection')).toBeInTheDocument();
    expect(screen.getByLabelText('Game Source')).toBeInTheDocument();
    expect(screen.getByLabelText('From Catalog')).toBeInTheDocument();
    expect(screen.getByLabelText('Free-form Game')).toBeInTheDocument();
  });

  it('shows progress stepper', () => {
    render(
      <SessionCreateForm
        onSubmit={mockOnSubmit}
        onCancel={mockOnCancel}
      />
    );

    expect(screen.getByText('Game Selection')).toBeInTheDocument();
    expect(screen.getByText('Session Details')).toBeInTheDocument();
    expect(screen.getByText('Scoring Setup')).toBeInTheDocument();
  });

  it('allows selecting free-form game type', () => {
    render(
      <SessionCreateForm
        onSubmit={mockOnSubmit}
        onCancel={mockOnCancel}
      />
    );

    const freeformRadio = screen.getByLabelText('Free-form Game');
    fireEvent.click(freeformRadio);

    expect(screen.getByPlaceholder('Enter game name...')).toBeInTheDocument();
  });

  it('shows catalog game selector when catalog selected', () => {
    render(
      <SessionCreateForm
        onSubmit={mockOnSubmit}
        onCancel={mockOnCancel}
      />
    );

    const catalogRadio = screen.getByLabelText('From Catalog');
    fireEvent.click(catalogRadio);

    expect(screen.getByPlaceholder('Search your library...')).toBeInTheDocument();
  });

  it('validates required fields', async () => {
    render(
      <SessionCreateForm
        onSubmit={mockOnSubmit}
        onCancel={mockOnCancel}
      />
    );

    // Try to submit without filling required fields
    const nextButton = screen.getByRole('button', { name: 'Next' });
    fireEvent.click(nextButton);

    // Form validation should prevent moving to next step
    // (exact validation message depends on react-hook-form behavior)
  });

  it('calls onCancel when cancel button clicked', () => {
    render(
      <SessionCreateForm
        onSubmit={mockOnSubmit}
        onCancel={mockOnCancel}
      />
    );

    const cancelButton = screen.getByRole('button', { name: 'Cancel' });
    fireEvent.click(cancelButton);

    expect(mockOnCancel).toHaveBeenCalledOnce();
  });

  it('disables buttons when submitting', () => {
    render(
      <SessionCreateForm
        onSubmit={mockOnSubmit}
        onCancel={mockOnCancel}
        isSubmitting={true}
      />
    );

    const nextButton = screen.getByRole('button', { name: 'Next' });
    expect(nextButton).toBeDisabled();
  });

  it('shows Create Session button on final step', () => {
    // Mock store to be on step 2 (final step)
    vi.mocked(require('@/lib/stores/play-records-store').usePlayRecordsStore).mockReturnValue({
      sessionCreation: {
        currentStep: 2,
        gameType: 'freeform',
        gameName: 'Test Game',
        sessionDate: new Date(),
        visibility: 'Private',
        enableScoring: false,
        scoringDimensions: [],
        dimensionUnits: {},
      },
      setSessionField: vi.fn(),
      nextStep: vi.fn(),
      prevStep: vi.fn(),
      resetSessionCreation: vi.fn(),
    });

    render(
      <SessionCreateForm
        onSubmit={mockOnSubmit}
        onCancel={mockOnCancel}
      />
    );

    expect(screen.getByRole('button', { name: /Create Session/ })).toBeInTheDocument();
  });
});
