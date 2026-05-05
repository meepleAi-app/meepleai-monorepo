/**
 * @vitest-environment jsdom
 *
 * Tests for the BGG paste-importer dialog (ADR-051 Sprint 2 / Task 18).
 *
 * The dialog is the operator's UI on top of the Sprint 2 Task 17 backend
 * contract (`BggImportResult { Inserted, Skipped }`). Behaviour under test:
 *  - Live preview re-renders on every paste — parser errors and parsed rows
 *    visible without clicking submit.
 *  - The "Insert N tags" CTA is disabled until at least one well-formed row
 *    exists and stays disabled while the mutation is in flight.
 *  - Submitting hands the parsed rows (not the raw text) to the import hook.
 *  - On success the dialog closes; the toast itself is owned by the hook
 *    (assertion lives in `useImportBggTags.test.tsx`).
 *  - On error the dialog stays open so the operator can retry / fix the paste.
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockMutate = vi.hoisted(() => vi.fn());
const mockState = vi.hoisted(() => ({ isPending: false }));

vi.mock('@/hooks/admin/useImportBggTags', () => ({
  useImportBggTags: () => ({
    mutate: mockMutate,
    isPending: mockState.isPending,
  }),
}));

import { BggImporterPasteDialog } from '../BggImporterPasteDialog';

const SHARED_GAME_ID = '22222222-2222-2222-2222-222222222222';

const VALID_PASTE = [
  'Mechanism\tRole Selection',
  'Mechanism\tVariable Phase Order',
  'Theme\tEconomic',
].join('\n');

function renderWithClient(ui: ReactNode) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe('BggImporterPasteDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockState.isPending = false;
  });

  it('renders the textarea and a disabled submit button when first opened', () => {
    renderWithClient(
      <BggImporterPasteDialog sharedGameId={SHARED_GAME_ID} open={true} onOpenChange={vi.fn()} />
    );

    expect(screen.getByLabelText(/paste/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Insert/i })).toBeDisabled();
  });

  it('shows the live preview rows after pasting well-formed TSV', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <BggImporterPasteDialog sharedGameId={SHARED_GAME_ID} open={true} onOpenChange={vi.fn()} />
    );

    const textarea = screen.getByLabelText(/paste/i);
    await user.click(textarea);
    await user.paste(VALID_PASTE);

    const previewTable = await screen.findByTestId('bgg-importer-preview-table');
    expect(within(previewTable).getByText('Role Selection')).toBeInTheDocument();
    expect(within(previewTable).getByText('Variable Phase Order')).toBeInTheDocument();
    expect(within(previewTable).getByText('Economic')).toBeInTheDocument();

    expect(screen.getByRole('button', { name: /Insert 3/i })).toBeEnabled();
  });

  it('renders per-line parser errors next to the textarea', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <BggImporterPasteDialog sharedGameId={SHARED_GAME_ID} open={true} onOpenChange={vi.fn()} />
    );

    const textarea = screen.getByLabelText(/paste/i);
    await user.click(textarea);
    await user.paste('Mechanism|Role Selection');

    expect(await screen.findByText(/Line 1: expected TAB separator/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Insert/i })).toBeDisabled();
  });

  it('keeps the submit button disabled while the parsed rows count is zero', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <BggImporterPasteDialog sharedGameId={SHARED_GAME_ID} open={true} onOpenChange={vi.fn()} />
    );

    const textarea = screen.getByLabelText(/paste/i);
    // Whitespace-only paste — parser returns rows: [], errors: [].
    await user.click(textarea);
    await user.paste('   \n   ');

    expect(screen.getByRole('button', { name: /Insert/i })).toBeDisabled();
  });

  it('submits the parsed rows (not the raw text) to the import mutation', async () => {
    const user = userEvent.setup();
    renderWithClient(
      <BggImporterPasteDialog sharedGameId={SHARED_GAME_ID} open={true} onOpenChange={vi.fn()} />
    );

    const textarea = screen.getByLabelText(/paste/i);
    await user.click(textarea);
    await user.paste(VALID_PASTE);

    await user.click(screen.getByRole('button', { name: /Insert 3/i }));

    expect(mockMutate).toHaveBeenCalledTimes(1);
    const [variables] = mockMutate.mock.calls[0];
    expect(variables).toEqual({
      sharedGameId: SHARED_GAME_ID,
      tags: [
        { category: 'Mechanism', name: 'Role Selection' },
        { category: 'Mechanism', name: 'Variable Phase Order' },
        { category: 'Theme', name: 'Economic' },
      ],
    });
  });

  it('closes the dialog when the mutation succeeds', async () => {
    const user = userEvent.setup();
    const onOpenChange = vi.fn();

    mockMutate.mockImplementation(
      (_vars, options?: { onSuccess?: (data: { inserted: number; skipped: number }) => void }) => {
        options?.onSuccess?.({ inserted: 3, skipped: 0 });
      }
    );

    renderWithClient(
      <BggImporterPasteDialog
        sharedGameId={SHARED_GAME_ID}
        open={true}
        onOpenChange={onOpenChange}
      />
    );

    const textarea = screen.getByLabelText(/paste/i);
    await user.click(textarea);
    await user.paste(VALID_PASTE);

    await user.click(screen.getByRole('button', { name: /Insert 3/i }));

    await waitFor(() => expect(onOpenChange).toHaveBeenCalledWith(false));
  });

  it('keeps the dialog open when the mutation fails', async () => {
    const user = userEvent.setup();
    const onOpenChange = vi.fn();

    mockMutate.mockImplementation((_vars, options?: { onError?: (err: Error) => void }) => {
      options?.onError?.(new Error('boom'));
    });

    renderWithClient(
      <BggImporterPasteDialog
        sharedGameId={SHARED_GAME_ID}
        open={true}
        onOpenChange={onOpenChange}
      />
    );

    const textarea = screen.getByLabelText(/paste/i);
    await user.click(textarea);
    await user.paste(VALID_PASTE);

    await user.click(screen.getByRole('button', { name: /Insert 3/i }));

    expect(onOpenChange).not.toHaveBeenCalled();
  });

  it('disables the submit button while the mutation is in flight', () => {
    mockState.isPending = true;

    renderWithClient(
      <BggImporterPasteDialog sharedGameId={SHARED_GAME_ID} open={true} onOpenChange={vi.fn()} />
    );

    // While pending the CTA label flips to "Importing…" (the spinner is
    // visible). Either way the button must be disabled to block double-submit.
    expect(screen.getByRole('button', { name: /Importing/i })).toBeDisabled();
  });
});
