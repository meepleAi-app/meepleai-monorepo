import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ApiKeysSection } from '../sections/ApiKeysSection';
import { api } from '@/lib/api';

vi.mock('@/lib/api', () => ({
  api: { auth: { listApiKeys: vi.fn(), createApiKey: vi.fn(), revokeApiKey: vi.fn() } },
}));

function wrap(ui: React.ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

beforeEach(() => {
  vi.mocked(api.auth.listApiKeys).mockResolvedValue({
    items: [
      {
        id: 'k1',
        keyName: 'CI token',
        keyPrefix: 'mai_live_ab',
        scopes: 'read',
        createdAt: '2026-01-01T00:00:00Z',
        expiresAt: null,
        lastUsedAt: null,
        isActive: true,
      },
    ],
    total: 1,
    page: 1,
    pageSize: 20,
  } as any);
  vi.mocked(api.auth.createApiKey).mockResolvedValue({
    id: 'k2',
    keyName: 'New key',
    keyPrefix: 'mai_live_xy',
    plaintextKey: 'mai_live_xyFULLSECRET123',
    scopes: 'read',
    createdAt: '2026-01-02T00:00:00Z',
    expiresAt: null,
  } as any);
  vi.mocked(api.auth.revokeApiKey).mockResolvedValue(undefined);
});

describe('ApiKeysSection', () => {
  it('lists existing keys by prefix (never the full key)', async () => {
    wrap(<ApiKeysSection />);
    await waitFor(() => expect(screen.getByText('CI token')).toBeInTheDocument());
    expect(screen.getByText(/mai_live_ab/)).toBeInTheDocument();
  });

  it('creates a key and reveals plaintextKey once in a copy dialog', async () => {
    wrap(<ApiKeysSection />);
    await waitFor(() => screen.getByText('CI token'));
    // fill the create form
    fireEvent.change(screen.getByTestId('api-key-name-input'), { target: { value: 'New key' } });
    fireEvent.click(screen.getByTestId('create-api-key-button'));
    await waitFor(() => expect(api.auth.createApiKey).toHaveBeenCalled());
    // plaintext shown once
    await waitFor(() =>
      expect(screen.getByTestId('api-key-plaintext')).toHaveTextContent('mai_live_xyFULLSECRET123')
    );
  });

  it('revokes a key', async () => {
    wrap(<ApiKeysSection />);
    await waitFor(() => screen.getByText('CI token'));
    fireEvent.click(screen.getByRole('button', { name: /revoke/i }));
    await waitFor(() => expect(api.auth.revokeApiKey).toHaveBeenCalledWith('k1'));
  });
});
