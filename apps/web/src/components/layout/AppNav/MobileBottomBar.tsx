'use client';

import { Gamepad2 } from 'lucide-react';
import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { BOTTOM_TAB_LABEL_OVERRIDES } from '@/config/navigation';
import { useNavigationItems } from '@/hooks/useNavigationItems';
import { useLiveSessionStore } from '@/lib/stores/live-session-store';
import { cn } from '@/lib/utils';

import { isImmersiveRoute } from './immersive-routes';

// Re-exported for the existing MobileBottomBar test + the AppNav barrel.
export { isImmersiveRoute };

interface MobileBottomBarProps {
  className?: string;
}

/**
 * Mobile bottom tab bar (sp4-dashboard graphic): the fixed primary tabs.
 * Hidden at `md`+ and on immersive in-session routes.
 *
 * Issue #2150 — runtime backfill of nav-chrome primitive D6:
 * the `chat` slot (slot 3 of `BOTTOM_TAB_NAV_IDS`) is dynamically swapped
 * with a "Live" entry pointing at the active live session when
 * `useLiveSessionStore.sessionId !== null` and the session is not yet
 * `Completed`. The slot returns to `chat` automatically when the session
 * ends. Matches the `primitive-nav-bottom-mobile.html` mockup contract
 * (`Aaron in /library, slot 3 = "💬 Chat"` idle, swapped during play).
 */
export function MobileBottomBar({ className }: MobileBottomBarProps) {
  const pathname = usePathname();
  const { bottomTabItems, isItemActive } = useNavigationItems();
  const liveSessionId = useLiveSessionStore(s => s.sessionId);
  const liveSessionStatus = useLiveSessionStore(s => s.status);

  if (isImmersiveRoute(pathname) || bottomTabItems.length === 0) {
    return null;
  }

  const showLiveSlot = liveSessionId !== null && liveSessionStatus !== 'Completed';

  return (
    <nav
      data-testid="mobile-bottom-bar"
      aria-label="Navigazione principale"
      className={cn(
        'fixed inset-x-0 bottom-0 z-40 flex border-t px-2 pb-3 pt-2 md:hidden',
        className
      )}
      style={{
        backdropFilter: 'blur(14px)',
        WebkitBackdropFilter: 'blur(14px)',
        background: 'var(--glass-bg)',
        borderColor: 'var(--border)',
      }}
    >
      {bottomTabItems.map(item => {
        // Issue #2150 — D6 dynamic slot-3: swap `chat` for a `live` link when
        // a session is active. Tested via the same `data-testid` slot prefix
        // (`bottom-tab-live`) so consumers can detect the swap.
        if (item.id === 'chat' && showLiveSlot && liveSessionId) {
          const liveActive = pathname?.startsWith(`/sessions/${liveSessionId}`);
          return (
            <Link
              key="live"
              href={`/sessions/${liveSessionId}/live`}
              data-testid="bottom-tab-live"
              data-slot="bottom-tab-live"
              aria-current={liveActive ? 'page' : undefined}
              aria-label="Riprendi sessione in corso"
              className={cn(
                'flex min-h-[44px] flex-1 flex-col items-center justify-center gap-0.5 rounded-md py-1 text-[10px] font-bold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
                liveActive
                  ? 'text-entity-game-text'
                  : 'text-entity-game-text/80 hover:text-entity-game-text'
              )}
            >
              <Gamepad2
                className={cn('h-[22px] w-[22px] transition-transform', liveActive && 'scale-105')}
              />
              <span>Live</span>
            </Link>
          );
        }

        const Icon = item.icon;
        const active = isItemActive(item, pathname);
        const label = BOTTOM_TAB_LABEL_OVERRIDES[item.id] ?? item.label;
        return (
          <Link
            key={item.id}
            href={item.href}
            data-testid={`bottom-tab-${item.id}`}
            aria-current={active ? 'page' : undefined}
            className={cn(
              'flex min-h-[44px] flex-1 flex-col items-center justify-center gap-0.5 rounded-md py-1 text-[10px] font-bold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring',
              active ? 'text-entity-game-text' : 'text-muted-foreground hover:text-foreground'
            )}
          >
            <Icon className={cn('h-[22px] w-[22px] transition-transform', active && 'scale-105')} />
            <span>{label}</span>
          </Link>
        );
      })}
    </nav>
  );
}
