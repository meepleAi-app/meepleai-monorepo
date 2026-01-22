'use client';

import { useEffect, useState } from 'react';
import { useRouter } from 'next/navigation';
import { AdminAuthGuard } from '@/components/admin/AdminAuthGuard';
import { useAuthUser } from '@/hooks/auth/useAuthUser';
import {
  useShareRequestDetails,
  useStartReview,
  useReleaseReview,
  useApproveRequest,
  useRejectRequest,
  useRequestChanges,
} from '@/hooks/queries';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Lock, Unlock, ArrowLeft } from 'lucide-react';
import { ShareRequestStatusBadge } from '../_components/ShareRequestStatusBadge';
import { LockStatusBadge } from '../_components/LockStatusBadge';
import { ContributorProfile } from '../_components/ContributorProfile';
import { DocumentsPreviewGrid } from '../_components/DocumentsPreviewGrid';
import { GameEditableFields } from '../_components/GameEditableFields';
import { ApproveButton } from '../_components/ReviewActions/ApproveButton';
import { RejectButton } from '../_components/ReviewActions/RejectButton';
import { RequestChangesButton } from '../_components/ReviewActions/RequestChangesButton';

/**
 * Admin Share Request Review Detail Page - Client Component
 *
 * Full review interface for share requests with:
 * - Game details (editable title/description)
 * - Contributor profile with stats and badges
 * - Document preview and selection
 * - Review lock management
 * - Action buttons (approve, reject, request changes)
 *
 * Issue #2745: Frontend - Admin Review Interface
 */

interface AdminShareRequestDetailClientProps {
  id: string;
}

