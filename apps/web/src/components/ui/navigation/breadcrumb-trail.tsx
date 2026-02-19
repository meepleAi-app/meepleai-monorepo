/**
 * BreadcrumbTrail - Glassmorphic entity navigation breadcrumbs
 *
 * Renders a horizontal breadcrumb bar showing the user's navigation trail
 * across MeepleCard entities. Each step uses the entity's colour and icon.
 *
 * Features:
 * - Entity-coloured steps with matching icons
 * - Glassmorphic pill design
 * - Responsive: collapses middle steps on small screens
 * - Clickable steps to navigate back in the trail
 * - Smooth entry/exit animations
 *
 * @see Issue #4704
 * @see Epic #4688 - MeepleCard Navigation System
 */

'use client';

import { useCallback } from 'react';

import Link from 'next/link';
import { ChevronRight, Home, X } from 'lucide-react';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';
import { ENTITY_NAV_ICONS } from '@/components/ui/data-display/meeple-card-features/navigation-icons';
import { cn } from '@/lib/utils';

import type { BreadcrumbStep } from '@/hooks/use-navigation-trail';
import { useNavigationTrail } from '@/hooks/use-navigation-trail';

// ---------------------------------------------------------------------------
// Entity HSL colours (duplicated from CardNavigationFooter to avoid circular)
// ---------------------------------------------------------------------------

const ENTITY_HSL: Record<MeepleEntityType, string> = {
  game: '25 95% 45%',
  player: '262 83% 58%',
  session: '240 60% 55%',
  agent: '38 92% 50%',
  document: '210 40% 55%',
  chatSession: '220 80% 55%',
  event: '350 89% 60%',
  custom: '220 70% 50%',
};

// ---------------------------------------------------------------------------
// Sub-components
// ---------------------------------------------------------------------------

function BreadcrumbStep({
  step,
  index,
  isLast,
  onNavigate,
}: {
  step: BreadcrumbStep;
  index: number;
  isLast: boolean;
  onNavigate: (index: number) => void;
}) {
  // eslint-disable-next-line security/detect-object-injection -- entity from typed union
  const Icon = ENTITY_NAV_ICONS[step.entity] ?? ENTITY_NAV_ICONS.game;
  // eslint-disable-next-line security/detect-object-injection -- entity from typed union
  const hsl = ENTITY_HSL[step.entity] ?? ENTITY_HSL.custom;

  const handleClick = useCallback(
    (e: React.MouseEvent) => {
      if (!isLast) {
        e.preventDefault();
        onNavigate(index);
      }
    },
    [index, isLast, onNavigate],
  );

  return (
    <>
      <Link
        href={step.href}
        onClick={handleClick}
        className={cn(
          'group/step flex items-center gap-1.5 px-2.5 py-1 rounded-full',
          'transition-all duration-200',
          'no-underline whitespace-nowrap',
          isLast
            ? 'bg-[hsl(var(--step-hsl)/0.12)] font-semibold'
            : 'hover:bg-[hsl(var(--step-hsl)/0.08)]',
        )}
        style={{ '--step-hsl': hsl } as React.CSSProperties}
        title={step.label}
      >
        <Icon
          className={cn(
            'w-3.5 h-3.5 shrink-0 transition-colors duration-200',
            isLast
              ? 'text-[hsl(var(--step-hsl))]'
              : 'text-muted-foreground group-hover/step:text-[hsl(var(--step-hsl))]',
          )}
        />
        <span
          className={cn(
            'text-xs font-nunito transition-colors duration-200',
            isLast
              ? 'text-[hsl(var(--step-hsl))]'
              : 'text-muted-foreground group-hover/step:text-foreground',
          )}
        >
          {step.label}
        </span>
      </Link>

      {!isLast && (
        <ChevronRight className="w-3 h-3 text-muted-foreground/50 shrink-0" />
      )}
    </>
  );
}

// ---------------------------------------------------------------------------
// Main component
// ---------------------------------------------------------------------------

interface BreadcrumbTrailProps {
  className?: string;
}

export function BreadcrumbTrail({ className }: BreadcrumbTrailProps) {
  const { trail, navigateTo, clear } = useNavigationTrail();

  if (trail.length === 0) return null;

  // On small screens show first, last, and an ellipsis for middle steps
  const showCollapsed = trail.length > 3;

  return (
    <nav
      className={cn(
        'flex items-center gap-1',
        'px-3 py-2',
        'bg-card/60 dark:bg-card/40',
        'backdrop-blur-md',
        'border border-border/30',
        'rounded-xl',
        'shadow-sm',
        'animate-in fade-in slide-in-from-top-2 duration-300',
        className,
      )}
      aria-label="Navigation trail"
      data-testid="breadcrumb-trail"
    >
      {/* Home / root icon */}
      <Link
        href="/"
        className="flex items-center justify-center w-6 h-6 rounded-full text-muted-foreground hover:text-foreground hover:bg-muted/50 transition-colors duration-200 shrink-0"
        title="Home"
      >
        <Home className="w-3.5 h-3.5" />
      </Link>

      <ChevronRight className="w-3 h-3 text-muted-foreground/50 shrink-0" />

      {/* Steps */}
      {showCollapsed ? (
        <>
          {/* First step always visible */}
          <BreadcrumbStep
            step={trail[0]}
            index={0}
            isLast={false}
            onNavigate={navigateTo}
          />

          {/* Collapsed middle */}
          {trail.length > 3 && (
            <span className="text-xs text-muted-foreground/50 px-1">
              &hellip;
            </span>
          )}

          {/* Second-to-last (if 3+ steps) */}
          {trail.length > 2 && (
            <BreadcrumbStep
              step={trail[trail.length - 2]}
              index={trail.length - 2}
              isLast={false}
              onNavigate={navigateTo}
            />
          )}

          {/* Last step */}
          <BreadcrumbStep
            step={trail[trail.length - 1]}
            index={trail.length - 1}
            isLast={true}
            onNavigate={navigateTo}
          />
        </>
      ) : (
        trail.map((step, i) => (
          <BreadcrumbStep
            key={`${step.href}-${i}`}
            step={step}
            index={i}
            isLast={i === trail.length - 1}
            onNavigate={navigateTo}
          />
        ))
      )}

      {/* Clear button */}
      <button
        onClick={clear}
        className="ml-1 flex items-center justify-center w-5 h-5 rounded-full text-muted-foreground/40 hover:text-muted-foreground hover:bg-muted/50 transition-colors duration-200 shrink-0"
        title="Clear trail"
        aria-label="Clear navigation trail"
      >
        <X className="w-3 h-3" />
      </button>
    </nav>
  );
}
