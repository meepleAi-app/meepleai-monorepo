'use client';

import { useState } from 'react';

import { ActivityFilters } from '@/components/admin/users/activity-filters';
import { ActivityTable } from '@/components/admin/users/activity-table';

export default function UserActivityPage() {
  const [actionFilter, setActionFilter] = useState('all');
  const [dateRange, setDateRange] = useState('24h');

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          User Activity Log
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Monitor user actions and system events
        </p>
      </div>

      {/* Filters */}
      <ActivityFilters
        onActionTypeChange={setActionFilter}
        onDateRangeChange={setDateRange}
      />

      {/* Activity Table */}
      <ActivityTable actionFilter={actionFilter} dateRange={dateRange} />
    </div>
  );
}
