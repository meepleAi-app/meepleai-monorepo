/**
 * LoginForm Tests
 *
 * Covers v2 primitives migration (InputField + PwdInput + Btn):
 * - Renders form + submit button with localized label
 * - Eye toggle renders and toggles password visibility
 * - aria-invalid surfaces on validation error
 * - onSubmit receives parsed form data
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';
import { describe, it, expect, vi } from 'vitest';
import React from 'react';

import { LoginForm } from '../LoginForm';

const loginMessages: Record<string, string> = {
  'auth.login.emailLabel': 'Email',
  'auth.login.emailPlaceholder': 'you@example.com',
  'auth.login.passwordLabel': 'Password',
  'auth.login.passwordPlaceholder': 'Your password',
  'auth.login.loginButton': 'Accedi',
  'auth.login.loggingIn': 'Logging in...',
  'auth.visibility.show': 'Mostra password',
  'auth.visibility.hide': 'Nascondi password',
  'validation.emailRequired': 'Email is required',
  'validation.invalidEmail': 'Invalid email',
  'validation.passwordMin': 'Password must be at least 8 characters',
  'validation.passwordMax': 'Password must be at most 100 characters',
};

function renderWithIntl(ui: React.ReactElement) {
  return render(
    <IntlProvider locale="en" messages={loginMessages}>
      {ui}
    </IntlProvider>
  );
}

describe('LoginForm (v2 primitives)', () => {
  it('renders the form with email and password labels', () => {
    renderWithIntl(<LoginForm onSubmit={vi.fn()} />);
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
    // Use exact match to avoid colliding with the eye-toggle button's
    // aria-label ("Mostra password" / "Nascondi password").
    expect(screen.getByLabelText('Password')).toBeInTheDocument();
    expect(screen.getByTestId('login-form')).toBeInTheDocument();
  });

  it('renders submit button with localized label', () => {
    renderWithIntl(<LoginForm onSubmit={vi.fn()} />);
    expect(screen.getByRole('button', { name: /accedi/i })).toBeInTheDocument();
  });

  it('calls onSubmit with the form data when valid', async () => {
    const user = userEvent.setup();
    const handleSubmit = vi.fn();

    renderWithIntl(<LoginForm onSubmit={handleSubmit} />);

    await user.type(screen.getByLabelText(/email/i), 'user@example.com');
    await user.type(screen.getByLabelText('Password'), 'SecurePass1');
    await user.click(screen.getByRole('button', { name: /accedi/i }));

    await waitFor(() => {
      expect(handleSubmit).toHaveBeenCalledWith({
        email: 'user@example.com',
        password: 'SecurePass1',
      });
    });
  });

  it('renders password field with eye toggle', () => {
    renderWithIntl(<LoginForm onSubmit={vi.fn()} />);
    expect(screen.getByRole('button', { name: /mostra password/i })).toBeInTheDocument();
  });

  it('toggles password visibility', () => {
    renderWithIntl(<LoginForm onSubmit={vi.fn()} />);
    const pwd = screen.getByLabelText('Password');
    expect(pwd).toHaveAttribute('type', 'password');
    fireEvent.click(screen.getByRole('button', { name: /mostra password/i }));
    expect(pwd).toHaveAttribute('type', 'text');
  });

  it('marks email aria-invalid on validation error', async () => {
    renderWithIntl(<LoginForm onSubmit={vi.fn()} />);
    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: '' } });
    fireEvent.submit(screen.getByTestId('login-form'));
    const email = await screen.findByLabelText(/email/i);
    await waitFor(() => {
      expect(email).toHaveAttribute('aria-invalid', 'true');
    });
  });
});
