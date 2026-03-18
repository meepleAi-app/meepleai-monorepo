'use client';

/**
 * RAG Quality Dashboard Component
 *
 * Displays RAG pipeline health metrics: indexed documents, embedded chunks,
 * RAPTOR summaries, entity relations, top games, and enhancement flags.
 */

import { useQuery } from '@tanstack/react-query';
import {
  Database,
  Layers,
  Network,
  FileText,
  RefreshCw,
  CheckCircle2,
  XCircle,
} from 'lucide-react';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { fetchRagQualityReport } from '@/lib/api/rag-quality';

// ============================================================================
// Utilities
// ============================================================================

/** Format a number with comma thousands separator (locale-independent) */
function formatNumber(n: number): string {
  return n.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ',');
}

// ============================================================================
// Summary Card
// ============================================================================

interface SummaryCardProps {
  title: string;
  value: number | undefined;
  icon: React.ReactNode;
  isLoading: boolean;
}

function SummaryCard({ title, value, icon, isLoading }: SummaryCardProps) {
  return (
    <Card className="bg-white/70 backdrop-blur-md">
      <CardContent className="flex items-center gap-4 p-6">
        <div className="flex h-12 w-12 items-center justify-center rounded-lg bg-amber-50 text-amber-700">
          {icon}
        </div>
        <div>
          <p className="font-nunito text-sm text-muted-foreground">{title}</p>
          {isLoading ? (
            <Skeleton className="mt-1 h-7 w-20" />
          ) : (
            <p className="font-quicksand text-2xl font-bold tracking-tight">
              {value !== undefined ? formatNumber(value) : '—'}
            </p>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

// ============================================================================
// Boolean Status Icon
// ============================================================================

function EnabledIcon({ enabled }: { enabled: boolean }) {
  return enabled ? (
    <CheckCircle2 className="h-4 w-4 text-emerald-600" aria-label="Enabled" />
  ) : (
    <XCircle className="h-4 w-4 text-gray-300" aria-label="Disabled" />
  );
}

// ============================================================================
// Main Dashboard
// ============================================================================

export function RagQualityDashboard() {
  const {
    data: report,
    isLoading,
    error,
    refetch,
  } = useQuery({
    queryKey: ['ragQualityReport'],
    queryFn: fetchRagQualityReport,
    staleTime: 60_000,
  });

  return (
    <div className="space-y-6">
      {/* Page Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            RAG Quality Dashboard
          </h1>
          <p className="font-nunito text-sm text-muted-foreground mt-1">
            Monitor RAG pipeline health and enhancement status
          </p>
        </div>
        <Button variant="outline" size="icon" onClick={() => refetch()}>
          <RefreshCw className="h-4 w-4" />
        </Button>
      </div>

      {/* Error State */}
      {error && (
        <Card className="border-destructive">
          <CardContent className="py-4">
            <p className="text-destructive">Failed to load RAG quality report. Please try again.</p>
          </CardContent>
        </Card>
      )}

      {/* Summary Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <SummaryCard
          title="Total Indexed Documents"
          value={report?.totalIndexedDocuments}
          icon={<FileText className="h-6 w-6" />}
          isLoading={isLoading}
        />
        <SummaryCard
          title="Embedded Chunks"
          value={report?.totalEmbeddedChunks}
          icon={<Database className="h-6 w-6" />}
          isLoading={isLoading}
        />
        <SummaryCard
          title="RAPTOR Summaries"
          value={report?.totalRaptorSummaries}
          icon={<Layers className="h-6 w-6" />}
          isLoading={isLoading}
        />
        <SummaryCard
          title="Entity Relations"
          value={report?.totalEntityRelations}
          icon={<Network className="h-6 w-6" />}
          isLoading={isLoading}
        />
      </div>

      {/* Top Games by Chunk Count */}
      <Card className="bg-white/70 backdrop-blur-md">
        <CardHeader>
          <CardTitle className="font-quicksand text-sm font-semibold">
            Top Games by Chunk Count
          </CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="space-y-3">
              {[1, 2, 3].map(i => (
                <Skeleton key={i} className="h-10 w-full" />
              ))}
            </div>
          ) : report?.topGamesByChunkCount && report.topGamesByChunkCount.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full font-nunito text-sm">
                <thead>
                  <tr className="border-b text-left text-muted-foreground">
                    <th className="pb-2 font-medium">Game</th>
                    <th className="pb-2 font-medium text-right">Chunks</th>
                    <th className="pb-2 font-medium text-right">RAPTOR Nodes</th>
                    <th className="pb-2 font-medium text-right">Entity Relations</th>
                  </tr>
                </thead>
                <tbody>
                  {report.topGamesByChunkCount.map(game => (
                    <tr key={game.gameId} className="border-b last:border-0">
                      <td className="py-2.5">{game.gameTitle}</td>
                      <td className="py-2.5 text-right tabular-nums">
                        {formatNumber(game.chunkCount)}
                      </td>
                      <td className="py-2.5 text-right tabular-nums">
                        {formatNumber(game.raptorNodeCount)}
                      </td>
                      <td className="py-2.5 text-right tabular-nums">
                        {formatNumber(game.entityRelationCount)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="flex h-32 items-center justify-center text-muted-foreground">
              No game data available
            </div>
          )}
        </CardContent>
      </Card>

      {/* Enhancement Flags */}
      <Card className="bg-white/70 backdrop-blur-md">
        <CardHeader>
          <CardTitle className="font-quicksand text-sm font-semibold">
            Enhancement Feature Flags
          </CardTitle>
        </CardHeader>
        <CardContent>
          {isLoading ? (
            <div className="space-y-3">
              {[1, 2, 3].map(i => (
                <Skeleton key={i} className="h-10 w-full" />
              ))}
            </div>
          ) : report?.enhancementStatuses && report.enhancementStatuses.length > 0 ? (
            <div className="overflow-x-auto">
              <table className="w-full font-nunito text-sm">
                <thead>
                  <tr className="border-b text-left text-muted-foreground">
                    <th className="pb-2 font-medium">Enhancement</th>
                    <th className="pb-2 font-medium text-center">Free</th>
                    <th className="pb-2 font-medium text-center">Normal</th>
                    <th className="pb-2 font-medium text-center">Premium</th>
                  </tr>
                </thead>
                <tbody>
                  {report.enhancementStatuses.map(enhancement => (
                    <tr key={enhancement.featureFlagKey} className="border-b last:border-0">
                      <td className="py-2.5">
                        <div>
                          <span>{enhancement.name}</span>
                          <span className="ml-2 text-xs text-muted-foreground">
                            ({enhancement.featureFlagKey})
                          </span>
                        </div>
                      </td>
                      <td className="py-2.5 text-center">
                        <div className="flex justify-center">
                          <EnabledIcon enabled={enhancement.freeEnabled} />
                        </div>
                      </td>
                      <td className="py-2.5 text-center">
                        <div className="flex justify-center">
                          <EnabledIcon enabled={enhancement.normalEnabled} />
                        </div>
                      </td>
                      <td className="py-2.5 text-center">
                        <div className="flex justify-center">
                          <EnabledIcon enabled={enhancement.premiumEnabled} />
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <div className="flex h-32 items-center justify-center text-muted-foreground">
              No enhancement data available
            </div>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
