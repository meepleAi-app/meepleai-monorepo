/**
 * PricingCard - Tier Pricing Display Card (Game Night Improvvisata - E2-5)
 *
 * Glassmorphic card showing tier name, price, features, and CTA button.
 */

'use client';

import React from 'react';

import { Check, X } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface PricingFeature {
  /** Feature description */
  label: string;
  /** Whether this feature is included in the tier */
  included: boolean;
}

export interface PricingCardProps {
  /** Tier name */
  name: string;
  /** Price display string (e.g., "Gratis", "4.99/mese") */
  price: string;
  /** Features list with inclusion status */
  features: PricingFeature[];
  /** Whether this is the user's current tier */
  isCurrent?: boolean;
  /** Whether this tier is highlighted as popular */
  isPopular?: boolean;
  /** CTA button label */
  ctaLabel?: string;
  /** Whether CTA is disabled (e.g., "Coming Soon") */
  ctaDisabled?: boolean;
  /** CTA click handler */
  onCtaClick?: () => void;
  /** Optional extra CSS class */
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export const PricingCard = React.memo(function PricingCard({
  name,
  price,
  features,
  isCurrent = false,
  isPopular = false,
  ctaLabel,
  ctaDisabled = false,
  onCtaClick,
  className,
}: PricingCardProps) {
  const highlighted = isCurrent || isPopular;

  return (
    <Card
      className={cn(
        'bg-white/70 backdrop-blur-md relative flex flex-col',
        highlighted && 'border-amber-400 ring-2 ring-amber-200',
        className
      )}
      data-testid="pricing-card"
    >
      {/* Badges */}
      <div className="absolute -top-3 left-1/2 -translate-x-1/2 flex gap-2">
        {isCurrent && (
          <Badge variant="default" data-testid="current-badge">
            Piano Attuale
          </Badge>
        )}
        {isPopular && !isCurrent && (
          <Badge variant="secondary" className="bg-amber-100 text-amber-900 border-amber-300">
            Consigliato
          </Badge>
        )}
      </div>

      <CardHeader className="text-center pt-8 pb-2">
        <CardTitle className="text-xl font-quicksand">{name}</CardTitle>
        <p className="text-3xl font-bold text-foreground font-quicksand mt-2">{price}</p>
      </CardHeader>

      <CardContent className="flex-1 px-6 py-4">
        <ul className="space-y-3">
          {features.map(feature => (
            <li key={feature.label} className="flex items-start gap-2.5 text-sm font-nunito">
              {feature.included ? (
                <Check className="h-4 w-4 text-emerald-500 shrink-0 mt-0.5" aria-hidden="true" />
              ) : (
                <X
                  className="h-4 w-4 text-muted-foreground/50 shrink-0 mt-0.5"
                  aria-hidden="true"
                />
              )}
              <span className={cn(!feature.included && 'text-muted-foreground/60')}>
                {feature.label}
              </span>
            </li>
          ))}
        </ul>
      </CardContent>

      <CardFooter className="px-6 pb-6">
        <Button
          className="w-full"
          variant={highlighted ? 'default' : 'outline'}
          disabled={ctaDisabled}
          onClick={onCtaClick}
          data-testid="pricing-cta"
        >
          {ctaLabel}
        </Button>
      </CardFooter>
    </Card>
  );
});

PricingCard.displayName = 'PricingCard';
