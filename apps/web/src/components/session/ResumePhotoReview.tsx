'use client';

/**
 * ResumePhotoReview - Full-screen overlay for reviewing session photos on resume.
 * Issue #5370 - ResumePhotoReview frontend component.
 * Epic #5358 - Session Photo Attachments (Phase 0).
 */

import { useState, useMemo, useCallback } from 'react';

import { Play, X, ChevronDown, ChevronRight, User, Clock, ImageIcon } from 'lucide-react';

import { Dialog, DialogContent, DialogTitle } from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';

import type { SessionAttachmentDto } from './PhotoUploadModal';

export interface ResumePhotoReviewProps {
  sessionId: string;
  /** All session photos to review. */
  attachments: SessionAttachmentDto[];
  /** Whether the current user is the session host. */
  isHost: boolean;
  /** Called when host confirms resume. */
  onAllReady: () => void;
  /** Called to go back / cancel. */
  onCancel: () => void;
  /** Timestamp of the pause (for display). */
  pausedAt?: string;
  /** Player name lookup. */
  playerNames?: Record<string, string>;
}

interface PlayerGroup {
  playerId: string;
  displayName: string;
  photos: SessionAttachmentDto[];
}

function formatRelativeTime(dateStr: string): string {
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMin = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMin / 60);
  const diffDays = Math.floor(diffHours / 24);

  if (diffDays > 0) return `${diffDays} day${diffDays > 1 ? 's' : ''} ago`;
  if (diffHours > 0) return `${diffHours} hour${diffHours > 1 ? 's' : ''} ago`;
  if (diffMin > 0) return `${diffMin} minute${diffMin > 1 ? 's' : ''} ago`;
  return 'just now';
}

