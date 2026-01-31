/**
 * EditNotesModal Component Tests (Issue #2464)
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { EditNotesModal } from '@/components/library/EditNotesModal';

// Mock useUpdateLibraryEntry hook
vi.mock('@/hooks/queries', () => ({
  useUpdateLibraryEntry: () => ({
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

describe('EditNotesModal', () => {
  const mockProps = {
    isOpen: true,
    onClose: vi.fn(),
    gameId: 'test-game-id',
    gameTitle: 'Catan',
    currentNotes: 'Test notes',
    onNotesUpdated: vi.fn(),
  };

  it('should render modal when open', () => {
    render(<EditNotesModal {...mockProps} />, { wrapper: createWrapper() });
    expect(screen.getByText('Modifica Note')).toBeInTheDocument();
  });

  it('should display game title in description', () => {
    render(<EditNotesModal {...mockProps} />, { wrapper: createWrapper() });
    expect(screen.getByText(/Catan/)).toBeInTheDocument();
  });

  it('should show current notes in textarea', () => {
    render(<EditNotesModal {...mockProps} />, { wrapper: createWrapper() });
    const textarea = screen.getByPlaceholderText('Inserisci le tue note qui...');
    expect(textarea).toHaveValue('Test notes');
  });

  it('should show character counter', () => {
    render(<EditNotesModal {...mockProps} currentNotes="Hello" />, { wrapper: createWrapper() });
    expect(screen.getByText('495 caratteri rimanenti')).toBeInTheDocument();
  });

  it('should update character counter when typing', () => {
    render(<EditNotesModal {...mockProps} currentNotes="" />, { wrapper: createWrapper() });
    const textarea = screen.getByPlaceholderText('Inserisci le tue note qui...');

    fireEvent.change(textarea, { target: { value: 'New notes' } });

    expect(screen.getByText('491 caratteri rimanenti')).toBeInTheDocument();
  });

  it('should call onClose when cancel button clicked', () => {
    render(<EditNotesModal {...mockProps} />, { wrapper: createWrapper() });
    const cancelButton = screen.getByText('Annulla');

    fireEvent.click(cancelButton);

    expect(mockProps.onClose).toHaveBeenCalled();
  });

  it('should enforce max length of 500 characters', () => {
    render(<EditNotesModal {...mockProps} />, { wrapper: createWrapper() });
    const textarea = screen.getByPlaceholderText('Inserisci le tue note qui...');

    expect(textarea).toHaveAttribute('maxLength', '500');
  });
});
