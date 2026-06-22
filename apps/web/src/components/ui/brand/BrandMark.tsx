'use client';

import { cn } from '@/lib/utils';

export type BrandMarkVariant = 'icon' | 'wordmark' | 'full';
export type BrandMarkSize = 'sm' | 'md' | 'lg';

export interface BrandMarkProps {
  /** Which parts to render. Default `full` (icon + wordmark). */
  variant?: BrandMarkVariant;
  /** Sizing scale. Default `sm` (matches AppTopBar). */
  size?: BrandMarkSize;
  /** Append "Admin" badge after wordmark (used by AdminShell topbar). */
  adminBadge?: boolean;
  /** Optional wrapper className. */
  className?: string;
  /** Optional override for the wordmark text (defaults to "MeepleAI"). */
  wordmarkText?: string;
}

const ICON_SIZE: Record<BrandMarkSize, string> = {
  sm: 'h-7 w-7 text-sm rounded-[10px]',
  md: 'h-9 w-9 text-base rounded-[12px]',
  lg: 'h-12 w-12 text-lg rounded-[14px]',
};

const WORDMARK_SIZE: Record<BrandMarkSize, string> = {
  sm: 'text-sm',
  md: 'text-base',
  lg: 'text-xl',
};

/**
 * Canonical MeepleAI brand mark. Single source of truth for the visual logo:
 * an "M" letter inside a gradient square (game → event entity tokens) + the
 * "MeepleAI" wordmark. Replaces the legacy `MeepleLogo` SVG (issue #2057).
 *
 * Used by `AppTopBar` (UserShell + AdminShell), `UnifiedHeader` (PublicLayout),
 * `AuthLayout` (login/register), and `PublicFooter`.
 */
export function BrandMark({
  variant = 'full',
  size = 'sm',
  adminBadge = false,
  className,
  wordmarkText = 'MeepleAI',
}: BrandMarkProps) {
  const showIcon = variant === 'icon' || variant === 'full';
  const showWordmark = variant === 'wordmark' || variant === 'full';

  return (
    <span
      data-slot="brand-mark"
      data-variant={variant}
      data-size={size}
      className={cn('inline-flex items-center gap-2', className)}
    >
      {showIcon && (
        <span
          aria-hidden="true"
          className={cn(
            'flex shrink-0 items-center justify-center bg-[linear-gradient(135deg,hsl(var(--c-game)),hsl(var(--c-event)))] font-quicksand font-extrabold text-white',
            ICON_SIZE[size]
          )}
        >
          M
        </span>
      )}
      {showWordmark && (
        <span className={cn('font-quicksand font-bold text-foreground', WORDMARK_SIZE[size])}>
          {wordmarkText}
        </span>
      )}
      {adminBadge && (
        <span className="ml-1 rounded bg-destructive/10 px-1.5 py-0.5 text-[10px] font-semibold leading-none text-destructive">
          Admin
        </span>
      )}
    </span>
  );
}
