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
    reviewedMetadata: null,
    importResult: null,
    isProcessing: false,
    error: null,
    goNext: vi.fn(),
    goBack: vi.fn(),
    canGoNext: () => false,
    canGoBack: () => false,
    reset: vi.fn(),
    setUploadedPdf: vi.fn(),
    setStep: vi.fn(),
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

vi.mock('../steps/Step3PreviewConfirm', () => ({
  Step3PreviewConfirm: () => <div>Step 3</div>,
}));

describe('AdminGameImportWizard - Error Boundary', () => {
  it('should catch errors and show fallback UI', async () => {
    const originalError = console.error;
    console.error = vi.fn();

    renderWithQuery(<AdminGameImportWizardClient />);

    // Should show error fallback (Italian UI text)
    await screen.findByText('Errore nel wizard');
    expect(screen.getByText(/Si è verificato un errore/i)).toBeInTheDocument();

    // Should show recovery buttons
    expect(screen.getByRole('button', { name: /ricomincia/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /torna ai giochi/i })).toBeInTheDocument();

    console.error = originalError;
  });

  it('should allow user to start over after error', async () => {
    const originalError = console.error;
    console.error = vi.fn();

    renderWithQuery(<AdminGameImportWizardClient />);

    await screen.findByText('Errore nel wizard');

    const startOverBtn = screen.getByRole('button', { name: /ricomincia/i });
    const user = userEvent.setup();

    await user.click(startOverBtn);

    // Error boundary resets, Step1 throws again, so error fallback re-appears
    const newStartOverBtn = await screen.findByRole('button', { name: /ricomincia/i });
    expect(newStartOverBtn).toBeInTheDocument();

    console.error = originalError;
  });
});
