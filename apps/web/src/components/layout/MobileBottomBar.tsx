'use client';

import {
  BarChart3,
  ChevronLeft,
  Home,
  Library,
  MessageCircle,
  MoreHorizontal,
  Search,
  User,
  Wrench,
} from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { useDashboardMode } from '@/components/dashboard';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';
import { cn } from '@/lib/utils';

const NORMAL_TABS = [
  { href: '/dashboard', icon: Home, label: 'Home' },
  { href: '/discover', icon: Search, label: 'Cerca' },
  { href: '/library', icon: Library, label: 'Libreria' },
  { href: '/chat', icon: MessageCircle, label: 'Chat' },
  { href: '/profile', icon: User, label: 'Profilo' },
] as const;

export function MobileBottomBar() {
  const pathname = usePathname();
  const { isGameMode, activeSessionId } = useDashboardMode();
  const inSession = isGameMode && !!activeSessionId;

  if (inSession) {
    return <SessionModeBar sessionId={activeSessionId!} />;
  }

  return (
    <nav
      className="fixed bottom-0 left-0 right-0 z-30 flex items-center justify-around border-t border-border bg-background/95 px-2 pb-[env(safe-area-inset-bottom)] pt-1.5 backdrop-blur-md md:hidden"
      data-testid="mobile-bottom-bar"
    >
      {NORMAL_TABS.map(tab => {
        const active = pathname.startsWith(tab.href);
        const Icon = tab.icon;
        return (
          <Link
            key={tab.href}
            href={tab.href}
            className={cn(
              'flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold',
              active ? 'text-[hsl(25,95%,45%)]' : 'text-muted-foreground'
            )}
          >
            <Icon className="h-5 w-5" />
            <span>{tab.label}</span>
          </Link>
        );
      })}
    </nav>
  );
}

function SessionModeBar({ sessionId }: { sessionId: string }) {
  const openDrawer = useCascadeNavigationStore(s => s.openDrawer);

  return (
    <nav
      className="fixed bottom-0 left-0 right-0 z-30 flex items-center justify-around border-t-2 border-indigo-400/60 bg-background/95 px-2 pb-[env(safe-area-inset-bottom)] pt-1.5 backdrop-blur-md md:hidden"
      data-testid="mobile-bottom-bar-session"
    >
      <button
        type="button"
        onClick={() => window.history.back()}
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold text-muted-foreground"
      >
        <ChevronLeft className="h-5 w-5" />
        <span>Back</span>
      </button>
      <button
        type="button"
        onClick={() => openDrawer('session', sessionId, 'live')}
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold text-indigo-600"
      >
        <BarChart3 className="h-5 w-5" />
        <span>Classifica</span>
      </button>
      <button
        type="button"
        onClick={() => openDrawer('session', sessionId, 'toolkit')}
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold text-muted-foreground"
      >
        <Wrench className="h-5 w-5" />
        <span>Toolkit</span>
      </button>
      {/* TODO: wire to openDrawer('chat', sessionId) once agent chat drawer is implemented */}
      <button
        type="button"
        disabled
        aria-label="Chat AI (prossimamente)"
        className={cn(
          'flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold',
          'opacity-40 cursor-not-allowed'
        )}
      >
        <MessageCircle className="h-5 w-5" />
        <span>AI</span>
      </button>
      <button
        type="button"
        className="flex min-w-[48px] flex-col items-center gap-0.5 py-1 text-[10px] font-bold text-muted-foreground"
      >
        <MoreHorizontal className="h-5 w-5" />
        <span>Altro</span>
      </button>
    </nav>
  );
}
