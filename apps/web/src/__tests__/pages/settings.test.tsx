/**
 * Unit tests for Settings Page (SPRINT-1, Issue #848)
 *
 * Tests cover:
 * - 4-tab interface (Profile, Preferences, Privacy, Advanced)
 * - Profile management and password change UI
 * - 2FA management (enable, QR code, backup codes, disable)
 * - OAuth account linking/unlinking
 * - Preferences and data retention settings
 * - Loading states and error handling
 * - Tab navigation and content switching
 */

import {  screen, waitFor } from '@testing-library/react';
import { renderWithQuery } from '../utils/query-test-utils';
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
    auth: {
      getTwoFactorStatus: jest.fn(),
      setup2FA: jest.fn(),
      enable2FA: jest.fn(),
      verify2FA: jest.fn(),
      disable2FA: jest.fn(),
    },
  },
}));

// Import the mocked api to get typed access
import { api } from '@/lib/api';
const mockAuthApi = api.auth as jest.Mocked<typeof api.auth>;

const useRouterMock = useRouter as jest.MockedFunction<typeof useRouter>;

// Mock confirm and alert
const mockConfirm = jest.fn();
const mockAlert = jest.fn();
global.confirm = mockConfirm;
global.alert = mockAlert;

// Mock URL.createObjectURL and revokeObjectURL
global.URL.createObjectURL = jest.fn(() => 'blob:mock-url');
global.URL.revokeObjectURL = jest.fn();

// Mock fetch for profile loading
global.fetch = jest.fn() as jest.MockedFunction<typeof fetch>;

// Test data
const mockProfile = {
  user: {
    id: '123',
    email: 'user@example.com',
    displayName: 'Test User',
    role: 'user',
    createdAt: '2024-01-01T00:00:00Z',
    isTwoFactorEnabled: false,
    twoFactorEnabledAt: null,
  }
};

const mockOAuthAccounts = [
  { provider: 'google', createdAt: '2024-01-01T00:00:00Z' },
];

const mock2FAStatusDisabled = {
  isEnabled: false,
  enabledAt: null,
  unusedBackupCodesCount: 0,
};

const mock2FAStatusEnabled = {
  isEnabled: true,
  enabledAt: '2024-01-01T00:00:00Z',
  unusedBackupCodesCount: 8,
};

