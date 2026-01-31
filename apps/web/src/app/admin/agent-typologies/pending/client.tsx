'use client';

/**
 * Pending Typologies Client Component (Issue #3181)
 *
 * Main container for pending typology approval queue.
 * Orchestrates:
 * - Fetch typologies with status=PendingReview
 * - Grid layout of TypologyProposalCard components
 * - Multi-select state management
 * - Bulk actions (approve all, reject all)
 * - Approval and rejection dialogs
 * - Optimistic UI updates with error handling
 *
 * Part of Epic #3174 (AI Agent System).
 */

import { useCallback, useEffect, useState } from 'react';

import { toast } from 'sonner';

import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';
import { useAuth } from '@/components/auth/AuthProvider';
import { Spinner } from '@/components/loading/Spinner';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { agentTypologiesApi } from '@/lib/api/agent-typologies.api';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import type { Typology } from '@/lib/api/schemas/agent-typologies.schemas';

const admin = createAdminClient({ httpClient: new HttpClient() });

import { ApproveConfirmDialog } from './_components/ApproveConfirmDialog';
import { BulkActionBar } from './_components/BulkActionBar';
import { RejectDialog } from './_components/RejectDialog';
import { TypologyProposalCard } from './_components/TypologyProposalCard';

export function PendingTypologiesClient() {
  const { user, loading: authLoading } = useAuth();
  const [typologies, setTypologies] = useState<Typology[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  // Dialog states
  const [approveDialog, setApproveDialog] = useState<{
    isOpen: boolean;
    typology: Typology | null;
    isBulk: boolean;
  }>({
    isOpen: false,
    typology: null,
    isBulk: false,
  });

  const [rejectDialog, setRejectDialog] = useState<{
    isOpen: boolean;
    typology: Typology | null;
    isBulk: boolean;
  }>({
    isOpen: false,
    typology: null,
    isBulk: false,
  });

  // Fetch pending typologies
  const fetchPendingTypologies = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);
      const allTypologies = await agentTypologiesApi.getAll();
      const pending = allTypologies.filter(t => t.status === 'PendingReview');
      setTypologies(pending);
    } catch (err) {
      const errorMessage =
        err instanceof Error ? err.message : 'Failed to fetch pending typologies';
      setError(errorMessage);
      toast.error(errorMessage);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchPendingTypologies();
  }, [fetchPendingTypologies]);

  // Single approve
  const handleApprove = useCallback(
    (typology: Typology) => {
      setApproveDialog({ isOpen: true, typology, isBulk: false });
    },
    []
  );

  const handleApproveConfirm = useCallback(async () => {
    if (!approveDialog.typology) return;

    try {
      // Optimistic update
      setTypologies((prev) =>
        prev.filter((t) => t.id !== approveDialog.typology!.id)
      );
      setSelectedIds((prev) => {
        const next = new Set(prev);
        next.delete(approveDialog.typology!.id);
        return next;
      });

      await admin.approveAgentTypology(approveDialog.typology.id);
      toast.success(`Approved typology: ${approveDialog.typology.name}`);
      setApproveDialog({ isOpen: false, typology: null, isBulk: false });
    } catch (err) {
      // Revert on error
      await fetchPendingTypologies();
      const errorMessage =
        err instanceof Error ? err.message : 'Failed to approve typology';
      toast.error(errorMessage);
    }
  }, [approveDialog.typology, fetchPendingTypologies]);

  // Single reject
  const handleReject = useCallback(
    (typology: Typology) => {
      setRejectDialog({ isOpen: true, typology, isBulk: false });
    },
    []
  );

  const handleRejectConfirm = useCallback(
    async (reason: string) => {
      if (!rejectDialog.typology) return;

      try {
        // Optimistic update
        setTypologies((prev) =>
          prev.filter((t) => t.id !== rejectDialog.typology!.id)
        );
        setSelectedIds((prev) => {
          const next = new Set(prev);
          next.delete(rejectDialog.typology!.id);
          return next;
        });

        await admin.rejectAgentTypology(rejectDialog.typology.id, reason);
        toast.success(`Rejected typology: ${rejectDialog.typology.name}`);
        setRejectDialog({ isOpen: false, typology: null, isBulk: false });
      } catch (err) {
        // Revert on error
        await fetchPendingTypologies();
        const errorMessage =
          err instanceof Error ? err.message : 'Failed to reject typology';
        toast.error(errorMessage);
      }
    },
    [rejectDialog.typology, fetchPendingTypologies]
  );

  // View details
  const handleViewDetails = useCallback((typology: Typology) => {
    // TODO: Navigate to details page or open modal
    toast.info(`View details for: ${typology.name}`);
  }, []);

  // Multi-select
  const handleToggleSelect = useCallback((id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }, []);

  const handleClearSelection = useCallback(() => {
    setSelectedIds(new Set());
  }, []);

  // Bulk approve
  const handleBulkApprove = useCallback(() => {
    if (selectedIds.size === 0) return;
    setApproveDialog({ isOpen: true, typology: null, isBulk: true });
  }, [selectedIds.size]);

  const handleBulkApproveConfirm = useCallback(async () => {
    const idsToApprove = Array.from(selectedIds);
    if (idsToApprove.length === 0) return;

    try {
      // Optimistic update
      setTypologies((prev) => prev.filter((t) => !selectedIds.has(t.id)));
      setSelectedIds(new Set());

      // Sequential approvals (no bulk endpoint)
      await Promise.all(
        idsToApprove.map((id) => admin.approveAgentTypology(id))
      );
      toast.success(`Approved ${idsToApprove.length} typologies`);
      setApproveDialog({ isOpen: false, typology: null, isBulk: false });
    } catch (err) {
      // Revert on error
      await fetchPendingTypologies();
      const errorMessage =
        err instanceof Error ? err.message : 'Failed to approve typologies';
      toast.error(errorMessage);
    }
  }, [selectedIds, fetchPendingTypologies]);

  // Bulk reject
  const handleBulkReject = useCallback(() => {
    if (selectedIds.size === 0) return;
    setRejectDialog({ isOpen: true, typology: null, isBulk: true });
  }, [selectedIds.size]);

  const handleBulkRejectConfirm = useCallback(
    async (reason: string) => {
      const idsToReject = Array.from(selectedIds);
      if (idsToReject.length === 0) return;

      try {
        // Optimistic update
        setTypologies((prev) => prev.filter((t) => !selectedIds.has(t.id)));
        setSelectedIds(new Set());

        // Sequential rejections (no bulk endpoint)
        await Promise.all(
          idsToReject.map((id) => admin.rejectAgentTypology(id, reason))
        );
        toast.success(`Rejected ${idsToReject.length} typologies`);
        setRejectDialog({ isOpen: false, typology: null, isBulk: false });
      } catch (err) {
        // Revert on error
        await fetchPendingTypologies();
        const errorMessage =
          err instanceof Error ? err.message : 'Failed to reject typologies';
        toast.error(errorMessage);
      }
    },
    [selectedIds, fetchPendingTypologies]
  );

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto p-6 max-w-7xl">
        <div className="mb-6">
          <h1 className="text-3xl font-bold tracking-tight">
            Pending Typology Approvals
          </h1>
          <p className="text-muted-foreground mt-2">
            Review and approve agent typology proposals from editors
          </p>
        </div>

        {/* Loading state */}
        {loading && (
          <div className="flex justify-center items-center h-64">
            <Spinner size="lg" />
          </div>
        )}

        {/* Error state */}
        {error && !loading && (
          <Alert variant="destructive">
            <AlertTitle>Error</AlertTitle>
            <AlertDescription>{error}</AlertDescription>
          </Alert>
        )}

        {/* Empty state */}
        {!loading && !error && typologies.length === 0 && (
          <Alert>
            <AlertTitle>No Pending Proposals</AlertTitle>
            <AlertDescription>
              There are currently no typologies pending approval.
            </AlertDescription>
          </Alert>
        )}

        {/* Typologies grid */}
        {!loading && !error && typologies.length > 0 && (
          <div className="grid gap-6 md:grid-cols-2 lg:grid-cols-3">
            {typologies.map((typology) => (
              <TypologyProposalCard
                key={typology.id}
                typology={typology}
                isSelected={selectedIds.has(typology.id)}
                onToggleSelect={handleToggleSelect}
                onApprove={handleApprove}
                onReject={handleReject}
                onViewDetails={handleViewDetails}
              />
            ))}
          </div>
        )}

        {/* Bulk action bar */}
        {selectedIds.size > 0 && (
          <BulkActionBar
            selectedCount={selectedIds.size}
            onApproveAll={handleBulkApprove}
            onRejectAll={handleBulkReject}
            onClearSelection={handleClearSelection}
          />
        )}

        {/* Approve dialog */}
        <ApproveConfirmDialog
          isOpen={approveDialog.isOpen}
          isBulk={approveDialog.isBulk}
          typologyName={approveDialog.typology?.name}
          selectedCount={selectedIds.size}
          onConfirm={
            approveDialog.isBulk ? handleBulkApproveConfirm : handleApproveConfirm
          }
          onCancel={() =>
            setApproveDialog({ isOpen: false, typology: null, isBulk: false })
          }
        />

        {/* Reject dialog */}
        <RejectDialog
          isOpen={rejectDialog.isOpen}
          isBulk={rejectDialog.isBulk}
          typologyName={rejectDialog.typology?.name}
          selectedCount={selectedIds.size}
          onConfirm={
            rejectDialog.isBulk ? handleBulkRejectConfirm : handleRejectConfirm
          }
          onCancel={() =>
            setRejectDialog({ isOpen: false, typology: null, isBulk: false })
          }
        />
      </div>
    </AdminAuthGuard>
  );
}
