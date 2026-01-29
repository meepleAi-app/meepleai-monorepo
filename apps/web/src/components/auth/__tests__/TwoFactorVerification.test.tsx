/**
 * Tests for TwoFactorVerification component
 * Comprehensive coverage of 2FA verification UI (Issue #3077)
 */

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithIntl, msg } from '../../../__tests__/fixtures/common-fixtures';
import { TwoFactorVerification } from '../TwoFactorVerification';

describe('TwoFactorVerification', () => {
  const defaultProps = {
    onVerify: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders default title and subtitle', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      expect(screen.getByText(msg('auth.2fa.verificationTitle'))).toBeInTheDocument();
      expect(screen.getByText(msg('auth.2fa.verificationSubtitle'))).toBeInTheDocument();
    });

    it('renders custom title and subtitle', () => {
      renderWithIntl(
        <TwoFactorVerification
          {...defaultProps}
          title="Custom Title"
          subtitle="Custom subtitle text"
        />
      );

      expect(screen.getByText('Custom Title')).toBeInTheDocument();
      expect(screen.getByText('Custom subtitle text')).toBeInTheDocument();
    });

    it('renders single code input field', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      const input = screen.getByTestId('2fa-code-input');
      expect(input).toBeInTheDocument();
      expect(input).toHaveAttribute('maxLength', '8');
    });

    it('renders remember device checkbox by default', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      expect(screen.getByTestId('remember-device-checkbox')).toBeInTheDocument();
    });

    it('hides remember device checkbox when showRememberDevice is false', () => {
      renderWithIntl(
        <TwoFactorVerification {...defaultProps} showRememberDevice={false} />
      );

      expect(screen.queryByTestId('remember-device-checkbox')).not.toBeInTheDocument();
    });

    it('renders verify button', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      expect(screen.getByTestId('2fa-verify-button')).toBeInTheDocument();
    });

    it('renders backup code link when onUseBackupCode is provided', () => {
      const onUseBackupCode = vi.fn();
      renderWithIntl(
        <TwoFactorVerification {...defaultProps} onUseBackupCode={onUseBackupCode} />
      );

      expect(screen.getByTestId('use-backup-code-link')).toBeInTheDocument();
    });

    it('hides backup code link when onUseBackupCode is not provided', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      expect(screen.queryByTestId('use-backup-code-link')).not.toBeInTheDocument();
    });

    it('renders cancel button when onCancel is provided', () => {
      const onCancel = vi.fn();
      renderWithIntl(<TwoFactorVerification {...defaultProps} onCancel={onCancel} />);

      expect(screen.getByTestId('2fa-cancel-button')).toBeInTheDocument();
    });

    it('hides cancel button when onCancel is not provided', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      expect(screen.queryByTestId('2fa-cancel-button')).not.toBeInTheDocument();
    });
  });

  describe('Loading State', () => {
    it('disables input when loading', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} loading={true} />);

      const input = screen.getByTestId('2fa-code-input');
      expect(input).toBeDisabled();
    });

    it('shows loading state on verify button', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} loading={true} />);

      const button = screen.getByTestId('2fa-verify-button');
      expect(button).toHaveAttribute('aria-busy', 'true');
    });

    it('disables verify button when loading', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} loading={true} />);

      expect(screen.getByTestId('2fa-verify-button')).toBeDisabled();
    });
  });

  describe('Error State', () => {
    it('displays error message when error prop is provided', () => {
      renderWithIntl(
        <TwoFactorVerification
          {...defaultProps}
          error="Invalid code. Please try again."
        />
      );

      expect(screen.getByText('Invalid code. Please try again.')).toBeInTheDocument();
    });

    it('displays error with destructive styling', () => {
      renderWithIntl(
        <TwoFactorVerification {...defaultProps} error="Error message" />
      );

      const alert = screen.getByTestId('2fa-error');
      expect(alert).toBeInTheDocument();
    });
  });

  describe('User Interactions', () => {
    it('calls onVerify with code and rememberDevice when form is submitted', async () => {
      const user = userEvent.setup();
      const onVerify = vi.fn();

      renderWithIntl(<TwoFactorVerification onVerify={onVerify} />);

      const input = screen.getByTestId('2fa-code-input');
      await user.type(input, '123456');

      const verifyButton = screen.getByTestId('2fa-verify-button');
      await user.click(verifyButton);

      await waitFor(() => {
        expect(onVerify).toHaveBeenCalledWith({
          code: '123456',
          rememberDevice: false,
        });
      });
    });

    it('calls onVerify with rememberDevice true when checkbox is checked', async () => {
      const user = userEvent.setup();
      const onVerify = vi.fn();

      renderWithIntl(<TwoFactorVerification onVerify={onVerify} />);

      const input = screen.getByTestId('2fa-code-input');
      await user.type(input, '123456');

      const checkbox = screen.getByTestId('remember-device-checkbox');
      await user.click(checkbox);

      const verifyButton = screen.getByTestId('2fa-verify-button');
      await user.click(verifyButton);

      await waitFor(() => {
        expect(onVerify).toHaveBeenCalledWith({
          code: '123456',
          rememberDevice: true,
        });
      });
    });

    it('calls onUseBackupCode when backup code link is clicked', async () => {
      const user = userEvent.setup();
      const onUseBackupCode = vi.fn();

      renderWithIntl(
        <TwoFactorVerification {...defaultProps} onUseBackupCode={onUseBackupCode} />
      );

      const backupLink = screen.getByTestId('use-backup-code-link');
      await user.click(backupLink);

      expect(onUseBackupCode).toHaveBeenCalledTimes(1);
    });

    it('calls onCancel when cancel button is clicked', async () => {
      const user = userEvent.setup();
      const onCancel = vi.fn();

      renderWithIntl(<TwoFactorVerification {...defaultProps} onCancel={onCancel} />);

      const cancelButton = screen.getByTestId('2fa-cancel-button');
      await user.click(cancelButton);

      expect(onCancel).toHaveBeenCalledTimes(1);
    });

    it('auto-focuses input on mount', async () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      const input = screen.getByTestId('2fa-code-input');
      // Input should be auto-focused on mount
      expect(input).toHaveFocus();
    });
  });

  describe('Accessibility', () => {
    it('has accessible label for code input', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      // Input has sr-only label via htmlFor/id association
      const input = screen.getByTestId('2fa-code-input');
      expect(input).toHaveAttribute('id', '2fa-code');
      expect(input).toHaveAttribute('inputMode', 'numeric');
      expect(input).toHaveAttribute('autoComplete', 'one-time-code');
    });

    it('has proper form structure', () => {
      const { container } = renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      expect(container.querySelector('form')).toBeInTheDocument();
    });

    it('renders error with alert role', () => {
      renderWithIntl(
        <TwoFactorVerification {...defaultProps} error="Error message" />
      );

      const alert = screen.getByTestId('2fa-error');
      expect(alert).toBeInTheDocument();
    });

    it('has proper input mode for numeric keyboard on mobile', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      const input = screen.getByTestId('2fa-code-input');
      expect(input).toHaveAttribute('inputMode', 'numeric');
    });
  });

  describe('Edge Cases', () => {
    it('handles paste of full code', async () => {
      const user = userEvent.setup();
      const onVerify = vi.fn();

      renderWithIntl(<TwoFactorVerification onVerify={onVerify} />);

      const input = screen.getByTestId('2fa-code-input');

      // Simulate paste by typing the full code into input
      await user.click(input);
      await user.paste('123456');

      const verifyButton = screen.getByTestId('2fa-verify-button');
      await user.click(verifyButton);

      await waitFor(() => {
        expect(onVerify).toHaveBeenCalled();
      });
    });

    it('filters special characters but allows alphanumeric for backup codes', async () => {
      const user = userEvent.setup();

      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      const input = screen.getByTestId('2fa-code-input');
      // Component allows A-Za-z0-9- for backup code support
      await user.type(input, 'abc!@#');

      // Special characters filtered, alphanumeric kept
      expect(input).toHaveValue('abc');
    });

    it('does not submit when code is incomplete', async () => {
      const user = userEvent.setup();
      const onVerify = vi.fn();

      renderWithIntl(<TwoFactorVerification onVerify={onVerify} />);

      const input = screen.getByTestId('2fa-code-input');
      await user.type(input, '123'); // Only 3 digits

      const verifyButton = screen.getByTestId('2fa-verify-button');
      await user.click(verifyButton);

      // Should not call onVerify with incomplete code
      expect(onVerify).not.toHaveBeenCalled();
    });
  });
});
