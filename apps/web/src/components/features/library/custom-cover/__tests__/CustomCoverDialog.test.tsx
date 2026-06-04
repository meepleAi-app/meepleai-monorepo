import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi } from 'vitest';
import React from 'react';

import { CustomCoverDialog } from '../CustomCoverDialog';

vi.mock('../CropDialog', () => ({
  CropDialog: ({ onConfirm }: { onConfirm: (blob: Blob) => void }) => (
    <div data-testid="mock-crop-dialog">
      <button
        onClick={() => onConfirm(new Blob([new ArrayBuffer(150_000)], { type: 'image/webp' }))}
      >
        confirm-crop
      </button>
    </div>
  ),
}));

vi.mock('@/hooks/mutations/useCustomCoverUpload', () => ({
  useCustomCoverUpload: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
}));

vi.mock('@/hooks/mutations/useRemoveCustomCover', () => ({
  useRemoveCustomCover: () => ({ mutate: vi.fn(), isPending: false, isError: false }),
}));

function wrapper({ children }: { children: React.ReactNode }) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return React.createElement(QueryClientProvider, { client: queryClient }, children);
}

describe('CustomCoverDialog', () => {
  it('renders file input when open', () => {
    render(<CustomCoverDialog gameId="g" open={true} onClose={() => {}} hasCustomCover={false} />, {
      wrapper,
    });
    expect(screen.getByLabelText(/seleziona foto/i)).toBeInTheDocument();
  });

  it('rejects file > 10MB', async () => {
    render(<CustomCoverDialog gameId="g" open={true} onClose={() => {}} hasCustomCover={false} />, {
      wrapper,
    });
    const input = screen.getByLabelText(/seleziona foto/i) as HTMLInputElement;
    const bigFile = new File([new ArrayBuffer(11 * 1024 * 1024)], 'big.jpg', {
      type: 'image/jpeg',
    });
    fireEvent.change(input, { target: { files: [bigFile] } });
    await waitFor(() => expect(screen.getByText(/troppo grande/i)).toBeInTheDocument());
  });

  it('shows remove button when hasCustomCover=true', () => {
    render(<CustomCoverDialog gameId="g" open={true} onClose={() => {}} hasCustomCover={true} />, {
      wrapper,
    });
    expect(
      screen.getByRole('button', { name: /rimuovi copertina personalizzata/i })
    ).toBeInTheDocument();
  });
});
