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
    it('does not render an inline header by default (wrapper-supplied)', () => {
      // #1816 P3-4: AuthCard / Dialog wrappers already render h1 + subtitle.
      // Component default-renders form-only to avoid duplicate heading reported
      // by audit (h1 IT + h2 EN observed simultaneously on /login 2FA step).
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      // Inline header (h2 + paragraph) should NOT be in DOM when title/subtitle
      // props are absent.
      expect(screen.queryByRole('heading', { level: 2 })).not.toBeInTheDocument();
    });

    it('renders inline header when title prop is provided', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} title="Custom Title" />);

      // Custom title renders as h2 inside the component.
      const heading = screen.getByRole('heading', { level: 2 });
      expect(heading).toHaveTextContent('Custom Title');
    });

    it('renders custom subtitle when subtitle prop is provided', () => {
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
      renderWithIntl(<TwoFactorVerification {...defaultProps} showRememberDevice={false} />);

      expect(screen.queryByTestId('remember-device-checkbox')).not.toBeInTheDocument();
    });

    it('renders verify button', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} />);

      expect(screen.getByTestId('2fa-verify-button')).toBeInTheDocument();
    });

    it('renders backup code link when onUseBackupCode is provided', () => {
      const onUseBackupCode = vi.fn();
      renderWithIntl(<TwoFactorVerification {...defaultProps} onUseBackupCode={onUseBackupCode} />);

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
        <TwoFactorVerification {...defaultProps} error="Invalid code. Please try again." />
      );

      expect(screen.getByText('Invalid code. Please try again.')).toBeInTheDocument();
    });

    it('displays error with destructive styling', () => {
      renderWithIntl(<TwoFactorVerification {...defaultProps} error="Error message" />);

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

      renderWithIntl(<TwoFactorVerification {...defaultProps} onUseBackupCode={onUseBackupCode} />);

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
      renderWithIntl(<TwoFactorVerification {...defaultProps} error="Error message" />);

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

  // #4817: at login the field is the way in for someone who lost the phone, so it has to take a backup
  // code (`XXXX-XXXX`, nine characters) and hand the server the form it compares EXACTLY.
  describe('Backup codes (allowBackupCode, #4817)', () => {
    const renderBackup = (onVerify = vi.fn()) => {
      renderWithIntl(<TwoFactorVerification onVerify={onVerify} allowBackupCode />);
      return { onVerify, input: screen.getByTestId('2fa-code-input') };
    };

    it('holds a whole backup code, dash included', async () => {
      const user = userEvent.setup();
      const { input } = renderBackup();

      await user.type(input, 'abcd-2345');

      expect(input).toHaveValue('ABCD-2345');
    });

    it('sends a backup code typed as read off the sheet in the form the server compares', async () => {
      const user = userEvent.setup();
      const { onVerify, input } = renderBackup();

      await user.type(input, 'abcd 2345');
      await user.click(screen.getByTestId('2fa-verify-button'));

      await waitFor(() => {
        expect(onVerify).toHaveBeenCalledWith({ code: 'ABCD-2345', rememberDevice: false });
      });
    });

    it('sends a pasted backup code with a typographic dash and a trailing space', async () => {
      const user = userEvent.setup();
      const { onVerify, input } = renderBackup();

      await user.click(input);
      await user.paste('abcd–2345 ');
      await user.click(screen.getByTestId('2fa-verify-button'));

      await waitFor(() => {
        expect(onVerify).toHaveBeenCalledWith({ code: 'ABCD-2345', rememberDevice: false });
      });
    });

    it('keeps submitting six digits on its own, as the authenticator code is the usual case', async () => {
      const user = userEvent.setup();
      const { onVerify, input } = renderBackup();

      await user.type(input, '123456');

      await waitFor(() => {
        expect(onVerify).toHaveBeenCalledTimes(1);
      });
      expect(onVerify).toHaveBeenCalledWith({ code: '123456', rememberDevice: false });
    });

    it('does not submit a backup code that is still being typed', async () => {
      const user = userEvent.setup();
      const { onVerify, input } = renderBackup();

      await user.type(input, 'ABCD-234');

      expect(screen.getByTestId('2fa-verify-button')).toBeDisabled();
      expect(onVerify).not.toHaveBeenCalled();
    });

    it('offers the backup-code link without a callback, and it brings up the letters keyboard', async () => {
      const user = userEvent.setup();
      const { input } = renderBackup();

      expect(input).toHaveAttribute('inputMode', 'numeric');
      await user.click(screen.getByTestId('use-backup-code-link'));

      expect(input).toHaveAttribute('inputMode', 'text');
      expect(input).toHaveAttribute('placeholder', 'XXXX-XXXX');
    });

    it('still tells the parent when the backup-code link is used', async () => {
      const user = userEvent.setup();
      const onUseBackupCode = vi.fn();
      renderWithIntl(
        <TwoFactorVerification
          onVerify={vi.fn()}
          onUseBackupCode={onUseBackupCode}
          allowBackupCode
        />
      );

      await user.click(screen.getByTestId('use-backup-code-link'));

      expect(onUseBackupCode).toHaveBeenCalledTimes(1);
    });

    // Six digits are also the start of a backup code made of digits (the server's alphabet has 2-9), and
    // every wrong attempt counts toward the lockout.
    it('does not send six digits that are the start of a longer backup code', async () => {
      const user = userEvent.setup();
      const { onVerify, input } = renderBackup();

      await user.type(input, '2345 6789');
      await new Promise(resolve => setTimeout(resolve, 700)); // longer than the wait before an automatic submit

      expect(input).toHaveValue('23456789');
      expect(onVerify).not.toHaveBeenCalled();
    });

    it('does not send six digits on its own once the person said it is a backup code', async () => {
      const user = userEvent.setup();
      const { onVerify, input } = renderBackup();

      await user.click(screen.getByTestId('use-backup-code-link'));
      await user.type(input, '234567');
      await new Promise(resolve => setTimeout(resolve, 700));

      expect(onVerify).not.toHaveBeenCalled();
      await user.click(screen.getByTestId('2fa-verify-button'));
      await waitFor(() => {
        expect(onVerify).toHaveBeenCalledWith({ code: '234567', rememberDevice: false });
      });
    });

    it('shows no error while a valid backup code is being typed', async () => {
      const user = userEvent.setup();
      const { input } = renderBackup();

      for (const typed of ['ABCD', '-', '2', '34']) {
        await user.type(input, typed);
        expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      }
      expect(input).toHaveValue('ABCD-234');
    });

    it('says in words, to a screen reader, that the field now takes a backup code', async () => {
      const user = userEvent.setup();
      renderBackup();

      const linkLabel = screen.getByTestId('use-backup-code-link').textContent ?? '';
      expect(linkLabel).not.toBe('');
      expect(screen.getByTestId('2fa-mode-status')).toBeEmptyDOMElement();
      await user.click(screen.getByTestId('use-backup-code-link'));

      expect(screen.getByTestId('2fa-mode-status')).toHaveTextContent(linkLabel);
    });

    it('leaves the authenticator-only field (step-up) as it was', () => {
      renderWithIntl(<TwoFactorVerification onVerify={vi.fn()} />);

      expect(screen.queryByTestId('use-backup-code-link')).not.toBeInTheDocument();
      expect(screen.getByTestId('2fa-code-input')).toHaveAttribute('maxLength', '8');
    });
  });
});
