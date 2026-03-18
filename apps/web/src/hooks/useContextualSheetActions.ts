'use client';

import { useMemo } from 'react';

import {
  HelpCircle,
  BookOpen,
  MessageSquare,
  Target,
  Gamepad2,
  Bot,
  StickyNote,
  Trophy,
  Square,
  Share2,
  Plus,
  Upload,
  Filter,
  Zap,
  Clock,
} from 'lucide-react';
import { usePathname, useRouter } from 'next/navigation';

import type { LucideIcon } from 'lucide-react';

export interface SheetAction {
  id: string;
  label: string;
  icon: LucideIcon;
  onClick: () => void;
  variant?: 'primary' | 'secondary';
}

export function useContextualSheetActions(): SheetAction[] {
  const pathname = usePathname();
  const router = useRouter();

  return useMemo(() => {
    if (!pathname) return [];

    // Game detail: /library/games/[id] or /games/[id]
    const gameMatch = pathname.match(/^\/(library\/)?games\/([^/]+)$/);
    if (gameMatch) {
      const gameId = gameMatch[2];
      return [
        {
          id: 'faq',
          label: 'FAQ',
          icon: HelpCircle,
          onClick: () => router.push(`${pathname}?tab=faq`),
        },
        {
          id: 'rules',
          label: 'Rules',
          icon: BookOpen,
          onClick: () => router.push(`${pathname}?tab=rules`),
        },
        {
          id: 'reviews',
          label: 'Reviews',
          icon: MessageSquare,
          onClick: () => router.push(`${pathname}?tab=reviews`),
        },
        {
          id: 'strategy',
          label: 'Strategy',
          icon: Target,
          onClick: () => router.push(`${pathname}?tab=strategy`),
        },
        {
          id: 'ask-ai',
          label: 'Ask AI',
          icon: Bot,
          onClick: () => router.push(`/chat/new?game=${gameId}`),
        },
        {
          id: 'new-session',
          label: 'New Session',
          icon: Gamepad2,
          onClick: () => router.push(`/sessions/new?game=${gameId}`),
          variant: 'primary' as const,
        },
      ];
    }

    // Session: /sessions/[id] (plural)
    if (/^\/sessions\/[^/]+/.test(pathname)) {
      return [
        { id: 'add-note', label: 'Add Note', icon: StickyNote, onClick: () => {} },
        { id: 'scoreboard', label: 'Scoreboard', icon: Trophy, onClick: () => {} },
        { id: 'end-session', label: 'End Session', icon: Square, onClick: () => {} },
        { id: 'share', label: 'Share', icon: Share2, onClick: () => {} },
      ];
    }

    // Chat: /chat/[id] (but not /chat/new)
    if (/^\/chat\/[^/]+$/.test(pathname) && !pathname.endsWith('/new')) {
      return [
        {
          id: 'new-topic',
          label: 'New Topic',
          icon: Plus,
          onClick: () => router.push('/chat/new'),
        },
        { id: 'history', label: 'History', icon: Clock, onClick: () => {} },
        {
          id: 'settings',
          label: 'Settings',
          icon: Filter,
          onClick: () => router.push('/settings'),
        },
      ];
    }

    // Library list
    if (pathname === '/library') {
      return [
        {
          id: 'add-game',
          label: 'Add Game',
          icon: Plus,
          onClick: () => {},
          variant: 'primary' as const,
        },
        { id: 'import', label: 'Import', icon: Upload, onClick: () => {} },
        { id: 'filter', label: 'Filter', icon: Filter, onClick: () => {} },
      ];
    }

    // Dashboard
    if (pathname === '/dashboard' || pathname === '/') {
      return [
        {
          id: 'quick-start',
          label: 'Quick Start',
          icon: Zap,
          onClick: () => router.push('/games'),
          variant: 'primary' as const,
        },
        {
          id: 'recent-games',
          label: 'Recent Games',
          icon: Clock,
          onClick: () => router.push('/library'),
        },
      ];
    }

    // Catalog, default: no actions
    return [];
  }, [pathname, router]);
}
