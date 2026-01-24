'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';
import { useAuthUser } from '@/hooks/useAuthUser';
import { useAdminShareRequests } from '@/hooks/queries';
import { ShareRequestFilters } from './_components/ShareRequestFilters';
import { ShareRequestsTable } from './_components/ShareRequestsTable';
import { Button } from '@/components/ui';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import type {
  ShareRequestStatus,
  ContributionType,
  ShareRequestSortField,
  SortDirection,
} from '@/lib/api/schemas/admin-share-requests.schemas';

/**
 * Admin Share Requests Queue Page - Client Component
 *
 * Displays paginated list of share requests with filtering and sorting.
 * Admins can review requests by clicking the Review button on each row.
 *
 * Features:
 * - Search by game title or contributor name
 * - Filter by status and contribution type
 * - Sort by multiple fields with direction control
 * - Server-side pagination
 * - Lock status indicators
 * - Waiting time warnings (>7 days)
 *
 * Issue #2745: Frontend - Admin Review Interface
 */

export function AdminShareRequestsClient(){
  const router = useRouter();
  const { user, loading: authLoading } = useAuthUser();

  // Filter state
  const [searchTerm, setSearchTerm] = useState('');
  const [status, setStatus] = useState<ShareRequestStatus | 'all'>('all');
  const [contributionType, setContributionType] = useState<ContributionType | 'all'>('all');
  const [sortBy, setSortBy] = useState<ShareRequestSortField>('CreatedAt');
  const [sortDirection, setSortDirection] = useState<SortDirection>('Descending');

  // Pagination state
  const [page, setPage] = useState(1);
  const pageSize = 20;

  // Fetch data
  const { data, isLoading } = useAdminShareRequests({
    page,
    pageSize,
    searchTerm: searchTerm || undefined,
    status: status !== 'all' ? status : undefined,
    contributionType: contributionType !== 'all' ? contributionType : undefined,
    sortBy,
    sortDirection,
  });

  // Handlers
  const handleReview = (id: string) => {
    router.push(`/admin/share-requests/${id}`);
  };

  const handleSearchChange = (value: string) => {
    setSearchTerm(value);
    setPage(1); // Reset to first page on search
  };

  const handleStatusChange = (value: ShareRequestStatus | 'all') => {
    setStatus(value);
    setPage(1); // Reset to first page on filter change
  };

  const handleContributionTypeChange = (value: ContributionType | 'all') => {
    setContributionType(value);
    setPage(1); // Reset to first page on filter change
  };

  const handleSortByChange = (value: ShareRequestSortField) => {
    setSortBy(value);
    setPage(1); // Reset to first page on sort change
  };

  const handleSortDirectionChange = (value: SortDirection) => {
    setSortDirection(value);
    setPage(1); // Reset to first page on sort change
  };

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto p-6 max-w-7xl space-y-6">
        {/* Header */}
        <div>
          <h1 className="text-3xl font-bold">Share Request Review</h1>
          <p className="text-muted-foreground mt-1">
            {data?.totalCount ?? 0} requests • {data?.items.filter(r => r.status === 'Pending').length ?? 0} pending review
          </p>
        </div>

        {/* Filters */}
        <ShareRequestFilters
          searchTerm={searchTerm}
          status={status}
          contributionType={contributionType}
          sortBy={sortBy}
          sortDirection={sortDirection}
          onSearchChange={handleSearchChange}
          onStatusChange={handleStatusChange}
          onContributionTypeChange={handleContributionTypeChange}
          onSortByChange={handleSortByChange}
          onSortDirectionChange={handleSortDirectionChange}
        />

        {/* Loading State */}
        {isLoading && (
          <div className="text-center py-12">
            <p className="text-muted-foreground">Loading share requests...</p>
          </div>
        )}

        {/* Table */}
        {!isLoading && data && (
          <>
            <ShareRequestsTable requests={data.items} onReview={handleReview} />

            {/* Pagination */}
            {data.totalPages > 1 && (
              <div className="flex items-center justify-between">
                <p className="text-sm text-muted-foreground">
                  Showing {(page - 1) * pageSize + 1} to{' '}
                  {Math.min(page * pageSize, data.totalCount)} of {data.totalCount} requests
                </p>
                <div className="flex items-center gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPage((p) => Math.max(1, p - 1))}
                    disabled={!data.hasPreviousPage}
                  >
                    <ChevronLeft className="h-4 w-4 mr-1" />
                    Previous
                  </Button>
                  <span className="text-sm">
                    Page {page} of {data.totalPages}
                  </span>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPage((p) => Math.min(data.totalPages, p + 1))}
                    disabled={!data.hasNextPage}
                  >
                    Next
                    <ChevronRight className="h-4 w-4 ml-1" />
                  </Button>
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </AdminAuthGuard>
  );
}