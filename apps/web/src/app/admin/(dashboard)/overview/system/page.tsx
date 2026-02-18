'use client';

import { useEffect, useState } from 'react';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import type { InfrastructureDetails } from '@/lib/api/schemas/admin.schemas';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

/**
 * System Health Page
 * Route: /admin/overview/system
 *
 * Shows service status grid and API metrics from real infrastructure data.
 *
 * Issue: #4629
 */

export default function SystemHealthPage() {
  const [infraData, setInfraData] = useState<InfrastructureDetails | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    async function fetchInfrastructure() {
      try {
        const data = await adminClient.getInfrastructureDetails();
        if (data) {
          setInfraData(data);
        }
      } catch (error) {
        console.error('Failed to fetch infrastructure details:', error);
      } finally {
        setLoading(false);
      }
    }
    fetchInfrastructure();
  }, []);

  const services = infraData?.services || [];
  const overall = infraData?.overall;
  const metrics = infraData?.prometheusMetrics;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div>
        <h1 className="text-2xl font-quicksand font-bold text-foreground">
          System Health
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Monitor infrastructure services and API metrics
        </p>
      </div>

      {/* Overall Status */}
      {overall && (
        <div className="bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/60 p-6">
          <div className="flex items-center gap-4">
            <div
              className={`flex items-center gap-2 px-4 py-2 rounded-full text-sm font-semibold ${
                overall.state === 'Healthy'
                  ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400'
                  : overall.state === 'Degraded'
                    ? 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400'
                    : 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400'
              }`}
            >
              <div
                className={`w-2.5 h-2.5 rounded-full ${
                  overall.state === 'Healthy'
                    ? 'bg-green-500'
                    : overall.state === 'Degraded'
                      ? 'bg-amber-500'
                      : 'bg-red-500'
                }`}
              />
              {overall.state}
            </div>
            <span className="text-sm text-muted-foreground">
              {overall.healthyServices}/{overall.totalServices} services healthy
              {overall.degradedServices > 0 && ` · ${overall.degradedServices} degraded`}
              {overall.unhealthyServices > 0 && ` · ${overall.unhealthyServices} unhealthy`}
            </span>
          </div>
        </div>
      )}

      {/* Service Grid */}
      <div>
        <h2 className="text-lg font-quicksand font-semibold mb-4">Services</h2>
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {loading ? (
            <div className="col-span-full text-center py-8 text-muted-foreground">Loading...</div>
          ) : services.length === 0 ? (
            <div className="col-span-full text-center py-8 text-muted-foreground">No services found</div>
          ) : (
            services.map((service) => (
              <div
                key={service.serviceName}
                className="bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/60 p-6 hover:shadow-lg transition-shadow"
              >
                <div className="flex items-start justify-between mb-4">
                  <h3 className="font-quicksand font-bold text-lg">{service.serviceName}</h3>
                  <div
                    className={`flex items-center gap-2 px-3 py-1 rounded-full text-xs font-semibold ${
                      service.state === 'Healthy'
                        ? 'bg-green-100 text-green-700 dark:bg-green-900/30 dark:text-green-400'
                        : service.state === 'Degraded'
                          ? 'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400'
                          : 'bg-red-100 text-red-700 dark:bg-red-900/30 dark:text-red-400'
                    }`}
                  >
                    <div
                      className={`w-2 h-2 rounded-full ${
                        service.state === 'Healthy'
                          ? 'bg-green-500'
                          : service.state === 'Degraded'
                            ? 'bg-amber-500'
                            : 'bg-red-500'
                      }`}
                    />
                    {service.state}
                  </div>
                </div>
                <div className="space-y-2 text-sm">
                  <div className="flex justify-between">
                    <span className="text-muted-foreground">Latency</span>
                    <span className="font-semibold font-mono">{Math.round(service.responseTimeMs)}ms</span>
                  </div>
                  {service.errorMessage && (
                    <p className="text-xs text-red-600 dark:text-red-400 mt-2">{service.errorMessage}</p>
                  )}
                </div>
              </div>
            ))
          )}
        </div>
      </div>

      {/* API Metrics */}
      {metrics && (
        <div>
          <h2 className="text-lg font-quicksand font-semibold mb-4">API Metrics (24h)</h2>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div className="bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/60 p-6">
              <p className="text-sm text-muted-foreground">Requests</p>
              <p className="text-2xl font-quicksand font-bold mt-1">
                {metrics.apiRequestsLast24h.toLocaleString()}
              </p>
            </div>
            <div className="bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/60 p-6">
              <p className="text-sm text-muted-foreground">Avg Latency</p>
              <p className="text-2xl font-quicksand font-bold mt-1">
                {Math.round(metrics.avgLatencyMs)}ms
              </p>
            </div>
            <div className="bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/60 p-6">
              <p className="text-sm text-muted-foreground">Error Rate</p>
              <p className={`text-2xl font-quicksand font-bold mt-1 ${
                metrics.errorRate > 0.05
                  ? 'text-red-600 dark:text-red-400'
                  : metrics.errorRate > 0.01
                    ? 'text-amber-600 dark:text-amber-400'
                    : 'text-green-600 dark:text-green-400'
              }`}>
                {(metrics.errorRate * 100).toFixed(2)}%
              </p>
            </div>
            <div className="bg-white/70 dark:bg-zinc-900/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/60 p-6">
              <p className="text-sm text-muted-foreground">LLM Cost</p>
              <p className="text-2xl font-quicksand font-bold mt-1">
                ${metrics.llmCostLast24h.toFixed(2)}
              </p>
            </div>
          </div>
        </div>
      )}
    </div>
  );
}
