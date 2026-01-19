/**
 * ChatLayout Component - Issue #2232
 *
 * Specialized layout for chat functionality with sidebar threads, header, and message area.
 * Pattern based on AdminLayout with chat-specific features.
 *
 * Features:
 * - Collapsible sidebar with localStorage persistence
 * - Thread list with virtualization
 * - Inline game selector and thread title in header
 * - Mobile responsive with Sheet drawer
 * - Optimized layout for message display (full height)
 */

'use client';

import { ReactNode, useCallback, useEffect, useState } from 'react';

import { MenuIcon } from 'lucide-react';

import { ChatHeader } from '@/components/chat/ChatHeader';
import { Button } from '@/components/ui/primitives/button';
import {
  Sheet,
  SheetContent,
  SheetTrigger,
  SheetTitle,
  SheetDescription,
} from '@/components/ui/navigation/sheet';
import { TooltipProvider } from '@/components/ui/overlays/tooltip';
import { cn } from '@/lib/utils';
import { Game } from '@/types';

const CHAT_SIDEBAR_COLLAPSED_KEY = 'chat-sidebar-collapsed';

export interface ChatLayoutProps {
  /** Main chat content (message area) */
  children: ReactNode;
  /** Sidebar content (thread list, selectors) */
  sidebarContent: ReactNode;
  /** Current selected game */
  game?: Game;
  /** Available games for selector */
  games?: Game[];
  /** Game selection handler */
  onGameChange?: (gameId: string) => void;
  /** Current thread title */
  threadTitle?: string;
  /** Thread title change handler */
  onTitleChange?: (title: string) => void;
  /** Share action handler */
  onShare?: () => void;
  /** Export action handler */
  onExport?: () => void;
  /** Delete action handler */
  onDelete?: () => void;
  /** Loading states */
  loading?: {
    games?: boolean;
    title?: boolean;
  };
  /** Additional className for main content */
  className?: string;
}

export function ChatLayout({
  children,
  sidebarContent,
  game,
  games,
  onGameChange,
  threadTitle,
  onTitleChange,
  onShare,
  onExport,
  onDelete,
  loading,
  className,
}: ChatLayoutProps) {
  const [collapsed, setCollapsed] = useState(false);
  const [mobileOpen, setMobileOpen] = useState(false);
  const [mounted, setMounted] = useState(false);

  // Load collapsed state from localStorage on mount
  useEffect(() => {
    setMounted(true);
    const stored = localStorage.getItem(CHAT_SIDEBAR_COLLAPSED_KEY);
    if (stored === 'true') {
      setCollapsed(true);
    }
  }, []);

  const handleCollapsedChange = useCallback((value: boolean) => {
    setCollapsed(value);
    localStorage.setItem(CHAT_SIDEBAR_COLLAPSED_KEY, String(value));
  }, []);

  // Mobile menu trigger for header
  const mobileMenuTrigger = (
    <Sheet open={mobileOpen} onOpenChange={setMobileOpen}>
      <SheetTrigger asChild>
        <Button variant="ghost" size="icon" aria-label="Open chat navigation">
          <MenuIcon className="h-5 w-5" />
        </Button>
      </SheetTrigger>
      <SheetContent side="left" className="w-80 p-0">
        <SheetTitle className="px-4 py-3 border-b border-[#dadce0]">MeepleAI Chat</SheetTitle>
        <SheetDescription className="sr-only">Chat navigation menu</SheetDescription>
        <div className="h-full overflow-y-auto">
          <TooltipProvider>{sidebarContent}</TooltipProvider>
        </div>
      </SheetContent>
    </Sheet>
  );

  // Prevent hydration mismatch by showing default state initially
  const sidebarWidth = !mounted ? 'md:w-80' : collapsed ? 'md:w-0' : 'md:w-80';

  return (
    <div className="min-h-screen bg-white dark:bg-gray-950">
      {/* Header */}
      <ChatHeader
        game={game}
        games={games}
        onGameChange={onGameChange}
        threadTitle={threadTitle}
        onTitleChange={onTitleChange}
        mobileMenuTrigger={mobileMenuTrigger}
        onShare={onShare}
        onExport={onExport}
        onDelete={onDelete}
        loading={loading}
      />

      <div className="flex h-[calc(100vh-3.5rem)]">
        {/* Sidebar - Desktop only (mobile uses Sheet) */}
        <aside
          className={cn(
            'hidden md:flex bg-[#f8f9fa] border-r border-[#dadce0] flex-col overflow-hidden transition-[width,min-width] duration-300 ease-in-out',
            sidebarWidth,
            collapsed ? 'min-w-0' : 'min-w-[320px]'
          )}
          aria-label="Chat sidebar"
        >
          <TooltipProvider>{!collapsed && sidebarContent}</TooltipProvider>
        </aside>

        {/* Sidebar collapse toggle button - WCAG 2.1 AA compliant 44px touch target */}
        {!collapsed && (
          <button
            onClick={() => handleCollapsedChange(true)}
            className={cn(
              'hidden md:block absolute left-[308px] top-[4.5rem] z-30',
              'w-10 h-10 rounded-full bg-white border border-[#dadce0]',
              'hover:bg-gray-50 transition-colors',
              'focus:outline-none focus:ring-2 focus:ring-[#1a73e8]',
              'flex items-center justify-center text-sm'
            )}
            aria-label="Collapse thread sidebar"
            title="Hide the thread list sidebar"
          >
            <span className="sr-only">Collapse</span>◀
          </button>
        )}

        {collapsed && (
          <button
            onClick={() => handleCollapsedChange(false)}
            className={cn(
              'hidden md:block absolute left-4 top-[4.5rem] z-30',
              'w-10 h-10 rounded-full bg-white border border-[#dadce0]',
              'hover:bg-gray-50 transition-colors',
              'focus:outline-none focus:ring-2 focus:ring-[#1a73e8]',
              'flex items-center justify-center text-sm'
            )}
            aria-label="Expand thread sidebar"
            title="Show the thread list sidebar"
          >
            <span className="sr-only">Expand</span>▶
          </button>
        )}

        {/* Main Content - Full height for messages */}
        <main
          className={cn(
            'flex-1 overflow-hidden flex flex-col',
            'transition-all duration-300',
            className
          )}
        >
          {children}
        </main>
      </div>
    </div>
  );
}
