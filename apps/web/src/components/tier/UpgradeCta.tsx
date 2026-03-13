/**
 * UpgradeCta - Upgrade Call-to-Action Card (Game Night Improvvisata - E2-4)
 *
 * Glassmorphic card shown when user hits a tier limit.
 * Links to /pricing for upgrade.
 */

'use client';

import React from 'react';

import { ArrowRight, Sparkles } from 'lucide-react';
import Link from 'next/link';

import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface UpgradeCtaProps {
  /** Which limit was reached (e.g., "giochi privati") */
  limitType: string;
  /** Current usage count */
  current: number;
  /** Maximum allowed count */
  max: number;
  /** Optional extra CSS class */
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export const UpgradeCta = React.memo(function UpgradeCta({
  limitType,
  current,
  max,
  className,
}: UpgradeCtaProps) {
  return (
    <Card
      className={cn('bg-white/70 backdrop-blur-md border-amber-200', className)}
      data-testid="upgrade-cta"
    >
      <CardContent className="p-4">
        <div className="flex items-start gap-3">
          <div className="rounded-full bg-amber-100 p-2 shrink-0">
            <Sparkles className="h-4 w-4 text-amber-700" aria-hidden="true" />
          </div>
          <div className="space-y-2 min-w-0">
            <p className="text-sm font-semibold text-foreground font-quicksand">
              Hai raggiunto il limite
            </p>
            <p className="text-xs text-muted-foreground font-nunito">
              {current}/{max} {limitType} utilizzati. Passa a Premium per aumentare i limiti.
            </p>
            <Button asChild size="sm" className="gap-1.5">
              <Link href="/pricing">
                Upgrade a Premium
                <ArrowRight className="h-3.5 w-3.5" aria-hidden="true" />
              </Link>
            </Button>
          </div>
        </div>
      </CardContent>
    </Card>
  );
});

UpgradeCta.displayName = 'UpgradeCta';
