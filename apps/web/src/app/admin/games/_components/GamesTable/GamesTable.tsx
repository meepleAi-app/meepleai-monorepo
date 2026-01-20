'use client';

/**
 * Games Table Component (Issue #2515)
 *
 * Main table component for admin games management dashboard.
 * Displays games with filters (Status, Search, Sort) and pagination.
 * Supports approval workflow (Draft → PendingApproval → Published).
 *
 * Features:
 * - Server-side pagination (20 items/page)
 * - Status filter: All/Draft/PendingApproval/Published/Archived
 * - Search by name
 * - Sort by name, status, created date
 * - Dropdown actions per row (Edit, Submit, Approve, Reject, Archive, Delete)
 */

import { useState, useEffect } from 'react';

import { Plus, Search, AlertCircle } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { Spinner } from '@/components/loading/Spinner';
import { Card, CardHeader, CardTitle, CardDescription, CardContent } from '@/components/ui/data-display/card';
import {
  Table,
  TableBody,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { useApiClient } from '@/lib/api/context';
import { type SharedGameDetail } from '@/lib/api/schemas/shared-games.schemas';

import { GamesTablePagination } from './GamesTablePagination';
import { GamesTableRow } from './GamesTableRow';

type StatusFilter = 'all' | '0' | '1' | '2' | '3'; // All, Draft, PendingApproval, Published, Archived

interface GamesTableProps {
  onEdit?: (gameId: string) => void;
  onReviewApproval?: (gameId: string) => void;
}

export function GamesTable({ onEdit, onReviewApproval }: GamesTableProps) {
  const { sharedGames } = useApiClient();
  const router = useRouter();

  // State
  const [games, setGames] = useState<SharedGameDetail[]>([]);
  const [loading, setLoading] = useState(true);
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [total, setTotal] = useState(0);

  // Fetch games
  const fetchGames = async () => {
    setLoading(true);
    try {
      const params: {
        status?: number;
        page: number;
        pageSize: number;
      } = { page, pageSize };

      if (statusFilter !== 'all') {
        params.status = parseInt(statusFilter);
      }

      const result = await sharedGames.getAll(params);
      setGames(result.items as SharedGameDetail[]);
      setTotal(result.total);
    } catch (error) {
      console.error('Failed to fetch games:', error);
      setGames([]);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchGames();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, pageSize, statusFilter]);

  // Handlers
  const handleSearch = () => {
    setPage(1);
    fetchGames();
  };

  const handleCreate = () => {
    router.push('/admin/games/new');
  };

  const handleStatusChange = (status: StatusFilter) => {
    setStatusFilter(status);
    setPage(1);
  };

  // Pagination
  const totalPages = Math.ceil(total / pageSize);

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle>Admin Dashboard - Games Management</CardTitle>
            <CardDescription>
              Manage shared games: configure, submit for approval, publish. Approval workflow:
              Draft → Pending → Published.
            </CardDescription>
          </div>
          <Button onClick={handleCreate}>
            <Plus className="h-4 w-4 mr-2" />
            New Game
          </Button>
        </div>
      </CardHeader>

      <CardContent className="space-y-6">
        {/* Filters */}
        <div className="flex items-center gap-4 flex-wrap">
          {/* Search */}
          <div className="flex items-center gap-2 flex-1 min-w-[300px]">
            <div className="relative flex-1">
              <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Search by game name..."
                className="pl-10"
                value={searchTerm}
                onChange={(e) => setSearchTerm(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleSearch()}
              />
            </div>
            <Button variant="secondary" onClick={handleSearch}>
              Search
            </Button>
          </div>

          {/* Status Filter */}
          <div className="flex items-center gap-2">
            <Label htmlFor="status-filter" className="shrink-0">
              Status:
            </Label>
            <Select value={statusFilter} onValueChange={handleStatusChange}>
              <SelectTrigger id="status-filter" className="w-[180px]">
                <SelectValue placeholder="All" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All</SelectItem>
                <SelectItem value="0">Draft</SelectItem>
                <SelectItem value="1">Pending Approval</SelectItem>
                <SelectItem value="2">Published</SelectItem>
                <SelectItem value="3">Archived</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>

        {/* Stats */}
        <div className="flex items-center gap-2 text-sm text-muted-foreground">
          <span>
            {total} {total === 1 ? 'game' : 'games'} total
          </span>
          {statusFilter !== 'all' && (
            <span className="text-primary">
              (filtered for{' '}
              {statusFilter === '0'
                ? 'drafts'
                : statusFilter === '1'
                  ? 'pending approval'
                  : statusFilter === '2'
                    ? 'published'
                    : 'archived'}
              )
            </span>
          )}
        </div>

        {/* Loading State */}
        {loading && (
          <div className="flex justify-center py-12">
            <Spinner size="lg" />
          </div>
        )}

        {/* Empty State */}
        {!loading && games.length === 0 && (
          <Alert>
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>
              {searchTerm || statusFilter !== 'all'
                ? 'No games found with selected filters.'
                : 'No games in catalog. Click "New Game" to add one.'}
            </AlertDescription>
          </Alert>
        )}

        {/* Games Table */}
        {!loading && games.length > 0 && (
          <>
            <div className="border rounded-md">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-[40%]">Name</TableHead>
                    <TableHead>BGG ID</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Last Modified</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {games.map((game) => (
                    <GamesTableRow
                      key={game.id}
                      game={game}
                      onEdit={onEdit}
                      onReviewApproval={onReviewApproval}
                      onRefresh={fetchGames}
                    />
                  ))}
                </TableBody>
              </Table>
            </div>

            {/* Pagination */}
            <GamesTablePagination
              page={page}
              pageSize={pageSize}
              total={total}
              totalPages={totalPages}
              onPageChange={setPage}
            />
          </>
        )}
      </CardContent>
    </Card>
  );
}
