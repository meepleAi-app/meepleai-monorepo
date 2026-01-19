/**
 * LedgerTimelineExport Component
 * Issue #2422: Ledger Mode History Timeline
 *
 * Export functionality for timeline history (JSON/CSV formats).
 */

'use client';

import { Download } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import type { GameStateSnapshot } from '@/types/game-state';

interface LedgerTimelineExportProps {
  snapshots: GameStateSnapshot[];
  sessionId: string;
}

/**
 * Export snapshots as JSON file
 */
function exportAsJSON(snapshots: GameStateSnapshot[], sessionId: string) {
  const data = {
    sessionId,
    exportedAt: new Date().toISOString(),
    snapshots,
  };

  const blob = new Blob([JSON.stringify(data, null, 2)], {
    type: 'application/json',
  });

  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `ledger-history-${sessionId}-${Date.now()}.json`;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

/**
 * Export snapshots as CSV file
 */
function exportAsCSV(snapshots: GameStateSnapshot[], sessionId: string) {
  // CSV headers
  const headers = ['Timestamp', 'Action', 'Round', 'Phase', 'Players', 'Snapshot ID'];

  // CSV rows
  const rows = snapshots.map(snapshot => [
    snapshot.timestamp,
    snapshot.action.replace(/,/g, ';'), // Escape commas
    snapshot.state.roundNumber ?? 'N/A',
    snapshot.state.phase ?? 'N/A',
    snapshot.state.players.length,
    snapshot.id,
  ]);

  // Combine headers and rows
  const csvContent = [headers.join(','), ...rows.map(row => row.join(','))].join('\n');

  const blob = new Blob([csvContent], { type: 'text/csv' });
  const url = URL.createObjectURL(blob);
  const link = document.createElement('a');
  link.href = url;
  link.download = `ledger-history-${sessionId}-${Date.now()}.csv`;
  document.body.appendChild(link);
  link.click();
  document.body.removeChild(link);
  URL.revokeObjectURL(url);
}

export function LedgerTimelineExport({ snapshots, sessionId }: LedgerTimelineExportProps) {
  const handleExport = (format: 'json' | 'csv') => {
    if (format === 'json') {
      exportAsJSON(snapshots, sessionId);
    } else {
      exportAsCSV(snapshots, sessionId);
    }
  };

  if (snapshots.length === 0) {
    return null;
  }

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button variant="outline" size="sm" data-testid="export-button">
          <Download className="mr-2 h-4 w-4" />
          Export
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end">
        <DropdownMenuItem onClick={() => handleExport('json')} data-testid="export-json">
          Export as JSON
        </DropdownMenuItem>
        <DropdownMenuItem onClick={() => handleExport('csv')} data-testid="export-csv">
          Export as CSV
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
