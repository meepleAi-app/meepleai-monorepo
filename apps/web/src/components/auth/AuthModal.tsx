/**
 * AuthModal Component
 *
 * Unified authentication modal supporting:
 * - Login and registration tabs
 * - OAuth authentication buttons
 * - Session expired warnings
 * - Accessible modal with Shadcn UI components
 *
 * This component consolidates authentication UI previously
 * scattered across index.tsx and login.tsx
 */

import { useState, useEffect } from 'react';

import { useRouter } from 'next/navigation';

import { AccessibleModal } from '@/components/accessible';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { useAuth, type AuthUser } from '@/hooks/useAuth';

import { LoginForm, LoginFormData } from './LoginForm';
import OAuthButtons from './OAuthButtons';
import { RegisterForm, RegisterFormData } from './RegisterForm';

// ============================================================================
// Component Props
// ============================================================================

export interface AuthModalProps {
  isOpen: boolean;
  onClose: () => void;
  defaultMode?: 'login' | 'register';
  onSuccess?: (user: AuthUser) => void;
  sessionExpiredMessage?: boolean;
  /** URL to redirect to after successful authentication. Defaults to '/dashboard' */
  redirectTo?: string;
}

// ============================================================================
// Component
// ============================================================================

export function AuthModal({
  isOpen,
  onClose,
  defaultMode = 'login',
  onSuccess,
  sessionExpiredMessage = false,
  redirectTo = '/dashboard',
}: AuthModalProps) {
  const router = useRouter();
  const { login, register, error, clearError } = useAuth();
  const [activeTab, setActiveTab] = useState<'login' | 'register'>(defaultMode);
  const [isAuthenticating, setIsAuthenticating] = useState(false);

  // Reset tab when modal opens/closes or default mode changes
  useEffect(() => {
    if (isOpen) {
      setActiveTab(defaultMode);
      clearError();
      setIsAuthenticating(false); // Reset authenticating state on modal open
    }
  }, [isOpen, defaultMode, clearError]);

  // Handle login submission
  const handleLogin = async (data: LoginFormData) => {
    setIsAuthenticating(true);
    try {
      const user = await login(data);
      onSuccess?.(user);
      onClose();
      // Redirect admins to admin dashboard, others to specified redirect or default dashboard
      const targetUrl = user.role?.toLowerCase() === 'admin' ? '/admin' : redirectTo;
      await router.push(targetUrl);
    } catch (err) {
      // Error is already set in useAuth hook
      console.error('Login failed:', err);
    } finally {
      setIsAuthenticating(false);
    }
  };

  // Handle registration submission
  const handleRegister = async (data: Omit<RegisterFormData, 'confirmPassword'>) => {
    setIsAuthenticating(true);
    try {
      const user = await register(data);
      onSuccess?.(user);
      onClose();
      // Redirect to email verification pending page with email as parameter
      // The user needs to verify their email before accessing the dashboard
      await router.push(`/verification-pending?email=${encodeURIComponent(data.email)}`);
    } catch (err) {
      // Error is already set in useAuth hook
      console.error('Registration failed:', err);
    } finally {
      setIsAuthenticating(false);
    }
  };

  // Handle tab change
  const handleTabChange = (value: string) => {
    setActiveTab(value as 'login' | 'register');
    clearError();
    setIsAuthenticating(false); // Reset authenticating state on tab switch
  };

  return (
    <AccessibleModal
      isOpen={isOpen}
      onClose={onClose}
      title={activeTab === 'login' ? 'Sign In to MeepleAI' : 'Create Your Account'}
      description="Access AI-powered board game rules assistance"
      size="md"
      data-testid="auth-modal"
    >
      <div className="space-y-6">
        {/* Session Expired Warning */}
        {sessionExpiredMessage && (
          <div
            className="rounded-md bg-yellow-50 dark:bg-yellow-900/20 border border-yellow-200 dark:border-yellow-800 p-3"
            role="alert"
          >
            <div className="flex">
              <span className="text-yellow-600 dark:text-yellow-400 mr-2" aria-hidden="true">
                ⚠️
              </span>
              <div>
                <p className="text-sm font-medium text-yellow-800 dark:text-yellow-200">
                  Session Expired
                </p>
                <p className="text-xs text-yellow-700 dark:text-yellow-300 mt-1">
                  Your session has expired. Please sign in again to continue.
                </p>
              </div>
            </div>
          </div>
        )}

        {/* Auth Tabs */}
        <Tabs value={activeTab} onValueChange={handleTabChange} className="w-full">
          <TabsList className="grid w-full grid-cols-2">
            <TabsTrigger value="login" data-testid="auth-tab-login">
              Login
            </TabsTrigger>
            <TabsTrigger value="register" data-testid="auth-tab-register">
              Register
            </TabsTrigger>
          </TabsList>

          {/* Login Tab */}
          <TabsContent value="login" className="space-y-4 mt-4">
            <LoginForm
              onSubmit={handleLogin}
              loading={isAuthenticating}
              error={error}
              onErrorDismiss={clearError}
            />
          </TabsContent>

          {/* Register Tab */}
          <TabsContent value="register" className="space-y-4 mt-4">
            <RegisterForm
              onSubmit={handleRegister}
              loading={isAuthenticating}
              error={error}
              onErrorDismiss={clearError}
              showRoleSelector={false}
            />
          </TabsContent>
        </Tabs>

        {/* OAuth Buttons (includes "Or continue with" separator) */}
        <OAuthButtons />
      </div>
    </AccessibleModal>
  );
}
