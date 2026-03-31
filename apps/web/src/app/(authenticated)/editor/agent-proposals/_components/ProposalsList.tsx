'use client';

/**
 * ProposalsList Component (Issue #3182)
 *
 * Displays editor's typology proposals with filters and status badges.
 * Features:
 * - Status filters (Draft/Pending/Approved/Rejected)
 * - Action buttons (Edit/Test/Submit/View)
 * - Status-based UI (badges, rejection reasons)
 */

import { useState } from 'react';

import { useQuery } from '@tanstack/react-query';

import { agentTypologiesApi } from '@/lib/api/agent-typologies.api';

import { ProposalsFilters } from './ProposalsFilters';
import { ProposalsTable } from './ProposalsTable';

type StatusFilter = 'all' | 'Draft' | 'PendingReview' | 'Approved' | 'Rejected';

export function ProposalsList() {
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [searchQuery, setSearchQuery] = useState('');

  // Fetch proposals
  const {
    data: proposals = [],
    isLoading,
    error,
  } = useQuery({
    queryKey: ['editor-proposals'],
    queryFn: () => agentTypologiesApi.getMyProposals(),
  });

  // Filter proposals
  const filteredProposals = proposals.filter(proposal => {
    // status is now a number; 'all' passes everything, else filter by isActive
    const matchesStatus =
      statusFilter === 'all' ||
      (statusFilter === 'Approved' && proposal.isActive) ||
      (statusFilter === 'Draft' && !proposal.isActive);
    const matchesSearch =
      !searchQuery ||
      proposal.name.toLowerCase().includes(searchQuery.toLowerCase()) ||
      proposal.description.toLowerCase().includes(searchQuery.toLowerCase());

    return matchesStatus && matchesSearch;
  });

  // Loading state
  if (isLoading) {
    return (
      <div className="space-y-4">
        <div className="h-12 bg-muted animate-pulse rounded" />
        <div className="h-64 bg-muted animate-pulse rounded" />
      </div>
    );
  }

  // Error state
  if (error) {
    return (
      <div className="border border-destructive rounded-lg p-6 text-center">
        <p className="text-destructive font-medium">Failed to load proposals</p>
        <p className="text-sm text-muted-foreground mt-2">
          {error instanceof Error ? error.message : 'Unknown error'}
        </p>
      </div>
    );
  }

  // Empty state
  if (proposals.length === 0) {
    return (
      <div className="border border-dashed rounded-lg p-12 text-center">
        <p className="text-lg font-medium mb-2">No proposals yet</p>
        <p className="text-muted-foreground mb-4">
          Create your first agent typology proposal to get started
        </p>
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <ProposalsFilters
        statusFilter={statusFilter}
        onStatusFilterChange={setStatusFilter}
        searchQuery={searchQuery}
        onSearchQueryChange={setSearchQuery}
        totalCount={proposals.length}
        filteredCount={filteredProposals.length}
      />

      <ProposalsTable proposals={filteredProposals} />
    </div>
  );
}
