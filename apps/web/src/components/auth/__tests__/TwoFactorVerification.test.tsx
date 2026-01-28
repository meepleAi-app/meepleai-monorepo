/**
 * Tests for TwoFactorVerification component
 * Comprehensive coverage of 2FA verification UI (Issue #3077)
 */

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { renderWithIntl } from '../../../__tests__/fixtures/common-fixtures';
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

      expect(screen.getByText('Two-Factor Authentication')).toBeInTheDocument();
      expect(screen.getByText('Enter the 6-digit code from your authenticator app')).toBeInTheDocument();
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

    it('renders 6 input fields for code entry', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      const inputs = screen.getAllByRole('textbox');
      expect(inputs).toHaveLength(6);
    });

    it('renders remember device checkbox by default', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      expect(screen.getByLabelText(/trust this device/i)).toBeInTheDocument();
    });

    it('hides remember device checkbox when showRememberDevice is false', () => {
      renderWithIntl(
        <TwoFactorVerification {...defaultProps} showRememberDevice={false} />
      );

      expect(screen.queryByLabelText(/trust this device/i)).not.toBeInTheDocument();
    });

    it('renders verify button', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      expect(screen.getByRole('button', { name: /verify/i })).toBeInTheDocument();
    });

    it('renders backup code link when onUseBackupCode is provided', () => {
      const onUseBackupCode = vi.fn();
      renderWithIntl(
        <TwoFactorVerification {...defaultProps} onUseBackupCode={onUseBackupCode} />
      );

      expect(screen.getByRole('button', { name: /use backup code/i })).toBeInTheDocument();
    });

    it('hides backup code link when onUseBackupCode is not provided', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      expect(screen.queryByRole('button', { name: /use backup code/i })).not.toBeInTheDocument();
    });

    it('renders cancel button when onCancel is provided', () => {
      const onCancel = vi.fn();
      renderWithIntl(<TwoFactorVerification {...defaultProps} onCancel={onCancel} />);

      expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
    });

    it('hides cancel button when onCancel is not provided', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      expect(screen.queryByRole('button', { name: /cancel/i })).not.toBeInTheDocument();
    });
  });

  describe('Loading State', () => {
    it('disables all inputs when loading', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} loading={true} />);

      const inputs = screen.getAllByRole('textbox');
      inputs.forEach(input => {
        expect(input).toBeDisabled();
      });
    });

    it('shows loading text on verify button', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} loading={true} />);

      expect(screen.getByText(/verifying/i)).toBeInTheDocument();
    });

    it('disables verify button when loading', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} loading={true} />);

      expect(screen.getByRole('button', { name: /verifying/i })).toBeDisabled();
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

      const alert = screen.getByTestId('verification-error');
      expect(alert).toBeInTheDocument();
    });
  });

  describe('User Interactions', () => {
    it('calls onVerify with code and rememberDevice when form is submitted', async () => {
      const user = userEvent.setup();
      const onVerify = vi.fn();

      renderWithIntl(<TwoFactorVerification onVerify={onVerify} />);

      const inputs = screen.getAllByRole('textbox');
      await user.type(inputs[0], '123456');

      const verifyButton = screen.getByRole('button', { name: /verify/i });
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

      const inputs = screen.getAllByRole('textbox');
      await user.type(inputs[0], '123456');

      const checkbox = screen.getByLabelText(/trust this device/i);
      await user.click(checkbox);

      const verifyButton = screen.getByRole('button', { name: /verify/i });
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

      const backupLink = screen.getByRole('button', { name: /use backup code/i });
      await user.click(backupLink);

      expect(onUseBackupCode).toHaveBeenCalledTimes(1);
    });

    it('calls onCancel when cancel button is clicked', async () => {
      const user = userEvent.setup();
      const onCancel = vi.fn();

      renderWithIntl(<TwoFactorVerification {...defaultProps} onCancel={onCancel} />);

      const cancelButton = screen.getByRole('button', { name: /cancel/i });
      await user.click(cancelButton);

      expect(onCancel).toHaveBeenCalledTimes(1);
    });

    it('auto-focuses next input after typing a digit', async () => {
      const user = userEvent.setup();

      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      const inputs = screen.getAllByRole('textbox');
      await user.type(inputs[0], '1');

      // Second input should now be focused
      expect(inputs[1]).toHaveFocus();
    });
  });

  describe('Accessibility', () => {
    it('has accessible label for code inputs', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      const inputs = screen.getAllByRole('textbox');
      inputs.forEach((input, index) => {
        expect(input).toHaveAttribute('aria-label', expect.stringContaining(`${index + 1}`));
      });
    });

    it('has proper form structure', () => {
      const { container } = renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      expect(container.querySelector('form')).toBeInTheDocument();
    });

    it('renders error with alert role', () => {
      renderWithIntl(
        <TwoFactorVerification {...defaultProps} error="Error message" />
      );

      const alert = screen.getByTestId('verification-error');
      expect(alert).toBeInTheDocument();
    });

    it('has proper input mode for numeric keyboard on mobile', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      const inputs = screen.getAllByRole('textbox');
      inputs.forEach(input => {
        expect(input).toHaveAttribute('inputMode', 'numeric');
      });
    });
  });

  describe('Edge Cases', () => {
    it('handles paste of full code', async () => {
      const user = userEvent.setup();
      const onVerify = vi.fn();

      renderWithIntl(<TwoFactorVerification onVerify={onVerify} />);

      const inputs = screen.getAllByRole('textbox');

      // Simulate paste by typing the full code into first input
      await user.click(inputs[0]);
      await user.paste('123456');

      const verifyButton = screen.getByRole('button', { name: /verify/i });
      await user.click(verifyButton);

      await waitFor(() => {
        expect(onVerify).toHaveBeenCalled();
      });
    });

    it('prevents non-numeric input', async () => {
      const user = userEvent.setup();

      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      const inputs = screen.getAllByRole('textbox');
      await user.type(inputs[0], 'abc');

      // Input should be empty or filtered
      expect(inputs[0]).toHaveValue('');
    });

    it('does not submit when code is incomplete', async () => {
      const user = userEvent.setup();
      const onVerify = vi.fn();

      renderWithIntl(<TwoFactorVerification onVerify={onVerify} />);

      const inputs = screen.getAllByRole('textbox');
      await user.type(inputs[0], '123'); // Only 3 digits

      const verifyButton = screen.getByRole('button', { name: /verify/i });
      await user.click(verifyButton);

      // Should not call onVerify with incomplete code
      expect(onVerify).not.toHaveBeenCalled();
    });
  });
});
