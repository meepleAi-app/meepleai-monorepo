/**
 * LibraryQuotaSection Component (Issue #2445)
 *
 * Dashboard widget showing library quota status.
 * Displays current game count and available slots with link to full library.
 *
 * Features:
 * - Real-time quota status
 * - Visual indicator when near/at limit
 * - Link to full library page
 * - Skeleton loading state
 * - Error handling
 */

'use client';

import React from 'react';

import { AlertCircle, BookOpen, ChevronRight } from 'lucide-react';
import Link from 'next/link';

import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useLibraryQuota } from '@/hooks/queries/useLibrary';
import { cn } from '@/lib/utils';

export function LibraryQuotaSection() {
  const { data: quota, isLoading, error } = useLibraryQuota();

  // Loading state
  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <BookOpen className="h-5 w-5 text-muted-foreground" />
            <CardTitle>La Mia Libreria</CardTitle>
          </div>
        </CardHeader>
        <CardContent className="space-y-2">
          <Skeleton className="h-6 w-24" />
          <Skeleton className="h-4 w-32" />
        </CardContent>
      </Card>
    );
  }

  // Error state
  if (error || !quota) {
    return (
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <BookOpen className="h-5 w-5 text-muted-foreground" />
            <CardTitle>La Mia Libreria</CardTitle>
          </div>
        </CardHeader>
        <CardContent>
          <Alert variant="destructive" className="py-2">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription className="text-xs">
              Impossibile caricare il quota. Riprova più tardi.
            </AlertDescription>
          </Alert>
        </CardContent>
      </Card>
    );
  }

  const isNearLimit = quota.percentageUsed >= 80;
  const isAtLimit = quota.currentCount >= quota.maxAllowed;

  return (
    <Link href="/library" className="block transition-transform hover:scale-[1.02]">
      <Card
        className={cn(
          'cursor-pointer',
          isAtLimit && 'border-destructive/50',
          isNearLimit && !isAtLimit && 'border-yellow-500/50'
        )}
      >
        <CardHeader>
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <BookOpen className="h-5 w-5 text-muted-foreground" />
              <CardTitle>La Mia Libreria</CardTitle>
            </div>
            <ChevronRight className="h-4 w-4 text-muted-foreground" />
          </div>
        </CardHeader>
        <CardContent className="space-y-1">
          <p className="text-2xl font-bold">
            {quota.currentCount}{' '}
            <span className="text-base font-normal text-muted-foreground">
              {quota.currentCount === 1 ? 'gioco' : 'giochi'}
            </span>
          </p>
          <p
            className={cn(
              'text-sm',
              isAtLimit && 'text-destructive font-medium',
              isNearLimit && !isAtLimit && 'text-yellow-600 dark:text-yellow-400 font-medium',
              !isNearLimit && 'text-muted-foreground'
            )}
          >
            {quota.remainingSlots === 0
              ? 'Limite raggiunto'
              : quota.remainingSlots === 1
                ? '1 slot disponibile'
                : `${quota.remainingSlots} slot disponibili`}
          </p>
        </CardContent>
      </Card>
    </Link>
  );
}
