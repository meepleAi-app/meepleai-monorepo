'use client';

import { useState, useEffect, useRef } from 'react';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { cn } from '@/lib/utils';
import { useContextualHandStore, selectContext, selectIsLoading } from '@/stores/contextual-hand';

const CTA_MAP: Record<string, { label: string; href: string }> = {
  '/library': { label: '+ Aggiungi gioco', href: '/library?action=add' },
  '/sessions': { label: '+ Nuova sessione', href: '/sessions/new' },
  '/play-records': { label: '+ Nuova partita', href: '/play-records/new' },
  '/chat': { label: '+ Nuova chat', href: '/chat/new' },
};

/**
 * MobileCTAPill — mobile-only floating action pill.
 * Shows a single primary CTA anchored to the bottom-center of the screen.
 * Hides on scroll-down, reappears on scroll-up or near top (<50px).
 * Only renders on pages that have a CTA mapping.
 */
export function MobileCTAPill() {
  const pathname = usePathname();
  const cta = CTA_MAP[pathname];

  // Hide when `ContextualHandBottomBar` is visible to avoid z-index overlap
  // (the bar is mobile-only and already occupies the bottom 64px when an
  // active session exists — see ContextualHandBottomBar render gate).
  const contextualHandContext = useContextualHandStore(selectContext);
  const contextualHandLoading = useContextualHandStore(selectIsLoading);
  const contextualHandActive = contextualHandContext !== 'idle' && !contextualHandLoading;

  const [visible, setVisible] = useState(true);
  const lastScrollY = useRef(0);

  useEffect(() => {
    const handleScroll = () => {
      const currentY = window.scrollY;
      const atTop = currentY < 50;
      const scrollingUp = currentY < lastScrollY.current;

      setVisible(atTop || scrollingUp);
      lastScrollY.current = currentY;
    };

    window.addEventListener('scroll', handleScroll, { passive: true });
    return () => window.removeEventListener('scroll', handleScroll);
  }, []);

  if (!cta) return null;
  if (contextualHandActive) return null;

  return (
    <div
      data-testid="mobile-cta-pill"
      className={cn(
        'fixed bottom-4 left-1/2 -translate-x-1/2 z-50',
        'md:hidden',
        'transition-all duration-300 ease-in-out',
        visible ? 'opacity-100 translate-y-0' : 'opacity-0 translate-y-24 pointer-events-none'
      )}
    >
      <Link
        href={cta.href}
        className={cn(
          'flex items-center gap-2 px-5 py-3 rounded-full',
          'bg-orange-600 hover:bg-orange-700 active:bg-orange-800',
          'text-white text-sm font-semibold',
          'shadow-lg shadow-orange-900/30',
          'transition-colors duration-150'
        )}
      >
        {cta.label}
      </Link>
    </div>
  );
}
