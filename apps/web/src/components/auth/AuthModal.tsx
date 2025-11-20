/**
 * AuthModal Component
 *
 * Unified authentication modal supporting:
 * - Login and registration tabs using Server Actions pattern
 * - OAuth authentication buttons
 * - Demo credentials display
 * - Session expired warnings (Italian localization)
 * - Accessible modal with Shadcn UI components
 *
 * Issue #1078: FE-IMP-002 — Server Actions per Auth & Export
 *
 * This component consolidates authentication UI previously
 * scattered across index.tsx and login.tsx
 */

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { AccessibleModal } from '@/components/accessible';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { LoginForm } from './LoginForm';
import { RegisterForm } from './RegisterForm';
import { DemoCredentialsHint, type DemoCredential } from './DemoCredentialsHint';
import OAuthButtons from './OAuthButtons';
import { Spinner } from '@/components/loading/Spinner';
import { useQueryClient } from '@/hooks/queries';
import { useTranslation } from '@/hooks/useTranslation';
import { useAuth } from '@/hooks/useAuth';
import type { AuthUser } from '@/types';

// ============================================================================
// Component Props
// ============================================================================

export interface AuthModalProps {
  isOpen: boolean;
  onClose: () => void;
  defaultMode?: 'login' | 'register' | 'demo';
  onSuccess?: (user: unknown) => void;
  showDemoCredentials?: boolean;
  sessionExpiredMessage?: boolean;
  initialEmail?: string;
  initialPassword?: string;
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
  initialEmail = '',
  initialPassword = '',
}: AuthModalProps) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { t } = useTranslation();
  const { demoLogin } = useAuth();
  const [activeTab, setActiveTab] = useState<'login' | 'register' | 'demo'>(defaultMode);
  const [demoLoginLoading, setDemoLoginLoading] = useState(false);
  const [demoLoginError, setDemoLoginError] = useState<string>('');

  // Reset tab when modal opens/closes or default mode changes
  useEffect(() => {
    if (isOpen) {
      setActiveTab(defaultMode);
    }
  }, [isOpen, defaultMode]);

  // Handle successful authentication
  const handleAuthSuccess = async (user: AuthUser) => {
    // Issue #1079: Update AuthProvider cache immediately so useAuth() reflects the authenticated state
    queryClient.setQueryData(['user', 'current'], user);

    onSuccess?.(user);
    onClose();
    await router.push('/chat');
  };

  // Handle demo credential click - use passwordless demo login
  const handleDemoCredentialClick = async (credential: DemoCredential) => {
    setDemoLoginLoading(true);
    setDemoLoginError('');

    try {
      const user = await demoLogin({ email: credential.email });
      await handleAuthSuccess(user);
    } catch (error) {
      const errorMessage = error instanceof Error ? error.message : 'Demo login failed';
      setDemoLoginError(errorMessage);
    } finally {
      setDemoLoginLoading(false);
    }
  };

  // Handle tab change
  const handleTabChange = (value: string) => {
    setActiveTab(value as 'login' | 'register' | 'demo');
  };

  return (
    <AccessibleModal
      isOpen={isOpen}
      onClose={onClose}
      title={
        activeTab === 'login'
          ? t('auth.login.signInTo')
          : activeTab === 'register'
          ? t('auth.login.createAccount')
          : 'Demo Accounts'
      }
      description={activeTab === 'demo' ? 'Quick access for testing' : t('auth.login.signInDescription')}
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
                  {t('auth.login.sessionExpired')}
                </p>
                <p className="text-xs text-yellow-700 dark:text-yellow-300 mt-1">
                  {t('auth.login.sessionExpiredMessage')}
                </p>
              </div>
            </div>
          </div>
        )}

        {/* Auth Tabs */}
        <Tabs value={activeTab} onValueChange={handleTabChange} className="w-full">
          <TabsList className="grid w-full grid-cols-3">
            <TabsTrigger value="login">{t('navigation.login')}</TabsTrigger>
            <TabsTrigger value="register">{t('navigation.register')}</TabsTrigger>
            <TabsTrigger value="demo">Demo</TabsTrigger>
          </TabsList>

          {/* Login Tab */}
          <TabsContent value="login" className="space-y-4 mt-4">
            <LoginForm
              onSuccess={handleAuthSuccess}
              initialEmail={initialEmail}
            />
          </TabsContent>

          {/* Register Tab */}
          <TabsContent value="register" className="space-y-4 mt-4">
            <RegisterForm
              onSuccess={handleAuthSuccess}
            />
          </TabsContent>

          {/* Demo Tab */}
          <TabsContent value="demo" className="space-y-4 mt-4">
            {demoLoginError && (
              <div
                className="rounded-md bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 p-3"
                role="alert"
              >
                <p className="text-sm text-red-800 dark:text-red-200">{demoLoginError}</p>
              </div>
            )}

            {demoLoginLoading ? (
              <div className="flex flex-col items-center justify-center py-8 space-y-3">
                <Spinner size="lg" className="text-primary" />
                <p className="text-sm text-slate-600 dark:text-slate-400">Logging in...</p>
              </div>
            ) : (
              <DemoCredentialsHint
                onCredentialClick={handleDemoCredentialClick}
                variant="default"
              />
            )}
          </TabsContent>
        </Tabs>

        {/* OAuth Buttons (includes "Or continue with" separator) - Only show for login/register tabs */}
        {activeTab !== 'demo' && <OAuthButtons />}
      </div>
    </AccessibleModal>
  );
}
