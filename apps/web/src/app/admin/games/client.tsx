'use client';

/**
 * Games Client Component (Issue #2515)
 *
 * Main client component for admin games dashboard.
 * Orchestrates:
 * - GamesTable (list, filters, pagination)
 * - GameEditModal (create/edit)
 * - ApprovalReviewModal (approve/reject workflow)
 *
 * Implements authorization check (Admin/Editor only).
 */

import { useState } from 'react';

import { AdminAuthGuard } from '@/components/auth/AdminAuthGuard';
import { useAuthContext } from '@/lib/auth/context';
import { GamesTable } from './_components/GamesTable/GamesTable';
import { GameEditModal } from './_components/GameEditModal/GameEditModal';
import { ApprovalReviewModal } from './_components/ApprovalReviewModal/ApprovalReviewModal';

export function GamesClient() {
  const { user, loading: authLoading } = useAuthContext();

  // Modal state
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [approvalModalOpen, setApprovalModalOpen] = useState(false);
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);

  // Handlers
  const handleEdit = (gameId: string) => {
    setSelectedGameId(gameId);
    setEditModalOpen(true);
  };

  const handleReviewApproval = (gameId: string) => {
    setSelectedGameId(gameId);
    setApprovalModalOpen(true);
  };

  const handleCloseEditModal = () => {
    setEditModalOpen(false);
    setSelectedGameId(null);
  };

  const handleCloseApprovalModal = () => {
    setApprovalModalOpen(false);
    setSelectedGameId(null);
  };

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto p-6 max-w-7xl">
        <GamesTable onEdit={handleEdit} onReviewApproval={handleReviewApproval} />

        {/* Edit Modal */}
        <GameEditModal
          isOpen={editModalOpen}
          onClose={handleCloseEditModal}
          gameId={selectedGameId}
          onSuccess={() => {
            // Refresh table after successful edit
            // Table will re-fetch via useEffect
          }}
        />

        {/* Approval Review Modal */}
        <ApprovalReviewModal
          isOpen={approvalModalOpen}
          onClose={handleCloseApprovalModal}
          gameId={selectedGameId}
          onSuccess={() => {
            // Refresh table after approval/rejection
          }}
        />
      </div>
    </AdminAuthGuard>
  );
}
