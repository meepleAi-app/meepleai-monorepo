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
  /** Backend gap — wire when available */
  totalDocuments?: number;
  /** Backend gap — wire when available */
  queueDepth?: number;
  /** Recently submitted games (last 7 days) */
  recentSubmissions?: number;
}

// ============================================================================
// Component
// ============================================================================

export function KPIStatsRow({
  totalGames,
  totalUsers,
  activeUsers,
  pendingApprovals,
  totalDocuments,
  queueDepth,
  recentSubmissions,
}: KPIStatsRowProps) {
  const showDocuments = totalDocuments !== undefined;
  const showPending = pendingApprovals > 0;

  // Compute active-user ratio as a proxy trend indicator
  const activeRatio =
    activeUsers !== undefined && totalUsers > 0
      ? Math.round((activeUsers / totalUsers) * 100)
      : undefined;

  // Compute the number of visible cards to set responsive grid cols
  const cardCount = 2 + (showDocuments ? 1 : 0) + (showPending ? 1 : 0);
  const gridColsClass: Record<number, string> = {
    2: 'lg:grid-cols-2',
    3: 'lg:grid-cols-3',
    4: 'lg:grid-cols-4',
  };

  return (
    <div
      className={`grid grid-cols-2 ${gridColsClass[cardCount] ?? 'lg:grid-cols-4'} gap-4`}
      data-testid="kpi-stats-row"
    >
      {/* Card 1: Giochi — always visible */}
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

      {/* Card 2: Documenti — only when totalDocuments is provided */}
      {showDocuments && (
        <KPICard
          title="Documenti"
          value={totalDocuments}
          icon={<FileText className="h-5 w-5" />}
          subtitle="processati"
          badge={queueDepth && queueDepth > 0 ? `${queueDepth} in coda` : undefined}
          badgeVariant="warning"
          data-testid="kpi-documents"
        />
      )}

      {/* Card 3: Utenti — always visible */}
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

      {/* Card 4: Pendenti — only when pendingApprovals > 0 */}
      {showPending && (
        <KPICard
          title="Pendenti"
          value={pendingApprovals}
          icon={<UserPlus className="h-5 w-5" />}
          subtitle="richieste di accesso"
          data-testid="kpi-pending"
        />
      )}
    </div>
  );
}

export default KPIStatsRow;
