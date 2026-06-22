/**
 * @vitest-environment jsdom
 *
 * StatusBanner component tests — Issue #1089.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

import itMessages from '@/locales/it.json';
import { flattenMessages } from '@/locales';
import { useDismissedBannersStore } from '@/lib/stores/dismissed-banners-store';

import { StatusBanner, StatusBannerView } from '../StatusBanner';
import type { PublicStatusBannerResponse } from '@/lib/api/schemas/status-banner.schemas';

vi.mock('@/lib/api/client', () => ({
  apiClient: {
    get: vi.fn(),
  },
}));

import { apiClient } from '@/lib/api/client';

const mockGet = apiClient.get as ReturnType<typeof vi.fn>;

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

const INFO_BANNER: PublicStatusBannerResponse = {
  messageId: 'info-1',
  message: 'Informational notice',
  severity: 'Info',
  updatedAt: '2026-05-17T10:00:00Z',
};
const WARNING_BANNER: PublicStatusBannerResponse = {
  ...INFO_BANNER,
  messageId: 'warn-1',
  severity: 'Warning',
  message: 'Degradation in progress',
};
const CRITICAL_BANNER: PublicStatusBannerResponse = {
  ...INFO_BANNER,
  messageId: 'crit-1',
  severity: 'Critical',
  message: 'Service unavailable',
};

describe('StatusBannerView (pure)', () => {
  it('renders Info banner with role=status and aria-live=polite', () => {
    renderWithProviders(<StatusBannerView banner={INFO_BANNER} onDismiss={() => {}} />);
    const el = screen.getByRole('status');
    expect(el).toHaveTextContent('Informational notice');
    expect(el).toHaveAttribute('aria-live', 'polite');
    expect(el).toHaveAttribute('data-severity', 'Info');
  });

  it('renders Warning banner with role=status and aria-live=polite', () => {
    renderWithProviders(<StatusBannerView banner={WARNING_BANNER} onDismiss={() => {}} />);
    const el = screen.getByRole('status');
    expect(el).toHaveAttribute('data-severity', 'Warning');
    expect(el).toHaveTextContent('Degradation in progress');
  });

  it('renders Critical banner with role=alert and aria-live=assertive', () => {
    renderWithProviders(<StatusBannerView banner={CRITICAL_BANNER} onDismiss={() => {}} />);
    const el = screen.getByRole('alert');
    expect(el).toHaveAttribute('aria-live', 'assertive');
    expect(el).toHaveAttribute('data-severity', 'Critical');
  });

  it('renders dismiss button for Info / Warning banners', () => {
    const onDismiss = vi.fn();
    renderWithProviders(<StatusBannerView banner={INFO_BANNER} onDismiss={onDismiss} />);
    expect(screen.getByRole('button')).toBeInTheDocument();
  });

  it('does NOT render dismiss button for Critical banners', () => {
    renderWithProviders(<StatusBannerView banner={CRITICAL_BANNER} onDismiss={() => {}} />);
    expect(screen.queryByRole('button')).toBeNull();
  });

  it('dismiss button is keyboard activatable', async () => {
    const user = userEvent.setup();
    const onDismiss = vi.fn();
    renderWithProviders(<StatusBannerView banner={WARNING_BANNER} onDismiss={onDismiss} />);
    const btn = screen.getByRole('button');
    btn.focus();
    await user.keyboard('{Enter}');
    expect(onDismiss).toHaveBeenCalled();
  });

  it('dismiss button click invokes onDismiss', async () => {
    const user = userEvent.setup();
    const onDismiss = vi.fn();
    renderWithProviders(<StatusBannerView banner={INFO_BANNER} onDismiss={onDismiss} />);
    await user.click(screen.getByRole('button'));
    expect(onDismiss).toHaveBeenCalledTimes(1);
  });

  it('exposes messageId via data attribute for analytics', () => {
    renderWithProviders(<StatusBannerView banner={INFO_BANNER} onDismiss={() => {}} />);
    const el = screen.getByRole('status');
    expect(el).toHaveAttribute('data-message-id', 'info-1');
  });
});

describe('StatusBanner (connected)', () => {
  beforeEach(() => {
    mockGet.mockReset();
    window.localStorage.clear();
    useDismissedBannersStore.getState().reset();
  });

  it('renders nothing when backend returns null (204)', async () => {
    mockGet.mockResolvedValue(null);
    const { container } = renderWithProviders(<StatusBanner />);
    // Wait a tick for React Query to settle.
    await Promise.resolve();
    expect(container.querySelector('[data-slot="status-banner"]')).toBeNull();
  });

  it('renders the banner when backend returns an active banner', async () => {
    mockGet.mockResolvedValue(INFO_BANNER);
    renderWithProviders(<StatusBanner />);
    expect(await screen.findByRole('status')).toHaveTextContent('Informational notice');
  });

  it('hides the banner after dismissal and persists across renders', async () => {
    const user = userEvent.setup();
    mockGet.mockResolvedValue(WARNING_BANNER);
    const { container } = renderWithProviders(<StatusBanner />);
    const banner = await screen.findByRole('status');
    expect(banner).toBeInTheDocument();
    await user.click(screen.getByRole('button'));

    // After dismissal the banner disappears (wait for re-render).
    await waitFor(() => expect(container.querySelector('[data-slot="status-banner"]')).toBeNull());

    // Store records the dismissed id — survives reloads (persisted in LS).
    expect(useDismissedBannersStore.getState().isDismissed('warn-1')).toBe(true);
  });

  it('renders Critical without dismiss control even if dismissed for another id', async () => {
    useDismissedBannersStore.getState().dismiss('warn-1');
    mockGet.mockResolvedValue(CRITICAL_BANNER);
    renderWithProviders(<StatusBanner />);
    const el = await screen.findByRole('alert');
    expect(el).toBeInTheDocument();
    expect(screen.queryByRole('button')).toBeNull();
  });
});
