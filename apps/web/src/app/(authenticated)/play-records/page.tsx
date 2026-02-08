/**
 * Play Records History Page
 *
 * Displays user's play history with filtering and search.
 * Issue #3892: Play Records Frontend UI
 */

'use client';

import Link from 'next/link';
import { Plus } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { PlayHistory } from '@/components/play-records/PlayHistory';

export default function PlayRecordsPage() {
  return (
    <div className="container mx-auto p-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-3xl font-bold">Play History</h1>
          <p className="text-muted-foreground mt-1">
            Track and review your game sessions
          </p>
        </div>
        <Button asChild>
          <Link href="/play-records/new">
            <Plus className="w-4 h-4 mr-2" />
            New Session
          </Link>
        </Button>
      </div>

      {/* Play History Component */}
      <PlayHistory />
    </div>
  );
}
