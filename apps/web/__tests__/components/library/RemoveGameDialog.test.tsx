/**
 * RemoveGameDialog Component Tests (Issue #2464)
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { RemoveGameDialog } from '@/components/library/RemoveGameDialog';

// Mock useRemoveGameFromLibrary hook
vi.mock('@/hooks/queries', () => ({
  useRemoveGameFromLibrary: () => ({
    mutateAsync: vi.fn().mockResolvedValue({}),
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

describe('RemoveGameDialog', () => {
  const mockProps = {
    isOpen: true,
    onClose: vi.fn(),
    gameId: 'test-game-id',
    gameTitle: 'Catan',
    onRemoved: vi.fn(),
  };

  it('should render dialog when open', () => {
    render(<RemoveGameDialog {...mockProps} />, { wrapper: createWrapper() });
    expect(screen.getByText('Rimuovi dalla Libreria?')).toBeInTheDocument();
  });

  it('should display game title in description', () => {
    render(<RemoveGameDialog {...mockProps} />, { wrapper: createWrapper() });
    expect(screen.getByText(/Catan/)).toBeInTheDocument();
  });

  it('should show warning message', () => {
    render(<RemoveGameDialog {...mockProps} />, { wrapper: createWrapper() });
    expect(screen.getByText(/Questa azione non può essere annullata/)).toBeInTheDocument();
  });

  it('should call onClose when cancel button clicked', () => {
    render(<RemoveGameDialog {...mockProps} />, { wrapper: createWrapper() });
    const cancelButton = screen.getByText('Annulla');

    fireEvent.click(cancelButton);

    expect(mockProps.onClose).toHaveBeenCalled();
  });

  it('should show remove button with icon', () => {
    render(<RemoveGameDialog {...mockProps} />, { wrapper: createWrapper() });
    expect(screen.getByText('Rimuovi')).toBeInTheDocument();
  });
});
