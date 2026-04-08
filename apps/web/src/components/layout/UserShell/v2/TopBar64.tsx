'use client';

import { UserMenuDropdown } from '@/components/layout/UserMenuDropdown';
import { NotificationBell } from '@/components/notifications';

import { TopBarChatButton } from './TopBarChatButton';
import { TopBarLogo } from './TopBarLogo';
import { TopBarNavLinks } from './TopBarNavLinks';
import { TopBarSearchPill } from './TopBarSearchPill';

interface TopBar64Props {
  onOpenChat?: () => void;
  onOpenSearch?: () => void;
}

/**
 * New 64px top bar for the UX redesign (Phase 1).
 * Composes: Logo + NavLinks + SearchPill + ChatButton + Notifications + UserMenu
 * Sticky positioning, backdrop-blur, border-bottom.
 */
export function TopBar64({ onOpenChat, onOpenSearch }: TopBar64Props) {
  return (
    <header
      data-testid="top-bar-64"
      className="sticky top-0 z-40 h-16 flex items-center gap-4 px-6 border-b border-[var(--nh-border-default)] backdrop-blur-md"
      style={{
        background: 'rgba(255, 252, 248, 0.85)',
      }}
    >
      <TopBarLogo />
      <TopBarNavLinks />
      <TopBarSearchPill onOpen={onOpenSearch} />
      <div className="flex items-center gap-2.5 shrink-0">
        <TopBarChatButton onOpen={onOpenChat} />
        <NotificationBell />
        <UserMenuDropdown />
      </div>
    </header>
  );
}
