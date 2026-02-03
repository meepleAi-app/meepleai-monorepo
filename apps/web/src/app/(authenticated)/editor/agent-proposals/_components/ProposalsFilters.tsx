'use client';

/**
 * ProposalsFilters Component (Issue #3182)
 *
 * Filter controls for proposals list:
 * - Status filter (tabs/buttons)
 * - Search input
 * - Result count
 */

import { Search } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';

type StatusFilter = 'all' | 'Draft' | 'PendingReview' | 'Approved' | 'Rejected';

interface ProposalsFiltersProps {
  statusFilter: StatusFilter;
  onStatusFilterChange: (status: StatusFilter) => void;
  searchQuery: string;
  onSearchQueryChange: (query: string) => void;
  totalCount: number;
  filteredCount: number;
}

export function ProposalsFilters({
  statusFilter,
  onStatusFilterChange,
  searchQuery,
  onSearchQueryChange,
  totalCount,
  filteredCount,
}: ProposalsFiltersProps) {
  const statusOptions: { value: StatusFilter; label: string }[] = [
    { value: 'all', label: 'All' },
    { value: 'Draft', label: 'Draft' },
    { value: 'PendingReview', label: 'Pending' },
    { value: 'Approved', label: 'Approved' },
    { value: 'Rejected', label: 'Rejected' },
  ];

  return (
    <div className="space-y-4">
      {/* Status filter buttons */}
      <div className="flex items-center gap-2 flex-wrap">
        <span className="text-sm font-medium text-muted-foreground">Status:</span>
        {statusOptions.map((option) => (
          <Button
            key={option.value}
            variant={statusFilter === option.value ? 'default' : 'outline'}
            size="sm"
            onClick={() => onStatusFilterChange(option.value)}
          >
            {option.label}
          </Button>
        ))}
      </div>

      {/* Search input */}
      <div className="flex items-center gap-4">
        <div className="relative flex-1 max-w-sm">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            type="search"
            placeholder="Search proposals..."
            value={searchQuery}
            onChange={(e) => onSearchQueryChange(e.target.value)}
            className="pl-9"
          />
        </div>

        {/* Result count */}
        <div className="text-sm text-muted-foreground">
          Showing {filteredCount} of {totalCount} proposals
        </div>
      </div>
    </div>
  );
}
