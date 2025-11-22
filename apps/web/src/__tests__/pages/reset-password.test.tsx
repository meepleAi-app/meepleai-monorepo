/**
 * Password Reset Page Tests (AUTH-04)
 *
 * Comprehensive test suite covering:
 * - Request reset mode (email submission)
 * - Reset password mode (token verification & password reset)
 * - Password validation and strength indicator
 * - Error handling and edge cases
 * - Authentication gate and redirects
 * - Accessibility compliance
 */

import { render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { useRouter } from "next/router";
import ResetPasswordPage from "@/pages/reset-password";
import { api } from "@/lib/api";

// Mock dependencies
jest.mock("next/router", () => ({
  useRouter: jest.fn(),
}));

jest.mock("@/lib/api", () => ({
  api: {
    get: jest.fn(),
    post: jest.fn(),
    put: jest.fn(),
  },
}));

jest.mock("framer-motion", () => ({
  motion: {
    div: ({ children, ...props }: any) => <div {...props}>{children}</div>,
  },
}));

const mockRouter = {
  push: jest.fn(),
  query: {},
};

const mockAuthUser = {
  id: "user-123",
  email: "user@example.com",
  displayName: "Test User",
  role: "User",
};

describe("ResetPasswordPage", () => {
  beforeEach(() => {
    jest.clearAllMocks();
    (useRouter as jest.Mock).mockReturnValue(mockRouter);
  });

  describe("Authentication Gate", () => {
    it("should redirect to /chat if user is already authenticated", async () => {
      (api.get as jest.Mock).mockResolvedValueOnce({
        user: mockAuthUser,
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      });

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(mockRouter.push).toHaveBeenCalledWith("/chat");
      });
    });

    it("should show loading state while checking authentication", () => {
      (api.get as jest.Mock).mockImplementation(
        () => new Promise((resolve) => setTimeout(resolve, 100))
      );

      render(<ResetPasswordPage />);

      expect(screen.getByText(/Loading.../i)).toBeInTheDocument();
    });

    it("should render page content when user is not authenticated", async () => {
      (api.get as jest.Mock).mockResolvedValueOnce(null);

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByText(/Reset Password/i)).toBeInTheDocument();
      });
    });
  });

  describe("Request Reset Mode", () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValueOnce(null); // Not authenticated
      mockRouter.query = {}; // No token
    });

    it("should render request reset form when no token is present", async () => {
      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByText(/Reset Password/i)).toBeInTheDocument();
        expect(
          screen.getByText(/Enter your email address and we'll send you instructions/i)
        ).toBeInTheDocument();
        expect(screen.getByLabelText(/Email Address/i)).toBeInTheDocument();
        expect(screen.getByRole("button", { name: /Send Reset Instructions/i })).toBeInTheDocument();
      });
    });

    it("should require email input before submitting", async () => {
      const user = userEvent.setup();
      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByRole("button", { name: /Send Reset Instructions/i })).toBeInTheDocument();
      });

      const submitButton = screen.getByRole("button", { name: /Send Reset Instructions/i });
      expect(submitButton).toBeDisabled();

      const emailInput = screen.getByLabelText(/Email Address/i);
      await user.type(emailInput, "user@example.com");

      expect(submitButton).toBeEnabled();
    });

    it("should submit email and show success message", async () => {
      const user = userEvent.setup();
      (api.post as jest.Mock).mockResolvedValueOnce({});

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Email Address/i)).toBeInTheDocument();
      });

      const emailInput = screen.getByLabelText(/Email Address/i);
      await user.type(emailInput, "user@example.com");

      const submitButton = screen.getByRole("button", { name: /Send Reset Instructions/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(api.post).toHaveBeenCalledWith("/api/v1/auth/password-reset/request", {
          email: "user@example.com",
        });
        expect(screen.getByText(/Check Your Email/i)).toBeInTheDocument();
        expect(
          screen.getByText(/We've sent password reset instructions to/i)
        ).toBeInTheDocument();
      });
    });

    it("should display error message on request failure", async () => {
      const user = userEvent.setup();
      (api.post as jest.Mock).mockRejectedValueOnce({
        message: "Email not found",
      });

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Email Address/i)).toBeInTheDocument();
      });

      const emailInput = screen.getByLabelText(/Email Address/i);
      await user.type(emailInput, "nonexistent@example.com");

      const submitButton = screen.getByRole("button", { name: /Send Reset Instructions/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByRole("alert")).toHaveTextContent(/Email not found/i);
      });
    });

    it("should allow retrying after success message", async () => {
      const user = userEvent.setup();
      (api.post as jest.Mock).mockResolvedValueOnce({});

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Email Address/i)).toBeInTheDocument();
      });

      const emailInput = screen.getByLabelText(/Email Address/i);
      await user.type(emailInput, "user@example.com");

      const submitButton = screen.getByRole("button", { name: /Send Reset Instructions/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/Check Your Email/i)).toBeInTheDocument();
      });

      // Click "try again" link
      const tryAgainButton = screen.getByRole("button", { name: /try again/i });
      await user.click(tryAgainButton);

      await waitFor(() => {
        expect(screen.getByText(/Reset Password/i)).toBeInTheDocument();
        expect(screen.getByLabelText(/Email Address/i)).toHaveValue("");
      });
    });
  });

  describe("Reset Password Mode - Token Verification", () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValueOnce(null); // Not authenticated
      mockRouter.query = { token: "valid-reset-token" };
    });

    it("should verify token on page load", async () => {
      (api.get as jest.Mock).mockResolvedValueOnce({}); // Token verification success

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(api.get).toHaveBeenCalledWith(
          "/api/v1/auth/password-reset/verify?token=valid-reset-token"
        );
      });
    });

    it("should show loading state during token verification", async () => {
      (api.get as jest.Mock).mockImplementation(
        () => new Promise((resolve) => setTimeout(resolve, 100))
      );

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByText(/Verifying reset token.../i)).toBeInTheDocument();
      });
    });

    it("should render password reset form when token is valid", async () => {
      (api.get as jest.Mock).mockResolvedValueOnce({}); // Token verification success

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByText(/Set New Password/i)).toBeInTheDocument();
        expect(screen.getByLabelText(/New Password/i)).toBeInTheDocument();
        expect(screen.getByLabelText(/Confirm Password/i)).toBeInTheDocument();
        expect(screen.getByRole("button", { name: /Reset Password/i })).toBeInTheDocument();
      });
    });

    it("should show error message when token is invalid", async () => {
      (api.get as jest.Mock).mockRejectedValueOnce({
        message: "Token has expired",
      });

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByText(/Invalid or Expired Link/i)).toBeInTheDocument();
        expect(screen.getByText(/Token has expired/i)).toBeInTheDocument();
        expect(screen.getByRole("link", { name: /Request New Reset Link/i })).toBeInTheDocument();
      });
    });
  });

  describe("Password Validation", () => {
    beforeEach(async () => {
      (api.get as jest.Mock).mockResolvedValueOnce(null); // Not authenticated
      (api.get as jest.Mock).mockResolvedValueOnce({}); // Token verification success
      mockRouter.query = { token: "valid-reset-token" };
    });

    it("should show password strength indicator", async () => {
      const user = userEvent.setup();

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/New Password/i)).toBeInTheDocument();
      });

      const passwordInput = screen.getByLabelText(/New Password/i);

      // Weak password
      await user.type(passwordInput, "weak");
      await waitFor(() => {
        expect(screen.getByText("Weak")).toBeInTheDocument();
      });

      // Clear and type medium password (8 chars, 2-3 requirements: length + lowercase + number, missing uppercase)
      await user.clear(passwordInput);
      await user.type(passwordInput, "medium123");
      await waitFor(() => {
        expect(screen.getByText("Medium")).toBeInTheDocument();
      });

      // Clear and type strong password (meets all requirements)
      await user.clear(passwordInput);
      await user.type(passwordInput, "Strong1Pass!");
      await waitFor(() => {
        expect(screen.getByText("Strong")).toBeInTheDocument();
      });
    });

    it("should show all password requirements", async () => {
      const user = userEvent.setup();

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/New Password/i)).toBeInTheDocument();
      });

      const passwordInput = screen.getByLabelText(/New Password/i);
      await user.type(passwordInput, "Test123!");

      await waitFor(() => {
        expect(screen.getByText(/At least 8 characters/i)).toBeInTheDocument();
        expect(screen.getByText(/At least 1 uppercase letter/i)).toBeInTheDocument();
        expect(screen.getByText(/At least 1 lowercase letter/i)).toBeInTheDocument();
        expect(screen.getByText(/At least 1 number/i)).toBeInTheDocument();
      });
    });

    it("should validate password meets all requirements", async () => {
      const user = userEvent.setup();

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/New Password/i)).toBeInTheDocument();
      });

      const passwordInput = screen.getByLabelText(/New Password/i);
      const submitButton = screen.getByRole("button", { name: /Reset Password/i });

      // Invalid password (too short)
      await user.type(passwordInput, "Short1");
      expect(submitButton).toBeDisabled();

      // Valid password
      await user.clear(passwordInput);
      await user.type(passwordInput, "ValidPass123");
      await user.type(screen.getByLabelText(/Confirm Password/i), "ValidPass123");

      await waitFor(() => {
        expect(submitButton).toBeEnabled();
      });
    });

    it("should show error when passwords do not match", async () => {
      const user = userEvent.setup();

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/New Password/i)).toBeInTheDocument();
      });

      const passwordInput = screen.getByLabelText(/New Password/i);
      const confirmInput = screen.getByLabelText(/Confirm Password/i);

      await user.type(passwordInput, "ValidPass123");
      await user.type(confirmInput, "DifferentPass123");

      await waitFor(() => {
        expect(screen.getByText(/Passwords do not match/i)).toBeInTheDocument();
      });
    });
  });

  describe("Password Reset Submission", () => {
    beforeEach(async () => {
      (api.get as jest.Mock).mockResolvedValueOnce(null); // Not authenticated
      (api.get as jest.Mock).mockResolvedValueOnce({}); // Token verification success
      mockRouter.query = { token: "valid-reset-token" };
    });

    it("should submit password reset and auto-login on success", async () => {
      const user = userEvent.setup();

      // Mock both PUT (for password reset) and POST (for auto-login)
      (api.put as jest.Mock).mockResolvedValueOnce({});
      (api.post as jest.Mock).mockResolvedValueOnce({
        user: mockAuthUser,
        expiresAt: new Date(Date.now() + 3600000).toISOString(),
      });

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/New Password/i)).toBeInTheDocument();
      });

      const passwordInput = screen.getByLabelText(/New Password/i);
      const confirmInput = screen.getByLabelText(/Confirm Password/i);

      await user.type(passwordInput, "NewSecurePass123");
      await user.type(confirmInput, "NewSecurePass123");

      const submitButton = screen.getByRole("button", { name: /Reset Password/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(api.put).toHaveBeenCalledWith("/api/v1/auth/password-reset/confirm", {
          token: "valid-reset-token",
          newPassword: "NewSecurePass123",
        });
      });

      await waitFor(
        () => {
          expect(screen.getByText("Password Reset Successful")).toBeInTheDocument();
        },
        { timeout: 2000 }
      );

      // Wait for redirect (2 seconds timeout)
      await waitFor(
        () => {
          expect(mockRouter.push).toHaveBeenCalledWith("/chat");
        },
        { timeout: 3000 }
      );
    });

    it("should redirect to login if auto-login fails", async () => {
      const user = userEvent.setup();
      (api.put as jest.Mock).mockResolvedValueOnce({});
      (api.post as jest.Mock).mockRejectedValueOnce(new Error("Login failed"));

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/New Password/i)).toBeInTheDocument();
      });

      const passwordInput = screen.getByLabelText(/New Password/i);
      const confirmInput = screen.getByLabelText(/Confirm Password/i);

      await user.type(passwordInput, "NewSecurePass123");
      await user.type(confirmInput, "NewSecurePass123");

      const submitButton = screen.getByRole("button", { name: /Reset Password/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText(/Password Reset Successful/i)).toBeInTheDocument();
      });

      // Wait for redirect to login page
      await waitFor(
        () => {
          expect(mockRouter.push).toHaveBeenCalledWith("/");
        },
        { timeout: 3000 }
      );
    });

    it("should display error message on reset failure", async () => {
      const user = userEvent.setup();
      (api.put as jest.Mock).mockRejectedValueOnce({
        message: "Token expired during reset",
      });

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/New Password/i)).toBeInTheDocument();
      });

      const passwordInput = screen.getByLabelText(/New Password/i);
      const confirmInput = screen.getByLabelText(/Confirm Password/i);

      await user.type(passwordInput, "NewSecurePass123");
      await user.type(confirmInput, "NewSecurePass123");

      const submitButton = screen.getByRole("button", { name: /Reset Password/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByRole("alert")).toHaveTextContent(/Token expired during reset/i);
      });
    });

    it("should prevent submission with invalid password", async () => {
      const user = userEvent.setup();

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/New Password/i)).toBeInTheDocument();
      });

      const passwordInput = screen.getByLabelText(/New Password/i);
      const confirmInput = screen.getByLabelText(/Confirm Password/i);
      const submitButton = screen.getByRole("button", { name: /Reset Password/i });

      // Invalid password (missing requirements)
      await user.type(passwordInput, "weak");
      await user.type(confirmInput, "weak");

      expect(submitButton).toBeDisabled();
      expect(api.put).not.toHaveBeenCalled();
    });
  });

  describe("Accessibility", () => {
    beforeEach(() => {
      (api.get as jest.Mock).mockResolvedValueOnce(null); // Not authenticated
    });

    it("should have proper heading hierarchy", async () => {
      (api.get as jest.Mock).mockResolvedValueOnce({}); // Token verification (for reset mode)
      mockRouter.query = { token: "valid-token" };

      render(<ResetPasswordPage />);

      await waitFor(() => {
        const heading = screen.getByRole("heading", { name: /Set New Password/i });
        expect(heading).toBeInTheDocument();
        expect(heading.tagName).toBe("H1");
      });
    });

    it("should have accessible form labels", async () => {
      mockRouter.query = {}; // Request mode

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Email Address/i)).toBeInTheDocument();
      });
    });

    it("should announce errors with ARIA live regions", async () => {
      const user = userEvent.setup();
      (api.post as jest.Mock).mockRejectedValueOnce({
        message: "Network error",
      });
      mockRouter.query = {}; // Request mode

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Email Address/i)).toBeInTheDocument();
      });

      const emailInput = screen.getByLabelText(/Email Address/i);
      await user.type(emailInput, "user@example.com");

      const submitButton = screen.getByRole("button", { name: /Send Reset Instructions/i });
      await user.click(submitButton);

      await waitFor(() => {
        const alert = screen.getByRole("alert");
        expect(alert).toHaveAttribute("aria-live", "polite");
        expect(alert).toHaveTextContent(/Network error/i);
      });
    });

    it("should have proper navigation links", async () => {
      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByRole("link", { name: /Back to Home/i })).toHaveAttribute(
          "href",
          "/"
        );
        expect(screen.getByRole("link", { name: /← Back to Login/i })).toHaveAttribute(
          "href",
          "/"
        );
      });
    });
  });

  describe("Edge Cases", () => {
    it("should handle network timeout gracefully", async () => {
      (api.get as jest.Mock).mockResolvedValueOnce(null); // Not authenticated
      (api.post as jest.Mock).mockRejectedValueOnce({
        message: "Request timeout",
      });
      mockRouter.query = {}; // Request mode

      const user = userEvent.setup();
      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Email Address/i)).toBeInTheDocument();
      });

      const emailInput = screen.getByLabelText(/Email Address/i);
      await user.type(emailInput, "user@example.com");

      const submitButton = screen.getByRole("button", { name: /Send Reset Instructions/i });
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByRole("alert")).toHaveTextContent(/Request timeout/i);
      });
    });

    it("should handle malformed token in URL", async () => {
      (api.get as jest.Mock).mockResolvedValueOnce(null); // Not authenticated
      (api.get as jest.Mock).mockRejectedValueOnce({
        message: "Malformed token",
      });

      mockRouter.query = { token: "invalid-malformed-token" };

      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByText(/Invalid or Expired Link/i)).toBeInTheDocument();
        expect(screen.getByText(/Malformed token/i)).toBeInTheDocument();
      });
    });

    it("should disable submit button during loading", async () => {
      (api.get as jest.Mock).mockResolvedValueOnce(null); // Not authenticated
      (api.post as jest.Mock).mockImplementation(
        () => new Promise((resolve) => setTimeout(resolve, 1000))
      );
      mockRouter.query = {}; // Request mode

      const user = userEvent.setup();
      render(<ResetPasswordPage />);

      await waitFor(() => {
        expect(screen.getByLabelText(/Email Address/i)).toBeInTheDocument();
      });

      const emailInput = screen.getByLabelText(/Email Address/i);
      await user.type(emailInput, "user@example.com");

      const submitButton = screen.getByRole("button", { name: /Send Reset Instructions/i });
      await user.click(submitButton);

      // Button should be disabled and show loading text
      await waitFor(() => {
        expect(screen.getByRole("button", { name: /Sending.../i })).toBeDisabled();
      });
    });
  });
});
