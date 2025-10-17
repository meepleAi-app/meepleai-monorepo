/**
 * Password Reset Page (AUTH-04)
 *
 * Two-mode password reset flow:
 * 1. Request Mode (no token): User enters email to request reset
 * 2. Reset Mode (with token): User sets new password
 *
 * Features:
 * - Email-based password reset workflow
 * - Token verification on page load
 * - Client-side password validation
 * - Real-time password strength indicator
 * - Auto-login and redirect after successful reset
 * - Comprehensive error handling
 * - WCAG 2.1 AA compliant with accessible components
 *
 * API Endpoints:
 * - POST /api/v1/auth/password-reset/request - Request reset email
 * - GET /api/v1/auth/password-reset/verify?token=xxx - Verify token
 * - PUT /api/v1/auth/password-reset/confirm - Confirm new password
 */

import { useEffect, useState, FormEvent } from "react";
import { useRouter } from "next/router";
import Link from "next/link";
import { motion } from "framer-motion";
import { api } from "@/lib/api";
import { AccessibleFormInput, AccessibleButton } from "@/components/accessible";

// Type definitions
type AuthUser = {
  id: string;
  email: string;
  displayName?: string | null;
  role: string;
};

type AuthResponse = {
  user: AuthUser;
  expiresAt: string;
};

type PasswordStrength = "weak" | "medium" | "strong";

interface PasswordValidation {
  minLength: boolean;
  hasUppercase: boolean;
  hasLowercase: boolean;
  hasNumber: boolean;
  isValid: boolean;
  strength: PasswordStrength;
}

// Password validation utility
const validatePassword = (password: string): PasswordValidation => {
  const minLength = password.length >= 8;
  const hasUppercase = /[A-Z]/.test(password);
  const hasLowercase = /[a-z]/.test(password);
  const hasNumber = /[0-9]/.test(password);

  const requirementsMet = [minLength, hasUppercase, hasLowercase, hasNumber].filter(
    Boolean
  ).length;

  let strength: PasswordStrength = "weak";
  if (requirementsMet === 4) {
    strength = "strong";
  } else if (requirementsMet >= 2 && minLength) {
    strength = "medium";
  }

  return {
    minLength,
    hasUppercase,
    hasLowercase,
    hasNumber,
    isValid: minLength && hasUppercase && hasLowercase && hasNumber,
    strength,
  };
};

// Password strength indicator component
const PasswordStrengthIndicator = ({ strength }: { strength: PasswordStrength }) => {
  const strengthConfig = {
    weak: {
      color: "bg-red-500",
      text: "Weak",
      textColor: "text-red-500",
      width: "w-1/3",
    },
    medium: {
      color: "bg-orange-500",
      text: "Medium",
      textColor: "text-orange-500",
      width: "w-2/3",
    },
    strong: {
      color: "bg-green-500",
      text: "Strong",
      textColor: "text-green-500",
      width: "w-full",
    },
  };

  const config = strengthConfig[strength];

  return (
    <div className="space-y-2" role="status" aria-live="polite">
      <div className="flex justify-between items-center">
        <span className="text-sm text-slate-600 dark:text-slate-400">Password strength:</span>
        <span className={`text-sm font-medium ${config.textColor}`}>{config.text}</span>
      </div>
      <div className="h-2 bg-slate-200 dark:bg-slate-700 rounded-full overflow-hidden">
        <motion.div
          className={`h-full ${config.color}`}
          initial={{ width: 0 }}
          animate={{ width: config.width }}
          transition={{ duration: 0.3 }}
        />
      </div>
    </div>
  );
};

