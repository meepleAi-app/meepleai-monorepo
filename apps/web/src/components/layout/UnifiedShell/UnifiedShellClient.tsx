'use client';

import { type ReactNode, useEffect, useState } from 'react';

import { usePathname } from 'next/navigation';

import { AgentCreationSheet } from '@/components/agent/config/AgentCreationSheet';
import { DashboardEngineProvider } from '@/components/dashboard';
import { ErrorBoundary } from '@/components/errors/ErrorBoundary';
import { SearchAgentSheet } from '@/components/sheets/SearchAgentSheet';
import { SearchGameSheet } from '@/components/sheets/SearchGameSheet';
import { SessionSheet } from '@/components/sheets/SessionSheet';
import { ToolkitSheet } from '@/components/sheets/ToolkitSheet';
import { ALL_DEFAULT_CARDS, PLACEHOLDER_ACTION_CARDS } from '@/config/entity-actions';
import { usePlaceholderActions } from '@/hooks/usePlaceholderActions';
import { useResponsive } from '@/hooks/useResponsive';
import { cn } from '@/lib/utils';
import { useCardHand } from '@/stores/use-card-hand';

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

  // Seed default cards (including placeholder action cards) on first load
  useEffect(() => {
    if (cards.length === 0) {
      ALL_DEFAULT_CARDS.forEach(card => {
        drawCard(card);
        pinCard(card.id);
      });
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  // Migration: add placeholder action cards for returning users who already have cards
  // Guard: only runs if user has existing cards but no placeholders (not for fresh users)
  useEffect(() => {
    if (cards.length > 0 && !cards.some(c => c.isPlaceholder)) {
      PLACEHOLDER_ACTION_CARDS.forEach(card => {
        drawCard(card);
        pinCard(card.id);
      });
    }
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

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

      {/* Hand Drawer (mobile only, non-admin) */}
      {!isDesktop && !isAdminContext && <HandDrawer onPlaceholderClick={handleCardClick} />}

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
            id="main-content"
            className={cn('flex-1 overflow-y-auto', 'focus:outline-none')}
            tabIndex={-1}
          >
            <ErrorBoundary componentName="PageContent">{children}</ErrorBoundary>
          </main>
        </div>
      </DashboardEngineProvider>

      {/* Bottom Nav */}
      <ErrorBoundary fallback={null} componentName="ContextualBottomNav">
        <ContextualBottomNav />
      </ErrorBoundary>

      {/* Action sheets */}
      <SearchGameSheet isOpen={activeSheet === 'search-game'} onClose={closeSheet} />
      <SessionSheet isOpen={activeSheet === 'start-session'} onClose={closeSheet} />
      <ToolkitSheet isOpen={activeSheet === 'toolkit'} onClose={closeSheet} />
      <SearchAgentSheet
        isOpen={activeSheet === 'search-agent'}
        onClose={closeSheet}
        onCreateAgent={(gameId, gameTitle, documentIds, documentSummary) => {
          closeSheet();
          setAgentWizardState({ gameId, gameTitle, documentIds, documentSummary });
        }}
      />
      {agentWizardState && (
        <AgentCreationSheet
          isOpen={!!agentWizardState}
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
