'use client';

import { type ReactNode, useEffect, useRef, useState } from 'react';

import { usePathname } from 'next/navigation';

import { AgentCreationSheet } from '@/components/agent/config/AgentCreationSheet';
import { DashboardEngineProvider } from '@/components/dashboard-v2';
import { ErrorBoundary } from '@/components/errors/ErrorBoundary';
import { ContextualBottomSheet } from '@/components/layout/ContextualBottomSheet';
import { MobileTabBar } from '@/components/layout/MobileTabBar';
import { SearchAgentSheet } from '@/components/sheets/SearchAgentSheet';
import { SearchGameSheet } from '@/components/sheets/SearchGameSheet';
import { SessionSheet } from '@/components/sheets/SessionSheet';
import { ToolkitSheet } from '@/components/sheets/ToolkitSheet';
import { ALL_DEFAULT_CARDS, PLACEHOLDER_ACTION_CARDS } from '@/config/entity-actions';
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

  // Seed default cards (pinned + placeholders) on first load
  useEffect(() => {
    if (cards.length === 0) {
      ALL_DEFAULT_CARDS.forEach(card => {
        drawCard(card);
        pinCard(card.id);
      });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Migration: inject placeholder cards for returning users who don't have them yet
  useEffect(() => {
    const hasPlaceholders = cards.some(c => c.isPlaceholder);
    if (cards.length > 0 && !hasPlaceholders) {
      PLACEHOLDER_ACTION_CARDS.forEach(card => {
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
                <CardStack onPlaceholderClick={handleCardClick} />
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

      {/* Placeholder action sheets */}
      <SearchAgentSheet
        isOpen={activeSheet === 'search-agent'}
        onClose={closeSheet}
        onCreateAgent={(gameId, gameTitle, documentIds, documentSummary) => {
          closeSheet();
          setAgentWizardState({ gameId, gameTitle, documentIds, documentSummary });
        }}
      />
      <SearchGameSheet isOpen={activeSheet === 'search-game'} onClose={closeSheet} />
      <SessionSheet isOpen={activeSheet === 'start-session'} onClose={closeSheet} />
      <ToolkitSheet isOpen={activeSheet === 'toolkit'} onClose={closeSheet} />

      {/* Agent creation wizard (opened after SearchAgentSheet KB selection) */}
      {agentWizardState && (
        <AgentCreationSheet
          isOpen={true}
          onClose={() => setAgentWizardState(null)}
          initialGameId={agentWizardState.gameId}
          initialGameTitle={agentWizardState.gameTitle}
          initialDocumentIds={agentWizardState.documentIds}
          initialDocumentSummary={agentWizardState.documentSummary}
          skipGameSelection
          skipKBUpload
        />
      )}
    </div>
  );
}
