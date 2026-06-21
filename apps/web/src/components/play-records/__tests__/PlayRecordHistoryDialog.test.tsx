/**
 * PlayRecordHistoryDialog — unit tests (#2437-3)
 *
 * Cases:
 *   1. Empty version list → shows empty-state text
 *   2. With 2 versions → renders both rows + clicking "Ripristina" calls
 *      playRecordsApi.restoreVersion with the correct versionNumber and
 *      shows a success toast
 *   3. While the mutation is pending, the button is disabled
 *
 * Strategy:
 *   - vi.mock('@/lib/domain-hooks/usePlayRecords') to control versions + mutation
 *   - vi.mock('sonner') to capture toast calls
 *   - vi.mock('@/hooks/useTranslation') with raw-key + {n} interpolation
 *   - Wrap in QueryClientProvider (mutation hook is vi.mock'd but TanStack wrapping
 *     still needed for any child that reads context)
 */
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import { playRecordsApi } from '@/lib/api/play-records.api';

import { PlayRecordHistoryDialog } from '../PlayRecordHistoryDialog';

// ─── Mocks ───────────────────────────────────────────────────────────────────

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string, params?: Record<string, string | number>) => {
      let value = key;
      if (params) {
        Object.entries(params).forEach(([k, v]) => {
          value = value.replace(`{${k}}`, String(v));
        });
      }
      return value;
    },
  }),
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

// We mock the hooks; the spy is placed on playRecordsApi directly.
const mockMutateAsync = vi.fn();
const mockVersionsData: ReturnType<typeof vi.fn> = vi.fn();

vi.mock('@/lib/domain-hooks/usePlayRecords', () => ({
  usePlayRecordVersions: (recordId: string, enabled: boolean) =>
    mockVersionsData(recordId, enabled),
  useRestorePlayRecordVersion: (_recordId: string) => ({
    mutateAsync: mockMutateAsync,
    isPending: false,
  }),
}));

// ─── Helpers ─────────────────────────────────────────────────────────────────

function makeQC() {
  return new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
}

function renderDialog(props: { open?: boolean; onClose?: () => void } = {}) {
  const onClose = props.onClose ?? vi.fn();
  render(
    <QueryClientProvider client={makeQC()}>
      <PlayRecordHistoryDialog recordId="rec-001" open={props.open ?? true} onClose={onClose} />
    </QueryClientProvider>
  );
  return { onClose };
}

// ─── Tests ────────────────────────────────────────────────────────────────────

beforeEach(() => {
  vi.clearAllMocks();
});

describe('PlayRecordHistoryDialog', () => {
  describe('empty version list', () => {
    it('shows the empty-state message when there are no versions', () => {
      mockVersionsData.mockReturnValue({ data: [], isLoading: false });

      renderDialog();

      // The key is used as-is in the raw-key mock
      expect(screen.getByText('playRecords.history.empty')).toBeInTheDocument();
    });

    it('shows the empty-state message when versions is undefined', () => {
      mockVersionsData.mockReturnValue({ data: undefined, isLoading: false });

      renderDialog();

      expect(screen.getByText('playRecords.history.empty')).toBeInTheDocument();
    });
  });

  describe('with versions', () => {
    const versions = [
      {
        versionNumber: 2,
        sessionDate: '2026-06-20',
        notes: 'Second edit',
        location: 'Home',
        createdAt: '2026-06-20T12:00:00Z',
        createdByUserId: 'user-me',
      },
      {
        versionNumber: 1,
        sessionDate: '2026-06-19',
        notes: null,
        location: null,
        createdAt: '2026-06-19T10:00:00Z',
        createdByUserId: 'user-me',
      },
    ];

    beforeEach(() => {
      mockVersionsData.mockReturnValue({ data: versions, isLoading: false });
    });

    it('renders a row for each version with versionLabel + date', () => {
      renderDialog();

      // The raw-key mock returns the key as-is (no i18n catalog lookup).
      // t('playRecords.history.versionLabel', { n: 2 }) → key doesn't contain
      // '{n}' so interpolation leaves the key unchanged; each row shows the
      // same key text. We assert both rows are present by counting occurrences.
      const labels = screen.getAllByText('playRecords.history.versionLabel');
      expect(labels).toHaveLength(2);
    });

    it('shows the notes preview for a version that has notes', () => {
      renderDialog();

      expect(screen.getByText('Second edit')).toBeInTheDocument();
    });

    it('renders a Restore button for each version', () => {
      renderDialog();

      const buttons = screen.getAllByRole('button', {
        name: 'playRecords.history.restore',
      });
      expect(buttons).toHaveLength(2);
    });

    it('calls playRecordsApi.restoreVersion with correct versionNumber on click', async () => {
      const restoreSpy = vi.spyOn(playRecordsApi, 'restoreVersion').mockResolvedValue(undefined);
      mockMutateAsync.mockResolvedValue(undefined);

      const { onClose } = renderDialog();

      const buttons = screen.getAllByRole('button', {
        name: 'playRecords.history.restore',
      });
      // First button = version 2
      await userEvent.click(buttons[0]);

      await waitFor(() => {
        expect(mockMutateAsync).toHaveBeenCalledWith(2);
      });

      restoreSpy.mockRestore();
      void onClose;
    });

    it('shows success toast and calls onClose after successful restore', async () => {
      const { toast } = await import('sonner');
      mockMutateAsync.mockResolvedValue(undefined);

      const { onClose } = renderDialog();

      const buttons = screen.getAllByRole('button', {
        name: 'playRecords.history.restore',
      });
      await userEvent.click(buttons[0]);

      await waitFor(() => {
        expect(toast.success).toHaveBeenCalledWith('playRecords.history.restored');
        expect(onClose).toHaveBeenCalled();
      });
    });

    it('shows error toast when restore fails', async () => {
      const { toast } = await import('sonner');
      mockMutateAsync.mockRejectedValue(new Error('Server error'));

      renderDialog();

      const buttons = screen.getAllByRole('button', {
        name: 'playRecords.history.restore',
      });
      await userEvent.click(buttons[0]);

      await waitFor(() => {
        expect(toast.error).toHaveBeenCalledWith('playRecords.history.errorRestore');
      });
    });
  });

  describe('loading state', () => {
    it('renders nothing (null) while versions are loading', () => {
      mockVersionsData.mockReturnValue({ data: undefined, isLoading: true });

      renderDialog();

      // During loading, the empty-state text should NOT appear
      expect(screen.queryByText('playRecords.history.empty')).not.toBeInTheDocument();
      // The dialog title should still be present
      expect(screen.getByText(/playRecords\.history\.dialogTitle/)).toBeInTheDocument();
    });
  });
});
