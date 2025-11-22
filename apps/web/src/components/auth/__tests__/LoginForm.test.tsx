/**
 * LoginForm Tests (FE-IMP-006)
 *
 * Tests for LoginForm component with React Hook Form + Zod.
 * Includes:
 * - Form submission (success/error)
 * - Client-side validation (Zod schema)
 * - Keyboard navigation
 * - Accessibility (jest-axe)
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { axe, toHaveNoViolations } from 'jest-axe';
import { LoginForm } from '../LoginForm';
import * as authActions from '@/actions/auth';

expect.extend(toHaveNoViolations);

// Mock Server Action
jest.mock('@/actions/auth', () => ({
  loginAction: jest.fn(),
}));

describe('LoginForm', () => {
  const mockOnSuccess = jest.fn();
  const mockLoginAction = authActions.loginAction as jest.MockedFunction<
    typeof authActions.loginAction
  >;

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render email and password fields', () => {
      render(<LoginForm />);

      expect(screen.getByLabelText(/email/i)).toBeInTheDocument();
      expect(screen.getByLabelText(/password/i)).toBeInTheDocument();
    });

    it('should render submit button with correct text', () => {
      render(<LoginForm />);

      expect(screen.getByRole('button', { name: /accedi/i })).toBeInTheDocument();
    });

    it('should pre-fill email field when initialEmail is provided', () => {
      render(<LoginForm initialEmail="test@example.com" />);

      const emailInput = screen.getByLabelText(/email/i) as HTMLInputElement;
      expect(emailInput.value).toBe('test@example.com');
    });
  });

  describe('Validation (Zod)', () => {
    it('should show validation error for empty email', async () => {
      const user = userEvent.setup();
      render(<LoginForm />);

      const submitButton = screen.getByRole('button', { name: /accedi/i });
      await user.click(submitButton);

      await waitFor(() => {
        // Both email and password show "Campo obbligatorio"
        const errors = screen.getAllByText(/campo obbligatorio/i);
        expect(errors.length).toBeGreaterThanOrEqual(1);
      });
    });

    it('should show validation error for invalid email format', async () => {
      const user = userEvent.setup();
      render(<LoginForm />);

      const emailInput = screen.getByLabelText(/email/i);
      await user.type(emailInput, 'invalid-email');

      const submitButton = screen.getByRole('button', { name: /accedi/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/email non valida/i)).toBeInTheDocument();
      });
    });

    it('should show validation error for empty password', async () => {
      const user = userEvent.setup();
      render(<LoginForm />);

      const emailInput = screen.getByLabelText(/email/i);
      await user.type(emailInput, 'test@example.com');

      const submitButton = screen.getByRole('button', { name: /accedi/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getAllByText(/campo obbligatorio/i)).toHaveLength(1);
      });
    });
  });

  describe('Form Submission', () => {
    it('should call loginAction with correct data on successful validation', async () => {
      const user = userEvent.setup();
      mockLoginAction.mockResolvedValue({
        success: true,
        user: { id: '1', email: 'test@example.com', displayName: 'Test User', role: 'user' },
      });

      render(<LoginForm onSuccess={mockOnSuccess} />);

      const emailInput = screen.getByLabelText(/email/i);
      const passwordInput = screen.getByLabelText(/password/i);

      await user.type(emailInput, 'test@example.com');
      await user.type(passwordInput, 'password123');

      const submitButton = screen.getByRole('button', { name: /accedi/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(mockLoginAction).toHaveBeenCalled();
        const formData = mockLoginAction.mock.calls[0][1] as FormData;
        expect(formData.get('email')).toBe('test@example.com');
        expect(formData.get('password')).toBe('password123');
      });
    });

    it('should call onSuccess callback when login succeeds', async () => {
      const user = userEvent.setup();
      const mockUser = { id: '1', email: 'test@example.com', displayName: 'Test User', role: 'user' };
      mockLoginAction.mockResolvedValue({
        success: true,
        user: mockUser,
      });

      render(<LoginForm onSuccess={mockOnSuccess} />);

      const emailInput = screen.getByLabelText(/email/i);
      const passwordInput = screen.getByLabelText(/password/i);

      await user.type(emailInput, 'test@example.com');
      await user.type(passwordInput, 'password123');

      const submitButton = screen.getByRole('button', { name: /accedi/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(mockOnSuccess).toHaveBeenCalledWith(mockUser);
      });
    });

    it('should show server error message when login fails', async () => {
      const user = userEvent.setup();
      mockLoginAction.mockResolvedValue({
        success: false,
        error: { type: 'auth' as const, message: 'Credenziali non valide' },
      });

      render(<LoginForm />);

      const emailInput = screen.getByLabelText(/email/i);
      const passwordInput = screen.getByLabelText(/password/i);

      await user.type(emailInput, 'test@example.com');
      await user.type(passwordInput, 'wrongpassword');

      const submitButton = screen.getByRole('button', { name: /accedi/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/credenziali non valide/i)).toBeInTheDocument();
      });
    });
  });

  describe('Loading State', () => {
    it('should disable inputs and show loading text during submission', async () => {
      const user = userEvent.setup();
      mockLoginAction.mockImplementation(() => new Promise(() => {})); // Never resolves

      render(<LoginForm />);

      const emailInput = screen.getByLabelText(/email/i);
      const passwordInput = screen.getByLabelText(/password/i);

      await user.type(emailInput, 'test@example.com');
      await user.type(passwordInput, 'password123');

      const submitButton = screen.getByRole('button', { name: /accedi/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/accesso in corso/i)).toBeInTheDocument();
        expect(emailInput).toBeDisabled();
        expect(passwordInput).toBeDisabled();
      });
    });
  });

  describe('Keyboard Navigation', () => {
    it('should support keyboard navigation between fields', async () => {
      const user = userEvent.setup();
      render(<LoginForm />);

      const emailInput = screen.getByLabelText(/email/i);
      const passwordInput = screen.getByLabelText(/password/i);

      await user.click(emailInput);
      expect(emailInput).toHaveFocus();

      await user.tab();
      expect(passwordInput).toHaveFocus();
    });

    it('should submit form on Enter key press', async () => {
      const user = userEvent.setup();
      mockLoginAction.mockResolvedValue({
        success: true,
        user: { id: '1', email: 'test@example.com', displayName: 'Test User', role: 'user' },
      });

      render(<LoginForm />);

      const emailInput = screen.getByLabelText(/email/i);
      const passwordInput = screen.getByLabelText(/password/i);

      await user.type(emailInput, 'test@example.com');
      await user.type(passwordInput, 'password123{Enter}');

      await waitFor(() => {
        expect(mockLoginAction).toHaveBeenCalled();
      });
    });
  });

  describe('Accessibility', () => {
    it('should have no accessibility violations', async () => {
      const { container } = render(<LoginForm />);
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('should announce errors with aria-live', async () => {
      const user = userEvent.setup();
      mockLoginAction.mockResolvedValue({
        success: false,
        error: { type: 'auth' as const, message: 'Errore di autenticazione' },
      });

      render(<LoginForm />);

      const emailInput = screen.getByLabelText(/email/i);
      const passwordInput = screen.getByLabelText(/password/i);

      await user.type(emailInput, 'test@example.com');
      await user.type(passwordInput, 'password123');

      const submitButton = screen.getByRole('button', { name: /accedi/i });
      await user.click(submitButton);

      await waitFor(() => {
        const errorAlert = screen.getByRole('alert');
        expect(errorAlert).toHaveAttribute('aria-live', 'polite');
      });
    });
  });
});
