'use client';

import { UserMenuDropdown } from '@/components/layout/UserMenuDropdown';
import { NotificationBell } from '@/components/notifications';
import { useChatPanel } from '@/hooks/useChatPanel';

import { TopBarChatButton } from './TopBarChatButton';
import { TopBarLogo } from './TopBarLogo';
import { TopBarNavLinks } from './TopBarNavLinks';
import { TopBarSearchPill } from './TopBarSearchPill';

interface TopBarProps {
  onOpenChat?: () => void;
  onOpenSearch?: () => void;
}

/**
 * Small wrapper that subscribes to the chat panel store.
 * Extracted so the subscription doesn't cause TopBar itself to re-render
 * on every chat open/close.
 */
function TopBarChatButtonConnected() {
  const { open } = useChatPanel();
  return <TopBarChatButton onOpen={open} />;
}

/**
 * New 64px top bar for the UX redesign (Phase 1).
 * Composes: Logo + NavLinks + SearchPill + ChatButton + Notifications + UserMenu
 * Sticky positioning, backdrop-blur, border-bottom.
 */
export function TopBar({ onOpenChat, onOpenSearch }: TopBarProps) {
  return (
    <header
      data-testid="top-bar"
      className="sticky top-0 z-40 h-16 flex items-center gap-4 px-6 border-b border-[var(--border-glass)] backdrop-blur-[16px]"
      style={{ background: 'color-mix(in srgb, var(--bg-elevated) 95%, transparent)' }}
    >
      <TopBarLogo />
      <TopBarNavLinks />
      <TopBarSearchPill onOpen={onOpenSearch} />
      <div className="flex items-center gap-2.5 shrink-0">
        {onOpenChat ? <TopBarChatButton onOpen={onOpenChat} /> : <TopBarChatButtonConnected />}
        <NotificationBell />
        <UserMenuDropdown />
      </div>
    </header>
  );
}
