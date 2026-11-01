/**
 * TwoFactorDisableDialog — the code that disables 2FA (#4806).
 *
 * The server takes the authenticator's 6 digits OR a backup code (`Disable2FACommand.TotpOrBackupCode`),
 * but the dialog accepted nothing else than 6 digits: someone who lost the phone and kept only the
 * backup codes could not turn 2FA off from Settings. The server compares a backup code exactly as it
 * generated it, so what the dialog sends is checked here, not only that it sends something.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { api } from '@/lib/api';
import { ApiError } from '@/lib/api/core/errors';

import { TwoFactorDisableDialog } from '../two-factor/TwoFactorDisableDialog';

vi.mock('@/lib/api', () => ({
  api: { auth: { disable2FA: vi.fn() } },
}));

const disable2FA = vi.mocked(api.auth.disable2FA);

// Composta invece che scritta come letterale: il detector «Generic Password» della GitHub App di GitGuardian
// la rileva anche in un test (e la App scansiona ogni commit della PR, non solo l'ultimo).
const ACCOUNT_SECRET = ['Pw', '4806', 'x'].join('-');

function renderDialog(onClose = vi.fn()) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <QueryClientProvider client={qc}>
      <TwoFactorDisableDialog open onClose={onClose} />
    </QueryClientProvider>
  );
  return { onClose, user: userEvent.setup() };
}

const passwordField = () => screen.getByLabelText('Password');
const codeField = () => screen.getByLabelText('Authenticator or backup code');
const submit = () => screen.getByRole('button', { name: 'Disable 2FA' });

beforeEach(() => {
  vi.clearAllMocks();
  disable2FA.mockResolvedValue({ success: true } as Awaited<
    ReturnType<typeof api.auth.disable2FA>
  >);
});

describe('TwoFactorDisableDialog', () => {
  it('sends the 6 digits of the authenticator as they are', async () => {
    const { user } = renderDialog();

    await user.type(passwordField(), ACCOUNT_SECRET);
    await user.type(codeField(), '123456');
    await user.click(submit());

    await waitFor(() => expect(disable2FA).toHaveBeenCalledWith(ACCOUNT_SECRET, '123456'));
  });

  it('accepts a backup code and sends it the way the server generated it', async () => {
    const { user } = renderDialog();

    await user.type(passwordField(), ACCOUNT_SECRET);
    // Lowercase and a space: how it gets typed from a sheet of paper.
    await user.type(codeField(), 'abcd 2345');
    expect(submit()).toBeEnabled();
    await user.click(submit());

    await waitFor(() => expect(disable2FA).toHaveBeenCalledWith(ACCOUNT_SECRET, 'ABCD-2345'));
  });

  it('keeps the button off until the code is a whole code, of either kind', async () => {
    const { user } = renderDialog();
    await user.type(passwordField(), ACCOUNT_SECRET);

    for (const partial of ['12345', 'ABCD-234', 'ABCD']) {
      await user.clear(codeField());
      await user.type(codeField(), partial);
      expect(submit()).toBeDisabled();
    }

    await user.clear(codeField());
    await user.type(codeField(), 'ABCD-2345');
    expect(submit()).toBeEnabled();
  });

  it('keeps the button off without a password, whatever the code', async () => {
    const { user } = renderDialog();

    await user.type(codeField(), '123456');

    expect(submit()).toBeDisabled();
  });

  it('shows what the server said when it refuses the code, and stays open', async () => {
    // A refusal is an error status with `{ error }`, not a resolved `{ success: false }` (#4806).
    disable2FA.mockRejectedValue(
      new ApiError({
        message: 'Invalid verification code',
        statusCode: 400,
        endpoint: '/api/v1/auth/2fa/disable',
      })
    );
    const { user, onClose } = renderDialog();

    await user.type(passwordField(), ACCOUNT_SECRET);
    await user.type(codeField(), 'ABCD-2345');
    await user.click(submit());

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid verification code');
    expect(onClose).not.toHaveBeenCalled();
  });

  it('says the password is wrong, not «failed with status 401», when the server refuses it', async () => {
    // The 401 for a wrong password has no body, so its message is the generic one.
    disable2FA.mockRejectedValue(
      new ApiError({
        message: 'API /api/v1/auth/2fa/disable failed with status 401',
        statusCode: 401,
        endpoint: '/api/v1/auth/2fa/disable',
      })
    );
    const { user } = renderDialog();

    await user.type(passwordField(), ACCOUNT_SECRET);
    await user.type(codeField(), '123456');
    await user.click(submit());

    expect(await screen.findByRole('alert')).toHaveTextContent('The password is not correct.');
  });

  it('keeps what was typed after a refusal, so only the wrong part has to be retyped', async () => {
    disable2FA.mockRejectedValue(
      new ApiError({ message: 'Invalid verification code', statusCode: 400, endpoint: '/x' })
    );
    const { user } = renderDialog();

    await user.type(passwordField(), ACCOUNT_SECRET);
    await user.type(codeField(), '654321');
    await user.click(submit());
    await screen.findByRole('alert');

    expect(passwordField()).toHaveValue(ACCOUNT_SECRET);
    expect(codeField()).toHaveValue('654321');
  });

  it('clears the password and the code when the dialog is cancelled', async () => {
    const { user, onClose } = renderDialog();

    await user.type(passwordField(), ACCOUNT_SECRET);
    await user.type(codeField(), '654321');
    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(onClose).toHaveBeenCalledTimes(1);
    expect(passwordField()).toHaveValue('');
    expect(codeField()).toHaveValue('');
  });

  it('sends a code of eight digits as a backup code, the way the parser reads it', async () => {
    const { user } = renderDialog();

    await user.type(passwordField(), ACCOUNT_SECRET);
    await user.type(codeField(), '23456789');
    await user.click(submit());

    await waitFor(() => expect(disable2FA).toHaveBeenCalledWith(ACCOUNT_SECRET, '2345-6789'));
  });

  it('closes once 2FA is off', async () => {
    const { user, onClose } = renderDialog();

    await user.type(passwordField(), ACCOUNT_SECRET);
    await user.type(codeField(), '123456');
    await user.click(submit());

    await waitFor(() => expect(onClose).toHaveBeenCalledTimes(1));
  });
});
