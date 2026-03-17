'use client';

import { Gamepad2, FileText, Users, UserPlus } from 'lucide-react';

import { KPICard } from '@/components/admin/KPICard';

// ============================================================================
// Types
// ============================================================================

export interface KPIStatsRowProps {
  totalGames: number;
  totalUsers: number;
  pendingApprovals: number;
  /** Backend gap — wire when available */
  totalDocuments?: number;
  /** Backend gap — wire when available */
  queueDepth?: number;
}

// ============================================================================
// Component
// ============================================================================

export function KPIStatsRow({
  totalGames,
  totalUsers,
  pendingApprovals,
  totalDocuments,
  queueDepth,
}: KPIStatsRowProps) {
  const showDocuments = totalDocuments !== undefined;
  const showPending = pendingApprovals > 0;

  // Compute the number of visible cards to set responsive grid cols
  // Tailwind requires complete class names (no interpolation) for static analysis
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
