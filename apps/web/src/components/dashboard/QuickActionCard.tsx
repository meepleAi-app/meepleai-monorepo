/**
 * QuickActionCard Component (Issue #1834: UI-007)
 *
 * Reusable action card for dashboard quick actions
 *
 * Features:
 * - Icon + title + optional description
 * - Hover animations (translateY + shadow + icon scale)
 * - Keyboard navigation (Enter, Space)
 * - Accessibility (ARIA labels, roles)
 * - CVA variants for consistency
 *
 * @see QuickActions.tsx for usage example
 */

import React from 'react';
import { cva, type VariantProps } from 'class-variance-authority';
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import { LucideIcon } from 'lucide-react';

// ============================================================================
// Variants Configuration (class-variance-authority)
// ============================================================================

const quickActionCardVariants = cva(
  // Base styles
  'group cursor-pointer transition-all duration-200 hover:-translate-y-1 hover:shadow-lg hover:border-primary/20',
  {
    variants: {
      variant: {
        default: 'bg-card hover:bg-muted/50',
        secondary: 'bg-secondary/10 hover:bg-secondary/20',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  }
);

const iconContainerVariants = cva(
  'flex items-center justify-center rounded-full transition-all duration-200 group-hover:scale-110',
  {
    variants: {
      variant: {
        default: 'bg-primary/10 text-primary',
        secondary: 'bg-secondary/10 text-secondary',
      },
    },
    defaultVariants: {
      variant: 'default',
    },
  }
);

// ============================================================================
// Props Interface
// ============================================================================

export interface QuickActionCardProps extends VariantProps<typeof quickActionCardVariants> {
  /** Lucide icon component or custom icon */
  icon: LucideIcon | React.ComponentType<{ className?: string }>;
  /** Action title */
  title: string;
  /** Optional description */
  description?: string;
  /** Click handler */
  onClick: () => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// QuickActionCard Component
// ============================================================================

export const QuickActionCard = React.memo(function QuickActionCard({
  icon: Icon,
  title,
  description,
  onClick,
  variant = 'default',
  className,
}: QuickActionCardProps) {
  const handleKeyDown = (e: React.KeyboardEvent<HTMLDivElement>) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      onClick();
    }
  };

  return (
    <Card
      className={cn(quickActionCardVariants({ variant }), className)}
      onClick={onClick}
      onKeyDown={handleKeyDown}
      role="button"
      tabIndex={0}
      aria-label={description ? `${title}: ${description}` : title}
    >
      <CardContent className="flex items-center gap-4 p-6">
        {/* Icon Container */}
        <div className={cn(iconContainerVariants({ variant }), 'h-12 w-12')}>
          <Icon className="h-6 w-6" aria-hidden="true" />
        </div>

        {/* Text Content */}
        <div className="flex-1 min-w-0">
          <h3 className="font-semibold text-base font-quicksand line-clamp-1">{title}</h3>
          {description && (
            <p className="text-sm text-muted-foreground line-clamp-1 mt-1">{description}</p>
          )}
        </div>
      </CardContent>
    </Card>
  );
});
