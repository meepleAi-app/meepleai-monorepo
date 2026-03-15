'use client';

import { useCallback, useState } from 'react';

import { Download } from 'lucide-react';

import { ActivityFilters } from '@/components/admin/users/activity-filters';
import { ActivityTable, getDateRange } from '@/components/admin/users/activity-table';
import { Button } from '@/components/ui/button';
import { api } from '@/lib/api';

export default function UserActivityPage() {
  const [actionFilter, setActionFilter] = useState('all');
  const [dateRange, setDateRange] = useState('24h');
  const [page, setPage] = useState(0);
  const [totalCount, setTotalCount] = useState(0);
  const [exporting, setExporting] = useState(false);

  // Reset page when filters change
  const handleActionChange = (value: string) => {
    setActionFilter(value);
    setPage(0);
  };
  const handleDateChange = (value: string) => {
    setDateRange(value);
    setPage(0);
  };

  const handleTotalCountChange = useCallback((count: number) => {
    setTotalCount(count);
  }, []);

  const handleExport = async () => {
    setExporting(true);
    try {
      const blob = await api.admin.exportAuditLogs({
        action: actionFilter !== 'all' ? actionFilter : undefined,
        ...getDateRange(dateRange),
      });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `audit-log-${new Date().toISOString().slice(0, 10)}.csv`;
      a.click();
      URL.revokeObjectURL(url);
    } catch {
      // export failed silently
    } finally {
      setExporting(false);
    }
  };

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Audit Log
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            {totalCount > 0
              ? `${totalCount.toLocaleString()} entries recorded`
              : 'Monitor user actions and system events'}
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={handleExport} disabled={exporting}>
          <Download className="mr-2 h-4 w-4" aria-hidden="true" />
          {exporting ? 'Exporting...' : 'Export CSV'}
        </Button>
      </div>

      {/* Filters */}
      <ActivityFilters
        onActionTypeChange={handleActionChange}
        onDateRangeChange={handleDateChange}
      />

      {/* Activity Table */}
      <ActivityTable
        actionFilter={actionFilter}
        dateRange={dateRange}
        page={page}
        onPageChange={setPage}
        onTotalCountChange={handleTotalCountChange}
      />
    </div>
  );
}
