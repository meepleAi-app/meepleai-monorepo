/**
 * Player Statistics Page
 *
 * Cross-game statistics dashboard with charts and visualizations.
 * Issue #3892: Play Records Frontend UI
 */

'use client';

import { ArrowLeft } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { PlayerStatistics } from '@/components/play-records/PlayerStatistics';
import { Button } from '@/components/ui/primitives/button';

export default function StatisticsPage() {
  const router = useRouter();

  return (
    <div className="container mx-auto p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center gap-4">
        <Button
          variant="ghost"
          size="icon"
          onClick={() => router.push('/play-records')}
          aria-label="Back to history"
        >
          <ArrowLeft className="w-5 h-5" />
        </Button>
        <div>
          <h1 className="text-3xl font-bold">Player Statistics</h1>
          <p className="text-muted-foreground mt-1">
            Your performance across all games
          </p>
        </div>
      </div>

      {/* Statistics Dashboard */}
      <PlayerStatistics />
    </div>
  );
}
