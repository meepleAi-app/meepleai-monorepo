import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import OfflinePage from '../page';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it' }),
}));

const usePWAMock = vi.fn();
vi.mock('@/lib/domain-hooks/usePWA', () => ({
  usePWA: () => usePWAMock(),
}));

describe('OfflinePage', () => {
  let reloadSpy: ReturnType<typeof vi.fn>;
  let historyBackSpy: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    reloadSpy = vi.fn();
    historyBackSpy = vi.fn();
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: { ...window.location, reload: reloadSpy },
    });
    window.history.back = historyBackSpy;
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('renders localized offline content', () => {
    usePWAMock.mockReturnValue({ storageStats: null, isOnline: false });
    render(<OfflinePage />);
    expect(
      screen.getByRole('heading', { name: /pages\.errors\.offline\.title/ })
    ).toBeInTheDocument();
    expect(screen.getByText('pages.errors.offline.subtitle')).toBeInTheDocument();
  });

  it('retry CTA triggers window.location.reload', async () => {
    const user = userEvent.setup();
    usePWAMock.mockReturnValue({ storageStats: null, isOnline: false });
    render(<OfflinePage />);

    await user.click(screen.getByRole('button', { name: 'pages.errors.offline.retryCta' }));
    expect(reloadSpy).toHaveBeenCalledTimes(1);
  });

  it('renders stats section when storageStats is provided', () => {
    usePWAMock.mockReturnValue({
      storageStats: { sessions: 3, cachedGames: 5, pendingActions: 2 },
      isOnline: false,
    });
    render(<OfflinePage />);

    expect(screen.getByText('pages.errors.offline.statsTitle')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
  });

  it('does not render stats section when storageStats is null', () => {
    usePWAMock.mockReturnValue({ storageStats: null, isOnline: false });
    render(<OfflinePage />);
    expect(screen.queryByText('pages.errors.offline.statsTitle')).not.toBeInTheDocument();
  });

  it('auto-navigates back when isOnline becomes true', () => {
    usePWAMock.mockReturnValue({ storageStats: null, isOnline: true });
    render(<OfflinePage />);
    expect(historyBackSpy).toHaveBeenCalledTimes(1);
  });

  it('exposes a polite live region announcing the connection status', () => {
    usePWAMock.mockReturnValue({ storageStats: null, isOnline: false });
    render(<OfflinePage />);
    const live = screen.getByText('pages.errors.offline.offlineStatus');
    expect(live.closest('[role="status"]')).toHaveAttribute('aria-live', 'polite');
  });
});
