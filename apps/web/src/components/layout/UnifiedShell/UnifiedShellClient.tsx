'use client';

import { type ReactNode, useEffect } from 'react';

import { usePathname } from 'next/navigation';

import { ErrorBoundary } from '@/components/errors/ErrorBoundary';
import { DEFAULT_PINNED_CARDS } from '@/config/entity-actions';
import { cn } from '@/lib/utils';
import { useCardHand } from '@/stores/use-card-hand';

import { AdminTabSidebar } from './AdminTabSidebar';
import { CardStack } from './CardStack';
import { ContextualBottomNav } from './ContextualBottomNav';
import { UnifiedTopNav } from './UnifiedTopNav';

interface UnifiedShellClientProps {
  children: ReactNode;
  isAdmin: boolean;
  /** Slot for ImpersonationBanner */
  impersonationBanner?: ReactNode;
  /** Slot for OnboardingReminderBanner */
  onboardingBanner?: ReactNode;
  /** Slot for user menu */
  userMenu?: ReactNode;
  /** Slot for notification bell */
  notificationBell?: ReactNode;
  /** Slot for command palette trigger */
  searchTrigger?: ReactNode;
}

export function UnifiedShellClient({
  children,
  isAdmin,
  impersonationBanner,
  onboardingBanner,
  userMenu,
  notificationBell,
  searchTrigger,
}: UnifiedShellClientProps) {
  const pathname = usePathname();
  const { context, toggleContext, drawCard, pinCard, cards } = useCardHand();
  const isAdminContext = context === 'admin';

  // Auto-set admin context when mounted from an admin route
  useEffect(() => {
    if (isAdmin && context !== 'admin') {
      toggleContext();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAdmin]); // Only on mount

  // Seed default pinned section cards on first load
  useEffect(() => {
    if (cards.length === 0) {
      DEFAULT_PINNED_CARDS.forEach(card => {
        drawCard(card);
        pinCard(card.id);
      });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <div className="flex flex-col h-screen">
      {/* Impersonation banner (above everything) */}
      {impersonationBanner}

      {/* Top Nav */}
      <ErrorBoundary fallback={null} componentName="UnifiedTopNav">
        <UnifiedTopNav
          isAdmin={isAdmin}
          userMenu={userMenu}
          notificationBell={notificationBell}
          searchTrigger={searchTrigger}
        />
      </ErrorBoundary>

      {/* Onboarding banner */}
      {onboardingBanner}

      {/* Main body: left panel + content */}
      <div className="flex flex-1 overflow-hidden">
        {/* Left panel: CardStack (user) or AdminTabSidebar (admin) */}
        <ErrorBoundary fallback={null} componentName="LeftPanel">
          {isAdminContext ? <AdminTabSidebar /> : <CardStack />}
        </ErrorBoundary>

        {/* Main content area */}
        <main
          id="main-content"
          className={cn('flex-1 overflow-y-auto', 'focus:outline-none')}
          tabIndex={-1}
        >
          <ErrorBoundary componentName="PageContent">{children}</ErrorBoundary>
        </main>
      </div>

      {/* Bottom Nav */}
      <ErrorBoundary fallback={null} componentName="ContextualBottomNav">
        <ContextualBottomNav />
      </ErrorBoundary>
    </div>
  );
}
