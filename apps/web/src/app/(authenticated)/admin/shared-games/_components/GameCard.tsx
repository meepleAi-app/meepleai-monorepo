/**
 * GameCard Component - Issue #3534
 *
 * Card component for displaying a shared game in the grid layout.
 * Shows thumbnail, title, status badge, player count, and action buttons.
 */

'use client';

import { Archive, Edit, Eye, FileText, MoreHorizontal, Trash2, Users, Clock } from 'lucide-react';
import Image from 'next/image';

import { GameStatusBadge } from '@/components/admin';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import { type SharedGame } from '@/lib/api';

export interface GameCardProps {
  game: SharedGame;
  isAdmin?: boolean;
  onEdit?: (game: SharedGame) => void;
  onPreview?: (game: SharedGame) => void;
  onPublish?: (game: SharedGame) => void;
  onArchive?: (game: SharedGame) => void;
  onDelete?: (game: SharedGame) => void;
}

export function GameCard({
  game,
  isAdmin = false,
  onEdit,
  onPreview,
  onPublish,
  onArchive,
  onDelete,
}: GameCardProps) {
  const hasImage = !!game.thumbnailUrl || !!game.imageUrl;
  const imageUrl = game.thumbnailUrl || game.imageUrl;

  return (
    <Card className="group overflow-hidden transition-all hover:shadow-lg">
      {/* Thumbnail */}
      <div className="relative aspect-[4/3] overflow-hidden bg-muted">
        {hasImage && imageUrl ? (
          <Image
            src={imageUrl}
            alt={game.title}
            fill
            className="object-cover transition-transform group-hover:scale-105"
            sizes="(max-width: 640px) 100vw, (max-width: 1024px) 50vw, 25vw"
          />
        ) : (
          <div className="flex h-full items-center justify-center">
            <FileText className="h-12 w-12 text-muted-foreground/50" />
          </div>
        )}

        {/* Status Badge Overlay */}
        <div className="absolute left-2 top-2">
          <GameStatusBadge status={game.status} size="sm" />
        </div>

        {/* Actions Dropdown */}
        <div className="absolute right-2 top-2 opacity-0 transition-opacity group-hover:opacity-100">
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="secondary" size="sm" className="h-8 w-8 p-0 shadow-md">
                <MoreHorizontal className="h-4 w-4" />
                <span className="sr-only">Open menu</span>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              {onPreview && (
                <DropdownMenuItem onClick={() => onPreview(game)}>
                  <Eye className="mr-2 h-4 w-4" />
                  Preview
                </DropdownMenuItem>
              )}
              {onEdit && (
                <DropdownMenuItem onClick={() => onEdit(game)}>
                  <Edit className="mr-2 h-4 w-4" />
                  Edit
                </DropdownMenuItem>
              )}
              {onPublish && game.status === 'Draft' && (
                <DropdownMenuItem onClick={() => onPublish(game)}>
                  <Eye className="mr-2 h-4 w-4" />
                  Publish
                </DropdownMenuItem>
              )}
              {onArchive && game.status === 'Published' && (
                <DropdownMenuItem onClick={() => onArchive(game)}>
                  <Archive className="mr-2 h-4 w-4" />
                  Archive
                </DropdownMenuItem>
              )}
              {onDelete && (
                <>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem
                    className="text-destructive focus:text-destructive"
                    onClick={() => onDelete(game)}
                  >
                    <Trash2 className="mr-2 h-4 w-4" />
                    {isAdmin ? 'Delete' : 'Request Delete'}
                  </DropdownMenuItem>
                </>
              )}
            </DropdownMenuContent>
          </DropdownMenu>
        </div>
      </div>

      {/* Card Content */}
      <CardContent className="p-4">
        {/* Title */}
        <h3 className="mb-1 line-clamp-2 font-semibold leading-tight">{game.title}</h3>

        {/* Year */}
        <p className="mb-3 text-sm text-muted-foreground">
          {game.yearPublished || 'Unknown Year'}
        </p>

        {/* Metadata Row */}
        <div className="flex items-center gap-4 text-xs text-muted-foreground">
          {/* Players */}
          <div className="flex items-center gap-1">
            <Users className="h-3.5 w-3.5" />
            <span>
              {game.minPlayers === game.maxPlayers
                ? game.minPlayers
                : `${game.minPlayers}-${game.maxPlayers}`}
            </span>
          </div>

          {/* Playing Time */}
          <div className="flex items-center gap-1">
            <Clock className="h-3.5 w-3.5" />
            <span>{game.playingTimeMinutes} min</span>
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

/**
 * Loading skeleton for GameCard
 */
export function GameCardSkeleton() {
  return (
    <Card className="overflow-hidden">
      <div className="aspect-[4/3] bg-muted">
        <Skeleton className="h-full w-full" />
      </div>
      <CardContent className="p-4">
        <Skeleton className="mb-2 h-5 w-3/4" />
        <Skeleton className="mb-3 h-4 w-1/4" />
        <div className="flex gap-4">
          <Skeleton className="h-4 w-12" />
          <Skeleton className="h-4 w-16" />
        </div>
      </CardContent>
    </Card>
  );
}
