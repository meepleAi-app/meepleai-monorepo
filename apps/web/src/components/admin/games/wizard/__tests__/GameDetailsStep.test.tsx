/**
 * GameDetailsStep Tests
 * Issue #4673: Step 2 of the admin wizard — review BGG game info and create it.
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import { GameDetailsStep } from '../steps/GameDetailsStep';
import type { CreateGameFromWizardResult } from '@/hooks/queries/useAdminGameWizard';

// ─── Mocks ───────────────────────────────────────────────────────────────────

const mockMutate = vi.fn();
let mockIsPending = false;
let mockIsSuccess = false;
let mockIsError = false;
let mockError: Error | null = null;

vi.mock('@/hooks/queries/useAdminGameWizard', () => ({
  useCreateGameFromWizard: () => ({
    mutate: mockMutate,
    isPending: mockIsPending,
    isSuccess: mockIsSuccess,
    isError: mockIsError,
    error: mockError,
  }),
}));

vi.mock('next/image', () => ({
  default: ({ alt }: { alt: string }) => <img alt={alt} />,
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

const selectedGame = {
  bggId: 174430,
  name: 'Gloomhaven',
  yearPublished: 2017,
  thumbnailUrl: null,
  imageUrl: null,
  type: 'boardgame' as const,
};

const defaultProps = {
  selectedGame,
  onBack: vi.fn(),
  onGameCreated: vi.fn(),
};

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('GameDetailsStep', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockIsPending = false;
    mockIsSuccess = false;
    mockIsError = false;
    mockError = null;
  });

  it('should display the selected game name', () => {
    render(<GameDetailsStep {...defaultProps} />, { wrapper: createWrapper() });
    expect(screen.getByText('Gloomhaven')).toBeDefined();
  });

  it('should display BGG ID', () => {
    render(<GameDetailsStep {...defaultProps} />, { wrapper: createWrapper() });
    expect(screen.getByText('174430')).toBeDefined();
  });

  it('should display year published', () => {
    render(<GameDetailsStep {...defaultProps} />, { wrapper: createWrapper() });
    expect(screen.getByText('2017')).toBeDefined();
  });

  it('should show "Create Game" button initially', () => {
    render(<GameDetailsStep {...defaultProps} />, { wrapper: createWrapper() });
    expect(screen.getByText('Create Game')).toBeDefined();
  });

  it('should call mutate with bggId when Create Game is clicked', () => {
    render(<GameDetailsStep {...defaultProps} />, { wrapper: createWrapper() });

    fireEvent.click(screen.getByText('Create Game'));

    expect(mockMutate).toHaveBeenCalledWith(174430, expect.any(Object));
  });

  it('should call onBack when Back button is clicked', () => {
    const onBack = vi.fn();
    render(<GameDetailsStep {...defaultProps} onBack={onBack} />, {
      wrapper: createWrapper(),
    });

    fireEvent.click(screen.getByText('Back to Search'));
    expect(onBack).toHaveBeenCalledOnce();
  });

  it('should show "Creating Game..." while isPending', () => {
    mockIsPending = true;
    render(<GameDetailsStep {...defaultProps} />, { wrapper: createWrapper() });
    expect(screen.getByText('Creating Game...')).toBeDefined();
  });

  it('should show "Game Created!" when isSuccess', () => {
    mockIsSuccess = true;
    render(<GameDetailsStep {...defaultProps} />, { wrapper: createWrapper() });
    expect(screen.getByText('Game Created!')).toBeDefined();
  });

  it('should show error message when creation fails', () => {
    mockIsError = true;
    mockError = new Error('Un gioco con questo ID esiste già');
    render(<GameDetailsStep {...defaultProps} />, { wrapper: createWrapper() });
    expect(screen.getByText('Un gioco con questo ID esiste già')).toBeDefined();
  });

  it('should disable Create Game button when isPending', () => {
    mockIsPending = true;
    render(<GameDetailsStep {...defaultProps} />, { wrapper: createWrapper() });
    const button = screen.getAllByRole('button').find(b => b.textContent?.includes('Creating'));
    expect((button as HTMLButtonElement).disabled).toBe(true);
  });

  it('should call onGameCreated via mutate onSuccess callback', () => {
    const createdResult: CreateGameFromWizardResult = {
      sharedGameId: 'shared-uuid',
      title: 'Gloomhaven',
      bggId: 174430,
      status: 'created',
    };

    mockMutate.mockImplementation((_bggId, options) => {
      options?.onSuccess?.(createdResult);
    });

    const onGameCreated = vi.fn();
    render(<GameDetailsStep {...defaultProps} onGameCreated={onGameCreated} />, {
      wrapper: createWrapper(),
    });

    fireEvent.click(screen.getByText('Create Game'));
    expect(onGameCreated).toHaveBeenCalledWith(createdResult);
  });
});
