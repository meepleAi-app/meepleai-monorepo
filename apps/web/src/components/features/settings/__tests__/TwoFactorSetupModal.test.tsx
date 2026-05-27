import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { TwoFactorSetupModal } from '../two-factor/TwoFactorSetupModal';
import { api } from '@/lib/api';

vi.mock('@/lib/api', () => ({
  api: { auth: { enable2FA: vi.fn() } },
}));

const SETUP = {
  secret: 'JBSWY3DPEHPK3PXP',
  qrCodeUrl: 'data:image/png;base64,AAAA',
  backupCodes: [],
};

function wrap(ui: React.ReactNode) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

beforeEach(() => {
  vi.mocked(api.auth.enable2FA).mockReset();
});

describe('TwoFactorSetupModal', () => {
  it('renders QR with data-testid="2fa-qr-code" at step 1 (setup)', () => {
    wrap(<TwoFactorSetupModal open setupData={SETUP} onClose={() => {}} onEnabled={() => {}} />);
    expect(screen.getByTestId('2fa-qr-code')).toBeInTheDocument();
  });

  it('moves to verify step when Continue clicked', () => {
    wrap(<TwoFactorSetupModal open setupData={SETUP} onClose={() => {}} onEnabled={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: /continue/i }));
    expect(screen.getByText(/enter the code/i)).toBeInTheDocument();
  });

  it('calls enable2FA + moves to codes step on success', async () => {
    vi.mocked(api.auth.enable2FA).mockResolvedValue({
      success: true,
      backupCodes: ['AAAA-1111', 'BBBB-2222'],
      errorMessage: null,
    });
    wrap(<TwoFactorSetupModal open setupData={SETUP} onClose={() => {}} onEnabled={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: /continue/i }));
    // Type a full 6-digit code: simulate paste into the first input
    const inputs = screen.getAllByRole('textbox');
    fireEvent.paste(inputs[0], { clipboardData: { getData: () => '123456' } });
    await waitFor(() => expect(api.auth.enable2FA).toHaveBeenCalledWith('123456'));
    await waitFor(() => expect(screen.getByText(/recovery codes|save/i)).toBeInTheDocument());
  });

  it('shows OTP error when enable2FA returns success:false (I4)', async () => {
    vi.mocked(api.auth.enable2FA).mockResolvedValue({
      success: false,
      backupCodes: null,
      errorMessage: 'Invalid code',
    });
    wrap(<TwoFactorSetupModal open setupData={SETUP} onClose={() => {}} onEnabled={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: /continue/i }));
    const inputs = screen.getAllByRole('textbox');
    fireEvent.paste(inputs[0], { clipboardData: { getData: () => '000000' } });
    await waitFor(() => expect(api.auth.enable2FA).toHaveBeenCalled());
    await waitFor(() => expect(screen.getByText(/invalid code|try again/i)).toBeInTheDocument());
    // OTPInput6Slot in error state
    expect(screen.getByRole('group')).toHaveClass('animate-shake');
  });
});
