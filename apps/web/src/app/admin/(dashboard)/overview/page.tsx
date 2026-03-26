'use client';

import { useQuery } from '@tanstack/react-query';
import { AlertTriangleIcon, RefreshCwIcon } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { AccessRequestDto } from '@/lib/api/clients/accessRequestsClient';
import type { AdminOverviewStats } from '@/lib/api/schemas/admin.schemas';

import { KPIStatsRow } from './KPIStatsRow';
import { LibrarySummaryCard } from './LibrarySummaryCard';
import { PendingRequestsBanner } from './PendingRequestsBanner';
import { ProcessingQueueWidget } from './ProcessingQueueWidget';
import { QuickActionsGrid } from './QuickActionsGrid';
import { TechActionsBar } from './TechActionsBar';
import { UsersSummaryCard } from './UsersSummaryCard';

import type { AccessRequest } from './PendingRequestsBanner';

interface RecentGame {
  id: string;
  title: string;
  createdAt: string;
}

interface RecentUser {
  id: string;
  displayName: string | null;
  email: string;
  createdAt: string;
}

interface OverviewData {
  stats: AdminOverviewStats | null;
  pendingRequests: { items: AccessRequest[]; totalCount: number };
  recentGames: RecentGame[];
  recentUsers: RecentUser[];
  pendingInvitationCount: number;
}

function toAccessRequest(dto: AccessRequestDto): AccessRequest {
  return {
    id: dto.id,
    email: dto.email,
    status: dto.status,
    requestedAt: dto.requestedAt,
  };
}

/**
 * Lightweight fetch without Zod schema validation for overview summary data.
 * Avoids SchemaValidationError noise when backend DTOs have extra/missing fields.
 */
async function fetchJson<T>(path: string, fallback: T): Promise<T> {
  try {
    const res = await fetch(path, { credentials: 'include' });
    if (!res.ok) return fallback;
    return (await res.json()) as T;
  } catch {
    return fallback;
  }
}

async function fetchOverviewData(): Promise<OverviewData> {
  const [statsRes, pendingRes, gamesRes, usersRes, invitationsRes] = await Promise.all([
    api.admin.getOverviewStats().catch(() => null),
    api.accessRequests
      .getAccessRequests({ status: 'Pending', pageSize: 5 })
      .catch(() => ({ items: [] as AccessRequestDto[], totalCount: 0, page: 1, pageSize: 5 })),
    // Use lightweight fetch for summary data — avoids Zod schema mismatch errors
    fetchJson<{ items?: Record<string, unknown>[]; total?: number }>(
      '/api/v1/admin/shared-games?page=1&pageSize=5',
      { items: [], total: 0 }
    ),
    fetchJson<{ items?: Record<string, unknown>[]; total?: number }>(
      '/api/v1/admin/users?limit=5',
      { items: [], total: 0 }
    ),
    api.invitations
      .getInvitations({ status: 'Pending', pageSize: 5 })
      .catch(() => ({ items: [], totalCount: 0 })),
  ]);

  const invitationsResAny = invitationsRes as { items?: unknown[]; totalCount?: number } | null;

  return {
    stats: statsRes as AdminOverviewStats | null,
    pendingRequests: {
      items: (pendingRes.items ?? []).map(toAccessRequest),
      totalCount: pendingRes.totalCount ?? 0,
    },
    recentGames: ((gamesRes?.items ?? []) as Record<string, unknown>[]).map(g => ({
      id: String(g['id'] ?? ''),
      title: String(g['title'] ?? g['name'] ?? 'Untitled'),
      createdAt: String(g['createdAt'] ?? g['addedAt'] ?? new Date().toISOString()),
    })),
    recentUsers: ((usersRes?.items ?? []) as Record<string, unknown>[]).map(u => ({
      id: String(u['id'] ?? ''),
      displayName: u['displayName'] != null ? String(u['displayName']) : null,
      email: String(u['email'] ?? ''),
      createdAt: String(u['createdAt'] ?? new Date().toISOString()),
    })),
    pendingInvitationCount: invitationsResAny?.totalCount ?? 0,
  };
}

export default function OverviewPage() {
  const { data, isLoading, isError, refetch } = useQuery({
    queryKey: ['admin', 'overview'],
    queryFn: fetchOverviewData,
    staleTime: 30_000,
    refetchOnWindowFocus: true,
  });

  if (isLoading) {
    return (
      <div className="space-y-8">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Dashboard
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Centro operativo: richieste, libreria e gestione utenti
          </p>
        </div>
        <div className="grid grid-cols-3 gap-4">
          {[1, 2, 3].map(i => (
            <div key={i} className="h-24 rounded-2xl bg-muted/50 animate-pulse" />
          ))}
        </div>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div className="h-48 rounded-2xl bg-muted/50 animate-pulse" />
          <div className="h-48 rounded-2xl bg-muted/50 animate-pulse" />
        </div>
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Dashboard
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Centro operativo: richieste, libreria e gestione utenti
        </p>
      </div>

      {isError && (
        <div className="flex items-center gap-3 rounded-xl border border-amber-200 dark:border-amber-800/40 bg-amber-50 dark:bg-amber-950/20 px-4 py-3">
          <AlertTriangleIcon className="h-5 w-5 text-amber-600 dark:text-amber-400 shrink-0" />
          <p className="text-sm text-amber-800 dark:text-amber-200 flex-1">
            Errore nel caricamento dei dati. Alcuni dati potrebbero non essere aggiornati.
          </p>
          <Button variant="outline" size="sm" onClick={() => refetch()}>
            <RefreshCwIcon className="h-3.5 w-3.5 mr-1.5" />
            Riprova
          </Button>
        </div>
      )}

      <KPIStatsRow
        totalGames={data?.stats?.totalGames ?? 0}
        totalUsers={data?.stats?.totalUsers ?? 0}
        activeUsers={data?.stats?.activeUsers}
        pendingApprovals={data?.stats?.pendingApprovals ?? 0}
        recentSubmissions={data?.stats?.recentSubmissions}
      />

      <PendingRequestsBanner
        requests={data?.pendingRequests.items ?? []}
        totalCount={data?.pendingRequests.totalCount ?? 0}
      />

      <ProcessingQueueWidget />

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <LibrarySummaryCard
          totalGames={data?.stats?.totalGames ?? 0}
          recentGames={data?.recentGames ?? []}
        />
        <UsersSummaryCard
          totalUsers={data?.stats?.totalUsers ?? 0}
          activeUsers={data?.stats?.activeUsers ?? 0}
          pendingInvitations={data?.pendingInvitationCount ?? 0}
          recentUsers={data?.recentUsers ?? []}
        />
      </div>

      <div>
        <h2 className="font-quicksand text-lg font-semibold text-foreground mb-4">Azioni Rapide</h2>
        <QuickActionsGrid />
      </div>

      <TechActionsBar />
    </div>
  );
}
