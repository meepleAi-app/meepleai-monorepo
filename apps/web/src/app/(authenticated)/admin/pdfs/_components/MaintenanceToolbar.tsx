'use client';

import { useState } from 'react';

import { Wrench, Clock } from 'lucide-react';
import { toast } from 'sonner';

import {
  AdminConfirmationDialog,
  AdminConfirmationLevel,
} from '@/components/ui/admin/admin-confirmation-dialog';
import { Button } from '@/components/ui/primitives/button';
import { useApiClient } from '@/lib/api/context';

interface MaintenanceToolbarProps {
  onComplete: () => void;
}

export function MaintenanceToolbar({ onComplete }: MaintenanceToolbarProps) {
  const apiClient = useApiClient();
  const [purgeOpen, setPurgeOpen] = useState(false);
  const [cleanupOpen, setCleanupOpen] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);

  const handlePurgeStale = async () => {
    setIsProcessing(true);
    try {
      const result = await apiClient.pdf.purgeStaleDocuments();
      toast.success(`Purged ${result.purgedCount} stale document(s)`);
      onComplete();
    } catch {
      toast.error('Failed to purge stale documents');
    } finally {
      setIsProcessing(false);
      setPurgeOpen(false);
    }
  };

  const handleCleanupOrphans = async () => {
    setIsProcessing(true);
    try {
      const result = await apiClient.pdf.cleanupOrphans();
      toast.success(
        `Cleaned up ${result.orphanedChunks} orphaned chunk(s) and ${result.orphanedVectors} orphaned vector(s)`
      );
      onComplete();
    } catch {
      toast.error('Failed to cleanup orphans');
    } finally {
      setIsProcessing(false);
      setCleanupOpen(false);
    }
  };

  return (
    <>
      <div className="flex items-center gap-2">
        <Button
          variant="outline"
          size="sm"
          onClick={() => setPurgeOpen(true)}
          disabled={isProcessing}
        >
          <Clock className="h-4 w-4 mr-2" />
          Purge Stale
        </Button>
        <Button
          variant="outline"
          size="sm"
          onClick={() => setCleanupOpen(true)}
          disabled={isProcessing}
        >
          <Wrench className="h-4 w-4 mr-2" />
          Cleanup Orphans
        </Button>
      </div>

      <AdminConfirmationDialog
        isOpen={purgeOpen}
        onClose={() => setPurgeOpen(false)}
        title="Purge Stale Documents"
        message="This will mark documents stuck in processing (>24h) as failed. They can be retried later."
        confirmText="Purge Stale"
        level={AdminConfirmationLevel.Level1}
        onConfirm={handlePurgeStale}
        isLoading={isProcessing}
      />

      <AdminConfirmationDialog
        isOpen={cleanupOpen}
        onClose={() => setCleanupOpen(false)}
        title="Cleanup Orphaned Data"
        message="This will delete text chunks and vectors that reference non-existent PDF documents. This action cannot be undone."
        confirmText="Cleanup Orphans"
        level={AdminConfirmationLevel.Level2}
        onConfirm={handleCleanupOrphans}
        isLoading={isProcessing}
      />
    </>
  );
}
