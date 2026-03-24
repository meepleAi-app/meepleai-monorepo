'use client';

/**
 * SessionPhotoGallery - Responsive grid of session photos with lightbox.
 * Issue #5369 - SessionPhotoGallery frontend component.
 * Epic #5358 - Session Photo Attachments (Phase 0).
 */

import { useState, useCallback, useMemo } from 'react';

import {
  Camera,
  Download,
  Trash2,
  X,
  ChevronLeft,
  ChevronRight,
  ImageIcon,
  User,
} from 'lucide-react';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/overlays/alert-dialog-primitives';
import { Dialog, DialogContent, DialogTitle } from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import type { AttachmentType, SessionAttachmentDto } from './PhotoUploadModal';

/** Detailed attachment with download URL (returned by GET by-id endpoint). */
export interface SessionAttachmentDetail extends SessionAttachmentDto {
  downloadUrl: string;
  playerDisplayName: string;
}

export interface SessionPhotoGalleryProps {
  sessionId: string;
  playerId?: string;
  snapshotIndex?: number;
  attachmentType?: AttachmentType;
  readOnly?: boolean;
  /** Attachment list (fetched externally or via hook). */
  attachments: SessionAttachmentDto[];
  /** Whether data is still loading. */
  isLoading?: boolean;
  /** Current user's player ID (for delete permission). */
  currentPlayerId?: string;
  /** Whether current user is host (can delete any photo). */
  isHost?: boolean;
  /** Called when a photo is deleted. */
  onDelete?: (attachmentId: string) => void;
  /** Called to fetch full-size detail for lightbox. */
  onFetchDetail?: (attachmentId: string) => Promise<SessionAttachmentDetail>;
}

const TYPE_BADGE_COLORS: Record<AttachmentType, string> = {
  PlayerArea: 'bg-purple-500/80 text-white',
  BoardState: 'bg-amber-500/80 text-white',
  CharacterSheet: 'bg-blue-500/80 text-white',
  ResourceInventory: 'bg-emerald-500/80 text-white',
  Custom: 'bg-slate-500/80 text-white',
};

const TYPE_LABELS: Record<AttachmentType, string> = {
  PlayerArea: 'Player Area',
  BoardState: 'Board',
  CharacterSheet: 'Character',
  ResourceInventory: 'Resources',
  Custom: 'Custom',
};

// ================================================================
// PhotoAttachmentCard
// ================================================================

interface PhotoCardProps {
  attachment: SessionAttachmentDto;
  onClick: () => void;
  canDelete: boolean;
  onDelete: () => void;
}

function PhotoAttachmentCard({ attachment, onClick, canDelete, onDelete }: PhotoCardProps) {
  return (
    <div
      className="group relative overflow-hidden rounded-lg border border-border/50 bg-card cursor-pointer transition-shadow hover:shadow-md"
      data-testid={`photo-card-${attachment.id}`}
    >
      {/* Thumbnail */}
      <div
        className="aspect-square overflow-hidden bg-muted"
        onClick={onClick}
        role="button"
        tabIndex={0}
        aria-label={`View photo${attachment.caption ? `: ${attachment.caption}` : ''}`}
        onKeyDown={e => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            onClick();
          }
        }}
      >
        {attachment.thumbnailUrl ? (
          // eslint-disable-next-line @next/next/no-img-element
          <img
            src={attachment.thumbnailUrl}
            alt={attachment.caption || 'Session photo'}
            className="h-full w-full object-cover transition-transform group-hover:scale-105"
            loading="lazy"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center">
            <ImageIcon className="h-8 w-8 text-muted-foreground" aria-hidden="true" />
          </div>
        )}
      </div>

      {/* Type badge (top-right) */}
      <span
        className={cn(
          'absolute top-1.5 right-1.5 rounded-full px-2 py-0.5 text-[10px] font-medium',
          TYPE_BADGE_COLORS[attachment.attachmentType]
        )}
        data-testid="type-badge"
      >
        {TYPE_LABELS[attachment.attachmentType]}
      </span>

      {/* Info overlay (bottom) */}
      <div className="p-2 space-y-0.5">
        {attachment.caption && (
          <p className="text-xs text-foreground line-clamp-1" title={attachment.caption}>
            {attachment.caption}
          </p>
        )}
        <div className="flex items-center justify-between text-[10px] text-muted-foreground">
          <span className="flex items-center gap-1">
            <User className="h-3 w-3" aria-hidden="true" />
            {attachment.playerId.slice(0, 8)}
          </span>
          {attachment.snapshotIndex != null && <span>Snap #{attachment.snapshotIndex}</span>}
        </div>
      </div>

      {/* Delete button (visible on hover) */}
      {canDelete && (
        <Button
          variant="destructive"
          size="icon"
          className="absolute top-1.5 left-1.5 h-6 w-6 opacity-0 group-hover:opacity-100 transition-opacity"
          onClick={e => {
            e.stopPropagation();
            onDelete();
          }}
          aria-label="Delete photo"
          data-testid="delete-button"
        >
          <Trash2 className="h-3 w-3" />
        </Button>
      )}
    </div>
  );
}

