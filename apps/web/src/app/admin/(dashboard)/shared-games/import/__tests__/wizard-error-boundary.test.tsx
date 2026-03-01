/**
 * Game Import Wizard - Error Boundary Tests
 * Issue #4167: Error boundary for wizard
 */

import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { AdminGameImportWizardClient } from '../client';

// Mock dependencies
vi.mock('@/components/auth/AuthProvider', () => ({
  useAuthUser: () => ({
    user: { id: 'user-1', email: 'test@example.com', role: 'Admin' },
    loading: false,
  }),
}));

vi.mock('@/stores/useGameImportWizardStore', () => ({
  useGameImportWizardStore: () => ({
    currentStep: 1,
    uploadedPdf: null,
    extractedMetadata: null,
    selectedBggId: null,
    bggGameData: null,
    enrichedData: null,
    isProcessing: false,
    error: null,
    goNext: vi.fn(),
    goBack: vi.fn(),
    canGoNext: () => false,
    canGoBack: () => false,
    reset: vi.fn(),
    submitWizard: vi.fn(),
    setUploadedPdf: vi.fn(),
    setStep: vi.fn(),
    setSelectedBggId: vi.fn(),
  }),
}));

vi.mock('@/hooks/wizard/useWizardAutoSave', () => ({
  useWizardAutoSave: vi.fn(),
  clearDraft: vi.fn(),
}));

vi.mock('../steps/Step1UploadPdf', () => ({
  Step1UploadPdf: () => {
    throw new Error('Step1 component crashed!');
  },
}));

vi.mock('../steps/Step3BggMatch', () => ({
  Step3BggMatch: () => <div>Step 3</div>,
}));

describe('AdminGameImportWizard - Error Boundary', () => {
  it('should catch errors and show fallback UI', async () => {
    // Suppress console.error for this test
    const originalError = console.error;
    console.error = vi.fn();

    renderWithQuery(<AdminGameImportWizardClient />);

    // Should show error fallback
    await screen.findByText('Wizard Error');
    expect(screen.getByText(/error occurred in the game import wizard/i)).toBeInTheDocument();

    // Should show recovery buttons
    expect(screen.getByRole('button', { name: /start over/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /back to games/i })).toBeInTheDocument();

    console.error = originalError;
  });

  it('should allow user to start over after error', async () => {
    const originalError = console.error;
    console.error = vi.fn();

    renderWithQuery(<AdminGameImportWizardClient />);

    await screen.findByText('Wizard Error');

    const startOverBtn = screen.getByRole('button', { name: /start over/i });
    const user = userEvent.setup();

    await user.click(startOverBtn);

    // Error boundary resets, Step1 throws again, so error fallback re-appears
    // Re-query for the button since DOM was re-rendered
    const newStartOverBtn = await screen.findByRole('button', { name: /start over/i });
    expect(newStartOverBtn).toBeInTheDocument();

    console.error = originalError;
  });
});