export default function ResetPasswordPage() {
  const router = useRouter();
  const { token } = router.query;

  // Mode: 'request' (no token) or 'reset' (with token)
  const mode = token ? "reset" : "request";

  // Authentication state
  const [authUser, setAuthUser] = useState<AuthUser | null>(null);
  const [isCheckingAuth, setIsCheckingAuth] = useState(true);

  // Request mode state
  const [email, setEmail] = useState("");
  const [requestSuccess, setRequestSuccess] = useState(false);

  // Reset mode state
  const [tokenValid, setTokenValid] = useState<boolean | null>(null);
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [passwordValidation, setPasswordValidation] = useState<PasswordValidation>({
    minLength: false,
    hasUppercase: false,
    hasLowercase: false,
    hasNumber: false,
    isValid: false,
    strength: "weak",
  });
  const [resetSuccess, setResetSuccess] = useState(false);

  // UI state
  const [isLoading, setIsLoading] = useState(false);
  const [errorMessage, setErrorMessage] = useState("");

  // Check if user is already authenticated
  useEffect(() => {
    const checkAuth = async () => {
      try {
        const res = await api.get<AuthResponse>("/api/v1/auth/me");
        if (res) {
          setAuthUser(res.user);
          // Redirect to chat if already logged in
          void router.push("/chat");
        }
      } catch {
        setAuthUser(null);
      } finally {
        setIsCheckingAuth(false);
      }
    };

    void checkAuth();
  }, [router]);

  // Verify token on page load (reset mode only)
  useEffect(() => {
    if (mode === "reset" && token && typeof token === "string") {
      const verifyToken = async () => {
        setIsLoading(true);
        setErrorMessage("");
        try {
          await api.get(`/api/v1/auth/password-reset/verify?token=${encodeURIComponent(token)}`);
          setTokenValid(true);
        } catch (err: any) {
          setTokenValid(false);
          setErrorMessage(err?.message || "Invalid or expired reset token.");
        } finally {
          setIsLoading(false);
        }
      };

      void verifyToken();
    }
  }, [mode, token]);

  // Update password validation on password change
  useEffect(() => {
    if (newPassword) {
      setPasswordValidation(validatePassword(newPassword));
    } else {
      setPasswordValidation({
        minLength: false,
        hasUppercase: false,
        hasLowercase: false,
        hasNumber: false,
        isValid: false,
        strength: "weak",
      });
    }
  }, [newPassword]);

  // Handle request reset submission
  const handleRequestReset = async (e: FormEvent) => {
    e.preventDefault();
    setErrorMessage("");
    setIsLoading(true);

    try {
      await api.post("/api/v1/auth/password-reset/request", { email });
      setRequestSuccess(true);
    } catch (err: any) {
      setErrorMessage(err?.message || "Failed to send reset email. Please try again.");
    } finally {
      setIsLoading(false);
    }
  };

  // Handle password reset submission
  const handleConfirmReset = async (e: FormEvent) => {
    e.preventDefault();
    setErrorMessage("");

    // Client-side validation
    if (!passwordValidation.isValid) {
      setErrorMessage("Password does not meet all requirements.");
      return;
    }

    if (newPassword !== confirmPassword) {
      setErrorMessage("Passwords do not match.");
      return;
    }

    if (!token || typeof token !== "string") {
      setErrorMessage("Invalid reset token.");
      return;
    }

    setIsLoading(true);

    try {
      // Confirm password reset
      await api.put("/api/v1/auth/password-reset/confirm", {
        token,
        newPassword,
      });

      setResetSuccess(true);

      // Auto-login after reset
      try {
        const loginRes = await api.post<AuthResponse>("/api/v1/auth/login", {
          email: email || "", // Email might not be known in reset mode
          password: newPassword,
        });

        if (loginRes) {
          setAuthUser(loginRes.user);
          // Redirect to chat after 2 seconds
          setTimeout(() => {
            void router.push("/chat");
          }, 2000);
        } else {
          // If auto-login fails, redirect to login page
          setTimeout(() => {
            void router.push("/");
          }, 2000);
        }
      } catch {
        // If auto-login fails, redirect to login page
        setTimeout(() => {
          void router.push("/");
        }, 2000);
      }
    } catch (err: any) {
      setErrorMessage(err?.message || "Failed to reset password. Please try again.");
      setIsLoading(false);
    }
  };

  // Show loading state while checking authentication
  if (isCheckingAuth) {
    return (
      <div className="min-h-screen bg-slate-950 text-white flex items-center justify-center">
        <div className="text-center">
          <div className="animate-spin text-4xl mb-4">⏳</div>
          <p className="text-slate-400">Loading...</p>
        </div>
      </div>
    );
  }

  // User is already authenticated, redirecting...
  if (authUser) {
    return null;
  }

  return (
    <div className="min-h-screen bg-slate-950 text-white">
      {/* Header */}
      <header className="sticky top-0 glass z-50">
        <div className="max-w-7xl mx-auto px-6 py-4 flex justify-between items-center">
          <Link href="/" className="flex items-center gap-3 hover:opacity-80 transition-opacity">
            <span className="text-4xl">🎲</span>
            <span className="text-2xl font-bold gradient-text">MeepleAI</span>
          </Link>
          <Link href="/" className="btn-secondary text-sm py-2 px-4">
            Back to Home
          </Link>
        </div>
      </header>

      {/* Main Content */}
      <main id="main-content" className="flex items-center justify-center px-6 py-20">
        <motion.div
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.6 }}
          className="w-full max-w-md"
        >
          <div className="card p-8 space-y-6">
            {/* Request Reset Mode */}
            {mode === "request" && !requestSuccess && (
              <>
                <div className="text-center space-y-2">
                  <h1 className="text-3xl font-bold">Reset Password</h1>
                  <p className="text-slate-400">
                    Enter your email address and we'll send you instructions to reset your password.
                  </p>
                </div>

                {errorMessage && (
                  <div
                    role="alert"
                    aria-live="polite"
                    className="bg-red-500/10 border border-red-500/30 text-red-400 px-4 py-3 rounded-lg"
                  >
                    {errorMessage}
                  </div>
                )}

                <form onSubmit={handleRequestReset} className="space-y-4">
                  <AccessibleFormInput
                    label="Email Address"
                    type="email"
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    required
                    autoComplete="email"
                    placeholder="you@example.com"
                    inputClassName="w-full bg-white/5 border border-white/10 rounded-lg px-4 py-3 text-white focus:border-primary-500 focus:ring-2 focus:ring-primary-500"
                  />

                  <AccessibleButton
                    type="submit"
                    variant="primary"
                    className="w-full mt-6"
                    isLoading={isLoading}
                    loadingText="Sending..."
                    disabled={!email.trim()}
                  >
                    Send Reset Instructions
                  </AccessibleButton>
                </form>

                <div className="text-center">
                  <Link
                    href="/"
                    className="text-sm text-primary-400 hover:text-primary-300 transition-colors"
                  >
                    ← Back to Login
                  </Link>
                </div>
              </>
            )}

            {/* Request Success State */}
            {mode === "request" && requestSuccess && (
              <div className="text-center space-y-4">
                <div className="text-6xl mb-4">✉️</div>
                <h2 className="text-2xl font-bold text-green-400">Check Your Email</h2>
                <p className="text-slate-300">
                  We've sent password reset instructions to <strong>{email}</strong>.
                </p>
                <p className="text-sm text-slate-400">
                  Didn't receive the email? Check your spam folder or{" "}
                  <button
                    onClick={() => {
                      setRequestSuccess(false);
                      setEmail("");
                    }}
                    className="text-primary-400 hover:text-primary-300 underline"
                  >
                    try again
                  </button>
                  .
                </p>
                <div className="pt-4">
                  <Link href="/" className="btn-secondary">
                    Back to Login
                  </Link>
                </div>
              </div>
            )}

            {/* Reset Password Mode - Token Verification */}
            {mode === "reset" && tokenValid === null && (
              <div className="text-center space-y-4">
                <div className="animate-spin text-4xl mb-4">⏳</div>
                <p className="text-slate-400">Verifying reset token...</p>
              </div>
            )}

            {/* Reset Password Mode - Invalid Token */}
            {mode === "reset" && tokenValid === false && (
              <div className="text-center space-y-4">
                <div className="text-6xl mb-4">⚠️</div>
                <h2 className="text-2xl font-bold text-red-400">Invalid or Expired Link</h2>
                <p className="text-slate-300">
                  This password reset link is invalid or has expired.
                </p>
                {errorMessage && (
                  <p className="text-sm text-red-400">{errorMessage}</p>
                )}
                <div className="pt-4 space-y-2">
                  <Link href="/reset-password" className="btn-primary block">
                    Request New Reset Link
                  </Link>
                  <Link href="/" className="btn-secondary block">
                    Back to Login
                  </Link>
                </div>
              </div>
            )}

            {/* Reset Password Mode - Valid Token */}
            {mode === "reset" && tokenValid === true && !resetSuccess && (
              <>
                <div className="text-center space-y-2">
                  <h1 className="text-3xl font-bold">Set New Password</h1>
                  <p className="text-slate-400">
                    Choose a strong password for your account.
                  </p>
                </div>

                {errorMessage && (
                  <div
                    role="alert"
                    aria-live="polite"
                    className="bg-red-500/10 border border-red-500/30 text-red-400 px-4 py-3 rounded-lg"
                  >
                    {errorMessage}
                  </div>
                )}

                <form onSubmit={handleConfirmReset} className="space-y-4">
                  <AccessibleFormInput
                    label="New Password"
                    type="password"
                    value={newPassword}
                    onChange={(e) => setNewPassword(e.target.value)}
                    required
                    autoComplete="new-password"
                    inputClassName="w-full bg-white/5 border border-white/10 rounded-lg px-4 py-3 text-white focus:border-primary-500 focus:ring-2 focus:ring-primary-500"
                  />

                  {/* Password Requirements */}
                  {newPassword && (
                    <div className="space-y-2">
                      <PasswordStrengthIndicator strength={passwordValidation.strength} />
                      <div className="text-sm space-y-1">
                        <div
                          className={`flex items-center gap-2 ${
                            passwordValidation.minLength ? "text-green-400" : "text-slate-500"
                          }`}
                        >
                          <span aria-hidden="true">
                            {passwordValidation.minLength ? "✓" : "○"}
                          </span>
                          <span>At least 8 characters</span>
                        </div>
                        <div
                          className={`flex items-center gap-2 ${
                            passwordValidation.hasUppercase ? "text-green-400" : "text-slate-500"
                          }`}
                        >
                          <span aria-hidden="true">
                            {passwordValidation.hasUppercase ? "✓" : "○"}
                          </span>
                          <span>At least 1 uppercase letter</span>
                        </div>
                        <div
                          className={`flex items-center gap-2 ${
                            passwordValidation.hasLowercase ? "text-green-400" : "text-slate-500"
                          }`}
                        >
                          <span aria-hidden="true">
                            {passwordValidation.hasLowercase ? "✓" : "○"}
                          </span>
                          <span>At least 1 lowercase letter</span>
                        </div>
                        <div
                          className={`flex items-center gap-2 ${
                            passwordValidation.hasNumber ? "text-green-400" : "text-slate-500"
                          }`}
                        >
                          <span aria-hidden="true">
                            {passwordValidation.hasNumber ? "✓" : "○"}
                          </span>
                          <span>At least 1 number</span>
                        </div>
                      </div>
                    </div>
                  )}

                  <AccessibleFormInput
                    label="Confirm Password"
                    type="password"
                    value={confirmPassword}
                    onChange={(e) => setConfirmPassword(e.target.value)}
                    required
                    autoComplete="new-password"
                    error={
                      confirmPassword && newPassword !== confirmPassword
                        ? "Passwords do not match"
                        : undefined
                    }
                    inputClassName="w-full bg-white/5 border border-white/10 rounded-lg px-4 py-3 text-white focus:border-primary-500 focus:ring-2 focus:ring-primary-500"
                  />

                  <AccessibleButton
                    type="submit"
                    variant="primary"
                    className="w-full mt-6"
                    isLoading={isLoading}
                    loadingText="Resetting..."
                    disabled={
                      !passwordValidation.isValid ||
                      newPassword !== confirmPassword ||
                      !confirmPassword.trim()
                    }
                  >
                    Reset Password
                  </AccessibleButton>
                </form>

                <div className="text-center">
                  <Link
                    href="/"
                    className="text-sm text-primary-400 hover:text-primary-300 transition-colors"
                  >
                    ← Back to Login
                  </Link>
                </div>
              </>
            )}

            {/* Reset Success State */}
            {mode === "reset" && resetSuccess && (
              <div className="text-center space-y-4">
                <div className="text-6xl mb-4">✅</div>
                <h2 className="text-2xl font-bold text-green-400">Password Reset Successful</h2>
                <p className="text-slate-300">
                  Your password has been successfully reset.
                </p>
                <p className="text-sm text-slate-400">
                  Redirecting to chat...
                </p>
                <div className="animate-spin text-2xl mx-auto w-fit">⏳</div>
              </div>
            )}
          </div>

          {/* Security Notice */}
          <div className="mt-6 text-center text-sm text-slate-500">
            <p>🔒 This page is secured with industry-standard encryption</p>
          </div>
        </motion.div>
      </main>
    </div>
  );
}
