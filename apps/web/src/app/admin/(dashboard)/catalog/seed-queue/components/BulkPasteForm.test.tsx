import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import * as api from '../lib/catalog-seed-api';
import { BulkPasteForm } from './BulkPasteForm';

vi.mock('../lib/catalog-seed-api');
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn() } }));

function wrapper() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

describe('BulkPasteForm', () => {
  it('disables the submit button when no valid IDs are entered', () => {
    render(<BulkPasteForm />, { wrapper: wrapper() });
    expect(screen.getByRole('button', { name: /Enqueue 0 IDs/i })).toBeDisabled();
  });

  it('shows valid + invalid badges from the paste', async () => {
    render(<BulkPasteForm />, { wrapper: wrapper() });
    await userEvent.type(screen.getByRole('textbox', { name: /BGG IDs/i }), '13, 42, foo');
    expect(screen.getByTestId('bgg-id-badge-13').getAttribute('data-state')).toBe('valid');
    expect(screen.getByTestId('bgg-id-badge-42').getAttribute('data-state')).toBe('valid');
    expect(screen.getByTestId('bgg-id-badge-foo').getAttribute('data-state')).toBe('invalid');
  });

  it('submits valid IDs via the bulk API call', async () => {
    vi.mocked(api.bulkEnqueueSeeds).mockResolvedValue({
      total: 2,
      enqueued: 2,
      duplicates: 0,
      newDraftIds: ['d1', 'd2'],
    });
    render(<BulkPasteForm />, { wrapper: wrapper() });
    await userEvent.type(screen.getByRole('textbox', { name: /BGG IDs/i }), '13, 42');
    await userEvent.click(screen.getByRole('button', { name: /Enqueue 2 IDs/i }));
    await waitFor(() => expect(api.bulkEnqueueSeeds).toHaveBeenCalledWith({ bggIds: [13, 42] }));
  });

  it('rejects > 100 IDs with an error message', async () => {
    render(<BulkPasteForm />, { wrapper: wrapper() });
    const ids = Array.from({ length: 101 }, (_, i) => i + 1).join('\n');
    await userEvent.click(screen.getByRole('textbox', { name: /BGG IDs/i }));
    // Use fireEvent-style direct set to avoid 101 keystrokes
    const textarea = screen.getByRole('textbox', { name: /BGG IDs/i }) as HTMLTextAreaElement;
    textarea.focus();
    // jsdom does not pump native input; use userEvent.paste for bulk insertion
    await userEvent.paste(ids);
    expect(await screen.findByText(/Batch cap exceeded/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Enqueue 101 IDs/i })).toBeDisabled();
  });
});
