/**
 * Login Page (AUTH-05)
 *
 * Standalone login page with session expiration handling.
 * Uses the unified AuthModal component for consistent UX.
 */

import { useState } from 'react';
import { useRouter } from 'next/router';
import Head from 'next/head';
import { AuthModal } from '@/components/auth';

export default function LoginPage() {
  const router = useRouter();
  const { reason } = router.query;
  const [showAuthModal, setShowAuthModal] = useState(true);

  const isSessionExpired = reason === 'session_expired';

  const handleClose = () => {
    setShowAuthModal(false);
    router.push('/');
  };

  return (
    <>
      <Head>
        <title>Login - MeepleAI</title>
      </Head>

      <div className="min-h-dvh flex items-center justify-center bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-900 dark:to-slate-800 px-4">
        {/* Unified Auth Modal */}
        <AuthModal
          isOpen={showAuthModal}
          onClose={handleClose}
          defaultMode="login"
          sessionExpiredMessage={isSessionExpired}
          showDemoCredentials={true}
        />
      </div>
    </>
  );
}

