'use client';

import { type ReactNode, useEffect, useRef, useState } from 'react';

import { usePathname } from 'next/navigation';

import { DashboardEngineProvider } from '@/components/dashboard';
import { ErrorBoundary } from '@/components/errors/ErrorBoundary';
import { ContextualBottomSheet } from '@/components/layout/ContextualBottomSheet';
import { MobileTabBar } from '@/components/layout/MobileTabBar';
import { DEFAULT_PINNED_CARDS } from '@/config/entity-actions';
import { useBottomPadding } from '@/hooks/useBottomPadding';
import { useContextualEntity } from '@/hooks/useContextualEntity';
import { useContextualSheetActions } from '@/hooks/useContextualSheetActions';
import { usePlaceholderActions } from '@/hooks/usePlaceholderActions';
import { useResponsive } from '@/hooks/useResponsive';
import { useScrollHideNav } from '@/hooks/useScrollHideNav';
import { cn } from '@/lib/utils';
import { useCardHand } from '@/stores/use-card-hand';

import { AdminBreadcrumb } from './AdminBreadcrumb';
import { AdminMobileDrawer } from './AdminMobileDrawer';
import { AdminTabSidebar } from './AdminTabSidebar';
import { CardStack } from './CardStack';
import { ContextualBottomNav } from './ContextualBottomNav';
import { HandDrawer } from './HandDrawer';
import { NavbarMiniCards } from './NavbarMiniCards';
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
  const {
    context,
    toggleContext,
    drawCard,
    pinCard,
    cards,
    isHandCollapsed,
    expandHand,
    focusCard,
  } = useCardHand();
  const isAdminContext = context === 'admin';
  const { isDesktop } = useResponsive();
  const { handleCardClick, activeSheet, closeSheet } = usePlaceholderActions();
  const [adminDrawerOpen, setAdminDrawerOpen] = useState(false);

  const mainRef = useRef<HTMLElement>(null);
  const { isNavVisible } = useScrollHideNav({ scrollContainerRef: mainRef });
  const contextualEntity = useContextualEntity();
  const sheetActions = useContextualSheetActions();
  const [isBottomSheetOpen, setIsBottomSheetOpen] = useState(false);
  const hasSheetContent = sheetActions.length > 0 || cards.length > 0;
  const bottomPaddingClass = useBottomPadding();

  // Agent wizard handoff state: carries game + KB selection from SearchAgentSheet → AgentCreationSheet
  const [agentWizardState, setAgentWizardState] = useState<{
    gameId: string;
    gameTitle: string;
    documentIds: string[];
    documentSummary: string;
  } | null>(null);

  // Sync context with current route: admin routes → admin context, user routes → user context
  useEffect(() => {
    if (isAdmin && context !== 'admin') {
      toggleContext();
    } else if (!isAdmin && context === 'admin' && !pathname.startsWith('/admin')) {
      toggleContext();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isAdmin, pathname]);

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
          isNavVisible={isNavVisible}
          onMenuToggle={isAdminContext ? () => setAdminDrawerOpen(true) : undefined}
          isMenuOpen={isAdminContext ? adminDrawerOpen : undefined}
          userMenu={userMenu}
          notificationBell={notificationBell}
          searchTrigger={searchTrigger}
          miniCards={
            !isDesktop && isHandCollapsed && cards.length > 0 ? (
              <NavbarMiniCards
                cards={cards}
                onExpand={id => {
                  expandHand();
                  const idx = cards.findIndex(c => c.id === id);
                  if (idx >= 0) focusCard(idx);
                }}
              />
            ) : undefined
          }
        />
      </ErrorBoundary>

      {/* Admin mobile: breadcrumb + drawer */}
      {isAdminContext && (
        <>
          <AdminBreadcrumb />
          <AdminMobileDrawer open={adminDrawerOpen} onOpenChange={setAdminDrawerOpen} />
        </>
      )}

      {/* Hand Drawer (desktop only, non-admin — replaced by MobileTabBar + ContextualBottomSheet on mobile) */}
      {isDesktop && !isAdminContext && <HandDrawer onPlaceholderClick={handleCardClick} />}

      {/* Onboarding banner */}
      {onboardingBanner}

      {/* Main body: left panel + content */}
      <DashboardEngineProvider>
        <div className="flex flex-1 overflow-hidden">
          {/* Left panel: CardStack (desktop user) or AdminTabSidebar (admin) */}
          <ErrorBoundary fallback={null} componentName="LeftPanel">
            {isAdminContext ? (
              <AdminTabSidebar />
            ) : (
              <div className="hidden lg:flex">
                <CardStack />
              </div>
            )}
          </ErrorBoundary>

          {/* Main content area */}
          <main
            ref={mainRef}
            id="main-content"
            className={cn('flex-1 overflow-y-auto', 'focus:outline-none', bottomPaddingClass)}
            tabIndex={-1}
          >
            <ErrorBoundary componentName="PageContent">{children}</ErrorBoundary>
          </main>
        </div>
      </DashboardEngineProvider>

      {/* Bottom Nav (desktop only — component uses hidden md:flex internally) */}
      <ErrorBoundary fallback={null} componentName="ContextualBottomNav">
        <ContextualBottomNav />
      </ErrorBoundary>

      {/* Mobile Tab Bar */}
      <MobileTabBar
        contextualEntity={contextualEntity}
        hasSheetContent={hasSheetContent}
        onCenterTabPress={() => setIsBottomSheetOpen(true)}
      />

      {/* Contextual Bottom Sheet (mobile) */}
      <ContextualBottomSheet
        isOpen={isBottomSheetOpen}
        onClose={() => setIsBottomSheetOpen(false)}
      />
    </div>
  );
}
