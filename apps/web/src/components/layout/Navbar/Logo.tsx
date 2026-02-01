/**
 * Logo - Brand logo with responsive variants
 * Issue #3288 - Phase 2: Navbar Components
 *
 * Features:
 * - 'full' variant: Icon + wordmark (desktop)
 * - 'icon' variant: Icon only (mobile)
 * - Responsive display based on viewport
 * - Reuses MeepleLogo component
 */

'use client';

import Link from 'next/link';

import { MeepleLogo } from '@/components/ui/meeple/meeple-logo';
import { useLayout } from '@/components/layout/LayoutProvider';
import { cn } from '@/lib/utils';

export interface LogoProps {
  /** Force a specific variant */
  variant?: 'full' | 'icon' | 'auto';
  /** Size of the logo */
  size?: 'sm' | 'md' | 'lg';
  /** Whether the logo links to home */
  asLink?: boolean;
  /** Additional className */
  className?: string;
}

/**
 * Logo Component
 *
 * Displays the MeepleAI brand logo with responsive behavior.
 * Shows icon-only on mobile, full logo on desktop.
 */
export function Logo({
  variant = 'auto',
  size = 'sm',
  asLink = true,
  className,
}: LogoProps) {
  const { responsive } = useLayout();
  const { isMobile } = responsive;

  // Determine actual variant based on viewport
  const actualVariant = variant === 'auto'
    ? isMobile ? 'icon' : 'full'
    : variant;

  const logoContent = (
    <div
      className={cn(
        'flex items-center gap-2',
        'transition-transform duration-200 hover:scale-[1.02]',
        className
      )}
    >
      <MeepleLogo
        variant={actualVariant}
        size={size}
      />
      {/* Show text on non-mobile or when forced to full */}
      {actualVariant === 'full' && (
        <span className="font-quicksand font-bold text-xl text-foreground hidden sm:inline">
          MeepleAI
        </span>
      )}
    </div>
  );

  if (asLink) {
    return (
      <Link
        href="/"
        className="focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(262_83%_62%)] focus-visible:ring-offset-2 rounded-md"
        aria-label="MeepleAI Home"
      >
        {logoContent}
      </Link>
    );
  }

  return logoContent;
}