export function ResumePhotoReview({
  attachments,
  isHost,
  onAllReady,
  onCancel,
  pausedAt,
  playerNames = {},
}: ResumePhotoReviewProps) {
  const [expandedPlayers, setExpandedPlayers] = useState<Set<string>>(new Set());
  const [lightboxUrl, setLightboxUrl] = useState<string | null>(null);

  const playerGroups = useMemo<PlayerGroup[]>(() => {
    const groupMap = new Map<string, SessionAttachmentDto[]>();
    for (const a of attachments) {
      const existing = groupMap.get(a.playerId);
      if (existing) {
        existing.push(a);
      } else {
        groupMap.set(a.playerId, [a]);
      }
    }
    return Array.from(groupMap.entries()).map(([playerId, photos]) => ({
      playerId,
      displayName: playerNames[playerId] || `Player ${playerId.slice(0, 6)}`,
      photos,
    }));
  }, [attachments, playerNames]);

  const togglePlayer = useCallback((playerId: string) => {
    setExpandedPlayers(prev => {
      const next = new Set(prev);
      if (next.has(playerId)) {
        next.delete(playerId);
      } else {
        next.add(playerId);
      }
      return next;
    });
  }, []);

  const expandAll = useCallback(() => {
    setExpandedPlayers(new Set(playerGroups.map(g => g.playerId)));
  }, [playerGroups]);

  return (
    <div
      className="fixed inset-0 z-50 flex flex-col bg-background/95 backdrop-blur-sm"
      data-testid="resume-photo-review"
    >
      {/* Header */}
      <div className="flex items-center justify-between border-b px-4 py-3">
        <div className="space-y-0.5">
          <h2 className="text-lg font-semibold" data-testid="review-title">
            Review Board State
          </h2>
          {pausedAt && (
            <p className="flex items-center gap-1 text-xs text-muted-foreground">
              <Clock className="h-3 w-3" aria-hidden="true" />
              Paused {formatRelativeTime(pausedAt)}
            </p>
          )}
        </div>
        <Button
          variant="ghost"
          size="icon"
          onClick={onCancel}
          aria-label="Cancel review"
          data-testid="cancel-button"
        >
          <X className="h-5 w-5" />
        </Button>
      </div>

      {/* Content */}
      <div className="flex-1 overflow-y-auto px-4 py-4 space-y-3">
        {attachments.length === 0 ? (
          <div
            className="flex flex-col items-center justify-center py-16 text-center"
            data-testid="no-photos"
          >
            <ImageIcon className="h-12 w-12 text-muted-foreground/50 mb-3" aria-hidden="true" />
            <p className="text-sm font-medium text-muted-foreground">No photos to review</p>
            <p className="text-xs text-muted-foreground/70 mt-1">
              No photos were captured before pausing.
            </p>
          </div>
        ) : (
          <>
            {/* Expand all */}
            <div className="flex justify-end">
              <Button
                variant="ghost"
                size="sm"
                onClick={expandAll}
                className="text-xs"
                data-testid="expand-all"
              >
                Expand all
              </Button>
            </div>

            {/* Player groups */}
            {playerGroups.map(group => {
              const isExpanded = expandedPlayers.has(group.playerId);
              return (
                <div
                  key={group.playerId}
                  className="rounded-lg border border-border/50 bg-card overflow-hidden"
                  data-testid={`player-group-${group.playerId}`}
                >
                  {/* Player header */}
                  <button
                    className="flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-muted/30 transition-colors"
                    onClick={() => togglePlayer(group.playerId)}
                    aria-expanded={isExpanded}
                    data-testid={`player-toggle-${group.playerId}`}
                  >
                    {isExpanded ? (
                      <ChevronDown className="h-4 w-4 text-muted-foreground shrink-0" />
                    ) : (
                      <ChevronRight className="h-4 w-4 text-muted-foreground shrink-0" />
                    )}
                    <div className="flex items-center gap-2 flex-1 min-w-0">
                      <div className="flex h-7 w-7 items-center justify-center rounded-full bg-amber-500/20 text-amber-600">
                        <User className="h-4 w-4" aria-hidden="true" />
                      </div>
                      <span className="text-sm font-medium truncate">{group.displayName}</span>
                    </div>
                    <span className="text-xs text-muted-foreground shrink-0">
                      {group.photos.length} photo{group.photos.length !== 1 ? 's' : ''}
                    </span>
                  </button>

                  {/* Photo grid (expanded) */}
                  {isExpanded && (
                    <div
                      className="grid grid-cols-3 gap-2 px-4 pb-3"
                      data-testid={`photo-grid-${group.playerId}`}
                    >
                      {group.photos.map(photo => (
                        <div
                          key={photo.id}
                          className="relative aspect-square overflow-hidden rounded-md bg-muted cursor-pointer hover:ring-2 hover:ring-amber-500/50"
                          onClick={() => setLightboxUrl(photo.thumbnailUrl || photo.blobUrl)}
                          role="button"
                          tabIndex={0}
                          aria-label={photo.caption || 'View photo'}
                          onKeyDown={e => {
                            if (e.key === 'Enter' || e.key === ' ') {
                              e.preventDefault();
                              setLightboxUrl(photo.thumbnailUrl || photo.blobUrl);
                            }
                          }}
                          data-testid={`review-photo-${photo.id}`}
                        >
                          {photo.thumbnailUrl ? (
                            // eslint-disable-next-line @next/next/no-img-element
                            <img
                              src={photo.thumbnailUrl}
                              alt={photo.caption || 'Session photo'}
                              className="h-full w-full object-cover"
                              loading="lazy"
                            />
                          ) : (
                            <div className="flex h-full w-full items-center justify-center">
                              <ImageIcon
                                className="h-6 w-6 text-muted-foreground"
                                aria-hidden="true"
                              />
                            </div>
                          )}
                          {photo.caption && (
                            <div className="absolute inset-x-0 bottom-0 bg-black/50 px-1.5 py-0.5">
                              <p className="text-[10px] text-white line-clamp-1">{photo.caption}</p>
                            </div>
                          )}
                        </div>
                      ))}
                    </div>
                  )}
                </div>
              );
            })}
          </>
        )}
      </div>

      {/* Footer */}
      <div className="border-t px-4 py-3 flex items-center justify-end gap-2">
        <Button variant="outline" onClick={onCancel} data-testid="back-button">
          Back
        </Button>
        {isHost && (
          <Button
            onClick={onAllReady}
            className="bg-emerald-600 hover:bg-emerald-700 text-white"
            disabled={attachments.length === 0}
            data-testid="resume-button"
          >
            <Play className="mr-1.5 h-4 w-4" aria-hidden="true" />
            Resume Game
          </Button>
        )}
      </div>

      {/* Simple lightbox */}
      <Dialog
        open={lightboxUrl != null}
        onOpenChange={open => {
          if (!open) setLightboxUrl(null);
        }}
      >
        <DialogContent
          className="max-w-3xl p-0 overflow-hidden"
          hideCloseButton
          data-testid="review-lightbox"
        >
          <DialogTitle className="sr-only">Photo viewer</DialogTitle>
          {lightboxUrl && (
            <div className="relative bg-black flex items-center justify-center min-h-[300px]">
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img
                src={lightboxUrl}
                alt="Session photo"
                className="max-h-[70vh] w-auto object-contain"
                data-testid="review-lightbox-image"
              />
              <Button
                variant="ghost"
                size="icon"
                className="absolute top-2 right-2 bg-black/50 hover:bg-black/70 text-white"
                onClick={() => setLightboxUrl(null)}
                aria-label="Close"
                data-testid="review-lightbox-close"
              >
                <X className="h-5 w-5" />
              </Button>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
