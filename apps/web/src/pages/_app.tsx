import type { AppProps } from 'next/app';
import Head from 'next/head';
import { ThemeProvider } from 'next-themes';
import { ErrorBoundary } from '../components/ErrorBoundary';
import { RouteErrorBoundary } from '../components/RouteErrorBoundary';
import { Toaster } from '@/components/ui/sonner';
import { useSessionCheck } from '../hooks/useSessionCheck';
import { SessionWarningModal } from '../components/SessionWarningModal';
import { AccessibleSkipLink } from '@/components/accessible';
import { AuthProvider } from '@/components/auth/AuthProvider';
import { api } from '@/lib/api';
import { KeyboardShortcutsHelp } from '@/components/KeyboardShortcutsHelp';
import { useGlobalKeyboardShortcuts } from '@/hooks/useKeyboardShortcuts';
import { useState } from 'react';
import '../styles/globals.css';
import '../styles/diff-viewer.css';
import 'prismjs/themes/prism-tomorrow.css';

// Enable axe-core accessibility checks in development (UI-05)
if (typeof window !== 'undefined' && process.env.NODE_ENV === 'development') {
  // @ts-ignore - dynamic import
  import('@axe-core/react').then((axe) => {
    const React = require('react');
    const ReactDOM = require('react-dom');
    axe.default(React, ReactDOM, 1000);
  }).catch(() => {
    // Silently fail if axe-core fails to load
  });
}

function AppContent({ Component, pageProps }: AppProps) {
  // AUTH-05: Session timeout monitoring
  const { remainingMinutes, isNearExpiry } = useSessionCheck();

  // Issue #1100: Keyboard shortcuts system
  const [showShortcutsHelp, setShowShortcutsHelp] = useState(false);
  const [showCommandPalette, setShowCommandPalette] = useState(false);

  // Global keyboard shortcuts
  useGlobalKeyboardShortcuts({
    onOpenCommandPalette: () => setShowCommandPalette(true),
    onOpenShortcutsHelp: () => setShowShortcutsHelp(true),
    onCloseModal: () => {
      setShowShortcutsHelp(false);
      setShowCommandPalette(false);
    },
  });

  const handleStayLoggedIn = async () => {
    try {
      await api.auth.extendSession();
      // Session extended - modal will close automatically when isNearExpiry becomes false
    } catch (error) {
      console.error('Failed to extend session:', error);
      // Redirect to login on error
      window.location.href = '/login?reason=session_expired';
    }
  };

  const handleLogOut = () => {
    window.location.href = '/login?reason=session_expired';
  };

  return (
    <>
      {/* Default document title (UI-05 WCAG 2.1 AA requirement) */}
      <Head>
        <title>MeepleAI - AI-Powered Board Game Rules Assistant</title>
      </Head>
      {/* Skip to main content link (UI-05 WCAG 2.1 AA requirement) */}
      <AccessibleSkipLink href="#main-content" />
      <Component {...pageProps} />
      <Toaster />

      {/* AUTH-05: Session expiry warning modal */}
      {isNearExpiry && remainingMinutes !== null && (
        <SessionWarningModal
          remainingMinutes={remainingMinutes}
          onStayLoggedIn={handleStayLoggedIn}
          onLogOut={handleLogOut}
        />
      )}

      {/* Issue #1100: Keyboard shortcuts help modal */}
      <KeyboardShortcutsHelp
        isOpen={showShortcutsHelp}
        onClose={() => setShowShortcutsHelp(false)}
      />
    </>
  );
}

export default function App(props: AppProps) {
  return (
    <ThemeProvider attribute="class" defaultTheme="system" enableSystem disableTransitionOnChange>
      <AuthProvider>
        <ErrorBoundary
          componentName="App"
          showDetails={process.env.NODE_ENV === 'development'}
        >
          <RouteErrorBoundary routeName="AppContent">
            <AppContent {...props} />
          </RouteErrorBoundary>
        </ErrorBoundary>
      </AuthProvider>
    </ThemeProvider>
  );
}
