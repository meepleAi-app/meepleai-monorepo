'use client';

/**
 * App Router Providers Component
 *
 * This file contains all client-side providers that need to wrap the application.
 * Extracted from _app.tsx for App Router compatibility.
 *
 * Issue #1077: FE-IMP-001 - Bootstrap App Router + Shared Providers
 */

import { ThemeProvider } from 'next-themes';
import { ErrorBoundary, RouteErrorBoundary } from '@/components/errors';
import { Toaster } from '@/components/ui/sonner';
import { useSessionCheck } from '@/hooks/useSessionCheck';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';
import { SessionWarningModal } from '@/components/modals';
import { AccessibleSkipLink } from '@/components/accessible';
import { AuthProvider } from '@/components/auth/AuthProvider';
import { QueryProvider } from '@/app/QueryProvider';
import { IntlProvider } from '@/components/providers/IntlProvider';
import { api } from '@/lib/api';
import { KeyboardShortcutsHelp } from '@/components/layout';
import { useGlobalKeyboardShortcuts } from '@/hooks/useKeyboardShortcuts';
import { hydrateApiKey } from '@/lib/api/core/apiKeyStore';
import { useState, useEffect, ReactNode } from 'react';

interface AppProvidersProps {
  children: ReactNode;
}

function AppContent({ children }: { children: ReactNode }) {
  // AUTH-05: Session timeout monitoring
  const { remainingMinutes, isNearExpiry } = useSessionCheck();

  // Issue #1100: Keyboard shortcuts system
  const [showShortcutsHelp, setShowShortcutsHelp] = useState(false);
  const [_showCommandPalette, setShowCommandPalette] = useState(false);

  // Enable axe-core accessibility checks in development (UI-05, Issue #841)
  // Moved to useEffect to avoid page crashes during Playwright tests
  useEffect(() => {
    if (process.env.NODE_ENV === 'development') {
      import('@axe-core/react')
        .then(axe => {
          const React = require('react');
          const ReactDOM = require('react-dom');
          axe.default(React, ReactDOM, 1000);
        })
        .catch(() => {
          // Silently fail if axe-core fails to load
        });
    }
  }, []);

  // Hydrate API key from sessionStorage on app startup
  // This restores the encrypted API key into memory after page reload
  useEffect(() => {
    hydrateApiKey().catch(error => {
      // Log error but don't block app startup
      logger.error(
        'Failed to hydrate API key on startup',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('AppProviders', 'hydrateApiKey', { operation: 'hydrate_api_key' })
      );
    });
  }, []);

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
      logger.error(
        'Failed to extend session',
        error instanceof Error ? error : new Error(String(error)),
        createErrorContext('AppProviders', 'handleStayLoggedIn', { operation: 'extend_session' })
      );
      // Redirect to login on error
      window.location.href = '/login?reason=session_expired';
    }
  };

  const handleLogOut = () => {
    window.location.href = '/login?reason=session_expired';
  };

  return (
    <>
      {/* Skip to main content link (UI-05 WCAG 2.1 AA requirement) */}
      <AccessibleSkipLink href="#main-content" />
      {children}
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

/**
 * AppProviders - Client Component Wrapper for App Router
 *
 * Provides all necessary context providers for the application:
 * - IntlProvider (internationalization - Issue #990)
 * - ThemeProvider (dark/light mode)
 * - QueryProvider (TanStack Query data layer - Issue #1079)
 * - AuthProvider (authentication state)
 * - ErrorBoundary (error handling)
 * - Session management
 * - Keyboard shortcuts
 * - Accessibility features
 */
export function AppProviders({ children }: AppProvidersProps) {
  return (
    <IntlProvider>
      <ThemeProvider attribute="class" defaultTheme="system" enableSystem disableTransitionOnChange>
        <QueryProvider>
          <AuthProvider>
            <ErrorBoundary componentName="App" showDetails={process.env.NODE_ENV === 'development'}>
              <RouteErrorBoundary routeName="AppContent">
                <AppContent>{children}</AppContent>
              </RouteErrorBoundary>
            </ErrorBoundary>
          </AuthProvider>
        </QueryProvider>
      </ThemeProvider>
    </IntlProvider>
  );
}
