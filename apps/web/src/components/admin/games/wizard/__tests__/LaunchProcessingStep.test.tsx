/**
 * LaunchProcessingStep Tests
 * Issue #4673: Step 4 of the admin wizard — launch PDF processing with admin priority.
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import { LaunchProcessingStep } from '../steps/LaunchProcessingStep';

// ─── Mocks ───────────────────────────────────────────────────────────────────

const mockMutate = vi.fn();
let mockIsPending = false;
let mockIsSuccess = false;
let mockIsError = false;
let mockError: Error | null = null;

vi.mock('@/hooks/queries/useAdminGameWizard', () => ({
  useLaunchAdminPdfProcessing: () => ({
    mutate: mockMutate,
    isPending: mockIsPending,
    isSuccess: mockIsSuccess,
    isError: mockIsError,
    error: mockError,
  }),
}));

vi.mock('@/lib/api', () => ({
  getApiBase: vi.fn(() => ''),
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

// ─── Helpers ─────────────────────────────────────────────────────────────────

const createWrapper = () => {
  const qc = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  return ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
};

const defaultProps = {
  gameId: 'game-uuid-1',
  gameTitle: 'Gloomhaven',
  pdfDocumentId: 'pdf-uuid-1',
  onBack: vi.fn(),
  onProcessingLaunched: vi.fn(),
};

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('LaunchProcessingStep', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockIsPending = false;
    mockIsSuccess = false;
    mockIsError = false;
    mockError = null;
  });

  it('should render the game title', () => {
    render(<LaunchProcessingStep {...defaultProps} />, { wrapper: createWrapper() });
    expect(screen.getByText('Gloomhaven')).toBeDefined();
  });

  it('should render the PDF document ID', () => {
    render(<LaunchProcessingStep {...defaultProps} />, { wrapper: createWrapper() });
    expect(screen.getByText('pdf-uuid-1')).toBeDefined();
  });

  it('should show "Launch Processing" button initially', () => {
    render(<LaunchProcessingStep {...defaultProps} />, { wrapper: createWrapper() });
    expect(screen.getByText('Launch Processing')).toBeDefined();
  });

  it('should call mutate with gameId and pdfDocumentId when launch button is clicked', () => {
    render(<LaunchProcessingStep {...defaultProps} />, { wrapper: createWrapper() });

    fireEvent.click(screen.getByText('Launch Processing'));

    expect(mockMutate).toHaveBeenCalledWith(
      { gameId: 'game-uuid-1', pdfDocumentId: 'pdf-uuid-1' },
      expect.any(Object)
    );
  });

  it('should call onBack when Back button is clicked', () => {
    const onBack = vi.fn();
    render(<LaunchProcessingStep {...defaultProps} onBack={onBack} />, {
      wrapper: createWrapper(),
    });

    fireEvent.click(screen.getByText('Back'));
    expect(onBack).toHaveBeenCalledOnce();
  });

  it('should show "Launching..." while isPending', () => {
    mockIsPending = true;
    render(<LaunchProcessingStep {...defaultProps} />, { wrapper: createWrapper() });
    expect(screen.getByText('Launching...')).toBeDefined();
  });

  it('should show "Processing Launched!" when isSuccess', () => {
    mockIsSuccess = true;
    render(<LaunchProcessingStep {...defaultProps} />, { wrapper: createWrapper() });
    expect(screen.getByText('Processing Launched!')).toBeDefined();
  });

  it('should show error message when isError', () => {
    mockIsError = true;
    mockError = new Error('PDF document not found');
    render(<LaunchProcessingStep {...defaultProps} />, { wrapper: createWrapper() });
    expect(screen.getByText('PDF document not found')).toBeDefined();
  });

  it('should disable launch button when isPending', () => {
    mockIsPending = true;
    render(<LaunchProcessingStep {...defaultProps} />, { wrapper: createWrapper() });
    const button = screen.getAllByRole('button').find((b) =>
      b.textContent?.includes('Launching')
    );
    expect(button).toBeDefined();
    expect((button as HTMLButtonElement).disabled).toBe(true);
  });

  it('should disable launch button when isSuccess', () => {
    mockIsSuccess = true;
    render(<LaunchProcessingStep {...defaultProps} />, { wrapper: createWrapper() });
    const button = screen.getAllByRole('button').find((b) =>
      b.textContent?.includes('Processing Launched')
    );
    expect(button).toBeDefined();
    expect((button as HTMLButtonElement).disabled).toBe(true);
  });

  it('should call onProcessingLaunched via mutate onSuccess callback', () => {
    mockMutate.mockImplementation((_params, options) => {
      options?.onSuccess?.();
    });

    const onProcessingLaunched = vi.fn();
    render(
      <LaunchProcessingStep {...defaultProps} onProcessingLaunched={onProcessingLaunched} />,
      { wrapper: createWrapper() }
    );

    fireEvent.click(screen.getByText('Launch Processing'));
    expect(onProcessingLaunched).toHaveBeenCalledWith('game-uuid-1');
  });
});