export function AdminShareRequestDetailClient({ id }: AdminShareRequestDetailClientProps): JSX.Element {
  const router = useRouter();
  const { user, loading: authLoading } = useAuthUser();

  // Fetch request details
  const { data: request, isLoading } = useShareRequestDetails(id);

  // Mutations
  const { mutate: startReview, isPending: isStarting } = useStartReview();
  const { mutate: releaseReview, isPending: isReleasing } = useReleaseReview();
  const { mutate: approve, isPending: isApproving } = useApproveRequest();
  const { mutate: reject, isPending: isRejecting } = useRejectRequest();
  const { mutate: requestChanges, isPending: isRequestingChanges } = useRequestChanges();

  // Editable state
  const [editedTitle, setEditedTitle] = useState('');
  const [editedDescription, setEditedDescription] = useState('');
  const [selectedDocIds, setSelectedDocIds] = useState<string[]>([]);

  // Initialize editable state when request loads
  useEffect(() => {
    if (request) {
      setEditedTitle(request.sourceGame.title);
      setEditedDescription(request.sourceGame.description ?? '');
      setSelectedDocIds(request.attachedDocuments.map((doc) => doc.documentId));
    }
  }, [request]);

  // Derived state
  const canTakeAction = request?.lockStatus.isLockedByCurrentAdmin ?? false;
  const isLocked = request?.lockStatus.isLocked ?? false;
  const isPending = isStarting || isReleasing || isApproving || isRejecting || isRequestingChanges;

  // Handlers
  const handleStartReview = () => {
    startReview({ shareRequestId: id });
  };

  const handleReleaseReview = () => {
    releaseReview(
      { shareRequestId: id },
      {
        onSuccess: () => router.push('/admin/share-requests'),
      }
    );
  };

  const handleApprove = (adminNotes: string) => {
    approve({
      shareRequestId: id,
      modifiedTitle: editedTitle !== request?.sourceGame.title ? editedTitle : undefined,
      modifiedDescription:
        editedDescription !== request?.sourceGame.description ? editedDescription : undefined,
      documentsToInclude: selectedDocIds,
      adminNotes: adminNotes || undefined,
    }, {
      onSuccess: () => router.push('/admin/share-requests'),
    });
  };

  const handleReject = (reason: string) => {
    reject(
      { shareRequestId: id, reason },
      {
        onSuccess: () => router.push('/admin/share-requests'),
      }
    );
  };

  const handleRequestChanges = (feedback: string) => {
    requestChanges(
      { shareRequestId: id, feedback },
      {
        onSuccess: () => router.push('/admin/share-requests'),
      }
    );
  };

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto p-6 max-w-7xl">
        {/* Loading State */}
        {isLoading && (
          <div className="text-center py-12">
            <p className="text-muted-foreground">Loading share request...</p>
          </div>
        )}

        {/* Content */}
        {!isLoading && request && (
          <div className="space-y-6">
            {/* Header */}
            <div className="flex items-start justify-between">
              <div className="space-y-2">
                <Button variant="ghost" size="sm" onClick={() => router.push('/admin/share-requests')}>
                  <ArrowLeft className="mr-2 h-4 w-4" />
                  Back to Queue
                </Button>
                <div>
                  <h1 className="text-3xl font-bold">{request.sourceGame.title}</h1>
                  <p className="text-muted-foreground">
                    Submitted by {request.contributor.userName}
                  </p>
                </div>
              </div>
              <div className="flex gap-2">
                <ShareRequestStatusBadge status={request.status} />
                <LockStatusBadge lockStatus={request.lockStatus} />
              </div>
            </div>

            {/* Main Layout: Content + Sidebar */}
            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
              {/* Main Content (2/3 width) */}
              <div className="lg:col-span-2 space-y-6">
                {/* Game Details */}
                <GameEditableFields
                  title={editedTitle}
                  description={editedDescription}
                  onTitleChange={setEditedTitle}
                  onDescriptionChange={setEditedDescription}
                  disabled={!canTakeAction}
                />

                {/* Additional Game Metadata (Read-Only) */}
                <Card>
                  <CardHeader>
                    <CardTitle>Game Metadata</CardTitle>
                  </CardHeader>
                  <CardContent className="grid grid-cols-2 gap-4 text-sm">
                    {request.sourceGame.bggId && (
                      <div>
                        <p className="text-muted-foreground">BGG ID</p>
                        <p className="font-medium">#{request.sourceGame.bggId}</p>
                      </div>
                    )}
                    {request.sourceGame.minPlayers && request.sourceGame.maxPlayers && (
                      <div>
                        <p className="text-muted-foreground">Players</p>
                        <p className="font-medium">
                          {request.sourceGame.minPlayers}-{request.sourceGame.maxPlayers}
                        </p>
                      </div>
                    )}
                    {request.sourceGame.playingTime && (
                      <div>
                        <p className="text-muted-foreground">Playing Time</p>
                        <p className="font-medium">{request.sourceGame.playingTime} min</p>
                      </div>
                    )}
                    {request.sourceGame.complexity && (
                      <div>
                        <p className="text-muted-foreground">Complexity</p>
                        <p className="font-medium">{request.sourceGame.complexity.toFixed(1)}/5</p>
                      </div>
                    )}
                  </CardContent>
                </Card>

                {/* Documents */}
                <Card>
                  <CardHeader>
                    <CardTitle>
                      Attached Documents ({request.attachedDocuments.length})
                    </CardTitle>
                  </CardHeader>
                  <CardContent>
                    <DocumentsPreviewGrid
                      documents={request.attachedDocuments}
                      selectedIds={selectedDocIds}
                      onSelectionChange={setSelectedDocIds}
                      selectable={canTakeAction}
                    />
                  </CardContent>
                </Card>

                {/* User Notes */}
                {request.userNotes && (
                  <Card>
                    <CardHeader>
                      <CardTitle>Contributor Notes</CardTitle>
                    </CardHeader>
                    <CardContent>
                      <p className="text-sm whitespace-pre-wrap">{request.userNotes}</p>
                    </CardContent>
                  </Card>
                )}
              </div>

              {/* Sidebar (1/3 width) */}
              <div className="space-y-6">
                {/* Contributor Profile */}
                <Card>
                  <CardHeader>
                    <CardTitle>Contributor</CardTitle>
                  </CardHeader>
                  <CardContent>
                    <ContributorProfile contributor={request.contributor} />
                  </CardContent>
                </Card>

                {/* Actions */}
                <Card>
                  <CardHeader>
                    <CardTitle>Review Actions</CardTitle>
                  </CardHeader>
                  <CardContent className="space-y-3">
                    {/* Start/Release Review */}
                    {!isLocked && (
                      <Button
                        className="w-full"
                        onClick={handleStartReview}
                        disabled={isPending}
                      >
                        <Lock className="w-4 h-4 mr-2" />
                        Start Review
                      </Button>
                    )}

                    {canTakeAction && (
                      <>
                        {/* Release Lock */}
                        <Button
                          variant="outline"
                          className="w-full"
                          onClick={handleReleaseReview}
                          disabled={isPending}
                        >
                          <Unlock className="w-4 h-4 mr-2" />
                          Release Lock
                        </Button>

                        {/* Divider */}
                        <div className="border-t pt-3" />

                        {/* Action Buttons */}
                        <ApproveButton onApprove={handleApprove} isPending={isPending} />
                        <RequestChangesButton
                          onRequestChanges={handleRequestChanges}
                          isPending={isPending}
                        />
                        <RejectButton onReject={handleReject} isPending={isPending} />
                      </>
                    )}

                    {/* Locked by Another Admin */}
                    {isLocked && !canTakeAction && (
                      <div className="p-3 rounded-lg bg-muted text-sm">
                        <p className="font-medium">Locked by Another Admin</p>
                        <p className="text-muted-foreground mt-1">
                          {request.lockStatus.lockedByAdminName} is currently reviewing this
                          request.
                        </p>
                      </div>
                    )}
                  </CardContent>
                </Card>

                {/* Contribution Type Info */}
                <Card>
                  <CardHeader>
                    <CardTitle>Contribution Type</CardTitle>
                  </CardHeader>
                  <CardContent>
                    <Badge variant="outline" className="mb-2">
                      {request.contributionType === 'NewGame' ? 'New Game' : 'Additional Content'}
                    </Badge>
                    <p className="text-sm text-muted-foreground mt-2">
                      {request.contributionType === 'NewGame'
                        ? 'This is a new game being added to the shared catalog.'
                        : 'This adds additional content to an existing shared game.'}
                    </p>
                  </CardContent>
                </Card>
              </div>
            </div>
          </div>
        )}
      </div>
    </AdminAuthGuard>
  );
}
