/**
 * Unit tests for Settings Page (AUTH-07)
 *
 * Tests cover:
 * - 2FA management (enable, QR code, backup codes, disable)
 * - TOTP verification and backup code flow
 * - Password validation and confirmation
 * - Loading states and error handling
 * - User interactions and confirmations
 * - Success/error alerts
 * - QR code generation and manual entry
 * - Backup code download functionality
 * - Accessibility
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useRouter } from 'next/router';
import SettingsPage from '../../pages/settings';
import { createMockRouter } from '../fixtures/common-fixtures';

// Mock Next.js router
jest.mock('next/router', () => ({
  useRouter: jest.fn(),
}));

// Mock QRCode.react
jest.mock('qrcode.react', () => ({
  QRCodeSVG: ({ value, size }: { value: string; size: number }) => (
    <svg data-testid="qr-code" data-value={value} width={size} height={size}>
      <title>QR Code</title>
    </svg>
  ),
}));

// Mock API client
jest.mock('@/lib/api', () => ({
  api: {
    twoFactor: {
      getStatus: jest.fn(),
      setup: jest.fn(),
      enable: jest.fn(),
      verify: jest.fn(),
      disable: jest.fn(),
    },
  },
}));

// Import the mocked api to get typed access
import { api } from '@/lib/api';
const mockTwoFactorApi = api.twoFactor as jest.Mocked<typeof api.twoFactor>;

const useRouterMock = useRouter as jest.MockedFunction<typeof useRouter>;

// Mock confirm and alert
const mockConfirm = jest.fn();
const mockAlert = jest.fn();
global.confirm = mockConfirm;
global.alert = mockAlert;

// Mock URL.createObjectURL and revokeObjectURL
global.URL.createObjectURL = jest.fn(() => 'blob:mock-url');
global.URL.revokeObjectURL = jest.fn();

// Test data
const mock2FAStatusDisabled = {
  isTwoFactorEnabled: false,
  backupCodesCount: 0,
};

const mock2FAStatusEnabled = {
  isTwoFactorEnabled: true,
  backupCodesCount: 8,
};

const mock2FAStatusLowBackupCodes = {
  isTwoFactorEnabled: true,
  backupCodesCount: 2,
};

const mockTotpSetup = {
  secret: 'JBSWY3DPEHPK3PXP',
  qrCodeUri: 'otpauth://totp/MeepleAI:user@example.com?secret=JBSWY3DPEHPK3PXP&issuer=MeepleAI',
  backupCodes: [
    'AAAA-BBBB',
    'CCCC-DDDD',
    'EEEE-FFFF',
    'GGGG-HHHH',
    'IIII-JJJJ',
    'KKKK-LLLL',
    'MMMM-NNNN',
    'OOOO-PPPP',
    'QQQQ-RRRR',
    'SSSS-TTTT',
  ],
};

describe('SettingsPage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockConfirm.mockClear();
    mockAlert.mockClear();
    mockTwoFactorApi.getStatus.mockClear();
    mockTwoFactorApi.setup.mockClear();
    mockTwoFactorApi.enable.mockClear();
    mockTwoFactorApi.verify.mockClear();
    mockTwoFactorApi.disable.mockClear();
    useRouterMock.mockReturnValue(createMockRouter({ pathname: '/settings' }));
  });

  describe('Initial Loading', () => {
    it('displays loading state initially', () => {
      mockTwoFactorApi.getStatus.mockReturnValue(new Promise(() => {})); // Never resolves

      render(<SettingsPage />);

      expect(screen.getByText('Loading...')).toBeInTheDocument();
    });

    it('loads 2FA status on mount', async () => {
      mockTwoFactorApi.getStatus.mockResolvedValue(mock2FAStatusDisabled);

      render(<SettingsPage />);

      await waitFor(() => {
        expect(mockTwoFactorApi.getStatus).toHaveBeenCalledTimes(1);
      });
    });

    it('hides loading after status loads', async () => {
      mockTwoFactorApi.getStatus.mockResolvedValue(mock2FAStatusDisabled);

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.queryByText('Loading...')).not.toBeInTheDocument();
      });
    });

    it('displays error message when status fetch fails', async () => {
      const consoleError = jest.spyOn(console, 'error').mockImplementation();
      mockTwoFactorApi.getStatus.mockRejectedValue(new Error('Network error'));

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Failed to load 2FA status')).toBeInTheDocument();
      });

      consoleError.mockRestore();
    });
  });

  describe('Page Structure', () => {
    beforeEach(() => {
      mockTwoFactorApi.getStatus.mockResolvedValue(mock2FAStatusDisabled);
    });

    it('renders page title', async () => {
      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });
    });

    it('renders "Two-Factor Authentication" section', async () => {
      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Two-Factor Authentication')).toBeInTheDocument();
      });
    });

    it('renders "Back to Home" button', async () => {
      render(<SettingsPage />);

      await waitFor(() => {
        const backButton = screen.getByRole('button', { name: /Back to Home/i });
        expect(backButton).toBeInTheDocument();
      });
    });

    it('navigates to home when Back button clicked', async () => {
      const user = userEvent.setup();
      const mockRouter = createMockRouter({ pathname: '/settings' });
      useRouterMock.mockReturnValue(mockRouter);

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const backButton = screen.getByRole('button', { name: /Back to Home/i });
      await user.click(backButton);

      expect(mockRouter.push).toHaveBeenCalledWith('/');
    });
  });

  describe('2FA Disabled State', () => {
    beforeEach(() => {
      mockTwoFactorApi.getStatus.mockResolvedValue(mock2FAStatusDisabled);
    });

    it('displays description about 2FA', async () => {
      render(<SettingsPage />);

      await waitFor(() => {
        expect(
          screen.getByText(/Two-factor authentication adds an extra layer of security/i)
        ).toBeInTheDocument();
      });
    });

    it('renders "Enable Two-Factor Authentication" button', async () => {
      render(<SettingsPage />);

      await waitFor(() => {
        expect(
          screen.getByRole('button', { name: /Enable Two-Factor Authentication/i })
        ).toBeInTheDocument();
      });
    });

    it('initiates 2FA setup when enable button clicked', async () => {
      const user = userEvent.setup();
      mockTwoFactorApi.setup.mockResolvedValue(mockTotpSetup);

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        expect(mockTwoFactorApi.setup).toHaveBeenCalledTimes(1);
      });
    });

    it('shows loading state during setup', async () => {
      const user = userEvent.setup();
      mockTwoFactorApi.setup.mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve(mockTotpSetup), 100))
      );

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      expect(screen.getByText('Setting up...')).toBeInTheDocument();
    });

    it('handles setup error', async () => {
      const consoleError = jest.spyOn(console, 'error').mockImplementation();
      const user = userEvent.setup();
      mockTwoFactorApi.setup.mockRejectedValue(new Error('Setup failed'));

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        expect(screen.getByText('Failed to setup 2FA')).toBeInTheDocument();
      });

      consoleError.mockRestore();
    });
  });

  describe('2FA Setup Flow', () => {
    beforeEach(() => {
      mockTwoFactorApi.getStatus.mockResolvedValue(mock2FAStatusDisabled);
      mockTwoFactorApi.setup.mockResolvedValue(mockTotpSetup);
    });

    it('displays QR code after setup', async () => {
      const user = userEvent.setup();
      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        const qrCode = screen.getByTestId('qr-code');
        expect(qrCode).toBeInTheDocument();
        expect(qrCode).toHaveAttribute('data-value', mockTotpSetup.qrCodeUri);
      });
    });

    it('displays manual entry secret in details', async () => {
      const user = userEvent.setup();
      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        expect(screen.getByText(/Can't scan QR code\? Enter manually/i)).toBeInTheDocument();
      });

      // Expand details
      const detailsSummary = screen.getByText(/Can't scan QR code\? Enter manually/i);
      await user.click(detailsSummary);

      expect(screen.getByText(mockTotpSetup.secret)).toBeInTheDocument();
    });

    it('displays backup codes after setup', async () => {
      const user = userEvent.setup();
      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        mockTotpSetup.backupCodes.forEach((code) => {
          expect(screen.getByText(code)).toBeInTheDocument();
        });
      });
    });

    it('downloads backup codes as text file', async () => {
      const user = userEvent.setup();
      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        expect(screen.getByText('Download Codes')).toBeInTheDocument();
      });

      // Mock document.createElement
      const mockAnchor = document.createElement('a');
      const createElementSpy = jest
        .spyOn(document, 'createElement')
        .mockReturnValue(mockAnchor);
      const clickSpy = jest.spyOn(mockAnchor, 'click').mockImplementation();

      const downloadButton = screen.getByRole('button', { name: 'Download Codes' });
      await user.click(downloadButton);

      expect(URL.createObjectURL).toHaveBeenCalled();
      expect(mockAnchor.download).toBe('meepleai-backup-codes.txt');
      expect(clickSpy).toHaveBeenCalled();
      expect(URL.revokeObjectURL).toHaveBeenCalled();

      createElementSpy.mockRestore();
      clickSpy.mockRestore();
    });

    it('hides backup codes when "I\'ve Saved My Codes" clicked', async () => {
      const user = userEvent.setup();
      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        expect(screen.getByText('Step 2: Save Backup Codes')).toBeInTheDocument();
      });

      const savedButton = screen.getByRole('button', { name: /I've Saved My Codes/i });
      await user.click(savedButton);

      expect(screen.queryByText('Step 2: Save Backup Codes')).not.toBeInTheDocument();
      expect(screen.getByText('Step 3: Verify & Enable')).toBeInTheDocument();
    });

    it('renders verification code input', async () => {
      const user = userEvent.setup();
      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        expect(screen.getByText('Step 2: Save Backup Codes')).toBeInTheDocument();
      });

      const savedButton = screen.getByRole('button', { name: /I've Saved My Codes/i });
      await user.click(savedButton);

      const verificationInput = screen.getByPlaceholderText('000000');
      expect(verificationInput).toBeInTheDocument();
      expect(verificationInput).toHaveAttribute('type', 'text');
      expect(verificationInput).toHaveAttribute('maxlength', '6');
    });

    it('formats verification code to digits only', async () => {
      const user = userEvent.setup();
      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        expect(screen.getByText('Step 2: Save Backup Codes')).toBeInTheDocument();
      });

      const savedButton = screen.getByRole('button', { name: /I've Saved My Codes/i });
      await user.click(savedButton);

      const verificationInput = screen.getByPlaceholderText('000000');
      await user.type(verificationInput, 'abc123xyz456');

      expect(verificationInput).toHaveValue('123456'); // Only digits, max 6
    });

    it('disables verify button when code length is not 6', async () => {
      const user = userEvent.setup();
      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        expect(screen.getByText('Step 2: Save Backup Codes')).toBeInTheDocument();
      });

      const savedButton = screen.getByRole('button', { name: /I've Saved My Codes/i });
      await user.click(savedButton);

      const verifyButton = screen.getByRole('button', { name: /Verify & Enable/i });
      expect(verifyButton).toBeDisabled();

      const verificationInput = screen.getByPlaceholderText('000000');
      await user.type(verificationInput, '12345');

      expect(verifyButton).toBeDisabled();

      await user.type(verificationInput, '6');
      expect(verifyButton).not.toBeDisabled();
    });

    it('enables 2FA with valid code', async () => {
      const user = userEvent.setup();
      mockTwoFactorApi.enable.mockResolvedValue(undefined);
      mockTwoFactorApi.getStatus.mockResolvedValueOnce(mock2FAStatusDisabled);
      mockTwoFactorApi.getStatus.mockResolvedValueOnce(mock2FAStatusEnabled);

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        expect(screen.getByText('Step 2: Save Backup Codes')).toBeInTheDocument();
      });

      const savedButton = screen.getByRole('button', { name: /I've Saved My Codes/i });
      await user.click(savedButton);

      const verificationInput = screen.getByPlaceholderText('000000');
      await user.type(verificationInput, '123456');

      const verifyButton = screen.getByRole('button', { name: /Verify & Enable/i });
      await user.click(verifyButton);

      await waitFor(() => {
        expect(mockTwoFactorApi.enable).toHaveBeenCalledWith('123456');
        expect(mockAlert).toHaveBeenCalledWith('Two-factor authentication enabled successfully!');
      });
    });

    it('shows error with invalid verification code', async () => {
      const consoleError = jest.spyOn(console, 'error').mockImplementation();
      const user = userEvent.setup();
      mockTwoFactorApi.enable.mockRejectedValue(new Error('Invalid code'));

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        expect(screen.getByText('Step 2: Save Backup Codes')).toBeInTheDocument();
      });

      const savedButton = screen.getByRole('button', { name: /I've Saved My Codes/i });
      await user.click(savedButton);

      const verificationInput = screen.getByPlaceholderText('000000');
      await user.type(verificationInput, '000000');

      const verifyButton = screen.getByRole('button', { name: /Verify & Enable/i });
      await user.click(verifyButton);

      await waitFor(() => {
        expect(screen.getByText('Invalid verification code. Please try again.')).toBeInTheDocument();
      });

      consoleError.mockRestore();
    });

    it('cancels setup and returns to initial state', async () => {
      const user = userEvent.setup();
      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        expect(screen.getByText('Step 1: Scan QR Code')).toBeInTheDocument();
      });

      const cancelButton = screen.getByRole('button', { name: 'Cancel Setup' });
      await user.click(cancelButton);

      expect(screen.queryByText('Step 1: Scan QR Code')).not.toBeInTheDocument();
      expect(
        screen.getByRole('button', { name: /Enable Two-Factor Authentication/i })
      ).toBeInTheDocument();
    });
  });

  describe('2FA Enabled State', () => {
    beforeEach(() => {
      mockTwoFactorApi.getStatus.mockResolvedValue(mock2FAStatusEnabled);
    });

    it('displays enabled status with checkmark', async () => {
      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Two-factor authentication is enabled')).toBeInTheDocument();
      });

      // Check for checkmark SVG
      const container = screen.getByText('Two-factor authentication is enabled').closest('div');
      const svg = container?.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('displays backup codes count', async () => {
      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Backup codes remaining: 8')).toBeInTheDocument();
      });
    });

    it('shows warning when backup codes are low', async () => {
      mockTwoFactorApi.getStatus.mockResolvedValue(mock2FAStatusLowBackupCodes);

      render(<SettingsPage />);

      await waitFor(() => {
        expect(
          screen.getByText(/Warning: You have only 2 backup codes remaining/i)
        ).toBeInTheDocument();
      });
    });

    it('does not show warning when backup codes are sufficient', async () => {
      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Two-factor authentication is enabled')).toBeInTheDocument();
      });

      expect(
        screen.queryByText(/Warning: You have only/i)
      ).not.toBeInTheDocument();
    });

    it('renders disable 2FA section', async () => {
      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Disable Two-Factor Authentication')).toBeInTheDocument();
      });
    });

    it('renders password input for disable', async () => {
      render(<SettingsPage />);

      await waitFor(() => {
        const passwordInput = screen.getByPlaceholderText('Enter your password');
        expect(passwordInput).toBeInTheDocument();
        expect(passwordInput).toHaveAttribute('type', 'password');
      });
    });

    it('renders code input for disable', async () => {
      render(<SettingsPage />);

      await waitFor(() => {
        const codeInput = screen.getByPlaceholderText('000000 or XXXX-XXXX');
        expect(codeInput).toBeInTheDocument();
        expect(codeInput).toHaveAttribute('type', 'text');
      });
    });

    it('disables button when password or code is empty', async () => {
      render(<SettingsPage />);

      await waitFor(() => {
        const disableButton = screen.getByRole('button', { name: 'Disable 2FA' });
        expect(disableButton).toBeDisabled();
      });
    });

    it('enables button when both password and code are provided', async () => {
      const user = userEvent.setup();
      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Two-factor authentication is enabled')).toBeInTheDocument();
      });

      const passwordInput = screen.getByPlaceholderText('Enter your password');
      await user.type(passwordInput, 'MyPassword123!');

      const codeInput = screen.getByPlaceholderText('000000 or XXXX-XXXX');
      await user.type(codeInput, '123456');

      const disableButton = screen.getByRole('button', { name: 'Disable 2FA' });
      expect(disableButton).not.toBeDisabled();
    });

    it('shows confirmation dialog before disabling', async () => {
      const user = userEvent.setup();
      mockConfirm.mockReturnValue(false);

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Two-factor authentication is enabled')).toBeInTheDocument();
      });

      const passwordInput = screen.getByPlaceholderText('Enter your password');
      await user.type(passwordInput, 'MyPassword123!');

      const codeInput = screen.getByPlaceholderText('000000 or XXXX-XXXX');
      await user.type(codeInput, '123456');

      const disableButton = screen.getByRole('button', { name: 'Disable 2FA' });
      await user.click(disableButton);

      expect(mockConfirm).toHaveBeenCalledWith(
        'Are you sure you want to disable 2FA? Your account will be less secure.'
      );
    });

    it('does not disable if user cancels confirmation', async () => {
      const user = userEvent.setup();
      mockConfirm.mockReturnValue(false);

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Two-factor authentication is enabled')).toBeInTheDocument();
      });

      const passwordInput = screen.getByPlaceholderText('Enter your password');
      await user.type(passwordInput, 'MyPassword123!');

      const codeInput = screen.getByPlaceholderText('000000 or XXXX-XXXX');
      await user.type(codeInput, '123456');

      const disableButton = screen.getByRole('button', { name: 'Disable 2FA' });
      await user.click(disableButton);

      expect(mockTwoFactorApi.disable).not.toHaveBeenCalled();
    });

    it('disables 2FA successfully', async () => {
      const user = userEvent.setup();
      mockConfirm.mockReturnValue(true);
      mockTwoFactorApi.disable.mockResolvedValue(undefined);
      mockTwoFactorApi.getStatus.mockResolvedValueOnce(mock2FAStatusEnabled);
      mockTwoFactorApi.getStatus.mockResolvedValueOnce(mock2FAStatusDisabled);

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Two-factor authentication is enabled')).toBeInTheDocument();
      });

      const passwordInput = screen.getByPlaceholderText('Enter your password');
      await user.type(passwordInput, 'MyPassword123!');

      const codeInput = screen.getByPlaceholderText('000000 or XXXX-XXXX');
      await user.type(codeInput, '123456');

      const disableButton = screen.getByRole('button', { name: 'Disable 2FA' });
      await user.click(disableButton);

      await waitFor(() => {
        expect(mockTwoFactorApi.disable).toHaveBeenCalledWith('MyPassword123!', '123456');
        expect(mockAlert).toHaveBeenCalledWith('Two-factor authentication disabled.');
      });
    });

    it('shows error when disable fails', async () => {
      const consoleError = jest.spyOn(console, 'error').mockImplementation();
      const user = userEvent.setup();
      mockConfirm.mockReturnValue(true);
      mockTwoFactorApi.disable.mockRejectedValue(new Error('Invalid credentials'));

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Two-factor authentication is enabled')).toBeInTheDocument();
      });

      const passwordInput = screen.getByPlaceholderText('Enter your password');
      await user.type(passwordInput, 'WrongPassword');

      const codeInput = screen.getByPlaceholderText('000000 or XXXX-XXXX');
      await user.type(codeInput, '000000');

      const disableButton = screen.getByRole('button', { name: 'Disable 2FA' });
      await user.click(disableButton);

      await waitFor(() => {
        expect(
          screen.getByText('Failed to disable 2FA. Check your password and code.')
        ).toBeInTheDocument();
      });

      consoleError.mockRestore();
    });

    it('clears password and code fields after successful disable', async () => {
      const user = userEvent.setup();
      mockConfirm.mockReturnValue(true);
      mockTwoFactorApi.disable.mockResolvedValue(undefined);
      mockTwoFactorApi.getStatus.mockResolvedValueOnce(mock2FAStatusEnabled);
      mockTwoFactorApi.getStatus.mockResolvedValueOnce(mock2FAStatusDisabled);

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Two-factor authentication is enabled')).toBeInTheDocument();
      });

      const passwordInput = screen.getByPlaceholderText('Enter your password');
      await user.type(passwordInput, 'MyPassword123!');

      const codeInput = screen.getByPlaceholderText('000000 or XXXX-XXXX');
      await user.type(codeInput, '123456');

      const disableButton = screen.getByRole('button', { name: 'Disable 2FA' });
      await user.click(disableButton);

      await waitFor(() => {
        expect(mockAlert).toHaveBeenCalledWith('Two-factor authentication disabled.');
      });

      // Fields should be cleared (component shows disabled state)
      expect(
        screen.getByRole('button', { name: /Enable Two-Factor Authentication/i })
      ).toBeInTheDocument();
    });
  });

  describe('Error Display', () => {
    it('displays error banner when present', async () => {
      const consoleError = jest.spyOn(console, 'error').mockImplementation();
      mockTwoFactorApi.getStatus.mockRejectedValue(new Error('Network error'));

      render(<SettingsPage />);

      await waitFor(() => {
        const errorBanner = screen.getByText('Failed to load 2FA status');
        expect(errorBanner).toBeInTheDocument();
        expect(errorBanner.closest('div')).toHaveClass('bg-red-50');
      });

      consoleError.mockRestore();
    });

    it('clears error when operation succeeds', async () => {
      const consoleError = jest.spyOn(console, 'error').mockImplementation();
      const user = userEvent.setup();

      // First load fails
      mockTwoFactorApi.getStatus.mockRejectedValueOnce(new Error('Network error'));
      mockTwoFactorApi.getStatus.mockResolvedValueOnce(mock2FAStatusDisabled);
      mockTwoFactorApi.setup.mockResolvedValue(mockTotpSetup);

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Failed to load 2FA status')).toBeInTheDocument();
      });

      // Retry by clicking enable button (which reloads status after setup)
      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        expect(screen.queryByText('Failed to load 2FA status')).not.toBeInTheDocument();
      });

      consoleError.mockRestore();
    });
  });

  describe('Accessibility', () => {
    beforeEach(() => {
      mockTwoFactorApi.getStatus.mockResolvedValue(mock2FAStatusDisabled);
    });

    it('has proper heading hierarchy', async () => {
      render(<SettingsPage />);

      await waitFor(() => {
        const h1 = screen.getByRole('heading', { level: 1, name: 'Account Settings' });
        expect(h1).toBeInTheDocument();

        const h2 = screen.getByRole('heading', { level: 2, name: 'Two-Factor Authentication' });
        expect(h2).toBeInTheDocument();
      });
    });

    it('labels input fields properly', async () => {
      const user = userEvent.setup();
      mockTwoFactorApi.setup.mockResolvedValue(mockTotpSetup);

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        expect(screen.getByText('Step 2: Save Backup Codes')).toBeInTheDocument();
      });

      const savedButton = screen.getByRole('button', { name: /I've Saved My Codes/i });
      await user.click(savedButton);

      const verificationInput = screen.getByPlaceholderText('000000');
      expect(verificationInput).toHaveAccessibleName();
    });

    it('QR code has accessible title', async () => {
      const user = userEvent.setup();
      mockTwoFactorApi.setup.mockResolvedValue(mockTotpSetup);

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        const qrCode = screen.getByTestId('qr-code');
        expect(qrCode).toContainHTML('<title>QR Code</title>');
      });
    });

    it('buttons have proper focus styling', async () => {
      render(<SettingsPage />);

      await waitFor(() => {
        const enableButton = screen.getByRole('button', {
          name: /Enable Two-Factor Authentication/i,
        });
        expect(enableButton).toHaveClass('focus:outline-none');
      });
    });
  });

  describe('Visual Elements', () => {
    beforeEach(() => {
      mockTwoFactorApi.getStatus.mockResolvedValue(mock2FAStatusDisabled);
    });

    it('renders with proper background color', async () => {
      const { container } = render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const background = container.querySelector('.bg-gray-50');
      expect(background).toBeInTheDocument();
    });

    it('renders card with shadow', async () => {
      const { container } = render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const card = container.querySelector('.shadow');
      expect(card).toBeInTheDocument();
    });

    it('displays checkmark icon when 2FA enabled', async () => {
      mockTwoFactorApi.getStatus.mockResolvedValue(mock2FAStatusEnabled);

      const { container } = render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Two-factor authentication is enabled')).toBeInTheDocument();
      });

      const svg = container.querySelector('svg');
      expect(svg).toHaveClass('text-green-600');
    });
  });

  describe('Edge Cases', () => {
    it('handles empty backup codes array', async () => {
      const user = userEvent.setup();
      mockTwoFactorApi.getStatus.mockResolvedValue(mock2FAStatusDisabled);
      mockTwoFactorApi.setup.mockResolvedValue({
        ...mockTotpSetup,
        backupCodes: [],
      });

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        // Should not crash, just not display codes
        expect(screen.getByText('Step 1: Scan QR Code')).toBeInTheDocument();
      });
    });

    it('does not download backup codes if none exist', async () => {
      const user = userEvent.setup();
      mockTwoFactorApi.getStatus.mockResolvedValue(mock2FAStatusDisabled);
      mockTwoFactorApi.setup.mockResolvedValue({
        ...mockTotpSetup,
        backupCodes: [],
      });

      render(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Account Settings')).toBeInTheDocument();
      });

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        expect(screen.getByText('Step 1: Scan QR Code')).toBeInTheDocument();
      });

      // Download button should not exist or should not create blob
      const downloadButton = screen.queryByRole('button', { name: 'Download Codes' });
      if (downloadButton) {
        await user.click(downloadButton);
        expect(URL.createObjectURL).not.toHaveBeenCalled();
      }
    });
  });
});
