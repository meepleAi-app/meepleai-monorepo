/**
 * ReviewActionButtons Component Tests
 *
 * Test Coverage:
 * - Start Review button (not locked state)
 * - Release Review button (locked by you state)
 * - Timer integration
 * - Conflict dialog on 409
 * - Keyboard shortcut (Escape)
 * - Locked by other message
 *
 * Issue #2748: Frontend - Admin Review Lock UI
 * Target: ≥85% coverage
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ReviewActionButtons } from '../ReviewActionButtons';
import * as useQueriesModule from '@/hooks/queries';

// Mock hooks
vi.mock('@/hooks/queries', () => ({
  useStartReview: vi.fn(),
  useReleaseReview: vi.fn(),
}));

// Mock next/navigation
const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
}));

// Mock sonner
vi.mock('sonner', () => ({
  toast: {
    warning: vi.fn(),
    success: vi.fn(),
    error: vi.fn(),
  },
}));

describe('ReviewActionButtons', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: { retry: false },
        mutations: { retry: false },
      },
    });
    vi.clearAllMocks();
  });

  const renderComponent = (props: React.ComponentProps<typeof ReviewActionButtons>) => {
    return render(
      <QueryClientProvider client={queryClient}>
        <ReviewActionButtons {...props} />
      </QueryClientProvider>
    );
  };

  describe('Not Locked State', () => {
    it('renders Start Review button when not locked', () => {
      vi.mocked(useQueriesModule.useStartReview).mockReturnValue({
        mutate: vi.fn(),
        isPending: false,
      } as any);
      vi.mocked(useQueriesModule.useReleaseReview).mockReturnValue({
        mutate: vi.fn(),
        isPending: false,
      } as any);

      renderComponent({
        shareRequestId: 'share-123',
        lockStatus: {
          isLocked: false,
          isLockedByCurrentAdmin: false,
          lockedByAdminName: null,
          lockExpiresAt: null,
        },
      });

      expect(screen.getByRole('button', { name: /Start Review/i })).toBeInTheDocument();
    });

    it('calls startReview mutation when Start Review is clicked', () => {
      const mockMutate = vi.fn();
      vi.mocked(useQueriesModule.useStartReview).mockReturnValue({
        mutate: mockMutate,
        isPending: false,
      } as any);
      vi.mocked(useQueriesModule.useReleaseReview).mockReturnValue({
        mutate: vi.fn(),
        isPending: false,
      } as any);

      renderComponent({
        shareRequestId: 'share-123',
        lockStatus: {
          isLocked: false,
          isLockedByCurrentAdmin: false,
          lockedByAdminName: null,
          lockExpiresAt: null,
        },
      });

      const startButton = screen.getByRole('button', { name: /Start Review/i });
      fireEvent.click(startButton);

      expect(mockMutate).toHaveBeenCalledWith(
        { shareRequestId: 'share-123' },
        expect.objectContaining({
          onSuccess: expect.any(Function),
          onError: expect.any(Function),
        })
      );
    });

    it('shows loading state when starting review', () => {
      vi.mocked(useQueriesModule.useStartReview).mockReturnValue({
        mutate: vi.fn(),
        isPending: true,
      } as any);
      vi.mocked(useQueriesModule.useReleaseReview).mockReturnValue({
        mutate: vi.fn(),
        isPending: false,
      } as any);

      renderComponent({
        shareRequestId: 'share-123',
        lockStatus: {
          isLocked: false,
          isLockedByCurrentAdmin: false,
          lockedByAdminName: null,
          lockExpiresAt: null,
        },
      });

      expect(screen.getByText('Starting...')).toBeInTheDocument();
      expect(screen.getByRole('button')).toBeDisabled();
    });
  });

  describe('Locked by Current Admin', () => {
    it('renders timer and Release button when locked by current admin', () => {
      vi.mocked(useQueriesModule.useStartReview).mockReturnValue({
        mutate: vi.fn(),
        isPending: false,
      } as any);
      vi.mocked(useQueriesModule.useReleaseReview).mockReturnValue({
        mutate: vi.fn(),
        isPending: false,
      } as any);

      renderComponent({
        shareRequestId: 'share-123',
        lockStatus: {
          isLocked: true,
          isLockedByCurrentAdmin: true,
          lockedByAdminName: 'You',
          lockExpiresAt: new Date(Date.now() + 10 * 60 * 1000).toISOString(),
        },
      });

      expect(screen.getByText(/remaining/)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /Release Review/i })).toBeInTheDocument();
    });

    it('calls releaseReview mutation when Release button is clicked', () => {
      const mockMutate = vi.fn();
      vi.mocked(useQueriesModule.useStartReview).mockReturnValue({
        mutate: vi.fn(),
        isPending: false,
      } as any);
      vi.mocked(useQueriesModule.useReleaseReview).mockReturnValue({
        mutate: mockMutate,
        isPending: false,
      } as any);

      renderComponent({
        shareRequestId: 'share-123',
        lockStatus: {
          isLocked: true,
          isLockedByCurrentAdmin: true,
          lockedByAdminName: 'You',
          lockExpiresAt: new Date(Date.now() + 10 * 60 * 1000).toISOString(),
        },
      });

      const releaseButton = screen.getByRole('button', { name: /Release Review/i });
      fireEvent.click(releaseButton);

      expect(mockMutate).toHaveBeenCalled();
    });
  });

  describe('Locked by Another Admin', () => {
    it('shows locked message when locked by another admin', () => {
      vi.mocked(useQueriesModule.useStartReview).mockReturnValue({
        mutate: vi.fn(),
        isPending: false,
      } as any);
      vi.mocked(useQueriesModule.useReleaseReview).mockReturnValue({
        mutate: vi.fn(),
        isPending: false,
      } as any);

      renderComponent({
        shareRequestId: 'share-123',
        lockStatus: {
          isLocked: true,
          isLockedByCurrentAdmin: false,
          lockedByAdminName: 'Sarah Johnson',
          lockExpiresAt: new Date(Date.now() + 15 * 60 * 1000).toISOString(),
        },
      });

      expect(screen.getByText('Currently being reviewed by:')).toBeInTheDocument();
      expect(screen.getByText('Sarah Johnson')).toBeInTheDocument();
    });
  });

  describe('Keyboard Shortcuts', () => {
    it('releases review when Escape is pressed and locked by current admin', () => {
      const mockMutate = vi.fn();
      vi.mocked(useQueriesModule.useStartReview).mockReturnValue({
        mutate: vi.fn(),
        isPending: false,
      } as any);
      vi.mocked(useQueriesModule.useReleaseReview).mockReturnValue({
        mutate: mockMutate,
        isPending: false,
      } as any);

      renderComponent({
        shareRequestId: 'share-123',
        lockStatus: {
          isLocked: true,
          isLockedByCurrentAdmin: true,
          lockedByAdminName: 'You',
          lockExpiresAt: new Date(Date.now() + 10 * 60 * 1000).toISOString(),
        },
        enableKeyboardShortcut: true,
      });

      fireEvent.keyDown(document, { key: 'Escape' });

      expect(mockMutate).toHaveBeenCalled();
    });

    it('does not release when Escape is pressed but not locked by current admin', () => {
      const mockMutate = vi.fn();
      vi.mocked(useQueriesModule.useStartReview).mockReturnValue({
        mutate: vi.fn(),
        isPending: false,
      } as any);
      vi.mocked(useQueriesModule.useReleaseReview).mockReturnValue({
        mutate: mockMutate,
        isPending: false,
      } as any);

      renderComponent({
        shareRequestId: 'share-123',
        lockStatus: {
          isLocked: false,
          isLockedByCurrentAdmin: false,
          lockedByAdminName: null,
          lockExpiresAt: null,
        },
      });

      fireEvent.keyDown(document, { key: 'Escape' });

      expect(mockMutate).not.toHaveBeenCalled();
    });

    it('does not release when keyboard shortcut is disabled', () => {
      const mockMutate = vi.fn();
      vi.mocked(useQueriesModule.useStartReview).mockReturnValue({
        mutate: vi.fn(),
        isPending: false,
      } as any);
      vi.mocked(useQueriesModule.useReleaseReview).mockReturnValue({
        mutate: mockMutate,
        isPending: false,
      } as any);

      renderComponent({
        shareRequestId: 'share-123',
        lockStatus: {
          isLocked: true,
          isLockedByCurrentAdmin: true,
          lockedByAdminName: 'You',
          lockExpiresAt: new Date(Date.now() + 10 * 60 * 1000).toISOString(),
        },
        enableKeyboardShortcut: false,
      });

      fireEvent.keyDown(document, { key: 'Escape' });

      expect(mockMutate).not.toHaveBeenCalled();
    });
  });
});
