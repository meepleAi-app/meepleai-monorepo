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
  // BE returns an otpauth:// URI (TotpService.GenerateQrCodeUrl), not a PNG.
  // The wizard renders it via <QRCodeSVG>; the value is opaque to the test.
  qrCodeUrl: 'otpauth://totp/MeepleAI:test%40example.com?secret=JBSWY3DPEHPK3PXP&issuer=MeepleAI',
  // Backup codes are the SETUP endpoint's plaintext payload (TotpSetupResponse.BackupCodes).
  // They are the canonical source surfaced in the 'codes' step — the enable endpoint
  // does NOT re-emit them (see TwoFactorWizardBody onSuccess for rationale).
  backupCodes: ['SETUP-AAAA-1111', 'SETUP-BBBB-2222', 'SETUP-CCCC-3333'],
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
    const qrWrapper = screen.getByTestId('2fa-qr-code');
    expect(qrWrapper).toBeInTheDocument();
    // Regression guard: BE returns an otpauth:// URI, NOT a PNG. The wrapper must contain a
    // client-rendered <svg> from qrcode.react (not an <img> trying to fetch otpauth://).
    expect(qrWrapper.querySelector('svg')).not.toBeNull();
    expect(qrWrapper.querySelector('img')).toBeNull();
  });

  it('moves to verify step when Continue clicked', () => {
    wrap(<TwoFactorSetupModal open setupData={SETUP} onClose={() => {}} onEnabled={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: /continue/i }));
    expect(screen.getByText(/enter the code/i)).toBeInTheDocument();
  });

  it('calls enable2FA + moves to codes step on success (BE-emitted codes wins)', async () => {
    // Forward-compatibility path: when the BE eventually populates `backupCodes` on
    // enable, the wizard surfaces those (more authoritative than the setup snapshot).
    vi.mocked(api.auth.enable2FA).mockResolvedValue({
      success: true,
      backupCodes: ['ENABLE-AAAA-1111', 'ENABLE-BBBB-2222'],
      errorMessage: null,
    });
    wrap(<TwoFactorSetupModal open setupData={SETUP} onClose={() => {}} onEnabled={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: /continue/i }));
    // Type a full 6-digit code: simulate paste into the first input
    const inputs = screen.getAllByRole('textbox');
    fireEvent.paste(inputs[0], { clipboardData: { getData: () => '123456' } });
    await waitFor(() => expect(api.auth.enable2FA).toHaveBeenCalledWith('123456'));
    await waitFor(() => expect(screen.getByText(/recovery codes|save/i)).toBeInTheDocument());
    // Codes surfaced are the enable-response ones, not the setup snapshot.
    expect(screen.getByText('ENABLE-AAAA-1111')).toBeInTheDocument();
    expect(screen.queryByText('SETUP-AAAA-1111')).not.toBeInTheDocument();
  });

  it('falls back to setupData.backupCodes when enable2FA omits codes (current BE behavior)', async () => {
    // Regression guard for the 2026-05-28 hotfix: Enable2FACommandHandler returns
    // `Success: true` without populating `BackupCodes` (the persisted codes are PBKDF2
    // hashed at setup time, so the BE can't re-emit them on enable). Without this
    // fallback the wizard would land on the 'codes' step with an empty list — exactly
    // the bug Aaron hit during the SP5 S3 enrollment dogfood.
    vi.mocked(api.auth.enable2FA).mockResolvedValue({
      success: true,
      backupCodes: null,
      errorMessage: null,
    });
    wrap(<TwoFactorSetupModal open setupData={SETUP} onClose={() => {}} onEnabled={() => {}} />);
    fireEvent.click(screen.getByRole('button', { name: /continue/i }));
    const inputs = screen.getAllByRole('textbox');
    fireEvent.paste(inputs[0], { clipboardData: { getData: () => '123456' } });
    await waitFor(() => expect(api.auth.enable2FA).toHaveBeenCalledWith('123456'));
    await waitFor(() => expect(screen.getByText(/recovery codes|save/i)).toBeInTheDocument());
    // All three setupData codes must surface.
    expect(screen.getByText('SETUP-AAAA-1111')).toBeInTheDocument();
    expect(screen.getByText('SETUP-BBBB-2222')).toBeInTheDocument();
    expect(screen.getByText('SETUP-CCCC-3333')).toBeInTheDocument();
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
