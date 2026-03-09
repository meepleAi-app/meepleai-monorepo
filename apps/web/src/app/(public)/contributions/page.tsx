/**
 * User Contributions Dashboard Page (Issue #2744)
 *
 * Client Component for viewing user's share requests, contributions, and badges.
 * Displays personal contribution history with status tracking and filtering.
 *
 * Route: /contributions
 * Features:
 * - Share requests list with status badges
 * - Advanced status filtering
 * - Pagination
 * - Empty states for new contributors
 * - Quick actions (view details, update, view game)
 *
 * @see Issue #2744 Frontend - Dashboard Contributi Utente
 */

'use client';

import { useState, useMemo } from 'react';

import { AlertCircle, FileText, TrendingUp } from 'lucide-react';
import Link from 'next/link';

import { CatalogPagination } from '@/components/catalog/CatalogPagination';
import { ShareRequestCard } from '@/components/contributions/ShareRequestCard';
import { ShareRequestFilters } from '@/components/contributions/ShareRequestFilters';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useShareRequests } from '@/hooks/queries/useShareRequests';
import type { GetUserShareRequestsParams } from '@/lib/api/clients/shareRequestsClient';
import type { ShareRequestStatus } from '@/lib/api/schemas/share-requests.schemas';

export default function ContributionsPage() {
  // Filter and pagination state
  const [page, setPage] = useState(1);
  const [pageSize] = useState(12);
  const [selectedStatuses, setSelectedStatuses] = useState<ShareRequestStatus[]>([]);

  // Build query params
  const queryParams = useMemo<GetUserShareRequestsParams>(() => {
    const params: GetUserShareRequestsParams = {
      page,
      pageSize,
    };

    // Status filter
    if (selectedStatuses.length > 0) {
      params.status = selectedStatuses.join(',');
    }

    return params;
  }, [page, pageSize, selectedStatuses]);

  // Fetch share requests
  const { data, isLoading, error } = useShareRequests(queryParams);

  const requests = data?.items || [];
  const totalCount = data?.total || 0;
  const totalPages = data ? Math.ceil(data.total / data.pageSize) : 0;

  // Reset to page 1 when filters change
  const handleFilterChange = () => {
    if (page !== 1) setPage(1);
  };

  // Filter handlers
  const handleStatusChange = (statuses: ShareRequestStatus[]) => {
    setSelectedStatuses(statuses);
    handleFilterChange();
  };

  const handleClearFilters = () => {
    setSelectedStatuses([]);
    if (page !== 1) setPage(1);
  };

  return (
    <div className="container mx-auto px-4 py-8">
      {/* Page Header */}
      <div className="mb-8">
        <h1 className="text-4xl font-bold font-quicksand mb-2">My Contributions</h1>
        <p className="text-lg text-muted-foreground">
          Track your share requests and contribution history
        </p>
      </div>

      {/* Layout: Sidebar + Main Content */}
      <div className="flex flex-col lg:flex-row gap-6">
        {/* Sidebar: Filters */}
        <aside className="lg:w-64 shrink-0">
          <ShareRequestFilters
            selectedStatuses={selectedStatuses}
            onStatusChange={handleStatusChange}
            onClearFilters={handleClearFilters}
          />
        </aside>

        {/* Main Content */}
        <main className="flex-1">
          {/* Error State */}
          {error && (
            <Alert variant="destructive" className="mb-6">
              <AlertCircle className="h-4 w-4" />
              <AlertTitle>Error Loading Contributions</AlertTitle>
              <AlertDescription>
                {error instanceof Error ? error.message : 'Failed to load share requests'}
              </AlertDescription>
            </Alert>
          )}

          {/* Loading State */}
          {isLoading && (
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6">
              {Array.from({ length: 6 }).map((_, i) => (
                <Skeleton key={i} className="h-80 w-full rounded-2xl" />
              ))}
            </div>
          )}

          {/* Empty State */}
          {!isLoading && !error && requests.length === 0 && (
            <div className="flex flex-col items-center justify-center py-16 text-center">
              <div className="rounded-full bg-primary/10 p-6 mb-6">
                <FileText className="h-16 w-16 text-primary" />
              </div>
              <h3 className="text-2xl font-semibold font-quicksand mb-2">No Share Requests Yet</h3>
              <p className="text-muted-foreground mb-6 max-w-md">
                {selectedStatuses.length > 0
                  ? 'No share requests match your current filters. Try adjusting your filters or clearing them.'
                  : 'Share games from your library to start contributing to the MeepleAI community!'}
              </p>
              {selectedStatuses.length === 0 && (
                <Button asChild size="lg">
                  <Link href="/library">
                    <TrendingUp className="mr-2 h-5 w-5" />
                    Go to My Library
                  </Link>
                </Button>
              )}
              {selectedStatuses.length > 0 && (
                <Button onClick={handleClearFilters} variant="outline">
                  Clear Filters
                </Button>
              )}
            </div>
          )}

          {/* Share Requests Grid */}
          {!isLoading && !error && requests.length > 0 && (
            <>
              {/* Results Count */}
              <div className="mb-4 flex items-center justify-between">
                <p className="text-sm text-muted-foreground">
                  Showing {requests.length} of {totalCount} share requests
                </p>
              </div>

              {/* Grid */}
              <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6 mb-8">
                {requests.map(request => (
                  <ShareRequestCard key={request.id} request={request} />
                ))}
              </div>

              {/* Pagination */}
              {totalPages > 1 && (
                <div className="flex justify-center">
                  <CatalogPagination
                    currentPage={page}
                    totalPages={totalPages}
                    onPageChange={setPage}
                  />
                </div>
              )}
            </>
          )}
        </main>
      </div>
    </div>
  );
}
