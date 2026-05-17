/**
 * @vitest-environment jsdom
 *
 * StatusBannerAdmin component tests — Issue #1089.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

import itMessages from '@/locales/it.json';
import { flattenMessages } from '@/locales';

import { StatusBannerAdmin } from '../StatusBannerAdmin';

vi.mock('@/lib/api/client', () => ({
  apiClient: {
    get: vi.fn(),
    put: vi.fn(),
  },
}));

vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

import { apiClient } from '@/lib/api/client';
import { toast } from 'sonner';

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;
const mockPut = apiClient.put as ReturnType<typeof vi.fn>;
const mockToastSuccess = toast.success as ReturnType<typeof vi.fn>;
const mockToastError = toast.error as ReturnType<typeof vi.fn>;

const FLAT_MESSAGES = flattenMessages(itMessages as Record<string, unknown>);

function renderWithProviders(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <IntlProvider locale="it" messages={FLAT_MESSAGES}>
        {ui}
      </IntlProvider>
    </QueryClientProvider>
  );
}

const SAMPLE_ADMIN = {
  message: 'Currently online',
  severity: 'Info' as const,
  isActive: false,
  startsAt: null,
  endsAt: null,
  updatedAt: '2026-05-17T08:00:00Z',
  updatedBy: 'admin@example.com',
};

describe('StatusBannerAdmin', () => {
  beforeEach(() => {
    mockGet.mockReset();
    mockPut.mockReset();
    mockToastSuccess.mockReset();
    mockToastError.mockReset();
  });

  it('shows loading state initially', async () => {
    mockGet.mockReturnValue(new Promise(() => {})); // never resolves
    renderWithProviders(<StatusBannerAdmin />);
    expect(
      await screen.findByText(
        (_t, node) => node?.getAttribute('data-slot') === 'status-banner-admin-loading'
      )
    ).toBeInTheDocument();
  });

  it('renders the form pre-populated from the read query', async () => {
    mockGet.mockResolvedValue(SAMPLE_ADMIN);
    renderWithProviders(<StatusBannerAdmin />);
    const textarea = (await screen.findByLabelText(/messaggio/i)) as HTMLTextAreaElement;
    expect(textarea.value).toBe('Currently online');
  });

  it('template buttons populate the form', async () => {
    const user = userEvent.setup();
    mockGet.mockResolvedValue({ ...SAMPLE_ADMIN, message: '', isActive: false });
    renderWithProviders(<StatusBannerAdmin />);
    await screen.findByLabelText(/messaggio/i);

    const investigatingBtn = screen.getByRole('button', { name: /incidente in corso/i });
    await user.click(investigatingBtn);

    const textarea = screen.getByLabelText(/messaggio/i) as HTMLTextAreaElement;
    expect(textarea.value).toMatch(/investigating an issue/i);
  });

  it('submitting calls PUT with the form values', async () => {
    const user = userEvent.setup();
    mockGet.mockResolvedValue(SAMPLE_ADMIN);
    mockPut.mockResolvedValue({ ...SAMPLE_ADMIN, isActive: true });
    renderWithProviders(<StatusBannerAdmin />);
    await screen.findByLabelText(/messaggio/i);

    await user.click(screen.getByRole('button', { name: /salva banner/i }));

    await waitFor(() => expect(mockPut).toHaveBeenCalled());
    const [path, body] = mockPut.mock.calls[0];
    expect(path).toBe('/api/v1/admin/status-banner');
    expect(body).toMatchObject({
      message: 'Currently online',
      severity: 'Info',
      isActive: false,
    });
  });

  it('shows success toast on successful save', async () => {
    const user = userEvent.setup();
    mockGet.mockResolvedValue(SAMPLE_ADMIN);
    mockPut.mockResolvedValue(SAMPLE_ADMIN);
    renderWithProviders(<StatusBannerAdmin />);
    await screen.findByLabelText(/messaggio/i);
    await user.click(screen.getByRole('button', { name: /salva banner/i }));
    await waitFor(() => expect(mockToastSuccess).toHaveBeenCalled());
  });

  it('shows error toast on failed save', async () => {
    const user = userEvent.setup();
    mockGet.mockResolvedValue(SAMPLE_ADMIN);
    mockPut.mockRejectedValue(new Error('boom'));
    renderWithProviders(<StatusBannerAdmin />);
    await screen.findByLabelText(/messaggio/i);
    await user.click(screen.getByRole('button', { name: /salva banner/i }));
    await waitFor(() => expect(mockToastError).toHaveBeenCalled());
  });

  it('disables submit when message is empty', async () => {
    const user = userEvent.setup();
    mockGet.mockResolvedValue({ ...SAMPLE_ADMIN, message: '' });
    renderWithProviders(<StatusBannerAdmin />);
    const textarea = (await screen.findByLabelText(/messaggio/i)) as HTMLTextAreaElement;
    await user.clear(textarea);
    const submit = screen.getByRole('button', { name: /salva banner/i });
    expect(submit).toBeDisabled();
  });
});
