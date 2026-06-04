/**
 * ManualAssignModal — tests for the BGG-ID assignment flow.
 * Issue #1879 — verify 409 conflict path (modal stays open, toast.error fires).
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { ManualAssignModal } from './ManualAssignModal';

const toastSuccess = vi.hoisted(() => vi.fn());
const toastError = vi.hoisted(() => vi.fn());

vi.mock('sonner', () => ({
  toast: { success: toastSuccess, error: toastError },
}));

const fetchMock = vi.fn();

beforeEach(() => {
  vi.stubGlobal('fetch', fetchMock);
});

afterEach(() => {
  vi.restoreAllMocks();
  toastSuccess.mockReset();
  toastError.mockReset();
});

async function fillAndSubmit() {
  await userEvent.type(
    screen.getByLabelText(/Shared Game ID/i),
    '00000000-0000-0000-0000-000000000001'
  );
  await userEvent.type(screen.getByLabelText(/BGG ID/i), '12345');
  await userEvent.click(screen.getByRole('button', { name: /Assign/i }));
}

describe('ManualAssignModal', () => {
  it('POSTs /assign-bgg-id and closes on 200 OK', async () => {
    fetchMock.mockResolvedValue({ ok: true, json: async () => ({ assigned: true }) });
    const onOpenChange = vi.fn();
    render(<ManualAssignModal open onOpenChange={onOpenChange} />);

    await fillAndSubmit();

    await waitFor(() => expect(fetchMock).toHaveBeenCalled());
    expect(fetchMock).toHaveBeenCalledWith(
      expect.stringContaining('/admin/catalog-ingestion/assign-bgg-id'),
      expect.objectContaining({
        method: 'POST',
        credentials: 'include',
        body: JSON.stringify({
          sharedGameId: '00000000-0000-0000-0000-000000000001',
          bggId: 12345,
        }),
      })
    );
    await waitFor(() => expect(toastSuccess).toHaveBeenCalledWith('BGG ID assigned'));
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('shows error toast with BE message when /assign-bgg-id returns 409', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 409,
      json: async () => ({ assigned: false, error: 'BGG ID already in use' }),
    });
    const onOpenChange = vi.fn();
    render(<ManualAssignModal open onOpenChange={onOpenChange} />);

    await fillAndSubmit();

    await waitFor(() => expect(toastError).toHaveBeenCalledWith('BGG ID already in use'));
    // Modal stays open so the user can correct input
    expect(onOpenChange).not.toHaveBeenCalledWith(false);
  });

  it('falls back to generic error when 409 body has no error field', async () => {
    fetchMock.mockResolvedValue({
      ok: false,
      status: 409,
      json: async () => ({ assigned: false }),
    });
    render(<ManualAssignModal open onOpenChange={vi.fn()} />);

    await fillAndSubmit();

    await waitFor(() => expect(toastError).toHaveBeenCalledWith('Assignment failed'));
  });
});
