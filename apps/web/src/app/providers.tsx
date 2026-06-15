'use client';

/**
 * App Router Providers Component
 *
 * This file contains all client-side providers that need to wrap the application.
 * Extracted from _app.tsx for App Router compatibility.
 *
 * Issue #1077: FE-IMP-001 - Bootstrap App Router + Shared Providers
 */

import { useState, useEffect, ReactNode } from 'react';

import { QueryProvider } from '@/app/QueryProvider';
import { AccessibleSkipLink } from '@/components/accessible';
import { AuthProvider } from '@/components/auth/AuthProvider';
import { ErrorBoundary, RouteErrorBoundary } from '@/components/errors';
import { KeyboardShortcutsHelp, LayoutProvider } from '@/components/layout';
import { CommandPalette } from '@/components/layout/CommandPalette';
import { CookieConsentBanner } from '@/components/legal';
import { AddGameWizardProvider } from '@/components/library/add-game-sheet/AddGameWizardProvider';
import { SessionWarningModal } from '@/components/modals';
import { IntlProvider } from '@/components/providers/IntlProvider';
import { ThemeProvider } from '@/components/providers/ThemeProvider';
import { PWAProvider } from '@/components/pwa';
import { Toaster } from '@/components/ui/feedback/sonner';
import { StatePreviewProvider } from '@/components/ui/state-preview';
import { useGlobalKeyboardShortcuts } from '@/hooks/useKeyboardShortcuts';
import { useSessionCheck } from '@/hooks/useSessionCheck';
import { api } from '@/lib/api';
import { hydrateApiKey } from '@/lib/api/core/apiKeyStore';
import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';

interface AppProvidersProps {
  children: ReactNode;
}

function AppContent({ children }: { children: ReactNode }) {
  // AUTH-05: Session timeout monitoring
  const { remainingMinutes, isNearExpiry } = useSessionCheck();

  // Issue #1100: Keyboard shortcuts system
  const [showShortcutsHelp, setShowShortcutsHelp] = useState(false);
  // Issue #2320: wire CommandPalette into the providers tree so the keyboard
  // shortcut (Cmd/Ctrl+K, registered by useGlobalKeyboardShortcuts below)
  // actually surfaces the palette. The SearchPill button in `AppTopBar`
  // calls into the same state via the `meeple:command-palette:open` event hub
  // (see useEffect below). Keeps providers.tsx + AppTopBar decoupled —
  // no React context plumbing required.
  const [showCommandPalette, setShowCommandPalette] = useState(false);

  // Issue #2320: listen for `meeple:command-palette:open` from the
  // SearchPill trigger button in `AppTopBar`. Decouples the trigger from
  // the modal state without introducing a new context layer.
  useEffect(() => {
    function handleOpen() {
      setShowCommandPalette(true);
    }
    window.addEventListener('meeple:command-palette:open', handleOpen);
    return () => window.removeEventListener('meeple:command-palette:open', handleOpen);
  }, []);

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
    <PWAProvider showInstallPrompt={false}>
      {/* Skip to main content link (UI-05 WCAG 2.1 AA requirement) */}
      <AccessibleSkipLink href="#main-content" />
      {children}
      <Toaster />

      {/* GDPR: Cookie consent banner (ePrivacy Art. 5(3)) */}
      <CookieConsentBanner />

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

      {/* Issue #2320 (#1101 v1): mount CommandPalette so Cmd/Ctrl+K and the
          AppTopBar SearchPill trigger an actual modal. v1 ships with empty
          dataSources — the cmdk index renders correctly (graceful empty
          state) while a follow-up issue wires per-domain React Query hooks
          (messages / chats / games / agents) or a unified global-search BE
          endpoint. The keyboard shortcut + open/close UX is testable today. */}
      <CommandPalette
        isOpen={showCommandPalette}
        onClose={() => setShowCommandPalette(false)}
        dataSources={{}}
      />
    </PWAProvider>
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
 * - LayoutProvider (layout state management - Issue #3287)
 * - ErrorBoundary (error handling)
 * - Session management
 * - Keyboard shortcuts
 * - Accessibility features
 */
export function AppProviders({ children }: AppProvidersProps) {
  return (
    <IntlProvider>
      <ThemeProvider>
        <QueryProvider>
          <AuthProvider>
            <LayoutProvider>
              <ErrorBoundary
                componentName="App"
                showDetails={process.env.NODE_ENV === 'development'}
              >
                <RouteErrorBoundary routeName="AppContent">
                  <AddGameWizardProvider>
                    {/*
                      Asse B (#1897) WP5 T5 — StatePreviewProvider via dynamic loader.
                      DEC-4: `dynamic({ssr:false, loading:() => null})` guarantees the
                      provider impl is tree-shaken from production chunks (verified by
                      apps/web/__tests__/state-preview-tree-shake.test.ts). In prod the
                      loader renders `null` and the provider impl never lands in entry
                      chunks. In dev, the StatePreviewToggle is mounted by consumers
                      that opt in via `useStatePreview`.
                    */}
                    <StatePreviewProvider>
                      <AppContent>{children}</AppContent>
                    </StatePreviewProvider>
                  </AddGameWizardProvider>
                </RouteErrorBoundary>
              </ErrorBoundary>
            </LayoutProvider>
          </AuthProvider>
        </QueryProvider>
      </ThemeProvider>
    </IntlProvider>
  );
}
