import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import * as api from '../lib/catalog-seed-api';
import { SingleAddForm } from './SingleAddForm';

vi.mock('../lib/catalog-seed-api');
vi.mock('sonner', () => ({ toast: { success: vi.fn(), error: vi.fn(), info: vi.fn() } }));

function wrapper() {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={client}>{children}</QueryClientProvider>
  );
}

describe('SingleAddForm', () => {
  it('starts in BGG ID mode and blocks submit when empty', () => {
    render(<SingleAddForm />, { wrapper: wrapper() });
    expect(screen.getByLabelText(/^BGG ID$/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Enqueue draft/i })).toBeDisabled();
  });

  it('enables submit for a valid BGG ID', async () => {
    render(<SingleAddForm />, { wrapper: wrapper() });
    await userEvent.type(screen.getByLabelText(/^BGG ID$/i), '13');
    expect(screen.getByRole('button', { name: /Enqueue draft/i })).toBeEnabled();
  });

  it('rejects non-numeric BGG input', async () => {
    render(<SingleAddForm />, { wrapper: wrapper() });
    await userEvent.type(screen.getByLabelText(/^BGG ID$/i), 'abc');
    expect(screen.getByRole('button', { name: /Enqueue draft/i })).toBeDisabled();
  });

  it('switches to Wikidata QID mode and validates Q-prefix', async () => {
    render(<SingleAddForm />, { wrapper: wrapper() });
    await userEvent.click(screen.getByRole('button', { name: /Wikidata QID/i }));
    await userEvent.type(screen.getByLabelText(/^Wikidata QID$/i), 'q170416');
    expect(screen.getByRole('button', { name: /Enqueue draft/i })).toBeEnabled();
  });

  it('submits a BGG ID and resets the input', async () => {
    vi.mocked(api.enqueueSeed).mockResolvedValue({
      id: 'd1',
      bggId: 13,
      status: 'Pending',
      isDuplicate: false,
    });
    render(<SingleAddForm />, { wrapper: wrapper() });
    await userEvent.type(screen.getByLabelText(/^BGG ID$/i), '13');
    await userEvent.click(screen.getByRole('button', { name: /Enqueue draft/i }));
    await waitFor(() => expect(api.enqueueSeed).toHaveBeenCalledWith({ bggId: 13 }));
  });
});
