/**
 * ShareRequestFilters Component (Issue #2744)
 *
 * Filters for share requests list:
 * - Status multi-select (Pending, InReview, etc.)
 * - Clear filters button
 */

'use client';

import { X, Filter } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Label } from '@/components/ui/primitives/label';
import type { ShareRequestStatus } from '@/lib/api/schemas/share-requests.schemas';

interface ShareRequestFiltersProps {
  // Status filter
  selectedStatuses: ShareRequestStatus[];
  onStatusChange: (statuses: ShareRequestStatus[]) => void;

  // Clear
  onClearFilters: () => void;
}

const statusOptions: { value: ShareRequestStatus; label: string }[] = [
  { value: 'Pending', label: 'Pending' },
  { value: 'InReview', label: 'In Review' },
  { value: 'ChangesRequested', label: 'Changes Requested' },
  { value: 'Approved', label: 'Approved' },
  { value: 'Rejected', label: 'Rejected' },
  { value: 'Withdrawn', label: 'Withdrawn' },
];

export function ShareRequestFilters({
  selectedStatuses,
  onStatusChange,
  onClearFilters,
}: ShareRequestFiltersProps) {
  // Status toggle
  const handleStatusToggle = (status: ShareRequestStatus) => {
    if (selectedStatuses.includes(status)) {
      onStatusChange(selectedStatuses.filter((s) => s !== status));
    } else {
      onStatusChange([...selectedStatuses, status]);
    }
  };

  // Clear all status filters
  const handleClearStatuses = () => {
    onStatusChange([]);
  };

  // Count active filters
  const activeFiltersCount = selectedStatuses.length;

  return (
    <Card className="sticky top-4">
      <CardHeader className="pb-4">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Filter className="h-5 w-5" />
            <CardTitle className="text-lg">Filters</CardTitle>
          </div>
          {activeFiltersCount > 0 && (
            <Badge variant="secondary" className="text-xs">
              {activeFiltersCount}
            </Badge>
          )}
        </div>
      </CardHeader>

      <CardContent className="space-y-6">
        {/* Status Multi-Select */}
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <Label className="text-sm font-medium">Status</Label>
            {selectedStatuses.length > 0 && (
              <Button variant="ghost" size="sm" onClick={handleClearStatuses} className="h-auto p-0 text-xs">
                Clear
              </Button>
            )}
          </div>
          <div className="space-y-2">
            {statusOptions.map((option) => (
              <div key={option.value} className="flex items-center gap-2">
                <Checkbox
                  id={`status-${option.value}`}
                  checked={selectedStatuses.includes(option.value)}
                  onCheckedChange={() => handleStatusToggle(option.value)}
                />
                <Label
                  htmlFor={`status-${option.value}`}
                  className="text-sm font-normal cursor-pointer"
                >
                  {option.label}
                </Label>
              </div>
            ))}
          </div>
        </div>

        {/* Clear All Filters */}
        {activeFiltersCount > 0 && (
          <Button variant="outline" onClick={onClearFilters} className="w-full">
            <X className="mr-2 h-4 w-4" />
            Clear All Filters
          </Button>
        )}
      </CardContent>
    </Card>
  );
}
