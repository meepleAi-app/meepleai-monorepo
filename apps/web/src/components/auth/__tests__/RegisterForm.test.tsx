/**
 * RegisterForm Tests
 *
 * Covers v2 primitives migration (InputField + PwdInput + Btn):
 * - Renders form with email, password, terms checkbox, submit button
 * - Strength meter appears after typing password (PwdInput showStrength)
 * - Submit is blocked until terms checkbox is checked
 * - onSubmit receives { email, password, termsAcceptedAt, honeypot }
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { IntlProvider } from 'react-intl';
import { describe, it, expect, vi } from 'vitest';
import React from 'react';

import { RegisterForm } from '../RegisterForm';

const registerMessages: Record<string, string> = {
  'auth.register.emailLabel': 'Email',
  'auth.register.emailPlaceholder': 'you@example.com',
  'auth.register.passwordLabel': 'Password',
  'auth.register.passwordPlaceholder': 'Your password',
  'auth.register.registerButton': 'Registrati',
  'auth.register.creatingAccount': 'Creazione account...',
  'auth.register.termsPrefix': 'Accetto i',
  'auth.register.termsLink': 'Termini di servizio',
  'auth.register.termsAnd': 'e la',
  'auth.register.privacyLink': 'Privacy Policy',
  'auth.register.termsRequired': 'Devi accettare i termini per continuare',
  'auth.visibility.show': 'Mostra password',
  'auth.visibility.hide': 'Nascondi password',
  'auth.meter.prefix': 'Password:',
  'auth.meter.weak': 'Debole',
  'auth.meter.fair': 'Discreta',
  'auth.meter.good': 'Buona',
  'auth.meter.strong': 'Ottima',
  'validation.emailRequired': 'Email is required',
  'validation.invalidEmail': 'Invalid email',
  'validation.passwordMin': 'Password must be at least 8 characters',
  'validation.passwordMax': 'Password must be at most 100 characters',
};

function renderWithIntl(ui: React.ReactElement) {
  return render(
    <IntlProvider locale="en" messages={registerMessages}>
      {ui}
    </IntlProvider>
  );
}

describe('RegisterForm (v2 primitives)', () => {
  it('renders the form with email and password fields', () => {
    renderWithIntl(<RegisterForm onSubmit={vi.fn()} />);
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
    // Use exact match to avoid the eye-toggle button's aria-label
    // ("Mostra password" / "Nascondi password") matching as well.
    expect(screen.getByLabelText('Password')).toBeInTheDocument();
    expect(screen.getByTestId('register-form')).toBeInTheDocument();
  });

  it('renders submit button with localized label', () => {
    renderWithIntl(<RegisterForm onSubmit={vi.fn()} />);
    expect(screen.getByRole('button', { name: /registrati/i })).toBeInTheDocument();
  });

  it('renders a terms-and-conditions checkbox', () => {
    renderWithIntl(<RegisterForm onSubmit={vi.fn()} />);
    expect(screen.getByTestId('register-terms')).toBeInTheDocument();
  });

  it('shows strength meter after typing password', () => {
    renderWithIntl(<RegisterForm onSubmit={vi.fn()} />);
    fireEvent.change(screen.getByLabelText('Password'), {
      target: { value: 'abc123' },
    });
    expect(screen.getByRole('status')).toBeInTheDocument();
  });

  it('blocks submit until terms checkbox checked', async () => {
    const onSubmit = vi.fn();
    renderWithIntl(<RegisterForm onSubmit={onSubmit} />);

    fireEvent.change(screen.getByLabelText(/email/i), {
      target: { value: 'a@b.co' },
    });
    fireEvent.change(screen.getByLabelText('Password'), {
      target: { value: 'SecurePass1!' },
    });
    fireEvent.submit(screen.getByTestId('register-form'));

    expect(await screen.findByText(/accetta.*(termini|condizioni)/i)).toBeInTheDocument();
    expect(onSubmit).not.toHaveBeenCalled();
  });

  it('calls onSubmit with termsAcceptedAt when form is valid and terms accepted', async () => {
    const user = userEvent.setup();
    const handleSubmit = vi.fn();

    renderWithIntl(<RegisterForm onSubmit={handleSubmit} />);

    await user.type(screen.getByLabelText(/email/i), 'user@example.com');
    await user.type(screen.getByLabelText('Password'), 'SecurePass1!');
    await user.click(screen.getByTestId('register-terms'));
    await user.click(screen.getByRole('button', { name: /registrati/i }));

    await waitFor(() => {
      expect(handleSubmit).toHaveBeenCalledTimes(1);
    });

    const arg = handleSubmit.mock.calls[0][0] as {
      email: string;
      password: string;
      termsAcceptedAt: Date;
      honeypot?: string;
    };
    expect(arg.email).toBe('user@example.com');
    expect(arg.password).toBe('SecurePass1!');
    expect(arg.termsAcceptedAt).toBeInstanceOf(Date);
  });

  it('marks email aria-invalid on validation error', async () => {
    renderWithIntl(<RegisterForm onSubmit={vi.fn()} />);
    fireEvent.change(screen.getByLabelText(/email/i), { target: { value: '' } });
    fireEvent.submit(screen.getByTestId('register-form'));
    const email = await screen.findByLabelText(/email/i);
    await waitFor(() => {
      expect(email).toHaveAttribute('aria-invalid', 'true');
    });
  });
});
