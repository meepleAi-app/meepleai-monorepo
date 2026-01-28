/**
 * Tests for TwoFactorSetup component
 * Comprehensive coverage of 2FA setup UI (Issue #3077)
 */

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithIntl } from '../../../__tests__/fixtures/common-fixtures';
import { TwoFactorSetup } from '../TwoFactorSetup';

describe('TwoFactorSetup', () => {
  const mockSetupData = {
    secret: 'JBSWY3DPEHPK3PXP',
    qrCodeUrl: 'otpauth://totp/MeepleAI:user@example.com?secret=JBSWY3DPEHPK3PXP&issuer=MeepleAI',
    backupCodes: ['12345678', '23456789', '34567890', '45678901'],
  };

  const defaultProps = {
    setupData: mockSetupData,
    onVerify: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders title and instructions', () => {
      renderWithIntl(<TwoFactorSetup {...defaultProps} />);

      expect(screen.getByText('Set Up Two-Factor Authentication')).toBeInTheDocument();
      expect(screen.getByText(/scan the QR code/i)).toBeInTheDocument();
    });

    it('renders QR code', () => {
      renderWithIntl(<TwoFactorSetup {...defaultProps} />);

      const qrContainer = screen.getByTestId('qr-code-container');
      expect(qrContainer).toBeInTheDocument();
    });

    it('renders manual entry section with secret', () => {
      renderWithIntl(<TwoFactorSetup {...defaultProps} />);

      expect(screen.getByText(/can't scan/i)).toBeInTheDocument();
      expect(screen.getByTestId('manual-secret')).toBeInTheDocument();
      expect(screen.getByText(mockSetupData.secret)).toBeInTheDocument();
    });

    it('renders verification code input', () => {
      renderWithIntl(<TwoFactorSetup {...defaultProps} />);

      expect(screen.getByLabelText(/verification code/i)).toBeInTheDocument();
    });

    it('renders verify button', () => {
      renderWithIntl(<TwoFactorSetup {...defaultProps} />);

      expect(screen.getByRole('button', { name: /verify|enable/i })).toBeInTheDocument();
    });

    it('renders cancel button when onCancel is provided', () => {
      const onCancel = vi.fn();
      renderWithIntl(<TwoFactorSetup {...defaultProps} onCancel={onCancel} />);

      expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
    });

    it('hides cancel button when onCancel is not provided', () => {
      renderWithIntl(<TwoFactorSetup {...defaultProps} />);

      expect(screen.queryByRole('button', { name: /cancel/i })).not.toBeInTheDocument();
    });
  });

  describe('Loading State', () => {
    it('disables inputs when loading', () => {
      renderWithIntl(<TwoFactorSetup {...defaultProps} loading={true} />);

      const input = screen.getByLabelText(/verification code/i);
      expect(input).toBeDisabled();
    });

    it('shows loading state on verify button', () => {
      renderWithIntl(<TwoFactorSetup {...defaultProps} loading={true} />);

      expect(screen.getByText(/verifying/i)).toBeInTheDocument();
    });

    it('disables verify button when loading', () => {
      renderWithIntl(<TwoFactorSetup {...defaultProps} loading={true} />);

      const buttons = screen.getAllByRole('button');
      const verifyButton = buttons.find(
        btn => btn.textContent?.toLowerCase().includes('verify') ||
               btn.textContent?.toLowerCase().includes('enable')
      );
      expect(verifyButton).toBeDisabled();
    });
  });

  describe('Error State', () => {
    it('displays error message when error prop is provided', () => {
      renderWithIntl(
        <TwoFactorSetup
          {...defaultProps}
          error="Invalid verification code. Please try again."
        />
      );

      expect(screen.getByText('Invalid verification code. Please try again.')).toBeInTheDocument();
    });

    it('displays error with alert styling', () => {
      renderWithIntl(<TwoFactorSetup {...defaultProps} error="Error message" />);

      const alert = screen.getByTestId('setup-error');
      expect(alert).toBeInTheDocument();
    });
  });

  describe('User Interactions', () => {
    it('calls onVerify with code when form is submitted', async () => {
      const user = userEvent.setup();
      const onVerify = vi.fn();

      renderWithIntl(<TwoFactorSetup {...defaultProps} onVerify={onVerify} />);

      const input = screen.getByLabelText(/verification code/i);
      await user.type(input, '123456');

      const verifyButton = screen.getByRole('button', { name: /verify|enable/i });
      await user.click(verifyButton);

      await waitFor(() => {
        expect(onVerify).toHaveBeenCalledWith({ code: '123456' });
      });
    });

    it('calls onCancel when cancel button is clicked', async () => {
      const user = userEvent.setup();
      const onCancel = vi.fn();

      renderWithIntl(<TwoFactorSetup {...defaultProps} onCancel={onCancel} />);

      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      expect(onCancel).toHaveBeenCalledTimes(1);
    });

    it('allows copying secret to clipboard', async () => {
      const user = userEvent.setup();

      // Mock clipboard API
      const mockClipboard = {
        writeText: vi.fn().mockResolvedValue(undefined),
      };
      Object.assign(navigator, { clipboard: mockClipboard });

      renderWithIntl(<TwoFactorSetup {...defaultProps} />);

      const copyButton = screen.getByTestId('copy-secret-button');
      await user.click(copyButton);

      await waitFor(() => {
        expect(mockClipboard.writeText).toHaveBeenCalledWith(mockSetupData.secret);
      });
    });
  });

  describe('Accessibility', () => {
    it('has proper form structure', () => {
      const { container } = renderWithIntl(<TwoFactorSetup {...defaultProps} />);

      expect(container.querySelector('form')).toBeInTheDocument();
    });

    it('has accessible label for code input', () => {
      renderWithIntl(<TwoFactorSetup {...defaultProps} />);

      const input = screen.getByLabelText(/verification code/i);
      expect(input).toBeInTheDocument();
    });

    it('has proper input mode for numeric keyboard', () => {
      renderWithIntl(<TwoFactorSetup {...defaultProps} />);

      const input = screen.getByLabelText(/verification code/i);
      expect(input).toHaveAttribute('inputMode', 'numeric');
    });

    it('renders step numbers for setup instructions', () => {
      renderWithIntl(<TwoFactorSetup {...defaultProps} />);

      // Should have step indicators (1, 2, 3)
      expect(screen.getByText('1')).toBeInTheDocument();
      expect(screen.getByText('2')).toBeInTheDocument();
      expect(screen.getByText('3')).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('validates code length before submission', async () => {
      const user = userEvent.setup();
      const onVerify = vi.fn();

      renderWithIntl(<TwoFactorSetup {...defaultProps} onVerify={onVerify} />);

      const input = screen.getByLabelText(/verification code/i);
      await user.type(input, '123'); // Only 3 digits

      const verifyButton = screen.getByRole('button', { name: /verify|enable/i });
      await user.click(verifyButton);

      // Should show validation error or not submit
      expect(onVerify).not.toHaveBeenCalled();
    });

    it('handles empty setup data gracefully', () => {
      const emptySetupData = {
        secret: '',
        qrCodeUrl: '',
        backupCodes: [],
      };

      // Should not throw
      expect(() => {
        renderWithIntl(
          <TwoFactorSetup
            setupData={emptySetupData}
            onVerify={vi.fn()}
          />
        );
      }).not.toThrow();
    });

    it('shows copy feedback after copying secret', async () => {
      const user = userEvent.setup();

      // Mock clipboard API
      Object.assign(navigator, {
        clipboard: {
          writeText: vi.fn().mockResolvedValue(undefined),
        },
      });

      renderWithIntl(<TwoFactorSetup {...defaultProps} />);

      const copyButton = screen.getByTestId('copy-secret-button');
      await user.click(copyButton);

      await waitFor(() => {
        expect(screen.getByText(/copied/i)).toBeInTheDocument();
      });
    });
  });
});
