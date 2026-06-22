import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { TwoFactorBottomSheet } from '../two-factor/TwoFactorBottomSheet';
import { api } from '@/lib/api';

vi.mock('@/lib/api', () => ({ api: { auth: { enable2FA: vi.fn() } } }));

const SETUP = {
  secret: 'JBSWY3DPEHPK3PXP',
  qrCodeUrl: 'data:image/png;base64,AAAA',
  backupCodes: [],
};

function wrap(ui: React.ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

beforeEach(() => vi.mocked(api.auth.enable2FA).mockReset());

describe('TwoFactorBottomSheet', () => {
  it('renders the wizard QR (step 1) when open', () => {
    wrap(<TwoFactorBottomSheet open setupData={SETUP} onClose={() => {}} onEnabled={() => {}} />);
    expect(screen.getByTestId('2fa-qr-code')).toBeInTheDocument();
  });

  it('advances to verify step and calls enable2FA on success', async () => {
    vi.mocked(api.auth.enable2FA).mockResolvedValue({
      success: true,
      backupCodes: ['A-1'],
      errorMessage: null,
    });
    wrap(<TwoFactorBottomSheet open setupData={SETUP} onClose={() => {}} onEnabled={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: /continue/i }));
    const inputs = screen.getAllByRole('textbox');
    fireEvent.paste(inputs[0], { clipboardData: { getData: () => '123456' } });
    await waitFor(() => expect(api.auth.enable2FA).toHaveBeenCalledWith('123456'));
  });
});