// ================================================================
// Loading Skeleton
// ================================================================

function GallerySkeleton({ count = 4 }: { count?: number }) {
  return (
    <div
      className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3"
      data-testid="gallery-skeleton"
    >
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className="space-y-2">
          <Skeleton className="aspect-square rounded-lg" />
          <Skeleton className="h-3 w-3/4" />
          <Skeleton className="h-2 w-1/2" />
        </div>
      ))}
    </div>
  );
}

// ================================================================
// Empty State
// ================================================================

function EmptyState() {
  return (
    <div
      className="flex flex-col items-center justify-center py-12 text-center"
      data-testid="empty-state"
    >
      <Camera className="h-12 w-12 text-muted-foreground/50 mb-3" aria-hidden="true" />
      <p className="text-sm font-medium text-muted-foreground">No photos yet</p>
      <p className="text-xs text-muted-foreground/70 mt-1">
        Use the camera to capture moments from your session
      </p>
    </div>
  );
}

// ================================================================
// Main Gallery
// ================================================================

export function SessionPhotoGallery({
  attachments,
  isLoading = false,
  currentPlayerId,
  isHost = false,
  readOnly = false,
  onDelete,
  onFetchDetail,
}: SessionPhotoGalleryProps) {
  const [lightboxIndex, setLightboxIndex] = useState<number | null>(null);
  const [lightboxDetail, setLightboxDetail] = useState<SessionAttachmentDetail | null>(null);
  const [lightboxLoading, setLightboxLoading] = useState(false);
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null);

  // Filter state
  const [filterType, setFilterType] = useState<AttachmentType | ''>('');

  const filteredAttachments = useMemo(() => {
    if (!filterType) return attachments;
    return attachments.filter(a => a.attachmentType === filterType);
  }, [attachments, filterType]);

  const canDeleteAttachment = useCallback(
    (attachment: SessionAttachmentDto) => {
      if (readOnly) return false;
      if (!onDelete) return false;
      if (isHost) return true;
      return currentPlayerId === attachment.playerId;
    },
    [readOnly, onDelete, isHost, currentPlayerId]
  );

  const openLightbox = useCallback(
    async (index: number) => {
      setLightboxIndex(index);
      setLightboxDetail(null);

      if (onFetchDetail) {
        setLightboxLoading(true);
        try {
          const detail = await onFetchDetail(filteredAttachments[index].id);
          setLightboxDetail(detail);
        } catch {
          // Fall back to thumbnail URL
        } finally {
          setLightboxLoading(false);
        }
      }
    },
    [onFetchDetail, filteredAttachments]
  );

  const closeLightbox = useCallback(() => {
    setLightboxIndex(null);
    setLightboxDetail(null);
  }, []);

  const navigateLightbox = useCallback(
    async (direction: -1 | 1) => {
      if (lightboxIndex == null) return;
      const next = lightboxIndex + direction;
      if (next < 0 || next >= filteredAttachments.length) return;
      await openLightbox(next);
    },
    [lightboxIndex, filteredAttachments.length, openLightbox]
  );

  const handleConfirmDelete = useCallback(() => {
    if (deleteTarget && onDelete) {
      onDelete(deleteTarget);
    }
    setDeleteTarget(null);
  }, [deleteTarget, onDelete]);

  const currentLightboxAttachment =
    lightboxIndex != null ? filteredAttachments[lightboxIndex] : null;

  if (isLoading) return <GallerySkeleton />;
  if (attachments.length === 0) return <EmptyState />;

  return (
    <div className="space-y-3" data-testid="session-photo-gallery">
      {/* Filter bar */}
      <div className="flex items-center gap-2">
        <select
          value={filterType}
          onChange={e => setFilterType(e.target.value as AttachmentType | '')}
          className={cn(
            'h-8 rounded-md border border-input bg-transparent px-2 text-xs shadow-sm',
            'focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring'
          )}
          data-testid="type-filter"
          aria-label="Filter by type"
        >
          <option value="">All types</option>
          <option value="PlayerArea">Player Area</option>
          <option value="BoardState">Board State</option>
          <option value="CharacterSheet">Character Sheet</option>
          <option value="ResourceInventory">Resources</option>
          <option value="Custom">Custom</option>
        </select>
        <span className="text-xs text-muted-foreground">
          {filteredAttachments.length} photo{filteredAttachments.length !== 1 ? 's' : ''}
        </span>
      </div>

      {/* Grid */}
      {filteredAttachments.length === 0 ? (
        <p className="text-sm text-muted-foreground py-6 text-center" data-testid="filter-empty">
          No photos match the selected filter.
        </p>
      ) : (
        <div
          className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3"
          data-testid="photo-grid"
        >
          {filteredAttachments.map((attachment, idx) => (
            <PhotoAttachmentCard
              key={attachment.id}
              attachment={attachment}
              onClick={() => void openLightbox(idx)}
              canDelete={canDeleteAttachment(attachment)}
              onDelete={() => setDeleteTarget(attachment.id)}
            />
          ))}
        </div>
      )}

      {/* Lightbox */}
      <Dialog
        open={lightboxIndex != null}
        onOpenChange={open => {
          if (!open) closeLightbox();
        }}
      >
        <DialogContent
          className="max-w-3xl p-0 overflow-hidden"
          hideCloseButton
          data-testid="lightbox"
        >
          <DialogTitle className="sr-only">Photo viewer</DialogTitle>
          {currentLightboxAttachment && (
            <div className="relative">
              {/* Image */}
              <div className="relative flex items-center justify-center bg-black min-h-[300px] max-h-[70vh]">
                {lightboxLoading ? (
                  <Skeleton className="h-64 w-full" />
                ) : (
                  // eslint-disable-next-line @next/next/no-img-element
                  <img
                    src={
                      lightboxDetail?.downloadUrl ||
                      currentLightboxAttachment.thumbnailUrl ||
                      currentLightboxAttachment.blobUrl
                    }
                    alt={currentLightboxAttachment.caption || 'Session photo'}
                    className="max-h-[70vh] w-auto object-contain"
                    data-testid="lightbox-image"
                  />
                )}

                {/* Navigation arrows */}
                {lightboxIndex != null && lightboxIndex > 0 && (
                  <Button
                    variant="ghost"
                    size="icon"
                    className="absolute left-2 top-1/2 -translate-y-1/2 bg-black/50 hover:bg-black/70 text-white"
                    onClick={() => void navigateLightbox(-1)}
                    aria-label="Previous photo"
                    data-testid="lightbox-prev"
                  >
                    <ChevronLeft className="h-5 w-5" />
                  </Button>
                )}
                {lightboxIndex != null && lightboxIndex < filteredAttachments.length - 1 && (
                  <Button
                    variant="ghost"
                    size="icon"
                    className="absolute right-2 top-1/2 -translate-y-1/2 bg-black/50 hover:bg-black/70 text-white"
                    onClick={() => void navigateLightbox(1)}
                    aria-label="Next photo"
                    data-testid="lightbox-next"
                  >
                    <ChevronRight className="h-5 w-5" />
                  </Button>
                )}

                {/* Close button */}
                <Button
                  variant="ghost"
                  size="icon"
                  className="absolute top-2 right-2 bg-black/50 hover:bg-black/70 text-white"
                  onClick={closeLightbox}
                  aria-label="Close lightbox"
                  data-testid="lightbox-close"
                >
                  <X className="h-5 w-5" />
                </Button>
              </div>

              {/* Info bar */}
              <div className="flex items-center justify-between px-4 py-3 bg-card">
                <div className="space-y-0.5">
                  {currentLightboxAttachment.caption && (
                    <p className="text-sm">{currentLightboxAttachment.caption}</p>
                  )}
                  <div className="flex items-center gap-2 text-xs text-muted-foreground">
                    <span
                      className={cn(
                        'rounded-full px-2 py-0.5 text-[10px] font-medium',
                        TYPE_BADGE_COLORS[currentLightboxAttachment.attachmentType]
                      )}
                    >
                      {TYPE_LABELS[currentLightboxAttachment.attachmentType]}
                    </span>
                    {lightboxDetail?.playerDisplayName && (
                      <span>{lightboxDetail.playerDisplayName}</span>
                    )}
                    <span>
                      {lightboxIndex != null &&
                        `${lightboxIndex + 1} / ${filteredAttachments.length}`}
                    </span>
                  </div>
                </div>
                {lightboxDetail?.downloadUrl && (
                  <a
                    href={lightboxDetail.downloadUrl}
                    download
                    className="inline-flex items-center gap-1 text-xs text-muted-foreground hover:text-foreground"
                    data-testid="download-link"
                  >
                    <Download className="h-4 w-4" aria-hidden="true" />
                    Download
                  </a>
                )}
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>

      {/* Delete confirmation */}
      <AlertDialog
        open={deleteTarget != null}
        onOpenChange={open => {
          if (!open) setDeleteTarget(null);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete photo?</AlertDialogTitle>
            <AlertDialogDescription>
              This action cannot be undone. The photo will be permanently removed.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel data-testid="delete-cancel">Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleConfirmDelete}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              data-testid="delete-confirm"
            >
              Delete
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
