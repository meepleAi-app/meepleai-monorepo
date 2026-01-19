'use client';

/**
 * Games Table Pagination Component (Issue #2515)
 *
 * Server-side pagination controls for games table.
 * Displays: [Prev] [1] [2] [3] ... [Next] + "Page X of Y" info
 */

import { ChevronLeft, ChevronRight } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

interface GamesTablePaginationProps {
  page: number;
  pageSize: number;
  total: number;
  totalPages: number;
  onPageChange: (page: number) => void;
}

export function GamesTablePagination({
  page,
  pageSize,
  total,
  totalPages,
  onPageChange,
}: GamesTablePaginationProps) {
  const canPrevPage = page > 1;
  const canNextPage = page < totalPages;

  const handlePrevPage = () => {
    if (canPrevPage) {
      onPageChange(page - 1);
    }
  };

  const handleNextPage = () => {
    if (canNextPage) {
      onPageChange(page + 1);
    }
  };

  const startItem = (page - 1) * pageSize + 1;
  const endItem = Math.min(page * pageSize, total);

  return (
    <div className="flex items-center justify-between">
      <div className="text-sm text-muted-foreground">
        Showing {startItem} to {endItem} of {total} games
      </div>

      <div className="flex items-center gap-2">
        <Button variant="outline" size="sm" onClick={handlePrevPage} disabled={!canPrevPage}>
          <ChevronLeft className="h-4 w-4 mr-1" />
          Previous
        </Button>

        <span className="text-sm text-muted-foreground">
          Page {page} of {totalPages}
        </span>

        <Button variant="outline" size="sm" onClick={handleNextPage} disabled={!canNextPage}>
          Next
          <ChevronRight className="h-4 w-4 ml-1" />
        </Button>
      </div>
    </div>
  );
}
