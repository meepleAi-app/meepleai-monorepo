'use client';

/**
 * CameraToolContent - Tool content area for the Camera tool in ToolRail.
 * Issue #5371 - ToolRail camera integration.
 * Epic #5358 - Session Photo Attachments (Phase 0).
 */

import { useState, useCallback } from 'react';

import { Camera, Plus } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

import { PhotoUploadModal, type SessionAttachmentDto } from './PhotoUploadModal';
import { SessionPhotoGallery } from './SessionPhotoGallery';

export interface CameraToolContentProps {
  sessionId: string;
  playerId: string;
  /** All session attachments. */
  attachments: SessionAttachmentDto[];
  /** Whether data is loading. */
  isLoading?: boolean;
  /** Whether current user is host. */
  isHost?: boolean;
  /** Called when a new photo is uploaded. */
  onUploadComplete?: (attachment: SessionAttachmentDto) => void;
  /** Called when a photo is deleted. */
  onDelete?: (attachmentId: string) => void;
  /** Current snapshot index (auto-incremented or provided). */
  snapshotIndex?: number;
}

export function CameraToolContent({
  sessionId,
  playerId,
  attachments,
  isLoading = false,
  isHost = false,
  onUploadComplete,
  onDelete,
  snapshotIndex,
}: CameraToolContentProps) {
  const [uploadOpen, setUploadOpen] = useState(false);

  const handleUploadComplete = useCallback(
    (attachment: SessionAttachmentDto) => {
      onUploadComplete?.(attachment);
      setUploadOpen(false);
    },
    [onUploadComplete]
  );

  return (
    <div className="space-y-4" data-testid="camera-tool-content">
      {/* Header with upload button */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Camera className="h-5 w-5 text-amber-600" aria-hidden="true" />
          <h3 className="text-sm font-semibold">Session Photos</h3>
        </div>
        <Button
          size="sm"
          onClick={() => setUploadOpen(true)}
          className="bg-amber-600 hover:bg-amber-700 text-white"
          data-testid="upload-photo-button"
        >
          <Plus className="mr-1 h-4 w-4" aria-hidden="true" />
          Upload
        </Button>
      </div>

      {/* Gallery */}
      <SessionPhotoGallery
        sessionId={sessionId}
        attachments={attachments}
        isLoading={isLoading}
        currentPlayerId={playerId}
        isHost={isHost}
        onDelete={onDelete}
      />

      {/* Upload modal */}
      <PhotoUploadModal
        sessionId={sessionId}
        playerId={playerId}
        snapshotIndex={snapshotIndex}
        open={uploadOpen}
        onOpenChange={setUploadOpen}
        onUploadComplete={handleUploadComplete}
      />
    </div>
  );
}