const mock2FAStatusLowBackupCodes = {
  isEnabled: true,
  enabledAt: '2024-01-01T00:00:00Z',
  unusedBackupCodesCount: 2,
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
    mockAuthApi.getTwoFactorStatus.mockClear();
    mockAuthApi.setup2FA.mockClear();
    mockAuthApi.enable2FA.mockClear();
    mockAuthApi.verify2FA.mockClear();
    mockAuthApi.disable2FA.mockClear();
    useRouterMock.mockReturnValue(createMockRouter({ pathname: '/settings' }));

    // Default fetch mocks for successful profile and OAuth loading
    (global.fetch as jest.MockedFunction<typeof fetch>).mockImplementation((url) => {
      const urlStr = url.toString();
      if (urlStr.includes('/api/v1/auth/me')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve(mockProfile),
        } as Response);
      }
      if (urlStr.includes('/api/v1/users/me/oauth-accounts')) {
        return Promise.resolve({
          ok: true,
          status: 200,
          json: () => Promise.resolve(mockOAuthAccounts),
        } as Response);
      }
      return Promise.reject(new Error('Unexpected URL'));
    });
  });

  describe('Initial Loading', () => {
    it('displays loading spinner initially', () => {
      mockAuthApi.getTwoFactorStatus.mockReturnValue(new Promise(() => {})); // Never resolves

      const { container } = renderWithQuery(<SettingsPage />);

      // Check for loading spinner by class
      const spinner = container.querySelector('.animate-spin');
      expect(spinner).toBeInTheDocument();
    });

    it('loads profile and 2FA status on mount', async () => {
      mockAuthApi.getTwoFactorStatus.mockResolvedValue(mock2FAStatusDisabled);

      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(fetch).toHaveBeenCalledWith(
          expect.stringContaining('/api/v1/auth/me'),
          expect.any(Object)
        );
        expect(mockAuthApi.getTwoFactorStatus).toHaveBeenCalledTimes(1);
      });
    });

    it('hides loading after data loads', async () => {
      mockAuthApi.getTwoFactorStatus.mockResolvedValue(mock2FAStatusDisabled);

      const { container } = renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        const spinner = container.querySelector('.animate-spin');
        expect(spinner).not.toBeInTheDocument();
      });
    });

    it('redirects to login when unauthorized', async () => {
      const mockRouter = createMockRouter({ pathname: '/settings' });
      useRouterMock.mockReturnValue(mockRouter);

      (global.fetch as jest.MockedFunction<typeof fetch>).mockImplementation((url) => {
        const urlStr = url.toString();
        if (urlStr.includes('/api/v1/auth/me')) {
          return Promise.resolve({
            ok: false,
            status: 401,
          } as Response);
        }
        return Promise.resolve({
          ok: true,
          json: () => Promise.resolve([]),
        } as Response);
      });

      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(mockRouter.push).toHaveBeenCalledWith('/login');
      });
    });
  });

  describe('Page Structure', () => {
    beforeEach(() => {
      mockAuthApi.getTwoFactorStatus.mockResolvedValue(mock2FAStatusDisabled);
    });

    it('renders page title', async () => {
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });
    });

    it('renders page description', async () => {
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText(/Manage your account settings and preferences/i)).toBeInTheDocument();
      });
    });

    it('renders all four tabs', async () => {
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByRole('tab', { name: 'Profile' })).toBeInTheDocument();
        expect(screen.getByRole('tab', { name: 'Preferences' })).toBeInTheDocument();
        expect(screen.getByRole('tab', { name: 'Privacy' })).toBeInTheDocument();
        expect(screen.getByRole('tab', { name: 'Advanced' })).toBeInTheDocument();
      });
    });

    it('renders "Back to Home" button', async () => {
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        const backButton = screen.getByRole('button', { name: /Back to Home/i });
        expect(backButton).toBeInTheDocument();
      });
    });

    it('navigates to home when Back button clicked', async () => {
      const user = userEvent.setup();
      const mockRouter = createMockRouter({ pathname: '/settings' });
      useRouterMock.mockReturnValue(mockRouter);

      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const backButton = screen.getByRole('button', { name: /Back to Home/i });
      await user.click(backButton);

      expect(mockRouter.push).toHaveBeenCalledWith('/');
    });
  });

  describe('Tab Navigation', () => {
    beforeEach(() => {
      mockAuthApi.getTwoFactorStatus.mockResolvedValue(mock2FAStatusDisabled);
    });

    it('shows Profile tab content by default', async () => {
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Profile Information')).toBeInTheDocument();
        expect(screen.getByText('Change Password')).toBeInTheDocument();
      });
    });

    it('switches to Preferences tab when clicked', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const preferencesTab = screen.getByRole('tab', { name: 'Preferences' });
      await user.click(preferencesTab);

      expect(screen.getByText('User Preferences')).toBeInTheDocument();
      expect(screen.getByLabelText('Language')).toBeInTheDocument();
    });

    it('switches to Privacy tab when clicked', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      expect(screen.getByText('Two-Factor Authentication')).toBeInTheDocument();
      expect(screen.getByText('Linked Accounts')).toBeInTheDocument();
    });

    it('switches to Advanced tab when clicked', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const advancedTab = screen.getByRole('tab', { name: 'Advanced' });
      await user.click(advancedTab);

      expect(screen.getByText('API Keys')).toBeInTheDocument();
      expect(screen.getByText('Active Sessions')).toBeInTheDocument();
      expect(screen.getByText('Danger Zone')).toBeInTheDocument();
    });
  });

  describe('Profile Tab', () => {
    beforeEach(() => {
      mockAuthApi.getTwoFactorStatus.mockResolvedValue(mock2FAStatusDisabled);
    });

    it('displays user profile information', async () => {
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByDisplayValue('Test User')).toBeInTheDocument();
        expect(screen.getByDisplayValue('user@example.com')).toBeInTheDocument();
        expect(screen.getByDisplayValue('user')).toBeInTheDocument();
      });
    });

    it('shows disabled Update Profile button', async () => {
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        const updateButton = screen.getByRole('button', { name: /Update Profile/i });
        expect(updateButton).toBeDisabled();
        expect(updateButton).toHaveTextContent('Coming Soon');
      });
    });

    it('shows password change fields', async () => {
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByLabelText('Current Password')).toBeInTheDocument();
        expect(screen.getByLabelText('New Password')).toBeInTheDocument();
        expect(screen.getByLabelText('Confirm New Password')).toBeInTheDocument();
      });
    });

    it('shows disabled Change Password button', async () => {
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        const changePasswordButton = screen.getByRole('button', { name: /Change Password/i });
        expect(changePasswordButton).toBeDisabled();
        expect(changePasswordButton).toHaveTextContent('Coming Soon');
      });
    });
  });

  describe('Preferences Tab', () => {
    beforeEach(() => {
      mockAuthApi.getTwoFactorStatus.mockResolvedValue(mock2FAStatusDisabled);
    });

    it('displays language selector', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const preferencesTab = screen.getByRole('tab', { name: 'Preferences' });
      await user.click(preferencesTab);

      expect(screen.getByLabelText('Language')).toBeInTheDocument();
      expect(screen.getByText('English')).toBeInTheDocument();
    });

    it('displays theme selector', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const preferencesTab = screen.getByRole('tab', { name: 'Preferences' });
      await user.click(preferencesTab);

      expect(screen.getByLabelText('Theme')).toBeInTheDocument();
      expect(screen.getByText('System')).toBeInTheDocument();
    });

    it('displays email notifications toggle', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const preferencesTab = screen.getByRole('tab', { name: 'Preferences' });
      await user.click(preferencesTab);

      expect(screen.getByLabelText('Email Notifications')).toBeInTheDocument();
      expect(screen.getByRole('switch', { name: 'Email Notifications' })).toBeInTheDocument();
    });

    it('displays data retention selector', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const preferencesTab = screen.getByRole('tab', { name: 'Preferences' });
      await user.click(preferencesTab);

      expect(screen.getByLabelText('Data Retention (days)')).toBeInTheDocument();
      expect(screen.getByText('90 days')).toBeInTheDocument();
    });

    it('shows Save Preferences button', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const preferencesTab = screen.getByRole('tab', { name: 'Preferences' });
      await user.click(preferencesTab);

      const saveButton = screen.getByRole('button', { name: 'Save Preferences' });
      expect(saveButton).toBeInTheDocument();
      expect(saveButton).not.toBeDisabled();
    });
  });

  describe('Privacy Tab - 2FA Disabled State', () => {
    beforeEach(() => {
      mockAuthApi.getTwoFactorStatus.mockResolvedValue(mock2FAStatusDisabled);
    });

    it('displays 2FA section in Privacy tab', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      expect(screen.getByText('Two-Factor Authentication')).toBeInTheDocument();
      expect(screen.getByText(/Add an extra layer of security/i)).toBeInTheDocument();
    });

    it('displays description about 2FA', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      expect(
        screen.getByText(/Two-factor authentication adds an extra layer of security/i)
      ).toBeInTheDocument();
    });

    it('renders "Enable Two-Factor Authentication" button', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      expect(
        screen.getByRole('button', { name: /Enable Two-Factor Authentication/i })
      ).toBeInTheDocument();
    });

    it('initiates 2FA setup when enable button clicked', async () => {
      const user = userEvent.setup();
      mockAuthApi.setup2FA.mockResolvedValue(mockTotpSetup);

      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        expect(mockAuthApi.setup2FA).toHaveBeenCalledTimes(1);
      });
    });

    it('shows loading state during setup', async () => {
      const user = userEvent.setup();
      mockAuthApi.setup2FA.mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve(mockTotpSetup), 100))
      );

      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      expect(screen.getByText('Setting up...')).toBeInTheDocument();
    });

    it('handles setup error', async () => {
      const consoleError = jest.spyOn(console, 'error').mockImplementation();
      const user = userEvent.setup();
      mockAuthApi.setup2FA.mockRejectedValue(new Error('Setup failed'));

      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

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

  describe('Privacy Tab - 2FA Setup Flow', () => {
    beforeEach(() => {
      mockAuthApi.getTwoFactorStatus.mockResolvedValue(mock2FAStatusDisabled);
      mockAuthApi.setup2FA.mockResolvedValue(mockTotpSetup);
    });

    it('displays QR code after setup', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

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
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        expect(screen.getByText(/Can't scan\? Enter manually/i)).toBeInTheDocument();
      });

      // Expand details
      const detailsSummary = screen.getByText(/Can't scan\? Enter manually/i);
      await user.click(detailsSummary);

      expect(screen.getByText(mockTotpSetup.secret)).toBeInTheDocument();
    });

    it('displays backup codes after setup', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

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
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

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
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

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
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

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
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

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
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

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
      mockAuthApi.enable2FA.mockResolvedValue({ success: true, backupCodes: null, errorMessage: null });
      mockAuthApi.getTwoFactorStatus.mockResolvedValueOnce(mock2FAStatusDisabled);
      mockAuthApi.getTwoFactorStatus.mockResolvedValueOnce(mock2FAStatusEnabled);

      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

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
        expect(mockAuthApi.enable2FA).toHaveBeenCalledWith('123456');
        expect(screen.getByText('Two-factor authentication enabled successfully!')).toBeInTheDocument();
      });
    });

    it('shows error with invalid verification code', async () => {
      const consoleError = jest.spyOn(console, 'error').mockImplementation();
      const user = userEvent.setup();
      mockAuthApi.enable2FA.mockRejectedValue(new Error('Invalid code'));

      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

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
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

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

  describe('Privacy Tab - 2FA Enabled State', () => {
    beforeEach(() => {
      mockAuthApi.getTwoFactorStatus.mockResolvedValue(mock2FAStatusEnabled);
    });

    it('displays enabled status with checkmark', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      expect(screen.getByText(/Two-factor authentication is enabled/i)).toBeInTheDocument();
    });

    it('displays backup codes count', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      await waitFor(() => {
        expect(screen.getByText(/Backup codes remaining: 8/)).toBeInTheDocument();
      });
    });

    it('shows warning when backup codes are low', async () => {
      mockAuthApi.getTwoFactorStatus.mockResolvedValue(mock2FAStatusLowBackupCodes);

      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      expect(
        screen.getByText(/Warning: You have only 2 backup codes remaining/i)
      ).toBeInTheDocument();
    });

    it('does not show warning when backup codes are sufficient', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      expect(screen.getByText(/Two-factor authentication is enabled/i)).toBeInTheDocument();
      expect(screen.queryByText(/Warning: You have only/i)).not.toBeInTheDocument();
    });

    it('renders disable 2FA section', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      expect(screen.getByText('Disable Two-Factor Authentication')).toBeInTheDocument();
    });

    it('renders password input for disable', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      const passwordInput = screen.getByPlaceholderText('Enter your password');
      expect(passwordInput).toBeInTheDocument();
      expect(passwordInput).toHaveAttribute('type', 'password');
    });

    it('renders code input for disable', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      const codeInput = screen.getByPlaceholderText('000000 or XXXX-XXXX');
      expect(codeInput).toBeInTheDocument();
      expect(codeInput).toHaveAttribute('type', 'text');
    });

    it('disables button when password or code is empty', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      const disableButton = screen.getByRole('button', { name: 'Disable 2FA' });
      expect(disableButton).toBeDisabled();
    });

    it('enables button when both password and code are provided', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

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

      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

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

      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      const passwordInput = screen.getByPlaceholderText('Enter your password');
      await user.type(passwordInput, 'MyPassword123!');

      const codeInput = screen.getByPlaceholderText('000000 or XXXX-XXXX');
      await user.type(codeInput, '123456');

      const disableButton = screen.getByRole('button', { name: 'Disable 2FA' });
      await user.click(disableButton);

      expect(mockAuthApi.disable2FA).not.toHaveBeenCalled();
    });

    it('disables 2FA successfully', async () => {
      const user = userEvent.setup();
      mockConfirm.mockReturnValue(true);
      mockAuthApi.disable2FA.mockResolvedValue({ success: true, errorMessage: null });
      mockAuthApi.getTwoFactorStatus.mockResolvedValueOnce(mock2FAStatusEnabled);
      mockAuthApi.getTwoFactorStatus.mockResolvedValueOnce(mock2FAStatusDisabled);

      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      const passwordInput = screen.getByPlaceholderText('Enter your password');
      await user.type(passwordInput, 'MyPassword123!');

      const codeInput = screen.getByPlaceholderText('000000 or XXXX-XXXX');
      await user.type(codeInput, '123456');

      const disableButton = screen.getByRole('button', { name: 'Disable 2FA' });
      await user.click(disableButton);

      await waitFor(() => {
        expect(mockAuthApi.disable2FA).toHaveBeenCalledWith('MyPassword123!', '123456');
        expect(screen.getByText('Two-factor authentication disabled.')).toBeInTheDocument();
      });
    });

    it('shows error when disable fails', async () => {
      const consoleError = jest.spyOn(console, 'error').mockImplementation();
      const user = userEvent.setup();
      mockConfirm.mockReturnValue(true);
      mockAuthApi.disable2FA.mockRejectedValue(new Error('Invalid credentials'));

      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

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
  });

  describe('Privacy Tab - OAuth Accounts', () => {
    beforeEach(() => {
      mockAuthApi.getTwoFactorStatus.mockResolvedValue(mock2FAStatusDisabled);
    });

    it('displays OAuth section in Privacy tab', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      expect(screen.getByText('Linked Accounts')).toBeInTheDocument();
      expect(screen.getByText(/Connect your accounts to login with social providers/i)).toBeInTheDocument();
    });

    it('displays OAuth provider options', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      expect(screen.getByText('Google')).toBeInTheDocument();
      expect(screen.getByText('Discord')).toBeInTheDocument();
      expect(screen.getByText('GitHub')).toBeInTheDocument();
    });

    it('shows connected status for linked accounts', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      await waitFor(() => {
        // Google should be connected (in mockOAuthAccounts)
        // Look for the parent element that contains both Google and Connected
        const googleCard = screen.getByText('Google').closest('div.flex');
        expect(googleCard).toBeInTheDocument();
        // Check that within that card's parent, we have "Connected" text
        const cardContainer = googleCard?.parentElement;
        expect(cardContainer).toHaveTextContent('Connected');
      });
    });

    it('shows Link button for unlinked accounts', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      // Discord and GitHub should not be connected
      const linkButtons = screen.getAllByRole('button', { name: 'Link' });
      expect(linkButtons).toHaveLength(2); // Discord and GitHub
    });

    it('shows Unlink button for linked accounts', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      // Google should have Unlink button
      const unlinkButtons = screen.getAllByRole('button', { name: 'Unlink' });
      expect(unlinkButtons).toHaveLength(1); // Only Google
    });
  });

  describe('Advanced Tab', () => {
    beforeEach(() => {
      mockAuthApi.getTwoFactorStatus.mockResolvedValue(mock2FAStatusDisabled);
    });

    it('displays API Keys section with coming soon message', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const advancedTab = screen.getByRole('tab', { name: 'Advanced' });
      await user.click(advancedTab);

      expect(screen.getByText('API Keys')).toBeInTheDocument();
      expect(screen.getByText('API key management coming soon')).toBeInTheDocument();
    });

    it('displays Active Sessions section with coming soon message', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const advancedTab = screen.getByRole('tab', { name: 'Advanced' });
      await user.click(advancedTab);

      expect(screen.getByText('Active Sessions')).toBeInTheDocument();
      expect(screen.getByText('Session management coming soon')).toBeInTheDocument();
    });

    it('displays Danger Zone section', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const advancedTab = screen.getByRole('tab', { name: 'Advanced' });
      await user.click(advancedTab);

      expect(screen.getByText('Danger Zone')).toBeInTheDocument();
      expect(screen.getByText(/Account deletion is permanent/i)).toBeInTheDocument();
    });

    it('shows disabled Delete Account button', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const advancedTab = screen.getByRole('tab', { name: 'Advanced' });
      await user.click(advancedTab);

      const deleteButton = screen.getByRole('button', { name: /Delete Account/i });
      expect(deleteButton).toBeDisabled();
      expect(deleteButton).toHaveTextContent('Coming Soon');
    });
  });

  describe('Error Display', () => {
    it('displays error alert when present', async () => {
      const consoleError = jest.spyOn(console, 'error').mockImplementation();
      mockAuthApi.getTwoFactorStatus.mockRejectedValue(new Error('Network error'));

      renderWithQuery(<SettingsPage />);

      // The error from getStatus is caught but doesn't set the main error state
      // Instead, let's test error display by simulating a setup error
      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      // Navigate to Privacy tab
      const user = userEvent.setup();
      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      // Try to setup 2FA which will fail
      mockAuthApi.setup2FA.mockRejectedValue(new Error('Setup failed'));

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        expect(screen.getByText('Failed to setup 2FA')).toBeInTheDocument();
      });

      consoleError.mockRestore();
    });

    it('displays success alert when operation succeeds', async () => {
      const user = userEvent.setup();
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const preferencesTab = screen.getByRole('tab', { name: 'Preferences' });
      await user.click(preferencesTab);

      const saveButton = screen.getByRole('button', { name: 'Save Preferences' });
      await user.click(saveButton);

      await waitFor(() => {
        expect(screen.getByText(/Preferences saved successfully/i)).toBeInTheDocument();
      });
    });
  });

  describe('Accessibility', () => {
    beforeEach(() => {
      mockAuthApi.getTwoFactorStatus.mockResolvedValue(mock2FAStatusDisabled);
    });

    it('has proper heading hierarchy', async () => {
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        const h1 = screen.getByRole('heading', { level: 1, name: 'Settings' });
        expect(h1).toBeInTheDocument();
      });
    });

    it('tabs have proper ARIA roles', async () => {
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        const tabList = screen.getByRole('tablist');
        expect(tabList).toBeInTheDocument();

        const tabs = screen.getAllByRole('tab');
        expect(tabs).toHaveLength(4);
      });
    });

    it('tab panels have proper ARIA roles', async () => {
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        const tabPanel = screen.getByRole('tabpanel');
        expect(tabPanel).toBeInTheDocument();
      });
    });

    it('form inputs have proper labels', async () => {
      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByLabelText('Display Name')).toBeInTheDocument();
        expect(screen.getByLabelText('Email')).toBeInTheDocument();
        expect(screen.getByLabelText('Current Password')).toBeInTheDocument();
      });
    });

    it('QR code has accessible title', async () => {
      const user = userEvent.setup();
      mockAuthApi.setup2FA.mockResolvedValue(mockTotpSetup);

      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        const qrCode = screen.getByTestId('qr-code');
        expect(qrCode).toContainHTML('<title>QR Code</title>');
      });
    });
  });

  describe('Edge Cases', () => {
    it('handles empty backup codes array', async () => {
      const user = userEvent.setup();
      mockAuthApi.getTwoFactorStatus.mockResolvedValue(mock2FAStatusDisabled);
      mockAuthApi.setup2FA.mockResolvedValue({
        ...mockTotpSetup,
        backupCodes: [],
      });

      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Settings')).toBeInTheDocument();
      });

      const privacyTab = screen.getByRole('tab', { name: 'Privacy' });
      await user.click(privacyTab);

      const enableButton = screen.getByRole('button', {
        name: /Enable Two-Factor Authentication/i,
      });
      await user.click(enableButton);

      await waitFor(() => {
        // Should not crash, just not display codes
        expect(screen.getByText('Step 1: Scan QR Code')).toBeInTheDocument();
      });
    });

    it('handles profile load failure gracefully', async () => {
      const consoleError = jest.spyOn(console, 'error').mockImplementation();
      (global.fetch as jest.MockedFunction<typeof fetch>).mockImplementation((url) => {
        const urlStr = url.toString();
        if (urlStr.includes('/api/v1/auth/me')) {
          return Promise.reject(new Error('Network error'));
        }
        return Promise.resolve({
          ok: true,
          json: () => Promise.resolve([]),
        } as Response);
      });

      renderWithQuery(<SettingsPage />);

      await waitFor(() => {
        expect(screen.getByText('Failed to load profile')).toBeInTheDocument();
      });

      consoleError.mockRestore();
    });
  });
});