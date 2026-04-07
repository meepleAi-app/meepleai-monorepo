/**
 * AddToLibraryButton Component Tests
 *
 * Tests for quota-aware add-to-library button behavior:
 * - Shows "Aggiungi alla Collezione" + quota count when under limit
 * - Shows "Upgrade per aggiungere" + disabled state when at limit
 * - Disables button when isAdding/loading
 * - Calls add handler when clicked (under limit)
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AddToLibraryButton } from '@/components/library/AddToLibraryButton';

// Default mock values
let mockQuotaData = {
  currentCount: 3,
  maxAllowed: 10,
  userTier: 'free' as const,
  remainingSlots: 7,
  percentageUsed: 30,
};

let mockStatusData = {
  inLibrary: false,
  isFavorite: false,
};

let mockIsLoadingQuota = false;
let mockIsLoadingStatus = false;
let mockAddMutateAsync: Mock;
let mockRemoveMutateAsync: Mock;
let mockAddIsPending = false;

// Mock hooks
vi.mock('@/hooks/queries', () => ({
  useGameInLibraryStatus: () => ({
    data: mockStatusData,
    isLoading: mockIsLoadingStatus,
  }),
  useLibraryQuota: () => ({
    data: mockQuotaData,
    isLoading: mockIsLoadingQuota,
  }),
  useAddGameToLibrary: () => ({
    mutateAsync: mockAddMutateAsync,
    isPending: mockAddIsPending,
  }),
  useRemoveGameFromLibrary: () => ({
    mutateAsync: mockRemoveMutateAsync,
    isPending: false,
  }),
}));

// Mock toast
vi.mock('@/components/layout/Toast', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });

  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
};

describe('AddToLibraryButton', () => {
  beforeEach(() => {
    mockQuotaData = {
      currentCount: 3,
      maxAllowed: 10,
      userTier: 'free',
      remainingSlots: 7,
      percentageUsed: 30,
    };
    mockStatusData = {
      inLibrary: false,
      isFavorite: false,
    };
    mockIsLoadingQuota = false;
    mockIsLoadingStatus = false;
    mockAddIsPending = false;
    mockAddMutateAsync = vi.fn().mockResolvedValue({});
    mockRemoveMutateAsync = vi.fn().mockResolvedValue({});
  });

  // --------------------------------------------------------------------------
  // Under quota limit
  // --------------------------------------------------------------------------

  it('shows "Aggiungi alla Collezione" with quota count when under limit', () => {
    render(<AddToLibraryButton gameId="game-123" gameTitle="Catan" />, {
      wrapper: createWrapper(),
    });

    expect(screen.getByText('Aggiungi alla Collezione')).toBeInTheDocument();
    expect(screen.getByTestId('quota-count')).toHaveTextContent('3/10');
  });

  it('renders button as enabled when under quota limit', () => {
    render(<AddToLibraryButton gameId="game-123" gameTitle="Catan" />, {
      wrapper: createWrapper(),
    });

    const button = screen.getByTestId('add-to-library-button');
    expect(button).not.toBeDisabled();
  });

  // --------------------------------------------------------------------------
  // Quota full
  // --------------------------------------------------------------------------

  it('shows "Upgrade per aggiungere" and disabled state when at limit', () => {
    mockQuotaData = {
      currentCount: 10,
      maxAllowed: 10,
      userTier: 'free',
      remainingSlots: 0,
      percentageUsed: 100,
    };

    render(<AddToLibraryButton gameId="game-123" gameTitle="Catan" />, {
      wrapper: createWrapper(),
    });

    expect(screen.getByText('Upgrade per aggiungere')).toBeInTheDocument();
    expect(screen.getByTestId('quota-count')).toHaveTextContent('10/10');

    const button = screen.getByTestId('add-to-library-button');
    expect(button).toBeDisabled();
  });

  it('does not call add handler when quota is full', () => {
    mockQuotaData = {
      currentCount: 10,
      maxAllowed: 10,
      userTier: 'free',
      remainingSlots: 0,
      percentageUsed: 100,
    };

    render(<AddToLibraryButton gameId="game-123" gameTitle="Catan" />, {
      wrapper: createWrapper(),
    });

    const button = screen.getByTestId('add-to-library-button');
    fireEvent.click(button);

    expect(mockAddMutateAsync).not.toHaveBeenCalled();
  });

  // --------------------------------------------------------------------------
  // Loading / pending states
  // --------------------------------------------------------------------------

  it('disables button when add mutation is pending', () => {
    mockAddIsPending = true;

    render(<AddToLibraryButton gameId="game-123" gameTitle="Catan" />, {
      wrapper: createWrapper(),
    });

    const button = screen.getByTestId('add-to-library-button');
    expect(button).toBeDisabled();
  });

  it('disables button when quota is loading', () => {
    mockIsLoadingQuota = true;

    render(<AddToLibraryButton gameId="game-123" gameTitle="Catan" />, {
      wrapper: createWrapper(),
    });

    const button = screen.getByTestId('add-to-library-button');
    expect(button).toBeDisabled();
  });

  // --------------------------------------------------------------------------
  // Click handler
  // --------------------------------------------------------------------------

  it('calls add mutation when clicked and under limit', async () => {
    render(<AddToLibraryButton gameId="game-123" gameTitle="Catan" />, {
      wrapper: createWrapper(),
    });

    const button = screen.getByTestId('add-to-library-button');
    fireEvent.click(button);

    expect(mockAddMutateAsync).toHaveBeenCalledWith({ gameId: 'game-123' });
  });

  // --------------------------------------------------------------------------
  // In library state
  // --------------------------------------------------------------------------

  it('shows "Rimuovi dalla Libreria" when game is already in library', () => {
    mockStatusData = { inLibrary: true, isFavorite: false };

    render(<AddToLibraryButton gameId="game-123" gameTitle="Catan" />, {
      wrapper: createWrapper(),
    });

    expect(screen.getByText('Rimuovi dalla Libreria')).toBeInTheDocument();
  });

  it('calls remove mutation when clicking in-library button', () => {
    mockStatusData = { inLibrary: true, isFavorite: false };

    render(<AddToLibraryButton gameId="game-123" gameTitle="Catan" />, {
      wrapper: createWrapper(),
    });

    const button = screen.getByTestId('add-to-library-button');
    fireEvent.click(button);

    expect(mockRemoveMutateAsync).toHaveBeenCalledWith('game-123');
  });
});
