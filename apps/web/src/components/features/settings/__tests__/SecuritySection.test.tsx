import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { SecuritySection } from '../sections/SecuritySection';
import { api } from '@/lib/api';

vi.mock('@/lib/api', () => ({
  api: {
    auth: {
      getTwoFactorStatus: vi.fn(),
      setup2FA: vi.fn(),
      enable2FA: vi.fn(),
      disable2FA: vi.fn(),
      getUserSessions: vi.fn().mockResolvedValue([]),
      revokeSession: vi.fn(),
      revokeAllSessions: vi.fn(),
      getSessionStatus: vi.fn().mockResolvedValue(null),
    },
  },
}));

function wrap(ui: React.ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

beforeEach(() => {
  vi.mocked(api.auth.getTwoFactorStatus).mockResolvedValue({
    isEnabled: false,
    enabledAt: null,
    unusedBackupCodesCount: 0,
  });
  vi.mocked(api.auth.setup2FA).mockResolvedValue({
    secret: 'JBSWY3DPEHPK3PXP',
    qrCodeUrl: 'data:image/png;base64,AAAA',
    backupCodes: [],
  });
});

describe('SecuritySection', () => {
  it('renders 2FA status card + active sessions card', async () => {
    wrap(<SecuritySection />);
    await waitFor(() => expect(screen.getByTestId('2fa-status')).toBeInTheDocument());
    expect(screen.getByText(/active sessions/i)).toBeInTheDocument();
  });

  it('opens the setup wizard when Enable 2FA is clicked', async () => {
    wrap(<SecuritySection />);
    await waitFor(() => screen.getByTestId('enable-2fa'));
    fireEvent.click(screen.getByTestId('enable-2fa'));
    await waitFor(() => expect(api.auth.setup2FA).toHaveBeenCalled());
    await waitFor(() => expect(screen.getByTestId('2fa-qr-code')).toBeInTheDocument());
  });

  it('opens the disable dialog when Disable 2FA is clicked (2FA enabled)', async () => {
    vi.mocked(api.auth.getTwoFactorStatus).mockResolvedValue({
      isEnabled: true,
      enabledAt: '2026-01-01T00:00:00Z',
      unusedBackupCodesCount: 10,
    });
    wrap(<SecuritySection />);
    await waitFor(() => screen.getByTestId('disable-2fa'));
    fireEvent.click(screen.getByTestId('disable-2fa'));
    await waitFor(() => expect(screen.getByTestId('disable-2fa-confirm')).toBeInTheDocument());
  });
});
