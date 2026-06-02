import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { describe, expect, it, vi, beforeEach } from 'vitest';

import { api } from '@/lib/api';

import { KbReindexDropdown } from '../KbReindexDropdown';

vi.mock('@/lib/api', () => ({
  api: {
    pdf: {
      reindexDocument: vi.fn(),
      getIndexerVersions: vi.fn(),
    },
  },
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

function makeWrapper() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={client}>{children}</QueryClientProvider>;
  };
}

describe('KbReindexDropdown', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.mocked(api.pdf.getIndexerVersions).mockResolvedValue([
      { version: 'v1.0', displayName: 'v1.0 — current pipeline', isCurrent: true },
    ]);
    vi.mocked(api.pdf.reindexDocument).mockResolvedValue(undefined);
  });

  it('renders the default reindex button (current version)', async () => {
    render(<KbReindexDropdown docId="abc" processingStatus="ready" />, {
      wrapper: makeWrapper(),
    });

    expect(await screen.findByRole('button', { name: /re-index/i })).toBeInTheDocument();
  });

  it('disables the trigger while processing/queued', () => {
    render(<KbReindexDropdown docId="abc" processingStatus="processing" />, {
      wrapper: makeWrapper(),
    });

    expect(screen.getByRole('button', { name: /re-index/i })).toBeDisabled();
  });

  it('opens the version menu and triggers reindex with selected version', async () => {
    const user = userEvent.setup();
    render(<KbReindexDropdown docId="abc" processingStatus="ready" />, {
      wrapper: makeWrapper(),
    });

    await user.click(await screen.findByRole('button', { name: /scegli versione/i }));
    await user.click(await screen.findByRole('menuitem', { name: /v1\.0/i }));

    await waitFor(() =>
      expect(api.pdf.reindexDocument).toHaveBeenCalledWith('abc', { indexerVersion: 'v1.0' })
    );
  });

  it('default click reindexes without explicit version (server uses Current)', async () => {
    const user = userEvent.setup();
    render(<KbReindexDropdown docId="abc" processingStatus="ready" />, {
      wrapper: makeWrapper(),
    });

    await user.click(await screen.findByRole('button', { name: /^⟳ re-index$/i }));

    await waitFor(() => expect(api.pdf.reindexDocument).toHaveBeenCalledWith('abc', undefined));
  });
});
