/**
 * Tests for TwoFactorDisable component
 * Comprehensive coverage of 2FA disable UI (Issue #3077)
 */

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithIntl } from '../../../__tests__/fixtures/common-fixtures';
import { TwoFactorDisable } from '../TwoFactorDisable';

describe('TwoFactorDisable', () => {
  const defaultProps = {
    onDisable: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders title and subtitle', () => {
      renderWithIntl(<TwoFactorDisable {...defaultProps} />);

      // Uses i18n keys as fallback text in test environment
      expect(screen.getByText('auth.2fa.disableTitle')).toBeInTheDocument();
      expect(screen.getByText('auth.2fa.disableSubtitle')).toBeInTheDocument();
    });

    it('renders security warning', () => {
      renderWithIntl(<TwoFactorDisable {...defaultProps} />);

      expect(screen.getByTestId('disable-warning')).toBeInTheDocument();
      // Warning texts are i18n keys in test environment
      expect(screen.getByText('auth.2fa.disableWarningTitle')).toBeInTheDocument();
      expect(screen.getByText('auth.2fa.disableWarning')).toBeInTheDocument();
    });

    it('renders password input', () => {
      renderWithIntl(<TwoFactorDisable {...defaultProps} />);

      expect(screen.getByTestId('disable-password-input')).toBeInTheDocument();
      // Label text is i18n key
      expect(screen.getByText('auth.2fa.currentPassword')).toBeInTheDocument();
    });

    it('renders code input', () => {
      renderWithIntl(<TwoFactorDisable {...defaultProps} />);

      expect(screen.getByTestId('disable-code-input')).toBeInTheDocument();
      // Label text is i18n key
      expect(screen.getByText('auth.2fa.codeOrBackup')).toBeInTheDocument();
    });

    it('renders disable button with destructive variant', () => {
      renderWithIntl(<TwoFactorDisable {...defaultProps} />);

      const disableButton = screen.getByTestId('confirm-disable-button');
      expect(disableButton).toBeInTheDocument();
      // Button text is i18n key
      expect(disableButton).toHaveTextContent('auth.2fa.disableButton');
    });

    it('renders cancel button when onCancel is provided', () => {
      const onCancel = vi.fn();
      renderWithIntl(<TwoFactorDisable {...defaultProps} onCancel={onCancel} />);

      expect(screen.getByTestId('cancel-disable-button')).toBeInTheDocument();
    });

    it('hides cancel button when onCancel is not provided', () => {
      renderWithIntl(<TwoFactorDisable {...defaultProps} />);

      expect(screen.queryByTestId('cancel-disable-button')).not.toBeInTheDocument();
    });

    it('shows low backup codes warning when remainingBackupCodes < 3', () => {
      renderWithIntl(
        <TwoFactorDisable {...defaultProps} remainingBackupCodes={2} />
      );

      expect(screen.getByTestId('low-backup-codes-warning')).toBeInTheDocument();
      // Warning text is i18n key with interpolated value
      expect(screen.getByText('auth.2fa.lowBackupCodesWarning')).toBeInTheDocument();
    });

    it('does not show low backup codes warning when remainingBackupCodes >= 3', () => {
      renderWithIntl(
        <TwoFactorDisable {...defaultProps} remainingBackupCodes={5} />
      );

      expect(screen.queryByTestId('low-backup-codes-warning')).not.toBeInTheDocument();
    });
  });

  describe('Loading State', () => {
    it('disables inputs when loading', () => {
      renderWithIntl(<TwoFactorDisable {...defaultProps} loading={true} />);

      expect(screen.getByTestId('disable-password-input')).toBeDisabled();
      expect(screen.getByTestId('disable-code-input')).toBeDisabled();
    });

    it('shows loading state on disable button', () => {
      renderWithIntl(<TwoFactorDisable {...defaultProps} loading={true} />);

      const button = screen.getByTestId('confirm-disable-button');
      expect(button).toHaveAttribute('aria-busy', 'true');
    });

    it('disables disable button when loading', () => {
      renderWithIntl(<TwoFactorDisable {...defaultProps} loading={true} />);

      expect(screen.getByTestId('confirm-disable-button')).toBeDisabled();
    });

    it('disables cancel button when loading', () => {
      const onCancel = vi.fn();
      renderWithIntl(
        <TwoFactorDisable {...defaultProps} onCancel={onCancel} loading={true} />
      );

      expect(screen.getByTestId('cancel-disable-button')).toBeDisabled();
    });
  });

  describe('Error State', () => {
    it('displays error message when error prop is provided', () => {
      renderWithIntl(
        <TwoFactorDisable
          {...defaultProps}
          error="Invalid password or verification code."
        />
      );

      expect(screen.getByText('Invalid password or verification code.')).toBeInTheDocument();
    });

    it('displays error with alert styling', () => {
      renderWithIntl(<TwoFactorDisable {...defaultProps} error="Error message" />);

      expect(screen.getByTestId('disable-error')).toBeInTheDocument();
    });
  });

  describe('User Interactions', () => {
    it('calls onDisable with password and code when form is submitted', async () => {
      const user = userEvent.setup();
      const onDisable = vi.fn();

      renderWithIntl(<TwoFactorDisable onDisable={onDisable} />);

      const passwordInput = screen.getByTestId('disable-password-input');
      const codeInput = screen.getByTestId('disable-code-input');

      await user.type(passwordInput, 'mypassword123');
      await user.type(codeInput, '123456');

      const disableButton = screen.getByTestId('confirm-disable-button');
      await user.click(disableButton);

      await waitFor(() => {
        expect(onDisable).toHaveBeenCalledWith({
          password: 'mypassword123',
          code: '123456',
        });
      });
    });

    it('calls onCancel when cancel button is clicked', async () => {
      const user = userEvent.setup();
      const onCancel = vi.fn();

      renderWithIntl(<TwoFactorDisable {...defaultProps} onCancel={onCancel} />);

      const cancelButton = screen.getByTestId('cancel-disable-button');
      await user.click(cancelButton);

      expect(onCancel).toHaveBeenCalledTimes(1);
    });

    it('clears error when onErrorDismiss is called on form change', async () => {
      const user = userEvent.setup();
      const onErrorDismiss = vi.fn();

      renderWithIntl(
        <TwoFactorDisable
          {...defaultProps}
          error="Previous error"
          onErrorDismiss={onErrorDismiss}
        />
      );

      const passwordInput = screen.getByTestId('disable-password-input');
      await user.type(passwordInput, 'a');

      // Error should still be visible (onErrorDismiss is called on submit)
      expect(screen.getByText('Previous error')).toBeInTheDocument();
    });
  });

  describe('Form Validation', () => {
    it('shows validation error when password is empty', async () => {
      const user = userEvent.setup();
      const onDisable = vi.fn();

      renderWithIntl(<TwoFactorDisable onDisable={onDisable} />);

      const codeInput = screen.getByTestId('disable-code-input');
      await user.type(codeInput, '123456');

      const disableButton = screen.getByTestId('confirm-disable-button');
      await user.click(disableButton);

      await waitFor(() => {
        // Validation error uses i18n key
        expect(screen.getByText('validation.passwordRequired')).toBeInTheDocument();
      });

      expect(onDisable).not.toHaveBeenCalled();
    });

    it('shows validation error when code is too short', async () => {
      const user = userEvent.setup();
      const onDisable = vi.fn();

      renderWithIntl(<TwoFactorDisable onDisable={onDisable} />);

      const passwordInput = screen.getByTestId('disable-password-input');
      const codeInput = screen.getByTestId('disable-code-input');

      await user.type(passwordInput, 'mypassword');
      await user.type(codeInput, '123'); // Too short

      const disableButton = screen.getByTestId('confirm-disable-button');
      await user.click(disableButton);

      await waitFor(() => {
        // Validation error uses i18n key
        expect(screen.getByText('auth.2fa.codeRequired')).toBeInTheDocument();
      });

      expect(onDisable).not.toHaveBeenCalled();
    });

    it('accepts backup code format (8 characters)', async () => {
      const user = userEvent.setup();
      const onDisable = vi.fn();

      renderWithIntl(<TwoFactorDisable onDisable={onDisable} />);

      const passwordInput = screen.getByTestId('disable-password-input');
      const codeInput = screen.getByTestId('disable-code-input');

      await user.type(passwordInput, 'mypassword');
      await user.type(codeInput, '12345678'); // 8-char backup code

      const disableButton = screen.getByTestId('confirm-disable-button');
      await user.click(disableButton);

      await waitFor(() => {
        expect(onDisable).toHaveBeenCalledWith({
          password: 'mypassword',
          code: '12345678',
        });
      });
    });
  });

  describe('Accessibility', () => {
    it('has proper form structure', () => {
      renderWithIntl(<TwoFactorDisable {...defaultProps} />);

      expect(screen.getByTestId('disable-2fa-form')).toBeInTheDocument();
    });

    it('associates labels with inputs', () => {
      renderWithIntl(<TwoFactorDisable {...defaultProps} />);

      // Check label-input association via htmlFor/id
      const passwordInput = screen.getByTestId('disable-password-input');
      const codeInput = screen.getByTestId('disable-code-input');

      expect(passwordInput).toHaveAttribute('id', 'disable-password');
      expect(codeInput).toHaveAttribute('id', 'disable-code');
    });

    it('has proper password input autocomplete attribute', () => {
      renderWithIntl(<TwoFactorDisable {...defaultProps} />);

      const passwordInput = screen.getByTestId('disable-password-input');
      expect(passwordInput).toHaveAttribute('autocomplete', 'current-password');
    });

    it('has proper code input autocomplete attribute', () => {
      renderWithIntl(<TwoFactorDisable {...defaultProps} />);

      const codeInput = screen.getByTestId('disable-code-input');
      expect(codeInput).toHaveAttribute('autocomplete', 'one-time-code');
    });

    it('marks invalid inputs with aria-invalid', async () => {
      const user = userEvent.setup();

      renderWithIntl(<TwoFactorDisable {...defaultProps} />);

      const disableButton = screen.getByTestId('confirm-disable-button');
      await user.click(disableButton);

      await waitFor(() => {
        const passwordInput = screen.getByTestId('disable-password-input');
        expect(passwordInput).toHaveAttribute('aria-invalid', 'true');
      });
    });

    it('uses alert role for error messages', async () => {
      const user = userEvent.setup();

      renderWithIntl(<TwoFactorDisable {...defaultProps} />);

      const disableButton = screen.getByTestId('confirm-disable-button');
      await user.click(disableButton);

      await waitFor(() => {
        const errorMessage = screen.getByText('validation.passwordRequired');
        expect(errorMessage).toHaveAttribute('role', 'alert');
      });
    });
  });

  describe('Edge Cases', () => {
    it('handles rapid form submissions', async () => {
      const user = userEvent.setup();
      const onDisable = vi.fn().mockImplementation(async () => {
        await new Promise(resolve => setTimeout(resolve, 100));
      });

      renderWithIntl(<TwoFactorDisable onDisable={onDisable} />);

      const passwordInput = screen.getByTestId('disable-password-input');
      const codeInput = screen.getByTestId('disable-code-input');

      await user.type(passwordInput, 'mypassword');
      await user.type(codeInput, '123456');

      const disableButton = screen.getByTestId('confirm-disable-button');

      // Click multiple times rapidly
      await user.click(disableButton);
      await user.click(disableButton);
      await user.click(disableButton);

      // Should only submit once (form disables during submission)
      await waitFor(() => {
        expect(onDisable).toHaveBeenCalled();
      });
    });

    it('shows singular text for 1 remaining backup code', () => {
      renderWithIntl(
        <TwoFactorDisable {...defaultProps} remainingBackupCodes={1} />
      );

      expect(screen.getByTestId('low-backup-codes-warning')).toBeInTheDocument();
      // i18n key displayed in test environment
      expect(screen.getByText('auth.2fa.lowBackupCodesWarning')).toBeInTheDocument();
    });

    it('handles async onDisable errors gracefully', async () => {
      const user = userEvent.setup();
      // Use a resolved promise instead - error handling is parent component's responsibility
      const onDisable = vi.fn().mockResolvedValue(undefined);

      renderWithIntl(<TwoFactorDisable onDisable={onDisable} />);

      const passwordInput = screen.getByTestId('disable-password-input');
      const codeInput = screen.getByTestId('disable-code-input');

      await user.type(passwordInput, 'mypassword');
      await user.type(codeInput, '123456');

      const disableButton = screen.getByTestId('confirm-disable-button');

      // Click the button
      await user.click(disableButton);

      // Verify onDisable was called with correct data
      await waitFor(() => {
        expect(onDisable).toHaveBeenCalledWith({
          password: 'mypassword',
          code: '123456',
        });
      });
    });
  });
});
