'use client';

/**
 * Typology Table Component (Issue #3179)
 *
 * Main table component for displaying agent typologies.
 * Integrates shadcn/ui DataTable with custom columns and actions.
 * Part of Epic #3174 (AI Agent System).
 */

import * as React from 'react';
import { useState, useEffect, useCallback } from 'react';

import { AlertCircle } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { Spinner } from '@/components/loading/Spinner';
import {
  DataTable,
  type SortingState,
} from '@/components/ui/data-display/data-table';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import type { AgentTypology } from '@/lib/api/clients/adminClient';
import { useApiClient } from '@/lib/api/context';

import {
  createTypologyColumns,
} from './typology-columns';
import { TypologyFilters } from './TypologyFilters';

export function TypologyTable() {
  const { admin } = useApiClient();
  const router = useRouter();

  // State
  const [typologies, setTypologies] = useState<AgentTypology[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [status, setStatus] = useState<string>('All');
  const [createdBy, setCreatedBy] = useState<string>('All');
  const [search, setSearch] = useState<string>('');

  // Sorting
  const [sorting, setSorting] = useState<SortingState>([
    { id: 'createdAt', desc: true },
  ]);

  // Pagination
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [total, setTotal] = useState(0);

  // Fetch typologies
  const fetchTypologies = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);

      const result = await admin.getAgentTypologies({
        status: status !== 'All' ? status : undefined,
        createdBy: createdBy !== 'All' ? createdBy : undefined,
        search: search || undefined,
        page,
        pageSize,
      });

      setTypologies(result.typologies);
      setTotal(result.total);
    } catch (err) {
      const errorMessage = err instanceof Error ? err.message : 'Failed to load typologies';
      setError(errorMessage);
      toast.error(errorMessage);
    } finally {
      setLoading(false);
    }
  }, [admin, status, createdBy, search, page, pageSize]);

  // Fetch on mount and filter changes
  useEffect(() => {
    fetchTypologies();
  }, [fetchTypologies]);

  // Reset page on filter change
  useEffect(() => {
    setPage(1);
  }, [status, createdBy, search]);

  // Action handlers
  const handleEdit = useCallback(
    (typology: AgentTypology) => {
      router.push(`/admin/agent-typologies/${typology.id}/edit`);
    },
    [router]
  );

  const handleApprove = useCallback(
    async (typology: AgentTypology) => {
      try {
        await admin.approveAgentTypology(typology.id);
        toast.success(`Approved typology: ${typology.name}`);
        fetchTypologies(); // Refresh list
      } catch (err) {
        const errorMessage = err instanceof Error ? err.message : 'Failed to approve typology';
        toast.error(errorMessage);
      }
    },
    [admin, fetchTypologies]
  );

  const handleDelete = useCallback(
    async (typology: AgentTypology) => {
      if (!confirm(`Delete typology "${typology.name}"? This action cannot be undone.`)) {
        return;
      }

      try {
        await admin.deleteAgentTypology(typology.id);
        toast.success(`Deleted typology: ${typology.name}`);
        fetchTypologies(); // Refresh list
      } catch (err) {
        const errorMessage = err instanceof Error ? err.message : 'Failed to delete typology';
        toast.error(errorMessage);
      }
    },
    [admin, fetchTypologies]
  );

  // Create columns with actions
  const columns = React.useMemo(
    () =>
      createTypologyColumns({
        onEdit: handleEdit,
        onApprove: handleApprove,
        onDelete: handleDelete,
      }),
    [handleEdit, handleApprove, handleDelete]
  );

  // Loading state
  if (loading && typologies.length === 0) {
    return (
      <div className="flex justify-center py-12">
        <Spinner size="lg" />
      </div>
    );
  }

  // Error state
  if (error && typologies.length === 0) {
    return (
      <Alert variant="destructive">
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>{error}</AlertDescription>
      </Alert>
    );
  }

  // Empty state
  if (!loading && typologies.length === 0 && !search && status === 'All') {
    return (
      <Alert>
        <AlertCircle className="h-4 w-4" />
        <AlertDescription>
          No agent typologies yet. Create your first typology to get started.
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <div className="space-y-4">
      {/* Filters */}
      <TypologyFilters
        status={status}
        onStatusChange={setStatus}
        createdBy={createdBy}
        onCreatedByChange={setCreatedBy}
        search={search}
        onSearchChange={setSearch}
        typologies={typologies}
      />

      {/* Stats */}
      <div className="text-sm text-muted-foreground">
        {total} {total === 1 ? 'typology' : 'typologies'} total
        {(status !== 'All' || createdBy !== 'All' || search) && ' (filtered)'}
      </div>

      {/* Table */}
      <DataTable
        columns={columns}
        data={typologies}
        sorting={sorting}
        onSortingChange={updater => {
          if (typeof updater === 'function') {
            setSorting(updater(sorting ?? []));
          } else {
            setSorting(updater);
          }
        }}
        getRowId={typology => typology.id}
        isLoading={loading}
        emptyMessage={
          search || status !== 'All' || createdBy !== 'All'
            ? 'No typologies match your filters.'
            : 'No typologies available.'
        }
      />
    </div>
  );
}
