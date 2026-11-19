'use client';

/**
 * Admin share-request queue (#5076): every request, whatever its type, filtered by status, type and title or
 * contributor. A row opens the review page.
 */

import { useEffect, useState, type JSX } from 'react';

import { ChevronLeftIcon, ChevronRightIcon, Gamepad2Icon, InboxIcon } from 'lucide-react';
import Link from 'next/link';

import { Card, CardContent } from '@/components/ui/data-display/card';
import { SafeImg } from '@/components/ui/data-display/safe-img';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useAdminShareRequests } from '@/hooks/queries/useAdminShareRequests';
import type {
  ContributionType,
  ShareRequestStatus,
  SortDirection,
} from '@/lib/api/schemas/admin-share-requests.schemas';

import { CONTRIBUTION_TYPE_LABELS, STATUS_LABELS, formatDateTime } from './share-request-labels';
import { ShareRequestStatusBadge } from './ShareRequestStatusBadge';

const PAGE_SIZE = 20;
const ALL = 'all';

// The queue holds the active requests only: the backend lists these three statuses, and answers 400 to a filter on
// any other. A closed request opens from its link (the admin notification's, or the review's).
const STATUSES: ShareRequestStatus[] = ['Pending', 'InReview', 'ChangesRequested'];
const TYPES = Object.keys(CONTRIBUTION_TYPE_LABELS) as ContributionType[];

interface ShareRequestsListProps {
  /** Oldest first, as the admin notification's link asks (`?sort=oldest`). */
  oldestFirst?: boolean;
}

export function ShareRequestsList({ oldestFirst = false }: ShareRequestsListProps): JSX.Element {
  const [sortDirection, setSortDirection] = useState<SortDirection>(
    oldestFirst ? 'Ascending' : 'Descending'
  );
  const [status, setStatus] = useState<ShareRequestStatus | typeof ALL>(ALL);
  const [type, setType] = useState<ContributionType | typeof ALL>(ALL);
  const [searchInput, setSearchInput] = useState('');
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  useEffect(() => {
    const timer = setTimeout(() => {
      setSearch(searchInput.trim());
      setPage(1);
    }, 300);
    return () => clearTimeout(timer);
  }, [searchInput]);

  const { data, isLoading, isError, refetch } = useAdminShareRequests({
    page,
    pageSize: PAGE_SIZE,
    status: status === ALL ? undefined : status,
    contributionType: type === ALL ? undefined : type,
    searchTerm: search || undefined,
    sortBy: 'CreatedAt',
    sortDirection,
  });

  const total = data?.total ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <div className="space-y-4 text-foreground">
      <div className="flex flex-wrap items-center gap-3">
        <Input
          type="search"
          value={searchInput}
          onChange={e => setSearchInput(e.target.value)}
          placeholder="Cerca per gioco o note"
          aria-label="Cerca per gioco o note"
          className="w-full sm:w-72"
        />
        <Select
          value={status}
          onValueChange={value => {
            setStatus(value as ShareRequestStatus | typeof ALL);
            setPage(1);
          }}
        >
          <SelectTrigger className="w-full sm:w-48" aria-label="Stato">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Tutte le attive</SelectItem>
            {STATUSES.map(s => (
              <SelectItem key={s} value={s}>
                {STATUS_LABELS[s]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select
          value={type}
          onValueChange={value => {
            setType(value as ContributionType | typeof ALL);
            setPage(1);
          }}
        >
          <SelectTrigger className="w-full sm:w-56" aria-label="Tipo">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL}>Tutti i tipi</SelectItem>
            {TYPES.map(t => (
              <SelectItem key={t} value={t}>
                {CONTRIBUTION_TYPE_LABELS[t]}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
        <Select
          value={sortDirection}
          onValueChange={value => {
            setSortDirection(value as SortDirection);
            setPage(1);
          }}
        >
          <SelectTrigger className="w-full sm:w-44" aria-label="Ordine">
            <SelectValue />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value="Descending">Più recenti prima</SelectItem>
            <SelectItem value="Ascending">Più vecchie prima</SelectItem>
          </SelectContent>
        </Select>
      </div>

      <Card>
        <CardContent className="p-0">
          {isLoading ? (
            <div className="space-y-2 p-4" data-testid="share-requests-loading">
              {[0, 1, 2].map(i => (
                <Skeleton key={i} className="h-12 w-full" />
              ))}
            </div>
          ) : isError ? (
            <div className="flex flex-col items-center gap-3 p-8 text-center">
              <p className="text-sm text-muted-foreground">
                Non è stato possibile caricare le richieste.
              </p>
              <Button variant="outline" size="sm" onClick={() => refetch()}>
                Riprova
              </Button>
            </div>
          ) : !data || data.items.length === 0 ? (
            <div className="flex flex-col items-center gap-2 p-8 text-center">
              <InboxIcon className="h-8 w-8 text-muted-foreground" aria-hidden />
              <p className="text-sm text-muted-foreground">Nessuna richiesta con questi filtri.</p>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Gioco</TableHead>
                    <TableHead>Tipo</TableHead>
                    <TableHead>Contributore</TableHead>
                    <TableHead>Stato</TableHead>
                    <TableHead>Creata il</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {data.items.map(item => (
                    <TableRow key={item.id}>
                      <TableCell>
                        <Link
                          href={`/admin/share-requests/${item.id}`}
                          className="flex items-center gap-3 font-medium hover:underline"
                        >
                          <SafeImg
                            src={item.gameThumbnailUrl}
                            alt=""
                            className="h-10 w-10 shrink-0 rounded object-cover"
                            fallback={
                              <span className="flex h-10 w-10 shrink-0 items-center justify-center rounded bg-muted">
                                <Gamepad2Icon
                                  className="h-4 w-4 text-muted-foreground"
                                  aria-hidden
                                />
                              </span>
                            }
                          />
                          <span>{item.gameTitle}</span>
                        </Link>
                      </TableCell>
                      <TableCell>{CONTRIBUTION_TYPE_LABELS[item.contributionType]}</TableCell>
                      <TableCell>{item.userName}</TableCell>
                      <TableCell>
                        <div className="flex flex-col gap-1">
                          <ShareRequestStatusBadge status={item.status} />
                          {item.isInReview && item.reviewingAdminName && (
                            <span className="text-xs text-muted-foreground">
                              da {item.reviewingAdminName}
                            </span>
                          )}
                        </div>
                      </TableCell>
                      <TableCell className="whitespace-nowrap text-muted-foreground">
                        {formatDateTime(item.createdAt)}
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </div>
          )}
        </CardContent>
      </Card>

      {total > PAGE_SIZE && (
        <div className="flex items-center justify-between text-sm text-muted-foreground">
          <span>
            Pagina {page} di {pageCount} · {total} richieste
          </span>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => setPage(p => p - 1)}
              disabled={page <= 1}
              aria-label="Pagina precedente"
            >
              <ChevronLeftIcon className="h-4 w-4" />
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => setPage(p => p + 1)}
              disabled={page >= pageCount}
              aria-label="Pagina successiva"
            >
              <ChevronRightIcon className="h-4 w-4" />
            </Button>
          </div>
        </div>
      )}
    </div>
  );
}
