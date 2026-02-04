/**
 * SharedGamesStats Component - Issue #3534
 *
 * Stats cards grid showing:
 * - Total games count
 * - Pending approvals count
 * - PDF coverage percentage
 * - Avg review time
 */

'use client';

import { BookOpen, Clock, FileText, Hourglass } from 'lucide-react';

import { Card, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';

export interface SharedGamesStatsProps {
  totalGames: number;
  pendingApprovals: number;
  pdfCoveragePercent: number;
  avgReviewDays: number;
  isLoading?: boolean;
}

interface StatCardProps {
  icon: React.ElementType;
  label: string;
  value: string | number;
  subLabel?: string;
  colorClass: string;
  isLoading?: boolean;
}

function StatCard({ icon: Icon, label, value, subLabel, colorClass, isLoading }: StatCardProps) {
  if (isLoading) {
    return (
      <Card className="overflow-hidden">
        <CardContent className="p-6">
          <div className="flex items-center gap-4">
            <Skeleton className="h-12 w-12 rounded-full" />
            <div className="space-y-2">
              <Skeleton className="h-4 w-24" />
              <Skeleton className="h-8 w-16" />
            </div>
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className="overflow-hidden transition-shadow hover:shadow-md">
      <CardContent className="p-6">
        <div className="flex items-center gap-4">
          <div className={`flex h-12 w-12 shrink-0 items-center justify-center rounded-full ${colorClass}`}>
            <Icon className="h-6 w-6" />
          </div>
          <div>
            <p className="text-sm font-medium text-muted-foreground">{label}</p>
            <p className="text-2xl font-bold">{value}</p>
            {subLabel && (
              <p className="text-xs text-muted-foreground">{subLabel}</p>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

export function SharedGamesStats({
  totalGames,
  pendingApprovals,
  pdfCoveragePercent,
  avgReviewDays,
  isLoading = false,
}: SharedGamesStatsProps) {
  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
      <StatCard
        icon={BookOpen}
        label="Total Games"
        value={totalGames.toLocaleString()}
        subLabel="in catalog"
        colorClass="bg-primary/10 text-primary"
        isLoading={isLoading}
      />
      <StatCard
        icon={Clock}
        label="Pending Approvals"
        value={pendingApprovals}
        subLabel="awaiting review"
        colorClass="bg-amber-100 text-amber-700 dark:bg-amber-900/20 dark:text-amber-400"
        isLoading={isLoading}
      />
      <StatCard
        icon={FileText}
        label="PDF Coverage"
        value={`${pdfCoveragePercent}%`}
        subLabel="games with PDFs"
        colorClass="bg-blue-100 text-blue-700 dark:bg-blue-900/20 dark:text-blue-400"
        isLoading={isLoading}
      />
      <StatCard
        icon={Hourglass}
        label="Avg Review Time"
        value={`${avgReviewDays}d`}
        subLabel="approval time"
        colorClass="bg-purple-100 text-purple-700 dark:bg-purple-900/20 dark:text-purple-400"
        isLoading={isLoading}
      />
    </div>
  );
}
