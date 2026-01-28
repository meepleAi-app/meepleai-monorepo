/**
 * SessionQuotaSection Component (Issue #3075)
 *
 * Dashboard widget showing session quota status.
 * Displays current active sessions and available slots with link to sessions page.
 *
 * Features:
 * - Real-time quota status
 * - Visual indicator when near/at limit
 * - Link to sessions management
 * - Skeleton loading state
 * - Error handling
 * - Unlimited sessions display for admins
 */

'use client';

import React from 'react';

import { AlertCircle, ChevronRight, Gamepad2, Infinity as InfinityIcon } from 'lucide-react';
import Link from 'next/link';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useSessionQuotaWithStatus } from '@/hooks/queries/useSessionQuota';
import { cn } from '@/lib/utils';

export function SessionQuotaSection() {
  const { data: quota, isLoading, error } = useSessionQuotaWithStatus();

  // Loading state
  if (isLoading) {
    return (
      <Card data-testid="session-quota-card">
        <CardHeader>
          <div className="flex items-center gap-2">
            <Gamepad2 className="h-5 w-5 text-muted-foreground" />
            <CardTitle data-testid="session-quota-title">Sessioni Attive</CardTitle>
          </div>
        </CardHeader>
        <CardContent className="space-y-2" data-testid="session-quota-loading">
          <Skeleton className="h-6 w-24" data-testid="session-quota-skeleton-count" />
          <Skeleton className="h-4 w-32" data-testid="session-quota-skeleton-status" />
        </CardContent>
      </Card>
    );
  }

  // Error state
  if (error || !quota) {
    return (
      <Card data-testid="session-quota-card">
        <CardHeader>
          <div className="flex items-center gap-2">
            <Gamepad2 className="h-5 w-5 text-muted-foreground" />
            <CardTitle data-testid="session-quota-title">Sessioni Attive</CardTitle>
          </div>
        </CardHeader>
        <CardContent>
          <Alert variant="destructive" className="py-2" data-testid="session-quota-error">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription className="text-xs" data-testid="session-quota-error-message">
              Impossibile caricare la quota sessioni. Riprova più tardi.
            </AlertDescription>
          </Alert>
        </CardContent>
      </Card>
    );
  }

  const isNearLimit = quota.warningLevel === 'warning' || quota.warningLevel === 'critical';
  const isAtLimit = quota.warningLevel === 'full';

  return (
    <Link
      href="/games"
      className="block transition-transform hover:scale-[1.02]"
      data-testid="session-quota-link"
    >
      <Card
        className={cn(
          'cursor-pointer',
          isAtLimit && 'border-destructive/50',
          isNearLimit && !isAtLimit && 'border-yellow-500/50'
        )}
        data-testid="session-quota-card"
      >
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <Gamepad2 className="h-5 w-5 text-muted-foreground" />
              <CardTitle data-testid="session-quota-title">Sessioni Attive</CardTitle>
            </div>
            <ChevronRight className="h-4 w-4 text-muted-foreground" />
          </div>
        </CardHeader>
        <CardContent className="space-y-1">
          <p className="text-2xl font-bold" data-testid="session-quota-count">
            <span data-testid="session-quota-count-number">{quota.currentSessions}</span>{' '}
            <span
              className="text-base font-normal text-muted-foreground"
              data-testid="session-quota-count-label"
            >
              {quota.currentSessions === 1 ? 'sessione' : 'sessioni'}
            </span>
          </p>

          {quota.isUnlimited ? (
            <p
              className="flex items-center gap-1 text-sm text-muted-foreground"
              data-testid="session-quota-unlimited"
            >
              <InfinityIcon className="h-3 w-3" />
              Sessioni illimitate
            </p>
          ) : (
            <p
              className={cn(
                'text-sm',
                isAtLimit && 'font-medium text-destructive',
                isNearLimit && !isAtLimit && 'font-medium text-yellow-600 dark:text-yellow-400',
                !isNearLimit && !isAtLimit && 'text-muted-foreground'
              )}
              data-testid="session-quota-status"
            >
              {quota.remainingSlots === 0
                ? 'Limite raggiunto'
                : quota.remainingSlots === 1
                  ? '1 slot disponibile'
                  : `${quota.remainingSlots} slot disponibili`}
            </p>
          )}
        </CardContent>
      </Card>
    </Link>
  );
}
