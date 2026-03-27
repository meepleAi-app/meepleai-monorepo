'use client';

/**
 * Token Balance Tab
 * Displays token balance summary, per-tier usage, and top consumers.
 */

import { useQuery } from '@tanstack/react-query';

import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';

// ─── Module-level client (stable reference) ──────────────────────────────────

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

// ─── Component ────────────────────────────────────────────────────────────────

export function TokenBalanceTab() {
  const { data: balance, isLoading: balanceLoading } = useQuery({
    queryKey: ['admin', 'tokens', 'balance'],
    queryFn: () => adminClient.getTokenBalance(),
    refetchInterval: 60_000,
  });

  const { data: tierUsage, isLoading: tierLoading } = useQuery({
    queryKey: ['admin', 'tokens', 'tiers'],
    queryFn: () => adminClient.getTokenTierUsage(),
    refetchInterval: 60_000,
  });

  const { data: topConsumers, isLoading: consumersLoading } = useQuery({
    queryKey: ['admin', 'tokens', 'top-consumers'],
    queryFn: () => adminClient.getTopConsumers(10),
    refetchInterval: 60_000,
  });

  return (
    <div className="space-y-6">
      {/* ── Balance Card ── */}
      <section aria-label="Token Balance">
        <h2 className="text-lg font-medium font-quicksand mb-4">Panoramica Saldo</h2>
        {balanceLoading ? (
          <div className="h-28 animate-pulse rounded-xl bg-muted" />
        ) : balance ? (
          <div className="rounded-xl border bg-card p-6 space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-sm text-muted-foreground">Current Balance</span>
              <span className="text-2xl font-semibold font-quicksand">
                {balance.currentBalance.toLocaleString()} {balance.currency}
              </span>
            </div>
            <div className="flex items-center justify-between">
              <span className="text-sm text-muted-foreground">Total Budget</span>
              <span className="text-base font-medium">
                {balance.totalBudget.toLocaleString()} {balance.currency}
              </span>
            </div>
            <div className="space-y-1">
              <div className="flex items-center justify-between text-sm">
                <span className="text-muted-foreground">Usage</span>
                <span>{balance.usagePercent.toFixed(1)}%</span>
              </div>
              <div className="h-2 rounded-full bg-muted overflow-hidden">
                <div
                  className="h-full rounded-full bg-primary transition-all"
                  style={{ width: `${Math.min(balance.usagePercent, 100)}%` }}
                />
              </div>
            </div>
            {balance.projectedDaysUntilDepletion !== null && (
              <p className="text-xs text-muted-foreground">
                Projected depletion in {balance.projectedDaysUntilDepletion} days
              </p>
            )}
          </div>
        ) : (
          <p className="text-sm text-muted-foreground">No balance data available.</p>
        )}
      </section>

      {/* ── Tier Usage + Top Consumers ── */}
      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        {/* Tier Usage */}
        <section aria-label="Tier Usage">
          <h2 className="text-lg font-medium font-quicksand mb-4">Utilizzo per Livello</h2>
          {tierLoading ? (
            <div className="h-48 animate-pulse rounded-xl bg-muted" />
          ) : tierUsage && tierUsage.tiers.length > 0 ? (
            <div className="rounded-xl border bg-card overflow-hidden">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b bg-muted/50">
                    <th className="px-4 py-3 text-left font-medium">Tier</th>
                    <th className="px-4 py-3 text-right font-medium">Used</th>
                    <th className="px-4 py-3 text-right font-medium">Limit/mo</th>
                    <th className="px-4 py-3 text-right font-medium">Users</th>
                    <th className="px-4 py-3 text-right font-medium">%</th>
                  </tr>
                </thead>
                <tbody>
                  {tierUsage.tiers.map(t => (
                    <tr key={t.tier} className="border-b last:border-0 hover:bg-muted/30">
                      <td className="px-4 py-3 capitalize">{t.tier}</td>
                      <td className="px-4 py-3 text-right">{t.currentUsage.toLocaleString()}</td>
                      <td className="px-4 py-3 text-right">{t.limitPerMonth.toLocaleString()}</td>
                      <td className="px-4 py-3 text-right">{t.userCount}</td>
                      <td className="px-4 py-3 text-right">{t.usagePercent.toFixed(1)}%</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">No tier usage data available.</p>
          )}
        </section>

        {/* Top Consumers */}
        <section aria-label="Top Consumers">
          <h2 className="text-lg font-medium font-quicksand mb-4">Maggiori Consumatori</h2>
          {consumersLoading ? (
            <div className="h-48 animate-pulse rounded-xl bg-muted" />
          ) : topConsumers && topConsumers.consumers.length > 0 ? (
            <div className="rounded-xl border bg-card overflow-hidden">
              <table className="w-full text-sm">
                <thead>
                  <tr className="border-b bg-muted/50">
                    <th className="px-4 py-3 text-left font-medium">User</th>
                    <th className="px-4 py-3 text-left font-medium">Tier</th>
                    <th className="px-4 py-3 text-right font-medium">Tokens</th>
                    <th className="px-4 py-3 text-right font-medium">% of Limit</th>
                  </tr>
                </thead>
                <tbody>
                  {topConsumers.consumers.map(c => (
                    <tr key={c.userId} className="border-b last:border-0 hover:bg-muted/30">
                      <td className="px-4 py-3">
                        <div className="font-medium truncate max-w-[12rem]">{c.displayName}</div>
                        <div className="text-xs text-muted-foreground truncate max-w-[12rem]">
                          {c.email}
                        </div>
                      </td>
                      <td className="px-4 py-3 capitalize">{c.tier}</td>
                      <td className="px-4 py-3 text-right">{c.tokensUsed.toLocaleString()}</td>
                      <td className="px-4 py-3 text-right">{c.percentOfTierLimit.toFixed(1)}%</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p className="text-sm text-muted-foreground">No consumer data available.</p>
          )}
        </section>
      </div>
    </div>
  );
}
