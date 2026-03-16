'use client';

import { useEffect, useState } from 'react';

import { createApiClient } from '@/lib/api';
import type { AccessRequestDto } from '@/lib/api/clients/accessRequestsClient';
import type { AdminOverviewStats } from '@/lib/api/schemas/admin.schemas';

import { KPIStatsRow } from './KPIStatsRow';
import { LibrarySummaryCard } from './LibrarySummaryCard';
import { PendingRequestsBanner } from './PendingRequestsBanner';
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

export default function OverviewPage() {
  const [data, setData] = useState<OverviewData | null>(null);

  useEffect(() => {
    const api = createApiClient();

    Promise.all([
      api.admin.getOverviewStats().catch(() => null),
      api.accessRequests
        .getAccessRequests({ status: 'Pending', pageSize: 5 })
        .catch(() => ({ items: [] as AccessRequestDto[], totalCount: 0, page: 1, pageSize: 5 })),
      api.sharedGames.getAll({ pageSize: 5, page: 1 }).catch(() => ({ items: [], total: 0 })),
      api.admin.getAllUsers({ limit: 5 }).catch(() => []),
      api.invitations
        .getInvitations({ status: 'Pending', pageSize: 5 })
        .catch(() => ({ items: [], totalCount: 0 })),
    ]).then(([statsRes, pendingRes, gamesRes, usersRes, invitationsRes]) => {
      const gamesResAny = gamesRes as { items?: unknown[] } | null;
      const invitationsResAny = invitationsRes as { items?: unknown[]; totalCount?: number } | null;

      setData({
        stats: statsRes as AdminOverviewStats | null,
        pendingRequests: {
          items: (pendingRes.items ?? []).map(toAccessRequest),
          totalCount: pendingRes.totalCount ?? 0,
        },
        recentGames: ((gamesResAny?.items ?? []) as Record<string, unknown>[]).map(g => ({
          id: String(g['id'] ?? ''),
          title: String(g['title'] ?? g['name'] ?? 'Untitled'),
          createdAt: String(g['createdAt'] ?? g['addedAt'] ?? new Date().toISOString()),
        })),
        recentUsers: (Array.isArray(usersRes)
          ? (usersRes as Record<string, unknown>[])
          : (((usersRes as { items?: unknown[] } | null)?.items ?? []) as Record<string, unknown>[])
        ).map(u => ({
          id: String(u['id'] ?? ''),
          displayName: u['displayName'] != null ? String(u['displayName']) : null,
          email: String(u['email'] ?? ''),
          createdAt: String(u['createdAt'] ?? new Date().toISOString()),
        })),
        pendingInvitationCount: invitationsResAny?.totalCount ?? 0,
      });
    });
  }, []);

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

      <KPIStatsRow
        totalGames={data?.stats?.totalGames ?? 0}
        totalUsers={data?.stats?.totalUsers ?? 0}
        pendingApprovals={data?.stats?.pendingApprovals ?? 0}
      />

      <PendingRequestsBanner
        requests={data?.pendingRequests.items ?? []}
        totalCount={data?.pendingRequests.totalCount ?? 0}
      />

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
