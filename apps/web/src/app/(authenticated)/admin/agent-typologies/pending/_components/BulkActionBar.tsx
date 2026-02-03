'use client';

/**
 * Bulk Action Bar Component (Issue #3181)
 *
 * Fixed bottom bar for bulk typology operations.
 * Displays when items are selected, with:
 * - Selected count
 * - Approve All (green)
 * - Reject All (red)
 * - Clear Selection
 *
 * Part of Epic #3174 (AI Agent System).
 */

import { CheckCircle, X, XCircle } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

interface BulkActionBarProps {
  selectedCount: number;
  onApproveAll: () => void;
  onRejectAll: () => void;
  onClearSelection: () => void;
}

export function BulkActionBar({
  selectedCount,
  onApproveAll,
  onRejectAll,
  onClearSelection,
}: BulkActionBarProps) {
  return (
    <div className="fixed bottom-0 left-0 right-0 bg-background border-t shadow-lg z-50">
      <div className="container mx-auto p-4 max-w-7xl">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="font-medium text-sm">
              {selectedCount} {selectedCount === 1 ? 'item' : 'items'} selected
            </span>
          </div>

          <div className="flex items-center gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={onClearSelection}
            >
              <X className="h-4 w-4 mr-1" />
              Clear Selection
            </Button>
            <Button
              variant="default"
              size="sm"
              onClick={onApproveAll}
              className="bg-green-600 hover:bg-green-700"
            >
              <CheckCircle className="h-4 w-4 mr-1" />
              Approve All ({selectedCount})
            </Button>
            <Button
              variant="destructive"
              size="sm"
              onClick={onRejectAll}
            >
              <XCircle className="h-4 w-4 mr-1" />
              Reject All ({selectedCount})
            </Button>
          </div>
        </div>
      </div>
    </div>
  );
}
