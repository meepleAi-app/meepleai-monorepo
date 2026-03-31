'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { getCtaForPathname } from '@/config/contextual-cta';
import { cn } from '@/lib/utils';

/**
 * Renders the primary contextual action button for the current section.
 * Returns null for sections with no defined primary action.
 * Shown in the TopNav center on desktop (lg+). Hidden on mobile — the FAB
 * in UserTabBar serves the same purpose there.
 */
export function ContextualCTA() {
  const pathname = usePathname();
  const cta = getCtaForPathname(pathname);

  if (!cta) return null;

  return (
    <Link
      href={cta.href}
      data-testid="contextual-cta"
      className={cn(
        'hidden lg:flex items-center h-8 px-4 rounded-lg',
        'bg-gradient-to-r',
        cta.gradient,
        'text-white text-[13px] font-semibold whitespace-nowrap',
        'hover:opacity-90 transition-opacity'
      )}
    >
      {cta.label}
    </Link>
  );
}
