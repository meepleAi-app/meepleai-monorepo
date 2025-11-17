/**
 * RegisterForm Tests (FE-IMP-006)
 *
 * Tests for RegisterForm component with React Hook Form + Zod.
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe, toHaveNoViolations } from 'jest-axe';
import { RegisterForm } from '../RegisterForm';
import * as authActions from '@/actions/auth';

expect.extend(toHaveNoViolations);

jest.mock('@/actions/auth', () => ({
  registerAction: jest.fn(),
}));

describe('RegisterForm', () => {
  const mockOnSuccess = jest.fn();
  const mockRegisterAction = authActions.registerAction as jest.MockedFunction<
    typeof authActions.registerAction
  >;

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render all required fields', () => {
      render(<RegisterForm />);

      expect(screen.getByLabelText(/^email$/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/nome visualizzato/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/^password$/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/conferma password/i)).toBeInTheDocument();
    });

    it('should show password requirements hint', () => {
      render(<RegisterForm />);

      expect(
        screen.getByText(/deve contenere maiuscole, minuscole e numeri/i)
      ).toBeInTheDocument();
    });
  });

  describe('Validation (Zod)', () => {
    it('should show error for invalid email', async () => {
      const user = userEvent.setup();
      render(<RegisterForm />);

      const emailInput = screen.getByLabelText(/^email$/i);
      await user.type(emailInput, 'invalid-email');

      const submitButton = screen.getByRole('button', { name: /crea account/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/email non valida/i)).toBeInTheDocument();
      });
    });

    it('should show error for weak password', async () => {
      const user = userEvent.setup();
      render(<RegisterForm />);

      const emailInput = screen.getByLabelText(/^email$/i);
      const passwordInput = screen.getByLabelText(/^password$/i);

      await user.type(emailInput, 'test@example.com');
      await user.type(passwordInput, 'weak');

      const submitButton = screen.getByRole('button', { name: /crea account/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(
          screen.getByText(/la password deve contenere almeno 8 caratteri/i)
        ).toBeInTheDocument();
      });
    });

    it('should show error when passwords do not match', async () => {
      const user = userEvent.setup();
      render(<RegisterForm />);

      const emailInput = screen.getByLabelText(/^email$/i);
      const passwordInput = screen.getByLabelText(/^password$/i);
      const confirmPasswordInput = screen.getByLabelText(/conferma password/i);

      await user.type(emailInput, 'test@example.com');
      await user.type(passwordInput, 'Password123');
      await user.type(confirmPasswordInput, 'DifferentPassword123');

      const submitButton = screen.getByRole('button', { name: /crea account/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/le password non coincidono/i)).toBeInTheDocument();
      });
    });
  });

  describe('Form Submission', () => {
    it('should call registerAction with correct data', async () => {
      const user = userEvent.setup();
      mockRegisterAction.mockResolvedValue({
        success: true,
        user: { id: '1', email: 'test@example.com', displayName: 'Test User', role: 'user' },
      });

      render(<RegisterForm onSuccess={mockOnSuccess} />);

      await user.type(screen.getByLabelText(/^email$/i), 'test@example.com');
      await user.type(screen.getByLabelText(/nome visualizzato/i), 'Test User');
      await user.type(screen.getByLabelText(/^password$/i), 'Password123');
      await user.type(screen.getByLabelText(/conferma password/i), 'Password123');

      const submitButton = screen.getByRole('button', { name: /crea account/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(mockRegisterAction).toHaveBeenCalled();
        const formData = mockRegisterAction.mock.calls[0][1] as FormData;
        expect(formData.get('email')).toBe('test@example.com');
        expect(formData.get('displayName')).toBe('Test User');
        expect(formData.get('password')).toBe('Password123');
      });
    });

    it('should call onSuccess when registration succeeds', async () => {
      const user = userEvent.setup();
      const mockUser = { id: '1', email: 'test@example.com', displayName: 'Test User', role: 'user' };
      mockRegisterAction.mockResolvedValue({
        success: true,
        user: mockUser,
      });

      render(<RegisterForm onSuccess={mockOnSuccess} />);

      await user.type(screen.getByLabelText(/^email$/i), 'test@example.com');
      await user.type(screen.getByLabelText(/^password$/i), 'Password123');
      await user.type(screen.getByLabelText(/conferma password/i), 'Password123');

      const submitButton = screen.getByRole('button', { name: /crea account/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(mockOnSuccess).toHaveBeenCalledWith(mockUser);
      });
    });
  });

  describe('Accessibility', () => {
    it('should have no accessibility violations', async () => {
      const { container } = render(<RegisterForm />);
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should announce password requirement hint', () => {
      render(<RegisterForm />);

      const passwordInput = screen.getByLabelText(/^password$/i);
      const hintId = passwordInput.getAttribute('aria-describedby');

      expect(hintId).toBeTruthy();
      const hint = document.getElementById(hintId!.split(' ')[0]);
      expect(hint).toHaveTextContent(/deve contenere maiuscole/i);
    });
  });
});
