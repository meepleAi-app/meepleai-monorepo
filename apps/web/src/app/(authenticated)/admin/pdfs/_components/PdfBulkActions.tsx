'use client';

import { Trash2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

interface PdfBulkActionsProps {
  selectedCount: number;
  onBulkDelete: () => void;
  isDeleting: boolean;
}

export function PdfBulkActions({ selectedCount, onBulkDelete, isDeleting }: PdfBulkActionsProps) {
  if (selectedCount === 0) return null;

  return (
    <div className="sticky bottom-4 z-10 flex items-center justify-between rounded-lg border bg-card p-3 shadow-lg">
      <span className="text-sm font-medium">
        {selectedCount} document{selectedCount !== 1 ? 's' : ''} selected
      </span>
      <Button
        variant="destructive"
        size="sm"
        onClick={onBulkDelete}
        disabled={isDeleting}
      >
        <Trash2 className="h-4 w-4 mr-2" />
        {isDeleting ? 'Deleting...' : 'Delete Selected'}
      </Button>
    </div>
  );
}
