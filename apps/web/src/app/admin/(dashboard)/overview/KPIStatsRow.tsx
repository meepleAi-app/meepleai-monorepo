'use client';

import { Gamepad2, FileText, Users, UserPlus } from 'lucide-react';

import { KPICard } from '@/components/admin/KPICard';

// ============================================================================
// Types
// ============================================================================

export interface KPIStatsRowProps {
  totalGames: number;
  totalUsers: number;
  activeUsers?: number;
  pendingApprovals: number;
  /** Backend gap — wire when available. Falls back to 0 in the 4-up overview tile. */
  totalDocuments?: number;
  /** Backend gap — wire when available */
  queueDepth?: number;
  /** Recently submitted games (last 7 days) */
  recentSubmissions?: number;
}

// ============================================================================
// Component
// ============================================================================

/**
 * 4-up KPI row for the admin Overview pilot (SP5 F1, mockup A1).
 * Always renders the four cards (Giochi · Documenti · Utenti · Pendenti) in a
 * 2-col mobile / 4-col desktop grid, matching the mockup `.kpi-row` layout.
 * Empty/missing data falls back to 0 (e.g. totalDocuments while the backend
 * gap is open) so the layout stays consistent.
 */
export function KPIStatsRow({
  totalGames,
  totalUsers,
  activeUsers,
  pendingApprovals,
  totalDocuments,
  queueDepth,
  recentSubmissions,
}: KPIStatsRowProps) {
  // Active-user ratio as a trend indicator on the Users card.
  const activeRatio =
    activeUsers !== undefined && totalUsers > 0
      ? Math.round((activeUsers / totalUsers) * 100)
      : undefined;

  return (
    <div className="grid grid-cols-2 lg:grid-cols-4 gap-4" data-testid="kpi-stats-row">
      {/* Card 1: Giochi */}
      <KPICard
        title="Giochi"
        value={totalGames}
        icon={<Gamepad2 className="h-5 w-5" />}
        subtitle="nel catalogo condiviso"
        badge={
          recentSubmissions !== undefined && recentSubmissions > 0
            ? `+${recentSubmissions} recenti`
            : undefined
        }
        badgeVariant="success"
        data-testid="kpi-games"
      />

      {/* Card 2: Documenti — always visible (fallback 0 while backend gap is open) */}
      <KPICard
        title="Documenti"
        value={totalDocuments ?? 0}
        icon={<FileText className="h-5 w-5" />}
        subtitle="processati"
        badge={queueDepth && queueDepth > 0 ? `${queueDepth} in coda` : undefined}
        badgeVariant="warning"
        data-testid="kpi-documents"
      />

      {/* Card 3: Utenti */}
      <KPICard
        title="Utenti"
        value={totalUsers}
        icon={<Users className="h-5 w-5" />}
        subtitle="registrati"
        badge={activeUsers !== undefined && activeUsers > 0 ? `${activeUsers} attivi` : undefined}
        badgeVariant="success"
        trend={activeRatio}
        trendLabel="tasso attività"
        data-testid="kpi-users"
      />

      {/* Card 4: Pendenti — always visible (shows 0 when no approvals are queued) */}
      <KPICard
        title="Pendenti"
        value={pendingApprovals}
        icon={<UserPlus className="h-5 w-5" />}
        subtitle="richieste di accesso"
        data-testid="kpi-pending"
      />
    </div>
  );
}

export default KPIStatsRow;
