/**
 * AuthModal Component
 *
 * Unified authentication modal supporting:
 * - Login and registration tabs
 * - OAuth authentication buttons
 * - Demo credentials display
 * - Session expired warnings
 * - Accessible modal with Shadcn UI components
 *
 * This component consolidates authentication UI previously
 * scattered across index.tsx and login.tsx
 */

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { AccessibleModal } from '@/components/accessible';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { LoginForm, LoginFormData } from './LoginForm';
import { RegisterForm, RegisterFormData } from './RegisterForm';
import { DemoCredentialsHint } from './DemoCredentialsHint';
import OAuthButtons from './OAuthButtons';
import { useAuth } from '@/hooks/useAuth';

// ============================================================================
// Component Props
// ============================================================================

export interface AuthModalProps {
  isOpen: boolean;
  onClose: () => void;
  defaultMode?: 'login' | 'register';
  onSuccess?: (user: any) => void;
  showDemoCredentials?: boolean;
  sessionExpiredMessage?: boolean;
}

// ============================================================================
// Component
// ============================================================================

export function AuthModal({
  isOpen,
  onClose,
  defaultMode = 'login',
  onSuccess,
  showDemoCredentials = true,
  sessionExpiredMessage = false,
}: AuthModalProps) {
  const router = useRouter();
  const { login, register, error, clearError, loading } = useAuth();
  const [activeTab, setActiveTab] = useState<'login' | 'register'>(defaultMode);

  // Reset tab when modal opens/closes or default mode changes
  useEffect(() => {
    if (isOpen) {
      setActiveTab(defaultMode);
      clearError();
    }
  }, [isOpen, defaultMode, clearError]);

  // Handle login submission
  const handleLogin = async (data: LoginFormData) => {
    try {
      const user = await login(data);
      onSuccess?.(user);
      onClose();
      await router.push('/chat');
    } catch (err) {
      // Error is already set in useAuth hook
      console.error('Login failed:', err);
    }
  };

  // Handle registration submission
  const handleRegister = async (data: Omit<RegisterFormData, 'confirmPassword'>) => {
    try {
      const user = await register(data);
      onSuccess?.(user);
      onClose();
      await router.push('/chat');
    } catch (err) {
      // Error is already set in useAuth hook
      console.error('Registration failed:', err);
    }
  };

  // Handle demo credential click
  const handleDemoCredentialClick = (credential: any) => {
    setActiveTab('login');
    // Auto-fill handled by component state if needed
  };

  // Handle tab change
  const handleTabChange = (value: string) => {
    setActiveTab(value as 'login' | 'register');
    clearError();
  };

  return (
    <AccessibleModal
      isOpen={isOpen}
      onClose={onClose}
      title={activeTab === 'login' ? 'Sign In to MeepleAI' : 'Create Your Account'}
      description="Access AI-powered board game rules assistance"
      size="md"
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
            <TabsTrigger value="login">Login</TabsTrigger>
            <TabsTrigger value="register">Register</TabsTrigger>
          </TabsList>

          {/* Login Tab */}
          <TabsContent value="login" className="space-y-4 mt-4">
            <LoginForm
              onSubmit={handleLogin}
              loading={loading}
              error={error}
              onErrorDismiss={clearError}
            />
          </TabsContent>

          {/* Register Tab */}
          <TabsContent value="register" className="space-y-4 mt-4">
            <RegisterForm
              onSubmit={handleRegister}
              loading={loading}
              error={error}
              onErrorDismiss={clearError}
              showRoleSelector={false}
            />
          </TabsContent>
        </Tabs>

        {/* OAuth Buttons (includes "Or continue with" separator) */}
        <OAuthButtons />

        {/* Demo Credentials */}
        {showDemoCredentials && (
          <>
            <div className="relative">
              <div className="absolute inset-0 flex items-center">
                <span className="w-full border-t border-slate-200 dark:border-slate-700" />
              </div>
              <div className="relative flex justify-center text-xs uppercase">
                <span className="bg-white dark:bg-slate-900 px-2 text-slate-500 dark:text-slate-400">
                  For Testing
                </span>
              </div>
            </div>

            <DemoCredentialsHint
              onCredentialClick={handleDemoCredentialClick}
              variant="default"
            />
          </>
        )}
      </div>
    </AccessibleModal>
  );
}
