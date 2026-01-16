'use client';

/**
 * Games Table Row Component (Issue #2515)
 *
 * Single row in games table with dropdown actions menu.
 * Actions vary based on game status:
 * - Draft (0): [Configure] [Delete]
 * - PendingApproval (1): [Review] [Reject]
 * - Published (2): [Edit] [Archive]
 * - Archived (3): [Restore] [Delete]
 */

import { useState } from 'react';
import { MoreHorizontal, Edit, Send, CheckCircle, XCircle, Archive, Trash2, Eye } from 'lucide-react';
import { format } from 'date-fns';

import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import { TableCell, TableRow } from '@/components/ui/table';
import { Badge } from '@/components/ui/badge';

import { useApiClient } from '@/lib/api/context';
import { type SharedGameDetail } from '@/lib/api/schemas/shared-games.schemas';
import { toast } from 'sonner';

interface GamesTableRowProps {
  game: SharedGameDetail;
  onEdit?: (gameId: string) => void;
  onReviewApproval?: (gameId: string) => void;
  onRefresh: () => void;
}

export function GamesTableRow({ game, onEdit, onReviewApproval, onRefresh }: GamesTableRowProps) {
  const { sharedGames } = useApiClient();
  const [loading, setLoading] = useState(false);

  // Get status badge variant
  const getStatusBadge = (status: string) => {
    switch (status) {
      case 'Draft':
        return <Badge variant="secondary">Draft</Badge>;
      case 'PendingApproval':
        return <Badge variant="warning">Pending Approval</Badge>;
      case 'Published':
        return <Badge variant="success">Published</Badge>;
      case 'Archived':
        return <Badge variant="destructive">Archived</Badge>;
      default:
        return <Badge>{status}</Badge>;
    }
  };

  // Action handlers
  const handleConfigure = () => {
    if (onEdit) onEdit(game.id);
  };

  const handleReview = () => {
    if (onReviewApproval) onReviewApproval(game.id);
  };

  const handleSubmitForApproval = async () => {
    setLoading(true);
    try {
      await sharedGames.submitForApproval(game.id);
      toast.success('Game submitted for approval successfully');
      onRefresh();
    } catch (error) {
      console.error('Failed to submit game:', error);
      toast.error('Failed to submit game for approval');
    } finally {
      setLoading(false);
    }
  };

  const handleApprove = async () => {
    setLoading(true);
    try {
      await sharedGames.approvePublication(game.id);
      toast.success('Game approved and published successfully');
      onRefresh();
    } catch (error) {
      console.error('Failed to approve game:', error);
      toast.error('Failed to approve game');
    } finally {
      setLoading(false);
    }
  };

  const handleArchive = async () => {
    setLoading(true);
    try {
      await sharedGames.archive(game.id);
      toast.success('Game archived successfully');
      onRefresh();
    } catch (error) {
      console.error('Failed to archive game:', error);
      toast.error('Failed to archive game');
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async () => {
    if (!confirm('Are you sure you want to delete this game? This action cannot be undone.')) {
      return;
    }

    setLoading(true);
    try {
      await sharedGames.delete(game.id);
      toast.success('Game deleted successfully');
      onRefresh();
    } catch (error) {
      console.error('Failed to delete game:', error);
      toast.error('Failed to delete game');
    } finally {
      setLoading(false);
    }
  };

  return (
    <TableRow>
      <TableCell className="font-medium">{game.title}</TableCell>
      <TableCell>{game.bggId || 'N/A'}</TableCell>
      <TableCell>{getStatusBadge(game.status)}</TableCell>
      <TableCell>{format(new Date(game.lastModifiedAt), 'PPp')}</TableCell>
      <TableCell className="text-right">
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="sm" disabled={loading}>
              <MoreHorizontal className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuLabel>Actions</DropdownMenuLabel>
            <DropdownMenuSeparator />

            {/* Draft (0) actions */}
            {game.status === 'Draft' && (
              <>
                <DropdownMenuItem onClick={handleConfigure}>
                  <Edit className="h-4 w-4 mr-2" />
                  Configure
                </DropdownMenuItem>
                <DropdownMenuItem onClick={handleSubmitForApproval}>
                  <Send className="h-4 w-4 mr-2" />
                  Submit for Approval
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={handleDelete} className="text-destructive">
                  <Trash2 className="h-4 w-4 mr-2" />
                  Delete
                </DropdownMenuItem>
              </>
            )}

            {/* PendingApproval (1) actions */}
            {game.status === 'PendingApproval' && (
              <>
                <DropdownMenuItem onClick={handleReview}>
                  <Eye className="h-4 w-4 mr-2" />
                  Review
                </DropdownMenuItem>
                <DropdownMenuItem onClick={handleApprove}>
                  <CheckCircle className="h-4 w-4 mr-2" />
                  Approve & Publish
                </DropdownMenuItem>
                <DropdownMenuItem onClick={handleReview} className="text-destructive">
                  <XCircle className="h-4 w-4 mr-2" />
                  Reject
                </DropdownMenuItem>
              </>
            )}

            {/* Published (2) actions */}
            {game.status === 'Published' && (
              <>
                <DropdownMenuItem onClick={handleConfigure}>
                  <Edit className="h-4 w-4 mr-2" />
                  Edit
                </DropdownMenuItem>
                <DropdownMenuItem onClick={handleArchive}>
                  <Archive className="h-4 w-4 mr-2" />
                  Archive
                </DropdownMenuItem>
              </>
            )}

            {/* Archived (3) actions */}
            {game.status === 'Archived' && (
              <>
                <DropdownMenuItem onClick={handleConfigure}>
                  <Edit className="h-4 w-4 mr-2" />
                  Restore & Edit
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem onClick={handleDelete} className="text-destructive">
                  <Trash2 className="h-4 w-4 mr-2" />
                  Delete Permanently
                </DropdownMenuItem>
              </>
            )}
          </DropdownMenuContent>
        </DropdownMenu>
      </TableCell>
    </TableRow>
  );
}
